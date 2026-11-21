-- REVIEW/APPLY by DB maintainer. Additive, no production data writes.
-- Public clients get EXECUTE on one fixed, parameter-free aggregate only.
begin;
do $setup$
begin
 if not exists(select 1 from pg_catalog.pg_roles where rolname='dda_exhibit_reader') then
  create role dda_exhibit_reader nologin nosuperuser nocreatedb nocreaterole noinherit noreplication nobypassrls;
 end if;
 if exists(select 1 from pg_catalog.pg_roles where rolname='dda_exhibit_reader'
   and (rolcanlogin or rolsuper or rolcreatedb or rolcreaterole or rolreplication or rolbypassrls or rolinherit))
   or exists(select 1 from pg_catalog.pg_auth_members where member=(select oid from pg_catalog.pg_roles where rolname='dda_exhibit_reader'))
   or pg_catalog.pg_has_role('anon','dda_exhibit_reader','MEMBER')
   or pg_catalog.pg_has_role('authenticated','dda_exhibit_reader','MEMBER') then
  raise exception 'dda_exhibit_reader must be a private non-login, non-inheriting, non-bypass role';
 end if;
 if exists(select 1 from pg_catalog.pg_class c join pg_catalog.pg_namespace n on n.oid=c.relnamespace
   where n.nspname='public' and c.relname in ('proficiency_assessments','proficiency_battles','proficiency_pattern_samples')
   and pg_catalog.has_table_privilege('dda_exhibit_reader',c.oid,'INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES')) then
  raise exception 'Private reader has unexpected raw write/owner privileges; review deployment first';
 end if;
 if exists(select 1 from pg_catalog.pg_class c join pg_catalog.pg_namespace n on n.oid=c.relnamespace
   where n.nspname='public' and c.relname in ('proficiency_assessments','proficiency_battles','proficiency_pattern_samples')
   and (not c.relrowsecurity or pg_catalog.has_table_privilege('anon',c.oid,'SELECT') or pg_catalog.has_table_privilege('authenticated',c.oid,'SELECT'))) then
  raise exception 'Expected raw-table RLS enabled and no public-client SELECT; review deployment first';
 end if;
end $setup$;
grant usage on schema public to dda_exhibit_reader;
grant select on public.proficiency_assessments,public.proficiency_battles,public.proficiency_pattern_samples to dda_exhibit_reader;
-- Only the private function-owner role can use these SELECT policies. No anon/authenticated policy.
drop policy if exists dda_exhibit_reader_select on public.proficiency_assessments;
create policy dda_exhibit_reader_select on public.proficiency_assessments for select to dda_exhibit_reader using(true);
drop policy if exists dda_exhibit_reader_select on public.proficiency_battles;
create policy dda_exhibit_reader_select on public.proficiency_battles for select to dda_exhibit_reader using(true);
drop policy if exists dda_exhibit_reader_select on public.proficiency_pattern_samples;
create policy dda_exhibit_reader_select on public.proficiency_pattern_samples for select to dda_exhibit_reader using(true);

create or replace function public.exhibit_dda_summary_v1()
returns jsonb language sql stable security definer
set search_path=pg_catalog set row_security=on set statement_timeout='5s'
as $summary$
with
sources(source) as (values ('demo'),('real'),('other_demo')),
group_names(group_name) as (values ('novice'),('normal'),('expert')),
-- Fixed game vocabulary: never expose attacker-chosen labels from raw input.
allowed_patterns(boss,pattern) as (values
 ('arakus','cleave'),('arakus','shield'),('arakus','kick'),('arakus','charge'),('arakus','throw'),('arakus','slam'),('arakus','slash'),('arakus','rush'),('arakus','sweep'),('arakus','step'),('arakus','rage'),
 ('phanes','orb'),('phanes','fan'),('phanes','beam'),('phanes','rain'),('phanes','barrier'),('phanes','pulse'),('phanes','phanes_star'),
 ('adicus','scythe'),('adicus','cross'),('adicus','chain'),('adicus','fall'),('adicus','prisoner'),('adicus','judgement'),('adicus','spin'),
 ('aethelos','blade'),('aethelos','lance'),('aethelos','rift'),('aethelos','thunder'),('aethelos','creation'),('aethelos','heaven'),
 ('kharos','twelve'),('kharos','draw'),('kharos','echo'),('kharos','eclipse'),('kharos','rain'),('kharos','phantom'),('kharos','end')),
classified as materialized (
 select a.*,case when a.raw_json #>> '{_demo,cohort}'='demo_exhibit_20261121' then 'demo'
   when jsonb_typeof(a.raw_json)='object' and not (a.raw_json ? '_demo') then 'real' else 'other_demo' end as source
 from public.proficiency_assessments a
 where a.is_first and a.completed and a.valid and not a.skipped and a.active_seconds>0
),
linked_battles as materialized (
 select b.*,a.source,a.group_name,a.confidence,a.parry_attempts,a.dodge_attempts
 from public.proficiency_battles b join classified a
 on (a.owner_key,a.profile_id,a.event_id)=(b.owner_key,b.profile_id,b.assessment_event_id)
 where a.source=case when b.raw_json #>> '{_demo,cohort}'='demo_exhibit_20261121' then 'demo'
  when jsonb_typeof(b.raw_json)='object' and not (b.raw_json ? '_demo') then 'real' else 'other_demo' end
),
linked_samples as materialized (
 select s.*,b.source,b.group_name,b.boss,b.confidence,b.parry_attempts,b.dodge_attempts
 from public.proficiency_pattern_samples s join linked_battles b
 on (s.owner_key,s.profile_id,s.battle_id)=(b.owner_key,b.profile_id,b.event_id)
 join allowed_patterns p on (p.boss,p.pattern)=(b.boss,s.pattern)
),
group_stats as (
 select source,group_name,count(*) as assessments,count(distinct (owner_key,profile_id)) as players,
 count(distinct owner_key)>=3 as released,round(avg(score)::numeric,3) as score,
 round(avg(active_seconds)::numeric,1) as active_seconds,round(avg(elapsed_seconds)::numeric,1) as elapsed_seconds,
 round((sum(parry_successes)::numeric/nullif(sum(parry_attempts),0)),3) as parry_rate,
 round((sum(dodge_successes)::numeric/nullif(sum(dodge_attempts),0)),3) as dodge_rate,round(avg(damage_hits)::numeric,2) as hits
 from classified group by source,group_name
),
group_output as (
 select s.source,g.group_name,coalesce(t.released,false) as released,
 case when t.released then t.players end as players,case when t.released then t.assessments end as assessments,
 case when t.released then t.score end as score,case when t.released then t.active_seconds end as active_seconds,
 case when t.released then t.elapsed_seconds end as elapsed_seconds,case when t.released then t.parry_rate end as parry_rate,
 case when t.released then t.dodge_rate end as dodge_rate,case when t.released then t.hits end as hits,
 case when t.released then (select count(*) from linked_battles b where (b.source,b.group_name)=(s.source,g.group_name)) end as battles,
 case when t.released then (select count(*) from linked_samples x where (x.source,x.group_name)=(s.source,g.group_name)) end as samples
 from sources s cross join group_names g left join group_stats t on (t.source,t.group_name)=(s.source,g.group_name)
),
source_output as (
 select source,case when bool_and(released) then jsonb_build_object('players',sum(players),'assessments',sum(assessments),'battles',sum(battles),'samples',sum(samples)) else 'null'::jsonb end as totals
 from group_output group by source
),
boss_output as (
 select source,group_name,boss,count(*) as battles,round(avg(seconds)::numeric,1) as seconds
 from linked_battles group by source,group_name,boss having count(distinct owner_key)>=3
),
pattern_stats as (
 select source,group_name,boss,pattern,count(distinct owner_key) as players,sum(opportunities) as opportunities,
 sum(parries+dodges)::numeric/nullif(sum(opportunities),0) as success_rate,
 sum(parries)::numeric/nullif(sum(opportunities),0) as parry_rate,
 sum(hits)::numeric/nullif(sum(opportunities),0) as hit_rate
 from linked_samples where confidence>=.5 and parry_attempts>=1 and dodge_attempts>=1 and level=0 and opportunities>0
 group by source,group_name,boss,pattern having count(distinct owner_key)>=3 and sum(opportunities)>=20
)
select jsonb_build_object(
 'schemaVersion',1,'cohort','demo_exhibit_20261121','source','server aggregate RPC',
 'policy',jsonb_build_object('minOwners',3,'minPatternOpportunities',20,'groupPopulation','valid completed first assessment, active_seconds>0','patternPopulation','same-source linked samples, confidence>=0.5, observed parry/dodge, level=0','totals','null unless all three groups released; never infer suppressed cells','privacy','fixed cohorts and vocabulary, no filters; small-cell suppression, not differential privacy'),
 'counts',(select jsonb_object_agg(source,totals) from source_output),
 'groups',(select jsonb_agg(jsonb_build_object('source',source,'group',group_name,'released',released,'players',players,'validFirst',assessments,'score',score,'activeSeconds',active_seconds,'elapsedSeconds',elapsed_seconds,'parryRate',parry_rate,'dodgeRate',dodge_rate,'hits',hits) order by source,group_name) from group_output),
 'bosses',coalesce((select jsonb_agg(jsonb_build_object('source',source,'group',group_name,'boss',boss,'battles',battles,'seconds',seconds) order by source,group_name,boss) from boss_output),'[]'::jsonb),
 'patterns',coalesce((select jsonb_agg(jsonb_build_object('source',source,'group',group_name,'boss',boss,'pattern',pattern,'players',players,'opportunities',opportunities,'successRate',round(success_rate,3),'parryRate',round(parry_rate,3),'hitRate',round(hit_rate,3),'eligible',true,'suggestedLevel',case when success_rate<.45 then -1 when success_rate>.85 then 1 else 0 end) order by source,group_name,boss,pattern) from pattern_stats),'[]'::jsonb)
);
$summary$;
-- PostgreSQL 17 requires the migration owner to be able to SET ROLE to the new function owner.
-- Temporarily grant only SET (not INHERIT/ADMIN), then revoke within the same transaction.
grant dda_exhibit_reader to postgres with admin false, inherit false, set true;
-- CREATE needed only during ownership transfer; never retained by function owner.
grant create on schema public to dda_exhibit_reader;
alter function public.exhibit_dda_summary_v1() owner to dda_exhibit_reader;
revoke create on schema public from dda_exhibit_reader;
revoke dda_exhibit_reader from postgres;
revoke all on function public.exhibit_dda_summary_v1() from public,anon,authenticated;
grant execute on function public.exhibit_dda_summary_v1() to anon,authenticated;
comment on function public.exhibit_dda_summary_v1() is 'Fixed read-only cohort aggregates; no IDs/raw JSON/keys, >=3 owners, pattern >=20 neutral opportunities. Existing raw privileges and writes remain blocked.';
notify pgrst,'reload schema';
commit;
