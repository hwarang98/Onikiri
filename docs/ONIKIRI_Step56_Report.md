# ONIKIRI 56단계 — 인트로/타이틀 화면 (부팅 게이트 + 브랜드 + 계정의 집)

> 지금까지 앱을 켜면 게임에 바로 떨어졌다. 이 스텝이 제대로 된 진입을 세운다:
> **스플래시(202 STUDIO → ONIKIRI) → 타이틀 → 게임.** 이 화면이 구글 로그인
> 버튼의 집이 되고, 부팅 시 무거운 초기화(Firebase·세이브 로드)를 가리는
> 로딩 게이트가 된다.

---

## 0. 한 줄 요약

부팅 오버레이 하나가 첫 프레임부터 게임을 가리고, 뒤에서 세이브 로드와
Firebase 워밍이 조용히 끝난다. 처음 온 사람에게만 **[구글로 로그인]/[게스트로
시작]**을 묻고(슬레이어 키우기 패턴), 이미 고른/연동된 사람은 "터치하여
시작" 한 줄이다. 계정의 상시 거처는 설정(⚙)으로 옮겨 앉았다.

밸런스·게임 로직·세이브 **영향 0** (새 세이브 필드 0, v19 그대로).
테스트 **580/580** (기존 570 + 인트로 정책 10).

---

## 1. 부팅 시퀀스 = 로딩 게이트 [I-1]

```
Unity 스플래시(강제) → 202 STUDIO → ONIKIRI → 타이틀 → (탭) → 게임
                       └──────── IntroOverlay (씬에 켜진 채 저장) ────────┘
                       뒤에서: GameSession.Load(원래 경로 그대로)
                               CloudScores 초기화 + 익명 로그인 (조용히)
```

- **오버레이는 씬에 켜진 채 저장되는 유일한 전면 UI다.** 첫 프레임부터
  게임을 가려야 하므로, 켜 줄 코드를 기다리면 이미 늦다.
- **게임 진입은 세이브 적용 후에만** — `IntroPolicy.CanEnter(saveLoaded, linkBusy)`.
  로드 전에 들어가면 빈 초기 상태가 한 프레임 보인다.
- **Firebase는 게이트에 없다.** 인자 자체가 없어서 넣으려면 테스트가 먼저
  깨진다(`IntroPolicyTests` — 오프라인 안전의 증인). 초기화 실패해도 게스트
  진입이 그대로 된다. 워밍은 `CloudScores.InitializeAsync()`(1회 캐시) →
  `SignInAnonymouslyAsync()`(세션 재사용)라 **이중 초기화가 구조적으로 없다.**
- 실기 logcat 증명 — 앱 시작 직후(버튼 아무것도 안 누름):
  ```
  11:45:02.899  [CloudScores] 초기화 완료. projectId = onikiri-9cc18
  11:45:02.902  [CloudScores] 기존 로그인 재사용. uid = A9aMgxR5...
  11:45:20.792  [CloudScores] 제출 생략(후퇴 방지). 로컬 1 / 서버 3
  ```
- 폰트/애셋 워밍은 따로 하지 않는다 — 정적 아틀라스는 이미 씬 로드에 실려
  오고, 그 씬 로드 자체가 오버레이 뒤에서 끝난다.
- **인트로 화면은 플레이가 아니다** (사용자 지적으로 잡은 결함). 처음 구현은
  오버레이가 화면만 가려서 뒤에서 전투가 그대로 돌았고, 보이지 않는 타격음이
  스플래시를 뚫고 들렸다. 고침 = `IntroFlow.Awake`에서
  `HitStop.RequestBaseTimeScale(0)`(직접 timeScale 금지 규칙 준수) +
  `AudioListener.pause` — 첫 Update 전이라 전투가 **한 프레임도 안 밟는다**
  (실측: 인트로 중 `timeScale=0 / audioPaused=True / Time.time=0`, 진입 후
  `1 / False`). 재개는 진입·파괴(안전망) 양쪽에서. 계속 도는 것은 전부
  unscaled인 것들뿐이다(인트로 자신·자동 저장·제출 디바운스).

## 2. Unity 강제 스플래시 (라이선스) [I-2]

**Personal 라이선스 확인** (`Application.HasProLicense() == False`,
`PlayerSettings.SplashScreen.show == True`) — **"Made with Unity"가 강제로
맨 앞에 붙는다.** 제거는 Pro부터. 우리 스플래시는 그 뒤에 온다(실기 GIF에서
확인됨). 출시 전 Pro 전환 여부는 별도 판단.

## 3. 브랜드/타이틀 화면 [I-2·I-3]

로고 애셋 없이 **타이포그래피가 로고다**: 먹빛 바탕(카메라 배경과 같은
sumi) · ONIKIRI 132pt(=44×3, 정수배 규칙) · 적선 한 획 + "귀참" 부제(보스
체력의 적 — 강조색 셋 규칙 안) · 39단계 벚가지 실루엣 재사용. 새 애셋 0.

| 화면 | 캡처 |
|---|---|
| 202 STUDIO 스플래시 | `media/step56/boot_00.png` |
| ONIKIRI 브랜드 | `media/step56/boot_03.png` |
| 첫 실행 타이틀 (두 CTA) | `media/step56/title_first_run_preview.png` |
| 재실행 타이틀 (터치하여 시작) | `media/step56/title_returning.png` |
| 부팅 GIF (에디터 1080×1920) | `media/step56/boot_sequence_editor.gif` |
| 부팅 GIF (실기 1080×2400, Unity 스플래시 포함) | `media/step56/boot_sequence_device.gif` |

- 첫 실행 = 계정 선택: **[구글로 로그인]** 주 버튼(같은 판을 밝힌 것 —
  랭킹 내 줄과 같은 InlayTint, 새 색 아님) 위에 안내 한 줄("기록이 계정에
  남아 기기를 옮겨도 이어집니다"), 아래 **[게스트로 시작]** 부 버튼.
  강제 아님 — 게스트는 한 탭.
- ⚠️ 두 CTA 스샷은 **에디터 강제 표시 미리보기**다. 구글 버튼은
  `IsAvailable`(실기 전용) 규칙대로 에디터에서 버튼째 숨는다(20단계 함정
  버튼 규칙). 실기의 실제 첫 실행 확인은 아래 6절 참고.
- 스플래시는 어느 장이든 **탭 한 번으로 스킵**(1.4s/1.1s 자동 진행).
  타이틀은 시간으로 지나가지 않는다 — 진입은 언제나 사람의 탭이다.

## 4. 계정의 집 = 설정(⚙) [I-4]

역할 분리: **타이틀 = 진입 순간의 선택만** / **설정 = 상시 계정 거처.**

설정 패널(스샷 `media/step56/settings_account.png`):
- 계정 상태 한 줄 — "게스트 · 앱을 지우면 사라집니다" / "구글 · ○○○"
  (연동 계정 표시 이름은 남이 지은 글자라 **동적 폰트** — 랭킹 화면과 같은 규칙)
- **[구글 연동]** — 게스트가 나중에 붙이는 주 경로
- **닉네임 변경** — 54단계 랭킹 것의 상시 자리(입력·onSubmit·검증 전부
  `PlayerProfile` 한 벌). 랭킹 첫 진입의 이름 입력은 그대로 둔다 —
  타이틀 직후로 당기지 않았다(온보딩 174초 마찰 불변이 우선) [I-5]
- 랭킹 화면의 계정 줄도 그대로다 — 복구의 결과(순위 회복)가 보이는 자리라는
  55단계의 근거가 여전히 유효하다.

**연동 흐름은 한 벌이다**: 타이틀 CTA·설정·랭킹 어디서 눌러도
`GoogleLinkButton`(신규 공용 컴포넌트) → `AccountLink.LinkAsync(구글)`.
숨김 규칙(실기 전용·이미 연동이면 버튼째 숨김)과 연타 방지가 이 한 곳에 산다.
Part B(link·복구·병합)는 55단계에 이미 서 있으므로 **훅이 비어 있지 않고
실제 로직에 바로 물렸다.**

## 5. 분기의 증명 (IntroPolicy, 순수 로직)

전부 "첫 실행의 사람"에게만 드러나는 갈래라 EditMode로 못 박았다
(`IntroPolicyTests` 10개):

| 계약 | 검사 |
|---|---|
| 스플래시는 탭/시간 어느 쪽이든 앞으로만 | `Splash_AdvancesByTime` / `Splash_TapSkipsImmediately` |
| 타이틀은 시간·탭으로 자동 통과 안 됨 | `Title_NeverAdvancesByTimeOrTap` |
| 선택은 1회만 (게스트 포함) | `ReturningUser_IsNotAskedAgain` |
| 연동 유저는 로컬 기록 없어도 안 물음 | `LinkedUser_IsNotAskedEvenWithoutLocalRecord` |
| 게이트 = 세이브 로드 (Firebase 없음) | `CannotEnter_BeforeSaveIsApplied` + 시그니처 자체 |
| 세이브 손상은 침묵 금지 | `BrokenSave_IsReportedNotSilent` |

"골랐다"는 기억은 PlayerPrefs(`onikiri_account_chosen`)다 — 기기 취향이지
진행이 아니다(음소거와 같은 결). 세이브 필드 0.

## 6. 실기 검증 (안드로이드, 무선)

- **콜드 부팅**: Unity 스플래시 → ONIKIRI → 타이틀, 1080×2400(9:21)에서
  레이아웃 안정 (`boot_sequence_device.gif`)
- **이미 연동된 기기 = 로그인 안 물어봄**: 이 기기는 55단계에서 구글 연동을
  마친 기기다. 로컬 선택 기록(새 키)이 없는데도 Firebase가 "연동됨"을
  알리는 순간 선택 화면이 소리 없이 접히고 "터치하여 시작"으로 선다
  (`AccountLink.Changed` 구독 — 설계 그대로). `dev_12.png`
- **탭 → 진입**: 세이브 로드된 게임으로(`dev_entered.png`)
- **게스트 즉시 진입 실측 (에디터)**: 버튼 탭과 **같은 프레임**에 오버레이가
  내려간다 (`overlay down same frame = True`) — 마찰 0탭 추가, 진입 지연 0프레임.

## 7. 세이브 손상 보고 [I-6]

그동안 손상 세이브는 조용히 새 게임이 됐다(로그 한 줄). 이제:
- `SaveSystem.LastOutcome` (NoFile/Loaded/**Corrupt**/**FutureVersion**) 신설
- 못 읽는 원본은 **먼저 `.broken`으로 백업** — 새 게임의 첫 자동 저장(30초)이
  원본을 덮기 전에. 문의가 오면 이 파일이 증거다
- 타이틀이 경고 한 줄을 적는다: "세이브를 읽지 못했습니다 - 원본은 백업해
  두었습니다" / "세이브가 더 새 버전입니다 - 앱을 업데이트하세요" (두 문구가
  다른 이유: 사람이 할 일이 다르다 — 문의 vs 업데이트)
- 로드 동작 자체는 불변 — 관측과 백업만 얹었다.

## 8. 세이브·밸런스 [I-7]

- 세이브 필드 0, v19 그대로. `GameSession`에는 읽기 전용 `IsLoaded` 하나.
- 도달층·전투·시뮬 코드 무변경. **기존 570 테스트 전부 통과가 그 증거**
  (총 580/580, 110초).
- 폰트: 새 문구를 UIStrings.txt에 수기 등록(47단계 순서 함정 회피) →
  차셋 427자 → 44/33 아틀라스 재굽기. ONIKIRI/202 STUDIO 라틴 대문자 포함.

## 9. 구현 자리

| 파일 | 역할 |
|---|---|
| `Scripts/UI/IntroPolicy.cs` | 순수 분기(단계·선택 1회·게이트·손상 문구) |
| `Scripts/UI/IntroFlow.cs` | 오버레이 상태기계 + 워밍 + 게이트 + Replay/SkipToGame |
| `Scripts/UI/GoogleLinkButton.cs` | 연동 버튼 한 벌 (타이틀·설정 공용) |
| `Scripts/UI/SettingsPanel.cs` | 계정 섹션(상태·연동·닉네임) 확장 |
| `Scripts/Progression/SaveSystem.cs` | LastOutcome + .broken 백업 |
| `Editor/IntroScreenBuilder.cs` | 오버레이 빌더 (UI Canvas 직속·전체 화면·켜진 채 저장) |
| `Editor/HudScreensBuilder.cs` | 설정 패널 계정 행들 |
| `Editor/OnikiriTestPanel.cs` | "인트로 (부팅 화면)" 절 — 상태·기록 지우기·다시 보기·건너뛰기 |
| `Tests/EditMode/IntroPolicyTests.cs` | 10개 |

빌더 순서: `Build Combat Content` 마지막(세션 배선 다음)에
`IntroScreenBuilder.Build()` — 게이트가 볼 `GameSession`이 먼저 서야 한다.
`WireScreenExclusivity` 목록에는 **넣지 않는다**(성장 띠 화면이 아니라 전체
화면 일회성 덮개). ⚠️ **플레이 자동화·캡처 리그는 이제 부팅 오버레이를
지나야 한다** — `IntroFlow.SkipToGame()` 한 줄.

## 10. 범위 밖 (다음)

- 애플 로그인 + iOS 브링업 (규정 4.8 — `AppleSignInProvider` 스텁이 자리)
- 클라우드 세이브 / Blaze 치팅 방어 / Remote Config
- 진짜 타이틀 로고 아트 (지금은 타이포그래피 — 폴리싱 스텝)
- Unity 스플래시 제거 여부 (Pro 전환 판단)
