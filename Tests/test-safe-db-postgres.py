"""Isolated PostgreSQL16 RLS/API regression. No ports, no external DB, temp container removed.
Small generated fixtures only inside this container; never the Supabase demo dataset.
"""
import argparse,hashlib,json,subprocess,time,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--image',default='postgres:16-alpine');a=p.parse_args();a.output.mkdir(parents=True,exist_ok=False)
cid=subprocess.check_output(['docker','run','--rm','-d','--network','none','--tmpfs','/var/lib/postgresql/data','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_DB=qa',a.image],text=True).strip();n=0

def sql(text,ok=True):
 r=subprocess.run(['docker','exec','-i',cid,'psql','-X','-q','-U','postgres','-d','qa','-v','ON_ERROR_STOP=1','-tAc',text],capture_output=True,text=True)
 if ok and r.returncode:raise RuntimeError(r.stderr)
 if not ok and not r.returncode:raise AssertionError('Expected permission/argument rejection')
 return r.stdout.strip() if ok else r.stderr

def check(value,label):
 global n
 if not value:raise AssertionError(label)
 n+=1;print('PASS',label,flush=True)
def lit(value):return "'"+str(value).replace("'","''")+"'"
def uid(label):return str(uuid.uuid5(uuid.NAMESPACE_URL,'isolated-safe-db-fixture/'+label))
def owner(label):return hashlib.sha256(('fixture/'+label).encode()).hexdigest()
def fixture(label,group,source,confidence=.8,pattern='cleave',level=0,battle_source=None,owner_label=None):
 o=owner(owner_label or label);profile=uid(label+'profile');assessment=uid(label+'assessment');battle=uid(label+'battle')
 raw={} if source=='real' else {'_demo':{'cohort':'demo_exhibit_20261121' if source=='demo' else 'other_fixture'}}
 br=raw if battle_source is None else {} if battle_source=='real' else {'_demo':{'cohort':'other_fixture'}}
 q=f"insert into public.proficiency_assessments values({lit(o)},{lit(profile)},{lit(assessment)},true,{lit(group)},true,false,true,.7,{confidence},60,80,2,1,2,1,2,0,'2026-11-21T00:00:00Z',{lit(json.dumps(raw))}::jsonb,now());"
 q+=f"insert into public.proficiency_battles(owner_key,profile_id,event_id,assessment_event_id,boss,seconds,raw_json) values({lit(o)},{lit(profile)},{lit(battle)},{lit(assessment)},'arakus',30,{lit(json.dumps(br))}::jsonb);"
 q+=f"insert into public.proficiency_pattern_samples values({lit(o)},{lit(profile)},{lit(battle)},{lit(pattern)},{level},10,2,5,3);"
 return q
try:
 for _ in range(100):
  logs=subprocess.run(['docker','logs',cid],capture_output=True,text=True)
  ready=subprocess.run(['docker','exec',cid,'psql','-X','-q','-U','postgres','-d','qa','-tAc','select 1'],capture_output=True)
  if 'PostgreSQL init process complete' in logs.stdout and ready.returncode==0:break
  time.sleep(.2)
 else:raise RuntimeError('isolated PostgreSQL startup timeout')
 sql('create role anon nologin;create role authenticated nologin;')
 sql((ROOT/'Docs/DB/migrations/20261118_proficiency.sql').read_text(encoding='utf-8'))
 for group in ['novice','normal','expert']:
  for i in range(3):sql(fixture('demo-'+group+str(i),group,'demo'))
 for group,count in [('novice',2),('normal',3),('expert',3)]:
  for i in range(count):sql(fixture('real-'+group+str(i),group,'real'))
 # Extra profiles of one owner cannot satisfy k=3. Mismatched links/levels/confidence/labels excluded.
 for i in range(3):sql(fixture('shared-'+str(i),'expert','other_demo',owner_label='same-owner'))
 sql(fixture('lowconfidence','normal','demo',confidence=.4,pattern='shield'))
 sql(fixture('nonneutral','expert','demo',pattern='kick',level=1))
 sql(fixture('mismatch','normal','demo',battle_source='real'))
 sql(fixture('untrusted-label','novice','demo',pattern='DO_NOT_EXPOSE_PERSONAL_LABEL'))
 names=['proficiency_assessments','proficiency_battles','proficiency_pattern_samples']
 snapshot=lambda:sql("select jsonb_build_array("+','.join("(select md5(string_agg(t::text,',' order by t::text)) from public."+t+' t)' for t in names)+");")
 before=snapshot();view_definition=sql("select pg_get_viewdef('public.pattern_difficulty_by_proficiency'::regclass,true)");write_function=sql("select pg_get_functiondef('public.submit_proficiency(text,text,uuid,uuid,uuid,jsonb)'::regprocedure)")
 migration=(ROOT/'Docs/DB/migrations/20261121_exhibit_summary.sql').read_text(encoding='utf-8');sql(migration)
 check(snapshot()==before,'migration preserves every existing raw row')
 check(sql("select pg_get_viewdef('public.pattern_difficulty_by_proficiency'::regclass,true)")==view_definition,'existing mixed aggregate view definition unchanged')
 check(sql("select pg_get_functiondef('public.submit_proficiency(text,text,uuid,uuid,uuid,jsonb)'::regprocedure)")==write_function,'existing protected INSERT RPC body unchanged')
 check(sql("select bool_and(relrowsecurity) from pg_class where oid in ('public.proficiency_assessments'::regclass,'public.proficiency_battles'::regclass,'public.proficiency_pattern_samples'::regclass)")=='t','all raw-table RLS stays ON')
 for role in ['anon','authenticated']:
  for table in names:
   check('permission denied' in sql('set session authorization '+role+';select * from public.'+table,ok=False),'raw SELECT denied '+role+'/'+table)
  check('permission denied' in sql('set session authorization '+role+';insert into public.proficiency_battles(owner_key) values(\'x\')',ok=False),'raw INSERT denied '+role)
  check('permission denied' in sql('set session authorization '+role+';set role dda_exhibit_reader',ok=False),'caller cannot assume private reader role '+role)
 result=json.loads(sql('set session authorization anon;select public.exhibit_dda_summary_v1()'))
 check(result['schemaVersion']==1 and len(result['groups'])==9,'public RPC returns fixed nine cohort/group rows')
 check(result['counts']['demo']['players']==13 and result['counts']['demo']['battles']==12 and result['counts']['demo']['samples']==11,'cohort counts reflect actual isolated rows, joins and fixed vocabulary')
 check(result['counts']['real'] is None and result['counts']['other_demo'] is None,'source totals withheld when a component group is suppressed')
 small=next(g for g in result['groups'] if g['source']=='real' and g['group']=='novice');check(not small['released'] and small['players'] is None and small['score'] is None,'small real group exposes neither exact count nor averages')
 shared=next(g for g in result['groups'] if g['source']=='other_demo' and g['group']=='expert');check(not shared['released'],'three profiles of one owner never meet k=3')
 check(all(p['players']>=3 and p['opportunities']>=20 for p in result['patterns']),'all released patterns satisfy owner and opportunity thresholds')
 check(not any(p['pattern'] in ['shield','kick'] for p in result['patterns']),'low-confidence and non-neutral samples excluded from pattern recommendations')
 pattern=next(p for p in result['patterns'] if p['source']=='demo' and p['group']=='normal');check(pattern['players']==3 and pattern['opportunities']==30 and pattern['successRate']==.8,'exact same-cohort three-key joins and weighted rates')
 mixed=json.loads(sql("select coalesce(jsonb_agg(to_jsonb(v) order by boss,pattern,group_name),'[]'::jsonb) from public.pattern_difficulty_by_proficiency v"))
 (a.output/'ISOLATED_TEST_FIXTURE-mixed.json').write_text(json.dumps(mixed,indent=2),encoding='utf-8')
 text=json.dumps(result);check(all(key not in text for key in ['owner_key','profile_id','event_id','raw_json','DO_NOT_EXPOSE_PERSONAL_LABEL']),'no identity/raw JSON/untrusted labels in output')
 check(json.loads(sql('set session authorization authenticated;select public.exhibit_dda_summary_v1()'))==result,'authenticated and anon receive same safe snapshot')
 check('does not exist' in sql("set session authorization anon;select public.exhibit_dda_summary_v1('real')",ok=False),'caller-selected filters rejected: zero-argument signature only')
 check(sql("select not rolcanlogin and not rolsuper and not rolbypassrls and not rolcreaterole and not rolcreatedb and not rolinherit from pg_roles where rolname='dda_exhibit_reader'")=='t','function owner has no login/superuser/bypass/inherited/admin powers')
 check(sql("select has_table_privilege('dda_exhibit_reader','public.proficiency_assessments','SELECT') and not has_table_privilege('dda_exhibit_reader','public.proficiency_assessments','INSERT') and not has_table_privilege('dda_exhibit_reader','public.proficiency_assessments','UPDATE') and not has_table_privilege('dda_exhibit_reader','public.proficiency_assessments','DELETE')")=='t','reader grants SELECT only, not table writes')
 check(sql("select not has_schema_privilege('dda_exhibit_reader','public','CREATE') and not has_function_privilege('dda_exhibit_reader','public.submit_proficiency(text,text,uuid,uuid,uuid,jsonb)','EXECUTE')")=='t','reader cannot create public objects or call protected write RPC')
 shadow=json.loads(sql('set session authorization anon;create temp table proficiency_assessments(secret text);set search_path=pg_temp,public;select public.exhibit_dda_summary_v1()'))
 check(shadow==result,'temporary shadow table/search_path cannot change definer source')
 # Existing write API still succeeds and remains idempotent in this isolated DB.
 assessment={'schemaVersion':1,'ultimateExecutionSuccess':True,'isFirstAssessment':True,'group':'normal','completed':True,'skipped':False,'valid':True,'score':.7,'confidence':.8,'activeDurationSeconds':60,'elapsedDurationSeconds':80,'parryAttempts':2,'parrySuccesses':1,'dodgeAttempts':2,'dodgeSuccesses':1,'damageHits':1,'retryCount':0,'completedAtUtc':'2026-11-21T00:00:00Z'}
 call="select public.submit_proficiency("+','.join([lit('a'*64),lit('assessment'),lit(uid('new-profile')),lit(uid('new-assessment')),'null',lit(json.dumps({'assessment':assessment}))+'::jsonb'])+')'
 check(sql('set session authorization anon;'+call)=='t','existing capability-bound assessment INSERT succeeds')
 rows=snapshot();check(sql('set session authorization anon;'+call)=='t' and snapshot()==rows,'existing INSERT retry remains idempotent')
 sql('grant insert on public.proficiency_assessments to dda_exhibit_reader')
 check('unexpected raw write/owner privileges' in sql(migration,ok=False),'unsafe pre-existing reader privileges abort deployment')
 check(snapshot()==rows,'failed unsafe deployment leaves all data intact')
 sql('revoke insert on public.proficiency_assessments from dda_exhibit_reader')
 sql(migration);check(snapshot()==rows,'migration rerun is idempotent and preserves rows')
 (a.output/'ISOLATED_TEST_FIXTURE-summary.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 (a.output/'result.json').write_text(json.dumps({'passed':n,'postgresImage':a.image,'network':'none','productionDbAccess':False,'rawWrites':'only throwaway container fixtures and original-RPC regression','migrationSha256':hashlib.sha256(migration.encode()).hexdigest()},indent=2),encoding='utf-8')
 print('SAFE_DB_POSTGRES_PASS',n,flush=True)
finally:
 subprocess.run(['docker','stop','-t','2',cid],capture_output=True)
