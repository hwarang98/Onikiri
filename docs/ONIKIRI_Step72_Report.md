# ONIKIRI 72단계 보고서 — UI 잔챙이 묶음 (70단계 기록 항목)

- **브랜치** `feature/step72-ui-leftovers` (develop 553dcfa에서 분기) · 커밋 1회 · push 승인 대기
- **건드리지 않은 것** 로직 · 밸런스 · 곡선 · `Cloud/` · `firestore.rules` · SaveData v23 · 실사용 세이브 · 운영 계정
- **실기** Z Flip 3(`R5CR81SQTXB`) · 이 브랜치 개발 빌드 · **새 QA 익명 uid `0BKmpR…`** — 1 · 2(인수 확인 · 취소 뒤) · 3 · 4를 실기로 찍었다. 종료 알림 · 충돌 두 장은 **에디터 캡처**로 갈음(§2.6). 처음 작업 때는 폰이 adb에 안 잡혀 전부 에디터 캡처였다

## 0. 한 줄 요약

"레벨업 n"은 100부터 **"레벨업 99+"**. 다만 "99+"도 44pt로 231px라 칸(210)을 넘어서, 칸을 넘을 때만 **캡션 아틀라스(33pt, 174px)로 내려 쓴다**(70단계 결과 판 제목과 같은 처리). 클라우드 팝업 셋(인수 확인 · 종료 알림 · 충돌)은 창 높이를 **내용 + 48px 고정**으로 묶었다(69단계 확률 팝업 방식). 넘친 혼 정수는 결과 판 아래 **"혼 정수 초과 · 파편 +n"** 한 줄("넘"이 아틀라스에 없다). "소환 중..."은 그대로 둔다. 프리셋은 소환 경험치 둘을 직접 찍고 감사한다.

**수치** EditMode **1103/1103** · PlayMode **80/80**(MCP) · Emulator **73/73**(규칙 무수정).

## 1. 변경 파일

| 파일 | 내용 |
|---|---|
| `Widget/Hud/LevelHud.cs` | `LevelUpText(n)`(1 → "레벨업", 2~99 → "레벨업 n", 100+ → "레벨업 99+") · `levelUpCaptionFont` — 글자가 칸을 넘을 때만 캡션으로 · `LevelUpDemoted`(검사용). `PendingLevelUps`는 그대로 |
| `Widget/Popups/GachaResultPopup.cs` | `OverflowPrefix` · `OverflowNote(n)` · `IsEssenceOverflow`(★3 · 갈 곳 없음 · 하락 아님) — 묶음당 **한 줄**, 하락 줄 뒤 · `NoteTexts()`(검사용) |
| `Editor/HudScreensBuilder.cs` | `FitWindowToContent`(앵커 y를 0.5로 모으고 높이 = 마지막 행 끝 + 48) — 세 팝업 · 인수 확인의 "다시 확인"을 취소·이어하기와 **같은 줄**(둘은 함께 서지 않는다) · `RebuildCloudPopups()`(세 팝업만 다시 짓고 형제 순서 · 설정 배선 복원) |
| `Editor/BattleContentBuilder.cs` | `LevelHud.levelUpCaptionFont = UiFonts.Caption` 배선 + 검증 `RequireReference` |
| `Editor/OnikiriTestPanel.cs` | 레벨 절 "+272렙 미청구 (99+)" · 요도 뽑기 치트 "넘침 견본 판"(결과 판에 견본만, 시스템 무변화) |
| `DevTools/TrialPresetForge.cs` | `yodoSummonXp = 0` · `skillSummonXp = 0`을 직접 쓴다 + `Audit`이 둘을 본다 |
| `Data/UIStrings.txt` | "레벨업 99+" · "혼 정수 초과 · 파편 +40" |
| `Scenes/Main.unity` | 세 클라우드 팝업 재생성(`RebuildCloudPopups`, 형제 순서 동일 · 설정→충돌 배선 유지) · LevelHud 캡션 글꼴 한 칸 |
| `Tests/EditMode/UiLeftoversStep72Tests.cs` (신규, 11) | §3 |
| `Tests/PlayMode/UiLeftoversStep72PlayTests.cs` (신규, 2) | §3 |
| `Tests/EditMode/Onikiri.Tests.EditMode.asmdef` | `Unity.TextMeshPro` 참조 추가(PlayMode asmdef와 같은 참조 — 칸 폭 검사가 TMP 폭을 잰다) |

씬의 끊긴 참조는 재생성 전후 같다 — 인수 확인 · 종료 알림의 `PopupPanel.closeButton` 둘(63단계 빌더가 X를 일부러 지운다. `{fileID: 0}` 개수 전후 2개).

## 2. 항목별 전후

"전"은 70단계 실기 캡처(`Captures/step70/flip3/`, 저장소 밖). "후(실기)"는 이번 Flip 3 캡처(`docs/media/step72_flip3_*`), "후(에디터)"는 SaveSandbox 에디터 캡처(`docs/media/step72_*`).

| # | 전 | 후 (실기) | 후 (에디터) | 결과 |
|---|---|---|---|---|
| 1 | `B6_levelup_badge_overflow` — "레벨업 272"가 빨간 칸 밖 | `step72_flip3_1_levelup_99plus.png` · `_crop.png` — 칸 안 | `step72_1_levelup_99plus.png` | 대기 272 그대로 · 99는 "레벨업 99"(본 글꼴, 202px — 에디터 PlayMode 검사) |
| 2 | `a02_qa_boot` — 버튼 아래 약 500px 빈 창 | `step72_flip3_2_takeover.png` · `_takeover_cancelled.png` | `step72_2_takeover.png` · `_takeover_cancelled.png` · `_evicted.png` · `_conflict.png` | 세 창 모두 마지막 행 아래 48px. 폭(좌우 36) 그대로. 실기 인수 확인 = 에디터와 같은 모양 |
| 3 | `A5_ten1` — 희귀 "+40"에 설명 없음 | `step72_flip3_3_overflow_note.png` — 한 줄 · `_overflow_with_downgrade.png` — 하락 줄 뒤 한 줄 | `step72_3_overflow_note.png` — "+80"(넘침 2장) | "등급 하락" 줄과 다른 문구, 묶음당 한 줄 |
| 4 | `A5f_revealing_title` — "소환 중..." | `step72_flip3_4_revealing.png` | `step72_4_revealing.png` | 바꾸지 않음(§2.4) |

**2.1 레벨업 99+ — 지시서와 다른 점 하나.** 지시는 "칸 폭 그대로, 100 이상이면 99+"였는데, 실측하니 "레벨업 99+"와 "레벨업 272"가 **같은 폭(231px)** 이다("+"가 숫자 한 칸 폭). 글자만 바꾸면 넘침이 그대로 남아서, 칸을 넘을 때만 캡션 아틀라스로 내려 쓰게 했다(174px). 두 자리까지는 본 글꼴 그대로다. 대안은 "레벨업 9+"(10부터)나 칸 확장인데, 둘 다 지시서의 다른 줄을 어긴다.

**2.2 팝업 창 높이.** 인수 확인 = 행 3 끝(346) + 48 = **394px**, 종료 알림 = 행 2 끝 + 48 = **328px**, 충돌 = 행 5 끝 + 48 = **526px**. 예전에는 세이프 영역 × 0.34 / 0.30 / 0.42라 22:9에서 각각 약 900 / 790 / 1110px였다. 63단계 §10.0이 확인한 모양(인트로 위 · 제목 없음 · `[취소]` `[이 기기로 이어하기]` · 취소 뒤 `[다시 확인]`)은 그대로다 — 바뀐 것은 창 아래 빈 영역과 "다시 확인"의 줄뿐이다.

**2.3 넘침 줄 문구.** 지시서 예시 "혼 정수 넘침"의 **"넘"이 아틀라스(472자)에 없어** "초과"로 썼다(초 · 과 둘 다 있음). 아틀라스는 다시 굽지 않았다. 10연에서 여러 장 넘치면 합계 한 줄이다 — 넘침마다 한 줄이면 하락 줄을 밀어내 "외 n"으로 접힌다.

**2.4 "소환 중..."** 마침표 셋이고 "."는 아틀라스에 있다(한 글자 "…"도 있다). 에디터 캡처에서 읽힌다. "…"로 바꾸면 픽셀 글꼴에서 점 셋이 한 칸에 몰려 더 작게 읽혀서 **바꾸지 않았다.** 검사는 문자열 고정 + 아틀라스 포함만 추가했다.

**2.5 프리셋 v23 필드.** 기존 프리셋 JSON에도 두 필드는 이미 `0`으로 있었다(`NewGame` 기본값을 `JsonUtility`가 그대로 씀). 이번 변경은 **직접 쓰기 + 감사**다 — 다른 v23 필드처럼 손으로 적고, 0이 아니면 `Audit`이 어긋난 축으로 낸다. `Builds/`는 git 제외라 재생성은 커밋에 없다.

**curve-gate6 보석 −3505의 원인(고치지 않음):** 곡선추종(`Policy.Default`)은 보석 잔액 검사가 `GemsFromQuestsOnly`일 때만 걸려 **"보석 무제한"으로 지출**하고(뽑기 · 파편 팩 · 동료 해금 · 장비 등급업), 프리셋은 `GemsEarned − GemsSpent`를 그대로 잔액으로 찍는다.

**2.6 실기 절차.** 사용자가 앱을 지운 뒤(지우기 전 폰은 운영 계정 로그인 상태였다 — 그 세이브 파일 다섯은 읽기만 해서 PC `Captures/step72/device_backup_before/`에 백업) 이 브랜치 개발 빌드(`AndroidDeployBuilder.BuildDevelopmentApkBatch`, 88.4MB)를 설치했다. `allowBackup=false`라 아무것도 되살아나지 않았고, "게스트로 시작" → 새 익명 uid. 이후 운영 계정 무접촉.
- **QA 세이브**: 첫 업로드(rev 1) 뒤 앱을 멈추고 기기 세이브를 직접 고쳤다 — st41 · 보석 30,000 · 경험치 4.17e29(Lv.1에서 대기 272). 새 세이브라 요도 티어가 모두 0이어서 희귀 혼 정수는 전부 넘친다(10연 7번 중 3번 넘침).
- **인수 확인**: 앱을 멈추고 사이드카의 기기 id만 바꿔 부팅 → "다른 기기가 세션을 쥐고 있습니다" → 취소 → 다시 확인 → 이어하기(세대 3) 정상.
- **종료 알림 · 충돌은 에디터 캡처로 갈음.** 폰이 한 대라 종료 알림은 실행 중인 앱 밖에서 세션을 넘겨야 하고(QA uid 세션 문서 직접 수정), 충돌은 옛 rev 세이브 · 사이드카로 되돌려 서버와 갈라놓아야 한다. 둘 다 클라우드 상태를 일부러 어긋나게 만드는 조작이라 이번에는 하지 않았다.

## 3. 테스트

| 묶음 | 수 | 내용 |
|---|---|---|
| EditMode 신규 `UiLeftoversStep72Tests` | 11 | 1: 1/2/99/100/272 문구 · 씬 칸 폭 210 · 캡션 배선 · 캡션으로 99+가 칸 안 / 3: 넘침 문구 ≠ 하락 머리 · 넘침 판정(요도 착지 · 상위 혼 미끄러짐 제외) / 1·3·4: 아틀라스 + `UIStrings.txt` / 4: "소환 중..." 고정 / 2: 세 창 앵커 y 0.5 · 폭 36 · 모든 행 창 안 · 마지막 행 아래 48(×3) · 다시 확인 = 버튼 줄 / 5: 프리셋 12개 v23 · 두 필드 0 · 감사가 잡음 |
| PlayMode 신규 `UiLeftoversStep72PlayTests` | 2 | 대기 272 → "레벨업 99+" · 캡션 · 칸 안 · `PendingLevelUps` 272 그대로 / 99 → "레벨업 99" 본 글꼴 · 넘침 1장 → 설명 줄 1개 "혼 정수 초과 · 파편 +40" · 넘침 3장 → 한 줄 "+120". `SaveSandbox` |
| 무회귀 | | `PopupLayerTests`(팝업 층위) · `GachaUiTests`(69 문구 · 검산) · `TrialPresetTests` 포함 전체 |

**EditMode 1103/1103**(1092 + 11) · **PlayMode 80/80**(78 + 2, MCP) · **Emulator 73/73**.

> 기준 수치: 지시서의 EditMode 1081은 71단계 값이고, 71.1(지문 검사 11)을 병합한 develop의 값은 1092다.

## 4. 남은 것

1. **종료 알림 · 충돌의 실기 캡처 없음**(§2.6) — 두 기기(63단계 §9 방식)나 QA uid 클라우드 상태 조작이 필요하다. 에디터 캡처는 렌더 경로가 달라(오버레이 캔버스를 잠시 카메라 모드로 돌려 RenderTexture에 그림 — 에디터 비포커스면 Game 뷰가 안 그려진다) **결과 판 · 충돌 화면 위에 캐릭터 스프라이트가 비친다** — 실제 화면의 문제가 아니라 캡처 방식 탓이다(실기 캡처에는 없다).
2. **충돌 화면 제목 · 요약 겹침**(71.1 §8.4-2) — 이번 범위 밖. 캡처(`step72_2_conflict`)에서 자리표시 문구로는 안 겹치지만, "최고 43층 · Lv.65 · …"처럼 길어지면 머리("클라우드 기록", 폭 45%)를 덮는다.
3. 전설이 상한이라 파편이 된 경우는 지금도 `Downgraded = true`라 "등급 하락 · 전설 → 파편 n" 줄이 선다(`GachaSystem.GrantLegendary`). 68단계 말대로라면 이것도 "넘침"에 가깝다 — 문구를 맞출지는 다음 판단으로 남긴다(이번 범위는 희귀 +40).
4. 출시 전 교체 항목 없음.
