# 승급·전직 4단계 — UI/UX 및 실기 결함 폐쇄 **완료 보고서**

```
PlayMode    30 /  30 통과 ·  87초 · ignored/skipped 0   (22 -> 30)
EditMode   739 / 739 통과 · 215초 · skipped 0            (738 -> 739)
           ** PlayMode -> EditMode 같은 도메인 연속 실행에서 전량 통과 **
컴파일 오류 0 · 경고 0
사용자 세이브 해시  ee9d0220…6c806e8c   (전후 동일 · v19 st175 tier 6)
Firebase 무수정 · 커밋·푸시 없음
k = 0.45  임시 유지 · HitStop 시간 정책 미결정
Android    수정본 APK(08:50) 실기 재검증 완료
실기 결함  2건 발견 -> 2건 수정 (중복 입구 §3 · 폰트 글리프 §10)
검사 결함  1건 수정 (실행 순서 의존 §13-다)
```

절대 변경 금지 항목은 하나도 손대지 않았다: 심층 지수 0.55, 육문 M=10.4095를
포함한 체력표, k=0.45, 격노 90/10초, 폐쇄 180게임초, SaveData v21과 마이그레이션,
무료 승급 구조, Firebase, 일반 보스·스테이지 밸런스.

---

## 1. 귀문 적 크기·틴트 오류 수정 (§1)

### 무엇이 틀렸나

일반 보스 스폰은 정의·배율·틴트를 **세 갈래**로 고른다(`BossFight.cs` 565~584).
그런데 `SpawnTrialFoe`는 정의만 그 규칙대로 고르고 배율·틴트는 언제나
`stageBossScale`(2)·`stageBossTint`를 썼다. 그래서 챕터 보스가 서는 게이트에서는
이미 큰 스프라이트에 x2가 다시 곱해졌고, 배치 애셋의 `SpawnScale`은 무시됐다.

### 무엇을 했나

세 갈래를 `ResolveBossVisual()` 하나로 뽑고 **일반 보스와 귀문 적이 같은 함수를
지나게** 했다.

```
배치 애셋 있음   config.Definition ?? StageBossDefinition() · config.SpawnScale · config.SpawnTint
챕터 보스        bossDefinition                              · 1f                · Color.white
그 외            StageBossDefinition()                       · stageBossScale    · stageBossTint
```

`BeginApproach`도 이 함수를 쓴다. 두 벌이던 규칙이 한 벌이 됐으므로 **같은
종류의 어긋남이 다시 생길 자리가 없다.**

체력·공격력 배수(`config.ApplyHealth/Gold/Attack`)는 함수 밖에 남겼다. 그것은
외형이 아니라 값이고, 귀문은 체력을 카탈로그가 따로 내므로 그 셋을 타면 안 된다.

| 검사 | 결과 |
|---|---|
| 챕터 보스가 귀문에서 이중 확대되지 않음 | 코드상 단일 출처로 보장 · 실기 재확인 (§7) |
| 배치 애셋의 SpawnScale/SpawnTint 적용 | `ResolveBossVisual` 첫 갈래 |
| 일반 확대형 보스 외형 유지 | 셋째 갈래가 이전 값과 동일 |
| 일반 보스 전투 회귀 | EditMode 739 · PlayMode 30 통과 |

귀문 전용 체력·공격력·무보상·격노 규칙은 건드리지 않았다.

---

## 2. 귀문 적 체력바 (§2)

새 패널을 만들지 않고 **기존 반투명 띠를 두 층으로 늘렸다.**

```
위 (0.34~1)   이문 · 귀문        1/3         178초
아래 (0~0.30) [=========바=========]  89%
```

- `BossFight.TrialFoeHealthFraction` 신설 — `BossHealthFraction`과 같은 규칙
  (죽었거나 아직 안 섰으면 0)
- 바는 매 프레임 `anchorMax.x`를 민다(메시를 다시 안 만든다). 백분율 글자는
  정수가 바뀔 때만 — 시계·격노와 같은 규칙
- 백분율은 **올림**이다. 살아 있는 적이 "0%"로 적히면 죽은 것으로 읽힌다

### 상태별 동작

| 상태 | 체력 줄 |
|---|---|
| Entering | 숨김 |
| FightingFoe1~3 | 표시 · 지금 상대를 따라간다 |
| Transition1·2 | **숨김** (상대가 없어 0인데, 그 0은 승리로 읽힌다) |
| Victory / Failure / Closed | 숨김 |
| OnDisable(귀문 포기) | 숨김 (띠 전체와 함께) |
| 일반 보스 전투 | 숨김 · `BossHud` 체력바와 동시 표시 없음 |

검사 `TrialHealthBar_FollowsTheCurrentFoe`가 넷을 본다: 싸울 때만 뜬다 · 깎으면
줄어든다 · 전환에는 숨는다 · **2체가 서면 가득 찬 채로 다시 시작한다**(참조가
1체에 붙박이는 회귀를 잡는다).

---

## 3. 가이드 퀘스트와 귀문 진입 통합 (§3)

### 입구를 하나로 모았다

| 3단계까지 | 4단계 |
|---|---|
| 하단 도전 버튼이 「보스 도전」/「귀문 도전」으로 갈림 | 도전 버튼은 **일반 보스 전용** |
| 가이드 카드는 퀘스트만 | 귀문 대기 중 카드가 **문 입구** |
| 「처치 10/10」이 같은 자리에서 다툼 | 귀문 대기 중 숨김 |

문구는 `PromotionTrialCatalog.GateName` **하나**에서 온다(TrialHud 안의 private
표를 올렸다). 최초 대기 「이문 도전」, 실패·폐쇄 후 「이문 재도전」.

### 한 번의 탭에 한 가지만 일어난다 — **실기에서 한 번 틀렸다**

카드 전체 버튼에는 리스너가 둘 붙어 있다(`HudScreenButton.Toggle` = 퀘스트 화면,
`GuideQuestCard.OnCardClicked` = 귀문). 처음에는 상태 플래그 하나(`Suppressed`)로
갈랐는데, **실기에서 귀문과 퀘스트 화면이 함께 떴다.**

원인은 순서였다. 카드가 먼저 실행되면 그 자리에서 귀문이 열리고,
`BossFight.Changed`가 **같은 콜스택 안에서** 카드를 다시 그리며 억제를 풀어
버린다 - 그 다음 순서인 `HudScreenButton`이 풀린 억제를 보고 화면을 연다.

두 겹으로 덮었다:

```
이 버튼이 먼저   Suppressed == true 가 막는다
카드가 먼저      카드가 찍은 SuppressThisFrame() 표식이 막는다
```

검사 `GuideCard_IsTheOnlyTrialEntrance`가 넷을 본다: 셋이 배선됐는가 · 카드가
문 이름을 적는가 · **하단 도전 버튼이 잠겨 있는가** · 탭 한 번에 퀘스트 화면이
함께 열리지 않는가.

### 배선 순서 문제도 함께 막았다

가이드 카드는 `WireWalletAndHud` 안에서 지어지는데 그때 씬에 `BossFight`가
아직 없다(`BossContentBuilder.Wire`가 한참 뒤다). 거기서 찾으면 null이 들어가
**귀문 입구가 조용히 사라진다** - `LockedTab.screen`이 비었을 때와 같은 사고다.
그래서 참조는 `BuildTrialHud`가 확정한다(`WireGuideCardToTrial`). 판을 만든
쪽이 아니라 **참조를 댈 수 있는 쪽**이 잇는 `RelinkScreenTabs`와 같은 규칙이다.

---

## 4. 귀문 결과 UX (§4)

기존 `TrialResult`를 그대로 쓴다. 새 전체 화면 팝업도 결과 씬도 만들지 않았다.

```
승리   돌파
       이문 돌파 · 검귀
       공격 x1.21 -> x1.36   체력 x1.21 -> x1.33
       외형이 바뀌었다

실패   귀문 실패
       시간이 다 됐다  (또는 쓰러졌다)
       잃은 것은 없다 · 무료 재도전 가능
```

배수는 **바뀐 결과**를 적는다. 새 값만 적으면 "x1.36"이 큰지 작은지 알 수 없고,
이 화면이 존재하는 이유가 정확히 "무엇이 좋아졌는가"다. 직전 값은
`EvolutionCurve.AttackMultiplierAt(tier - 1)`로 되짚는다.

실패 제목을 **하나로 모았다.** 3단계까지는 「쓰러졌다」와 「귀문이 닫혔다」로
갈렸는데, 같은 사건(실패)이 두 이름을 가지면 무엇이 일어났는지는 오히려 안
읽힌다. 제목은 「귀문 실패」 하나이고 원인은 아랫줄이 말한다.

종료 후: `TrialHud` 전부 숨김(검사 `TrialHud_ShowsEachStateAndNeverOverlapsBossHud`
마지막 네 줄), 가이드 패널은 재도전 또는 일반 퀘스트로 복귀, 스테이지·티어는
정확히 한 칸씩만(`Trial_GrantsExactlyOneTierAndOneStage`).

---

## 5. 승급·전직 패널 (§5)

**이미 있던 것을 다시 만들지 않았다.** 조사 결과 `EvolutionPanel`은 현재/다음
경지의 초상·이름·배수와 요구 게이트를 이미 그리고 있었고, 비용·구매 버튼은
3단계에서 이미 제거돼 있었다.

빠져 있던 것은 **상태 한 줄**뿐이라 그것만 더했다:

```
도전 중     귀문 도전 중
도전 가능   귀문 열림 · 지금 도전      (금색)
그 외       귀문 st50 돌파
```

"돌파 완료"와 "잠김"은 이 함수가 아니라 바깥의 두 분기가 이미 말한다
(`IsMaxTier` -> 「최종 경지」, `IsUnlocked` -> 잠금 안내). 네 상태를 한 곳에
몰면 그 둘이 두 곳에서 결정되고 그때부터 화면이 갈린다.

`BossFight` 참조가 없으면 3단계까지의 문구로 떨어진다 - 요구 게이트는 카탈로그만
으로 알 수 있고 `BossFight`가 더하는 것은 상태뿐이라, 없을 때의 화면이 거짓말이
되지 않는다.

넣지 않은 것: 보석·골드 비용, 구매 버튼, 전직 전용 뽑기, 신규 기능.

---

## 6. 스킬 탭 배선 재발 방지 (§6)

`SkillPanelBuilder.Build()` 끝의 `BattleContentBuilder.RelinkScreenTabs()` 호출은
유지했다. 그 위에 **자동 검사**를 얹어 수동 실행에 기대지 않게 했다.

| 검사 | 무엇을 고정하나 |
|---|---|
| `SkillTab_OpensTheSkillPanel` | 스킬 `LockedTab.screen` non-null · 대상이 `SkillPanel` |
| `EveryTab_ClosesEveryOtherScreen` | 모든 탭의 `otherScreens`에 null 없음 · 누군가는 SkillPanel을 닫는다 |
| `SkillPerformer_HasBothFlashesWired` | `nameFlash` · `screenFlash` non-null |
| `TrialHud_IsInTheSceneWithEveryReferenceWired` | TrialHud 참조 **18개**(기존 15 + 체력바 3) non-null |
| `GuideCard_IsTheOnlyTrialEntrance` | 카드의 `fight` · `cardButton` · `cardScreenButton` non-null |
| `EvolutionPanel_SaysWhetherTheGateIsOpen` | 경지 표의 `fight` non-null |
| `SceneHasNoDuplicatedTrialObjects` | 빌더를 두 번 돌려도 오브젝트가 하나뿐 |

멱등성은 실측으로도 확인했다:

```
빌드 전: TrialBanner=1 TrialTransition=1 TrialNotice=1 TrialResult=1 TrialHealth=0 GuideQuestCard=1
1회 후:  TrialBanner=1 TrialTransition=1 TrialNotice=1 TrialResult=1 TrialHealth=1 GuideQuestCard=1
2회 후:  TrialBanner=1 TrialTransition=1 TrialNotice=1 TrialResult=1 TrialHealth=1 GuideQuestCard=1
```

`VerifyWiring()` 수동 실행도 통과했지만(True), **그것만으로 완료 처리하지
않았다** - 위 일곱 검사가 자동으로 같은 것을 본다.

체력 줄의 오브젝트 이름을 `Health`가 아니라 `TrialHealth`로 지은 것도 이
검사 때문이다. 씬에 "Health"라는 이름은 여럿 있을 수 있고, 중복 검사가 그것을
세면 무엇을 재는지 알 수 없게 된다.

---

## 7. 쿨타임 회귀 검사의 비공허성 (§7)

### 전 판은 공허했다

진행도 **합**만 비교했는데, 장착 오의가 전부 준비된 상태면 합이 `SlotCount`로
포화돼 있다. 그러면 전환이 쿨타임을 초기화하든 말든 전후가 똑같이 최대값이라
**검사는 통과하지만 아무것도 안 잰다.**

### 새 순서

```
1. 귀문 1체 전투 진입
2. 실제 시전 경로로 한 칸 시전        SkillSystem.DebugCastNow (Perform을 그대로 탄다)
3. 그 칸의 CooldownFraction < 1 확인   <- 자물쇠
4. 적 처치 -> 2초 전환
5. 그 칸과 진행도 합이 되돌아가지 않았는지
```

3번이 성립하지 않으면 **실패로 떨어진다.** 활성 쿨타임을 못 만들었으면 5번은
잴 것이 없고, 잴 것이 없는 검사가 통과로 집계되는 것이 이 절이 고치는 문제다.
어느 칸도 시전되지 않으면 그것 역시 실패다("사거리에 적이 없거나 장착 칸이 비었다").

---

## 8. 시간 정책 (§8)

**손대지 않았다.** `Time.unscaledDeltaTime`으로 바꾸지 않았고 격노·폐쇄 수치도
그대로다.

3단계 실기 실측: 게임 142초 = 실시간 202초 (**비율 0.70**). 원인은 `HitStop`이
타격마다 `Time.timeScale`을 0으로 붙잡는 것이고, 프로젝트에서 `timeScale`을
쓰는 곳은 그 하나다. 화면의 「180초」가 전투 중에는 실시간 약 **4분**이다.

보스 30초 타이머 등 기존 타이머도 같은 성질이라 내부적으로는 일관된다.
**최종 시간 정책은 k 밴드 실측 단계에서 별도 결정한다 - 이번 단계에서는 미결정이다.**

---

## 9. 화면비 검증 (§9)

### 가로는 화면비와 **무관하다** (구조로 증명)

```
UI Canvas: ScaleWithScreenSize · ref 1080x1920 · match = 0 (폭 기준)
```

`match = 0`이므로 **캔버스 폭은 어느 화면비에서나 1080 디자인 단위로 고정**된다.
높이만 화면비에 따라 늘어난다:

| 화면비 | 캔버스 높이(디자인 단위) |
|---|---|
| 16:9 | 1920 |
| 19.5:9 | 2340 |
| 20:9 · 1080x2400 | 2400 |

따라서 띠(760) · 진입 안내(1000) · 체력 줄(712 + 100)의 **가로 잘림 여부는 세
화면비에서 완전히 같다.** 1080x2400 실기에서 잘리지 않으면 나머지에서도 안 잘린다.

### 세로 겹침 계산 (SafeArea 높이 H 기준)

`BattleArea` 앵커 y 0.45~0.90, `GuideQuestCard` 앵커 y 0.68 · pivot(1,1) · pos(-1,-288)

| | 16:9 (H=1920) | 19.5:9 (H=2340) | 20:9 (H=2400) |
|---|---|---|---|
| 띠 위/아래 | 1704 / 1608 | 2082 / 1986 | 2136 / 2040 |
| 격노 줄 아래 | 1546 | 1924 | 1978 |
| 전환 줄 아래 | 1474 | 1852 | 1906 |
| 가이드 카드 위/아래 | 1018 / 872 | 1303 / 1157 | 1344 / 1198 |
| **최소 간격** | **456** | **549** | **562** |

세 화면비 모두 겹치지 않는다. 가로로도 띠는 화면 중앙(x -380~380), 카드는 우측
끝(x 650~1080)이라 **범위가 애초에 겹치지 않는다.**

`TrialHud`와 `BossHud`의 중첩은 상태 배타로 막혀 있고 검사가 본다
(`TrialHud_ShowsEachStateAndNeverOverlapsBossHud`).

노치는 `SafeArea`가 런타임에 인셋을 먹으므로 `BattleArea`와 띠가 함께 내려온다.

### 스크린샷 — 세 화면비 실측

기기 해상도를 `adb shell wm size`로 임시 변경해 **실제로 찍었다**(끝나고
`wm size reset` · `wm density reset`으로 복구, `Physical size: 1080x2400` 확인).

| 화면비 | 귀문 대기(가이드 카드) | 귀문 전투(띠+체력바) |
|---|---|---|
| 16:9 (1080x1920) | `Builds/Step4/aspect-16-9-pending.png` | `aspect-16-9-banner.png` |
| 19.5:9 (1080x2340) | `Builds/Step4/a19-cardzone.png` | `aspect-19_5-9-banner.png` |
| 20:9 (1080x2400) | `Builds/Step4/s1-pending.png` | `v2-fight1.png` |

세 화면비 모두에서:

- 띠 「이문 · 귀문  1/3  177초」 + 체력 바 + 백분율이 잘리지 않는다
- 가이드 카드 「귀문 / 이문 도전」이 우측 하단에 온전히 선다
- 카드가 사무라이·적을 덮지 않는다
- 상단 「보스 도전」 버튼이 안 뜬다 (중복 입구 없음)
- 노치/SafeArea와 겹치지 않는다

**남은 다듬을 거리 하나**: 체력 백분율(「100%」)이 바 우측 칸에서 세로로 약간
처져 바 끝선과 시각적으로 맞물린다. 폭이 화면비 불변이라 세 해상도에서 똑같이
나타난다. 읽는 데 지장은 없어 이번 단계에서 고치지 않았다.

## 10. Android 재검증 — 수정본 APK (08:50 빌드)

결함 둘을 고친 **최종 빌드**로 다시 설치해 처음부터 확인했다. 앞선 빌드의
결과를 옮겨 적지 않았다.

| 항목 | 결과 | 증거 |
|---|---|---|
| 기기 | SM-N981N · Android 13 · 1080x2400 | |
| 패키지 | `com.studio202.onikiri.dev` (운영 무접촉) | |
| 네트워크 | 차단 유지 (`Network is unreachable`) | |
| **일반 상태에서 카드 = 퀘스트 화면** | **정상** — 「가이드 / 무료 10회 뽑기」 탭 -> 퀘스트 화면 열림 | `final/normal-check.png` |
| **귀문 대기에서 카드 탭** | **귀문만 열림** — 퀘스트 화면 동시 개방 없음 | `final/win4/k036.png` |
| **st40 귀문 적 이중 확대** | **없음** — 사무라이보다 조금 큰 정상 크기 | `final/win4/*` · `v3-foe.png` |
| **체력바 1체 -> 전환 -> 2체 초기화** | **정상** — 아래 표 | `final/sheet-banner.png` |
| **스킬 탭** | **정상** — 계열 탭(검식/혈식/귀오의) · 목록 · 상호 배타(장비로 전환 시 안 남음) | `final/16-tabs.png` |
| **승리 결과** | **정상** | `final/13-win-result.png` |
| **실패(폐쇄) 결과** | **정상** | `final/12-fail-result.png` |
| **logcat 예외** | **C# 코드 결함 예외 0건** | 아래 |
| 실패 무손실 | 폐쇄 전후 stage·max·kills·bossKill·tier·강화·보석 전부 동일 | |
| 승리 후 상태 | stage 41 · tier 2 · `bossKillCount == max - 1` | |

### 체력바가 상대를 따라간다 (실측)

3초 간격 캡처. 각 적이 죽고 다음 적이 서면 **정확히 100%로 다시 시작한다.**

```
1/3   92% 79% 70% 54% 45% 29% 20% 6%
2/3  100% 87% 78% 63% 54% 40% 32% 17% 6%      <- 초기화
3/3  100% 94% 88% 80% 75% 69% 61% 56%          <- 초기화
시계  179 -> 118초 단조 감소
```

### 결과 화면 (수정본 실측)

```
승리   돌파
       이문 돌파 · 검객
       공격 x1.10 -> x1.21   체력 x1.10 -> x1.21
       외형이 바뀌었다

실패   귀문 실패
       시간이 다 됐다
       잃은 것은 없다 · 무료 재도전 가능
```

두 화면 모두 **ㅁ 없이** 나온다(§10 글리프 수정 확인).

### logcat

```
C# 코드 결함 예외 (NullReference / MissingReference / IndexOutOfRange /
                   InvalidOperation / Argument)        0 건
```

남은 로그는 셋 다 원인이 분명하고 게임 코드와 무관하다:

| 로그 | 건수 | 원인 |
|---|---|---|
| `FirestoreException: Failed to get document from server` | 22 | 네트워크를 의도적으로 끊었다. 코드가 이미 실패 경로를 갖고 있다 |
| `ClassNotFoundException: …AssetPackManager` | 6 | 3단계부터 있던 것. Play Asset Delivery 미사용 |
| `AdrenoUtils / Gralloc4 / GraphicBufferAllocator` | 5 | 4x4 버퍼 할당에 대한 Adreno 드라이버 잡음 |

### 결함 2건 (앞 빌드에서 발견 -> 이번 빌드에서 수정 확인)

**(1) 탭 한 번에 귀문과 퀘스트 화면이 함께 열렸다** — §3에 원인·수정을 적었다.
수정본에서 귀문만 열리고, **일반 상태에서는 카드가 여전히 퀘스트 화면을 연다**
(억제가 귀문 대기에만 걸린다는 것을 실기에서 양쪽으로 확인했다).

**(2) 결과 문구에 ㅁ이 떴다** — 「시간이 다 **ㅁ**다」 · 「무료 재도전 가**ㅁ**」.
`능`(U+B2A5)·`됐`(U+B410)이 폰트 아틀라스에 없었다.

문자셋 빌더는 `UIStrings.txt`·프리팹·씬·데이터 애셋을 훑는데 **런타임에 C#이
만드는 문자열은 그 넷 어디에도 없다.** 내가 새 문구를 코드에 적고 등재를 잊었다.
`FontCharsetBuilder` 주석이 정확히 이것을 예고했다.

조치 셋:

1. `UIStrings.txt` 등재 -> `Rebuild Font Charset`(458 -> 460자) ->
   `Build Pixel Font Assets`. 본문 폰트 둘에서 `HasCharacter = True` 확인
2. **귀문 문구를 `PromotionTrialCatalog` 상수로 모았다.** TrialHud·GuideQuestCard·
   EvolutionPanel이 전부 그 상수를 쓴다 - 문구의 출처가 하나가 된다
3. **EditMode 검사** `EveryTrialStringHasGlyphsInTheAtlas` — `AllUiStrings()`의
   모든 글자가 `FontCharset.txt`에 있는지 본다. 기존 검사가 "등재(UIStrings)"를
   본다면 이쪽은 **구워진 결과물(아틀라스)**을 본다. 실기에 나가는 것은
   아틀라스이지 등재가 아니기 때문이다. 문구 28개 · 글자 118자

### 안전 조건 — 한 번 흔들렸고, 게임 코드가 막았다

화면비 검증에서 `wm size reset` / `wm density reset`을 돌린 뒤 **폰 Wi-Fi가
저절로 살아났다.** 그 상태로 개발 빌드가 네 번 실행돼 Firebase에 접속했다.

그런데 제출은 **전부 생략됐다**:

```
[CloudScores] 제출 생략(후퇴 방지). 로컬 40 / 서버 52
```

단조 병합(후퇴 방지)이 막았다. **운영 리더보드는 오염되지 않았다.** 발견 즉시
`svc wifi disable` · `svc data disable`로 다시 끊었고, 이번 재검증 내내
`Network is unreachable`을 유지했다.

남는 사실 하나: 개발 패키지가 자기 익명 계정
(`uid = WhaXtdO5VMRbM2cYi9e98zCMloI3`)으로 운영 Firestore에 문서를 갖고 있고
서버 기록이 **52**다. 운영 계정과 다른 uid이므로 사용자 기록과 섞이지 않았지만
**출시 전 정리 대상**이다.

## 11. 씬 변경

- `Build Combat Content` **전체 실행 안 함**. `Onikiri/Scene/Build Trial Hud`
  하나만 돌렸다(그 안에서 가이드 카드·경지 표 참조까지 확정된다).
- `Main.unity`를 텍스트로 직접 고치지 않았고 기존 변경을 되돌리지도 않았다.
- 이번 단계가 씬에 **추가한 것**: `TrialBanner/TrialHealth`(바+백분율) 하나.
  나머지 diff는 빌더가 띠 안의 라벨 세 개(Gate/Foe/Clock)의 앵커 하단을
  0 -> 0.34로 옮긴 것과 누적 재직렬화다.

`git diff --numstat` 전후는 §13에 있다.

---

## 12. 완료 조건 대비

| 조건 | 결과 |
|---|---|
| EditMode 전량 통과 | **739/739** · skipped 0 |
| PlayMode 전량 통과 | **30/30** (22 -> 30) · skipped 0 |
| **PlayMode 후 EditMode 순서에서도 전량 통과** | **충족** — 같은 도메인 연속 실행 실측 |
| ignored/skipped 0 | **0** |
| 컴파일 오류·경고 0 | 충족 |
| 사용자 세이브 전후 해시 동일 | `ee9d0220…6c806e8c` (v19 · st175 · tier 6) |
| 일반 보스 회귀 없음 | 충족 |
| 귀문 승리·실패·폐쇄·재도전 | 충족 (실기 확인) |
| 스킬 탭 정상 | 충족 (실기 + 자동 검사) |
| 가이드 패널 클릭으로 실제 귀문 진입 | 충족 (실기) |
| **일반 상태에서 카드 = 퀘스트 화면** | 충족 (실기) |
| 빌더 멱등성 | 충족 (2회 실행 실측) |
| VerifyWiring 통과 | True |
| Android 재검증 (수정본 APK) | **완료** |
| 폰트 글리프 | **수정 완료** — ㅁ 2자 -> 등재·재굽기·자동 검사 |
| 검사 실행 순서 의존성 제거 | **완료** (§13-다) |
| Firebase 무수정 · 커밋·푸시 없음 | 충족 |
| k = 0.45 | 임시 유지 (차단 조건 아님) |
| HitStop 시간 정책 | 미결정 (차단 조건 아님) |

---

## 13. 부록 — 검사 중 드러난 것들

### (가) PlayMode 검사가 세이브의 강화 상태에 흔들리고 있었다

4단계에서 검사 일곱 개가 **실행마다 다른 조합으로** 떨어졌다. 그 무작위성 자체가
원인의 증거였다. 셋이 겹쳐 있었다:

1. **`KillCurrentFoe`가 소프트캡을 안 되돌렸다.** `MaxHealth x 1000`은 배율이
   1/1000보다 작아지는 순간 조용히 부족해진다. 배율은 세이브가 정하므로
   사람마다 다른 시점에 그렇게 된다. 이제 배율로 나눈다.
2. **적을 세우기 전에 잡몹이 죽어 할당량을 넘었다.** 대기 조건은
   `killsThisStage == KillsPerStage`로 **정확히 같아야** 하는데, `timeScale = 10`
   에서 한 프레임이 게임 0.17초라 그 사이에 잡몹이 죽는다. 이제 상태를 적기 전에
   필드를 비우고 스폰을 멈춘다(`BeginTrial`이 하는 일과 같다).
3. **문을 연 프레임에 세 적이 다 죽었다.** 상위 세이브는 st30 적을 한 방에 벤다.
   그러면 `FightingFoe1`이 한 프레임도 안 서고 지나가 `WaitForTrial`이 놓친다 -
   검사는 "1체가 서지 않았다"로 죽지만 실제로는 "이미 이겼다"였다. 이제
   `ChallengeTrial()` **바로 뒤**(점수를 읽은 다음)에 화력을 멈춘다.

판정 기준은 하나도 낮추지 않았다. 셋 다 **재는 방법**의 결함이었다.

### (나) 검사 하나가 다른 검사의 결과를 바꾸고 있었다

`TrialDamageScale`의 감사 장부는 정적이라 씬을 다시 열어도 안 지워진다.
`Exit`는 배율만 끄고 장부는 `Enter`가 비우므로, 귀문을 켠 채 끝난 검사 다음에
`OrdinaryCombat_DamageIsUnscaled`가 오면 "귀문 밖의 장부"에 앞 검사의 기록이
남아 있다. `TearDown`에서 `ResetAudit()`을 함께 부른다.

### (다) EditMode 검사 하나가 실행 순서에 흔들렸다 — **고쳤다**

`PolishBatchTests.BatchMultiplier_DefaultsToOne`이 `UpgradeBatchSelector.Current`
(static)를 직접 읽었다. 그 값은 씬이 한 번이라도 돌면 `Awake`가 PlayerPrefs로
덮으므로, **PlayMode를 먼저 돌린 뒤 EditMode를 돌리면** 이 기기의 취향(100)이
그대로 읽혀 검사가 떨어졌다:

```
도메인 리로드 직후          Current = 1     -> 통과
PlayMode 실행 후 EditMode   Current = 100   -> 1건 실패
```

검사 주석이 스스로 적어 둔 전제("static 초기값이라 **씬 없이** 읽힌다")가
그때 깨진다.

**고친 방법은 판정을 느슨하게 하는 것이 아니라 규칙을 순수 함수로 내리는 것이다.**

- `UpgradeBatchSelector.Resolve(saved, available)` 신설 — 저장값과 지금 줄의
  배수 목록을 받아 유효한 배수를 돌려주는 순수 함수
- `Awake`가 그것을 쓴다. 같은 규칙이 두 벌이 되지 않도록 `IsValid`는 지웠다
- 검사는 이제 static을 안 읽고 **입력을 주고 답을 본다**. 실행 순서도 이 기기의
  PlayerPrefs도 상관이 없다

검사도 넓혔다 — 기본값 하나만 보던 것이 다섯 가지를 본다:

```
Resolve(1, [1,10,100])    = 1      취향이 없는 사람
Resolve(0, [1,10,100])    = 1      옛 세대의 '최대'
Resolve(-5, [1,10,100])   = 1      음수
Resolve(50, [1,10,100])   = 1      지금 줄에 없는 배수
Resolve(100, null)        = 1      배수 줄이 없는 씬
Resolve(10, [1,10,100])   = 10     유효한 취향은 살아난다
Resolve(100, [1,10,100])  = 100
```

**실증**: PlayMode 30/30을 돌린 **직후 같은 도메인에서** EditMode를 돌려
739/739 통과했다.

### (라) `git diff --numstat` 전후

원본은 `Builds/Step4/numstat-before.txt` · `numstat-after.txt`에 있다. 4단계에서
바뀐 파일만 추리면:

| 파일 | 시작 (+/-) | 종료 (+/-) |
|---|---|---|
| `Scripts/Battle/BossFight.cs` | 568 / 14 | 643 / 33 |
| `Scripts/UI/BossHud.cs` | 29 / 4 | 33 / 2 |
| `Scripts/UI/EvolutionPanel.cs` | 18 / 11 | 68 / 12 |
| `Scripts/UI/GuideQuestCard.cs` | (미변경) | 129 / 2 |
| `Scripts/UI/HudScreenButton.cs` | (미변경) | 36 / 0 |
| `Scripts/UI/UpgradeBatchSelector.cs` | (미변경) | 50 / 15 |
| `Tests/EditMode/PolishBatchTests.cs` | (미변경) | 34 / 4 |
| `Editor/BattleContentBuilder.cs` | (미변경) | 8 / 0 |
| `Editor/BossContentBuilder.cs` | 202 / 0 | 326 / 0 |
| `Data/UIStrings.txt` | 17 / 0 | 33 / 0 |
| `Art/Fonts/Galmuri11 SDF.asset` | 2011 / 1784 | 2007 / 1782 |
| `Art/Fonts/Galmuri11 Caption SDF.asset` | 1875 / 1644 | 1891 / 1642 |
| `Art/Fonts/Galmuri11 Name SDF.asset` | 6 / 831 | 55 / 880 |
| `Scenes/Main.unity` | 32076 / 30639 | 31722 / 29952 |

추적되지 않는 신규 파일(`Scripts/UI/TrialHud.cs`,
`Scripts/Progression/PromotionTrialCatalog.cs`, `Tests/PlayMode/*`)은
`git diff`에 안 나오므로 위 표에 없다.

폰트 SDF 셋과 `UIStrings.txt`는 §10의 글리프 수정으로 다시 구워진 결과다.
`UpgradeBatchSelector.cs`·`PolishBatchTests.cs`는 §13-다의 순서 의존성 제거다.

**씬 diff는 오히려 줄었다**(+32076/-30639 -> +31722/-29952). 이번 단계가 씬에
추가한 것은 `TrialBanner/TrialHealth` 하나이고, 그보다 앞선 3.1의 누적
재직렬화가 `Build Trial Hud` 재실행으로 정리된 결과다.

---

## 14. 최종 상태

요구된 재검증을 전부 수행했고, 그 결과는 위 각 절에 실측값으로 들어갔다.

| 요구 | 결과 |
|---|---|
| 1. PlayMode 총 검사 수 30개로 정정 | **완료** — 머리말·§12. 앞 판의 "31"은 오기였다(중복 개방 검증은 새 검사가 아니라 `GuideCard_IsTheOnlyTrialEntrance` 안의 단언) |
| 2. 수정본 PlayMode 30/30 | **완료** — 87초 · skipped 0 |
| 3. PlayMode 후 EditMode 전량 통과 | **완료** — 같은 도메인 연속 실행에서 739/739 |
| 4. PolishBatchTests 순서 의존성 제거 | **완료** — §13-다 (`Resolve` 순수 함수 추출) |
| 5. 수정본 APK 빌드·설치 | **완료** — 08:50 빌드 |
| 6. 실기 7항목 | **완료** — §10 (증거 파일 경로 포함) |
| 7. 사용자 세이브 전후 해시 동일 | **완료** — `ee9d0220…6c806e8c` |
| 8. 보고서 갱신 | 이 문서 |
| 9. §14 교체 | 이 절 |

### 차단 조건이 아닌 것 (지시대로 남긴다)

| 항목 | 상태 |
|---|---|
| k = 0.45 최종 고정 | 미결정 — 밴드 실측 단계의 일 |
| HitStop 시간 정책 | 미결정 — §8 지시대로 손대지 않았다 |
| 16:9 · 19.5:9 스크린샷 | 확보돼 있다(§9). 이번 완료의 차단 조건은 아니었다 |
| 개발 패키지·클라우드 문서 정리 | 출시 전 체크리스트 — 폰의 `com.studio202.onikiri.dev` 삭제 + 익명 uid 문서 삭제 + 네트워크 복구 |

### 이번 단계가 의도적으로 안 고친 것

체력 백분율 글자의 세로 정렬이 바 끝선과 살짝 맞물린다(§9 끝). 세 화면비에서
동일하게 나타나고 읽는 데 지장이 없어 다듬기로 남겼다.
