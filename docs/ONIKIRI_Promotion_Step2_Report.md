# 승급·전직 재설계 — 2단계 (도메인·데이터) 보고서

기준 커밋 `b229bee`. 승인값 A-1~A-8을 반영해 **순수 도메인·데이터·마이그레이션까지만**
구현했다. 전투·씬·프리팹·UI·Firebase는 손대지 않았고 커밋도 하지 않았다.

**EditMode 723개 중 718 통과 / 5 실패.** 실패 다섯은 전부 **하나의 뿌리**에서 나오고,
그 뿌리가 §7의 중단 보고다.

---

## 0. 먼저 고친 검증 계산기 둘

### 0.A 마이그레이션 경계 — `gateStage < maxStageReached`

`maxStageReached`는 최전선이지 클리어 기록이 아니다. st30에 **도착한** 상태는 st30을
클리어한 상태가 아니므로 등호를 쓰면 문 하나가 전투 없이 열린다.

`PromotionTrialCatalog.TierAtFrontier`가 단일 출처이고, `EvolutionCurve.ExpectedTierAtStage`·
`PromotionBandModel.TierAtStage`·`SaveData.GateEvolutionTierFor`가 전부 이것을 부른다.

| 최전선 | 29 | 30 | 31 | 40 | 41 | 50 | 51 | 70 | 71 | 100 | 101 | 150 | 151 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 티어 | 0 | 0 | 1 | 1 | 2 | 2 | 3 | 3 | 4 | 4 | 5 | 5 | 6 |

전부 통과 — `PromotionDomainTests.TierAtFrontier_HasTheRightBoundaries` ·
`MigrationV21_HasTheRightBoundaries` · `EvolutionTests.ExpectedCurve_TracksTheSimulation`
셋이 같은 표를 각자 잰다. 기존 티어 보존(`max`)도 함께 확인했다.

### 0.B 평균 재도전 시간 — 곱하기 모형 폐기

```
옛  (성공 + 오버헤드) x 횟수                             <- 45초짜리를 1.5번으로 읽는다
새  성공 + max(0, 횟수-1) x 실패 + 횟수 x 오버헤드        <- 실패 0.5번 + 성공 1번
```

오버헤드에 횟수를 곱하는 것은 그대로다 — 실패한 시도도 진입 연출과 결과 화면을 지난다.

**실패 시간은 게이트별 실측이다** (하한 x0.35, 새 격노 규칙 90s/10s/x1.3):

```
문1 92.6s   문2 98.6s   문3 96.6s   문4 94.6s   문5 94.6s   문6 94.6s
```

전부 격노(90초)와 폐쇄(180초) 사이이고 성공(45초)의 두 배가 넘는다. 값을 굽지 않고
`PromotionEconomyFixture.TrialFailureSecondsFloor`가 매번 시뮬레이션에서 잰다.

#### 수정 후 도달일 (문1~6 / st150 / st200)

| 정책 | 시도 | 파밍 | 문1~6 | st150 | st200 | 귀문 총시간 | 귀문 몫 |
|---|---:|---|---|---:|---:|---:|---:|
| 20분 **교정후** | 1.0 | 0 | 1 1 2 2 3 3 | 3일 | 4일 | 324s | 10.1% |
| 20분 **교정후** | 1.5 | 0 | 1 2 2 2 3 3 | 3일 | 4일 | **637s** | 18.1% |
| 20분 (교정전) | 1.5 | 0 | 1 1 2 2 3 3 | 3일 | 4일 | 486s | 14.5% |
| 5분 **교정후** | 1.0 | 0 | 3 4 5 7 9 11 | 11일 | 13일 | 324s | 10.1% |
| 5분 **교정후** | 1.5 | 0 | 3 5 6 7 9 12 | **12일** | 14일 | **637s** | 18.1% |
| 5분 (교정전) | 1.5 | 0 | 3 4 6 7 9 11 | 11일 | 14일 | 486s | 14.5% |

교정이 귀문 총시간을 486초 -> **637초(+31%)** 로 올리고, 5분 정책의 st150 도달을
11일 -> 12일로 하루 민다. 20분 정책은 하루 안에 흡수된다.

st150 도달이 v1.5의 12일(5분)보다 빠른 것은 재도전 교정과 **반대 방향의** 변경 때문이다 —
게이트 무료 티어가 하한 플레이어의 화력을 올려 st200 누적 전투 시간이
4,084.9초 -> 3,495.4초(-14.4%)가 됐다.

#### 파밍 민감도 — **문1에만** 넣는다

하한의 0.50배 화력은 **문1에서만** 실패하고 문2~6은 86초에 통과한다
(`PromotionTrialTests.HalfPowerFloor_OnlyFailsAtTheFirstGate`). 문1이 하한 플레이어가
유일하게 0티어로 서는 자리라서 그렇다. 여섯 문에 일괄로 파밍을 넣으면 그 세계보다
다섯 배 비관적인 표가 나온다.

| 정책 | 파밍 | 문1~6 | st150 | st200 | 귀문 몫 |
|---|---|---|---:|---:|---:|
| 20분 | 5분(문1) | 1 2 2 2 3 3 | 3일 | 4일 | 17.8% |
| 20분 | 10분(문1) | 1 2 2 3 3 4 | 4일 | 4일 | 24.3% |
| 5분 | 5분(문1) | 3 5 6 8 10 12 | 12일 | 14일 | 17.8% |
| 5분 | 10분(문1) | 3 6 7 9 11 13 | 13일 | 15일 | 24.3% |

`GateFarmSeconds`는 게이트별 배열로 유지했고 `FarmAtGateOne` / `UniformFarm` 둘 다 있다.

---

## 1. 제거·대체된 기존 계약

지우지 않고 **다른 질문으로 바꿨다.** 왜 유효하지 않은지가 각 검사 주석에 남아 있다.

| 기존 | 처리 | 대체 |
|---|---|---|
| `GemLadder_TotalIsIntentional` (3,010) | 대체 | `EvolutionTests.PromotionCostsNothing` — 승급의 값이 0이고, **두 보석 정책의 티어 곡선이 스테이지마다 완전히 같다**(재화 직교성) |
| `Gems_AreTheBindingConstraintForTheFloorPlayer` | **폐기** | `EvolutionTests.FloorPlayerIsGatedByPower_NotByGems` — 하한도 여섯 티어를 갖고, 병목이 재화가 아니라 화력이다 |
| `Evolution_MovesDpsAndProgress` | 재계약 | 같은 이름, `SkipEvolution`의 뜻이 "귀문을 안 깬다"로 바뀜. 자(4%)와 구간(st31~50) 유지 |
| `ExpectedCurve_TracksTheSimulation` | 재계약 | ±1티어 허용 -> **등호**. 구간 st50 -> st200(문 여섯 중 셋이 st50 뒤에 있다) + 경계 열셋 |
| `CorridorCompensation_IsExactlyTheMultiplicativeIdentity` | 재계약 | `CorridorHasNoPromotionAtAll` — 잴 보정값이 없어졌다. 근거가 "1.0을 곱한다"에서 "첫 문이 st30 클리어"로 바뀜 |
| `SoftCapExponent_ZeroFortyFive_IsTheOnlyCandidateThatFitsEveryRule` | **판정 뒤집힘** | `EverySoftCapCandidate_NowFitsTheBand_AndTheChoiceMovesToPlayMode` — §5 참고 |
| `TryEvolve`의 재화 소비 (`StageSimulation`) | 제거 | 게이트 기반 무료. 라이브 `EvolutionSystem.TryEvolve`는 **3단계까지 그대로** (§6) |

`EvolutionCatalog`의 `GemCost`/`GoldCost` 표는 **일부러 남겼다.** 전직 패널이 아직 그
값을 받는다 — 지금 0으로 만들면 귀문 없이 여섯 티어가 통째로 풀린다.

---

## 2. 새 데이터·도메인 API

### `Onikiri.Progression.PromotionTrialCatalog` (신규)

```
GateStages                30 / 40 / 50 / 70 / 100 / 150
TotalHealthMultiple       1.7376 / 1.9528 / 2.1813 / 3.1164 / 8.6655 / 11.2413   (§3)
SwapSeconds 2 · EnrageSeconds 90 · EnrageIntervalSeconds 10
EnrageMultiplierPerStep 1.3 · CloseSeconds 180
FoeHealthShare            0.25 / 0.25 / 0.50
FoeAttackIntervalSeconds 2 · FirstAttackDelaySeconds 2
SoftCapExponent           0.45  (**후보 확정값**. PlayMode 실측 전이다)
PromotionGemCost 0 · PromotionGoldCost 0
SoftCapNotice             "귀문에서는 지나친 화력이 완만하게 조정됩니다."

TierAtFrontier(frontier)                    지나온 문의 수 = count(gate < frontier)
GateStageOfTier / GateNumberAtStage / IsGateStage / NextGateStage
RequiredGateAfterClearing(stage, tier)      진행 판정 — **3단계가 부를 자리**
TierAfterTrialVictory(tier, gate)           무료 상승 = max(tier, gate)
EnrageStepsAt / EnrageMultiplierAt
TotalTrialHealth / FoeHealth / ReferencePowerForGate
```

### `Onikiri.Progression.TrialPowerScore` (테스트 -> 프로덕션 이동)

여섯 항(`AutoAttack` / `Skills` / `MultiHit` / `DamageOverTime` / `Companions` /
`BossApplicableSpecials`)과 공통 `DamageScale` / `EffectivePower` / `Apply`.
잡몹 전용 처형은 `BossApplicableSpecials`에 0으로 넣는다.

프로덕션으로 옮긴 이유는 3단계가 **실제 피해 파이프라인에서** 이것을 부르기 때문이다 —
테스트에만 있는 산식은 런타임이 같은 답을 낸다는 보장이 없다.

### `SaveData` (v21 설계, **비활성**)

```csharp
public static int  GateEvolutionTierFor(int existingTier, int maxStageReached)
public static bool ApplyGateEvolutionTier(SaveData data)
```

`Migrate`가 부르지 않고 `CurrentVersion`은 **20 그대로**다. 신규 필드 없음.
`PromotionDomainTests.IntermediateState_SaveVersionIsStillTwenty`가 그 사실을 못 박는다.

---

## 3. `EvolutionMarginExponent` 0.00 실측

`StageCurve.EvolutionMarginExponent`와 `EvolutionCompensation`을 **삭제**했고
`BossHealthForStage`의 곱 한 줄도 지웠다. `Math.Pow(x, 0)`을 남기지 않았다.

| 검사 | 결과 |
|---|---|
| st1~30 비트 불변 | **통과** — 240개 원시 비트 그대로 (`EvolutionCharacterizationTests` 넷) |
| 오버레이 모형 재현 | **통과, 오차 1.0000** (st31~200). 자를 0.95~1.02 -> **0.995~1.005**로 조였다 |
| st31~50 밴드 | **통과** — 추종 천장 / 하한 바닥 전부 (`AcceleratedZone_*`, `EveryCandidate_*`) |
| st51~150 심층 바닥 | **통과** |
| 하한 도달층 >= 260 | **통과** |
| 과금 리드 <= 240 | **통과** |
| 문1 실질 상승 | **10.0%** (>= 10%) |
| 문6 실질 상승 | **20.0%** (>= 20%) |
| **st100 -> st200 심층 수렴** | **실패 — §7** |

문 직후 실질 상승이 명목 배수와 **정확히 같다**(보정이 없으므로).
`PromotionDomainTests.EveryGate_MovesRealPowerByTheApprovedAmount`가 그 등식을 잰다.

### M 표를 다시 구웠다 — 승인값에서 벗어난 유일한 데이터

v1.5가 승인받은 `1.74 / 1.68 / 1.49 / 1.91 / 4.75 / 5.36`은 **지수 0.42의 세계에서** 잰
값인데, 같은 승인이 지수를 0.00으로 내렸다(A-1). 두 결정이 같은 표를 두 방향으로 당긴다:

- 보정항 제거 -> 게이트 보스 체력이 st50+에서 x1.354만큼 가벼워진다
- 무료 게이트 티어 -> 하한 플레이어가 1티어가 아니라 2~5티어로 문을 만난다

| 문 | v1.5 (e=0.42) | 2단계 재유도 (e=0.00) | 옛 표를 유지했다면 하한 클리어 |
|---:|---:|---:|---:|
| 1 | 1.74 | **1.7376** | 45.0s |
| 2 | 1.68 | **1.9528** | 39.3s |
| 3 | 1.49 | **2.1813** | 32.1s |
| 4 | 1.91 | **3.1164** | 29.2s |
| 5 | 4.75 | **8.6655** | 26.5s |
| 6 | 5.36 | **11.2413** | **23.6s** |

옛 표를 고집하면 하한이 육문을 23.6초에 끝내고 밴드 하한(35초)이 통째로 무너진다.
**값이 아니라 규칙(45초 하한 앵커)이 설계**이므로 규칙을 지켰고, 일문이 거의 안 움직인
것(1.74 -> 1.7376)이 그 판단이 옳다는 증거다 — st30에는 지나온 문이 없어서 두 변경 중
어느 쪽도 닿지 않는다.

`PromotionTrialTests.HealthMultiple_TracksTheFortyFiveSecondAnchor`가 앞으로 이 벌어짐을
자동으로 잡는다.

---

## 4. 전투 실측 (k=0.45, 새 격노 규칙)

| 문 | st | 하한 | 중간 | 곡선 추종 | 0.70x | 0.50x | 0.35x |
|---:|---:|---|---|---|---|---|---|
| 1 | 30 | 45.0s | 43.9s | 42.8s | 62.6s | **사망 85.0s** | 사망 92.6s |
| 2 | 40 | 45.0s | 43.7s | 42.3s | 62.6s | 86.0s | 사망 98.6s |
| 3 | 50 | 45.0s | 43.2s | 41.5s | 62.6s | 86.1s | 사망 96.6s |
| 4 | 70 | 45.0s | 41.2s | 37.8s | 62.6s | 86.0s | 사망 94.6s |
| 5 | 100 | 45.0s | 39.5s | 34.7s | 62.6s | 86.0s | 사망 94.6s |
| 6 | 150 | 45.0s | 39.6s | 34.9s | 62.6s | 86.1s | 사망 94.6s |

- 하한 45.0초 (앵커 그대로) · 중간 39.5~43.9 · 추종 34.7~42.8 — 전부 25초 바닥 위
- 격노 여유 최소 2.58배 (90 / 34.9)
- 정지 시도(DPS 1/100 + 최대 재생): 96~132초에 사망. 폐쇄(180초) 안, 격노 단계 > 0
- 0.70x 통과 / 0.35x 전 문 실패 — 폭 계약 유지

---

## 5. 소프트캡 — 판정이 뒤집혔다

1.6단계에는 k=0.45가 **유일한** 답이었다. 근거는 두 플레이어의 DPS 격차 **x3.61**이었고,
0.60에서 곡선 추종이 23.0초로 25초 바닥을 못 넘었다.

승급이 무료가 되면서 하한 플레이어도 같은 티어를 받고, **격차가 x1.91로 줄었다.**

| k | 추종 최속 | 하한 최장 | 판정 |
|---:|---:|---:|---|
| 0.35 | 42.9s | 45.0s | 통과 |
| **0.45** | **34.7s** | **45.0s** | **통과** |
| 0.60 | 31.8s | 45.0s | 통과 (1.6단계에는 23.0s로 실패했다) |

**셋 다 통과한다.** 캡은 여전히 필요하다(격차 1.91 > 밴드 비 1.571, 무캡 추종 약 25.5초)
— 다만 **밴드가 더 이상 k를 가르지 못한다.** 고를 근거는 체감뿐이고 그것은 PlayMode에서만
잴 수 있다. 그래서 0.45는 **후보 확정값**으로만 기록했고, 카탈로그 주석에도 "최종
고정값이 아니다"라고 적었다.

캡 계약 다섯(공통 배율 / 빌드 모양 무관 / 기준 이하 불변 / 단조 증가 / 기준이 시험에서
유도)은 전부 통과한다. 실제 피해 파이프라인 연결은 3단계다.

---

## 6. 중간 상태 안전성

셋을 검사가 지킨다 (`PromotionDomainTests.IntermediateState_*`):

| 항목 | 상태 | 검사 |
|---|---|---|
| `SaveData.CurrentVersion` | **20** | v20 세이브가 `Migrate`를 지나도 티어가 안 움직인다 |
| `StageProgress.AdvanceStage` | 게이트 **미연결** | 소스에 `PromotionTrialCatalog` 문자열이 없다 |
| `BossFight` | 귀문 모드 **없음** | 같은 방식 |

라이브 게임은 이 커밋에서 **귀문이 없는 게임**이다. 전직 패널도 옛 경로(보석+골드) 그대로
동작한다 — 밸런스 시뮬레이션만 새 세계를 잰다. 이 불일치는 의도한 것이고 3단계가 없앤다.

테스트 패널에 **읽기 전용** 절을 추가했다(`귀문 (승급전) — 2단계: 읽기 전용`) — 표·판정·
v21 미리보기만 표시하고 버튼이 없다. 미리보기가 이 절의 값어치다: 라이브 세이브를 올리기
**전에** 마이그레이션 결과를 눈으로 확인할 수 있는 자리가 여기 하나뿐이다.

---

## 7. **중단 보고 — 심층 수렴 계약이 깨진다**

### 실패한 검사 다섯 (전부 같은 뿌리)

| 검사 | 실측 | 기준 |
|---|---|---|
| `StageSimulationTests.DeepZone_MarginConverges` | st100 12.09 -> st200 18.19 (**+50.4%**) | < 35% |
| `StageSimulationTests.MasteryNeutralized_ReproducesTheStep42World` | st40 여유 1.666 | 1.3771 ±0.01 |
| `YodoPowerTests.YodoPowerNeutralized_ReproducesTheStep44World` | st105 천장 8.92 | <= 8.894 |
| `YodoPowerTests.Affinity_IsNotADeadButton` | 이득 **3.6%** | > 4% |
| `YodoPowerTests.SpiritSummon_IsNotADeadButton` | 이득 **2.486%** | > 2.5% |

### 뿌리 — 문 여섯 중 **둘이 무한 구간 안에** 있다

게이트 st100과 st150이 심층 구간(st51+)에 앉아 있고, 보정항이 사라져 그 두 문의 배수
x1.15 · x1.20 = **x1.38이 통째로 여유가 된다.**

```
전직이 없는 세계(SkipEvolution)   st100 -> st200 드리프트  +8.98%
게이트 무료 티어 + e=0.00         st100 -> st200 드리프트  +50.4%
                                  1.0898 x 1.38 = 1.504    <- 정확히 일치
```

뒤 셋(요도 두 축과 재현 앵커)은 같은 상승의 파생이다 — 심층이 쉬워지면 보스 시간이
총 시간에서 차지하는 몫이 줄고, 그 위에서 재는 다른 축의 % 이득이 희석된다.

### 지수를 되돌려도 안 풀린다

오버레이로 후보를 다시 훑었다 (드리프트는 낮을수록 좋고 기준 0.35):

| e | 드리프트 추종 | **드리프트 하한** | 문1 체감 | 문6 체감 |
|---:|---:|---:|---:|---:|
| 0.00 | 0.504 | **0.624** | 10.0% | 20.0% |
| 0.30 | 0.365 | 0.475 | 6.9% | 13.6% |
| 0.42 | 0.314 | 0.419 | 5.7% | 11.2% |
| 0.50 | 0.280 | 0.383 | 4.9% | 9.5% |
| 1.00 | 0.090 | 0.177 | 0.0% | 0.0% |

**하한 플레이어를 0.35 안에 넣으려면 e >= 0.58이 필요하고, 그러면 문1 체감이 4.1%로
떨어져 마일스톤 바닥(5%)을 못 넘는다.** 균일 지수로는 두 계약을 동시에 만족할 수 없다.

이것은 v1.5가 재지 않은 자리다 — 그 판의 밴드 검사는 st31~150의 바닥·천장과 도달층만
봤고, st100 -> st200 수렴은 후보 비교표에 없었다.

### 임의로 계수를 조정하지 않았다

지시대로 `BossHealthRampDeep`도 `YodoPowerMarginExponent`도 `AffinityTierStep`도 손대지
않았다. 판단이 필요한 선택지를 숫자와 함께 올린다:

| # | 선택지 | 대가 |
|---|---|---|
| **B-1** | 문 5·6에만 **게이트 모양 보정**을 되살린다 (지수 0.58~0.65) | st31~50 체감은 10%/12% 그대로 유지. 문5·6 체감이 20% -> 6~8%로 내려간다. 하한 드리프트 0.318~0.347로 복귀 |
| **B-2** | `BossHealthRampDeep`를 올려 심층에서 흡수한다 | 게이트 상승은 st100·st150의 **불연속 계단**인데 램프는 스테이지마다 곱해진다 — 문 사이 구간을 과보정한다 |
| **B-3** | 게이트 5·6을 st50 이하로 옮긴다 | 승인된 게이트 표(A) 변경. 여섯 문이 20스테이지 안에 몰린다 |
| **B-4** | `DeepZone_MarginConverges` 계약을 완화한다 | 심층 보스가 점점 형식이 된다. 요도 두 축의 죽은 버튼 문제가 그대로 남는다 |

B-1이 A-1(체감 최대화)의 의도를 **문제가 실제로 있는 구간에만** 양보하는 안이라 가장
좁은 변경으로 보이지만, 승인 없이 시작하지 않는다. 요도 두 축이 B-1로 회복되는지는
**재실측이 필요하다**(다른 정책의 런이라 오버레이로 추정할 수 없다).

---

## 8. 검증

```
EditMode 723 / 718 통과 / 5 실패 (§7)      약 216초
컴파일 오류 0 · 경고 0
런타임 씬·프리팹 변경 없음
Firebase 변경 없음 (firestore.rules / firestore.indexes.json / firebase.json 무수정)
커밋·푸시 없음
```

### 프로덕션 변경 파일과 목적

| 파일 | 목적 |
|---|---|
| `Progression/PromotionTrialCatalog.cs` (신규) | 귀문의 표와 순수 판정. 3단계 전투가 읽을 값 |
| `Progression/TrialPowerScore.cs` (신규, 테스트에서 이동) | 소프트캡의 구현 가능한 산식 |
| `Progression/StageCurve.cs` | `EvolutionMarginExponent`·`EvolutionCompensation` 삭제 + `BossHealthForStage`의 곱 한 줄 제거 |
| `Progression/EvolutionCurve.cs` | 기대 티어 곡선을 닫힌 식(st37+2/티어)에서 **게이트 표**로 교체 |
| `Progression/StageSimulation.cs` | `TryEvolve`가 재화·레벨을 안 보고 지나온 문의 수만 본다. `NeutralizeEvolution`의 보정 나눗셈 제거 |
| `Progression/SaveData.cs` | v21 마이그레이션 함수 둘 추가(**비활성**) + `evolutionTier` 의미 변경 문서화 |
| `Data/UIStrings.txt` | 소프트캡 안내 문구 등재 (아틀라스 하베스트의 출처) |
| `Editor/OnikiriTestPanel.cs` | 읽기 전용 귀문 절 (표·판정·v21 미리보기) |

### 신규·개작 테스트

```
신규  PromotionDomainTests            14   표 / 변환 / 진행 판정 / 무료 상승 /
                                           v21 경계·보존·멱등·범위·max 병합 /
                                           중간 상태 셋 / 글리프
신규  PromotionLedgerReportTests       1   도달일 표 출력 (계약이 아니라 보고서)
신규  PromotionTrialTests             +4   45초 앵커 추적 / 0.50배 문1 전용 실패 /
                                           실패 시간 실측 / 규칙의 프로덕션 출처
신규  PromotionEconomyTests           +3   재도전 교정 식 / 교정의 도달일 효과 /
                                           문1 파밍 민감도
신규  EvolutionTests                  +1   FloorPlayerIsGatedByPower_NotByGems
개작  EvolutionTests                   4   위 §1
개작  EvolutionCharacterizationTests   1   CorridorHasNoPromotionAtAll
개작  PromotionBandTests               2   오버레이 자 조임 / 생존 되먹임 계단
개작  PromotionEconomyTests            1   픽스처 동기화에 **전투 시간표** 추가
개작  PromotionTrialTests              1   k 후보 판정 뒤집기
```

`FixtureStillMatchesTheSimulation`에 전투 시간표 검사를 더한 것이 이번 판의 숨은 성과다 —
1.6단계에는 보석만 봤고, 그래서 누적 시간표가 14% 어긋난 채로 도달일 계약이 전부
초록이었을 수 있었다.

### 글리프

`친`(U+CE5C)이 현재 아틀라스(`FontCharset.txt`, 449자)에 **없다** — 이 문구가 처음
들여오는 글자다. `UIStrings.txt`에 등재해 두었고, `Onikiri/Art/Rebuild Font Charset` +
`Build Pixel Font Assets`는 **3단계에서 UI를 붙일 때** 돌린다. 2단계에서 굽지 않은 이유는
그것이 아트 애셋 변경이고 이 단계의 계약이 도메인·데이터·EditMode이기 때문이다.

---

## 9. 3단계 연결 목록

**하나의 원자적 변경이어야 한다.** 아래 넷 중 하나만 들어간 빌드가 나가면 진행이
막히거나 티어가 거짓이 된다.

1. `BossFight`에 `TrialMode` — 3연전 / 전환 2초 / 격노 90·10·x1.3 / 폐쇄 180
2. `StageProgress.AdvanceStage`에 `RequiredGateAfterClearing` 연결
3. 귀문 승리 -> `EvolutionSystem`에 `TierAfterTrialVictory` 적용 (무료)
4. `SaveData.CurrentVersion = 21` + `Migrate`의 v20 분기에서 `ApplyGateEvolutionTier`

함께 가는 것들:

5. `TrialPowerScore`를 실제 피해 파이프라인에 연결 — `damageScale`이 곱해지는 자리가
   **하나**여야 하고, `PetCombat`과 지속 피해가 그 자리를 지나는지 확인
6. `EvolutionSystem.TryEvolve` 제거 + `EvolutionCatalog`의 `GemCost`/`GoldCost` 삭제 +
   `EvolutionCurve.UnlockLevel`(Lv.30) 폐기
7. 진입 화면에 `SoftCapNotice` 1회 노출 + 폰트 아틀라스 재굽기
8. 테스트 패널 귀문 절에 입장·승리·실패·재도전 버튼
9. PlayMode 검사 — 기존 보스가 흔들리지 않는지(R6), `damageScale` 단일 적용
10. k=0.45의 PlayMode 실측 -> 최종 고정

---

## 10. 3단계 시작 가능 여부

**조건부 불가.** §7의 심층 수렴 문제가 열려 있다.

전투·연결 작업 자체는 §9의 목록대로 시작할 수 있고 §7과 독립이다. 다만 **B-1~B-4 중
하나가 정해지기 전에는 밸런스가 확정되지 않으므로**, 지금 전투를 붙이면 붙이자마자
보스 체력 곡선을 다시 만져야 한다. 그 순서가 9단계의 교훈("계수를 먼저 지어내고 화면에서
맞추면 어느 쪽이 맞는지 모른다")을 그대로 반복한다.

권하는 순서:

```
1. B-1~B-4 승인            -> 심층 수렴 복구, M 표 재유도(45초 규칙은 그대로)
2. 3단계 전투·연결          -> §9의 열 항목을 원자적으로
3. Android 실기 테스트      -> 아래
```

---

## 11. 실기 테스트 계획 (3단계)

2단계에서는 휴대폰 빌드를 강제하지 않는다. 3단계에서 전투·진행 연결·`CurrentVersion=21`이
완료되면 연결된 Android 기기로 반드시 수행한다.

### 안전 조건

- 운영 계정·실사용 세이브 사용 금지 — 개발용 패키지 또는 테스트 계정
- Firebase 운영 프로젝트 동기화 비활성화
- 테스트 전 **v20 세이브 백업** (테스트 패널의 v21 미리보기로 변환 결과를 먼저 확인)
- v21 세이브를 운영 클라우드에 업로드하지 않는다

### 항목

1. st29 -> 30 -> 일문 -> st31 진행
2. st40 / 50 / 70 / 100 / 150 경계
3. 승리 시 티어·배수·이름·외형·오라 동시 반영
4. 실패 시 재화·스테이지·세이브 손실 없음
5. 무료·무제한 재도전
6. 격노 90초와 180초 강제 종료
7. 하한·중간·강한 빌드의 실제 클리어 시간 (EditMode 예측: 45.0 / 39.5~43.9 / 34.7~42.8초)
8. 소프트캡 적용 전후 체감 — **k 최종 고정의 근거**
9. 모든 스킬·동료·지속 피해가 공통 `damageScale`을 통과하는지
10. 앱 백그라운드·강제 종료·재실행
11. v20 -> v21 마이그레이션과 멱등성
12. 터치·문구 가독성·프레임·발열·VFX
13. logcat 오류·예외·MissingReference

결과는 기기명·해상도·FPS·클리어 시간·영상 또는 스크린샷·로그와 함께 보고한다.
**실기 테스트 완료 전에는 귀문을 출시 가능으로 판정하지 않는다.**
