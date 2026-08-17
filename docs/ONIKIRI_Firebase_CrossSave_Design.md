# 귀참 키우기 — Firebase 크로스 저장 확정 설계 v1.0

기준일: 2026-08-18  
기준 세이브: `SaveData.CurrentVersion = 21`  
상태: **설계 확정 · 구현 전**  
범위: 설계 문서만. 런타임·씬·프리팹·세이브·Firebase 콘솔·보안 규칙은 수정하지 않는다.

---

## 0. 최종 결정

귀참 키우기의 크로스 저장은 다음 구조로 확정한다.

> **전체 세이브 스냅샷 + 서버 revision 낙관적 잠금 + 소프트 단일 작성 세션 + 충돌 시 한 브랜치 선택**

필드별 자동 병합은 하지 않는다. 특히 `max`, 합집합, 최신 시각을 섞어 새 세이브를
만드는 방식은 금지한다.

확정값:

| 항목 | 결정 |
|---|---|
| 클라우드의 저장 단위 | `SaveData` 전체 JSON 한 벌 |
| 서버 정본 | `playerSaves/{uid}` 한 문서 |
| 동시 쓰기 | `revision + 1` Firestore 트랜잭션 |
| 오프라인 | 로컬 저장은 계속. 클라우드 쓰기는 큐잉하지 않고 온라인 복귀 후 트랜잭션 재시도 |
| 여러 기기 | 소프트 단일 작성 세션. 동시 진행이 갈라지면 자동 병합하지 않음 |
| 충돌 | 기기 기록 또는 계정 기록 중 하나를 사용자가 선택 |
| 안전망 | 로컬 최근 3개 백업 + 클라우드 직전 revision 1개 백업 |
| 계정 연동 | 익명 계정에 Link되면 uid 유지. Recover로 uid가 바뀌면 두 세이브를 비교해 선택 |
| SaveData 버전 | **v21 유지**. 동기화 메타데이터는 별도 sidecar와 클라우드 envelope에 둠 |
| App Check | Android Play Integrity를 출시 전 활성화·강제 |
| 유료 재화 | 현재 스냅샷 저장만으로 판매 금지. IAP 전 서버 원장/영수증 검증 별도 필요 |

---

## 1. 현재 코드에서 확인된 사실

### 1.1 세이브는 이미 하나의 경제 트랜잭션이다

`GameSession.Save()`은 다음을 한 `SaveData`에 모아 원자적으로 로컬 파일로 바꿔치기한다.

- 골드·평생 골드·강화 레벨
- 현재/최고 스테이지·보스 처치 수
- 레벨·경험치·스탯 포인트
- 오의 레벨·보유·장착·XP
- 보석·퀘스트 수령 상태·일일/누적 카운터
- 장비 등급·단련
- 귀문 승급 티어
- 동료·요도·요도 뽑기·오의 뽑기·각 천장
- 온보딩 무료 10연 수령·첫 장착
- 닉네임·방치 보상 기준 시각과 수입률

보석을 쓴 결과와 뽑기 보상, 천장 카운터가 같은 스냅샷에 있다. 장비 등급과 보석,
퀘스트 수령 표시와 보석도 같은 이유로 묶여 있다. 이 중 일부만 다른 기기 값과
합치면 지불 전 지갑과 지불 후 상품을 동시에 얻을 수 있다.

### 1.2 `evolutionTier`의 옛 전제는 폐기한다

현재 v21에서 `evolutionTier`는 `EvolutionSystem.GrantTrialVictory()`가 귀문 승리 후
무료로 올린다. 보석·골드를 지불하는 `TryEvolve` 경로는 삭제됐다.

따라서 과거 문서의 다음 전제는 크로스 저장 설계에 사용하지 않는다.

```
evolutionTier 상승 + gems 차감 + gold 차감 = 단일 전직 구매 트랜잭션
```

현재의 정확한 계약은 다음이다.

```
귀문 승리 -> evolutionTier 단조 증가
```

단, `evolutionTier`만 `max` 병합하지는 않는다. 높은 승급 티어와 다른 브랜치의
지불 전 재화를 섞으면 전체 경제가 다시 복제되기 때문이다.

### 1.3 기존 계정 복구는 게임 세이브를 복구하지 않는다

`AccountLink`의 현재 병합은 리더보드 `maxStage`만 대상으로 한다.

```
mergedStage = max(local, abandoned score document, recovered score document)
```

이 값은 랭킹 기록일 뿐 `SaveData`가 아니다. 크로스 저장 구현 뒤에도 리더보드 병합은
그대로 둘 수 있지만, 그 결과를 게임 진행 복구에 사용해서는 안 된다.

---

## 2. 왜 필드별 병합을 금지하는가

### 2.1 대표적인 복제 사례

기기 A:

```
보석 1,000 -> 10연 뽑기 -> 보석 775 + 신규 오의 + 천장 10
```

기기 B:

```
오래된 세이브 -> 보석 1,000 + 신규 오의 없음 + 천장 0
```

`gems=max`, 보유 오의=합집합, 천장=max로 합치면:

```
보석 1,000 + 신규 오의 + 천장 10
```

지불 전 지갑과 지불 후 상품이 동시에 남는다. 장비·동료 해금·요도·퀘스트 보상도
같은 문제가 생긴다.

### 2.2 단조 필드도 따로 합치지 않는다

`maxStageReached`, `evolutionTier`, 누적 퀘스트, 보유 목록처럼 겉으로 안전해 보이는
필드도 전체 브랜치 밖에서 합치지 않는다. 단조 진행은 그 과정에서 받은 보상과 쓴
재화에 연결돼 있기 때문이다.

**v1의 병합 함수는 존재하지 않는다.** 같은 스냅샷인지 비교하거나, 한 브랜치를
고르는 함수만 존재한다.

---

## 3. 저장 구조

### 3.1 Firestore 컬렉션

```
playerSaves/{uid}          현재 정본
playerSaveBackups/{uid}   직전 정본 한 벌
playerSaveSessions/{uid}  현재 작성 세션(소프트 임대)
```

리더보드 `scores/{uid}`와 분리한다. 랭킹은 공개 읽기지만 세이브는 소유자만 읽을 수
있어야 하며, 보안 규칙과 수명도 다르다.

### 3.2 `playerSaves/{uid}` envelope

| 필드 | 형식 | 의미 |
|---|---|---|
| `formatVersion` | int | 클라우드 envelope 형식. 최초 1 |
| `saveVersion` | int | payload의 `SaveData.version` |
| `revision` | long | 서버 정본 revision. 최초 1, 이후 정확히 +1 |
| `baseRevision` | long | 이 쓰기가 읽고 출발한 revision |
| `payload` | string | `JsonUtility.ToJson(SaveData, false)` 전체 |
| `payloadSha256` | string | payload UTF-8 SHA-256 |
| `stateSha256` | string | 시간성 필드를 제외한 게임 상태 지문 |
| `lastMutationId` | string | 응답 유실 뒤 재시도 판별용 GUID |
| `sessionId` | string | 이 쓰기를 만든 실행 세션 |
| `deviceId` | string | 진단·충돌 화면용 설치 ID. 인증 수단 아님 |
| `updatedAt` | server timestamp | 서버가 확정한 최종 갱신 시각 |
| `summary` | map | 충돌 화면에 표시할 최소 요약 |

`summary`에는 아래 값만 둔다.

- `maxStageReached`
- `characterLevel`
- `evolutionTier`
- `gems`
- `gachaTotalPulls`
- `skillGachaTotalPulls`

서버에서 payload를 열지 않고 두 기록을 비교해서 보여주기 위한 값이다. 복원 계산에는
쓰지 않는다.

현재 로컬 세이브는 약 4KB다. 그래도 payload 상한은 **200KB**로 고정한다. Firestore
문서 상한 1MiB에 기대어 무제한으로 두지 않는다. `payload`와 해시·요약 필드는 쿼리하지
않으므로 단일 필드 인덱스를 끈다.

### 3.3 `playerSaveSessions/{uid}`

| 필드 | 의미 |
|---|---|
| `sessionId` | 앱 실행마다 새 GUID |
| `deviceId` | 설치별 GUID |
| `heartbeatAt` | server timestamp |
| `released` | 정상 백그라운드 전환 시 true |

- 클라우드 저장 주기: 최대 120초
- 세션 만료: 마지막 heartbeat 뒤 180초
- 앱 일시정지 시 저장과 함께 release를 시도
- 세션은 충돌을 줄이는 UX 장치이고 데이터 정합성의 마지막 방어선은 revision이다

기기가 오프라인이면 이전 세션은 만료될 수 있다. 그 상태에서 두 기기가 각각 진행하는
것은 완전히 막을 수 없으며, 재접속 시 revision 충돌로 드러내야 한다.

### 3.4 로컬 sidecar

`onikiri_cloud_state.json`을 별도 원자 파일로 둔다.

```text
formatVersion
ownerUid
baseRevision
lastSyncedPayloadSha256
lastSyncedStateSha256
pendingMutationId
pendingPayloadSha256
deviceId
lastKnownServerUpdatedAt
```

`SaveData`에는 동기화 구현 세부사항을 넣지 않는다. 따라서 `SaveData.CurrentVersion`은
21을 유지한다. sidecar가 없어도 로컬 세이브는 정상 플레이 가능해야 한다.

로컬 저장 순서:

1. `onikiri_save.json`을 현재 방식대로 임시 파일 후 바꿔치기
2. 동기화가 필요하면 pending mutation을 sidecar에 먼저 기록
3. 서버 트랜잭션 수행
4. 성공 확인 뒤 sidecar의 base revision과 해시 갱신

서버 커밋은 성공했지만 응답 전에 앱이 죽은 경우, 다음 실행에서 서버의
`lastMutationId`가 pending 값과 같은지 확인한다. 같으면 중복 revision을 만들지 않고
성공으로 복구한다.

---

## 4. 해시 두 종류

### 4.1 `payloadSha256`

전체 JSON을 검증한다. 다운로드 뒤 해시가 다르면 절대 적용하지 않는다.

### 4.2 `stateSha256`

자동 저장만으로 매번 충돌하는 것을 막는 게임 상태 지문이다. 다음 세 필드는 0으로
정규화한 복사본을 직렬화해 계산한다.

- `lastQuitUtcTicks`
- `goldPerSecond`
- `expPerSecond`

나머지 필드는 전부 포함한다. 골드·경험치·현재 처치 수도 실제 진행이므로 제외하지 않는다.

배열은 현재 카탈로그/id 수집 순서를 그대로 사용한다. 구현 시 같은 상태가 같은 JSON을
내는 결정론 검사를 먼저 둔다.

---

## 5. 서버 쓰기 프로토콜

일반 `SetAsync`와 Firestore 오프라인 큐를 세이브 정본 쓰기에 사용하지 않는다.
Firestore 오프라인 동기화는 같은 문서의 여러 변경을 last-write-wins로 처리하므로
오래된 기기가 나중에 최신 세이브를 덮을 수 있다.

클라우드 저장은 반드시 온라인 Firestore transaction으로 한다.

```text
1. playerSaveSessions/{uid}를 읽어 현재 sessionId가 작성권을 가졌는지 확인
2. playerSaves/{uid}를 서버에서 읽음
3. lastMutationId가 pendingMutationId와 같으면 이미 성공한 쓰기로 종료
4. 서버 revision != 로컬 baseRevision이면 Conflict
5. 기존 정본을 playerSaveBackups/{uid}에 복사
6. 새 정본을 revision + 1, baseRevision = 기존 revision으로 기록
7. updatedAt은 서버 타임스탬프
8. 트랜잭션 성공 뒤에만 로컬 sidecar 갱신
```

트랜잭션은 오프라인에서 실패하는 것이 정상이다. 실패 시 로컬 세이브를 유지하고
`로컬 저장됨 · 클라우드 대기` 상태로 둔다. 네트워크 복귀 후 같은 mutation ID로
재시도한다.

---

## 6. 부팅 시 선택 정책

클라우드 세이브를 `GameSession.Apply()` 뒤에 덮어씌우면 방치 보상이 두 번 지급될 수
있다. 따라서 **로컬/클라우드 선택을 먼저 끝내고 선택된 SaveData를 딱 한 번 Apply**한다.

부팅 순서:

```text
로컬 JSON 읽기
  -> sidecar 읽기
  -> Firebase/Auth 서버 확인(시간 제한 있음)
  -> 로컬/클라우드 정책 판정
  -> 선택된 SaveData 한 벌 확정
  -> GameSession.Apply 1회
  -> 선택된 세이브 기준 방치 보상 1회
  -> 즉시 로컬 저장
  -> 게임 진입
```

Firebase는 무한 로딩 게이트가 아니다. 온라인 확인이 제한 시간 안에 끝나지 않으면
로컬로 진입하고 `LocalOnly/Dirty`로 둔다. 이후 서버가 돌아왔을 때 다른 revision이
발견되면 전투 중 핫스왑하지 않고 충돌 화면을 연다.

### 6.1 자동 판정표

| 로컬 | 클라우드 | 판정 |
|---|---|---|
| 없음/손상 | 유효 | 클라우드 자동 복구 |
| 유효 | 없음 | 로컬 사용, 온라인일 때 revision 1 생성 |
| 해시 동일 | 유효 | 같은 기록. 높은 서버 revision을 sidecar에 채택 |
| 로컬 state가 마지막 동기화와 동일 | 서버 revision 증가 | 클라우드 자동 적용 |
| 서버 revision = 로컬 base, 로컬 변경 | 로컬 사용 후 업로드 |
| 서버 revision 증가 + 로컬 state도 변경 | **충돌 화면** |
| 서버 revision < 로컬 base | 서버 롤백/메타 손상. 자동 쓰기 금지, 충돌 화면 |
| 클라우드 saveVersion > 현재 앱 | 적용·업로드 금지, 앱 업데이트 안내 |
| payload 해시 불일치/JSON 손상 | 적용 금지, 클라우드 백업 복구 안내 |

`lastQuitUtcTicks`나 기기 시각으로 어느 쪽이 최신인지 결정하지 않는다. 기기 시각은
사용자가 바꿀 수 있고 revision보다 약한 증거다.

---

## 7. 충돌 UX

충돌에서는 자동 병합 버튼을 제공하지 않는다.

```text
계정 기록이 두 곳에서 변경되었습니다

[현재 기기 기록]
최고 82스테이지 · Lv.48 · 검귀 · 보석 320

[클라우드 기록]
최고 79스테이지 · Lv.51 · 검객 · 보석 540
서버 저장 8월 18일 06:12

[클라우드 기록 사용]  [현재 기기 기록 사용]
[나중에 결정]
```

- 어느 쪽도 단순히 더 좋다고 자동 추천하지 않는다. 높은 스테이지가 희귀 오의·보석까지
  더 낫다는 보장은 없다.
- `나중에 결정`은 현재 기기로 로컬 플레이만 허용하고 클라우드 쓰기를 멈춘다.
- 클라우드를 선택하기 전 현재 로컬을 timestamp 백업한다.
- 현재 기기를 선택하면 기존 서버 정본을 backup 문서로 옮긴 뒤 새 revision으로 올린다.
- 선택 뒤에는 Main 씬을 다시 로드해 `GameSession.Apply`와 방치 보상을 처음부터 한 번만
  실행한다. 전투 중 상태를 부분적으로 Restore하지 않는다.

로컬 백업은 최근 3개만 순환 보관한다.

```text
onikiri_save.precloud.1.json
onikiri_save.precloud.2.json
onikiri_save.precloud.3.json
```

---

## 8. 계정 연동·복구 정책

### 8.1 Link — uid 유지

익명 uid에 Google 자격 증명이 처음 붙으면 현재 코드처럼 uid가 유지된다.

- 같은 `playerSaves/{uid}`를 계속 사용
- sidecar `ownerUid`도 그대로
- 로컬이 dirty면 정상 업로드
- 별도 병합 없음

### 8.2 Recover — uid 교체

재설치 후 이미 존재하는 Google 계정으로 복구하면 익명 uid에서 기존 uid로 바뀐다.

이때 현재 `AccountLink`의 `maxStage` 병합을 세이브에 확대 적용하지 않는다.

1. uid 교체 전 현재 로컬 SaveData를 복구 후보로 메모리/백업에 보존
2. 복구 로그인 성공 뒤 기존 uid의 `playerSaves`를 서버에서 읽음
3. 기존 계정 세이브가 없으면 현재 로컬을 새 계정의 revision 1로 채택
4. 둘 다 있으면 해시가 같을 때만 자동 채택
5. 다르면 충돌 화면에서 한 브랜치 선택
6. 버려진 익명 save 문서는 클라이언트가 삭제하지 않음

리더보드 `maxStage`는 지금처럼 별도로 max 병합할 수 있다. 다만 랭킹 값과 실제
세이브의 `maxStageReached`가 잠시 다를 수 있음을 UI와 코드가 인정해야 한다.

---

## 9. 동기화 시점과 쓰기 비용

로컬 저장 주기는 현재 30초를 유지한다. 모든 로컬 저장을 곧바로 Firestore write로
바꾸지 않는다.

클라우드 정책:

- 일반 진행: dirty 상태에서 최대 120초 debounce
- 앱 백그라운드 전환: 로컬 즉시 저장 + 가능한 경우 클라우드 저장/release 시도
- 앱 복귀: 세션 재획득 후 서버 revision 확인
- 귀문 승리, 유료 보석 소비가 수반된 뽑기, 무료 10연 수령, 동료/장비 보석 구매:
  온라인이면 urgent sync 예약
- 오프라인이면 어떤 기능도 멈추지 않고 로컬 dirty로 남김

OnApplicationQuit의 네트워크 완료는 보장으로 세지 않는다. 모바일의 실제 종료 경로는
Pause/Focus이며, 그 전에 주기 동기화가 서 있어야 한다.

설정 화면에 네 상태만 표시한다.

- `클라우드 저장 완료`
- `기기에 저장됨 · 연결되면 동기화`
- `다른 기기에서 플레이 중`
- `기록 선택 필요`

---

## 10. 보안 규칙

`scores/{uid}`의 공개 읽기 규칙을 세이브에 복사하지 않는다.

`playerSaves/{uid}`:

- read/create/update: `request.auth.uid == uid`
- delete: false
- 허용 필드만 존재
- create revision은 정확히 1, baseRevision은 0
- update는 `revision == resource.revision + 1`
- update는 `baseRevision == resource.revision`
- `updatedAt == request.time`
- `saveVersion`과 `formatVersion`은 유효 범위
- `payload`는 string이며 200KB 이하
- summary 타입·범위 검사
- session 문서의 현재 `sessionId`와 save write의 `sessionId` 일치

`playerSaveBackups/{uid}`와 `playerSaveSessions/{uid}`도 소유자만 읽고 쓴다. delete는
계정 삭제 서버 경로 전까지 막는다.

Security Rules는 payload 안의 게임 경제가 진짜인지 증명하지 못한다. Auth와 Rules는
남의 저장 접근과 오래된 revision 덮어쓰기를 막는 층이고, 개조 클라이언트의 조작을
막는 서버 검증은 아니다.

---

## 11. App Check와 출시 경계

현재 보고서에는 App Check provider가 없어 placeholder 경고가 난 기록이 있다.
크로스 저장을 출시하기 전에 다음 순서를 지킨다.

1. Editor/개발 빌드: Debug App Check provider
2. Android 릴리스: Play Integrity provider를 Firebase 서비스 생성 전에 초기화
3. 실제 배포 빌드에서 토큰 수신 확인
4. 모니터링 기간 뒤 Firestore App Check enforcement 활성화

App Check는 공식 앱에서 온 요청인지 확인하는 층이다. 로그인 소유권, revision 규칙,
게임 경제 검증을 대신하지 않는다.

### 유료 보석 출시 전 필수

현재 `SaveData.gems`는 클라이언트가 만든 JSON 안에 있다. 이 값을 그대로 크로스 저장한다고
해서 유료 재화 원장이 되지는 않는다.

보석을 현금으로 판매하기 전에는 최소한 다음을 별도 구현한다.

- 스토어 영수증 서버 검증
- 구매 idempotency key
- 유료 지급 내역 서버 원장
- 필요 시 무료/유료 보석 분리
- 계정 삭제·환불 처리

따라서 이 설계는 **게임 진행 크로스 저장의 확정안**이며, IAP 서버 경제의 대체물이 아니다.

---

## 12. 구현 구성

| 구성 | 책임 |
|---|---|
| `FirebaseRuntime` | Firebase 초기화·Auth 공유. `CloudScores`가 맡던 공통 기반을 분리 |
| `CloudSaveEnvelope` | Firestore 문서 DTO |
| `CloudSaveLocalState` | sidecar DTO·원자 저장 |
| `CloudSaveFingerprint` | payload/state SHA-256·정규화 |
| `CloudSavePolicy` | 순수 판정: upload/download/conflict/block |
| `CloudSaveStore` | 서버 read·revision transaction·backup |
| `CloudSaveSession` | session 획득·heartbeat·release |
| `CloudSaveCoordinator` | 부팅 선택·GameSession 연결·debounce·상태기계 |
| `CloudSaveConflictPanel` | 두 브랜치 비교·선택·백업·씬 재로드 |

상태기계:

```text
Bootstrapping
 -> LocalOnly       서버 확인 불가
 -> InSync          같은 revision/hash
 -> Dirty           로컬 변경
 -> Uploading
 -> InSync          성공
 -> Conflict        revision 충돌
 -> Blocked         미래 버전/손상/권한 문제
```

`CloudScores`의 오프라인 write 큐 정책은 리더보드에만 유지한다. `CloudSaveStore`는
코드를 공유하지 않고 transaction 실패를 로컬 dirty로 되돌린다.

---

## 13. 구현 단계

### 1단계 — 순수 정책과 로컬 포맷

- envelope/sidecar/fingerprint/policy
- SaveData v21 전체 직렬화 왕복
- 코드·씬에서 Firebase write 없음

### 2단계 — Firestore 저장소와 규칙

- `playerSaves`, `playerSaveBackups`, `playerSaveSessions`
- Emulator 또는 개발 프로젝트에서 rules test
- stale revision·남의 uid·delete·future version 거부

### 3단계 — 부팅 선택과 단일 Apply

- GameSession이 스스로 즉시 Apply하지 않고 Coordinator가 고른 데이터 한 벌만 적용
- 오프라인 시간 제한·로컬 fallback
- 방치 보상 1회 계약

### 4단계 — 자동 동기화와 충돌 UI

- 120초 debounce·urgent sync·pause release
- 로컬 3벌/클라우드 1벌 백업
- 전투 중 hot swap 금지·선택 뒤 씬 재로드

### 5단계 — 계정 Link/Recover 통합

- uid 유지/교체 두 경로
- Recover에서 전체 세이브 선택
- 리더보드 max 병합과 세이브 선택 분리

### 6단계 — App Check·두 기기 실기

- Play Integrity
- 실제 Android 두 기기 또는 한 기기+에디터 충돌 실측
- 출시 규칙 배포 전 최종 확인

---

## 14. 필수 테스트

### EditMode

- SaveData v21의 모든 필드 JSON 왕복
- 같은 상태는 같은 payload/state hash
- 시간성 세 필드만 바뀌면 state hash 동일
- 서버 revision = base + 로컬 변경 -> Upload
- 서버 revision 증가 + 로컬 clean -> Download
- 서버 revision 증가 + 로컬 dirty -> Conflict
- payload hash 불일치 -> Blocked
- 미래 saveVersion -> Blocked
- `lastMutationId` 동일 -> 이미 성공으로 복구
- **필드별 max/합집합 병합 API가 존재하지 않음**

### Firestore Rules/통합

- 소유자만 read/write
- create revision 1만 허용
- stale baseRevision 거부
- revision 건너뛰기 거부
- delete 거부
- 서버 timestamp 아닌 updatedAt 거부
- payload 200KB 초과 거부
- 현재 session이 아닌 write 거부
- canonical+backup transaction 원자성

### PlayMode

- 선택된 세이브만 GameSession.Apply 1회
- 방치 보상 1회
- 클라우드 적용 전 로컬 백업 생성
- 충돌 선택 후 씬 재로드로 완전 복원
- Firebase 실패가 전투와 로컬 저장을 막지 않음

### Android 실기

1. 기기 A 진행 -> 기기 B 로그인 -> 같은 세이브 복원
2. 기기 B 진행 -> A 재실행 -> 새 revision 다운로드
3. A 오프라인 진행 + B 온라인 진행 -> A 복귀 시 충돌 화면
4. Link 성공(uid 유지) -> 세이브 문서 경로 유지
5. Recover 성공(uid 교체) -> 두 브랜치 비교
6. 앱 데이터 삭제 -> Google 복구 -> 클라우드 세이브 복원
7. 서버 커밋 직후 응답 유실 -> mutation ID로 중복 revision 방지
8. 미래 버전 cloud save -> 로컬/서버 모두 덮어쓰지 않음

---

## 15. 완료 조건

다음을 모두 만족할 때만 크로스 저장 완료로 판정한다.

1. 오프라인 플레이와 로컬 저장이 Firebase 상태와 무관하게 계속된다.
2. 오래된 기기가 새 revision을 조용히 덮을 수 없다.
3. 자동 필드 병합으로 재화·상품이 복제되지 않는다.
4. 같은 서버 커밋을 응답 유실 때문에 두 번 올리지 않는다.
5. 클라우드 적용 전 로컬 기록과 서버 직전 기록이 각각 복구 가능하다.
6. 부팅과 계정 Recover에서 SaveData가 정확히 한 번만 Apply된다.
7. 방치 보상이 정확히 한 번만 지급된다.
8. 계정 Link는 uid와 저장 경로를 유지한다.
9. 계정 Recover는 명시적 브랜치 선택 없이는 서로 다른 저장을 덮지 않는다.
10. App Check Play Integrity와 소유자/revision 보안 규칙이 실기에서 통과한다.

---

## 16. 최종 판정

이 설계는 구현 시작 가능하다.

구현 중 다시 기획 승인을 받아야 하는 값은 하나뿐이다.

- **충돌 화면에서 현재 기기 기록과 클라우드 기록 중 어느 쪽도 자동 추천하지 않는다.**

나머지 핵심 정책 — 전체 스냅샷, 필드 병합 금지, revision transaction, 오프라인 로컬
우선, 브랜치 선택, uid Recover 처리, v21 유지 — 은 확정한다.

### 공식 근거

- Firestore transaction은 동시 수정 시 재시도되고 성공한 쓰기는 원자적이며, 오프라인에서는 실패한다: https://firebase.google.com/docs/firestore/manage-data/transactions
- Firestore 일반 오프라인 동기화는 같은 문서에 대해 last-write-wins다: https://firebase.google.com/docs/firestore/manage-data/enable-offline
- Firestore 문서 최대 크기는 1MiB다: https://firebase.google.com/docs/firestore/quotas
- Unity App Check의 Android 기본 provider는 Play Integrity다: https://firebase.google.com/docs/app-check/unity/default-providers
