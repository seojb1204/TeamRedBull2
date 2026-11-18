# 숙련도 통계 추가 적용안 — 운영 미적용

이 문서는 신규 migration과 클라이언트의 **검토·수동 적용 절차**이다. Codex는 실제 Supabase 설정/인증키/개인 저장/운영 CloudQueue를 읽지 않았고 운영 DB에 접속하지 않았다.

## 구현과 기본 상태

`migrations/20261118_proficiency.sql`은 기존 players/battles/pattern_stats/view/RLS를 변경하지 않는다. 신규 원시 평가·전투·패턴 표본 3개 테이블과 RPC, 억제된 집계 view를 추가한다. 클라이언트의 `CloudSync.Config.proficiencySync` 기본값은 false다. 설정 파일을 자동 생성하거나 수정하지 않는다. DB 없이도 튜토리얼 초기 배정은 작동한다.

공개 UUID/anon key만으로는 소유권을 증명할 수 없다. 신규 경로는 **프로필별 무작위 256-bit capability**를 생성하고 서버가 SHA256으로 소유자 namespace를 계산한다. secret은 TLS RPC에만 전달하며 SaveData/전송 큐에는 넣지 않는다. 원시 테이블은 anon/authenticated 조회·직접 쓰기 권한과 RLS 정책이 없고, RPC만 실행할 수 있다. 동일 이벤트의 동일 payload는 성공, 수정 payload는 거부한다. 실패/409/false 응답에는 로컬 큐를 보존한다.

이는 기존 공개 UUID를 인증 계정으로 바꾸는 기능이 아니다. 같은 UUID를 아는 다른 capability는 별도 namespace이며 기존 원시 행을 덮어쓰거나 연결할 수 없다. 클라이언트가 거짓 데이터를 보내는 것, 여러 capability를 만드는 Sybil, 동일 인물 판별은 해결하지 않는다. `players`는 **서로 다른 익명 capability 소유자 수**이지 검증된 사람 수가 아니다. k=3 집계는 재식별 불가능성의 보증이 아니다.

## 로컬 소유권과 복원

- 기존 진행/아카이브/CloudQueue 포맷 및 UUID reset/restore 동작은 유지한다.
- 별도 `ProficiencyKeys/<uuid>.key`와 `.owner` marker, `ProficiencyOutbox`, `ProficiencyReceipts`를 사용한다. Linux key directory 0700/key 0600. Windows는 기존 AppData ACL에 의존하며 OS 보안 저장소 암호화는 구현하지 않았다.
- 평가 이벤트 ID는 원래 UUID+최초/재평가+완료 UTC 시각의 해시로 안정화한다. 같은 저장을 반복해도 새 평가가 아니다. 최초·재평가 원시 기록을 분리한다.
- 초기화는 새 UUID/key를 만든다. 같은 로컬 root에서 이전 프로필 복원은 원래 key와 pending/receipt를 재사용한다. 기존 CloudQueue 파일은 재라벨하지 않는다.
- **다른 PC로 progress.json만 복사하면 key는 복원되지 않는다.** keys/owner marker/receipts도 접근 통제된 백업이 필요하다. 원본 key 분실 시 기존 marker가 있는 프로필은 자동 재발급하지 않고 큐를 보존한다. 키를 함께 잃은 복사본을 원래 원격 소유자로 인증할 수 있다고 주장하지 않는다.
- 키는 bearer 권한이다. 공유·로그·Git 업로드 금지. 큐의 owner hash만으로 raw 조회/수정은 불가능하다. 로컬 기기 장악/키 탈취는 방어 범위 밖이다.

## 집계 계약

유효·완료·미스킵 **최초 평가**(schema1, confidence≥.5, active>0, 패링/회피 시도≥1)에 같은 owner/profile로 연결된 실제 보스전만 집계한다. 재평가로 최초 그룹을 덮어쓰지 않는다. battle에 당시 assessment ID를 고정하고 null/과거 미분류 자료를 나중에 소급 연결하지 않는다. 튜토리얼 보스 ID는 DB에서 거부한다.

난이도에 의해 이미 변한 성공률 혼합을 줄이기 위해 새 그룹 view는 **배정 level=0 표본만** 사용한다. 20기회·3소유자 이상만 공개한다. 보정된 모든 난이도의 표본은 private 원시 테이블에 남지만 그룹 추천에는 섞지 않는다. 따라서 초반 그룹 표본 축적이 느리거나 없을 수 있고 전체 DB→튜토리얼→기본값 fallback이 필요하다. view ±1 추천은 기존 전체 집계와 같은 .45/.85 기준이며 개인 Choose의 .45/.90 기준과 다르다.

## 수동 적용 체크리스트

1. 새 SQL과 capability/익명 데이터 처리·보존·삭제 정책을 팀에서 검토한다. 익명 프로필 수와 사람 수를 구분한다. 실제 개인정보 운영 동의/보존 정책은 이 코드로 확정하지 않았다.
2. 별도 staging Supabase에서 기존 스키마 백업 후 새 migration을 적용한다. anon/authenticated role 존재와 함수 owner 권한, public schema CREATE 권한, PostgREST RPC 노출·schema cache를 점검한다. **기존 RLS를 완화하지 않는다.**
3. 신규 테이블 raw SELECT/INSERT/UPDATE/DELETE 불가, RPC execute/집계 SELECT만 허용됨을 실제 REST에서 확인한다. private 테스트 capability로 중복·충돌·다른 owner FK를 검사한다.
4. 새 수신 view의 필드, TLS/키 헤더, bool `true` 반환을 확인한다. raw 오류 payload나 secret을 로깅하지 않는다. 기존 legacy DB 경로의 소유권 보안 문제는 이번 migration이 소급 해결하지 않는다.
5. 테스트 복사본의 설정에만 `proficiencySync:true`를 추가하고 평가→전투 순서, 오프라인 보존, 409 보존, 재시도 멱등, 초기화/복원을 확인한다. timeout은 기존 값을 재사용하며 신규 영속 캐시·자동 재시도 정책은 추가하지 않았다.
6. 평가 raw의 설명/정지/active 합, 신규/구버전 save 호환, Windows key ACL/파일 백업을 확인한 뒤 기능 활성화를 결정한다. 기존 평가의 새 elapsed 필드 0은 **미측정**이며 실제 0초로 분석하지 않는다.
7. 문제 시 flag=false로 새 전송/그룹 수신만 중단한다. 기존 게임/전체 DB/로컬 초기값은 유지한다. 신규 raw table 삭제는 별도 보존·백업 결정 후 수행한다.

## 실행된 격리 검증

`Tests/test-proficiency-sql.py`는 지정한 network=none, host mount/port 없는 disposable PostgreSQL 컨테이너에 매번 새 QA database를 만든다. 공식 PostgreSQL17 이미지의 자동 생성 data volume만 허용한다. schema 재적용·anon RPC·실제 raw 거부·FK·롤백·집계 억제·재평가/스킵/저신뢰 제외 39개를 실행했다. 이는 Supabase/PostgREST 운영 검증이 아니다.
