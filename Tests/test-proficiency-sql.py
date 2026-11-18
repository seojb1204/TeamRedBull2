"""Apply and test ONLY an explicitly named network-none disposable PostgreSQL container.
No connection strings, host sockets, production configuration, or user data are read.
"""
import argparse,json,subprocess,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--container',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
r=Path(__file__).resolve().parents[1]
info=json.loads(subprocess.check_output(['docker','inspect',a.container]))[0]
assert info['HostConfig']['NetworkMode']=='none' and all(m['Type']=='volume' and m['Destination']=='/var/lib/postgresql/data' and len(m['Name'])==64 for m in info['Mounts']) and not info['HostConfig']['PortBindings'],'Isolated empty container required'
database="qa_"+uuid.uuid4().hex
subprocess.run(["docker","exec",a.container,"createdb","-U","postgres",database],check=True)
def sql(s):
 x=subprocess.run(['docker','exec','-i',a.container,'psql','-X','-v','ON_ERROR_STOP=1','-U','postgres','-d',database],input=s,text=True,capture_output=True)
 if x.returncode:raise RuntimeError(x.stderr)
 return x.stdout
sql("do $$ begin if not exists(select 1 from pg_roles where rolname='anon') then create role anon;end if;if not exists(select 1 from pg_roles where rolname='authenticated') then create role authenticated;end if;end $$;")
migration=(r/'Docs/DB/migrations/20261118_proficiency.sql').read_text();sql(migration);sql(migration) # idempotent migration
checks=[]
def check(name,query):
 out=sql(query)
 checks.append(name);print('PASS',name,flush=True)
def uid():return str(uuid.uuid4())
def payload(group='expert',first=True,skip=False):return {'assessment':dict(schemaVersion=1,isFirstAssessment=first,group='normal' if skip else group,completed=not skip,skipped=skip,valid=not skip,score=0 if skip else .95,confidence=0 if skip else .75,activeDurationSeconds=60,elapsedDurationSeconds=80,parryAttempts=3,parrySuccesses=3,dodgeAttempts=3,dodgeSuccesses=3,damageHits=0,retryCount=0,ultimateExecutionSuccess=not skip,completedAtUtc='2026-11-18T01:00:00Z')}
def call(secret,kind,profile,event,data,link=None):return "select public.submit_proficiency('%s','%s','%s','%s',%s,$data$%s$data$::jsonb);"%(secret,kind,profile,event,"null" if link is None else "'%s'"%link,json.dumps(data))
def expect_error(name,query,code):
 # PL/pgSQL nested block ensures rejected changes roll back; failure if call unexpectedly succeeds.
 check(name,"do $test$ begin begin "+query+" raise exception 'Unexpected acceptance'; exception when sqlstate '"+code+"' then null; end; end $test$;")
profiles=[]
for i in range(3):
 secret=('%064x'%(i+1));profile,event,battle=uid(),uid(),uid();assessment=payload();profiles.append((secret,profile,event,battle,assessment))
 query=call(secret,'assessment',profile,event,assessment)
 check('anon capability-bound first assessment '+str(i), 'set role anon;'+query+'reset role;')
 check('idempotent identical assessment '+str(i),query)
 data={'battle':{'battleId':battle,'boss':'arakus','seconds':60,'abandoned':False},'samples':[{'id':'cleave','level':0,'opportunities':10,'parries':10,'dodges':0,'hits':0}]}
 check('linked neutral battle '+str(i),call(secret,'battle',profile,battle,data,event))
 count=0 if i<2 else 1
 check('cohort suppression / sufficient owners '+str(i),"do $$ begin if (select count(*) from public.pattern_difficulty_by_proficiency)<>%d then raise exception 'cohort count';end if;end $$;"%count)
secret,profile,event,battle,assessment=profiles[0]
check('aggregate counts rates and recommendation',"set role anon;do $$ begin if not exists(select 1 from public.pattern_difficulty_by_proficiency where players=3 and opportunities=30 and success_rate=1 and suggested_level=1 and group_name='expert') then raise exception 'wrong aggregate';end if;end $$;reset role;")
changed=payload();changed['assessment']['score']=.1
expect_error('immutable assessment update rejected',call(secret,'assessment',profile,event,changed).replace('select public.','perform public.'),'23505')
expect_error('second initial assessment rejected',call(secret,'assessment',profile,uid(),assessment).replace('select public.','perform public.'),'23505')
check('reassessment separately stored',call(secret,'assessment',profile,uid(),payload(first=False)))
expect_error('other capability cannot link victim assessment',call('f'*64,'battle',profile,uid(),{'battle':{'battleId':'00000000-0000-0000-0000-000000000001','boss':'arakus','seconds':1},'samples':[]},event).replace('select public.','perform public.'),'22023')
x=uid();expect_error('cross-owner foreign key rejected',call('f'*64,'battle',profile,x,{'battle':{'battleId':x,'boss':'arakus','seconds':1},'samples':[]},event).replace('select public.','perform public.'),'23503')
check('same UUID with unrelated capability is separate namespace',call('f'*64,'assessment',profile,event,payload('novice')))
x=uid();expect_error('tutorial boss rejected transactionally',call(secret,'battle',profile,x,{'battle':{'battleId':x,'boss':'tutorial','seconds':1},'samples':[]},event).replace('select public.','perform public.'),'23514')
x=uid();expect_error('invalid observation sum rolls back header',call(secret,'battle',profile,x,{'battle':{'battleId':x,'boss':'arakus','seconds':1},'samples':[dict(id='cleave',level=0,opportunities=10,parries=20,dodges=0,hits=0)]},event).replace('select public.','perform public.'),'23514')
check('failed payload leaves no partial battle',"do $$ begin if exists(select 1 from proficiency_battles where event_id='%s') then raise exception 'partial';end if;end $$;"%x)
for label,link,level in [('unclassified',None,0),('adjusted',event,1)]:
 x=uid();check(label+' remains outside neutral cohort',call(secret,'battle',profile,x,{'battle':{'battleId':x,'boss':'arakus','seconds':1},'samples':[dict(id='cleave',level=level,opportunities=100,parries=0,dodges=0,hits=100)]},link))
# Replay, skipped and low-confidence assessments cannot enter grouped statistics.
for label,a_fixture in [('replay',payload(first=False)),('skip',payload(skip=True)),('low_confidence',payload())]:
 if label=='low_confidence':a_fixture['assessment']['confidence']=.49
 owner=('%064x'%(10+len(checks)));pr,ev,ba=uid(),uid(),uid()
 check(label+' accepted for private archival',call(owner,'assessment',pr,ev,a_fixture))
 check(label+' battle retained but not grouped',call(owner,'battle',pr,ba,{'battle':{'battleId':ba,'boss':'arakus','seconds':1},'samples':[dict(id='cleave',level=0,opportunities=100,parries=0,dodges=0,hits=100)]},ev))
check('no retroclassification or mixed-level contamination',"do $$ begin if (select opportunities from pattern_difficulty_by_proficiency)<>30 then raise exception 'contamination';end if;end $$;")
for table in ['proficiency_assessments','proficiency_battles','proficiency_pattern_samples']:
 for role in ['anon','authenticated']:
  check('raw privileges denied '+role+' '+table,"do $$ begin if has_table_privilege('%s','public.%s','SELECT,INSERT,UPDATE,DELETE') then raise exception 'raw grant';end if;end $$;"%(role,table))
expect_error('anon actual raw read denied','set local role anon;perform 1 from public.proficiency_assessments;','42501')
check('RLS enabled on all three raw tables',"do $$ begin if (select count(*) from pg_class where relname in ('proficiency_assessments','proficiency_battles','proficiency_pattern_samples') and relrowsecurity)<>3 then raise exception 'RLS';end if;end $$;")
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(dict(passed=len(checks),checks=checks,environment='PostgreSQL17 disposable Docker network=none; only auto-created disposable PG volume; no host mounts/ports; not Supabase/PostgREST'),indent=2));print('SQL_PASS',len(checks))
