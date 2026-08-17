# 승급·전직 재설계 — 3단계 + 3.1 폐쇄 **완료 보고서**

```
EditMode  738 / 738 통과 · 195초
PlayMode   22 /  22 통과 ·  60초 · ignored/skipped 0
컴파일 오류 0 · 경고 0
사용자 세이브 해시  EE-9D-02-…-6E-8C  (전후 동일)
Firebase 무수정 · 커밋·푸시 없음
Android   실기 완료 (SM-N981N / Android 13) — §9
k = 0.45  임시값 유지 (실기로 고정하지 못함 — §9.6)
```

---

## 1. TrialHud 실제 씬 연결 (지적 1)

**전 판의 결함**: `TrialHud.cs`만 있고 `Main.unity`·프리팹·빌더 어디에도 없었다.
클래스 생성을 UI 구현으로 보고했다.

### 무엇을 했는가

`BossContentBuilder.BuildTrialHud()` 신설 — 기존 `BuildHud`와 같은 파이프라인·같은
헬퍼(`CreateLabel` / `Stretch` / `CenterBox` / `Replace` / `UiSkin`)를 쓴다.
`BuildHud` 끝에서 부르고, **독립 진입점**도 뒀다:

```
Onikiri/Scene/Build Trial Hud     <- 신규 메뉴. 멱등(Replace가 중복을 치운다)
```

독립 진입점이 필요했던 이유: `Build Combat Content`가 서드파티 VFX 텍스처의
meta 잠금으로 **HUD 배선에 도달하기 전에 중단**된다(실측). 귀문 HUD는 그
베이커와 무관하므로 따로 돌 수 있어야 한다.

### 씬에 실제로 들어갔다

```
BattleArea (TrialHud)
├ TrialBanner      bannerRoot   ├ Gate / Foe / Clock / Enrage(Label)
├ TrialTransition  transitionRoot
SafeArea
├ TrialNotice      noticeRoot   ├ Label
└ TrialResult      resultRoot   ├ Title / Detail
```

배선 15개 전부 non-null (실측 `NULL count = 0`).
`git diff`: `Assets/_Project/Scenes/Main.unity` · `Editor/BossContentBuilder.cs`.

### PlayMode 검사 위치

| 계약 | 검사 |
|---|---|
| 씬에 존재 + 참조 15개 non-null | `TrialHud_IsInTheSceneWithEveryReferenceWired` |
| 일반 보스·파밍 중 숨김 | `TrialHud_IsHiddenOutsideTheTrial` |
| Entering 안내 / Fighting 띠·1/3·시계 / Transition `다음 적` / Victory 결과 / 종료 후 전부 숨김 / **BossHud와 비중첩** | `TrialHud_ShowsEachStateAndNeverOverlapsBossHud` |
| 격노 단계 표시 | `TrialHud_ShowsEnrageAfterNinetySeconds` |

---

## 2. 등록 실패 시 귀문 우회 제거 (지적 2)

**전 판의 결함**: `gate > 0`인데 `RegisterGateBossKill()`이 false면 게이트 분기를
빠져나와 `progress.AdvanceStage()`가 돌았다 — **귀문을 통째로 건너뛰고 스테이지가
올랐다.** "손상 상태에서 자동 돌파 없음"을 지키려던 코드가 자동 돌파 그 자체였다.

### 정규화 정책

부르는 쪽이 이미 셋을 확인했고(최전선 · 게이트 스테이지 · 할당량 충족) 이 함수는
**실제 일반 보스 처치 콜백 안**에서만 불린다. 그 넷이 맞으면 "방금 이 게이트의
보스를 벴다"는 사실 자체는 참이므로, `bossKillCount`가 몇이든 `stage`로 맞춘다.

**공짜 돌파가 아니다** — 정규화가 여는 것은 문이지 통과가 아니다. 플레이어는
여전히 귀문을 이겨야 티어와 진행을 받는다.

### `AdvanceStage`로 떨어지는 경로가 사라졌다

```
등록 성공(정규화 포함)  ->  귀문 대기
그 외                   ->  Debug.LogError + 아무것도 안 올리고 Failed -> Farming
```

둘 다 `AdvanceStage`를 안 지난다. 도전 버튼은 `CanChallenge`가 잠그므로 같은
보스를 반복해 보상을 파밍할 수도 없다.

| 계약 | 검사 |
|---|---|
| stage+1로 부푼 손상 상태에서 stage·tier 불변, 귀문 안 건너뜀, 정규화 후 문이 정확히 열림, 일반 보스 잠김 | `CorruptBossKillCount_NeverSkipsTheTrial` |
| 진짜 손상(할당량 미달)은 등록도 돌파도 없음 | `GateBossRegistration_RefusesWhenTheQuotaIsUnmet` |
| 중복 콜백에 `bossKillCount` 불변 | `GateBossRegistration_IsIdempotent` |

---

## 3. OnDisable 후 재활성화 상태 복구 (지적 3)

**전 판의 결함**: `OnDisable`이 배율만 껐다. 다시 켜면 `phase`는 `Trial`인데 캡은
꺼진 상태 — 적도 없어서 폐쇄까지 아무 일도 안 일어난다.

`AbandonTrialIfRunning()`이 시도를 통째로 접는다:

```
TrialDamageScale.Exit()   적 제거(ClearField)   스폰 재개(ResumeSpawning)
PlayerHealth.EndFight()   trial=Idle   TrialGate=0   phase=Farming   관문 Hide
```

**D-4 대기는 유지한다** — `bossKillCount`를 안 건드리므로 `PendingTrialGate`가
그대로 열려 있고, 다시 켜면 무료 재도전만 가능하다.

| 계약 | 검사 |
|---|---|
| 배율 해제 · phase=Farming · trial=Idle · TrialGate=0 · 적 제거 · tier/stage 불변 · 대기 유지 · `CanChallengeTrial=true` · `CanChallenge=false` · 실제 재진입 | `DisableThenEnable_AbandonsTheAttemptButKeepsThePendingGate` |

---

## 4. PlayMode 누락 계약 보완 (지적 4)

`Assert.Ignore` **0개**. 검증 못 한 검사는 통과로 세지 않는다 — 전환에 못
도달하면 `Ignore`가 아니라 **실패**다.

| 계약 | 검사 |
|---|---|
| 사망 실패 → 결과 → Farming → 무료 재도전, 일반 보스 잠김, 재화 불변 | `DeathFailure_ReturnsToFarmingAndAllowsFreeRetryOnly` |
| 전환이 **게임 시간 2초** + 재생 실제 증가 | `Transition_LastsTwoSecondsAndRegenerationContinues` |
| 전환 중 적 부재 + 체력 안 줄어듦 | `Transition_SilencesTheFoes` |
| 전환 중 오의 쿨타임 미초기화 | `Transition_DoesNotResetSkillCooldowns` |
| 첫 공격 2초 · 간격 2초 | `TrialFoe_FirstAttackAndIntervalAreTwoSeconds` |
| 90초 전 0단계 · 직후 1단계 · 10초마다 누적(2·3단계) | `Enrage_StartsAtNinetyAndStacksEveryTenSeconds` |
| 180초 Closed · tier 불변 · 무료 재도전 | `Close_EndsAtOneEightyAndAllowsFreeRetry` |
| 골드 0 · 경험치 0 · 퀘스트 0 · 할당량 0 · 보스 처치 수 0 | `TrialFoes_GrantNoRewardOfAnyKind` |
| 표시=적용, 출처 넷 각각 1회, `Unattributed` 0, 총합비=배율 | `DamageAudit_AppliedEqualsShownAndEachSourceScalesOnce` |
| 귀문 밖 피해 무보정 + 장부 미기록 (회귀) | `OrdinaryCombat_DamageIsUnscaled` |
| 승리 시 tier·stage 각각 1회, `bossKillCount` 불변 | `Trial_GrantsExactlyOneTierAndOneStage` |
| 귀문 밖 일반 보스 경로 불변 | `OrdinaryStage_IsUnchanged` |
| D-4 유도 | `PendingTrial_IsDerivedFromSavedProgress` |
| `Exit` 멱등 | `ExitIsIdempotent` |

### 타이밍 검사의 전제 — 플레이어 화력을 멈춘다

격노·폐쇄·공격 간격을 재려면 적이 그때까지 살아 있어야 하는데, **세이브의 강화
상태가 그것을 정한다** — st175 세이브에서 네 검사가 "귀문이 이미 끝났다"로
실패했다. `SuspendPlayerDamage()`가 `PlayerCombat`·`PetCombat`·`SpiritSummon`·
`SkillPerformer`를 끈다. 귀문 **입장 뒤에** 부른다 — 소프트캡 점수는 입장 시 한
번 읽으므로 그 전에 끄면 다른 세계를 재게 된다.

적의 공격은 체력 변화가 아니라 **`Enemy.Attacked` 사건**으로 잰다. 처음엔 체력
감소로 쟀다가 실패했다 — 그 세이브의 재생이 st30 보스의 피해를 즉시 메웠다.
사건은 피해량·재생과 무관하게 같은 시각을 낸다.

---

## 5. 결정론과 세이브 격리 (지적 5)

### 백업 시점을 씬 로드 **앞**으로

전 판은 씬을 한 프레임 돌린 뒤에 떴다. 그 사이 `GameSession`이 로드·마이그레이션·
저장할 수 있으므로 그 "원본"은 이미 오염된 값이다.

### 세 겹

| 겹 | 내용 |
|---|---|
| 1 | 씬 로드 **전**에 바이트 + MD5 확보 |
| 2 | `GameSession` **컴포넌트 파괴** — 저장 경로 넷(자동 30초·Pause·Focus·Quit)이 한 번에 사라진다 |
| 3 | `TearDown`에서 바이트 복원 후 **해시 일치 단언** |

실패·중단된 검사에서도 `TearDown`은 돈다.

시도했다가 버린 것: `GameSession` **오브젝트** 비활성화 — 같은 자리의 전투
시스템까지 죽어 귀문이 200초를 채우고 갇혔다. 파일 복원만 두는 것 —
`OnApplicationQuit`이 `TearDown` 뒤에 돌아 복원본을 도로 덮었다.

### 결정론

적은 `KillCurrentFoe()`로 **직접 처치**한다 — `Enemy.TakeDamage`를 그대로 지나므로
상태 머신·보상 차단·소프트캡은 실제 코드가 돌고, 갈리는 것은 "얼마나 세게
때렸는가"뿐이다. 소프트캡의 산수는 `DamageAudit_*`가 알려진 값(1000)으로 따로 잰다.

### 실측

```
원본 해시   EE-9D-02-20-9E-EA-63-57-5E-3B-7F-C1-6C-80-6E-8C
최종 해시   EE-9D-02-20-9E-EA-63-57-5E-3B-7F-C1-6C-80-6E-8C   동일
세이브      version=19 · stage=175 · max=175 · bossKills=144 · tier=6   (원본 그대로)
```

작업 시작 전 백업 `onikiri_save.json.v20-backup-step3`는 남겨 뒀다.

---

## 6. 완료 조건 대비

| 조건 | 결과 |
|---|---|
| 런타임 문제 전부 해결 | 지적 1~5 모두 (§1~§5) |
| TrialHud 실제 Main 씬 배선 | 확인 (참조 15개 non-null · 씬 diff 존재) |
| EditMode 전체 통과 | **738/738** |
| 확대된 PlayMode 전체 통과 | **22/22** (9 → 22) |
| ignored/skipped 0 | **0** |
| 컴파일 오류·경고 0 | 충족 |
| 사용자 세이브 해시 전후 동일 | 충족 |
| Android | **실기 완료** — 게이트 6곳 통과 · 결함 3건 (§9) |
| k = 0.45 | 임시 유지 — 실기가 밴드 빌드를 재현하지 못해 고정 불가 (§9.6) |
| Firebase·커밋·푸시 | 없음 |

---

## 7. 3.1에서 바뀐 파일

| 파일 | 목적 |
|---|---|
| `Scenes/Main.unity` | TrialHud + 4개 루트 실제 배선 |
| `Editor/BossContentBuilder.cs` | `BuildTrialHud` + `Onikiri/Scene/Build Trial Hud` 메뉴 |
| `Battle/BossFight.cs` | `CanChallengeTrial`·`TrialAttempted` · 게이트 우회 제거 · `AbandonTrialIfRunning` · 죽은 코드 제거 |
| `Progression/StageProgress.cs` | `RegisterGateBossKill` 정규화 |
| `UI/BossHud.cs` | 버튼 라우팅 · 문구 · 할당량 배타 |
| `Tests/PlayMode/*` | 검사 9 → 22 · 세이브 격리 3겹 |

---

## 8. Android 실기로 넘길 것

2단계 보고서 §11에 더해:

- `Time.timeScale` 가속 없이 **실제 속도**의 3연전 체감 (PlayMode는 10배속)
- 상단 띠가 폰 화면에서 전투를 가리지 않는가
- 격노 90초 이후의 압박이 화면에서 읽히는가
- k=0.35 / 0.45 / 0.60 체감 비교 → **k 최종 고정**

실기 전 v20 세이브 백업과 테스트 계정 사용. PlayMode가 세이브를 두 번 덮었던
사고(§5)가 실기에서는 백업 없이 일어난다.

---

## 9. Android 실기 검증 (2026-08-16)

### 9.1 환경과 안전 조치

| 항목 | 값 |
|---|---|
| 기기 | Samsung SM-N981N (Galaxy Note 20) |
| OS | Android 13 (SDK 33) |
| 해상도 / 밀도 | 1080 × 2400 / 450 (UI 420) |
| 빌드 | `Onikiri/Build/귀문 실기 빌드 (개발 패키지)` · Development Build |
| 패키지 | `com.studio202.onikiri.dev` — **운영 `com.studio202.onikiri`은 설치·세이브 모두 무접촉** |
| 네트워크 | `svc wifi disable` + `svc data disable`로 차단 |

안전 조건 이행:

- 운영 세이브를 `Builds/DeviceSaveBackup/onikiri_save.device-prod.json`에 백업(v19 ·
  st42 · bossKillCount 41 · tier 5). 테스트 내내 운영 패키지는 건드리지 않았다.
- `google-services.json`을 치워 빌드했지만 **`google-services.xml`은 이미 운영
  프로젝트(`onikiri-9cc18`)로 생성돼 있었다.** 파킹만으로는 못 막는다 —
  기기 네트워크를 끊어 막았고, 로그로 차단을 증명했다:
  `[CloudScores] 익명 로그인 실패: FirebaseException: A network error…`
  `[Leaderboard] 제출 시도 (도달층 갱신): 도달층 29` → 인증 단계에서 실패.
- v21 세이브는 클라우드로 한 번도 올라가지 않았다.

### 9.2 매트릭스 결과

| # | 항목 | 결과 |
|---|---|---|
| 1 | st29→30→일문→st31 | **통과** — st30 보스 처치 후 stage가 멈추고 귀문이 걸림 |
| 2 | 게이트 6곳 경계 (30/40/50/70/100/150) | **통과** — 6곳 모두 tier가 정확히 +1, `bossKillCount == max−1` 유지 |
| 3 | 승리 시 tier·외형 동시 반영 | **통과** — tier 0→1 순간 초상화·배경·동료 탭이 함께 바뀜 |
| 4 | 실패 무손실 | **통과** — 폐쇄 후 stage·max·bossKillCount·tier·골드·강화 전부 불변 |
| 5 | 무료·무제한 재도전 | **통과** — 폐쇄 즉시 「귀문 재도전」, 비용 표기 없음 |
| 6 | 90초 격노 / 180초 폐쇄 | **통과** — 아래 9.3 |
| 7 | 빌드 강도별 클리어 시간 | 측정 — 아래 9.4 |
| 8 | 소프트캡 체감 · 안내 문구 | **통과** — 진입 2초간 「이문 / 귀문에서는 지나친 화력이 완만하게 조정됩니다.」 |
| 9 | 공통 damageScale | 간접 확인 — 단일 초크포인트(`Enemy.TakeDamage`) 유지, 실기 예외 0 |
| 10 | 백그라운드 · 강제 종료 · 재실행 | **통과** — 아래 9.5 |
| 11 | v19→v21 마이그레이션·멱등성 | **통과** — 19→21, stage/max/bossKillCount/tier/젬 전부 보존 (tier 5 = max(5, 2)) |
| 12 | 가독성 · VFX | **결함 2건** — 아래 9.7 |
| 13 | logcat 오류·예외 | **통과** — Unity 예외 / MissingReference / NullReference **0건**. 남은 오류는 의도적으로 끊은 네트워크(`Curl error 6`)와 GMS 인증뿐 |
| 14 | k 비교 → 최종 고정 | **고정 못 함** — 아래 9.6 |

### 9.3 격노·폐쇄 타이밍 (실측)

이문(st40)에서 공격력 Lv.240 빌드로 캡처를 최소화해 측정.

| 실시간 | 배너 남은 시간 | 경과(게임) | 격노 표시 | 공식 `1+floor((t−90)/10)` |
|---|---|---|---|---|
| 85초 | 120초 | 60 | 없음 | 0 |
| 96초 | 113초 | 67 | 없음 | 0 |
| 142초 | 80초 | 100 | **2단계** | 2 |
| 173초 | 58초 | 122 | **4단계** | 4 |
| 186초 | 49초 | 131 | **5단계** | 5 |
| 202초 | 38초 | 142 | **6단계** | 6 |

전 구간에서 공식과 정확히 일치. 남은 시간도 `180 − 경과`와 정확히 일치했고,
경과 180초에 폐쇄되며 버튼이 「귀문 재도전」으로 바뀌었다.

**주의 — 게임 시간과 실시간이 다르다.** 위 표에서 게임 142초가 실시간 202초였다
(비율 **0.70**). 원인은 `HitStop`이다. 프로젝트에서 `Time.timeScale`을 쓰는 곳은
`HitStop` 하나뿐이고, 타격마다 0으로 붙잡는다. 귀문 시계는 스케일 시간이므로
전투가 빽빽할수록 실시간으로는 길어진다 — 화면의 「180초」가 플레이어에겐 약
**4분**이다. 보스 30초 타이머 등 기존 타이머도 같은 성질이라 내부적으로는
일관되지만, 문구가 실시간처럼 읽힌다는 점은 4단계에서 판단할 문제다.

### 9.4 빌드 강도별 클리어 시간 (이문 · st40)

공격력 강화 레벨만 바꾸고 나머지는 고정. 실시간 측정(게임 시간 ≈ ×0.70).

| 공격력 Lv | 실시간 | 게임 시간(환산) | 결과 |
|---|---|---|---|
| 240 | — | >180 | 폐쇄 (첫 적도 못 잡음) |
| 800 | — | >180 | 폐쇄 |
| 1000 | 119초 | ≈83초 | 승리 |
| 1150 | 22초 | ≈15초 | 승리 |
| 1314 (원본 세이브) | ≈10초 | ≈7초 | 승리 |

1000 → 1150은 원시 화력 **×8.37**, 전투 시간은 77초 → 9초로 **÷8.5**. 지수 1.0,
즉 **이 구간에서는 소프트캡이 걸리지 않는다** — 기준 화력 아래라는 뜻이고 설계
의도대로다. 캡은 과화력에만 붙는다.

고정 오버헤드는 6초(입장 2초 + 교체 2초 × 2)로, 강한 빌드의 클리어 시간 하한이다.

### 9.5 종료·복구 (D-4)

| 시점 | stage | max | killsThisStage | bossKillCount | tier |
|---|---|---|---|---|---|
| 귀문 대기 | 40 | 40 | 10 | 40 | 1 |
| 홈 버튼 → 복귀 | 40 | 40 | 10 | 40 | 1 |
| 귀문 진행 중 강제 종료 | 40 | 40 | 10 | 40 | 1 |
| 재실행 후 | 40 | 40 | 10 | 40 | 1 |

- 백그라운드 복귀 시 귀문이 **이어졌고**, 백그라운드 12초 동안 시계가 흐르지
  않았다(경과 23초 ↔ 포그라운드 체류 25초).
- 진행 중 강제 종료 후 재실행하면 「귀문 도전」으로 돌아온다. 우회도 손실도 없다.

새 세이브 필드 없이 `bossKillCount`만으로 대기 상태를 유도하는 D-4가 실기에서
그대로 성립한다.

### 9.6 k 비교 — **고정하지 못했다**

k = 0.35 / 0.45 / 0.60으로 각각 APK를 구워 육문(st150)에서 측정했다.

| k | 공격력 5400 | 6000 | 6600 |
|---|---|---|---|
| 0.35 | — | 폐쇄 | 7초 |
| 0.45 | 폐쇄 | 11초 | — |
| 0.60 | 폐쇄 | 9초 | — |

0.45와 0.60의 두 점은 코드가 구현한 닫힌 형태

```
클리어(게임초) ≈ 6 + 41 / P^k        P = 플레이어 화력 / 문 기준 화력
```

와 **1% 안에서** 맞는다(두 점에서 역산한 P가 각각 1174·1130). 실기가 소프트캡
파이프라인이 설계대로 붙어 있음을 증명한 것이다.

**그런데 이것으로 k를 고정할 수는 없다.** k의 판정 기준은 코드 주석이 적어 둔
대로 "하한·중간·곡선추종 세 빌드가 모두 밴드(35~45초)에 드는가"인데, 그 세 빌드는
`StageSimulation`이 정의한다. 실기에서 내가 만든 세이브는 강화 레벨만 손으로
바꾼 합성 빌드라 밴드 빌드가 아니다. 합성 빌드로 잰 숫자를 근거로 k를 고정하면
근거가 바뀐 것을 고정값으로 승격하는 셈이다.

또 k=0.35 · 공격력 6000의 「폐쇄」는 위 닫힌 형태와 어긋난다(예측 ≈13초). 재현
1회를 못 돌려 원인 미상이며, 이 한 점은 **신뢰하지 않는다**.

→ **k = 0.45는 EditMode 밴드 실측 근거 그대로 유지한다.** 고정하려면 밴드 세
빌드를 세이브로 찍어 내는 도구가 필요하고, 그것이 없는 한 실기는 k를 고정할
자격이 없다.

### 9.7 실기에서 발견한 결함 3건

**결함 A — 귀문 적이 화면을 뒤덮는 크기로 나온다 (`BossFight.cs:1139`)**

일반 보스 스폰은 정의·배율·틴트를 세 갈래로 고른다(`BossFight.cs:565~584`):
배치 애셋이 있으면 애셋이, 챕터 보스면 `1f`/흰색, 잡몹 확대판이면
`stageBossScale`(2)/분홍 틴트. 그런데 `SpawnTrialFoe`는 정의만 그 규칙대로
고르고 **배율·틴트는 언제나 `stageBossScale`·`stageBossTint`를 쓴다.**

챕터 보스가 서는 게이트(st40 등)에서는 이미 큰 챕터 보스 스프라이트에 ×2가
다시 곱해져 전투 화면 대부분을 덮고, 배치 애셋의 `SpawnScale`은 무시된다.

고치는 법: `SpawnTrialFoe`가 565~584의 세 갈래를 그대로 따라가게 한다.

**결함 B — 귀문 중에 적 체력이 보이지 않는다 (`TrialHud.cs`)**

`TrialHud`의 직렬화 필드에 체력 표시가 없고, 보스용 `BossHud` 막대는 귀문에서
숨는다. 180초짜리 3연전 내내 플레이어는 자기가 깎고 있는지 알 수 없다.
실측에서도 1/3이 87초 동안 그대로였는데 화면만으로는 진행 중인지 멈춘 건지
구분이 안 됐다. 4단계 UI 폴리싱 항목으로 올린다.

부수적으로 격노 문구가 적 스프라이트 위에 겹쳐 낮은 대비로 찍히고, 진입 안내
문구는 1080px 폭에서 좌우 여백이 각 60px뿐이라 더 좁은 기기에서 잘릴 여지가 있다.

**결함 C — 개발 패키지명이 `ProjectSettings.asset`에 남았다 (`TrialFieldTestBuilder.cs`)**

`finally`가 `PlayerSettings.SetApplicationIdentifier`로 메모리만 되돌려서,
디스크에는 `Android: com.studio202.onikiri.dev`가 **커밋 대기 상태로 남았다.**
게다가 다음 빌드가 그 값을 "원래 값"으로 읽으므로 한 번 오염되면 스스로 낫지
않는다. 출시 빌드의 패키지명이 바뀌는 사고다.

조치(이번에 적용):
- 원래 값을 읽을 때 `.dev` 접미사를 떼고 시작 — 남은 오염이 스스로 낫는다.
- `finally`에서 `AssetDatabase.SaveAssets()`로 디스크까지 밀어 넣는다.
- `ProjectSettings.asset`은 `git checkout`으로 운영 값(`com.studio202.onikiri`)
  으로 되돌렸다. `Assets/google-services.json`도 무수정 확인.

### 9.8 남은 정리

- 폰 네트워크가 아직 꺼져 있다(`svc wifi/data disable`). 개발 패키지를 지운 뒤
  되살려야 조작된 st150 기록이 운영 리더보드로 새지 않는다.
- 기기의 개발 패키지에는 합성 시나리오 세이브(st150 등)가 들어 있다. 운영
  패키지와 물리적으로 분리돼 있으나, 실기 확인이 끝나면 삭제한다.

### 9.9 결함 D — 스킬 탭이 눌리지 않는다 (실기에서 사용자가 발견)

**증상**: 하단 「스킬」 탭이 밝게 열려 있는데 눌러도 아무 일도 없다. 캐릭터
패널이 그대로 남는다. 장비·동료·상점은 정상 전환된다.

**원인**: 씬의 `LockedTab(스킬).screen`이 `{fileID: 0}`. HEAD에는
`{fileID: 1174662494}`가 들어 있었다. `LockedTab`의 계약이 "`screen`이 비면
잠금 표시만 하고 눌리지 않는다"라서, 참조가 죽으면 **콘솔 한 줄 없이** 탭이
죽는다.

참조가 죽은 경위: 3.1에서 씬 빌더를 돌리다 서드파티 VFX meta 잠금으로
`Build Combat Content`가 중간에 끊겼다. `SkillPanelBuilder.Build()`는 이미
돌아 옛 `SkillPanel`을 파괴하고 새로 만들었는데(fileID 1174662494 → 901515535),
탭을 다시 물리는 `WireLockedTabs()`는 그 뒤라 도달하지 못했다.

**진짜 원인은 그보다 앞에 있다.** 이 실패 모드는 이미 알려져 있었고
(`BattleContentBuilder.RelinkScreenTabs()` 주석이 "45c에 물렸다: … Build
Equipment Panel이 판을 새로 만들자 장비 탭이 잠긴 채 남았다"로 기록),
대책으로 판 빌더가 끝에서 `RelinkScreenTabs()`를 부르게 돼 있다. 그런데

| 판 빌더 | `RelinkScreenTabs()` 호출 |
|---|---|
| `EquipmentPanelBuilder` | 있음 (167행) |
| `PetPanelBuilder` | 있음 (141행) |
| `QuestPanelBuilder` | 있음 (140행) |
| `ShopPanelBuilder` | 있음 (294행) |
| **`SkillPanelBuilder`** | **없었음** |

다섯 중 스킬만 빠져 있었다. 그래서 같은 실행에서 장비 판도 새로 만들어졌지만
(fileID 2096869448 → 353250070) 장비 탭은 멀쩡했고 스킬 탭만 죽었다.

**조치**

1. `SkillPanelBuilder.Build()` 끝에 `BattleContentBuilder.RelinkScreenTabs()`
   추가 — 빌더 실행 순서에 대한 의존이 사라진다.
2. 씬 복구: `RelinkScreenTabs()` 실행 → `screen`과 상호 배타 목록
   (`otherScreens`)까지 전부 다시 물렸다. SkillPanel 참조 수가 HEAD와 같은
   **13개**로 돌아왔다.
3. 같은 사고로 함께 끊겨 있던 `SkillPerformer.nameFlash` ·
   `SkillPerformer.screenFlash`도 복구했다(오의 이름 번쩍임·화면 번쩍임).
   이 둘은 `VerifyWiring()`이 잡아 준 것이고, 사용자가 스킬 탭을 지적하지
   않았다면 함께 묻힐 뻔했다.
4. `VerifyWiring()` 재실행 결과 **통과(True)**. `TrialHud` 참조 15개도 전부
   non-null 유지.

**교훈**: 빌더가 중간에 끊기면 "만든 것"과 "물린 것"이 어긋난 채 저장된다.
`Build Combat Content`를 끊는 서드파티 VFX meta 잠금은 아직 그대로이므로,
이 빌더를 돌린 뒤에는 `VerifyWiring()`을 반드시 확인해야 한다.
