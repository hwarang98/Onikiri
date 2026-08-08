# ONIKIRI 13단계 보고서 — 보스 설정 애셋 + 지역 1 배치

작업일: 2026-08-06
테스트: EditMode **181/181 통과** (13단계에서 10개 추가)

---

## 1. BossConfig 애셋으로 추출

### 걷어낸 것

| 있던 곳 | 상수 |
|---|---|
| `CharacterSpriteSlicer.cs` | `BossCellWidth/Height`, `BossFeetPadding`, `BossSpriteFolder` |
| `BossContentBuilder.cs` | `BossSpriteFolder` (**중복**), `BossDisplayName` |

경로가 두 파일에 각각 적혀 있어서, 한쪽만 고치면 슬라이싱은 새 팩에 정의는
옛 팩에 붙었다. 이제 출처가 애셋 하나다.

### 새 애셋 세 종류

```
BossConfig   보스 하나의 설계값 (시트형 / 확대형)
RegionConfig 지역 길이 + 그 안의 배치
BossRoster   지역들을 순서대로. 스테이지 -> 보스
```

`BossConfig`가 두 유형을 한 구조로 다룬다. 스테이지 매핑이 둘을 구분할 필요가
없기 때문이다 — "이 스테이지의 보스"라는 슬롯 하나에 어느 쪽이든 들어가고,
어떻게 그릴지는 애셋이 안다.

| | 시트형 | 확대형 |
|---|---|---|
| 아트 | IDLE/HURT/DEATH/ATTACK 시트 4장 (드래그) | 잡몹 `EnemyDefinition` 참조 |
| 크기 | 셀 폭/높이 + 발밑 여백 | 확대 배율 (**정수만**) |
| 색 | — | 틴트 |
| 공통 | 표시 이름, HP/공격력/골드 추가 배수, 등장 연출 여부 | |

### 빌드 안전 — 방향을 뒤집었다

**빌더는 설정 애셋을 읽기만 한다.** 12단계까지는 반대여서
`Enemy_DarkSamurai.asset`을 인스펙터에서 고쳐도 Build Combat Content 한 번에
날아갔다.

이제 손으로 고치는 것(`BossConfig`)과 생성되는 것(`EnemyDefinition`)이 갈라져
있고, 생성물만 `Assets/_Project/Data/Generated/`에 들어간다.
`EnsureDefaultAssets()`는 **없을 때만** 씨앗을 깔고, 있으면 건드리지 않는다.

### 발밑 여백 자동 측정

`[발밑 여백 자동 측정]` 버튼이 네 시트를 픽셀로 훑어 아래쪽 빈 줄 수를 센다.
시트마다 다르면 **가장 작은 값**을 쓴다 — 큰 값에 맞추면 아트가 더 아래로
내려간 프레임이 땅에 박힌다.

**실측 (요청하신 항목):**

```
Boss_DarkSamurai  cell 128x108
  IDLE=12px, HURT=12px, DEATH=12px, ATTACK 1=12px
```

**네 시트 모두 12px로 일치**했고, 12단계까지 손으로 세서 상수에 적어둔 값과
같다. 자동 측정이 사람이 센 값을 재현한다.

인스펙터에는 `[셀 크기 추정]` 버튼도 넣었다. 모든 시트 폭의 최대공약수를
셀 폭으로, 시트 높이를 셀 높이로 잡는다 — 이 방법이 다크 사무라이 128과
FULL_Samurai 96을 정확히 맞춘다.

---

## 2. 지역 1 배치

| 위치 | 보스 | 유형 | 연출 |
|---|---|---|---|
| 일반 스테이지 | 그 스테이지 잡몹 확대판 | 확대형 (자동) | 짧게 (0.4초) |
| **5스테이지 챕터 관문** | **외눈 등롱** (Chochin-obake) | 확대형 ×2 + 붉은 틴트 | 짧게 |
| **10스테이지 지역 피날레** | **다크 사무라이** | 시트형 (Demon_Samurai) | **전체** (암전 + 이름 + 5.3초 워크인) |

**다크 사무라이는 피날레에만 나온다.** 12단계까지는 5의 배수마다 나왔고,
그래서 10스테이지의 등장이 5스테이지와 다를 이유가 없었다.
`DarkSamurai_AppearsOnlyAtTheRegionFinale` 테스트가 그 배치로 되돌아가는 것을
막는다.

전체 등장 연출도 피날레로 좁혔다. 연출을 줄이는 것이 아니라 한 곳에 몰아준다.

### 실측 (플레이 모드)

```
stage 5  config=Boss_CyclopsLantern/ScaledMob  name='외눈 등롱'
         fullIntro=False  scale=2.0  hp=2435   atk=13.2
stage 10 config=Boss_DarkSamurai/Sheets       name='다크 사무라이'
         fullIntro=True   scale=1.0  hp=77783  atk=26.6
```

스크린샷:

| 파일 | 내용 |
|---|---|
| `step13_stage5_chapter.png` | 2배 확대 + 붉은 틴트의 외눈 등롱 |
| `step13_stage10_intro.png` | 암전 + 붉은 "다크 사무라이" (피날레에만) |
| `step13_stage10_finale.png` | 도착한 다크 사무라이. 체력 바 만재 |

---

## 3. 보스 등급 3단계 + 밴드 재확인

배치가 바뀌면서 5스테이지와 10스테이지가 서로 다른 무게를 가져야 했다.
둘이 같은 배수를 쓰면 피날레는 그냥 또 하나의 챕터 보스다.

| 등급 | 체력 | 공격력 | 골드 | 경험치 |
|---|---|---|---|---|
| 일반 | ×1 | ×1 | ×1 | ×1 |
| 챕터 | ×1.25 | ×1.4 | ×2 | ×2 |
| **피날레** | **×1.5** | **×1.6** | **×2.2** | **×3** |

피날레 배수는 챕터를 **대체한다** (곱하지 않는다). 곱하면 지역이 길어질수록
피날레가 지수로 무거워진다.

### 밴드 (1~20 스테이지)

| 등급 | 밴드 | 실측 |
|---|---|---|
| 일반 | 1.5 ~ 3.0 | **1.79 ~ 2.82** ✓ |
| 챕터 | 1.3 ~ 2.0 | **1.47 ~ 1.55** ✓ |
| 피날레 | 1.15 ~ 1.7 (신설) | **1.27 ~ 1.48** ✓ |
| 최소 생존 여유 | > 1.0 | **1.17** ✓ |

세 밴드가 겹치지 않고 **피날레 < 챕터 < 일반** 순으로 놓인다. "지역의 마지막이
가장 빡빡하다"가 수치에서도 성립하고, 테스트가 그 순서를 못 박는다.

### 중간에 잡은 것 — 피날레 골드가 다음 스테이지를 망가뜨렸다

처음에 `FinaleGoldMultiplier = 3`으로 잡았더니 **11스테이지 여유가 3.48로
천장(3.0)을 넘었다.** 피날레 보상이 커서 그 다음 스테이지가 공짜가 된 것이다.
2.2로 낮춰 2.82에 넣었다. 챕터(2.0)보다는 여전히 후하다.

### 발견 — 21스테이지 이후의 여유 드리프트 (13단계 이전부터 있던 것)

밴드를 30스테이지까지 확장해 재보니 일반 보스 여유가 이렇게 간다.

```
1~4:   1.79 ~ 1.83
6~9:   2.30 ~ 2.75
11~14: 2.15 ~ 2.82
16~19: 2.03 ~ 2.49
21~24: 2.77 ~ 3.48
26~29: 3.01 ~ 3.27   <- 천장 3.0 초과
```

**이것은 13단계가 만든 것이 아니다.** 일반 보스의 배수는 이번에 손대지 않았고,
12단계 테스트가 20스테이지까지만 검사해서 그 너머를 아무도 본 적이 없었다.
보스 체력 램프(`BossHealthRampFinal = 1.065`)가 후반 플레이어 DPS 성장을
따라가지 못하는 것으로 보인다.

이번 지시 범위(Step 11 기준 = 1~20)를 넘어서고, 고치려면 11단계에서 튜닝한
램프 상수를 건드려야 하므로 **손대지 않고 보고만 한다.** 다음 단계에서
`BossHealthRampFinal`을 올리고 밴드 검사 범위를 30으로 넓히는 것을 제안한다.

---

## 4. VerifyWiring 확장

배치가 틀리면 **해당 스테이지에 도달해야만** 드러난다. 10스테이지 피날레의
참조가 비어 있어도 1~9는 멀쩡히 돌고, 그때까지 아무 신호가 없다.

고치기 전에 검사가 나쁜 상태를 잡는지 먼저 확인했다.

```
clean:        OK
피날레 비움:  Region_1: finale boss slot is empty
길이 12:      Region_1: stageCount 12 != BossCurve.RegionLength 10
              - the simulation would put the finale on a different stage
배율 0:       Boss_CyclopsLantern: scale 0 must be an integer >= 1
어두운 틴트:  Boss_CyclopsLantern: tint is too dark - SpriteRenderer.color
              multiplies, so a dark tint turns the mob into a black blob
셀 폭 96:     Boss_DarkSamurai: idle clip has 6 frames but the sheet holds 8
              - cell size is wrong
restored:     OK
```

다섯 가지 전부 잡고, 되돌리면 다시 통과한다.

`stageCount != RegionLength` 검사가 특히 중요하다. 시뮬레이션은 순수 함수라
애셋을 읽을 수 없어서 지역 길이의 **사본**을 상수로 들고 있고, 사본은 언젠가
갈린다. 11단계의 `ChapterHealthMultiplier`가 정확히 그 종류의 사고였다.

---

## 5. 앞으로 보스를 바꾸는 방법

코드를 건드리지 않는다.

1. `Assets/_Project/Data/Bosses/` 에서 우클릭 → Create → Onikiri → Boss Config
2. 시트 4장을 드래그, `[셀 크기 추정]`, `[발밑 여백 자동 측정]`
3. `Region_1.asset`의 챕터/피날레 슬롯에 드래그
4. `Onikiri/Scene/Build Combat Content`

지역을 추가하려면 Region Config를 하나 더 만들어 `BossRoster.regions`에
넣으면 된다. 지역 2·3 피날레는 신규 보스 대기 중이고, 그때까지는 마지막
지역의 배치가 반복된다 (`PastTheLastRegion_ThePatternRepeats`가 진행이 멈추지
않는 것을 확인한다).

---

## 검증 요약

- EditMode **181/181 통과** (12단계 171 → 13단계 181, 10개 추가)
- `Build Combat Content` 성공 — VerifyWiring 통과
- 세이브 마이그레이션 없음 (에디터 데이터라 세이브 형식 불변, v5 유지)
- 새 문구 `외눈 등롱`을 `UIStrings.txt`에 추가하고 폰트 재빌드
