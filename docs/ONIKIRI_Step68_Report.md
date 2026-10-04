# ONIKIRI 68단계 보고서 — 천장 제거, 소환 레벨 도입

- **브랜치** `feature/step68-summon-level` (develop 9e31f5a에서 분기)
- **상태** 적용 완료. §5.1 세 계약은 **사용자 결정(선택지 1)으로 재기준**했다. 커밋 1회, push 승인 대기
- **테스트** EditMode **1060 / 1060** · PlayMode **63 / 63** · Emulator **68 / 68**, Unity MCP(§6)
- **배포** `firestore.rules` 상한 22 → 23 **배포 완료**, 서버 규칙 되읽기 = 로컬과 동일(§6.4)
- **건드리지 않은 것** 비용(25 / 10연 225)·횟수·해금 스테이지·일일 무료·온보딩 10연과 그 보장·Lv.1 확률표·막힘 캐스케이드(`Downgraded`)·Cloud/ 코드·실사용 세이브

## 0. 한 줄 요약

뽑기의 천장(요도 30회 ★4+ · 오의 30회 ★4 / 100회 ★5)을 전부 걷어내고, 배너별 **소환 레벨**(1회 = 1 XP, 요구 XP `ceil(20 x 1.15^(L-1))`, 등급 가중치 `C_i x g^(L-1)` 정규화)을 넣었다. (a) 반씩 나눈 무과금이 28일차에 옛 천장의 ★4+ 리듬을 따라잡고, (b) st500 추종의 ★5는 Lv.1의 x4.87, (c) 초반 레벨은 20 / 23 / 27회다. 다만 **천장 제거 자체가** 추종 플레이어의 심층 전설을 두 배로 만들어 붕괴 새니티(150)를 165.7로 넘긴다 - 소환 레벨 상수가 얹는 몫은 0이다(§5.1).

## 1. 변경 파일

| 파일 | 변경 |
|---|---|
| `Scripts/Balance/Curves/SummonLevelCurve.cs` | **신규.** `XpBase 20 · XpGrowth 1.15 · GradeGrowth {1,1,1.35,1.35,1.36}`, `XpToNext`·`LevelFor`·`XpIntoLevel`·`ChancesAt`(로그 공간 정규화)·`ChanceAt`·`LevelText(For)`·`LevelUpText` |
| `Scripts/Balance/Curves/GachaCurve.cs` | 천장 API 전부 삭제(`PityText`·`PityPulls`·`PullsUntilPity`·`PityShare`·`Effective*`·`SuppressedChance`). `Roll(value, level)`, `EssenceChanceAt`·`RarityChanceAt`·`LegendaryChanceAt`·`EpicOrBetterChanceAt`·`ExpectedPullsPer*(level)`·`ExpectedShardsPerPull(level)` |
| `Scripts/Balance/Curves/SkillGachaCurve.cs` | `AwakenPityPulls`·`PullsUntilAwakenPity`·`PityText`·`PityPulls`·`PullsUntilPity`·`Suppressed`·`SteadyRates` 삭제. `Roll(value, level)`, `ExpectedXpPerPull(level)`·`EffectiveUnlockChance(level)`·`EffectiveAwakenChance(level)`·`ExpectedPullsPerUnlock/Awaken(level)` |
| `Scripts/Balance/Formulas/SkillGachaPityModel.cs` | **삭제**(+ `Tests/EditMode/SkillGachaPityModelTests.cs`) |
| `Scripts/Subsystems/GachaSystem.cs` · `SkillGachaSystem.cs` | 천장 카운터 → `summonXp`(long)·파생 `SummonLevel`·`LastBatchLevelUp`, `SummonLevelUp(int)` 이벤트(오른 레벨마다 한 번, `Pulled`보다 먼저). 회차마다 그 순간의 레벨로 굴리고 1 XP. `FromPity` 삭제. `Restore`는 음수만 막는다 |
| `Scripts/SaveGame/SaveData.cs` | **v23**. `gachaPity`·`skillGachaPity`·`skillGachaAwakenPity` 삭제, `yodoSummonXp`·`skillSummonXp` 추가. v22 → v23 = 누적 뽑기 수를 경험치로(멱등) |
| `Scripts/Subsystems/GameSession.cs` | 복원·수집 배선 |
| `Scripts/Balance/Simulation/StageSimulation.cs` | 요도·오의 뽑기가 회차의 소환 레벨 표를 쓴다. 50단계 정적 천장 분포 삭제. **`Policy.GachaPre68`**(49단계 규칙 ⓐ - 47단계 천장의 실효 표 재현) |
| `Scripts/Widget/Panels/ShopPanel.cs` | `pityLabel`·`skillPityLabel` → `summonLevelLabel`·`skillSummonLevelLabel`, 문구 `SummonLevelCurve.LevelTextFor`, 다음 레벨이 10연 안이면 금색 |
| `Scripts/Widget/Popups/GachaResultPopup.cs` | "천장!" 꼬리표 삭제, `Show(…, summonLevelUp)` → 제목 줄 끝 "소환 Lv.n 달성"(`WithLevelUp`) |
| `Editor/ShopPanelBuilder.cs` | 줄 이름 `SummonLevel`, 초기 문구·연결 필드명, 폭 검사(소환 줄 · 레벨업 붙은 제목 줄) |
| `Editor/OnikiriTestPanel.cs` | **신규 절 "소환 레벨"**(두 배너의 지금 레벨·경험치·★4+/★5 대기, 식, 요구 XP Lv.1~10, Lv.1/5/10/20/50 표, 레벨업 직전 치트), 두 뽑기 절의 천장 줄·버튼 교체 |
| `DevTools/TrialPresetForge.cs` | 생략 칸 설명 문구 |
| `Scenes/Main.unity` | 직렬화 키 두 개 이름 변경(같은 fileID), 오브젝트 `Pity` → `SummonLevel` 두 개, 초기 문구, 두 뽑기 시스템의 천장 칸 → `summonXp`. 저장할 때 에디터가 다시 쓴 배치 값 셋(`SafeArea` 앵커, `Background`·`GroundAnchor` 위치)은 **되돌렸다**(결정 3 - 런타임이 다시 쓰는 값은 diff에 넣지 않는다) |
| `Data/UIStrings.txt` · `FontCharset.txt` · 폰트 `.asset` 셋 | "환"이 아틀라스에 없었다 - 문구 등재 후 차셋(472자)·44·33 아틀라스 재굽기. 두 아틀라스에 U+D658 확인 |
| `firestore.rules` | 상한 한 줄 `<= 22` → `<= 23` |
| `tools/firestore-rules-tests/rules.spec.mjs` | v23 수용 · v22 수용(신규) · v24 거부 |
| 테스트 | §6.3 |

## 2. 소환 레벨 식과 상수 근거

    XpToNext(L) = ceil(20 x 1.15^(L-1))            최대 레벨 없음
    w_i(L)      = Chances_i x g_grade(i)^(L-1),  확률 = w_i / Σw
    g           = ★1 1 · ★2 1 · ★3 1.35 · ★4 1.35 · ★5 1.36

| | 1차(승인 출발값) | 최종 | 왜 바꿨나 |
|---|---|---|---|
| Base / Growth | 20 / 1.15 | 20 / 1.15 | (c) 20·23·27회 그대로 |
| g★3 | 1.03 | **1.35** | 추종의 뽑기는 재고가 멈춘다 - ★3·★4가 같은 비율로 커야 재고가 같은 비율로 일찍 닫혀 그 사이에 얹히는 ★5 기대가 늘지 않는다 |
| g★4 | 1.20 → 1.35 | **1.35** | (a) 반씩 배분 30일. 1.20이면 40일(1차 추천의 결과) |
| g★5 | 1.25 | **1.36** | **★5가 가장 커야 단조다.** g★4 > g★5면 고레벨에서 ★4가 ★5를 잡아먹는다(1.50/1.25: Lv.10 3.2% → Lv.50 0.01%) |

★5 → 1 수렴은 성립하지만 g★5가 g★3·g★4보다 0.01 위라 **아주 느리다**(Lv.50 ★5 18%, Lv.5000에서 99% 위). "전설이 흔해지지 않게"(b)와 새니티(§5.1)가 이 간격을 좁게 묶었다.

## 3. 레벨별 확률표

| Lv | ★1 | ★2 | ★3 | ★4 | ★5 | ★4+ 하나에 | ★5 하나에 | 누적 뽑기 |
|---|---|---|---|---|---|---|---|---|
| 1 | 71.6% | 22.5% | 3.00% | 2.10% | 0.80% | 34.5회 | 125회 | 0 |
| 5 | 62.9% | 19.8% | 8.76% | 6.13% | 2.41% | 11.7회 | 42회 | 101 |
| 10 | 39.2% | 12.3% | 24.44% | 17.11% | 6.97% | 4.2회 | 14회 | 340 |
| 20 | 3.8% | 1.2% | 47.36% | 33.15% | 14.53% | 2.1회 | 7회 | 1,775 |
| 50 | 0.0% | 0.0% | 48.01% | 33.61% | 18.38% | 1.9회 | 5회 | — |

참고: Lv.3(43회)에서 ★4+ 하나에 19.8회로 옛 천장(20.2회)을 지난다.

## 4. 천장 세계 대비 전후

### 4.1 기대값

| | 천장(47·50단계) | 소환 레벨 |
|---|---|---|
| 요도 ★4+ 하나에 | 20.2회(첫 뽑기부터) | Lv.1 34.5 → Lv.3 19.8 → Lv.5 11.7 |
| 요도 ★5 | 0.8%(천장이 안 건드림) | Lv.1 0.8% → Lv.7 3.9% |
| 오의 ★5 간격 | 장기 평균 69.0회(하드 천장 100) | Lv.1 125 → Lv.5 42 → Lv.7 26 → Lv.10 14 |
| 귀오의 넷 기대 | 276회 | **191회** |
| 개발 세이브 시작 레벨 | — | 요도 101회 → **Lv.5** (0/35) · 오의 40회 → **Lv.2** (20/23) |

### 4.2 오의 해금 시점 (뽑기 횟수 · 장부 일차, 반씩 배분)

일차는 오의 배너 뽑기 = 온보딩 10 + 일일 무료(하루 1) + 장부 10연의 절반으로 셌다.

| | S1 | S2 | S3 | S4 | S5 | O1 | O2 | O3 | O4 |
|---|---|---|---|---|---|---|---|---|---|
| 천장 (회 / 일) | 30 / 17 | 59 / 29 | 81 / 41 | 107 / 52 | 133 / 63 | 100 / 50 | 165 / 78 | 230 / 107 | 300 / 140 |
| 소환 레벨 (회 / 일) | 41 / 21 | 69 / 34 | 91 / 46 | 109 / 54 | **126 / 61** | **82 / 41** | **128 / 62** | **162 / 77** | **191 / 91** |

표준 앞쪽 셋은 4~5일 늦어지고, S5와 귀오의 넷은 빨라진다(O4 140일 → 91일). S1은 두 세계 모두 온보딩 10연의 보장이 0일차에 앞당긴다.

### 4.3 47단계 계약 재측정 (시뮬레이션, 기본 정책 = 추종)

| | 천장 (`GachaPre68`) | 소환 레벨 |
|---|---|---|
| 요도 뽑기 st100 / st200 / st500 | 121 / 121 / 314회 | 109 / 109 / 210회 |
| 뽑기 보석 st500 | 9,590 | 6,990 |
| `SkipGacha` 죽은 버튼 (st51~400 시간) | +17.89% | **+20.75%** |
| 같은 시간 도달층 (st400 기준) | st336 (−64) | st328 (−72) |
| 사다리 보석당 가치 (평탄 대비) | x1.84 | **x1.45** (검사 하한 x1.3) |
| 전설 사본 st200 / st500 | 0·0 / 1·1 | 1·0 / **2·2** |
| 심층 여유 st100 / st200 / st500 | 14.51 / 16.92 / 66.56 | 16.72 / 19.58 / 79.41 |

`SkipGacha`의 값이 지시서의 +10.48% / −37층과 다른 것은 자의 차이다(이 하네스는 `GachaTests` 필드 · st51~400). 같은 자로 잰 전후가 위 표다. 보석당 가치가 내려간 것은 뽑기 수가 줄었기 때문이다 - 같은 재고를 더 싸게 채운다.

## 5. 밴드

### 5.1 천장 제거 자체가 깬 세 계약 — **재기준했다**(사용자 결정, 선택지 1)

| 검사 | 옛 계약 | 새 계약 |
|---|---|---|
| `DeepZone_MarginStaysSane` (`MarginSanityCap`) | 150 | **180** - 주석: "93(52) → 139(67) → 166(68, 천장 제거로 전설 사본 두 배). 다음에 180을 넘으면 재기준이 아니라 전설 정지 규칙(사본 상한·넘침)을 본다." |
| `Affinity_IsNotADeadButton` 지금 세계 하한 | 3% | **2.5%** (45단계 세계 4%는 그대로) |
| `AfterShopping_…Formal` 쇼핑 후 추종 최속 | 27~30초 | **26~30초** (하드 계약 25초 바닥은 그대로) |

아래는 결정 전의 기록이다.

| 검사 | 계약 | 천장 세계 | 지금 |
|---|---|---|---|
| `DeepZone_MarginStaysSane` | 붕괴 새니티 < 150 | 138.9 @st461 | **165.7 @st461** (EditMode 검사는 st431 164.8) |
| `Affinity_IsNotADeadButton` (지금 세계 하한) | 상성 이득 > 3% | 통과 | **2.76%** (45단계 세계 4% 하한은 통과) |
| `AfterShopping_…Formal` (크기) | 쇼핑 후 추종 최속 27~30초 | 28.0초 | **26.5초** (하드 계약 25초 바닥은 통과) |

**원인은 하나다.** 추종 플레이어의 요도 뽑기는 혼격(★4, 12칸) 재고가 닫힐 때 멈춘다. 천장은 ★4 실효를 4.145%로 부풀려 혼격 하나를 채우는 동안 전설 기대가 0.19였고, 표 그대로(2.1%)면 0.38이다. 그래서 st500까지 전설 사본이 1·1 → 2·2로 늘고, 요도 배수가 x1.145, 새니티가 x1.19가 된다. 요도 티어·혼격은 두 세계에서 스테이지마다 똑같다.

**레벨 상수가 얹는 몫은 0이다.** g를 전부 1로 둔 세계(레벨 효과 없음, 572회)도 새니티 165.7이고, 최종 상수도 165.7이다. 단조(★5 ≥ ★4)를 지키는 어떤 g도 이 값 아래로 못 내린다 - 실측 조합 16개, 165.7~197.8.

선택지(구현 전 보고와 같다):

1. **(추천) 세 계약을 천장 없는 세계로 재기준한다** - 새니티 150 → 약 180, 상성 지금 세계 하한 3% → 2.5%, 쇼핑 후 크기 27~30 → 26~30.
2. 새니티 150을 지킨다 - 전설 재고(사본 상한·넘침)나 시뮬의 전설 정지 규칙을 손대야 한다(범위 밖).
3. g★4 > g★5 - 수치는 통과하지만 레벨이 오를수록 전설이 준다(비추천).

### 5.2 통과

| 항목 | 천장 세계 | 지금 |
|---|---|---|
| (a) 반씩 배분 무과금이 옛 ★4+ 실효에 닿는 날 | — | **28일차 (Lv.3)** · 전부 요도면 23일차 |
| (b) st500 추종의 ★5 / Lv.1 | — | **x4.87** (Lv.7, 210회) |
| (c) 요구 XP Lv.1~3 / Lv.15 | — | 20 / 23 / 27 · 142 |
| 코리더 바닥 / 천장 · 가속 바닥 / 천장 | +0.04 / +0.06 · +0.84 / +0.03 | **비트 동일**(뽑기는 st41부터, 기본 정책 코리더 밖) |
| 피날레 < 챕터 (간격) | 0.046 | 동일 |
| 온보딩 st1~5 · 무강화 게이트 st2 | 171.4초 · st2 | 동일 |
| 귀문 통과 시점 · M표 | T1@31…T6@151 | 동일(하한 플레이어는 뽑지 않는다) |
| f2p 바닥 · 무과금 경로 | — | 비트 동일(무과금은 시뮬에서 보석 뽑기를 안 한다) |
| 수렴 비 (< 0.35) | 0.166 / 0.115 | 0.171 / 0.115 |
| 리드 H200 / H500 (≤100 / ≤240) | 33 / 98 | 37 / 107 |
| f2p 도달 바닥 (≥ st260) | st402 | st393 |
| 꼬리 몫 0.68 · 촉매 계약 · 사다리 죽은 버튼 | 통과 | 통과 |

재현 앵커 셋(`HitRatingNeutralized_ReproducesTheStep64World` st100 14.37 → 16.55, `MasteryNeutralized_…` st151, 66 전 세계 명중 4%)은 49단계 규칙 ⓐ대로 `GachaPre68` 한 줄을 정책에 더해 통과시켰다. 이 비교군은 옛 런타임(HEAD 소스로 따로 컴파일)과 심층 표가 표시 정밀도까지 같다.

## 6. 테스트 수치

### 6.1 EditMode — 1060 / 1060 (MCP, 333초)

결정 반영 뒤 전량 1회 통과(1059 + 신규 `Shards_CollapseOnlyAfterTheStockCloses_AndTheCatalystAlwaysWins`).
첫 시도는 에디터가 MCP 재연결 중 메인 스레드에서 7분 멈춰(CPU 거의 0) 사용자가 에디터를 재시작했고, 재시작 뒤 정상으로 돌았다.

**신규 검사 - 파편과 촉매(결정 2).** 파편 기대는 Lv.1 11.30 → Lv.10 6.18 → Lv.20 0.60 → Lv.50 0으로 떨어진다. 추종은 st1~1000에서 마지막 뽑기가 **st640 · 246회 · Lv.8**이고 거기서 파편이 Lv.1의 **70%**다 - 무너지는 레벨(Lv.20 = 1775회)은 재고가 닫힌 일곱 배 뒤다(지시서의 "재고가 닫힌 뒤" 쪽). 촉매(보석당 0.667)는 모든 레벨에서 뽑기(최대 0.452)를 이긴다. 검사는 재고가 닫히는 레벨의 파편 ≥ Lv.1의 50%와 촉매 우위를 못 박는다.

결정 전 기록:

- 전체 1회(1059건) → 실패 4 = 결정 대기 3 + `CloudSaveTests.CrossSaveDoesNotTouchTheSaveFormat`(버전 숫자 22). 숫자를 고친 뒤 `CloudSaveTests`·`SummonLevelTests`·`GachaTests` 픽스처 재실행 **137 / 137**.
- 에디터 밖 하네스(사전 점검): 695건 중 실패 3(같은 셋), 에디터 전용 364건.

### 6.2 PlayMode — 63 / 63 (MCP, 104초)

기존 60 + **`SummonLevelPlayTests` 3**: 10연 → 요도 XP +10 · 보석 −225 · 오의 배너 무변(배너별), 레벨업 1회 → 이벤트 1건(`[2]`), 결과 팝업 제목 끝 "소환 Lv.2 달성" · "천장" 없음 · 안 오른 묶음엔 꼬리 없음. `SaveSandbox` 사용.

### 6.3 테스트 변경

- **신규 `SummonLevelTests` 15**: 합 1(Lv.1~100000), Lv.1 = 공개 표, 단조(Lv.2~400 ★5↑ ★1↓, Lv.5000 ★5 > 99%), 상한 없음(리플렉션 + 10억 XP > Lv.100), XpToNext 정수·단조·식, LevelFor/XpIntoLevel, 문구, v22→v23(101 → Lv.5 · 40 → Lv.2), 멱등·v24 거부, JSON 왕복(옛 천장 칸 무시), **천장 식별자 0**(Scripts·Editor·DevTools, 주석·문자열 제외), 밴드 (a)(b)(c), Lv.3 교차
- `GachaTests`: 천장 검사 6개 삭제·대체(`EpicOrBetter_AtLevelOneIsTheTable`, `Epic_IsAlwaysWorthAtLeastARare` - 캐스케이드 유지), 런타임 넷 신규(`PullsEarnSummonXp`·`LevelUpRaisesOneEvent`·`SaveRoundTripKeepsTheSummonXp`·`RestoreRejectsOnlyNegatives`)
- `SkillGachaTests`: 하드 천장 검사 → `TheFifthStar_ComesCloserWithSummonLevel`(귀오의 넷 < 276회), 일일 무료·XP 곡선은 레벨을 따라 걷도록, 배너 소진 하한은 같은 기대 누적으로
- `SkillGachaV20Tests`: 이중 천장 두 검사 삭제, v19 누적 213 → 소환 XP 213
- `SaveMigrationTests`·`CloudSaveTests`·`SlideChainTests`: 천장 칸 → 소환 경험치. 버전 숫자 22 → 23 여섯 곳

### 6.4 Emulator · 배포

- `tools/firestore-rules-tests` `npm test` **68 passing**(6초, Unity 동봉 OpenJDK). 신규 "옛 빌드의 saveVersion(22)도 계속 받는다", 미래 거부 24.
- `npx firebase-tools deploy --only firestore:rules` → `onikiri-9cc18` released.
- 되읽기: Firebase MCP가 이 세션에서 연결되지 않아 **Rules REST API**(`releases/cloud.firestore` → ruleset 소스)로 받았다. 릴리스 `418270ba…`(2026-10-04T02:37:10Z), **로컬 `firestore.rules`와 동일**, 상한 줄 `<= 23`.
- `CloudSaveEnvelope` 요약 6필드는 누적 뽑기 수(`gachaTotalPulls`·`skillGachaTotalPulls`)를 읽고, 그 칸은 그대로라 영향 없음.

## 7. 남은 것

1. **푸시·병합** — 커밋 1회 완료, push 승인 뒤 develop `--no-ff` 병합 · push.
2. **로드맵 이관** — §4.2(오의 해금 이동)·§5.1 재기준·§3 확률표(사용자가 한다).
3. **69단계(UI 개편)** — 소환 게이지 바, 레벨업 연출(`SummonLevelUp` 이벤트가 이미 선다).
4. **기존 폭 경고 하나** — 빌더 폭 검사에서 "희귀 스킬 XP 240"(확률표 이름)이 270px / 칸 254px. 이번 변경 전부터 있던 것으로 보인다(문구 무수정).
5. **실사용 세이브** — 작업 중 사용자가 이 빌드로 플레이해 세이브가 **v23**으로 올랐다(요도 101 → 122회). 이 세이브는 develop 병합 전 빌드(v22)로는 열리지 않는다. 작업 전 v22 사본은 세션 scratchpad에 있다.
6. **firestore.rules 주석** — "상한 한 줄만" 지시대로 위 설명 주석(“= 22 (66단계…)”)은 안 고치고 같은 줄 끝에 68단계 주석을 달았다.
7. `docs/media/ref_slayer_gacha_*.png` — 커밋 금지(레퍼런스 로컬 참고용).
