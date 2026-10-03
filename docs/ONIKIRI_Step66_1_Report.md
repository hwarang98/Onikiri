# ONIKIRI 66.1단계 보고서 — 명중·회피 축 해금 st1

- **브랜치** `feature/step66-1-hit-rating-from-start` (develop c338a44에서 분기)
- **상태** 적용 완료. EditMode **1053/1053** · PlayMode **60/60**, 둘 다 Unity MCP(§6). 커밋 1회, push 승인 대기
- **건드리지 않은 것** 비용·계수·보정 지수(1.0)·잡몹 체력·귀문 M표·k=0.45·150게임초·격노·심층 지수·경험치 축 Lv.15 게이트·귀참 st20·`UIStrings`·Cloud/·firestore.rules·실사용 세이브

## 0. 한 줄 요약

명중(st11)·회피(st5) 해금을 st1로 내리고 기대 명중 곡선을 st1부터 다시 맞췄다. 밴드·코리더 순서·온보딩이 그대로 버텨서 **초반 잡몹 체력 되갚기는 필요 없었다.** 하한 플레이어의 여섯 게이트 빌드가 비트 단위로 같아 **귀문 M표도 그대로다.**

## 1. 변경 파일

| 파일 | 변경 |
|---|---|
| `Scripts/Balance/Curves/AccuracyCurve.cs` | `UnlockStage` 11 → **1**. 기대 곡선 상수 22.1 / 3.0144 → **−4 / 2.98**(§2), `ExpectedValueAtStage`의 해금 전 분기 삭제. 주석 |
| `Scripts/Balance/Curves/EvasionCurve.cs` | `UnlockStage` 5 → **1**. 주석(해금 근거, BaseCost 12 재측정 결과) |
| `Scenes/Main.unity` | 강화 탭 명중·회피 행의 `UpgradeButton.unlockStage` 11·5 → **1** 두 줄. 빌더(`UpgradePanelBuilder`)가 `AccuracyCurve/EvasionCurve.UnlockStage`에서 쓰는 값과 같다. MCP가 없던 동안 YAML에서 직접 고쳤다. MCP 복구 뒤 에디터에 로드된 씬(dirty 아님)에서 트랙 9·10의 `unlockStage`가 1이고 `AccuracyCurve/EvasionCurve.UnlockStage`와 같음을 execute_code로 대조했다(트랙 6 골드 획득 st6은 그대로). Build Combat Content는 씬을 디스크에서 다시 열고 폰트 잔재를 남겨서 돌리지 않았다 |
| `Tests/EditMode/AccuracyEvasionTests.cs` | `Unlocks_SitWhereTheyMeanSomething` → **`Unlocks_FromTheFirstStage`**: 두 해금이 1이고 곡선 추종이 st1에 명중을 실제로 사는지(Lv.5) 본다. 옛 단언(회피 = 무강화 첫 사망 스테이지, 명중 > 지역 1)은 확정과 정면으로 어긋나 지웠다 |
| `Tests/EditMode/StageSimulationTests.cs` | `EveryAxisIsBoughtAtLeastOnceThrough20` 주석: 첫 구매 명중 st11 → **st1**, 회피 st8 → **st9**(66단계 기준 실측도 st9였다 — 옛 주석이 낡아 있었다) |
| `Tests/EditMode/EvolutionCharacterizationTests.cs` | 코리더 비트 배열 8개 중 6개 재굽기(생존 여유 둘은 그대로) + 주석 |
| `Tests/EditMode/PromotionEconomyFixture.cs` | `ProgressionGems`(st55·st61 업적이 한 스테이지씩 당겨짐)·`CumulativeSeconds`(st200 3339.0 → 3341.2초) 재굽기 + 주석 |

재굽기는 하네스(§6)에서 했다. 하네스가 develop의 기존 배열 8개와 경제 픽스처를 **비트 단위로 그대로 재현**하는 것을 먼저 확인했다.

## 2. 기대 곡선 재적합

곡선 추종 명중 레벨 실측: st1~10 = 5 5 6 7 8 11 13 16 18 22, st11 = 25(66단계와 같다). **st11부터는 레벨 경로가 st500까지 한 칸도 다르지 않다** — 해금 전 열 스테이지 동안 산 칸이 st11에서 원래 경로에 합류한다.

직선 하나로 이 모양을 다 따라갈 수는 없다. st1에 Lv.5(첫 칸 2골드라 바로 산다)이고 st11 뒤는 칸당 약 3이다. 후보 비교(허용 1%p, 보스 상대 기대 명중률 vs 실측):

| L* = a + k(st−1) | st1~10 최대 오차 | st11~500 최대 오차 |
|---|---|---|
| 65단계 직선을 늘인 것 (−8.04, 3.0144) | 1.38%p @st4 — **불합격** | 0.32%p |
| 레벨 최소제곱 (−7.54, 3.0294) | 1.24%p @st4 — **불합격** | 0.27%p |
| **채택 (−4, 2.98)** | **0.93%p @st2** | **0.47%p @st13** |
| (−3, 2.95) | 0.93%p @st2 | 0.55%p |

채택한 직선은 st1~3을 강화 전 값으로 읽어서(L < 1) 그 자리 오차 0.9%p가 남는다. 절편을 올려 st1을 맞추면 st11 뒤가 위로 뜬다. 허용 1%p까지 0.07%p 남는다 — 앞으로 초반 경제를 건드리는 스텝에서는 이 검사가 먼저 물린다.

`StageCurve.AccuracyAxisCompensation`은 손대지 않았다. 해금이 1이라 분기가 늘 통과하고, 보정이 st1~10에 생긴다(st5 x1.0187, st10 x1.0511). 지수 1.0 유지. st50에 얼리는 이득이 곡선 재적합으로 아주 조금 바뀌어 심층 보정이 0.1% 안에서 움직인다(st50 1.2026 → 1.2037).

## 3. 밴드 전후 표

에셋 필드(`DevSimField`와 같은 산술)로 쟀다. 여유는 곡선 추종 / 무과금(`GemsFromQuestsOnly`) / 명중 안 산 플레이어.

| st | 등급 | 명중 Lv | 회피 Lv | 명중률 잡몹 / 보스 | 회피율 | 보스 여유 추종 | 무과금 | 명중 안 삼 |
|---|---|---|---|---|---|---|---|---|
| 1 | 일반 | 1 → **5** | 1 | 90.0 / 85.7 → **90.7 / 86.6%** | 0% | 1.782 → **1.786** | 1.782 → 1.786 | 1.782 |
| 5 | 챕터 | 1 → **8** | 1 | 89.4 / 84.9 → **90.6 / 86.5%** | 0% | 1.507 → **1.495** | 1.507 → 1.495 | 1.507 → 1.479 |
| 10 | 피날레 | 1 → **22** | 4 | 88.7 / 84.0 → **91.6 / 87.9%** | 3.8% | 1.659 → **1.635** | 1.659 → 1.635 | 1.659 → 1.578 |
| 20 | 피날레 | 51 | 6 | 93.1 / 90.0% | 5.4% | 1.485 → **1.480** | 1.237 → 1.233 | 1.365 → 1.360 |
| 30 | 피날레 | 84 | 9 | 94.2 / 91.5% | 7.6% | 1.641 → 1.637 | 1.445 → 1.442 | 1.461 → 1.458 |
| 50 | 피날레 | 144 | 15 | 95.1 / 92.8% | 10.5% | 2.367 → 2.365 | 2.018 → 2.016 | 2.134 → 2.132 |

| 계약 | 66단계 | 66.1 |
|---|---|---|
| 온보딩 st1~5 | 171.3초 | **171.5초** (+0.1%) |
| st1~30 누적 | 807.3초 | 809.0초 |
| 코리더 순서: st20 피날레 < st5 챕터 | 1.485 < 1.507 (차 0.022) | **1.480 < 1.495** (차 0.015) |
| 코리더 바닥 여유(추종·무과금) | +0.04 @st2 | +0.04 @st2 |
| 코리더 천장 여유(추종) | +0.04 @st10 | +0.06 @st30 |
| 가속 구간 바닥 / 천장 | +0.85 / +0.15 | +0.85 / +0.15 |
| 무강화 게이트 (화력 / 체력) | st2 / st5 | st2 / st5 |
| 명중 안 산 바닥 st15 (≥ 1.15) | 1.681 | **1.673** |
| 명중 안 산 최저 st1~50 (≥ 1.0) | 1.365 @st20 | **1.360 @st20** |
| 첫 구매 명중 / 회피 | st11 / st9 | **st1** / st9 |
| 명중 죽은 버튼: st1~50 / 무한 구간 / 66 전 세계 | 3.83% / 3.86% / 4.38% | **4.06%** / 3.86% / 4.38% |
| 회피/체력 자 (경로 위 최악) | x1.22 @st13 | x1.98 @st3 (st5부터 x1.22 @st13) |
| 도달 하한 / 리드500 | st399 / 101 | st399 / 101 |

회피/체력 자가 x1.98이 된 것은 자의 출발점이 `EvasionCurve.UnlockStage`라서다 — 이제 st1~4(회피 Lv.1, 체력 칸이 싸고 보스 명중이 낮은 자리)까지 잰다. st5 뒤는 비트 단위로 같은 경로라 x1.22 그대로이고, 5배 문턱 안이다.

## 4. 되갚기 — 없음

확정이 허락한 손잡이(초반 잡몹 체력, `OnboardingMobHealth`)는 쓰지 않았다.

- **회피 첫 비용 12 재측정(지시 3):** st1 해금에서도 곡선 추종의 첫 회피 구매는 st9이고, 회피 레벨 경로는 **st1~500 전부 66단계와 같다.** 65단계가 막으려던 "해금 즉시 여섯 칸" 경로가 생기지 않는다 — 앞 구간에서는 화력이 이 칸을 이긴다. 코리더 순서는 위 표대로 유지된다.
- **명중:** 처음에 해금만 내리고 옛 기대 곡선을 그대로 두었을 때는 순서가 뒤집혔다(st20 1.450 > st5 1.431, 코리더 바닥 −0.03 @st2). 원인은 체력이 아니라 **기대 곡선이 st1~10을 크게 과대평가한 것**(st1에 Lv.22 기대, 실측 Lv.5 → 기대 오차 3.8%p @st5)이었다. §2 재적합으로 보정이 실측을 따라가자 순서와 바닥이 66단계 값으로 돌아왔다. 그래서 체력 손잡이가 필요 없다.

## 5. 귀문 재굽기 — 없음

하한 플레이어(`GemsFromQuestsOnly`)의 여섯 게이트(st30·40·50·70·100·150)에서 `PlayerAt`가 읽는 네 값(ExpectedDps·MaxHealth·RegenPerSecond·DodgeChance)과 귀문 체력 바탕(`BossHealthBeforeHitRating`)이 **전부 비트 단위로 같다.** 명중 구매가 st3·st6~9의 공격력을 한 칸씩 늦췄지만 st10에 합류한다. M표 무수정, k=0.45·150게임초·격노·심층 지수 무수정.

경험치 축 Lv.15 게이트(곡선 추종이 st15에 Lv.15)·귀참 st20: 캐릭터 레벨 경로 st1~30이 같다. 무수정.

UI: 강화 탭 명중·회피 행의 `unlockStage`가 1이 되어 `MaxStageReached(≥1) < 1`이 늘 거짓 — st1부터 활성이다. `UIStrings` 변경 없음, 새 글자 없음.

## 6. 테스트 수치

### 6.1 지시서의 검증(MCP)

세션 앞부분에는 MCP 서버가 `ECONNREFUSED`였고, 사용자가 `/mcp`로 다시 붙인 뒤 돌렸다. 매 실행 전에 Play 모드 아님 · 컴파일 아님 · 콘솔 에러 0을 확인했다. 실사용 세이브는 실행 전후 md5가 같다(ee71fdb4…).

**EditMode — 1053 / 1053.** 2026-10-03 09:02 시작, 347.3초, 실패 0 · 건너뜀 0.

**PlayMode — 60 / 60 (픽스처 단위).**

| 픽스처 | 결과 | 시간 | 비고 |
|---|---|---|---|
| HitRatingPlayTests | 5/5 | 4.5초 | |
| PromotionTrialPlayTests | 30/30 | 78.4초 | MCP 작업은 "tests did not start within timeout"으로 끝났지만, Unity가 쓴 `TestResults.xml`에 09:08:43~09:10:01 실행 30/30 통과로 남았다. 작업 추적만 놓친 것이라 재요청하지 않았다 |
| TrialPresetEquivalencePlayTests | 2/2 | 2.1초 | |
| CloudConflictPlayTests | 7/7 | 7.2초 | |
| CloudSaveBootPlayTests | 5/5 | 3.7초 | |
| CloudSessionPlayTests | 8/8 | 6.4초 | PromotionTrial과 같은 패턴. `TestResults.xml` 09:11:41~47, 8/8 통과 |
| StatPointResetPlayTests | 3/3 | 2.5초 | |

"시작 안 됨" 응답이 두 번 나왔지만 둘 다 결과 파일로 실행·통과가 확인되어 타임아웃 2회 연속 규칙에는 걸리지 않았다. 두 결과 파일 사본은 세션 scratchpad에 두었다(`pm_PromotionTrial.xml`, `pm_CloudSession.xml`).

### 6.2 하네스 실행 — MCP 전 사전 점검

MCP가 없는 동안 오차를 먼저 잡으려고 EditMode 검사 어셈블리를 에디터 밖에서 돌렸다. Unity의 csc로 Runtime·DevTools·EditMode 세 어셈블리를 컴파일하고, NUnit 리플렉션 러너로 실행했다. 에셋 읽기(`AssetDatabase`)만 같은 산술의 YAML 리더로 바꿨다(사본에서만, 프로젝트 파일 무접촉).

- **실행 가능 690건: 통과 690 · 실패 0.** 밴드·코리더 순서·`EveryAxisIsBoughtAtLeastOnceThrough20`·명중 죽은 버튼·`Accuracy_ExpectedCurve_TracksTheSimulation`·`Onboarding_TakesTheStep64Time`·`HealthGate_BitesAfterTheDamageGate`·`SkippingAccuracy_StillClearsTheTunedZone`·`Evasion_StaysWithinFiveTimesOfHealth`·코리더 비트·경제 픽스처·귀문 M표 검사가 여기 들어간다.
- 재굽기 전에 같은 러너가 정확히 세 건(코리더 비트 둘, 경제 픽스처 st55)을 잡았다 — 러너가 실패를 실제로 검출한다는 확인이다.
- **실행 불가 363건** — Unity 엔진 네이티브 호출(MonoBehaviour·ScriptableObject·JsonUtility 등)이 필요한 검사다. 클라우드 세이브·세션·앱체크·VFX·풀링 등이고, 이번 변경(곡선 상수 둘·씬 두 줄)과 닿는 것은 `UpgradeTrackTests`·`GrowthTabStep66Tests` 일부 정도다. MCP로만 확인할 수 있다.

## 7. 남은 것

1. push(승인 후).
2. 기대 곡선의 st1~3 오차 0.93%p는 허용 1%p에 붙어 있다(§2). 초반 경제를 건드리는 스텝이 먼저 이 검사를 깰 것이다.
3. MCP `run_tests`가 PlayMode 작업을 실제로는 돌려 놓고 "시작 안 됨"으로 보고하는 일이 이번에도 두 번 있었다. 결과는 `TestResults.xml`로 확인할 것.
4. 출시 전 교체 항목 — 이번 스텝이 새로 만든 것은 없다.
