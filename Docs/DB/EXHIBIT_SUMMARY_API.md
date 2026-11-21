# 전시용 안전 집계 API v1 — 배포 안내

이 변경은 원시 테이블을 공개하지 않는다. 운영 DB 적용은 아직 수행하지 않았다. DB 관리자가 실제 정의/권한을 확인하고 승인한 뒤 migration을 적용해야 한다. 클라이언트에는 기존 anon/publishable 키만 사용한다. service_role/서버 secret은 Lab·게임·ZIP에 필요하지 않으며 넣으면 안 된다.

## 계약과 최소 권한

- 새 endpoint: **GET `/rest/v1/rpc/exhibit_dda_summary_v1`**, 인자 없음, JSONB v1 반환.
- `STABLE SECURITY DEFINER`, `search_path=pg_catalog`, 모든 테이블은 `public.`로 한정한다. 동적 SQL·사용자 조건·원시행 출력·DB 쓰기 없음.
- 함수 소유자 `dda_exhibit_reader`: NOLOGIN/NOSUPERUSER/NOINHERIT/NOBYPASSRLS/관리권한 없음. 필요한 숙련도3테이블의 SELECT만 부여한다.
- RLS를 끄지 않는다. 새 SELECT 정책은 **전용 비로그인 역할만** 대상으로 한다. anon/authenticated에는 원시 SELECT/INSERT 권한을 부여하지 않고 함수 EXECUTE만 부여한다. 기존 쓰기 RPC·기존 view·기존 정책은 변경하지 않는다.
- 기존에 같은 reader 역할이 로그인/관리/상속/원시쓰기 권한을 보유하거나 anon/authenticated가 그 역할을 가질 수 있으면 migration은 중단한다. 기존 raw RLS가 꺼져 있거나 익명/인증 역할의 raw SELECT가 열려 있어도 중단한다. 강제로 기존 보안을 고치거나 우회하지 않는다.
- 소표본 보호: 그룹 지표는 서로 다른 owner가 최소3개일 때만 공개. 미달 그룹은 인원·평균·비율 모두 null이다. 전체 출처 합계는 그 출처의 **세 그룹 모두 공개 가능할 때만** 반환하여 작은 그룹을 합계에서 역산하지 못하게 한다.
- 패턴은 기존 SQL과 같이3 owner/20기회, 중립Lv0, 유효 최초평가, confidence>=.5, 패링·회피 관측 조건이다. 보스 통계도3 owner 미만이면 반환하지 않는다.
- 기존 혼합 뷰와 공개 집계의 차분이나 반복 조회/시간차/외부 지식에 대한 differential privacy를 보장하는 API는 아니다. API gateway의 기존 rate limit을 유지하고, 데이터 규모에 맞춰 호출 비용과 공개 범위를 검토한다. Lab은5초 refresh cooldown을 유지한다.

정책의 `owner`는 익명 capability 소유자이며 실제 사람 인증이 아니다. 화면의 players는 유효 최초평가 프로필 수다. 게임의 DDA·LinUCB 판단식이나 실제 전투 배정을 바꾸는 기능이 아니다.

## 집계 범위

발표용 판별은 `raw_json #>> '{_demo,cohort}' = 'demo_exhibit_20261121'`. `_demo` 없는 object는 real, 다른/잘못된 marker는 other_demo다. owner_key 접두사로 판별하지 않는다.

평가: 완료·유효·최초·비skip·active_seconds>0. 그룹 score/수행·전체시간/피격은 이 평가들의 산술평균이다. 패링·회피율은 성공 합계/시도 합계이다. 그룹 분류 공식·데이터를 수정하지 않는다.

보스전은 `(owner_key,profile_id,assessment_event_id)`로 해당 평가에 연결하고, 평가와 전투의 출처가 같아야 한다. 샘플은 `(owner_key,profile_id,battle_id)`로 그 전투에 연결한다. 고아/출처충돌·알 수 없는 패턴은 제외한다. 합계의 전투·샘플은 이 연결 범위이며 raw 테이블 전체 건수가 아니다. 보스·패턴 이름은 현재 Catalog의 고정 목록만 공개하여 입력자가 임의 개인정보를 pattern 문자열로 출력할 수 없게 한다.

`groups`는 고정9개(source3 × group3), `counts`는 출처별 공개 가능 합계 또는 null, `bosses/patterns`는 조건을 충족한 행만 반환한다. 패턴이 보이지 않는 것은 기록 없음 **또는** 표본 부족이며0건으로 단정하지 않는다. raw JSON/owner_key/profile_id/event_id/날짜별 행/업로드 payload/키는 반환하지 않는다.

기존 `pattern_difficulty_by_proficiency`는 계속 별도로 조회하고 **실제·가상 혼합 가능**으로 표시한다. 그 결과33행만으로300평가/300전투/3300샘플 또는 그룹 평균을 추정하지 않는다. 새 API가 없거나 실패하면 기존 혼합 뷰만 표시하며 원시 SELECT나 관리자 키로 fallback하지 않는다.

## 배포 전 확인 (DB 관리자)

1. 운영 DB에서 현재 migration 적용 상태 및 실제 테이블 열/제약/PK·FK/RLS를 확인한다. 기대 기반은 `20261118_proficiency.sql`이다.
2. `pg_get_viewdef('public.pattern_difficulty_by_proficiency'::regclass,true)`로 실제 집계 조건을 확인한다. 이 구현의3 owner/20기회/.45·.85/중립Lv0 기준은 repository SQL을 기반으로 한다. 운영 정의가 다르면 그대로 일치한다고 주장하지 말고 배포 전에 정책을 검토한다. 기존 DDA view를 이 작업 때문에 변경하지 않는다.
3. `pg_get_functiondef`로 기존 `submit_proficiency` 정의를 별도 보존하고, anon/authenticated의 함수 EXECUTE 및 raw SELECT 차단을 확인한다.
4. 고정 Catalog 목록이 현재 게임과 일치하는지, 공개 그룹 지표의 정의와 소표본 기준이 전시 공개 범위로 적절한지 검토한다.
5. staging에서 아래 테스트/권한 검사를 먼저 실행한다. 운영 관리자 자격증명은 클라이언트/ZIP/저장소에 저장하지 않는다.

## 적용

DB 관리자의 승인된 SQL Editor/마이그레이션 도구에서 다음 파일을 하나의 transaction으로 적용한다.

`Docs/DB/migrations/20261121_exhibit_summary.sql`

역할 생성·GRANT·RLS 정책 생성·함수 ownership 변경 권한이 필요하다. 권한 부족 또는 사전 조건 불일치이면 전체 transaction이 실패하며 raw 공개로 우회하지 않는다. 마지막 NOTIFY로 PostgREST schema cache를 다시 읽도록 한다.

적용 후 확인:

- raw3테이블의 RLS가 모두 ON, anon/authenticated raw SELECT는 false.
- reader 역할은 비로그인/비관리/BYPASSRLS 없음, raw SELECT만 있고 INSERT/UPDATE/DELETE 없음. anon/authenticated는 reader 역할로 전환할 수 없음.
- anon/authenticated가 `exhibit_dda_summary_v1()`을 실행할 수 있고 PUBLIC 기본 EXECUTE는 제거됨.
- 기존 보호 INSERT RPC와 기존 혼합 view의 정의/권한이 바뀌지 않음.
- 공개 클라이언트 GET으로 새 endpoint를 호출하여 schemaVersion=1과 소표본 null을 확인. HTTP404/PGRST202이면 배포/스키마캐시,401/403이면 공개키/EXECUTE/스키마 노출을 점검한다. 원시 권한을 열지 않는다.
- Lab 재시작 또는 DB 분석 새로고침. 발표용/실제/다른 데모를 전환하고 실제 조회 집계만 표시하는지 확인한다. 알려진300/300/3300은 **검증 기준값**이며 API가 보장하거나 만들어 넣는 값이 아니다.

개발자 검증(운영 DB에 연결하지 않음):

```bash
python Tests/test-safe-db.py
python Tests/test-exhibit.py
python Tests/test-safe-db-postgres.py --output <새 결과 폴더>
```

마지막 검사는 Docker `postgres:16-alpine`을 사용한다. host port 없이 network=none, tmpfs 데이터, 종료시 container 삭제. 테스트용 소량 fixture만 만들고 실제 Supabase 데이터는 사용하지 않는다. 테스트 후 운영 INSERT를 직접 해본 것으로 보고하면 안 된다.

## 중단/롤백

문제가 있으면 DB 관리자가 **새 집계 함수만** 익명/인증 역할의 실행권한을 회수하여 즉시 노출을 중단한다.

```sql
revoke execute on function public.exhibit_dda_summary_v1() from anon,authenticated;
```

그 후 필요하면 보존한 이전 함수 정의로 복원한다. 기존 원시 테이블·데이터·보호 INSERT RPC·혼합 view를 삭제/수정하지 않는다. `DROP OWNED`, raw RLS 해제, anon SELECT grant 같은 광범위 롤백은 사용하지 않는다. Lab은 새 RPC 실패 상태와 기존 혼합 view를 표시한다.

## 검증 한계

격리 PostgreSQL에서 SQL/RLS/권한/기존 INSERT 동작을, 모의 HTTP와 실제 격리 SQL 결과로 Python/UI를 검증했다. 운영 Supabase에 새 API를 적용하거나 실제 PostgREST 배포·운영 view 정의·실제300명 데이터를 확인한 것은 아니다. Windows/Unity/LIVE 실기와 최종 ZIP도 이 작업에서 생성/완료했다고 주장하지 않는다.
