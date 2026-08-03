# ONIKIRI — 1~2단계 작업 보고서

> 대상: `docs/ONIKIRI_MVP_handoff.md` §7 개발 순서 1~2단계
> 2026-08-03 · Unity 6 (6000.5.2f1) · Universal 2D (URP 17.6.0) · Portrait 고정
> 상태: **완료** (3단계 이후 미착수)

---

## 1. 이번 세션 범위

| 단계 | 내용 | 결과 |
|---|---|---|
| 1 | 프로젝트 세팅 (Portrait, Pixel Perfect Camera, CanvasScaler, 폴더 구조) | 완료 |
| 2 | BigNumber 도입 + NumberFormatter | 완료 |
| 3~ | 배경/캐릭터 배치 이후 | **미착수** |

신규 패키지 설치 없음. `PixelPerfectCamera`는 URP에 내장되어 있어 `com.unity.2d.pixel-perfect` 별도 설치가 불필요했습니다.

---

## 2. 에셋 실측 결과

작업 전 `C:\Users\ASUS\Desktop\UnityAssets`의 픽셀 스케일을 실측했습니다. 캔버스 크기가 아니라 **불투명 픽셀의 실제 아트 높이**를 측정했습니다 — PPU 결정에 의미 있는 값은 이쪽입니다.

| 팩 | 시트/프레임 | 실제 아트 높이 |
|---|---|---|
| Samurai (Mattz Art, FULL) | 960×96 (96×96 × 10프레임) | **34px** |
| Feudal Japan Enemies — 일반 8종 | 92×92 | **32~46px** |
| Feudal Japan Enemies — 보스 | 184×184 (2색상) | 미측정 (aseprite) |
| Demon Samurai | 768×108 | 52px |
| Executioner | 1560×92 | 55px |
| Slashes | 64×64 / 128×128 | 105px / 128 |
| Tiny Pixel Japan (배경) | 353×180 | 가로형 |
| Spring Forest (배경) | 384×216 | 가로형 |
| Autumn Forest (배경) | 320×180 | 가로형 |

**핵심 발견:** 사양서는 "팩마다 픽셀 스케일이 다름"을 경고했지만, 실측 결과 **주인공(34px)과 일반 적(32~46px)의 아트 스케일은 이미 서로 맞습니다.** 팩별 재스케일 없이 단일 PPU로 해결 가능합니다. Demon Samurai·Executioner의 52~55px는 그대로 두면 자연스럽게 1.6배 커 보여 보스급으로 활용할 수 있습니다.

---

## 3. 확정한 기준

### 3.1 PPU = 32 (전 에셋 단일)

근거는 위 실측. **PPU는 나중에 바꾸면 전체 재임포트**라 이번에 확정했습니다.

### 3.2 Pixel Perfect Reference Resolution = 216×384

1080×1920의 **정확히 5배 정수배**입니다. 대안 비교:

| Ref | 정수 배율 | 캐릭터가 전투 영역에서 차지하는 비율 | 동시 표시 가능 적 |
|---|---|---|---|
| 270×480 | 4× | 약 16% | 7~8마리 |
| **216×384** | **5×** | **약 20%** | **5~6마리** |
| 180×320 | 6× | 약 24% | 4~5마리 |

216×384를 선택한 이유는 사양서 §2의 "동시에 보이는 적 3~5마리" 요구와 맞고, 배경 3종이 모두 전투 밴드보다 살짝 커서 **늘리지 않고 크롭**되기 때문입니다(stretch는 픽셀 아트를 깨뜨림).

Reference Resolution은 Pixel Perfect Camera의 필드 하나이므로 언제든 조정 가능합니다. PPU와 달리 재임포트가 필요 없습니다.

### 3.3 피벗 통일

사양서가 "PPU와 피벗을 통일"이라 지시한 부분입니다. 카테고리별로 분리했습니다.

- **캐릭터 / 적 → Bottom-Center**: 92px 프레임과 184px 프레임이 같은 지면선에 서게 됩니다. 사이드뷰에서 이게 없으면 적마다 발이 뜨거나 잠깁니다.
- **배경 / UI / VFX → Center**

### 3.4 CanvasScaler match = 0 (width 우선)

사양서는 "Match = 0.5 (또는 width 우선 1)"이라 적혀 있으나, Unity에서 `matchWidthOrHeight`는 **0 = width, 1 = height**입니다. 세로 화면에서는 폭이 고정축이므로:

| match | 1080×2340 (9:21) 기기에서 |
|---|---|
| **0 (width)** | scale 1.0, UI 1:1 유지, 남는 높이는 여백 — **채택** |
| 0.5 | scale ~1.10, 가로로 약간 넘침 |
| 1 (height) | scale 1.22, 가로 22% 넘쳐 잘림 |

사양서의 "width 우선"이라는 **의도**에 해당하는 값이 0이라 판단해 0으로 설정했습니다.

### 3.5 BigNumber = 자체 `BigDouble` 구현

사양서가 제시한 두 선택지(BreakInfinity.cs / 자체 구현) 중 **자체 구현**을 택했습니다. 외부 의존성·라이선스 없이 통제 가능하고, 네트워크 취득 과정이 필요 없습니다.

---

## 4. 1단계 작업 내역

### 4.1 에셋 임포트 — `Assets/ThirdParty/` 301개 파일

| 대상 | 파일 수 |
|---|---|
| Characters (12팩) | 186 |
| Enemies/FeudalJapan (.aseprite) | 9 |
| Backgrounds (TinyPixelJapan / SpringForest / AutumnForest) | 39 |
| VFX/Slashes | 29 |
| UI/KenneyPixelUI | 38 |

사양서는 서드파티를 "최상위 유지"라 했으나 팩이 12개라 `ThirdParty/` 하나로 묶었습니다. 정리 과정의 판단:

- 팩마다 `Foo/Foo/Foo/Sprites` 식 **중복 중첩을 걷어냄**
- **적은 `.aseprite` 원본을 임포트**. 낱장 PNG 599장은 프레임 태그가 없어 어디부터 어디까지가 idle/walk/attack인지 알 수 없습니다. aseprite 임포터(`com.unity.2d.aseprite` 5.0.3 설치됨)는 태그를 읽어 애니메이션 클립을 자동 생성합니다.
- 참격 VFX는 **스프라이트시트만** 복사 (낱장 프레임 수백 장 제외)
- 라이선스 표기용 `License.txt` 동반 복사

### 4.2 임포트 규칙 자동화

`PixelArtImportPostprocessor` (AssetPostprocessor)가 `Assets/ThirdParty/`와 `Assets/_Project/Art/`에 들어오는 에셋에 기준을 자동 적용합니다. 새 팩을 넣어도 PPU 100 / Bilinear로 조용히 들어오는 사고를 막습니다.

적용 항목:

| 설정 | 값 | 이유 |
|---|---|---|
| PPU | 32 | 위 §3.1 |
| Filter Mode | Point | 사양서 §2 |
| Compression | Uncompressed | 사양서 §2 |
| Mipmap | off | 2D 고정 카메라 |
| npotScale | None | 픽셀 정렬 보존 |
| **maxTextureSize** | **4096** | 기본 2048은 1560px 시트를 조용히 축소시켜 픽셀 정렬이 깨짐 |
| **Android/iOS 오버라이드** | **RGBA32** | 블록 압축(ETC/ASTC)이 픽셀 아트를 뭉갬 |

첫 임포트에만 적용됩니다(`importSettingsMissing` 가드). 그렇지 않으면 재임포트마다 수동 슬라이싱·피벗 조정이 날아갑니다. 강제 재적용이 필요하면 메뉴 `Onikiri > Art > Reapply Pixel Art Import Settings`.

**검증 결과: 텍스처 277개 + aseprite 9개 전부 PPU 32 / Point / 무압축.**

### 4.3 Player Settings

| 항목 | 값 |
|---|---|
| Default Orientation | Portrait |
| Auto-rotate Portrait / UpsideDown / LandscapeL / LandscapeR | True / False / False / False |
| Default Screen | 1080 × 1920 |
| Product / Company | ONIKIRI / Onikiri |
| renderOutsideSafeArea | True |

`Application.targetFrameRate = 60`, `vSyncCount = 0`, `sleepTimeout = NeverSleep`은 Player Settings로 표현할 수 없어 `AppBootstrap`(`RuntimeInitializeOnLoadMethod`)에 넣었습니다. 방치형은 손을 대지 않는 동안에도 화면을 보게 되므로 sleep 방지가 필요합니다.

### 4.4 Main.unity

`MainSceneBuilder` 에디터 스크립트로 생성합니다. 재실행 가능하므로 씬 구성의 문서 역할도 겸합니다 (메뉴 `Onikiri > Scene > Rebuild Main Scene`).

```
Main Camera      Orthographic + PixelPerfectCamera
                 assetsPPU 32, ref 216×384
                 CropFrame.None      ← 긴 화면(9:21)은 레터박스 대신 월드를 더 보여줌
                 GridSnapping.PixelSnapping  ← RT 업스케일 없이 픽셀 스냅 (VFX는 부드럽게)
UI Canvas        Screen Space - Overlay, CanvasScaler 1080×1920, match 0
  ├ BottomTabBar   anchorY 0.00 → 0.10   (10%)
  ├ GrowthPanel    anchorY 0.10 → 0.45   (35%)
  ├ BattleArea     anchorY 0.45 → 0.90   (45%)
  └ TopBar         anchorY 0.90 → 1.00   (10%)
EventSystem      InputSystemUIInputModule
Battle
  ├ Background / Player / Enemies / VFX
```

4분할 컨테이너는 **빈 채로 배치만** 했습니다 (내용물은 3단계 이후).

**픽셀 계산 실측 검증 (9:16 기준):**

| 항목 | 측정값 | 설계값 |
|---|---|---|
| aspect | 0.5625 | 0.5625 (9:16) |
| pixelRatio (정수 배율) | 5 | 5 |
| orthographicSize | 6 | 6 |
| 월드 가시 영역 | 6.75 × 12 units | 6.75 × 12 |

---

## 5. 2단계 작업 내역

### 5.1 `BigDouble`

`value = mantissa × 10^exponent`. 가수부는 항상 `1 ≤ |m| < 10`으로 정규화, 지수부는 `long`.

- **범위**: 약 10^±9.2e18 — 게임이 도달할 수 없는 수준
- **정밀도**: 크기와 무관하게 double의 15자리 유지
- **Unity 직렬화 가능** — `JsonUtility` 세이브에 그대로 들어감 (사양서 §4의 PlayerPrefs + JSON 방침)

구현 시 주의한 지점:

- **덧셈의 지수 격차**: 10^100에 1을 더할 때 double 유효자리(17)를 넘는 항은 버림. 이게 이 타입의 존재 이유입니다.
- **음수 비교 순서**: `-1e50 < -1e10` — 지수가 클수록 더 작은 수. 부호에 따라 비교 방향이 뒤집힙니다.
- **`Pow` 정확도**: 결과가 double 범위에 들어오면 `Math.Pow` 직행(빠르고 정확), 넘어가면 log10 경로. 방치형 비용 곡선 `1.15^level`이 상시 호출되는 지점이라 정확도가 중요합니다.

### 5.2 `NumberFormatter`

3자리마다 티어 상승. 1~4티어는 `K/M/B/T`, 10^15부터 2글자 태그(`aa`, `ab`, … `zz`), 그 이후는 지수 표기 폴백.

실제 출력:

```
재화 성장:  7 → 6.0K → 5.1M → 4.3B → 3.7T → 3.1aa → 2.6ab → … → 846.3ah
업그레이드 비용 (10 × 1.15^level):
  lv1 → 12      lv25 → 329     lv100 → 11.7M
  lv300 → 16.2ab               lv1000 → 49.9ap
방치 시간:  42s / 3h 12m / 8h
```

`FormatDuration`도 함께 넣었습니다 — 8단계 오프라인 보상 팝업에서 쓰입니다.

### 5.3 테스트

`Onikiri.Runtime.asmdef`를 추가해 런타임 코드를 테스트 대상으로 만들고, EditMode 테스트를 붙였습니다.

**38개 전부 통과 (0.60초).**

커버 범위:

- 정규화 (큰 값 / 작은 값), 0 처리
- 사칙연산, 자리올림, 0으로 나누기 예외
- **거대값 + 미세값 흡수** — 1e100에 1을 더해도 손상되지 않음
- **음수 비교 순서**
- `Pow`: 정수승 정확성, `1.15^100` 오차 1 이내, **double 범위 초과(10^400)**
- `ToDouble` 포화 (wrap 대신 ±Infinity)
- 파싱 왕복, 쓰레기 입력 거부, 암시적 변환
- 1만 회 누적 가산 안정성
- **사양서 §4의 4개 표기 예시 정확 일치** (`1.5K` / `3.2M` / `7.8B` / `1.2aa`)
- 반올림 자리올림 (999,999 → `1.0M`, 999.6 → `1.0K`)
- `zz` 이후 지수 표기 폴백
- **`JsonUtility` 세이브 왕복**

---

## 6. 생성된 파일

```
Assets/_Project/
├─ Editor/
│  ├─ PixelArtImportSettings.cs       임포트 기준 정의
│  ├─ PixelArtImportPostprocessor.cs  자동 적용 + 강제 재적용 메뉴
│  └─ MainSceneBuilder.cs             Main.unity 생성 (재실행 가능)
├─ Scenes/Main.unity
├─ Scripts/
│  ├─ Onikiri.Runtime.asmdef
│  └─ Core/
│     ├─ DisplayConfig.cs             1080×1920 / PPU 32 / ref 216×384 단일 출처
│     ├─ AppBootstrap.cs              targetFrameRate 60, sleep 방지
│     ├─ NumberFormatter.cs
│     └─ BigNumber/BigDouble.cs
└─ Tests/EditMode/
   ├─ Onikiri.Tests.EditMode.asmdef
   ├─ BigDoubleTests.cs
   └─ NumberFormatterTests.cs
```

`DisplayConfig`가 화면 상수의 단일 출처입니다. 임포터·카메라·캔버스가 모두 여기를 참조하므로 값이 서로 어긋날 수 없습니다.

빈 폴더도 사양서 §5대로 만들어 두었습니다: `Scripts/{Save,Battle,Progression,UI}`, `Prefabs`, `Data`, `Art`.

---

## 7. 주의사항 / 미해결

| # | 내용 | 영향 단계 |
|---|---|---|
| 1 | **Thaleah 폰트, RPG Essentials 효과음이 에셋 폴더에 없음** | 6단계 (데미지 숫자) |
| 2 | **빌드 타깃이 StandaloneWindows64.** Android/iOS 모듈은 설치돼 있으나 전환 시 전체 재임포트라 임의 실행 안 함 | 빌드 시점 |
| 3 | **Safe Area 미처리.** `renderOutsideSafeArea = true`로 켜둔 상태라 `SafeAreaFitter`가 반드시 필요 | 7단계 (상단 바·하단 탭바) |
| 4 | **Game View 프리셋** "ONIKIRI Portrait 1080×1920"을 드롭다운에 추가함 — 직접 선택 필요 | 즉시 |
| 5 | **보스(184×184) 아트 높이 미측정.** aseprite라 실측을 못 했고 MVP 범위 밖. 일반 적의 2배 스케일이면 PPU 예외 처리가 필요할 수 있음 | MVP 이후 |
| 6 | **배경이 전부 가로형.** 세로 전투 밴드(5.4 units)에 맞춘 크롭 배치 필요. Spring Forest(384×216)가 가장 잘 맞음 | 3단계 |
| 7 | 라이선스: CC-BY 항목 크레딧 표기, **Frostwindz(참격 VFX) 상업 라이선스 확인 필요** | 출시 전 |

커밋은 하지 않았습니다.

---

## 8. 다음 단계

사양서 §7의 3단계 — 배경 배치(세로 크롭) + 사무라이 배치 + idle 애니메이션.

선행 작업으로 캐릭터 스프라이트시트 슬라이싱이 필요합니다 (현재 `SpriteImportMode.Single` 상태). 96×96 그리드로 자르면 되고, 피벗은 임포트 규칙이 이미 Bottom-Center로 잡아 둡니다.
