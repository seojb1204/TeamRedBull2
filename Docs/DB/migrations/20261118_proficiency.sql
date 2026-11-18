-- ADDITIVE migration. Review and apply manually; never run against production in Codex QA.
-- PostgreSQL 14+ / Supabase. Existing players/battles/pattern_stats/RLS remain untouched.
-- A public key + UUID is NOT authentication. New raw rows are scoped to SHA256(random 256-bit
-- per-profile capability), never to caller-supplied UUID alone. Secret is not stored in SQL.
-- This protects ownership/immutability, not honesty of client-reported skill or Sybil resistance.
begin;
create table if not exists public.proficiency_assessments (
 owner_key text not null check(owner_key ~ '^[0-9a-f]{64}$'), profile_id uuid not null, event_id uuid not null,
 is_first boolean not null, group_name text not null check(group_name in ('novice','normal','expert')),
 completed boolean not null, skipped boolean not null, valid boolean not null,
 score real not null check(score between 0 and 1), confidence real not null check(confidence between 0 and 1),
 active_seconds real not null check(active_seconds >=0 and active_seconds<86400),
 elapsed_seconds real not null check(elapsed_seconds>=0 and elapsed_seconds<172800),
 parry_attempts int not null check(parry_attempts>=0), parry_successes int not null,
 dodge_attempts int not null check(dodge_attempts>=0), dodge_successes int not null,
 damage_hits int not null check(damage_hits>=0), retries int not null check(retries>=0),
 assessed_at timestamptz not null, raw_json jsonb not null, received_at timestamptz not null default now(),
 primary key(owner_key,profile_id,event_id),
 check(parry_successes between 0 and parry_attempts),check(dodge_successes between 0 and dodge_attempts),
 check(completed or skipped),check(not(completed and skipped)),
 check(not valid or (completed and not skipped and active_seconds>0)),
 check(not skipped or (group_name='normal' and score=0 and confidence=0 and not valid))
);
create unique index if not exists proficiency_one_initial on public.proficiency_assessments(owner_key,profile_id) where is_first;
create table if not exists public.proficiency_battles (
 owner_key text not null,profile_id uuid not null,event_id uuid not null,assessment_event_id uuid,
 boss text not null check(boss in ('arakus','phanes','adicus','aethelos','kharos')),
 seconds real not null check(seconds>=0 and seconds<7200), raw_json jsonb not null,
 received_at timestamptz not null default now(),primary key(owner_key,profile_id,event_id),
 foreign key(owner_key,profile_id,assessment_event_id) references public.proficiency_assessments(owner_key,profile_id,event_id)
);
create table if not exists public.proficiency_pattern_samples (
 owner_key text not null,profile_id uuid not null,battle_id uuid not null,pattern text not null check(length(pattern) between 1 and 80),
 level int not null check(level between -2 and 2),opportunities int not null check(opportunities>=0),
 hits int not null check(hits>=0),parries int not null check(parries>=0),dodges int not null check(dodges>=0),
 primary key(owner_key,profile_id,battle_id,pattern),
 foreign key(owner_key,profile_id,battle_id) references public.proficiency_battles(owner_key,profile_id,event_id),
 check(hits::bigint+parries+dodges=opportunities)
);
alter table public.proficiency_assessments enable row level security;
alter table public.proficiency_battles enable row level security;
alter table public.proficiency_pattern_samples enable row level security;
-- NO raw policies/grants for anon/authenticated. All writes use the capability-bound definer.
revoke all on public.proficiency_assessments,public.proficiency_battles,public.proficiency_pattern_samples from public,anon,authenticated;

create or replace function public.submit_proficiency(p_secret text,p_kind text,p_profile uuid,p_event uuid,p_assessment uuid,p_payload jsonb)
returns boolean language plpgsql security definer set search_path=pg_catalog,public as $$
declare owner text; a jsonb;b jsonb;s jsonb;previous jsonb;
begin
 if p_secret is null or p_secret !~ '^[0-9a-f]{64}$' or p_profile is null or p_event is null or p_payload is null or octet_length(p_payload::text)>1048576 then
  raise exception 'Invalid protected telemetry' using errcode='22023';
 end if;
 owner:=encode(sha256(convert_to(p_secret,'UTF8')),'hex');
 if p_kind='assessment' then
  if p_assessment is not null then raise exception 'Unexpected assessment link' using errcode='22023';end if;
  select raw_json into previous from public.proficiency_assessments where owner_key=owner and profile_id=p_profile and event_id=p_event;
  if found then
   if previous<>p_payload then raise exception 'Immutable assessment conflict' using errcode='23505';end if;
   return true;
  end if;
  a:=p_payload->'assessment';
  if a is null or jsonb_typeof(a)<>'object' or (a->>'schemaVersion')::int is distinct from 1 or
     (a->>'ultimateExecutionSuccess')::boolean is not true and (a->>'valid')::boolean is true then
   raise exception 'Unsupported or invalid assessment' using errcode='22023';
  end if;
  insert into public.proficiency_assessments(owner_key,profile_id,event_id,is_first,group_name,completed,skipped,valid,score,confidence,active_seconds,elapsed_seconds,
   parry_attempts,parry_successes,dodge_attempts,dodge_successes,damage_hits,retries,assessed_at,raw_json)
  values(owner,p_profile,p_event,(a->>'isFirstAssessment')::boolean,a->>'group',(a->>'completed')::boolean,(a->>'skipped')::boolean,(a->>'valid')::boolean,
   (a->>'score')::real,(a->>'confidence')::real,(a->>'activeDurationSeconds')::real,coalesce((a->>'elapsedDurationSeconds')::real,0),
   (a->>'parryAttempts')::int,(a->>'parrySuccesses')::int,(a->>'dodgeAttempts')::int,(a->>'dodgeSuccesses')::int,(a->>'damageHits')::int,(a->>'retryCount')::int,
   (a->>'completedAtUtc')::timestamptz,p_payload);
 elsif p_kind='battle' then
  select raw_json into previous from public.proficiency_battles where owner_key=owner and profile_id=p_profile and event_id=p_event;
  if found then
   if previous<>p_payload then raise exception 'Immutable battle conflict' using errcode='23505';end if;
   return true;
  end if;
  b:=p_payload->'battle';
  if b is null or coalesce((b->>'abandoned')::boolean,false) or (b->>'battleId')::uuid is distinct from p_event or
     jsonb_typeof(p_payload->'samples') is distinct from 'array' or jsonb_array_length(p_payload->'samples')>64 then
   raise exception 'Invalid real battle telemetry' using errcode='22023';
  end if;
  insert into public.proficiency_battles(owner_key,profile_id,event_id,assessment_event_id,boss,seconds,raw_json)
   values(owner,p_profile,p_event,p_assessment,b->>'boss',(b->>'seconds')::real,p_payload);
  for s in select value from jsonb_array_elements(p_payload->'samples') loop
   insert into public.proficiency_pattern_samples(owner_key,profile_id,battle_id,pattern,level,opportunities,hits,parries,dodges)
   values(owner,p_profile,p_event,s->>'id',(s->>'level')::int,(s->>'opportunities')::int,(s->>'hits')::int,(s->>'parries')::int,(s->>'dodges')::int);
  end loop;
 else raise exception 'Unsupported protected event kind' using errcode='22023';
 end if;
 return true;
end $$;
revoke all on function public.submit_proficiency(text,text,uuid,uuid,uuid,jsonb) from public,anon,authenticated;
grant execute on function public.submit_proficiency(text,text,uuid,uuid,uuid,jsonb) to anon,authenticated;

-- Only own-linked, completed VALID FIRST assessment, captured before THIS battle.
-- Null assessment links are immutable: no retroactive classification of legacy/unclassified battles.
-- Baseline level=0 samples only avoid recommending from a mix of already adjusted difficulties.
create or replace view public.pattern_difficulty_by_proficiency with(security_barrier=true) as
select b.boss,s.pattern,a.group_name,sum(s.opportunities)::int as opportunities,count(distinct b.owner_key)::int as players,
 (sum(s.parries+s.dodges)::double precision/nullif(sum(s.opportunities),0))::real as success_rate,
 (sum(s.parries)::double precision/nullif(sum(s.opportunities),0))::real as parry_rate,
 (sum(s.hits)::double precision/nullif(sum(s.opportunities),0))::real as hit_rate,
 (case when sum(s.parries+s.dodges)::double precision/sum(s.opportunities)<.45 then -1
       when sum(s.parries+s.dodges)::double precision/sum(s.opportunities)>.85 then 1 else 0 end)::int as suggested_level
from public.proficiency_battles b
join public.proficiency_pattern_samples s on (s.owner_key,s.profile_id,s.battle_id)=(b.owner_key,b.profile_id,b.event_id)
join public.proficiency_assessments a on (a.owner_key,a.profile_id,a.event_id)=(b.owner_key,b.profile_id,b.assessment_event_id)
where a.is_first and a.completed and a.valid and not a.skipped and a.confidence>=.5 and a.active_seconds>0
 and a.parry_attempts>=1 and a.dodge_attempts>=1 and s.level=0 and s.opportunities>0
-- Every anonymous owner counts once even if it supplies several profile UUID labels.
group by b.boss,s.pattern,a.group_name
having sum(s.opportunities)>=20 and count(distinct b.owner_key)>=3;
revoke all on public.pattern_difficulty_by_proficiency from public,anon,authenticated;
grant select on public.pattern_difficulty_by_proficiency to anon,authenticated;
comment on view public.pattern_difficulty_by_proficiency is 'Suppressed aggregates: >=20 neutral-level opportunities and >=3 distinct anonymous capability owners; not verified unique humans.';
commit;
