-- ============================================================================
-- 역전의 탑 · 전투 기록 DB (Supabase / PostgreSQL)
-- Supabase 대시보드 → SQL Editor → New query 에 전부 붙여 넣고 Run.
-- 여러 번 실행해도 안전하도록 작성했습니다 (if not exists / or replace).
-- ============================================================================

-- 1) 플레이어: 게임이 처음 실행될 때 만든 익명 ID. 이름·이메일 같은 개인정보는 저장하지 않는다.
create table if not exists public.players (
  id            uuid primary key,
  game_version  text,
  created_at    timestamptz not null default now()
);

-- 2) 전투 한 판
create table if not exists public.battles (
  id              uuid primary key,                       -- 게임이 만든 ID (다시 보내도 중복 저장 안 됨)
  player_id       uuid not null references public.players(id) on delete cascade,
  boss            text not null,
  clear           boolean not null,
  seconds         real not null check (seconds >= 0 and seconds < 7200),
  dda             boolean not null,
  hits            int not null default 0 check (hits >= 0),
  parries         int not null default 0 check (parries >= 0),
  attack          real,
  max_hp          real,
  potion_heal     real,
  card_purchases  int,
  cards           text,                                   -- 예: "blade:2|vitality:1"
  game_version    text,
  played_at       timestamptz not null default now()
);
create index if not exists battles_boss_time on public.battles (boss, played_at desc);
create index if not exists battles_player on public.battles (player_id);

-- 3) 전투 안의 패턴별 기록 (전투 1 : 패턴 N)
create table if not exists public.pattern_stats (
  battle_id      uuid not null references public.battles(id) on delete cascade,
  boss           text not null,
  pattern        text not null,
  level          int  not null check (level between -2 and 2),   -- 그 전투에서 DDA가 고른 난이도 단계
  opportunities  int  not null default 0 check (opportunities >= 0),
  hits           int  not null default 0 check (hits >= 0),
  parries        int  not null default 0 check (parries >= 0),
  dodges         int  not null default 0 check (dodges >= 0),
  ignored        int  not null default 0 check (ignored >= 0),
  damage         real not null default 0,
  primary key (battle_id, pattern)
);
create index if not exists pattern_stats_boss_pattern on public.pattern_stats (boss, pattern);

-- 4) 로비에서 고른 강화 카드
create table if not exists public.card_picks (
  id          uuid primary key,
  player_id   uuid not null references public.players(id) on delete cascade,
  card        text not null,
  cost        int  not null check (cost >= 0),
  pick_index  int  not null check (pick_index >= 0),       -- 몇 번째 강화인지
  picked_at   timestamptz not null default now()
);
create index if not exists card_picks_card on public.card_picks (card);

-- ----------------------------------------------------------------------------
-- 집계 뷰: 게임은 이 뷰만 읽는다 (다른 사람의 원본 기록은 읽을 수 없음)
-- ----------------------------------------------------------------------------

-- 패턴별 전체 성공률 → 처음 만나는 플레이어의 시작 난이도 추천
-- 성공 = 패링 + 회피. 기회가 20번 미만이면 근거 부족으로 0(보통).
create or replace view public.pattern_difficulty as
select
  s.boss,
  s.pattern,
  sum(s.opportunities)::int                                                        as opportunities,
  count(distinct b.player_id)::int                                                 as players,
  coalesce(sum(s.parries + s.dodges)::real / nullif(sum(s.opportunities), 0), 0)   as success_rate,
  coalesce(sum(s.parries)::real / nullif(sum(s.opportunities), 0), 0)              as parry_rate,
  coalesce(sum(s.hits)::real / nullif(sum(s.opportunities), 0), 0)                 as hit_rate,
  case
    when sum(s.opportunities) < 20 then 0
    when sum(s.parries + s.dodges)::real / sum(s.opportunities) < 0.45 then -1   -- 다들 자주 맞음 → 예고를 길게
    when sum(s.parries + s.dodges)::real / sum(s.opportunities) > 0.85 then 1    -- 다들 쉽게 막음 → 예고를 짧게
    else 0
  end::int                                                                          as suggested_level
from public.pattern_stats s
join public.battles b on b.id = s.battle_id
group by s.boss, s.pattern;

-- 보스별 요약 (발표용 그래프)
create or replace view public.boss_summary as
select
  boss,
  count(*)::int                                        as battles,
  count(distinct player_id)::int                       as players,
  coalesce(avg(case when clear then 1 else 0 end), 0)  as clear_rate,
  coalesce(avg(seconds), 0)                            as avg_seconds,
  coalesce(avg(parries), 0)                            as avg_parries,
  coalesce(avg(hits), 0)                               as avg_hits
from public.battles
group by boss;

-- 카드 인기 (발표용)
create or replace view public.card_popularity as
select card, count(*)::int as picks, count(distinct player_id)::int as players
from public.card_picks
group by card;

-- ----------------------------------------------------------------------------
-- 보안 (RLS): 게임에 들어가는 공개 키(anon)로는
--   · 원본 테이블에는 "추가"만 가능 (조회·수정·삭제 불가)
--   · 집계 뷰는 "조회"만 가능
-- ----------------------------------------------------------------------------
-- "Automatically expose new tables"를 끈 프로젝트에서도 동작하도록 필요한 권한을 직접 준다.
grant usage on schema public to anon;

alter table public.players       enable row level security;
alter table public.battles       enable row level security;
alter table public.pattern_stats enable row level security;
alter table public.card_picks    enable row level security;

drop policy if exists "game inserts players"       on public.players;
drop policy if exists "game inserts battles"       on public.battles;
drop policy if exists "game inserts pattern_stats" on public.pattern_stats;
drop policy if exists "game inserts card_picks"    on public.card_picks;
create policy "game inserts players"       on public.players       for insert to anon with check (true);
create policy "game inserts battles"       on public.battles       for insert to anon with check (true);
create policy "game inserts pattern_stats" on public.pattern_stats for insert to anon with check (true);
create policy "game inserts card_picks"    on public.card_picks    for insert to anon with check (true);

revoke all on public.players, public.battles, public.pattern_stats, public.card_picks from anon;
grant insert on public.players, public.battles, public.pattern_stats, public.card_picks to anon;

-- 뷰는 만든 사람(소유자) 권한으로 집계하므로 anon은 원본 행을 보지 못하고 집계 결과만 받는다.
revoke all on public.pattern_difficulty, public.boss_summary, public.card_popularity from anon;
grant select on public.pattern_difficulty, public.boss_summary, public.card_popularity to anon;
