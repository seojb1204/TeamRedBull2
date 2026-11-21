# DDA Lab 안전 집계 API — 2026-11-21

## 결과

원시 숙련도 테이블 공개 SELECT를 없애고, 고정된 집계 RPC와 기존 혼합 뷰만 읽도록 Lab을 수정했다. 운영 마이그레이션은 **미적용**이며 실제 Supabase 새 endpoint는 아직 검증하지 않았다. 사용자 보고의 원시HTTP401/공개뷰33행/300평가·300전투·3300샘플은 직접 재조회한 관측값으로 주장하지 않는다.

기준 main `9d7d228fd48df75df5a829da15891e1adc238adb`을 fetch 확인했다. 로컬 work의 HEAD는 `d9847ffd38361b91d3f1c8512d5924ee440f884d`이나 수정 시작 전 두 커밋의 tracked tree는 동일했다. 기존 로컬 Death.png.meta 및 이전 보고서를 보존하며 work에서 미커밋 수정했다. 이 구현 단계에서는 새 브랜치/PR/Commit/Push/Merge를 수행하지 않았다. 최종 ZIP 생성 없음.

## 구현

- `Docs/DB/migrations/20261121_exhibit_summary.sql`: 인자 없는 `public.exhibit_dda_summary_v1()` JSONB 함수. STABLE/SECURITY DEFINER, 고정 search_path와 schema-qualified 테이블.
- 전용 NOLOGIN/NOINHERIT/NOBYPASSRLS 비관리 역할은 raw3테이블 SELECT만 보유. 전용 역할만을 위한 RLS SELECT 정책이며 기존 anon/authenticated raw권한은 열지 않는다. 공개 클라이언트에는 함수 EXECUTE만 부여한다. 기존 원시 데이터/쓰기 RPC/혼합뷰는 변경하지 않는다.
- 고정 cohort `_demo.cohort` 기준 demo/real/other_demo 구분, 같은 owner/profile/event 연결을 충족한 데이터만 집계. owner_key 접두사 판별 없음.
- 그룹: 유효 완료 최초평가·비skip·실습시간>0. 인원은 프로필 수, 평균점수·수행/전체시간·피격, 성공/시도 합계 기준 패링·회피율.3 owner 미만이면 해당 그룹 수치 모두 null.
- 출처 전체 합계: 해당 출처의 세 그룹이 모두 공개 가능할 때만 반환. 그 외에는 null이며 감춰진 그룹을0으로 표시하지 않는다.
- 보스/패턴: 고정 Catalog 명칭만 사용. 보스도3 owner, 패턴은3 owner·20기회·중립Lv0·confidence>=.5·패링/회피 관측 조건. 공개 가능 패턴만 반환. 권장 Lv는 기존 repository SQL의.45/.85 기준이며 게임의 실제 DDA 수식을 바꾸지 않는다.
- `database.py`: GET RPC + GET 기존 공개 혼합 뷰의 두 경로만 사용. raw3테이블과 기존 개별 battles는 조회 allowlist에서 제거했다. raw endpoint를 직접 호출하는 helper 사용도 네트워크 전에 거부한다. SQL버전/숫자/고정명칭/소표본 정책/공개 합계를 검증하고 모든 출력은 whitelist로 투영한다.
- `database.js/index.html`: source별 서버 집계, 소표본 비공개, API 미배포/실패 상태, 기존 혼합 view를 구분한다. 새 API 실패 시 기존33행 같은 혼합 결과는 유지하되 그룹별 수치/300명 등을 만들어내지 않는다.
- 기존 순수 `aggregate()`는 테스트/오프라인 reference helper로만 유지하며 runtime은 raw 데이터를 fetch하거나 그 helper를 호출하지 않는다.

새 공개 API도 완전한 재식별 방지나 differential privacy를 보장하지 않는다. 기존 혼합뷰/여러 공개집계/시간차와의 차분 위험은 남으며 게이트웨이의 호출 제한·전시 공개 범위를 배포 담당자가 검토해야 한다. `_demo` 표시는 기록 출처 태그이며 실제 사람/데이터 진실성을 인증하지 않는다.

## 실제 검증

모든 DB 쓰기는 **network=none, host port 없는 일회용 PostgreSQL16 컨테이너의 소량 fixture 또는 기존 보호 RPC 회귀 검사**에만 수행했다. Supabase 데이터 생성/수정/삭제나 운영 마이그레이션은 실행하지 않았다. container는 종료 후 제거했다.

| 검사 | 결과 |
|---|---|
| 실제 PostgreSQL SQL·RLS·권한·집계 | **34 PASS** |
| Python 집계 계약·GET전용·오류·fallback | **16 PASS** |
| 기존 exhibit unit | **15 PASS** |
| 기존 LIVE reader | **22 PASS** |
| 기존 Windows 시작 경로 | **9 PASS** |
| 실제 게임 보호HTTP 코드(모의HTTP) | **28 PASS** |
| 실제 게임 송신 진단(모의HTTP) | **13 PASS** |
| 브라우저 통합 | **PASS**: 실제 격리 SQL 집계 결과→모의 GET transport→실제 Lab UI, source별 표/그래프·소표본 비공개·RPC404 fallback, 실제 C# 실험, 기존 HEADLESS 모니터 유지 |
| Python compile / 자체 수정분 diff check | PASS |

PostgreSQL 검증에는 raw SELECT/INSERT 차단(anon/authenticated), private 역할 전환 거부, 함수 권한·reader 비관리/쓰기불가, schema/temp-table shadow 방어, 소표본 은닉·1owner다중프로필 방어, 기준 미달패턴 제외, 정확한 cohort 연결/비율, 신원/원시JSON/임의label 비노출, 기존 raw행/혼합 view/보호쓰기함수 보존, 기존 INSERT·재시도 멱등성, migration 재적용, 기존 reader에 과도한 권한이 있으면 적용 중단을 포함했다.

기존 공개 뷰33행 fallback은 합성 HTTP fixture로 검증했다. 실제 운영 view의33행을 재조회했다는 의미가 아니다. 브라우저 성공 화면은 **격리 PostgreSQL 검증 데이터·실제 Supabase 아님** 배너를 표시했다. 이 fixture의13평가/12전투/11샘플 등은 테스트 연결 제외 조건을 검증하기 위한 값이며 운영300명 데이터가 아니다. 운영 데이터에 추가하지 않았다.

초기 DB 테스트 harness는 psql의 SET command tag와 초기화 중 임시 서버 readiness를 잘못 처리해 실패했다. quiet output 및 최종 DB 준비 대기로 수정한 후 위34개를 완료했다. 최종 migration SHA256은 실제 실행 결과와 일치한다.

[실행 로그·SQL 결과·화면·hash](SafeDbAggregates20261121). SQL 결과 JSON은 파일명에 ISOLATED_TEST_FIXTURE를 명시했다.

## 보존 및 파일 목록

이 작업에서 Assets/Scripts, 게임 설정, 세이브, 기존 CloudSync/LIVE 발행기·서버 실행기·시뮬레이터·패키징 코드는 수정하지 않았다. 새 Python API 외 기존 SQL/보호 INSERT 함수/혼합 view 정의도 수정하지 않았다. 기존 공개 클라이언트 설정에 새 secret을 추가하지 않았다.

수정:

- `Tools/DdaLab/database.py`
- `Tools/DdaLab/database.js`
- `Tools/DdaLab/index.html` — 서버 집계/소표본 표기
- `Tools/DdaLab/EXHIBIT.md` — 집계 API 배포 안내로 갱신
- `Tests/test-exhibit.py` — raw조회 기대를 집계RPC 기대와 명시적 혼합view fallback 검사로 변경

신규:

- `Docs/DB/migrations/20261121_exhibit_summary.sql`
- `Docs/DB/EXHIBIT_SUMMARY_API.md`
- `Tests/test-safe-db-postgres.py`
- `Tests/test-safe-db.py`
- `Tests/test-safe-db-ui.py`
- 본 보고서/검증 증거

기존 Death.png.meta와 이전 untracked 보고서는 건드리지 않았다. 구현 단계에서 main 직접 병합/추가 커밋/Push는 하지 않았다.

## 배포와 미검증

정확한 적용·사전검사·권한검사·롤백은 [DB 배포 안내](../DB/EXHIBIT_SUMMARY_API.md)를 따른다. 운영관리자가 actual view SQL과 column/PK/FK/RLS·공개범위를 확인하고 migration을 승인해 적용한 뒤, 공개키로 GET endpoint와 Lab을 확인해야 한다. 원시 SELECT grant, RLS 해제, service_role 클라이언트 배포로 해결하지 않는다.

새 RPC가 배포되지 않은 현재에도 Lab은 혼합뷰만 정상 표시할 수 있다. 실제 공개키 endpoint/운영SQL 정의·실데이터 건수·Windows/Unity LIVE 실기·최종 ZIP은 이 작업에서 미검증이다. UI의 집계 성공은 게임 INSERT 성공이 아니며 기존 송신 진단은 별도 유지한다.


## 후속 승인에 따른 GitHub 전달

사용자의 별도 승인에 따라 최신 origin/main `9d7d228`에서 `feat/dda-safe-aggregate-api-20261121`을 생성했다. 안전 집계 API의 코드/마이그레이션/가이드/테스트/검증 증거만 커밋·Push 대상으로 선별한다. 기존 Death.png.meta, 이전 전시 구현 보고서, Windows 게임/ZIP은 포함하거나 변경하지 않는다. 운영 migration은 적용하지 않고 main 병합도 수행하지 않는다. 전달 직전 Python16개/기존exhibit15개 검사 및 실제 PostgreSQL34개 결과와 최종 SQL hash 일치를 재확인했다.
