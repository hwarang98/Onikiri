# ONIKIRI 53단계 보고서 — Firebase 스파이크: 도달층 write/read 왕복

작업일: 2026-08-13
테스트: EditMode **516/516 통과** (신규 0 — 아래 "왜 새 테스트가 없는가" 참고)
세이브: **불변** — 새 필드 0, 버전 그대로 v18
게임 로직·밸런스: **영향 0** — 이 스텝이 만든 코드는 게임 상태를 읽기만 한다

52단계가 도달층(MaxStageReached)에 리더보드 점수의 성질 셋(결정론·단조·유일
경로)을 세워두고 끝났다. 이 스텝은 그 숫자 하나가 **안드로이드 실기에서**
Firestore까지 갔다가 그대로 돌아오는지만 증명한다. 리더보드 UI도, 자동 제출도,
서버 재검증도 없다 — 토대가 서지 않은 채 그 위를 짓는 것을 막는 것이 스파이크의 일이다.

| 항목 | 상태 |
|---|---|
| S-1 부트스트랩 | 완료 — CheckAndFixDependencies가 Available일 때만 진행, 1회 캐시 |
| S-2 익명 로그인 | 완료 — 실기 uid `A9aMgxR5qNSmuR9tmobfs4PT8St1`, 기존 세션 재사용 |
| S-3 도달층 write | 완료 — `scores/{uid}` merge + 서버 타임스탬프 |
| S-4 read 왕복 | 완료 — **실기 write 1 → read 1**, `IsFromCache = False` |
| S-5 오프라인 안전 | 완료 — write는 로컬 큐, 게임은 계속 돎, 복귀 시 서버 반영 확인 |
| S-6 수동 호출 | 완료 — 테스트 패널 절 + 기기 오버레이 버튼(개발 빌드 전용) |
| S-7 실기 검증 | 완료 — SM-N981N(Galaxy Note20) / logcat 캡처 아래 |

---

## S-1~4. 왕복 — 실기 logcat

```
04:57:42.664 I/Unity: [CloudScores] 스파이크 시작. 도달층 = 1
04:57:42.671 D/nativeloader: Load .../lib/arm64/libFirebaseCppApp-13_15_0.so ... ok
04:57:42.919 I/Unity: [CloudScores] 초기화 완료. projectId = onikiri-9cc18
04:57:44.082 D/FirebaseAuth: Notifying auth state listeners about user ( A9aMgxR5qNSmuR9tmobfs4PT8St1 )
04:57:44.090 I/Unity: [CloudScores] 익명 로그인 성공. uid = A9aMgxR5qNSmuR9tmobfs4PT8St1
04:57:44.495 I/Unity: [CloudScores] write 성공. scores/A9aMgxR5qNSmuR9tmobfs4PT8St1 maxStage = 1
04:57:44.641 I/Unity: [CloudScores] read 성공. maxStage = 1 / updatedAt = 2026-08-13 04:57:44 / 캐시에서 옴 = False
04:57:44.642 I/Unity: [CloudScores] ★ 왕복 성공 - write 1 -> read 1
```

**전 과정 1.98초.** 콜드 스타트(네이티브 로드 + 로그인 + write + read)를 다 포함한
값이다. 도달층이 1인 것은 폰의 세이브가 새로 깔린 것이기 때문이고, 같은 코드가
에디터(도달층 170)에서도 같은 왕복을 돈다.

**`캐시에서 옴 = False`가 이 보고서의 핵심 한 줄이다.** 읽기를 기본 소스로 하면
방금 내가 쓴 값이 로컬 캐시에 그대로 있어서, 서버에 한 글자도 안 갔어도 똑같이
돌아온다 — 왕복을 증명하는 것처럼 보이지만 아무것도 증명하지 않는다. 그래서
`Source.Server`로 못 박고 스냅샷의 출처를 함께 찍는다.

캡처 원본은 `docs/media/step53/`에 있다 — `logcat_roundtrip.txt`(위 로그의 전문,
Firebase 네이티브 로드와 App Check 경고까지 포함), 기기 오버레이 스크린샷 2장.

## 로그를 보는 자리 넷

| 어디 | 무엇이 보이나 | 방법 |
|---|---|---|
| 기기 (실기) | 이 스텝의 증거 전부 | `adb logcat -s Unity` — 태그 `[CloudScores]`로 거른다 |
| 에디터 콘솔 | 같은 줄이 그대로 | 플레이 모드에서 테스트 패널의 버튼을 누르면 Console에 찍힌다 |
| 기기 화면 | 마지막 상태 한 줄 | 오버레이의 노란 글씨(`CloudScores.Status`) — logcat 없이도 성패가 읽힌다 |
| 서버 | 실제로 올라간 값 | REST 조회(아래) 또는 Firebase 콘솔 Firestore |

adb는 PATH에 없고 Unity 번들 SDK에 있다:
`C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`

## 콘솔 확인 — 제3의 클라이언트로

Firestore REST에 인증 없이(테스트 모드 규칙) 직접 물어봤다. 게임 클라이언트가
아닌 경로에서 같은 문서가 보인다는 것이 콘솔 스크린샷보다 강한 증거다:

```
GET https://firestore.googleapis.com/v1/projects/onikiri-9cc18/databases/(default)/documents/scores

scores/A9aMgxR5qNSmuR9tmobfs4PT8St1   name "Rang"  maxStage 1    updatedAt 2026-08-12T19:57:44.400Z  (폰)
scores/ojKkA7ZkzZcZvXxEZnvaQBpwPfi2   name "Rang"  maxStage 170  updatedAt 2026-08-12T19:49:21.721Z  (에디터)
```

문서 ID = uid, 필드 4개(uid·name·maxStage·updatedAt) 전부 의도대로. 서버
타임스탬프는 클라이언트 시계가 아니라 서버가 찍었다(UTC 기준이라 로그의 로컬
시각과 9시간 차).

## S-5. 오프라인 안전 — 방치형의 필수 조건

기기의 wifi를 끄면 무선 디버깅도 함께 끊겨 **관측 수단이 대상과 함께 사라진다.**
그래서 Firestore의 네트워크만 끄는 스위치(`SetNetworkEnabledAsync`)를 두고 같은
코드 경로를 밟았다 — 흉내가 아니라 Firestore가 실제 오프라인에서 하는 그 동작이다.

| 시점 | 관측 |
|---|---|
| 네트워크 OFF → 스파이크 | 상태 = "write 응답 없음 - 오프라인 큐에 남았습니다 (복귀 시 자동 전송)" |
| 그 동안 게임 | **계속 돎** — 프레임 789 → 2362, 예외 0, 플레이 모드 유지 |
| 네트워크 ON | 큐가 자동 전송 — 서버 문서 updateTime `19:49:22` → **`20:07:29`** |

마지막 줄이 요점이다. 오프라인에서 버린 것이 아니라 **미룬 것**이고, 복귀하면
사람이 아무것도 안 해도 올라간다. 방치형에서 이것이 안 되면 지하철에서 깬 보스가
기록에서 사라진다.

무한 대기도 막았다. Firestore의 오프라인 write Task는 **영영 완료되지 않는다**
(로컬 큐에 남는 것이 정상 동작이다). 그대로 await하면 버튼이 영원히 도는 것처럼
보이므로 15초에서 기다리기를 포기하고 보고한다 — 포기하는 것은 기다림이지 write가
아니다.

## 빌드 — Firebase를 얹은 대가

| 빌드 | 크기 | dex |
|---|---|---|
| 52단계(Firebase 이전, 릴리스) | 44.08 MB | 1장 |
| 53단계(Firebase 포함, 릴리스) | **52.84 MB** | **3장** |
| 53단계(개발 빌드, 실기 검증용) | 66.90 MB | — |

**증가분 +8.76 MB (+19.9%).** 패키지 8개(Auth·Firestore·Analytics·Crashlytics·
Messaging·RemoteConfig·AppCheck·Installations)를 다 얹은 값이고, 그중 절반 이상이
`libFirebaseCppApp-13_15_0.so` 하나(4.19 MB)다. 아키텍처는 arm64 단독이라 이
숫자가 그대로 사용자가 받는 양이다.

**multidex는 손대지 않았다.** dex가 1장에서 3장으로 늘어난 것이 65k 메서드 한계를
넘었다는 증거인데, minSdk가 26이라 안드로이드가 멀티덱스를 네이티브로 지원한다 —
AGP가 알아서 쪼갰고 Player Settings에는 아무 변경이 없다. minSdk 21 미만이었다면
Publishing Settings에서 켜야 했을 자리다.

빌드 자체: 성공, 287초, 오류 0, 경고 990(전부 기존 수준의 애셋/스크립트 경고).

## 코드 — 무엇이 어디에 생겼나

| 파일 | 역할 |
|---|---|
| `Scripts/Cloud/CloudScores.cs` | 초기화·로그인·write·read·오프라인 스위치. 게임 상태는 **읽기만** |
| `Scripts/Cloud/CloudSpikeOverlay.cs` | 기기 화면의 디버그 버튼. **파일 전체가 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`** |
| `Editor/OnikiriTestPanel.cs` | "클라우드" 절 — 통합 버튼 + 단계별(초기화/로그인/write/read) + 오프라인 토글 |
| `Editor/AndroidDeployBuilder.cs` | "폰으로 빌드 + 설치 (개발 빌드)" 메뉴 추가 |

단계별 버튼을 따로 둔 이유는 실패 지점이 네 군데로 뚜렷하게 갈리기 때문이다 —
의존성(Play 서비스), 로그인(콘솔의 익명 인증 설정), write(보안 규칙),
read(네트워크). 한 버튼만 있으면 어디서 멈췄는지 로그를 거슬러 올라가야 한다.

오버레이가 런타임 플래그가 아니라 `#if`로 묶인 이유: 화면에 떠 있는 디버그 버튼은
사용자가 누를 수 있는 버튼이고, 눌러서 되는 일이 서버 쓰기라면 그건 기능이다.
플래그는 뒤집히지만 컴파일에서 지운 코드는 뒤집히지 않는다.

### 왜 새 EditMode 테스트가 없는가

이 스텝이 증명하려는 것이 **네트워크 왕복**이라서다. EditMode 테스트로 쓸 수 있는
것은 결국 목(mock)이고, 목은 "내가 짠 목이 내 코드와 맞다"만 증명한다 — Play 서비스
버전도, 보안 규칙도, gRPC 채널도 목 뒤에 있다. 그래서 증거를 실기 logcat과 제3
클라이언트(REST) 조회에 뒀다. 기존 516개는 전부 통과했고, 이는 이 스텝이 게임
로직에 아무것도 하지 않았다는 쪽의 증거다.

## 막힌 점 / 남은 가시

- **App Check가 placeholder 토큰으로 돈다.** logcat에 `No AppCheckProvider
  installed` 경고가 매 요청 뜬다. 지금은 규칙이 전면 개방이라 무해하지만, 규칙을
  잠그는 순간(리더보드 스텝) Play Integrity 공급자를 등록해야 한다. **출시 전 필수.**
- **이름이 상수 `"Rang"`이다.** 닉네임 입력 UI가 서기 전까지 모든 문서가 같은
  이름을 갖는다. **출시 전 교체.**
- **보안 규칙이 테스트 모드**(전면 개방, 2026-09-30 만료)다. 만료되면 조용히
  전부 거부되므로 그 전에 4-B로 교체해야 한다. **출시 전 필수.**
- **오버레이는 출시 전 제거 대상**이다. 리더보드 UI가 서면 할 일이 없어진다.
- wifi를 토글하면 무선 디버깅이 함께 꺼진다(안드로이드 기본 동작). 기기에서
  오프라인을 재현하려면 wifi가 아니라 오버레이의 오프라인/온라인 버튼을 쓴다 —
  그래야 adb가 붙어 있는 채로 관측이 된다.

## 범위 밖 (다음 스텝)

리더보드: 도달층 갱신 시 자동 제출 · "높을 때만 갱신" · 상위 N 조회 UI ·
Cloud Functions 서버 재검증(치팅 방어, Blaze 필요) · 보안 규칙 4-B 잠금.
그 다음이 Remote Config 밸런스 이관.
