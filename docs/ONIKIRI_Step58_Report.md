# ONIKIRI 58단계 — 크로스 저장 2단계 (Firestore 저장소 · revision 트랜잭션 · 보안 규칙)

> 57단계가 확정한 계약(봉투·지문·sidecar·판정)을 **서버에 붙인다.** 정본 컬렉션
> 셋, revision 낙관적 잠금 트랜잭션, 소프트 작성 세션, 그리고 그 전부를 서버에서
> 한 겹 더 강제하는 보안 규칙까지.
>
> **운영 프로젝트(onikiri-9cc18)에 배포하지 않았다.** 검증은 Firestore Emulator에서
> 끝냈고, 규칙 파일은 저장소에만 있다.

---

## 0. 한 줄 요약

세이브 정본이 `playerSaves/{uid}` 문서 하나가 되고, 그 문서는 **오직 한
트랜잭션 안에서** 직전 정본을 백업으로 옮긴 뒤에야 revision + 1로 교체된다.
같은 검사를 클라이언트(`CloudSaveStore`)와 보안 규칙이 각각 한 벌씩 들고 있고,
**둘의 상수가 갈리지 않는지는 EditMode가 규칙 파일을 읽어 대조한다.**

Emulator 규칙 테스트 **47/47**, EditMode **856/856**(기존 837 + 신규 19).
Firebase 운영 배포 **0**, SaveData **v21 그대로**, 씬·프리팹·`GameSession`
**무수정**, 커밋·푸시 **없음**.

---

## 1. 변경 파일

| 파일 | 상태 | 역할 |
|---|---|---|
| `Scripts/Cloud/FirebaseRuntime.cs` | 신규 | 초기화 공용 입구(최소 범위) · 타임아웃 · 오프라인/거부 분류 · Timestamp→ticks |
| `Scripts/Cloud/CloudSaveDocument.cs` | 신규 | 봉투 ↔ Firestore 필드 맵. **Firebase 타입이 없다**(EditMode가 잰다) |
| `Scripts/Cloud/CloudSaveStore.cs` | 신규 | 정본 읽기(Source.Server) · revision 트랜잭션 · 백업 복사 · 결과 8종 |
| `Scripts/Cloud/CloudSaveSession.cs` | 신규 | 세션 획득·heartbeat·release (트랜잭션) |
| `Scripts/Cloud/CloudSavePolicy.cs` | 확장 | `CloudSaveSessionClaim` · `ClaimFor()` (순수 판단) |
| `Tests/EditMode/CloudSaveStoreTests.cs` | 신규 | 19개 (문서 매핑 · **규칙 대조** · 세션 판단 · 결과 계약) |
| `Editor/OnikiriTestPanel.cs` | 확장 | "크로스 저장 (2단계 - 저장소·세션)" 절 |
| `firestore.rules` | 확장 | 새 컬렉션 셋. **`scores` 4-B 계약은 한 줄도 안 건드렸다** |
| `firebase.json` | 확장 | `emulators` 블록(포트 8089). 배포 설정 변경 없음 |
| `tools/firestore-rules-tests/` | 신규 | 규칙 검증 하네스(개발 의존성만, `node_modules`는 gitignore) |
| `.gitignore` | 확장 | 하네스 의존성·에뮬레이터 로그 |

**손대지 않은 것:** `SaveData`(v21) · `SaveSystem` · `GameSession` · 씬 · 프리팹 ·
`CloudScores`(리더보드 오프라인 큐 정책 포함) · `AccountLink` · `firestore.indexes.json`.

`CloudScores`를 고치지 않은 것이 의도다. 그 코드는 실기에서 증명된 부팅
경로이고(53~55단계), 크로스 저장이 자기 초기화를 새로 만들면 **한 앱에 Firebase
부팅 경로가 둘**이 되어 uid가 갈리거나 이중 초기화가 난다. `FirebaseRuntime`은
그 경로를 **부르기만** 한다.

---

## 2. 트랜잭션 계약 [C-1]

일반 `SetAsync`/`UpdateAsync` 단독 호출과 Firestore 오프라인 큐를 **정본에
쓰지 않는다.** 그 경로는 같은 문서에 대해 last-write-wins라, 오프라인으로 논
오래된 기기가 나중에 연결되는 순간 최신 진행을 조용히 덮는다.

```
[0] 로컬  MarkPending → CloudSaveSidecar.Save() == true 확인   ← 실패하면 여기서 멈춘다
[1] 서버  playerSaveSessions/{uid} 읽기 → 내 세션인가 · released가 아닌가
[2]       playerSaves/{uid} 읽기
[3]       문서 없음 + sidecar revision > 0  → 차단
[4]       서버 lastMutationId == pending    → AlreadyApplied (revision 안 올림)
[5]       서버 revision != baseRevision     → Conflict
[6]       직전 정본을 playerSaveBackups/{uid}에 **그대로** 복사
[7]       playerSaves/{uid}에 revision + 1 기록 · updatedAt = 서버 타임스탬프
[8] 로컬  성공을 확인한 뒤에만 MarkSynced + Save
```

[1]~[7]이 **한 트랜잭션 안**이다. [0]이 밖에 있는 것이 57단계의 계약이다 —
서버는 커밋했는데 그 mutation id가 로컬 어디에도 없으면 다음 실행이 같은 쓰기를
다시 올려 백업 한 벌을 밀어낸다.

### 상태별 결과표

| 결과 | 언제 | 로컬이 하는 일 |
|---|---|---|
| `Committed` | [7]까지 통과 | sidecar를 새 revision으로 민다 (pending 지움) |
| `AlreadyApplied` | 서버 `lastMutationId` == pending | **revision을 더 올리지 않고** 성공으로 복구 |
| `Conflict` | 서버 revision != baseRevision | pending 유지. 사람이 브랜치를 고른다(4단계 UI) |
| `Busy` | 세션이 없거나·남의 세션이거나·released | pending 유지. 잠시 뒤 다시 |
| `Invalid` | sidecar 불일치 · 봉투 생성 실패 · **문서 소실(MissingServerAfterSync)** | 아무것도 쓰지 않는다 |
| `Offline` | 트랜잭션 시간 초과 · `Unavailable`/`DeadlineExceeded` | 로컬 dirty 유지 (실패가 아니라 정상 경로) |
| `Failed` | 규칙 거부 · 그 밖의 예외 · **sidecar 영속 실패** | 로컬 dirty 유지 |
| `Missing` / `Found` | 읽기 경로 | 57단계 판정표의 입력이 된다 |

**성공한 둘만 pending을 지운다**(`CloudSaveCommitResult.IsSynced`). EditMode가
전 상태를 훑어 그것을 못 박는다(`OnlyASyncedResultMayClearThePending`) — 여기가
틀리면 지하철에서 한 번 실패한 저장이 영원히 안 올라가고 아무도 모른다.

최초 생성은 `revision = 1, baseRevision = 0`만이다. **sidecar가 revision을 들고
있는데 서버 문서가 없으면 새로 만들지 않는다**(`MissingServerAfterSync`) — 사슬을
1로 되돌리면 다른 기기의 base가 갈 곳을 잃는다. 진짜 원인은 대개 로그인이
갈린 것이다.

---

## 3. 문서 세 벌

| 컬렉션 | 내용 | 읽기 |
|---|---|---|
| `playerSaves/{uid}` | 정본 봉투 12필드 | 소유자만 |
| `playerSaveBackups/{uid}` | **직전 정본 한 벌 그대로** | 소유자만 |
| `playerSaveSessions/{uid}` | `sessionId` · `deviceId` · `heartbeatAt` · `released` | 소유자만 |

랭킹(`scores`)과 갈라 둔 이유는 읽기 권한이다 — 저쪽은 공개 읽기이고 값이
하나뿐이지만, 이쪽은 진행 전체가 들어 있다.

`updatedAt`만 클라이언트가 만들지 않는다. `CloudSaveDocument.ToFields()`는 **11개
필드만** 만들고, `CloudSaveStore`가 그 자리에 `FieldValue.ServerTimestamp`를 넣는다.
규칙이 `updatedAt == request.time`을 요구하므로 클라 시각은 애초에 통과하지 못한다
(EditMode `TheClientNeverWritesTheServerTimestamp`).

서버에서 받은 문서는 **`CloudSaveEnvelope.Validate()`(14겹)를 지나기 전에는 쓰지
못한다.** 규칙은 우리가 쓴 문서만 지키기 때문이다 — 콘솔에서 손댄 문서, 옛 앱이
남긴 문서, 전송 중 잘린 문서는 규칙을 지나온 적이 없다.

---

## 4. 정본 + 백업의 원자성 [C-2]

두 문서가 **같은 요청 안에서만** 함께 움직인다. 규칙이 양쪽에서 그것을 잠근다:

```
playerSaves        update: getAfter(playerSaveBackups/{uid}).data == resource.data
                           (= 백업이 "지금 교체되는 그 정본"과 정확히 같아야 한다)

playerSaveBackups  write : request.resource.data == get(playerSaves/{uid}).data
                        && getAfter(playerSaves/{uid}).data.revision
                             == get(playerSaves/{uid}).data.revision + 1
                           (= 정본이 실제로 한 칸 나아가는 요청에서만)
```

두 줄이 짝이다. 앞줄이 **백업 없는 덮어쓰기**를 막고, 뒷줄이 **백업만 조용히
바꾸는 것**을 막는다. Emulator 증거:

| 검사 | 결과 |
|---|---|
| 정본 교체 + 백업 복사를 한 배치로 | 통과, `revision 5 → 6` · 백업 = 옛 정본(rev 5) |
| 정본만 갱신 | **거부** |
| 백업만 조작 (옛 정본 복사본으로도, 지어낸 값으로도) | **거부** |
| 백업이 교체되는 정본과 한 글자라도 다름 | **거부** |
| 거부된 교체 뒤 상태 | 정본 rev 5 **무변** · 백업 **없음**(무변) |

---

## 5. 작성 세션

```
획득    자리가 비었다 · released == true · heartbeatAt이 180초 이상 조용하다
갱신    이미 내 sessionId다 (heartbeat)
거절    그 밖 = 다른 기기가 살아 있다 (Busy)
release 내 세션일 때만 released = true. 남의 자리를 놓아주지 않는다
```

- `heartbeatAt`은 **서버 타임스탬프**여야 한다(`== request.time`). 기기 시각을
  허용하면 미래로 적힌 heartbeat가 그 세션을 영원히 살아 있게 만든다 — Emulator가
  그 시도를 거부하는 것을 확인했다.
- 180초는 `CloudSavePolicy.SessionExpirySeconds`와 **같은 값**이고, 규칙 안의
  `duration.value(180, 's')`와 갈리지 않는지는 EditMode가 규칙 파일을 읽어 대조한다.
- 클라의 만료 판단(`ClaimFor`)은 **기기 시계 기준의 추정**이다. 최종 판정은 서버
  규칙이다 — 미래로 어긋난 heartbeat는 "살아 있음"으로 본다(남의 세션을 뺏는
  것보다 안전하다).
- heartbeat/release는 **내 세션일 때만** 쓴다. 만료된 남의 자리를 heartbeat로
  조용히 가져가면, 코디네이터는 자기가 언제 작성권을 얻었는지 모르는 채 쓰기를 시작한다.

세션은 **정합성의 방어선이 아니다.** 마지막 방어선은 언제나 revision 트랜잭션이고,
이것은 충돌 화면을 덜 보게 하는 UX 장치다.

---

## 6. Emulator 검증 — 47/47

`tools/firestore-rules-tests` · Firestore Emulator(포트 8089) · 프로젝트
`demo-onikiri`(실 프로젝트에 절대 닿지 않는 데모 id) · Unity 번들 JDK 17.

```
npm --prefix tools/firestore-rules-tests test
```

| 묶음 | 검사 | 통과 |
|---|---|---|
| 소유권 | 소유자 read/write · 타 uid 거부 · 미인증 거부 · delete 거부 | 4 |
| revision 사슬 | 첫 정본 1 · 첫 정본 2 거부 · baseRevision 0 강제 · stale 거부 · 건너뛰기 거부 · 같은 revision 재쓰기 거부 · 정확히 +1 통과 · **커밋된 mutation id 확인** | 8 |
| 원자성 | 4절의 다섯 줄 | 5 |
| 봉투 형식 | 클라 시각 · 미래 saveVersion · saveVersion 0 · 형식 2 · 200KB 초과 · 빈 payload · 지문 형식 · 대문자 지문 · 쓰기 id · 기기 id · 요약 범위 · 음수 보석 · 요약 누락 · 요약 임의 필드 · 봉투 임의 필드 · 필드 누락 | 16 |
| 작성 세션 | 세션 없음 · 다른 세션 · released · 최초 생성 · 갱신 · 살아 있는 자리 인수 거부 · **만료 인수 성공** · released 인수 성공 · 미래 heartbeat 거부 · 임의 필드 | 10 |
| 랭킹 회귀 | 공개 읽기 · 자기 기록 쓰기 · **단조 증가** · 남의 기록 거부 | 4 |

두 항목은 규칙이 아니라 클라이언트의 계약이라 EditMode가 맡는다:

- **같은 mutation 재시도 → AlreadyApplied**: 서버는 사슬로 그 재시도를 거부하고
  (Emulator 확인), 그 앞에서 멈추는 것이 `CloudSaveStore`의 [4]다. 서버가 마지막
  쓰기의 `lastMutationId`를 그대로 들고 있다는 것까지 Emulator가 확인한다.
- **네트워크 불가 시 pending 유지**: `IsSynced`가 아닌 모든 결과가 pending을
  남긴다(`OnlyASyncedResultMayClearThePending`), 그리고 sidecar 영속 실패 시
  서버 쓰기를 시작조차 하지 않는다(57단계 `AFailedSidecarWriteStopsTheServerWrite`).

---

## 7. 두 곳에 적힌 것은 **테스트가 맞춘다**

규칙과 코드가 같은 숫자·같은 이름을 각자 들고 있다. 갈리면 증상은 "저장이 조용히
안 된다" 하나이고 원인은 서버 로그에도 안 남는다 — 55단계가 웹 클라이언트 ID를
눈으로 맞추다 실패한 자리와 같은 종류다. 그래서 EditMode가 `firestore.rules`를
읽어 대조한다:

| 대조 | 값 |
|---|---|
| 필드 허용 목록 | `CloudSaveDocument.Fields` 12 + 요약 6 + 세션 4 |
| 컬렉션 이름 | `playerSaves` · `playerSaveBackups` · `playerSaveSessions` (+ `scores` 보존) |
| 세이브 버전 상한 | `SaveData.CurrentVersion` = 21 |
| payload 상한 | `CloudSaveFingerprint.MaxPayloadBytes` = 204800 |
| 세션 만료 | `CloudSavePolicy.SessionExpirySeconds` = 180 |
| 봉투 형식 | `CloudSaveEnvelope.CurrentFormatVersion` = 1 |
| 도달층·티어 상한 | `StageProgress.ReachSanityCap` = 100000 · `EvolutionCurve.MaxTier` = 6 |
| id·지문 형식 | `[0-9a-f]{32}` · `[0-9a-f]{64}` |
| delete 금지 | 네 컬렉션 전부 |

⚠️ 규칙의 `payload.size()`는 **글자 수**라 UTF-8 바이트보다 느슨하다. 정확한
바이트 상한은 클라(`CloudSaveFingerprint`)가 지키고, 규칙의 그것은 문서가 무한정
자라는 것을 막는 천장이다 — 규칙 주석에 적어 두었다.

---

## 8. 테스트 패널 — "크로스 저장 (2단계 - 저장소·세션)"

1단계 절 바로 다음이다(33단계에 못 박은 규칙: 새 기능은 자기 절을 얹는다).
1단계 절이 파일과 순수 규칙만 만졌다면, 이 절의 버튼은 **실서버로 나간다** —
그래서 성격이 다르고, 그 차이를 창이 스스로 말해야 한다.

- 표시: uid · 이번 실행의 세션 id · 마지막 세션 상태(작성권 보유 여부) ·
  sidecar의 기기 id · 문서 경로
- 버튼: 세션 획득 / heartbeat / release · 정본 읽기(`Source.Server`) · 지금 커밋
- **안전 스위치 "실서버 호출 허용"이 기본 꺼짐이다.** 에디터의 Firebase는
  운영 프로젝트(onikiri-9cc18)에 붙고, 58단계는 규칙을 배포하지 않았으므로
  `playerSaves` 쓰기는 **거부되는 것이 정답**이다(현재 배포된 규칙의 catch-all이
  닫혀 있다). 그 거부를 눈으로 보는 것도 용도이지만, 모르고 누르는 일은 없어야 한다.
- 커밋 버튼은 플레이 중이면 먼저 로컬 저장을 시켜 **화면의 상태와 올라가는 것이
  같게** 만들고, sidecar가 없으면 실제 생성 함수로 만든다(임의 문자열 금지).

진짜 규칙 검증은 이 창이 아니라 Emulator다 — 만료된 heartbeat처럼 **규칙을
통해서는 만들 수 없는 상태**까지 만들어 재기 때문이다.

---

## 9. 범위 밖 (이번 단계에서 하지 않은 것)

`GameSession` 부팅 Apply 변경 · 자동 120초 동기화 · 충돌 선택 UI · 씬/프리팹 변경 ·
Link/Recover 통합 · App Check · **운영 Firebase 배포** · SaveData 변경(v22) ·
필드별 병합 · 커밋/푸시. 전부 손대지 않았다.

`CloudSaveStore`/`CloudSaveSession`은 아직 **아무도 부르지 않는다.** 배선은
59단계의 몫이고, 그래야 부팅 Apply가 한 번만 도는 계약을 그 스텝이 통째로 지킬 수 있다.

---

## 10. 완료 판정

| 게이트 | 결과 |
|---|---|
| Firestore Emulator 규칙 테스트 | **47/47 통과** |
| EditMode 전량 | **856/856 통과** (기존 837 + 신규 19) |
| 컴파일 오류·경고 | 0 |
| `git diff --check` | 통과 |
| 운영 Firebase 배포 | **0** (규칙은 저장소에만) |
| 커밋·푸시 | **없음** |

두 게이트가 모두 통과했으므로 2단계 **완료**로 판정한다.

---

## 11. 다음 — 59단계

**부팅 선택과 `GameSession.Apply` 1회.** 지금까지 세운 것(판정표 · 저장소 ·
세션)을 부팅 순서에 끼우되, 방치 보상이 정확히 한 번만 지급되어야 한다:

```
로컬 읽기 → sidecar → (제한 시간 안에) 서버 확인 → 판정 →
선택된 SaveData 한 벌 → Apply 1회 → 방치 보상 1회 → 즉시 로컬 저장 → 진입
```

지금은 `GameSession`이 스스로 즉시 Apply한다. 그 순서를 바꾸는 것이 59단계의
전부이고, 클라우드를 `Apply` **뒤에** 덮어씌우면 방치 보상이 두 번 지급된다 —
그것이 이 스텝을 여기서 끊은 이유다.
