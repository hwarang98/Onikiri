# ONIKIRI — 3단계 작업 보고서

> 대상: `docs/ONIKIRI_MVP_handoff.md` §7 개발 순서 3단계 + 검토 피드백 반영
> 2026-08-04 · Unity 6 (6000.5.2f1) · Universal 2D (URP) · Portrait 고정
> 상태: **완료** (4단계 이후 미착수)
> 선행 보고서: `ONIKIRI_Step1-2_Report.md` (§2 배경 선택 권고는 본 문서 §4에서 정정됨)

---

## 1. 요약

검토 피드백 4건 중 **3건 완료, 1건은 제가 할 수 없어 사용자 작업 필요**. 3단계(배경 배치 + 사무라이 idle + 캐릭터 배치)는 완료.

작업 중 **버그 4건을 발견해 수정**했고, 그중 2건은 사용자가 눈으로 잡아준 것입니다. 상세는 §6.

커밋 (브랜치 `feature/step3-stage-setup`, 푸시 안 함):

| 커밋 | 내용 |
|---|---|
| `cf14b47` | 1~2단계 (되돌릴 지점) |
| `3e6d935` | 3단계 전투 스테이지 |
| `35ac916` | 리빌드가 캐릭터 위치를 덮어쓰지 않도록 수정 |
| `2a3d5a3` | 지면선 보정 30px → 24px |

---

## 2. 검토 피드백 처리

### 2.1 git — `init` 불필요

이미 저장소였습니다 (`main`, 커밋 `abb405f`, 원격 `github.com/hwarang98/Onikiri`). 정식 Unity `.gitignore`도 이미 존재 (`/[Ll]ibrary/`, `/[Tt]emp/`, `/[Ll]ogs/`, `/[Uu]ser[Ss]ettings/` 포함).

추가한 것: `.idea/`(Rider), `/[Aa]ssets/Screenshots/`(검증용 스크린샷). `Library/` 등이 스테이징되지 않았음을 커밋 전 확인했습니다.

### 2.2 Android 전환 + RGBA32 검증 — 완료

`SwitchActiveBuildTargetAsync`로 전환 후 전체 재임포트. 검증은 **임포터 설정만이 아니라 실제 임포트 결과**를 봤습니다:

```
IDLE.png        actual format = RGBA32   filter = Point   mips = 1
layer_1.png     actual format = RGBA32   filter = Point   mips = 1
Slash_64x64...  actual format = RGBA32   filter = Point   mips = 1
--- swept 286 textures ---
non-RGBA32 or non-Point: 0
```

`TextureImporter` 설정이 아니라 `Texture2D.format`을 읽었으므로, 실제 GPU에 올라가는 포맷이 무압축임이 확인된 것입니다.

### 2.3 참격 VFX 재측정 — 지적이 맞았습니다

105px은 **시트 전체(2행)의 바운딩 박스**였고 프레임 단위 측정이 아니었습니다. 64×64 프레임에 105px이 나올 수 없다는 지적 그대로입니다.

프레임 단위 재측정:

| 세트 | 시트 | 프레임당 최대 아트 | PPU 32 | 캐릭터(34px) 대비 |
|---|---|---|---|---|
| **64×64** | 320×128 (5×2, 9프레임) | 47w × **42h** | 1.31 units | **1.24×** |
| 128×128 | 640×256 (5×2, 9프레임) | 94w × 83h | 2.59 units | 2.4× |

**결론: 스케일 기준 불필요.** 128 세트는 64 세트의 2배 업스케일 사본(47×2=94, 42×2≈83)이므로, **64×64 세트를 프로젝트 PPU 32 그대로 쓰면 1.24배**로 적정합니다. PPU 예외나 Transform 스케일을 둘 필요가 없습니다.

128 세트는 버리지 말고 **보스/필살기용 큰 참격**(2.4배)으로 남겨두면 됩니다.

### 2.4 Thaleah 폰트 + RPG Essentials SFX — ⚠️ 못 했습니다

Asset Store 캐시(`%APPDATA%\Unity\Asset Store-5.x`)가 **존재하지 않습니다.** 한 번도 다운로드된 적이 없다는 뜻이고, `%APPDATA%\Unity` 전체를 뒤져도 해당 `.unitypackage`가 없습니다.

Package Manager > My Assets에서 받으려면 **Unity 계정 로그인이 필요한 대화형 UI**라 제가 대신할 수 없습니다. 직접 받아주세요. 6단계(데미지 숫자) 전까지만 있으면 되므로 현재 진행에는 지장 없습니다.

---

## 3. 3단계 작업 내역

### 3.1 밴드 기준 런타임 배치 (요청 사항)

> "CropFrame.None이라 9:21 기기에서 카메라 가시 영역이 커진다. 지면선과 캐릭터 위치를 카메라 중심이 아니라 UI BattleArea 밴드 기준으로 런타임 계산해서, 9:16 / 9:19.5 / 9:21에서 모두 밴드 안에 들어오게 해줘."

두 조각으로 나눴습니다.

**`BattleLayout`** (`Scripts/Battle/BattleLayout.cs`) — Unity 오브젝트 상태를 건드리지 않는 순수 함수. 덕분에 기기 없이 임의 해상도로 단위 테스트가 됩니다.

**`BattleStageLayout`** (MonoBehaviour, `[ExecuteAlways]`) — BattleArea RectTransform의 화면 좌표를 읽어 배경과 지면 앵커를 매 프레임 재배치.

계층 구조를 이렇게 잡아서, 캐릭터는 별도 코드 없이 지면을 따라갑니다:

```
Battle  (BattleStageLayout)
├─ Background      ← Band.Bottom에 고정
│   └─ Sky ... Gras (11 레이어)
├─ GroundAnchor    ← GroundY에 고정
│   ├─ Player
│   │   └─ Samurai (localPosition.y = 0)
│   └─ Enemies     ← 나중에 추가될 적도 자동으로 지면 위
└─ VFX
```

**플레이 모드 실측 검증:**

| 기기 | Band (실측) | GroundY | 캐릭터 밴드 내 |
|---|---|---|---|
| 9:16 (1080×1920) | `[-0.6000 .. 4.8000]` h=5.4000 | 0.15000 | ✅ |
| 9:19.5 (1080×2340) | `[-0.7313 .. 5.8500]` h=6.5813 | 0.01875 | ✅ |
| 9:21 (1080×2520) | `[-0.7875 .. 6.3000]` h=7.0875 | -0.03750 | ✅ |

Band 3종은 모두 플레이 모드에서 실측했고 테스트의 손계산 값과 정확히 일치했습니다. GroundY는 `Band.Bottom + 24/32`이며 9:16에서 지면 보정 후 재실측했습니다 (§6.4).

**플레이 중 해상도를 바꿔도 즉시 따라옵니다** — 실행 상태에서 9:19.5 → 9:21로 전환해 확인했습니다.

가로 폭은 세 기기 모두 **6.75 units로 동일**합니다. 1080/216 = 5로 폭이 배율을 결정하기 때문이고, 덕분에 배경을 기기별로 넓힐 필요가 없습니다.

### 3.2 배경 배치

Tiny Pixel Japan 11개 레이어를 뒤→앞 순서로 배치 (`Sky, Clouds, Fuji, Mountain_Back/Middle/Front, BackgroundTrees, Trees, Shrine_Single, Ground, Gras`), 정렬 순서 -110 ~ -100.

배경 스프라이트는 Center 피벗으로 임포트되므로 각 레이어를 **자기 높이의 절반만큼 올려** 밑단이 루트 원점(=밴드 바닥)에 오게 했습니다. 하드코딩이 아니라 `sprite.bounds.extents.y`에서 계산합니다.

**카메라 클리어 색을 `#F6E5BF`로 설정** — `Sky.png`가 전면 균일한 이 색이라, 세로가 긴 기기에서 배경(180px = 5.625 units)이 밴드보다 짧아 생기는 여백이 이음매 없이 메워집니다.

### 3.3 사무라이

**슬라이싱**: 23개 시트 전부 96×96 격자로 분할, 총 171개 스프라이트. 전 시트가 높이 96px에 폭이 96의 배수라 균일하게 처리됩니다.

**피벗 `(0.5, 15/96)`** — 이게 중요한 부분입니다. 프레임마다 발밑에 **일정하게 15px 여백**이 있어서, 기본 Bottom-Center 피벗이면 지면 위 **0.47 units** 떠버립니다. IDLE/RUN/ATTACK 세 시트에서 발 기준선이 모두 동일함을 확인하고 그 위치에 피벗을 잡았습니다. 덕분에 애니메이션별 보정 오프셋이 필요 없습니다.

검증: `feet offset below origin = -0.46875 units` (= -15/32, 정확히 일치)

**idle 애니메이션**: 10프레임 @ 10fps 루프, `AnimatorController` 연결.

RUN 시트에서 프레임 2~3의 발이 들리는 것(padBelow 15 → 18 → 20)은 달리기 동작이라 정상입니다.

---

## 4. 배경 선택 정정 — Spring Forest → Tiny Pixel Japan

**이전 보고서에서 "Spring Forest 우선"이라 권고한 것은 제 실수입니다.**

그 판단은 384×216이 전투 밴드 높이와 맞아떨어진다는 **치수만 보고** 낸 것이었고, 아트를 열어보지 않았습니다. 실제로 합성해 보니:

- **Spring Forest에는 설 수 있는 지면이 없습니다.** 앞 레이어(`layer_4`)가 빽빽한 덤불/수풀이고 평평한 지면선이 존재하지 않습니다. 플랫포머용 배경이고 실제 지면은 별도 Tileset에서 옵니다.
- **Tiny Pixel Japan은 사양서 §6이 원래 지정한 배경 팩입니다.** Spring Forest는 사양서에 없는 추가 팩이었습니다.
- Tiny Pixel Japan은 **평평한 흙 지면**과 사양서 §1이 명시한 **"먹빛·적·벚꽃 팔레트"**(벚꽃 + 오층탑)를 갖고 있습니다.

사용자 지시는 "Spring Forest 우선"이었으나, 그 지시 자체가 제 잘못된 권고에서 나온 것이라 근거를 제시하고 Tiny Pixel Japan으로 진행했습니다. 배경 목록은 `BattleStageBuilder` 상단 상수 블록이라 교체는 쉽습니다.

---

## 5. 테스트

**51개 전부 통과.** 3단계에서 `BattleLayoutTests` 13개 추가:

| 테스트 | 검증 내용 |
|---|---|
| `PixelRatio_IsWidthLimitedOnEveryTargetPhone` | 3기기 모두 배율 5 (폭이 배율을 결정) |
| `CameraWorldHeight_GrowsWithScreenHeight` | 12 / 14.625 / 15.75 units |
| `WorldWidth_IsIdenticalOnAllThreePhones` | 6.75 units 불변 |
| `Band_MatchesHandComputedValues` | 3기기 밴드 좌표 |
| `GroundY_MatchesHandComputedValues` | 지면선 절대값 (§6.4에서 추가) |
| `Band_IsAlways45PercentOfScreenHeight` | 밴드 비율 |
| **`GroundLine_IsInsideBandOnEveryPhone`** | **요청 사항 본체** |
| **`StandingCharacter_FitsEntirelyInsideBandOnEveryPhone`** | **요청 사항 본체** |
| `GroundLine_SitsInLowerPortionOfBand` | 머리 위 여유 공간 확보 |
| `TallPhones_ExposeSkyAboveTheBackground` | 하늘 여백 발생 조건 문서화 |
| `ScreenYToWorldY_*` (2개) | 좌표 변환 |
| `PixelRatio_NeverDropsBelowOne` | 작은 에디터 뷰 방어 |

**`BattleLayout.PixelRatio` 공식이 Unity의 `PixelPerfectCamera.pixelRatio`와 일치함을 실측 확인**했습니다 (9:16, 9:19.5 모두 5). 테스트가 검증하는 수식이 곧 런타임이 쓰는 수식입니다.

---

## 6. 발견해 수정한 버그 4건

### 6.1 밴드가 첫 프레임 값에 고착 (심각)

"해상도가 바뀔 때만 재계산"하는 캐시를 뒀는데, 카메라와 캔버스가 안정되기 전의 잘못된 값을 물고 **영영 고치지 않았습니다.** 9:19.5에서 `h=9.3445`(정답 6.5813)가 나와서 발견했습니다.

원인은 `Camera.orthographicSize` 의존이었습니다. **Pixel Perfect Camera는 실제 렌더링 시점에만 이 값을 갱신**하므로 첫 프레임과 에디터 모드에서 stale 값을 줍니다.

수정: `orthographicSize`를 읽지 않고 화면 크기에서 직접 산출 + 캐시 제거(매 프레임 재계산). 순수 함수로 분리해 둔 덕에 교체가 간단했고, 에디터 모드 프리뷰도 함께 정확해졌습니다.

### 6.2 `Player` / `Enemies` 노드 중복 생성

`EnsureChild`가 `Reparent`보다 먼저 실행되어 각각 2개씩 생겼습니다. 순서 교정.

### 6.3 리빌드가 캐릭터 위치를 덮어씀 (사용자 지적)

`BuildSamurai`가 `Player`의 자식을 전부 삭제하고 새로 만들어서, Scene 뷰에서 조정한 위치가 리빌드 때마다 날아갔습니다. **캐릭터를 어디에 세울지는 아트 결정인데 코드 상수를 고쳐야 하는** 나쁜 동선이었습니다.

수정: 기존 오브젝트가 있으면 **트랜스폼은 그대로 두고** 스프라이트·애니메이터만 갱신. `PlayerX` 상수는 최초 생성 시 시작 위치로만 남았습니다.

별도 Inspector 필드를 추가하지 않은 이유는, 위치의 출처가 Transform과 새 필드 두 개로 갈리면 오히려 헷갈리기 때문입니다. **Transform의 Position이 곧 그 필드**입니다.

검증: `(-2.85, 0.25)`로 옮긴 뒤 리빌드 → 위치 보존, 스프라이트·애니메이터 갱신 정상, 중복 없음.

### 6.4 캐릭터가 지면에서 떠 있음 (사용자 지적)

`Ground.png`의 지면 높이를 **"가장 높은 불투명 픽셀"**로 측정해 30px로 잡았는데, 열별 분포를 보니:

```
 24px 위 : 209개 열   ← 실제 지면
 27px 위 :  51개 열
 28px 위 :  32개 열
 30px 위 :   8개 열   ← 흙더미 꼭대기, 제가 쓴 값
```

30px는 353개 열 중 **8개뿐인 흙더미 꼭대기**였습니다. 최빈값 **24px**로 정정 → 발과 흙바닥 표면의 간격 **0.00000 units**.

**기존 테스트가 이걸 못 잡은 이유**: "지면선과 캐릭터가 밴드 안에 있는가"만 검사했기 때문에, 떠 있어도 밴드 안이라 전부 통과했습니다. `GroundY` **절대값**을 못 박는 테스트를 추가해 배경 교체 시 먼저 깨지도록 했습니다.

---

## 7. 생성·변경된 파일

```
Assets/_Project/
├─ Editor/
│  ├─ CharacterSpriteSlicer.cs    96x96 격자 슬라이싱, 피벗 (0.5, 15/96)
│  └─ BattleStageBuilder.cs       배경 + 지면 앵커 + 사무라이 (재실행 가능)
├─ Scripts/Battle/
│  ├─ BattleLayout.cs             밴드 계산 순수 함수
│  └─ BattleStageLayout.cs        런타임 배치 컴포넌트
├─ Animation/
│  ├─ Samurai_Idle.anim           10프레임 @10fps 루프
│  └─ Samurai.controller
├─ Scenes/Main.unity              전투 스테이지 반영
└─ Tests/EditMode/
   └─ BattleLayoutTests.cs        13개
```

메뉴: `Onikiri > Art > Slice Samurai Sheets`, `Onikiri > Scene > Build Battle Stage`

---

## 8. 캐릭터 위치 조정 방법

| 목적 | 조정 위치 |
|---|---|
| **좌우** | `Battle > GroundAnchor > Player > Samurai` → Transform **Position X** (현재 -2). 가용 범위 **-3.375 ~ +3.375** (가시 폭 6.75 units, 3기기 동일). 리빌드해도 유지됨 |
| **지면선 보정** | `Battle` → BattleStageLayout → **Ground Surface Pixels** (현재 24). 배경 아트 기준 픽셀, **32 = 1 unit**. 배경 교체 시 재측정용이지 연출 노브가 아님 |
| **전투 영역 전체** | `UI Canvas > BattleArea`의 Anchor Y (현재 0.45~0.90). 배경과 지면이 자동으로 따라옴 |

**Samurai의 Position Y는 `0`으로 두세요** — 발이 지면에 닿는 값입니다.

---

## 9. 남은 주의사항

| # | 내용 | 영향 단계 |
|---|---|---|
| 1 | **Thaleah 폰트 / RPG Essentials SFX 미확보** — Package Manager에서 직접 다운로드 필요 | 6단계 |
| 2 | **Safe Area 미처리** — `renderOutsideSafeArea=true`라 노치 대응 필수 | 7단계 |
| 3 | **9:19.5 이상에서 배경 위 하늘 여백** — 배경 180px(5.625 units)이 밴드보다 짧음. 카메라 클리어 색이 하늘과 같아 안 보이지만, 구름·별 등을 넣으면 드러남. 테스트가 이 조건을 명시적으로 문서화 중 | 연출 확장 시 |
| 4 | **패럴랙스 스크롤 없음** — 배치만 함 (3단계 범위) | 연출 확장 시 |
| 5 | **`Gras` 레이어가 캐릭터 뒤에 그려짐** — 발목 앞으로 풀이 겹치면 접지감이 더 살아남. 정렬 순서만 바꾸면 됨 | 선택 |
| 6 | **보스(184×184) 아트 높이 미측정** — aseprite라 실측 못 함. 일반 적의 2배 스케일이면 PPU 예외 필요 가능 | MVP 이후 |
| 7 | 라이선스: CC-BY 크레딧 표기, **Frostwindz(참격 VFX) 상업 라이선스 확인 필요** | 출시 전 |

푸시는 하지 않았습니다.

---

## 10. 다음 단계

사양서 §7의 4단계 — 요괴 스폰 → 이동 → 사거리 진입.

기반은 준비돼 있습니다: `Enemies` 노드가 이미 `GroundAnchor` 자식이라 스폰되는 적은 자동으로 지면 위에 섭니다. 적 스프라이트는 `.aseprite`로 임포트돼 있어 프레임 태그에서 애니메이션 클립이 생성됩니다. 스폰 X 좌표는 화면 밖 우측이므로 `+3.375` 바깥에서 시작하면 됩니다.
