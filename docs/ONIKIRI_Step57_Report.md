# ONIKIRI 57단계 — 크로스 저장 1단계 (순수 규칙과 로컬 형식)

> 확정 설계(2026-08-18) 13절의 **1단계**다: envelope · sidecar · fingerprint ·
> policy 넷을 세우고 EditMode로 못 박는다. **Firestore 쓰기는 한 줄도 없다** —
> 저장소와 보안 규칙은 2단계, 부팅 단일 Apply는 3단계다.
>
> 초판 뒤 **폐쇄 보완을 두 번** 돌렸다(같은 단계 안에서). 메운 구멍이 여섯이다 —
> 다운로드 검증이 해시 하나뿐이었고, 사슬이 끊긴 봉투를 판정이 그냥 지나갔고,
> 반쯤 쓰인 sidecar가 살아남았고, 임의 문자열이 id 자리에 들어갔고, **봉투를
> 만드는 쪽이 자기 결과를 검사하지 않았고**, **sidecar 저장 실패를 아무도
> 못 듣고 있었다**(void 반환).

---

## 0. 한 줄 요약

세이브 한 벌이 **문자열 하나(payload)와 지문 둘**이 되고, "부팅에서 무엇을 할
것인가"가 **여섯 갈래의 순수 함수** 하나로 정리됐다. 내려받은 봉투는 열네 겹의
검사를 지나야 적용되고, 올리는 봉투도 같은 검사를 지나야 만들어지며, 반쯤 맞는
sidecar는 없는 것으로 친다. **pending이 디스크에 남기 전에는 서버로 나가지 않는다.**

세이브 **v21 그대로**(신규 필드 0), 씬·프리팹·밸런스 영향 **0**, Firebase 호출
**0**, 보안 규칙 변경 **0**. 테스트 **837/837** (기존 763 + 크로스 저장 74).
커밋·푸시 없음.

---

## 1. 이 단계가 맡은 것 / 안 맡은 것

```
1단계 ← 여기   순수 정책 + 로컬 형식 (Firebase 없음)
2단계          Firestore 저장소 + 보안 규칙 + revision 트랜잭션
3단계          부팅 선택과 단일 Apply (방치 보상 1회 계약)
4단계          자동 동기화(120초) + 충돌 UI + 백업
5단계          계정 Link/Recover 통합
6단계          App Check(Play Integrity) + 기기 둘 실측
```

1단계의 산출물은 전부 **Firestore를 다른 것으로 바꿔도 참인 것들**이다. 그래서
EditMode가 이 단계를 통째로 증명할 수 있고, 실기 logcat이 필요한 부분이 0이다 —
53~55단계(스파이크·랭킹·계정)에서 증명 가능한 부분을 정책 파일로 끌어냈던 것과
같은 방식이고, 이번에는 그 비율이 100%다.

---

## 2. 필드별 병합은 **만들지 않았다** [C-1]

이 설계의 중심이라 코드보다 먼저 적는다.

```
기기 A  보석 1,000 → 10연 → 보석 775 + 신규 오의 + 천장 10
기기 B  오래된 세이브 (보석 1,000 · 오의 없음 · 천장 0)

max/합집합 병합 = 보석 1,000 + 신규 오의 + 천장 10   ← 뽑기가 공짜가 된다
```

`GameSession.Save()`가 지불(보석 차감)과 상품(오의·천장)을 **같은 스냅샷**에
담기 때문이다. 반만 합치면 지불 전 지갑과 지불 후 상품이 동시에 남는다. 장비
등급·동료 해금·퀘스트 수령·요도 티어가 전부 같은 구조다.

`maxStageReached`·`evolutionTier`처럼 단조로 보이는 값도 예외가 아니다. 그
진행은 지나오며 받은 보상과 쓴 재화에 묶여 있어서, 브랜치 밖에서 합치면 같은
복제가 생긴다.

**그것을 실제로 막는 것은 두 가지다:**

| 무엇이 막는가 | 어떻게 |
|---|---|
| **판정** | 두 곳이 갈라지면 `Conflict`가 나온다. 합칠 자리가 애초에 없다 |
| **형식** | 서버에 놓이는 것은 필드가 아니라 payload **문자열 한 덩어리**다. 반만 가져올 방법이 없다 |

메서드 이름에서 merge/union을 찾는 테스트(`NoMethodNameAdvertisesABranchMerge`)는
**보조 검사다.** 어떤 함수 안에서 손으로 필드를 섞는 코드는 이름을 아무렇게나
지을 수 있고 리플렉션은 그것을 보지 못한다 — 이 검사가 잡는 것은 구현이 아니라
**의도**이고, 병합 API를 세우려는 사람이 가장 먼저 부딪히는 벽이다.

랭킹의 도달층 max 병합(`AccountLinkPolicy.MergedStage`)은 그대로 둔다 — 세이브가
아니라 순위표의 값 하나이고 지불과 묶여 있지 않다. 같은 테스트가 그 **경계**를
함께 적어 둔다(그 메서드는 있어야 한다).

---

## 3. payload = 세이브 한 벌 [C-2]

서버에 가는 것은 `JsonUtility.ToJson(SaveData, false)` **문자열 한 덩어리**다.
Firestore 필드로 펼치지 않는다 — 펼치면 필드별 병합이 **가능해지고**, 가능해지면
언젠가 누군가 그것을 한다.

- 로컬 파일은 지금처럼 pretty(사람이 읽는다), payload는 compact(아무도 안 읽는다).
- 상한 **200KB**를 우리가 못 박는다. Firestore의 1MiB에 기대는 순간 상한이 남의
  것이 되고, 배열 하나가 잘못 자라 한도를 밟는 날 그 계정은 클라우드 저장이 통째로
  죽는다 — 원인은 세이브가 아니라 Firestore 에러로 보인다. 현재 세이브는 4KB 남짓
  (테스트가 32KB 미만을 못 박아 둔다).
- 상한을 넘으면 **봉투를 만들지도 않는다** — 거부될 쓰기를 들고 있지 않는다.

왕복 검사는 리플렉션으로 돈다(`TheWholeSaveSurvivesThePayloadRoundTrip`).
`SaveData`의 public 필드 전부(57개)를 훑으므로 **다음 스텝이 필드를 늘리면 그것도
자동으로 재진다.**

---

## 4. 지문이 둘인 이유 [C-3]

| 지문 | 답하는 질문 | 계산 |
|---|---|---|
| `payloadSha256` | 내려받은 **바이트**가 온전한가 | payload UTF-8 SHA-256 |
| `stateSha256` | **게임 상태**가 실제로 달라졌는가 | 시간성 3필드를 0으로 민 사본의 SHA-256 |

하나로 합칠 수 없다. payload 지문은 30초 자동 저장마다 달라진다 —
`lastQuitUtcTicks`가 매번 바뀌기 때문이다. 그것을 변경으로 세면 **두 기기가
번갈아 켜져 있기만 해도 충돌 화면이 뜬다.** 반대로 상태 지문만 두면 전송 중
망가진 payload를 걸러낼 수단이 사라진다.

정규화되는 셋은 `lastQuitUtcTicks` · `goldPerSecond` · `expPerSecond`뿐이다.
**골드·경험치·처치 수는 빼지 않는다** — 빼면 두 기기가 다른 재화를 들고도 같은
지문을 갖고, 그 상태의 InSync는 한쪽의 파밍을 통째로 버린다는 뜻이다. 테스트가
골드·경험치·보석·처치·티어·천장·이름 일곱을 하나씩 흔들어 지문이 움직이는 것을 잰다.

정규화는 **사본에서** 한다(`NormalizingDoesNotTouchTheOriginal`) — 원본을 밀면
다음 로컬 저장이 방치 보상의 기준 시각을 잃는다.

---

## 5. 봉투는 **열네 겹을 지난다** — 받을 때도, 만들 때도 [C-4]

보안 규칙(2단계)이 같은 것들을 한 겹 더 검사하겠지만, **규칙은 우리가 쓴 문서만
지킨다.** 콘솔에서 손으로 고친 문서, 옛 앱이 남긴 문서, 전송 중 잘린 문서는
규칙을 지나온 적이 없다. `CloudSaveEnvelope.Validate()`가 순서대로 본다:

| # | 검사 | 실패 이름 |
|---|---|---|
| 1 | `formatVersion`이 **정확히** 현재 형식인가 (옛 것도 아니다) | `FormatVersionMismatch` |
| 2 | `revision >= 1` | `RevisionOutOfRange` |
| 3 | `baseRevision == revision - 1` (사슬) | `RevisionChainBroken` |
| 4 | `lastMutationId`가 id 규격인가 | `MutationIdMalformed` |
| 5 | `sessionId`가 id 규격인가 | `SessionIdMalformed` |
| 6 | `deviceId`가 id 규격인가 | `DeviceIdMalformed` |
| 7 | payload 1바이트 이상 | `PayloadEmpty` |
| 8 | payload 200KB 이하 | `PayloadTooLarge` |
| 9 | `payloadSha256` 재계산 일치 | `PayloadHashMismatch` |
| 10 | JSON 파싱 성공 | `PayloadUnreadable` |
| 11 | **껍데기가 아닌가** (원문에 `"version"` 키 + 1 이상이어야 할 네 값) | `EmptySave` |
| 12 | `saveVersion`이 1..`SaveData.CurrentVersion` | `SaveVersionOutOfRange` |
| 13 | `parsed.version == saveVersion` | `SaveVersionMismatch` |
| 14 | `stateSha256` 재계산 일치 · `summary` 여섯 값 일치 | `StateHashMismatch` / `SummaryMismatch` |

네 검사가 특히 중요하다:

- **4~6 (id 셋)** — 규격 밖의 값이 있다는 것은 **이 문서를 우리 코드가 쓰지
  않았다**는 뜻이다. 그대로 두면 응답 유실 복구가 서버의 id와 영원히 안 맞고,
  2단계의 세션 일치 규칙이 임의 문자열을 진짜 세션으로 받아들인다. 그래서
  손상(`CorruptPayload`)이 아니라 `InvalidEnvelope`로 간다 — 봐야 할 곳이
  백업이 아니라 서버 메타다.
- **11 (껍데기)** — `"{}"`는 JsonUtility가 군말 없이 객체 하나로 만들어 준다.
  해시까지 맞춰 두면 파싱에만 기대는 검사는 그대로 통과하고, 적용하는 순간 진행이
  통째로 사라진다. 그래서 **원문에 버전 키가 있는지**를 따로 본다 — 파싱된
  객체만 보면 "적혀 있지 않았다"를 알 방법이 없다.
- **13 (버전 불일치)** — 봉투가 말한 버전과 안의 버전이 다르면 둘 중 하나는
  거짓이고, 어느 쪽이 거짓인지 알 방법이 없다.
- **14 (요약)** — 요약은 충돌 화면이 그리는 값이다. 위조되면 **사람이 거짓을
  보고 브랜치를 고른다** — 고른 결과가 화면에서 본 것과 다르면 그것은 데이터
  손실과 같다.

**`ForUpload`은 만든 봉투를 이 검사에 통과시킨 뒤에만 반환한다.** 내려받는 쪽에만
검사가 있으면 잘못된 것을 만들어 놓고 서버가 거부하기를 기다리는 코드가 남는다.
`version 0`·`stage 0`·레벨 0 같은 값은 id·revision·상한 검사를 전부 지나서
payload가 된 뒤에야 드러나므로(11·12), 만드는 자리가 자기 결과를 한 번 봐야만
걸린다. 비용은 직렬화 몇 번이고 업로드는 120초에 한 번이다.

---

## 6. sidecar — 세이브 버전을 **안 올린다**, 그리고 **반만 맞으면 버린다** [C-5]

동기화 메타(`baseRevision`·마지막 동기화 지문·pending)는 `onikiri_cloud_state.json`
이라는 **별도 원자 파일**에 산다. `SaveData`에 넣지 않는 이유가 둘이다:

1. 넣으면 `CurrentVersion`을 22로 올려야 하고, 그러면 **크로스 저장을 켜는 일이
   모든 라이브 세이브의 되돌릴 수 없는 형식 변경**이 된다.
2. 세이브가 통째로 payload가 되므로, 메타가 그 안에 있으면 **자기 자신을 담은
   해시**를 계산하게 된다 — 올릴 때마다 지문이 달라져 "상태가 안 바뀌었다"를
   영원히 말할 수 없다.

없어도 게임은 돈다. 위험한 것은 **반쯤 맞는 상태**다 — "revision 41까지
동기화했다"고 적혀 있는데 지문 자리가 비어 있으면, 다음 부팅은 그 41을 믿고
로컬이 안 변한 것으로 읽어 다른 기기의 진행을 조용히 덮거나 내려받는다.
`IsWellFormed()`가 다음을 모두 요구하고, 하나라도 어기면 **없는 것으로 친다**:

- `formatVersion == 1` · `baseRevision >= 0` · 서버 시각 >= 0
- `ownerUid` 있음 · `deviceId`가 **id 규격**(소문자 16진 32자)
- `baseRevision > 0`이면 마지막 동기화 지문 **둘 다** 유효한 64자리 SHA-256
- `pendingMutationId`와 `pendingPayloadSha256`은 **둘 다 있거나 둘 다 없다**
  (하나만 남으면 "무엇을 보내는 중이었는가"를 말할 수 없다)

읽기(`Load`)만이 아니라 **쓰기(`Save`)도 막는다** — 어차피 다음 부팅이 버릴
파일을 남기면 진단만 방해한다.

### `Save`는 bool이다 — **pending이 남기 전에는 서버로 안 나간다**

`CloudSaveSidecar.Save`는 원자 교체까지 끝났을 때만 `true`를 낸다(null·불변식
실패·IO 예외는 `false`). 이 반환값이 2단계의 계약이다:

```
MarkPending  ->  Save() == true 확인  ->  서버 트랜잭션
                 (false면 여기서 멈춘다)
```

응답 유실 복구가 그 파일 하나에 걸려 있기 때문이다. 서버는 커밋했는데 그 커밋의
mutation id가 로컬 어디에도 없으면, 다음 실행이 같은 쓰기를 다시 올려 revision을
하나 더 올리고 **직전 백업을 한 칸 밀어낸다** — 잃을 것이 없던 상황에서 백업 한
벌을 잃는다.

저장이 실패한 상태의 정답은 **클라우드 write 금지 + 로컬 dirty 유지**다. 게임은
그대로 돌고(오프라인 계약과 같은 자리), 다음 디바운스에서 다시 시도한다.
`CloudSavePolicy.MayStartServerWrite(state, persisted)`가 그 판단을 한 줄로 들고
있고, 테스트는 **파일이 놓일 자리를 폴더로 막아 진짜 IO 실패를 만들어** 잰다
(`AFailedSidecarWriteStopsTheServerWrite`).

uid가 다르면 그 sidecar는 **다른 계정의 사슬**이라 없는 것으로 친다
(`CloudSavePolicy.SidecarAppliesTo`). 5단계 Recover가 이 한 줄 위에 선다.

---

## 7. 판정표 [C-6]

`CloudSavePolicy.Decide(CloudSaveFacts)` 하나가 답한다. 입력에 **시각이 없다**는
것이 이 구조의 요점이다 — 어느 쪽이 최신인지 시계로 정하면 시간대를 바꾼 기기
하나가 남의 진행을 덮는다. 사슬은 revision만이 말한다.

| 상황 | 판정 | 테스트 |
|---|---|---|
| 서버 확인 못 함(오프라인·시간 초과) | `LocalOnly` | `WithoutTheServerWeJustPlayLocally` |
| 서버 문서 없음 + 동기화 이력 없음 | `UploadLocal` (revision 1) | `AnEmptyServerTakesTheLocalSaveAsRevisionOne` |
| **서버 문서 없음 + `baseRevision > 0`** | **`Blocked/MissingServerAfterSync`** | `ASyncedDeviceNeverRecreatesAVanishedDocument` |
| 로컬 없음/손상 + 클라우드 유효 | `DownloadCloud` | `WithoutALocalSaveTheCloudIsTheOnlyRecord` |
| 상태 지문 동일 | `InSync` (sidecar만 갱신) | `TheSameRecordOnBothSidesWritesNothing` |
| 서버 rev↑ + 로컬 무변화 | `DownloadCloud` | `AnUntouchedDeviceFollowsTheServer` |
| 서버 rev = base + 로컬 변경 | `UploadLocal` | `APlayedDeviceOnAnUnchangedServerUploads` |
| 서버 rev↑ + 로컬도 변경 | **`Conflict`** | `TwoBranchesAreNeverMergedAutomatically` |
| sidecar 없음 + 두 기록이 다름 | **`Conflict`** | `WithoutASidecarWeCannotTellWhichCameFirst` |
| 서버 rev < base (롤백) | `Blocked/ServerRollback` | `AServerThatWentBackwardsIsNeverWrittenTo` |
| **서버 rev < 1 · 사슬 불일치** | **`Blocked/InvalidEnvelope`** | `AnEnvelopeWithoutARevisionIsBlocked` · `ABrokenChainIsBlockedEvenWhenThePayloadIsFine` |
| 클라우드 saveVersion > 앱 | `Blocked/FutureSaveVersion` | `AFutureSaveIsNeverAppliedAndNeverOverwritten` |
| 봉투 formatVersion > 앱 | `Blocked/FutureFormatVersion` | `AFutureEnvelopeFormatIsAlsoBlocked` |
| 봉투 검증 실패 (5절) | `Blocked/CorruptPayload` 또는 `PayloadTooLarge`·`InvalidEnvelope` | `EveryEnvelopeFaultMapsToABlock` |

**순서가 곧 우선순위다.** 미래 버전이 언제나 먼저다 — 미래 버전 세이브를 "로컬이
변했으니 업로드"로 덮으면 다른 기기의 최신 진행이 옛 앱에 의해 지워진다. 봉투가
동시에 망가져 있어도 사람에게 할 말은 여전히 "앱을 업데이트하세요"다
(`FutureVersionsWinOverEveryOtherReason`).

**revision 1은 한 번도 동기화한 적 없는 기기만 만든다**
(`CloudSavePolicy.CanCreateFirstRevision`). sidecar가 revision을 들고 있다는 것은
이 uid의 사슬이 이미 존재했다는 뜻이고, 그것을 1로 되돌리는 것은 복구가 아니라
다른 기기의 base를 무효로 만드는 일이다. 문서가 사라진 진짜 이유는 대개 **로그인이
갈렸거나** 서버 사고다.

빈 지문끼리는 **절대 같다고 하지 않는다**(`SameState`) — "지문을 못 구했다"와
"같은 기록이다"를 섞으면 계산 실패가 InSync로 읽혀, 아무 동기화도 안 하는 상태가
조용히 정상처럼 보인다.

---

## 8. 잘못된 객체는 **만들지 않는다** [C-7]

검증이 내려받는 쪽에만 있으면, 잘못된 것을 만들어 놓고 서버가 거부하기를
기다리는 코드가 남는다. 만드는 자리에서도 막는다:

| 자리 | 거부하는 것 |
|---|---|
| `CloudSaveEnvelope.ForUpload` | 세이브 없음 · 200KB 초과 · `baseRevision` 음수/`long.MaxValue`(overflow) · id 셋 중 하나라도 형식 밖 · **`Validate() != None`인 결과 전부** |
| `CloudSaveLocalState.NewFor` | uid 없음 · deviceId 형식 밖 → **null** |
| `MarkPending` | 형식 아닌 mutation id · 지문 아닌 해시 → `false`, 상태는 그대로 |
| `MarkSynced` | `revision < 1` · 지문 아닌 해시 · 음수 서버 시각 → `false`, 상태는 그대로 |
| `CloudSaveSidecar.Save` | 불변식을 어긴 sidecar · IO 실패 → **`false`** (그리고 서버 write 금지) |

id 셋(session·device·mutation)은 **한 형식**이다 — `CloudSaveIds` = 소문자 16진
32자(`Guid "N"`). 임의 문자열을 허용하면 `sessionId = "editor-session"` 같은 값이
서버에 올라가고, 2단계의 세션 일치 규칙이 그것을 진짜 세션으로 받아들인다.
그래서 **테스트 패널도 실제 생성 함수를 쓴다** — 창에서만 통과하는 봉투를 만들면
여기서 본 것이 실기에서 나갈 것과 다른 물건이 된다.

`WasCommitAlreadyApplied`도 형식을 본다. 형식이 아닌 값이 양쪽에 똑같이 적혀
있어도 "이미 성공했다"로 읽지 않는다 — 그 읽기는 첫 업로드를 영원히 막는다.

---

## 9. 응답 유실 복구

서버는 커밋했는데 응답 전에 앱이 죽는 경로가 실재한다. 그때 로컬은 "안 갔다"고
믿고, 재시도하면 revision이 하나 더 오르고 직전 백업이 한 칸 밀린다 — 잃을 것이
없는 상황에서 백업 한 벌을 잃는다.

```
보내기 직전   sidecar.MarkPending(mutationId, payloadSha)   ← 먼저 적는다
트랜잭션      서버의 lastMutationId == pending 이면 이미 성공
성공 확인 뒤  sidecar.MarkSynced(revision, ...)             ← 그 다음에 지운다
```

---

## 10. 상수

| 값 | 근거 |
|---|---|
| `DebounceSeconds = 120` | 로컬 저장(30초)마다 쓰기를 내지 않는다. 서버가 붙잡을 것은 매 순간의 상태가 아니라 기기를 바꿔도 이어지는 지점 |
| `SessionExpirySeconds = 180` | 저장 주기보다 길어야 한다 — 짧으면 정상적으로 놀고 있는 기기가 저장과 저장 사이에 스스로 만료된다(테스트가 부등호를 못 박음) |
| `BootServerCheckSeconds = 6` | Firebase는 무한 로딩 게이트가 아니다. 값은 3단계에서 실기 조정, 중요한 것은 **0이 아니고 유한**하다는 것 |
| `LocalBackupCount = 3` | 클라우드 적용 전 로컬 순환 백업 (4단계에서 사용) |
| `MaxPayloadBytes = 200KB` | 3절 |
| `CloudSaveIds.Length = 32` | 8절 |

세션은 **정합성의 방어선이 아니다.** 오프라인 기기의 세션은 어차피 만료되고, 그
상태에서 두 기기가 갈라지는 것을 막을 방법은 없다. 마지막 방어선은 언제나
revision 트랜잭션이고, 세션은 "다른 기기에서 플레이 중"을 미리 알려 충돌 화면을
덜 보게 하는 UX 장치다.

---

## 11. 테스트 패널 — "크로스 저장 (1단계 - 규칙과 지문)"

계정 절 바로 다음이다(같은 uid를 쓴다). 하는 일 셋:

- **지문 뽑기** — 디스크의 세이브에서 payload 바이트·두 지문·봉투 요약과
  **검증 결과**를 실제로 뽑는다. 플레이 모드가 아니어도 된다. 바로 위 세이브
  절의 "방치 적용"을 누른 뒤 다시 뽑으면 **payload 지문만 달라진다** — 4절의
  계약을 눈으로 보는 자리.
- **판정 계산기** — 체크박스 여섯 + 봉투 검증 결과(EnumPopup) + revision 둘로
  7절의 줄들을 손으로 만든다. 프리셋 아홉(다른 기기가 앞서 감 / 이 기기만 놀았다 /
  둘 다 놀았다 / 재설치 / 오프라인 / 미래 버전 / **문서가 사라짐** / **봉투 손상** /
  **사슬 끊김**).
- **sidecar** — 경로·소유 uid·base revision·보내는 중인 쓰기, 그리고 지우기.

---

## 12. 파일

| 파일 | 역할 |
|---|---|
| `Scripts/Cloud/CloudSaveEnvelope.cs` | 서버 문서 DTO + 요약 여섯 값 + `ForUpload`(자기 검증) + **`Validate()` 열네 겹** |
| `Scripts/Cloud/CloudSaveFingerprint.cs` | 직렬화·정규화·SHA-256·형식 검사·200KB 상한 |
| `Scripts/Cloud/CloudSaveIds.cs` | 세션·기기·쓰기 id 한 형식 (생성 + 검사) |
| `Scripts/Cloud/CloudSaveLocalState.cs` | sidecar DTO + **불변식** + pending/synced 전이 |
| `Scripts/Cloud/CloudSaveSidecar.cs` | sidecar 원자 저장 → **bool** (불변식 위반은 읽지도 쓰지도 않음) |
| `Scripts/Cloud/CloudSavePolicy.cs` | 판정표·상수·재시도·소유 판정·fault→block 사상·`MayStartServerWrite` |
| `Tests/EditMode/CloudSaveTests.cs` | **74개** |
| `Editor/OnikiriTestPanel.cs` | 크로스 저장 절 |

건드리지 않은 것: `SaveData`(v21 그대로) · `SaveSystem` · `GameSession` ·
`CloudScores` · `AccountLink` · 씬 · 프리팹 · `firestore.rules` · Firebase 콘솔.

`CloudScores`의 오프라인 write 큐는 리더보드에만 남는다 — 2단계의 저장소는 그
코드를 공유하지 않는다. Firestore의 일반 오프라인 동기화는 같은 문서에 대해
last-write-wins라, 세이브 정본에 쓰면 오래된 기기가 나중에 최신을 덮는다.

---

## 13. 2단계 진입 판정

**가능하다.** 2단계가 기대는 계약이 전부 서 있고 EditMode로 고정돼 있다:

- 올릴 것(`CloudSaveEnvelope.ForUpload`)과 받을 것(`Validate`)의 모양이 확정
- 트랜잭션이 검사할 값(`revision`/`baseRevision`/`lastMutationId`/`sessionId`)이
  전부 봉투 안에 있고 형식이 검사 가능
- 보안 규칙이 옮겨 적을 조건(형식 정확히 1 · revision = 이전 + 1 ·
  baseRevision = 이전 revision · payload 200KB · saveVersion 범위 · id 규격)이
  이미 클라이언트 쪽 검사로 한 벌 존재 — 규칙은 그 사본이 된다
- 실패했을 때 로컬이 무엇을 하는가(`Blocked` 일곱 이유)도 확정
- 트랜잭션을 **언제 시작해도 되는가**가 한 줄로 있다 —
  `MayStartServerWrite(state, sidecarPersisted)`. **pending sidecar 영속에
  성공하기 전에는 서버 write를 시작하지 않는다**

2단계에서 **처음으로** 콘솔·규칙 배포가 필요하다(`playerSaves` ·
`playerSaveBackups` · `playerSaveSessions` 규칙 + Emulator 규칙 테스트). 그
배포는 라이브 프로젝트(onikiri-9cc18)를 건드리므로 착수 전에 확인이 필요하다.

---

## 14. 범위 밖 (다음)

- **2단계** — Firestore 저장소, revision 트랜잭션, 보안 규칙(소유자·revision +1·
  서버 타임스탬프·200KB·세션 일치), Emulator 규칙 테스트
- **3단계** — 부팅 선택 후 `GameSession.Apply` **1회** + 방치 보상 1회 계약
- **4단계** — 120초 디바운스·urgent sync·pause release·백업(로컬 3 / 클라우드 1)·
  충돌 UI(자동 추천 없음, 선택 뒤 씬 재로드)
- **5단계** — Link(uid 유지) / Recover(uid 교체) 통합
- **6단계** — App Check Play Integrity + 기기 둘 실측 8종
- **유료 재화** — 현재 `gems`는 클라이언트가 만든 JSON 안의 값이다. 크로스 저장은
  그것을 원장으로 만들지 않는다. IAP 전에 영수증 서버 검증·idempotency key·서버
  원장·무료/유료 분리가 별도로 필요하다 (⚠️ 출시 전 필수)
