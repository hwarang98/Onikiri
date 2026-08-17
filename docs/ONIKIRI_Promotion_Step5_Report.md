# 승급·전직 5.0단계 — 밸런스 최종 확정 **완료 보고서**

```
상태         **Step5 기능 구현 완료** (코드 폐쇄)
소프트캡 k    0.45 **확정 유지**            (0.35 채택 안 함 · 0.60 기각)
시계 기준     Time.deltaTime = **scaled 확정**  (unscaled 기각)
폐쇄 시간     180 -> **150 게임초 적용 완료**

EditMode     763 / 763 통과 · 212초 · skipped 0   (이 단계 신규 24)
PlayMode      32 /  32 통과 ·  84초 · skipped 0   (이 단계 신규 2 = 프리셋 동등성)
컴파일        오류 0 · 경고 0
사용자 세이브  82d7b335632d4ed961a3b8dcda67e217351a1694d78f99369337ff11e4e66a15  전후 동일
빌드          개발 APK **성공** 83.4MB / 184초 · 운영 bundleId 복구 확인 ·
              Resolver 상태 HEAD와 동일(18항목) · DevTools는 빌드 산출물에 없음
Android 실기   **출시 전 실기 QA 대기** — 기기가 USB에서 빠졌다 (§5.4)
```

절대 변경 금지 항목은 손대지 않았다: 심층 지수 0.55, 문별 M 표와 체력표, 격노 90초 /
10초 / x1.3, SaveData v21과 마이그레이션, 무료 승급 구조, Firebase 백엔드,
일반 보스·스테이지 밸런스, 씬·프리팹.

---

## 0. 근거 문서와 충돌 우선순위

| 문서 | 역할 |
|---|---|
| `ONIKIRI_Promotion_Step2_Report.md` | M 표 재유도(지수 0.42 -> 0.00) |
| `ONIKIRI_Promotion_Step2_1_Report.md` | 심층 수렴 보정 B-1, 육문 M 재굽기 |
| `ONIKIRI_Promotion_Step2_1_1_Report.md` | 심층 지수 하한 탐색(0.55 유지) |
| `ONIKIRI_Promotion_Step3_Report.md` | 전투·진행·Save v21 연결, Android 실기 1차 |
| `ONIKIRI_Promotion_Step4_UIUX_Report.md` | UI/UX·실기 결함 폐쇄, 시간 정책 미결정 명시 |
| `ONIKIRI_Promotion_Evolution_Balance_Redesign.md` | 설계 원본 (의도의 근거) |

```
현재 코드·자동 테스트
  -> Step4 · Step3 실제 결과
  -> Step2.1.1 -> Step2.1 -> Step2
  -> 설계 원본
```

설계 원본은 의도의 근거이고, 후속 실측이 확정한 값을 과거 값으로 되돌리는 근거로
쓰지 않는다.

### 밴드는 둘이고, **하드 계약이 서 있는 프레임은 하나다**

| | 값 | 어디서 재는가 |
|---|---|---|
| **하드 계약** | 하한 `[35,55]` · 중간 `[34.5,55]` · 추종 `>=25` · 격노 여유 `>=2` · 0.70x 통과 / 0.35x 실패 | **게이트 도착 즉시 도전 프레임** |
| **선호 목표** | `35~45`초 (Step3 §9.6) | 같은 프레임. 하드를 통과한 후보를 줄 세우는 자 |

이 구분이 5.0단계에서 한 번 잘못 적혔다가 정정됐다 — §3.4가 그 내용이다.

---

## 1. 기존 계약 (읽은 그대로)

### 1.1 세 프로필 — 둘은 실재하고 하나는 계산 대리값이다

| 프로필 | 정의 | 정체 |
|---|---|---|
| **Floor** | `Policy { GemsFromQuestsOnly = true }` | 실재 빌드. 실제 SaveData로 변환 · PlayMode·Android에서 사용 |
| **CurveFollower** | `Policy.Default` | 실재 빌드. 실제 SaveData로 변환 · PlayMode·Android에서 사용 |
| **Middle** | `PromotionTrialTests.MidAt()` — `sqrt(Floor x Curve)` DPS, 체력·재생은 Floor 값 | **계산 대리값.** PlayMode 수학 검증에만 쓴다. 실제 플레이어도, 실제 세이브도, Android 프리셋도 아니다 |

Middle의 체력·재생이 Floor 값이므로 **생존 측면에서 보수적인 대리값**이다(화력은 높고
체력은 같다). 새 중간 Policy를 만들지 않았고, 공격력만 역산한 합성 세이브도 만들지 않았다.

M 표의 앵커는 하한 플레이어 하나다.

```
M(gate) = (45초 - 전환 2회 x 2초) / 하한 플레이어의 그 게이트 보스 처치 시간
        = TrialPowerScore.ReferenceSeconds(41초) / 그 시간
```

### 1.2 문별 목표 시간 (k=0.45 · 도착 즉시 프레임)

Step2.1 §6.2 = Step2.1.1 §1의 값을 5.0단계에서 **재실행해 전부 재현했다.**

| 문 | st | M | 하한 | 중간 | 추종 | 0.70x | 0.50x | 0.35x |
|---:|---:|---|---|---|---|---|---|---|
| 1 | 30 | 1.7376 | 45.0 | 43.9 | 42.8 | 62.6 | **사망 85.0** | 사망 92.6 |
| 2 | 40 | 1.9528 | 45.0 | 43.7 | 42.3 | 62.6 | 86.0 | 사망 98.6 |
| 3 | 50 | 2.1813 | 45.0 | 43.2 | 41.5 | 62.6 | 86.1 | 사망 96.6 |
| 4 | 70 | 3.1164 | 45.0 | 41.2 | 37.8 | 62.6 | 86.0 | 사망 94.6 |
| 5 | 100 | 8.6655 | 45.0 | 39.5 | 34.7 | 62.6 | 86.0 | 사망 94.6 |
| 6 | 150 | 10.4095 | 45.0 | 39.6 | 34.9 | 62.6 | 86.0 | 사망 94.6 |

정지 시도: 96~132초 사망. 격노 여유 최소 2.58배(90 / 34.9).

---

## 2. 실제 프리셋 생성 (§2) — 완료

### 2.1 만든 것

| 파일 | 어셈블리 | 역할 |
|---|---|---|
| `Assets/_Project/DevTools/Onikiri.DevTools.asmdef` | 신규 · **`includePlatforms: ["Editor"]`** | 에디터 전용. 플레이어 빌드에 안 들어간다 |
| `Assets/_Project/DevTools/TrialPresetForge.cs` | Onikiri.DevTools | `StageResult` -> `SaveData` · 감사 · 두 시점 측정 · 기대값 표 |
| `Assets/_Project/DevTools/DevSimField.cs` | Onikiri.DevTools | 에셋에서 `Field`를 만드는 **단일 출처** |
| `Assets/_Project/Editor/TrialFieldTestBuilder.cs` | Assembly-CSharp-Editor | 메뉴 `귀문 밴드 프리셋 생성` · 패키지명 복구 강화 |

`DevSimField`를 올린 이유는 단일 출처다. 에셋 읽기가 두 벌이 되면 프리셋이 밴드와
**다른 잡몹 평균**에서 유도되고 그 어긋남은 세이브 어디에도 안 적힌다.
`PromotionTrialFixture.FieldFromAssets`는 이제 그것을 부르기만 한다(값 무변경).

### 2.2 반영한 필드 — 문 여섯 x 프로필 둘 = 12벌, 감사 불일치 0

```
진행      stage · maxStageReached · killsThisStage(=10) · bossKillCount(=stage)
경지      evolutionTier = gate - 1
캐릭터    characterLevel · attackPoints · healthPoints
강화 9축  공격·공속·치명타확률·치명타피해·체력·재생·골드획득·초월·연격
장비      무기/방어구 등급 + 단련
오의      15종 레벨 · 장착 4칸 · 뽑기 보유 · skillXp
동료      3종 보유 + 레벨
요도      4자루 티어 · 혼격 · 발견 · 혼 · 파편 · 전설 사본 2종
재화      보석 잔액
```

문 대기 조건은 `PromotionTrialCatalog.PendingGate`를 **실제로 불러** 확인한다.

모델링하지 않은 것(도구가 스스로 적는다): 골드·경험치 0, 퀘스트 진행 빈 상태,
요도 혼 잔량은 첫 자루에 합계, 뽑기 천장 0, 온보딩 10연은 받은 것으로 처리,
`playerName` 빈 문자열, 방치 보상 0.

### 2.3 **발견 — `StageResult`는 두 시점을 섞어 담는다**

`StageSimulation.Run`의 순서:

```
잡몹 10마리 + 구매
var stats = levels.Stats;      <- ExpectedDps · Damage · CritRate · SkillRate · SpiritRate
보스 골드 · 클리어 보너스 · 업적 · 요도 · 오의 뽑기
Buy(stage + 1, ...)            <- **여기서 또 산다**
results.Add(... levels.*)      <- 강화 레벨 · 장비 · 티어 · 체력 · 재생 · 동료 보너스
```

밴드는 구매 **전** 화력(`stats`)에 앵커돼 있고, 세이브는 구매 **뒤** 레벨을 담는다.
그리고 구매 뒤가 게임에서 실제로 일어나는 일이다 — `BossFight.OnBossKilled`은 게이트
스테이지에서도 보상을 평소대로 정확히 한 번 주고(멈추는 것은 진행뿐이다), 강화 화면은
입장 전까지 열려 있다.

| 문 | 프로필 | 도착 즉시 P | 쇼핑 후 P | 비 |
|---:|---|---:|---:|---:|
| 1 | Floor / Curve | 1.0000 / 1.1355 | 1.8304 / 1.9505 | x1.83 / x1.72 |
| 2 | Floor / Curve | 1.0000 / 1.1662 | 1.7000 / 1.9921 | x1.70 / x1.71 |
| 3 | Floor / Curve | 1.0000 / 1.2233 | 1.8747 / 2.3091 | x1.87 / x1.89 |
| 4 | Floor / Curve | 1.0000 / 1.5415 | 1.8130 / 2.7947 | x1.81 / x1.81 |
| 5 | Floor / Curve | 1.0000 / 1.9074 | 1.7270 / 3.2941 | x1.73 / x1.73 |
| 6 | Floor / Curve | 1.0000 / 1.8881 | 1.7121 / 3.2327 | x1.71 / x1.71 |

여섯 문 전부에서 `postDps(N-1) <= ExpectedDps(N) <= postDps(N)`이 성립한다 — 끝 구매
하나가 거의 한 스테이지 몫이다(한 스테이지 성장이 x1.72~1.80).

### 2.4 안전

- 출력은 `Builds/Step5/presets/` — 프로젝트 안이다. 도구가 쓰기 직전에
  `SaveSystem.Path`와 대조하고 같으면 중단한다
- 에디터의 `SaveSystem.Path`는 실사용 세이브를 가리키므로 **그 경로를 아예 쓰지 않는다**
- 프리셋 메뉴는 Editor 어셈블리에만 있어 Release 빌드에 존재하지 않는다

---

## 3. k 전체 비교 (§3) — **0.45 확정 유지**

### 3.1 폐쇄식이 실기와 코드를 잇는다

```
클리어(게임초) = 4 + 41 / P^k          P = 원시 화력 / 문 기준 화력
                (4 = 전환 2회 x 2초)
```

54칸 전부에서 시뮬레이션과 **0.1초 이내** 일치한다. Step3 실기가 역산으로 확인한
같은 식이다(그쪽은 입장 연출 2초 포함 `6 + 41/P^k`).

### 3.2 도착 즉시 프레임 — 전체 행렬

| k | 문 | scale(중간/추종) | 하한 | 중간 | 추종 |
|---:|---:|---|---:|---:|---:|
| 0.35 | 1~6 | .96/.92 → .81/.66 | 45.0 | 44.1 → 40.7 | 43.3 → 36.9 |
| **0.45** | 1 | .9657 / .9325 | **45.0** | **43.9** | **42.8** |
| **0.45** | 2 | .9586 / .9189 | **45.0** | **43.7** | **42.3** |
| **0.45** | 3 | .9461 / .8951 | **45.0** | **43.2** | **41.5** |
| **0.45** | 4 | .8878 / .7882 | **45.0** | **41.2** | **37.8** |
| **0.45** | 5 | .8373 / .7011 | **45.0** | **39.5** | **34.7** |
| **0.45** | 6 | .8396 / .7050 | **45.0** | **39.6** | **34.9** |
| 0.60 | 1~6 | .97/.95 → .88/.78 | 45.0 | 43.5 → 37.9 | 42.0 → 31.8 |

1·2·3체별 시간은 총 시간을 `10.3 / 22.5 / 45.0`(하한) 비율로 나눈다 — 체력 배분
0.25/0.25/0.50과 전환 2초가 그대로 보인다. **격노 진입은 한 칸도 없다**(전부 45초 이하).
종료 체력은 전 칸 양수.

**하드 계약: 세 후보 다 통과.** 출시값 0.45가 그 안에 서 있다.

### 3.3 폭 계약과 정지 시도는 k와 무관하다

| 케이스 | 문1 | 문2~6 |
|---|---|---|
| 0.70x | 통과 62.6초 | 통과 62.6초 |
| 0.50x | **사망 85.0초** | 통과 86.0~86.1초 |
| 0.35x | 사망 92.6초 | 사망 94.6~98.6초 |
| 정지 시도 | 사망 132초 | 사망 96~116초 |

세 k에서 완전히 동일하다 — 이 케이스들은 P<1이라 소프트캡이 아예 걸리지 않는다.

### 3.4 쇼핑 후 프레임 — **정정된 판정 근거**

5.0단계 중간에 이 프레임의 단축을 "하드 계약 위반"으로 적었고, 그것을 **정정한다.**

- `[35,55]` 하한 계약은 **게이트 도착 즉시 도전 프레임** 기준이다. 45초 앵커의 뜻이
  "보상을 쓰기 전의 하한 플레이어도 통과한다"이므로 그 프레임에서 재는 것이 맞다.
- 쇼핑 후 Floor 34.9초를 **하드 계약 위반이라고 쓰지 않는다.**
- 쇼핑 후의 단축은 플레이어가 **보스 보상으로 성장한 결과**다. 시험이 빨라지는 것이
  성장의 뜻이고, 그것을 벌점으로 세면 성장을 벌주는 셈이 된다.
- 이전 초안의 **"0.35만 하드 계약을 지키는 유일한 후보"라는 문구는 전부 제거했다.**
  코드 쪽 검사 이름과 주석도 함께 고쳤다
  (`AfterShopping_TheShippedExponentKeepsTheTrialFromBecomingFormal`).

이 프레임에서 계약으로 남는 것은 **하나**다 — 25초 바닥. 그 아래로 내려가면 빨라진
것이 아니라 시험이 없어진 것이다(v1.3의 15.4초가 승인받지 못한 자리).

| k | 쇼핑 후 하한 (최속) | 쇼핑 후 추종 (최속) | 25초 바닥 | 판정 |
|---:|---|---|---|---|
| 0.35 | 36.9~38.1 | 31.0~36.5 | ○ | 통과하지만 **채택 안 함** — 성장 체감이 가장 적다 |
| **0.45** | 34.9~36.3 | **28.0**~34.4 | ○ | **확정** — 도착 즉시 계약을 지키면서 성장 체감을 보존한다 |
| 0.60 | 32.1~33.8 | **24.1** | ✗ | **기각** — 곡선을 따라온 플레이어에게 귀문이 형식이 된다 |

쇼핑 후 화력은 **하한**이다(§2.3의 `SkillRate`·`SpiritRate`가 구매 전 값). 실제 화력은
이보다 크고 시간은 더 짧다.

### 3.5 성장 체감 — 0.35를 채택하지 않은 이유

실효 화력이 `ref x P^k`이므로 화력을 두 배로 올리면 실효는 `2^k`배다.

| k | 화력 2배가 남기는 몫 | 선호 밴드 이탈 합(24칸) |
|---:|---:|---:|
| 0.35 | x1.27 | 10.6초 |
| **0.45** | **x1.37** | 24.1초 |
| 0.60 | x1.52 | 61.9초 |

두 자가 **반대 방향으로 당긴다.** 0.35는 선호 밴드에 가깝지만 강화가 귀문에서 가장
덜 실감되고, 0.60은 반대다. 0.45가 그 사이에 서 있고, 도착 즉시 프레임의 하드 계약을
지키면서 쇼핑 후에도 28.0초를 남긴다 — 그것이 유지 근거다.

### 3.6 단조성 (§3의 추가 검사 넷)

| 검사 | 결과 |
|---|---|
| P 증가 -> 적용 DPS 비감소 | 세 k · 여섯 문 · P 0.30~12.0 (39점) **전부 성립** |
| P 증가 -> 클리어 시간 비증가 | 같은 범위에서 **전부 성립** |
| k 증가 -> 과화력 성장이 더 남는다 | `2^k` = x1.27 / x1.37 / x1.52 |
| P <= 1 에 보정 없음 | 기준의 10·50·90·100%에서 배율이 정확히 1 |

두 끝점 사이 어디에 서든 역전이 없으므로 Floor·CurveFollower 둘만 실측해도 그 사이
플레이어를 말할 수 있다.

### 3.7 시험이 형식이 되는 지점

`4 + 41/P^k = 25`를 풀면:

| k | P@25초 | 추종 최대 P (즉시 / 쇼핑 후) | 25초까지 여유 |
|---:|---:|---|---:|
| 0.35 | 6.76 | 1.907 / 3.294 | x3.55 / x2.05 |
| **0.45** | **4.42** | 1.907 / 3.294 | x2.32 / **x1.34** |
| 0.60 | 3.05 | 1.907 / 3.294 | x1.60 / **x0.93** |

k=0.60에서는 쇼핑 후 추종이 이미 그 지점을 지나 있다(0.93 < 1). 그것이 기각 근거다.

---

## 4. 시간 정책 (§4) — **scaled 유지 · 폐쇄 150 적용 완료**

### 4.1 결과 계약은 180 / 150 / 120에서 완전히 동일하다

폐쇄 셋 × 여섯 문 × 일곱 케이스를 세 k에서 전부 돌렸다.

| 케이스 | 180 | 150 | 120 |
|---|---|---|---|
| 하한 · 중간 · 추종 | 통과 (34.7~45.0) | **동일** | **동일** |
| 0.70x | 통과 62.6 | **동일** | **동일** |
| 0.50x | 문1 사망 85.0 / 나머지 통과 86.0 | **동일** | **동일** |
| 0.35x | 전 문 사망 92.6~98.6 | **동일** | **동일** |
| 정지 시도 문2~6 | 사망 96~116 | **동일** | **동일** |
| 정지 시도 문1 | 사망 **132.0** | 사망 132.0 | 폐쇄 120.0 |

결과가 바뀌는 칸이 정확히 하나 — 지어낸 정지 시도의 문1이 120에서만 잘린다.
**150은 그것까지 그대로 담는 가장 작은 값이고, 그래서 150을 골랐다.**

### 4.2 격노 압박 구간

| 폐쇄 | 격노 창 | 단계 수 | 최대 공격 배수 |
|---:|---:|---:|---:|
| 180 | 90초 | 10 | x13.79 |
| **150** | **60초** | **7** | **x6.27** |
| 120 | 30초 | 4 | x2.86 |

실측 정지 시도가 격노 1~5단계에서 죽으므로 일곱 단계면 충분하고 여유도 남는다.
120은 계약을 지키지만 그 여유가 없다.

### 4.3 벽시계 — 화면의 숫자가 실제로 얼마인가

3단계 실기 실측 비율 **게임/실시간 = 0.70** (게임 142초 = 실시간 202초).

| | 게임초 | 실시간 |
|---|---:|---|
| 폐쇄 (이전 180) | 180 | 257초 (4분 17초) |
| **폐쇄 (확정 150)** | **150** | **214초 (3분 34초)** |
| 하한 통과 | 45.0 | 64초 |
| 0.70x 통과 | 62.6 | 89초 |
| 0.35x 사망 | 92.6~98.6 | 132~141초 |

### 4.4 unscaled(후보 D)를 기각한 근거

두 가지가 각각 독립적으로 충분하다.

**(가) 조작할 수 없는 시간을 예산에서 뺀다.** `HitStop`이 타격마다 `timeScale`을 0으로
붙드는데 unscaled 시계는 그 동안에도 흐른다. 비율 0.70이면 확정값 150은 실효
**105 게임초**가 된다 — 이름은 그대로 두고 예산을 3분의 1 가까이 깎는다.

**(나) 빼는 양이 빌드 모양에 비례한다.** 히트스톱 횟수는 타격 수에 비례하므로
다단·연격·영체 빌드가 같은 초당 피해로도 시간을 더 잃는다. 소프트캡이 하려는
일("과잉 화력을 완만하게 줄인다")과 정반대로 **빌드 모양에 벌점을 매기는** 일이고,
`TrialPowerScore` 머리 주석이 타격당 캡을 금지한 이유가 그대로 걸린다.

덧붙여 일반 보스 30초 타이머도 scaled이므로, 귀문만 unscaled로 두면 게임 안에 서로
다른 시계 규칙 둘이 생긴다.

### 4.5 구현 확인

| 항목 | 상태 |
|---|---|
| `PromotionTrialCatalog.CloseSeconds` | `180d` -> **`150d`** (이 단계에서 실제로 바꾼 유일한 밸런스 상수) |
| 종료 판정 | `BossFight.UpdateTrial`의 두 분기가 `PromotionTrialCatalog.CloseSeconds`를 읽는다 |
| HUD 카운트다운 | `TrialHud` -> `BossFight.TrialSecondsLeft` -> **같은 상수**. 하드코딩된 180은 없다 |
| 낡은 주석 | `TrialHud`·`BossFight`의 "180초" 서술 셋을 함께 고쳤다 |
| 격노 상수 | 90 / 10 / x1.3 **무변경** |

---

## 5. Android (§5) — 빌드 복구 완료 · 스모크는 실기 QA 대기

### 5.1 빌드가 깨져 있었다 — 그리고 원인은 파일 락이었다

처음 개발 APK를 굽자 Gradle이 매니페스트 병합에서 실패했다.

```
Namespace 'com.google.firebase.unity.firestore' is used in multiple modules
and/or libraries: com.google.firebase:firebase-firestore-unity:13.15.0,
:firebase-firestore-unity-13.15.0:
FAILURE: Execution failed for task ':launcher:processDebugMainManifest'
```

같은 라이브러리가 **Maven 좌표와 플러그인 두 경로로** 들어왔다. 그 이유는 생성된
AAR의 `.meta`에서 `PluginImporter` 블록이 통째로 **잘려 있었기** 때문이다:

```diff
 fileFormatVersion: 2
-guid: 3994ca618cf68b54e9b4a7b37521a935
-labels: [gpsr]
-PluginImporter:
-  platformData:
-    Android:
-      enabled: 0        <-- 이 한 줄이 "이 AAR을 플러그인으로 넣지 말라"였다
-    ...
+guid: 3994ca618cf68b54e9b4a7b37521a935
```

`Android.enabled: 0`이 사라지면 Unity가 기본값(포함)으로 읽어 AAR을 플러그인으로도
넣고, `GeneratedLocalRepo`의 maven 좌표로도 들어가 namespace가 두 번 선언된다.

**왜 잘렸는가** — 에디터 로그가 정확히 적고 있었다.

```
Cannot open file '…firebase-analytics-unity-13.15.0.aar.meta' for write.
Failed to write meta file …
Unable to write patch maven POM … (System.IO.IOException: Win32 IO returned 1224)
Resolution Failed.
```

**Win32 1224 = ERROR_USER_MAPPED_FILE.** 다른 프로세스가 그 파일을 메모리 매핑한 채라
쓸 수 없다는 뜻이고, 그 프로세스는 **빌드가 남긴 Gradle 데몬**이다(jetifier가 AAR을
매핑한다). 즉 리졸브가 메타를 다시 쓰려다 실패하고 **잘린 파일을 남긴다.**

그리고 이 손상은 **이 단계 전부터 있었다** — 세션 시작 시점의 `git status`에 이미
`firebase-firestore-unity-13.15.0.aar.meta`가 수정된 상태로 올라와 있었다(3·4단계의
빌드가 남긴 것). 즉 Android 빌드는 **이 단계가 손대기 전에 이미 깨져 있었고**, 이번
빌드 시도가 그것을 드러냈다.

### 5.2 고친 방법 — 파일을 지우거나 SDK를 바꾸지 않았다

| 조치 | 내용 |
|---|---|
| 1 | Gradle 데몬을 멈춘다 (`java` 프로세스). 매핑이 풀려 메타를 쓸 수 있게 된다 |
| 2 | 잘린 `.meta`들을 **HEAD에서 되돌린다** (`git checkout --`). 생성물의 importer 설정을 원래대로 |
| 3 | 운영 bundleId 상태에서 **Force Resolve** 를 돌린다 |
| 4 | `AndroidResolverDependencies.xml`을 HEAD 상태(18항목)로 맞춰 둔다 |

파일 수동 삭제·Firebase SDK 버전 변경은 하지 않았다. 마지막 상태:

```
생성된 .aar.meta 9개    전부 PluginImporter 유지 (Android.enabled: 0)
AndroidResolverDependencies.xml   HEAD와 동일 · 18항목 · bundleId 운영값
Assets/GeneratedLocalRepo         HEAD와 동일 (diff 0)
```

`Assets/Plugins/Android/FirebaseCrashlytics.androidlib/res/values/crashlytics_build_id.xml`
하나만 남아 있는데, 그것은 세션 시작 전부터 수정돼 있던 빌드 산출물이고 안드로이드
빌드마다 다시 쓰인다.

### 5.3 빌드 성공

```
[Onikiri] 실기 빌드 성공: Builds/Android/ONIKIRI-trial-dev.apk (83.4MB, 184초)
[Onikiri] 패키지명을 파일까지 확인해 되돌렸다: com.studio202.onikiri
[Onikiri] Firebase 설정을 원래대로 되돌렸다.
```

패키지명 복구를 강화한 것이 이 로그의 둘째 줄이다. 3단계 결함 C가 5.0단계에 **다시
났고**(빌드가 실패한 경로에서 `.dev`가 디스크에 남았다), 원인은 `finally`의
`AssetDatabase.SaveAssets()`가 **더티 플래그가 없으면 아무것도 쓰지 않는다**는 것이었다.
이제 `RestoreApplicationIdentifier`가 세 단계로 처리한다 — 메모리 복구 →
`ProjectSettings.asset`을 강제로 더티로 만들어 저장 → **파일을 읽어 접미사가 남았는지
확인**하고 남았으면 `LogError`.

**DevTools 제외 확인**은 빌드 산출물로 했다.

```
Builds/Android/ONIKIRI-trial-dev_BackUpThisFolder_…/Managed/
  Onikiri.Runtime.dll        <- 있다
  Onikiri.DevTools.dll       <- **없다**
```

### 5.4 대표 스모크는 **출시 전 실기 QA 대기**

빌드 성공 직후 기기가 USB에서 빠졌다(`adb devices` 목록 없음, `get-state` = no devices).
지시대로 이 항을 QA 대기로 명시하고 코드 폐쇄를 막지 않는다.

준비는 전부 끝나 있다 — 남은 것은 명령 여섯 줄이다.

```bash
ADB="…/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
PKG=com.studio202.onikiri.dev
FILES=/storage/emulated/0/Android/data/$PKG/files/onikiri_save.json

"$ADB" install -r Builds/Android/ONIKIRI-trial-dev.apk
"$ADB" shell am force-stop $PKG
"$ADB" push Builds/Step5/presets/floor-gate2.json $FILES     # st40 Floor
"$ADB" logcat -c
"$ADB" shell am start -n $PKG/com.google.firebase.MessagingUnityPlayerActivity
# 「이문 도전」 카드를 눌러 귀문에 들어가고, 끝나면
"$ADB" logcat -d | grep Trial5
# 같은 순서로 curve-gate6.json (st150 CurveFollower)
```

읽을 값은 `[Trial5]` 세 줄이다 — `enter`(P·damageScale·기준 화력),
`foe n/3`(1·2·3체별 시간), `end`(게임초·실시간·**ratio**·격노·종료 체력·감사 장부).
`ratio`가 3단계 스톱워치 실측 0.70 근처인지가 §4.3 벽시계 표의 유일한 실기 의존 항이다.

기기 확인 사항: Galaxy Note 20 (SM-N981N) · Android 13 · 1080x2400 ·
개발 패키지 설치돼 있음 · 세이브 경로 adb 읽기·쓰기 확인됨.

---

## 6. 안전 조건 (§6)

| 조건 | 상태 |
|---|---|
| 사용자 세이브 직접 사용·덮어쓰기 금지 | 준수. 프리셋은 `Builds/Step5/presets/`에만 쓰고, PlayMode 검사는 `GameSession.Apply`로 **파일을 지나지 않는다** |
| 운영 패키지 사용 금지 | 준수. `.dev` 접미사 + `google-services.json` 파킹 |
| SaveData 버전 변경 금지 | 준수 (v21 그대로) |
| Firebase 백엔드 수정 금지 | 준수. `google-services.json`·규칙·인덱스 무변경 |
| 씬·프리팹 변경 금지 | 준수 (이 단계 diff 0) |
| 임시 후보 복구 | 완료. k는 0.45로 되돌렸고 `TheShippedClockConstants_…`가 그것을 잰다 |
| PlayerPrefs / static / 세이브 바이트 복구 | 세이브 해시 동일. `TrialDamageScale`의 정적 상태는 `Exit()`이 멱등이고 `TearDown`이 장부를 비운다 |
| 커밋·푸시 금지 | 준수 |

---

## 7. 최종 검증 (§7)

| 항목 | 결과 |
|---|---|
| EditMode 전량 | **763 / 763** · 212초 · skipped 0 |
| PlayMode 전량 | **32 / 32** · 84초 · skipped 0 |
| 컴파일 오류·경고 | 0 / 0 |
| 사용자 세이브 해시 | `82d7b335632d4ed961a3b8dcda67e217351a1694d78f99369337ff11e4e66a15` 전후 동일 |
| DevTools의 Android 빌드 제외 | **확인** — 빌드 산출물 `Managed/`에 `Onikiri.DevTools.dll`이 없다 |
| 운영 bundleId 복구 | **확인** — `ProjectSettings.asset`이 `com.studio202.onikiri`이고 HEAD와 동일 |
| Resolver 상태 | **확인** — `AndroidResolverDependencies.xml` HEAD와 동일(18항목·운영 bundleId), 생성 메타 9개 전부 온전 |
| Firebase 백엔드·SaveData | 무변경 (v21 · 규칙·인덱스·`google-services.json` 무수정) |
| 씬·프리팹 | 이 단계 diff 0 |
| 커밋·푸시 | 하지 않았다 |

### 7.1 이 단계가 만든 diff 전부

| 파일 | 성질 |
|---|---|
| `Scripts/Progression/PromotionTrialCatalog.cs` | **`CloseSeconds` 180 -> 150** + 그 근거 주석. 이 단계가 바꾼 유일한 밸런스 상수 |
| `Scripts/Progression/GameSession.cs` | `Load()`에서 `Apply(SaveData)`를 갈라냈다 (한 줄 추출 · 순서·동작 무변경) |
| `Scripts/Battle/BossFight.cs` | 계측(`[Trial5]` 로그 셋 + `trialUnscaledClock`)을 **`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`로 제한** · 낡은 "180초" 주석 정정 |
| `Scripts/UI/TrialHud.cs` | 주석의 폐쇄 시간 서술만 정정 (로직 무변경 — 이미 상수를 읽는다) |
| `DevTools/Onikiri.DevTools.asmdef` | 신규 · **`includePlatforms: ["Editor"]`** |
| `DevTools/TrialPresetForge.cs` · `DevTools/DevSimField.cs` | 신규 · 프리셋 변환·감사·기대값 표 |
| `Editor/TrialFieldTestBuilder.cs` | 프리셋 생성 메뉴 + **패키지명 복구 강화**(파일까지 확인) |
| `Tests/EditMode/Onikiri.Tests.EditMode.asmdef` | 참조 한 줄 |
| `Tests/EditMode/PromotionTrialFixture.cs` | `FieldFromAssets`를 `DevSimField` 위임으로 (값 무변경) |
| `Tests/EditMode/TrialPresetTests.cs` | 신규 검사 9 |
| `Tests/EditMode/PromotionSoftCapMatrixTests.cs` | 신규 검사 7 |
| `Tests/EditMode/TrialClockPolicyTests.cs` | 신규 검사 8 |
| `Tests/PlayMode/TrialPresetEquivalencePlayTests.cs` | 신규 검사 2 |
| `docs/ONIKIRI_Promotion_Step5_Report.md` | 이 문서 |
| `Builds/Step5/presets/*` | 산출물 12벌 + `manifest.md` + `expected.json` |

### 7.2 새 검사 26개가 잠그는 것

| 파일 | 수 | 잠그는 것 |
|---|---:|---|
| `TrialPresetTests` (EditMode) | 9 | 프리셋이 실재 정책 둘뿐 · 결정론 · JSON 왕복 · 모든 성장 축 · 문 대기 · 두 시점의 크기와 부등식 · 출력 경로 · 미모델링 목록 |
| `PromotionSoftCapMatrixTests` (EditMode) | 7 | 도착 즉시 프레임의 하드 계약 · 쇼핑 후 프레임의 25초 바닥과 0.60 기각 근거 · 선호 밴드 이탈 순서 · 단조성 둘 · 탄력성 = k · 기준 이하 무보정 · 과화력 여유 순서 |
| `TrialClockPolicyTests` (EditMode) | 8 | 폐쇄 후보 셋의 결과 동일성 · 정지 시도 종료 · 120에서만 바뀌는 칸 하나 · 격노 창 · unscaled 환산 · **출시 상수 고정(k 0.45 · 폐쇄 150)** · 150의 압박 구간 60초/7단계 · 150에서 다섯 계약 |
| `TrialPresetEquivalencePlayTests` (PlayMode) | 2 | **런타임 화력 동등성**(AutoAttack·영체 시전율 등호, 총 화력 범위) · 런타임에서 문이 열린 상태 |

### 7.3 프리셋 동등성 실측 — 검사가 실제로 결함을 하나 잡았다

첫 실행에서 `curve-gate4`가 1.5% 모자랐다. 원인은 **런타임이 아니라 기대값**이었다.

```
런타임 영체 시전율        1.3234
YodoSpiritCurve.RateFor(프리셋의 요도 배열)  1.3234   <- 정확히 같다
row.SpiritRate (구매 전 스냅샷)             1.5352   <- 내가 기준으로 쓴 값
```

12벌 전부에서 `RateFor(프리셋 상태) == 런타임`이었다. 즉 **누락된 축은 없었고**, 구매 전
스냅샷을 기준으로 삼은 것이 잘못이었다(§2.3). 기대값이 이제 런타임과 **같은 함수·같은
입력**을 쓰고, 그 축은 등호로 검사한다.

최종 실측 (12벌, `[Trial5]` 표):

```
AutoAttack     12/12 등호 일치 (상대오차 < 1e-3, float 치명타 계수 몫)
영체 시전율     12/12 등호 일치
동료 보너스     12/12 등호 일치
총 화력         12/12 기대값의 1.00~1.04배 (오의 시전율만 하한이라 그만큼 초과)
문 대기        12/12 런타임 PendingGate == 그 문
```

---

## 8. 남은 비차단 항목

1. **Android 대표 스모크와 반복 체감 비교** — st40 Floor · st150 CurveFollower 두 프리셋
   실행(§5.4의 명령 여섯 줄)과, k 후보 사이의 "빨라졌다"는 느낌을 사람이 재는 일.
   **출시 전 실기 QA로 이관한다.** 코드 폐쇄를 막지 않는다 — 결정이 시뮬레이션과 자동
   검사로 서 있고, 폐쇄식이 3단계 실기에서 1% 안에 맞았다.
2. **Gradle 데몬과 생성 메타** — Android 빌드 뒤 데몬이 AAR을 매핑한 채 남으면 다음
   리졸브가 `.meta`를 못 쓰고 잘린 파일을 남긴다(§5.1, Win32 1224). 빌드 후 리졸브를
   돌릴 일이 있으면 **데몬을 먼저 멈춘다**. 근본 해결은 Unity·EDM4U 쪽 문제이고 이
   단계의 범위가 아니다.
2. **HitStop 비율의 기기별 확인** — 0.70은 3단계 스톱워치 실측 한 점이다. 계측 로그가
   기기에서 그 비를 직접 적으므로(`[Trial5] end … ratio=`), 비가 크게 다르면 §4.3의
   벽시계 표만 움직인다. 밸런스 판정에는 영향이 없다.
3. **밴드 앵커의 프레임** — M 표는 "보상을 쓰기 전"에 앵커돼 있다(§2.3). 그대로 두는
   것이 이 단계의 결론이다 — 앵커는 보수적인 쪽이고, 45초는 "최악의 경우에도 이 시간"
   으로 읽으면 여전히 참이다. 앵커를 옮기면 M·체력표·장부·도달일을 전부 다시 굽는
   재유도가 되고, 그것은 이 단계의 범위가 아니다.
4. **테스트 패널** — `OnikiriTestPanel`에 절을 넣지 않았다. 프리셋을 패널에서
   적용하려면 실사용 세이브를 덮어써야 하고(§6이 금지), 프리셋은 기기에 adb로 밀어
   넣는 물건이다. 그 자리는 Editor 메뉴 `Onikiri/Build/귀문 밴드 프리셋 생성`이다.
