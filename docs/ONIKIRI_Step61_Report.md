# ONIKIRI 61단계 보고서 — 크로스 저장 5단계: 계정 Link/Recover 통합

2026-08-19 · feature/firebase · SaveData **v21 유지** · `firestore.rules` **무수정** ·
밸런스·전투·귀문 **무수정**

---

## 0. 한 줄 요약

재설치한 사람이 구글로 돌아오면 이제 **순위만이 아니라 진행도 돌아온다** —
uid 교체 순간을 설계 §8.2의 여섯 순서로 닫았고(없으면 rev 1 · 같으면 조용히 ·
다르면 60단계 충돌 화면), 그 전에 테스트가 실사용 세이브를 만질 수 있던 빚(S5-0)을
경로째 격리로 갚았다.

---

## 1. [S5-0] 테스트 세이브 경로 격리 ★ — 실사용 파일 무접촉 증명

### 무엇이 빚이었나

60단계 §9의 행 국면에서 TearDown이 못 돌아 **실사용 세이브가 테스트 세이브로
덮였다**(백업으로 복원). 백업/복원은 반창고다 — 검사가 죽거나 에디터가 멈추면
안 붙는다. 이 스텝이 계정 교체(세이브를 갈아끼우는 가장 위험한 경로)를 다루므로
여기서 먼저 닫았다.

### 근본 수정 — 루트 하나를 갈아 끼운다

- `SaveSystem.Root` — 세이브 파일들이 사는 폴더의 **유일한 뿌리**. 세이브 ·
  sidecar(`CloudSaveSidecar.Path`가 이 뿌리를 쓰도록 변경) · `.broken` ·
  `.precloud.1~3` · `.prerecover`가 전부 여기서 파생된다.
- `SaveSystem.UseRootForTests(dir)` — `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
  가드 안. **릴리스 빌드에는 존재하지 않는다** (S4-0-1 검사가 이 파일까지 훑도록
  목록에 추가).
- `SaveSandbox`(IDisposable) — 만들 때 실사용 파일 9개의 지문을 뜨고 루트를 임시
  폴더로 교체, **버릴 때 실사용 파일이 바이트 그대로인지 대조**한다. 달라졌으면
  예외 → 그 검사가 실패로 적힌다. 즉 **모든 픽스처의 TearDown이 곧 격리 회귀
  검사**다.

### 전환한 픽스처 (백업/복원 코드 전부 삭제)

| 픽스처 | 전 | 후 |
|---|---|---|
| CloudConflictPlayTests | 바이트 백업 → TearDown 복원 | SaveSandbox |
| CloudSaveBootPlayTests | 〃 | 〃 |
| PromotionTrialPlayTests | 〃 + 해시 대조 | 〃 |
| TrialPresetEquivalencePlayTests | 〃 + 해시 대조 | 〃 |
| CloudSaveTests (EditMode) | sidecar 백업/복원 | 〃 |
| CloudSaveSyncTests 백업 순환 검사 | 인라인 preserve/restore | 〃 |

`GameSession`을 TearDown에서 파괴하는 규칙은 **그대로 둔다** —
`OnApplicationQuit`이 TearDown 뒤에 오고, 그 시점에는 루트가 이미 실사용으로
돌아가 있기 때문이다.

### 회귀 방지 검사

`SavePathIsolationTests` (EditMode, 신규 3개):
- 루트를 갈아 끼우면 파생 경로 **전부**가 따라온다 / 실사용 폴더를 가리키는 것이
  하나도 없다
- 격리 안에서 저장·불러오기·삭제·백업 순환을 전부 돌려도 실사용 파일은 바이트
  그대로다 (Dispose의 대조가 통과 = 증명)
- 격리 해제 후 정확히 실사용 경로로 복귀

---

## 2. [S5-2] Recover — uid 교체 + 전체 세이브 선택 ★

### 새 파일 둘, 판정과 실행의 분리

- `CloudSaveRecoveryPolicy` — **순수 규칙** (Firebase·파일 IO 0줄). 갈래 다섯:
  `KeepLocalAsFirstRevision` / `AdoptServerQuietly` / `AskTheHuman` /
  `WaitForServer` / `Blocked`. 여섯 번째 "병합"은 만들지 않았다.
- `CloudSaveRecovery` — 오케스트레이터. `AccountLink`의 Recover 갈래가 부른다.

### 여섯 순서의 실측 (설계 §8.2)

1. **uid 교체 전** `PrepareCandidate()` — 살아 있는 GameSession이 있으면 저장을
   한 번 돌려(후보가 30초 낡지 않게) 디스크를 `.prerecover`로 복사하고 메모리에
   한 벌 든다. 복사 실패는 삼킨다(백업은 최선의 노력).
2. 복구 로그인 성공 뒤 `RunAsync(newUid, candidate)` — **새 uid의**
   `playerSaves`만 읽는다 (fetch 기록 검사로 증명: 읽힌 uid가 정확히 1개).
3. `Missing` → 새 uid sidecar를 rev 0으로 세우고 `Dirty` + **urgent** —
   다음 커밋이 revision 1을 만든다. 계정 교체는 120초를 기다리지 않는다.
4. `Found` + 상태 지문 동일 → sidecar가 서버 head를 `MarkSynced`로 채택,
   `InSync`. **조용히** — 같은 기록을 두고 묻는 것은 소음이다. dirty로 남아
   있던 동일 스냅샷은 `DropPending`으로 정리(내용 그대로인 rev+1 방지).
5. `Found` + 다름 → `CloudSaveSync.NoteConflict(봉투)` + 상태 `Conflict`만.
   **파일도 서버도 아무것도 안 쓴다** (바이트 대조 검사). 선택은 60단계
   충돌 화면이, 선택 뒤는 씬 재로드가 맡는다.
6. 버려진 익명 문서는 **안 지운다** — `MayDeleteAbandonedSave`가 모든 갈래에
   false + `CloudSaveRecovery.cs`에 `.DeleteAsync(`/`.SetAsync(` 호출이 없음을
   소스 스캔 검사로 잠갔다(규칙 4-B `allow delete: if false`의 클라이언트 거울).

지문 비교는 부팅 판정과 **같은 함수**(`CloudSavePolicy.SameState`)다 — 빈 지문
끼리를 같다고 하지 않는 것까지 물려받아, 계산 실패가 "자동 채택"으로 새지 않는다
(전용 검사 있음).

### Offline/Failed = WaitForServer

못 본 것과 없는 것을 섞지 않는다 — 오프라인 복구가 "없음 → rev 1"로 읽히면 기존
계정 세이브 위에 새 사슬을 얹으려는 쓰기가 나간다. 아무것도 정하지 않고
`LocalOnly`로 남긴다. 이 상태는 자가 치유다: 다음 커밋이 새 uid로 나가면 58단계
revision 트랜잭션이 서버 문서를 발견하고 Conflict로 떨어져 어차피 충돌 화면이다.

---

## 3. [S5-1] Link — uid 유지, 무수정

55단계 Link 경로는 **한 줄도 안 바꿨다**. uid가 유지되므로 `playerSaves/{uid}`도
sidecar `ownerUid`도 그대로고, 로컬이 dirty면 다음 커밋이 평소처럼 올라간다.
`LinkingKeepsTheSidecarChainIntact` 검사가 그 전제(같은 uid = 같은 사슬)를 잰다.
복구 코드는 Recover 갈래(`AlreadyInUse`)에서만 불린다.

---

## 4. [S5-3] 랭킹 max 병합과 세이브 선택의 분리

- 리더보드 `maxStage`는 55단계 그대로 **max 병합 유지** (`AccountLinkPolicy.MergedStage`,
  무수정).
- 세이브에는 확대 적용하지 않는다 — `TheRankingMergeNeverLeaksIntoTheChosenSave`
  검사: 랭킹 병합이 30을 내는 상황에서 세이브 후보의 `maxStageReached`는 12
  그대로다.
- **UI의 인정**: 랭킹 화면 내 순위 줄을 `내 순위 3위 (최고 30층)`으로 바꿨다 —
  "최고" 한 단어가 "랭킹 = 이 계정의 최고 기록 / 세이브 = 지금 이어가는 진행"을
  선언한다. 두 값이 잠시 다른 것은 버그가 아니라 이 구분이다.
- **코드의 인정**: `AccountLink` Recover 갈래와 `CloudSaveRecoveryPolicy` 머리
  주석에 두 규칙이 다른 이유(랭킹 = 값 하나·지불과 안 묶임 / 세이브 = 경제
  트랜잭션 한 벌)를 적었다.

---

## 5. [S5-4] 계정 UI 배선 — 새 화면 0개

- **충돌 화면 재사용**: 복구의 ⑤는 `CloudSaveSync.NoteConflict`로 재료만 채운다.
  60단계 `CloudConflictPanel`이 그대로 뜨고, 선택 뒤 씬 재로드 → `ApplyBoot` 1회 +
  방치 보상 1회 (59단계 계약, 기존 PlayMode 검사가 계속 잰다).
- **설정에서 복구한 경우**: `SettingsPanel.OnLinkFinished`가 복구 직후 충돌이면
  충돌 화면을 **바로 연다** — 방금 복구를 누른 사람이 버튼을 찾게 두지 않는다.
  설정 창은 안전한 시점이라 hot swap 금지와 충돌하지 않는다.
- **타이틀에서 복구한 경우**: IntroFlow는 무수정. 게임 진입 → 부팅이 새 uid로
  서버를 보고 Conflict 판정 → **이번 스텝의 수정**: 부팅 충돌도
  `NoteConflict`를 채우도록 `CloudSaveCoordinator.Finish`에 한 줄을 넣었다.
  (60단계의 구멍: 부팅에서 발견된 충돌은 봉투가 안 채워져 설정의 "기록 선택"
  버튼이 영영 안 살았다 — 커밋 거부·복귀 확인만 채웠기 때문.)
- 4상태 줄: 복구 도중/직후 전부 기존 `StatusLine` 규칙 그대로 — ⑤ 뒤에는
  "기록 선택 필요"가 뜬다 (시뮬로 확인, 테스트 패널 5단계 절).
- 복구 상태줄: 세이브가 갈라졌으면 `"계정을 복구했습니다 - 이어갈 기록을
  선택하세요"`가 "최고 N층"보다 먼저 온다. `UIStrings.txt`에 추가(폰트 굽기용).

---

## 6. [S5-5] 실패·경계

- **익명 uid 유실 없음**: 복구 코드는 로그인이 **이미 성공한 뒤**에만 불린다.
  취소·네트워크·AlreadyInUse 아닌 오류는 전부 그 전에 걸러진다(55단계 분기
  무수정).
- **복구 중 사망**: `CloudSaveRecovery`는 로컬 세이브를 **읽고 복사만** 한다 —
  어느 갈래도 세이브 파일을 덮지 않으므로 도중에 죽어도 로컬 진행 무손실.
  `.prerecover` 백업이 증거로 남는다. (⑤에서 덮는 것은 사람이 고른 뒤의
  패널이고, 그 직전에 3벌 순환 백업이 남는다 — 60단계 그대로.)
- **남의 sidecar**: `ownerUid ≠ 새 uid`면 없는 것 (59단계 `SidecarAppliesTo`
  무수정, `TheOldSidecarDoesNotApplyToTheRecoveredUid`로 재확인).
- **에디터 게이트**: 복구의 서버 읽기도 `EditorServerCheckAllowed`(기본 꺼짐)를
  지난다 — 60단계 행 사고의 방어선이 이 경로에도 선다.

---

## 7. 검증

- **EditMode: 897/897** — 신규: `CloudSaveRecoveryTests` 14개(판정표 ·
  ①~⑥ 각 갈래 · 랭킹/세이브 분리 · Link 사슬 유지 · 경계) +
  `SavePathIsolationTests` 3개. S4-0-1 소스 스캔이 `SaveSystem.cs`·
  `CloudSaveRecovery.cs`까지 훑는다.
- **PlayMode (배치, `-batchmode -runTests`): 44/44** — 픽스처 4벌이 전부
  SaveSandbox 위에서 돌았고, TearDown의 지문 대조가 **실사용 파일 무접촉**을
  실행마다 증명한다. 배치가 지운 Sentis define(`ProjectSettings`)은 60단계
  규칙대로 `git checkout`으로 복원.

### ★ 격리가 첫 실행에서 바로 잡아낸 것 — 시계 검사 다섯의 숨은 의존

첫 배치에서 귀문 시계 검사 다섯(격노 90초 · 폐쇄 180초 · 공격 간격 · HUD 격노 ·
결과 문구)이 떨어졌다. `SuspendPlayerDamage`의 주석은 "죽지 않으므로 폐쇄까지
간다"고 적었지만, 그 생존은 **실사용 세이브의 강화 상태가 우연히 보장**하고
있었다 — 샌드박스(새 게임) 부팅이 되자 st30 귀문 적의 첫 타가 기본 체력(100)을
넘어 게임시간 2.3초 만에 사망 실패(`[Trial5] end result=Failure game=2.33s
hpLeft=0`). 검사가 재는 것은 시계이지 생존이 아니므로 체력 풀(1e12)을 검사
스스로 세우게 고쳤다 — 사망 검사는 `Current+1` 타격이라 무관, 회복 검사는 비율
기반이라 그대로 성립. **"통과하던 검사가 사실은 실사용 세이브를 읽고 있었다"의
실물 증거**이고, S5-0을 이 스텝에서 먼저 닫은 이유가 결과로 확인됐다.
- **테스트 패널**: "크로스 저장 (5단계 - 계정 복구)" 절 신규 — 갈래 시뮬 버튼
  넷(③/④/⑤/오프라인)이 전부 SaveSandbox 안에서 돌아 실사용 파일 무접촉.
  마지막 복구 갈래 · `.prerecover` 존재 · 계정 갈래 표시.
- **실기 검증 완료** — §7.5. Recover 전 과정(재설치 → AlreadyInUse → 충돌
  화면 → 클라우드 선택 → 42층 복원 → rev 12 동기화 재개)을 운영 서버에서
  실측했다. 6단계에 남는 실기 몫은 두 기기 동시 충돌과 urgent뿐이다.

## 7.5 실기 실측 (2026-08-19, Galaxy Note 20 · 무선 adb) ★

개발 빌드(88.3MB)를 설치하는 과정에서 앱이 재설치되어 **실제 재설치 시나리오가
그대로 성립**했다 — 새 익명 uid `iJLol7zL…`, 새 게임(1층), 새 사슬 rev 1~6.
서버에는 기존 계정 `A9aMgxR5…`(42층 · Lv.44 · rev 11 · 구글 연동)이 있었다.

설정에서 구글 로그인(snow2271) → logcat 전문이 설계 §8.2 그대로다:

```
22:16:17 [AccountLink] 연동 실패 = AlreadyInUse -> 계획 Recover
22:16:18 [CloudScores] 사용자 채택. uid = A9aMg… / 교체됨 = True
22:16:18 [CloudSave] ★ 복구 세이브 판정 = AskTheHuman (서버 Found / 로컬 6267de24 / 서버지문 5864998f)
22:16:18 [AccountLink] 병합 - 로컬 1 / 버린 문서 1 / 복구된 문서 42 -> 42     ← 랭킹 max 병합
22:16:18 [AccountLink] 계정을 복구했습니다 - 이어갈 기록을 선택하세요
22:16:48 [CloudScores] 제출 생략(후퇴 방지). 로컬 1 / 서버 42                  ← 세이브(1층)와 랭킹(42층)이
                                                                                다른 상태를 코드가 인정
22:17:46 [CloudSave] 클라우드 적용 전 로컬 백업 (3벌 순환): …precloud.1
22:17:46 [CloudSave] 충돌 해결: 클라우드 기록 사용 (rev 11)                    ← 사람이 충돌 화면에서 선택
22:17:46 [CloudSave] 부팅 선택: 같은 기록 - 로컬 사용 (서버 rev 11)            ← 재로드 부팅이 InSync
22:17:46 [Onikiri] Boot apply #1 - … (방치 보상 1회)                           ← ApplyBoot 1회 + 보상 1회 계약
22:18:12 [CloudSave] 주기 동기화 완료 (rev 12)                                 ← 복구된 사슬 위에서 동기화 재개
```

배포된 운영 Firestore를 되읽어 확정:

| 검증 | 실측 |
|---|---|
| ⑥ 버려진 익명 문서 미삭제 | `playerSaves/iJLol7zL…` **rev 6 그대로 잔존** (`scores/iJLol7zL…`도 잔존) |
| 세이브 복구 | `playerSaves/A9aMg…` **rev 12 (base 11)** — 사슬이 리셋 없이 이어짐. 42층 · Lv.44 · 보석 582 |
| 랭킹 분리 | 복구 직후 랭킹 42층 vs 세이브 1층 공존 → 선택 뒤 일치. 후퇴 방지 제출 생략 실측 |
| 방치 보상 | 채택된 클라우드 세이브 기준 1회 (460.9T, 클라우드의 초당 수입 77.1G/s로 계산) |

관찰 하나: 복구가 `scores`의 옛 이름("랑랑무사")을 되살렸다가, 클라우드 세이브
채택 뒤 세이브의 이름("랑무사")이 정본이 되어 재제출됐다 — 세이브가 이름의
출처라는 54단계 설계 그대로이고 61단계의 회귀가 아니다.

## 8. 불변 확인

- SaveData **v21 그대로** — 새 필드 0. 동기화 메타는 전부 sidecar(별도 파일).
- `firestore.rules` **무수정** — 복구는 새 uid의 문서를 읽을 뿐이고(규칙상
  본인 read 허용), 쓰기는 기존 58단계 트랜잭션 경로뿐이다.
- 밸런스·전투·귀문(승급 Step5 폐쇄) 무수정 — 건드린 런타임 파일은 계정·클라우드·
  UI 문구뿐.
- 에디터 실서버 게이트 기본 꺼짐 유지 + 복구 경로에도 적용.

## 9. 변경 파일

**신규**
- `Scripts/Cloud/CloudSaveRecoveryPolicy.cs` — 복구 갈래의 순수 규칙
- `Scripts/Cloud/CloudSaveRecovery.cs` — ①~⑥ 오케스트레이터
- `Scripts/SaveGame/SaveSandbox.cs` — 테스트 경로 격리 + 무접촉 검증 (가드 안)
- `Tests/EditMode/CloudSaveRecoveryTests.cs` · `Tests/EditMode/SavePathIsolationTests.cs`

**수정**
- `Scripts/SaveGame/SaveSystem.cs` — `Root` + 테스트 seam (가드 안)
- `Scripts/Cloud/CloudSaveSidecar.cs` — Path가 `SaveSystem.Root`에서 파생
- `Scripts/Cloud/AccountLink.cs` — Recover 갈래에 ①·②~⑤ 배선, 상태줄 한 줄
- `Scripts/Cloud/CloudSaveSync.cs` — `NoteConflict` · `DropPending`
- `Scripts/Cloud/CloudSaveCoordinator.cs` — 부팅 충돌도 충돌 화면 재료를 채움
- `Scripts/Widget/Panels/SettingsPanel.cs` — 복구 직후 충돌 화면 자동 열기
- `Scripts/Widget/Panels/LeaderboardPanel.cs` — 내 순위 줄 "(최고 N층)"
- `Tests/PlayMode/*` 4벌 + `Tests/EditMode/CloudSaveTests.cs`·`CloudSaveSyncTests.cs` —
  SaveSandbox 전환
- `Editor/OnikiriTestPanel.cs` — 5단계 절 + 복구 시뮬
- `Data/UIStrings.txt` — 61단계 문구

## 10. 남은 것 (다음 = 6단계, 마지막)

- App Check(Play Integrity) + **두 기기 실기 충돌 실측** + urgent(뽑기·보석) 실기
  확인 + 출시 규칙 최종 점검. (Recover 실기는 §7.5에서 완료.)
- 이후: 골드획득 상한 제거·밴드 재적합 / 승급 Android 실기 QA / 애플·iOS /
  Remote Config / 릴리스 keystore SHA-1.
