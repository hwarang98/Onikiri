# ONIKIRI 62단계 보고서 — Firebase App Check 배선과 크로스 저장 6단계

- **브랜치** `feature/firebase`
- **기준 커밋** `a46b960`
- **커밋·푸시** 없음 (작업 트리에만 존재)
- **세이브 버전** SaveData v21 유지 (변경 없음)
- **firestore.rules** 변경 없음 → 재배포 없음

> ## 이 문서를 읽는 법
>
> 두 회차가 한 문서에 있다.
>
> - **62단계** (§1~§9) — App Check 배선과 크로스 저장 6단계 실측. 여기서 결함 둘을
>   찾았고, 하나(urgent)는 그 자리에서 고쳤다.
> - **62.1단계** (§0.1, §11) — 62단계가 남긴 **P0 두 개**를 닫은 회차.
>
> **§5.B와 §9.3의 "결함"·"미완주" 서술은 62단계 당시의 관측이다.** 그 상태가 지금도
> 그렇다는 뜻이 아니다 - 현재 상태는 언제나 **§0.1의 표**가 정본이고, 각 절에는
> 62.1에서 무엇이 바뀌었는지 되짚는 줄을 달아 뒀다.

---

## 0.1 현재 상태 (62.1.2단계 종료 시점) — **정본**

| # | 완료 조건 | 상태 |
|---|-----------|------|
| 1 | Editor Debug Provider Verified | ✗ 환경 변수에 토큰이 없다 (§2.4) |
| 2 | Android Play Integrity Verified | ✔ **토큰 발급**(§3.6.1) + **Metrics 530/530 Verified**(§3.7) — 단 **개발 인증서 + `PLAY_RECOGNIZED` 해제** 조건이다(§3.2·§3.4) |
| 3 | 두 기기 충돌 및 양쪽 선택 실측 | ✔ 실기 2대로 **A~F 전부 통과** — **B는 62.1에서 고치고 실기 재검증까지 완료**(§11.10) |
| 4 | urgent 실측 | △ **네 트리거 실측 + 결함 수정**(§6) · 귀문 승리는 **조건 미달로 측정 불가**(§6.6) |
| 5 | 응답 유실 중복 revision 방지 실측 | ✔ **실기 완료**(§7.3) |
| 6 | 기존 테스트 전량 통과 | ✔ **EditMode 963/963 · PlayMode 44/44 · Emulator 47/47** (§13.6) |
| 7 | 로컬 저장·오프라인 플레이 무회귀 | ✔ (§9.2·§11.4) |
| 8 | Firestore enforcement 이후 정상 통과·미검증 거부 | ✗ **켜지 않았다** (§8) — 이번 단계 금지 항목 |

### 아직 남은 출시 차단 요소

1. **production 서명 인증서가 없다.** Firebase에 등록된 SHA-256은 이 기계의
   **디버그 keystore** 것이고, `androidUseCustomKeystore: 0`이다(§3.2).
2. **`PLAY_RECOGNIZED` 요구를 해제해 둔 상태다**(§3.4). 출시 전 되돌려야 한다 -
   켜진 채로 나가면 App Check가 **켜져 있으나 아무것도 막지 않는다.**
3. **Editor Debug Provider 미등록** — 콘솔에서 디버그 토큰을 발급해 환경 변수에
   넣어야 §8 승인 절차를 시작할 수 있다.
4. **Firestore enforcement 미활성**(§8).
> **62.1·62.1.1의 실기 검증 항목은 전부 닫혔다.** P0-1 부팅(§11.10) · 사슬 이동(§11.13) ·
> 오프라인 타임아웃(§11.16) · **저장 확정 순서(§12.6)** 넷 다 실기에서 확인했다.
> 위 1~4번은 **출시 준비 항목**이지 미완료 과제가 아니다.
>
> **62.1.1이 닫은 P1 둘**(§12): ① 클라우드 사슬이 로컬 저장보다 먼저 확정되던 것 ·
> ② App Check 토큰 실패 상태에서 정본 읽기가 직접 호출되던 우회 경로.
>
> **62.1.2가 닫은 것**(§13): 로컬 저장이 실패해도 클라우드 업로드가 나가던 것 —
> 디스크에 없는 스냅샷도, 그보다 낡은 `pendingData`도. **실기 미측정**(저장 실패를
> 억지로 만들지 않았다) — 그 갈래는 seam으로 EditMode 11개가 잰다.

**판정하지 않는 것**: 62단계 전체 완료, 크로스 저장 6단계 완료.

---

## 1. App Check 초기화 순서

### 1.1 단일 진입점

`Assets/_Project/Scripts/Cloud/FirebaseAppCheckBootstrap.cs` (신규)

```
FirebaseAppCheckBootstrap.EnsureConfigured()
        │
        ├─ 멱등: 두 번째 호출부터 아무 일도 하지 않는다
        ├─ 예외를 밖으로 내보내지 않는다 (게임은 계속 돈다)
        │
        └─ 플랫폼 갈래 → SetAppCheckProviderFactory(...)
                 ↓
        ─────── 여기까지가 Firebase를 만지기 **전** ───────
                 ↓
        FirebaseApp.CheckAndFixDependenciesAsync()
        FirebaseApp.DefaultInstance
        FirebaseAuth.GetAuth(app)
        FirebaseFirestore.GetInstance(app)
```

### 1.2 왜 순서가 전부인가

`SetAppCheckProviderFactory`는 **아직 만들어지지 않은** FirebaseApp에 대해서만
온전히 먹는다. 위 넷 중 하나라도 먼저 지나가면 그 핸들은 provider 없이 서고, 그
뒤에 factory를 붙여도 이미 만들어진 핸들은 토큰을 달지 않는다.

이 실패의 성질이 고약하다 — **enforcement를 켜기 전까지 증상이 전혀 없다.**
켜는 순간 전 요청이 거부되고, 그 거부는 로그에서 "보안 규칙이 틀렸다"와 구분되지
않는다. 그래서 이 순서를 주석이 아니라 **검사**로 못 박았다 (§9의 첫 두 검사).

### 1.3 두 경로가 모두 지난다

| 파일 | 부트스트랩 호출 위치 |
|------|---------------------|
| `CloudScores.cs` `InitializeCoreAsync` | `try` 블록 첫 줄 — `CheckAndFixDependenciesAsync` 바로 위 |
| `FirebaseRuntime.cs` `EnsureReadyAsync` | `try` 블록 첫 줄 — `CloudScores.InitializeAsync` 바로 위 |

`FirebaseRuntime`이 `CloudScores`를 부르므로 보통은 후자가 이기지만, **그 순서에
기대지 않았다.** 기대면 나중에 초기화 경로가 하나 더 생겼을 때 조용히 깨진다.
중복 호출은 멱등이므로 비용이 없다.

`OnlyTwoFilesEverCreateFirebaseHandles` 검사가 "Firebase 핸들을 만드는 파일은 이
둘뿐"을 못 박는다 — 세 번째 경로가 생기면 그 검사가 먼저 빨개진다.

---

## 2. 플랫폼별 provider 표와 debug token 관리

### 2.1 provider 표

| 플랫폼 | 컴파일 심볼 | provider | 근거 |
|--------|------------|----------|------|
| Unity Editor | `UNITY_EDITOR` | `DebugAppCheckProviderFactory` | 환경 변수의 debug token |
| Android 실기 | `UNITY_ANDROID && !UNITY_EDITOR` | `PlayIntegrityProviderFactory` | 기기·앱을 Play가 증명 |
| 그 밖 (iOS·데스크톱) | — | **붙이지 않음** | `Setup = UnsupportedPlatform` |

세 번째 줄이 중요하다. 미지원 플랫폼에서 **Firebase 부팅을 깨지 않는다** — provider만
안 붙이고 그대로 둔다. 그쪽은 enforcement를 켜기 전까지 지금처럼 돌고, 켜는 순간
거부된다는 사실이 `Setup`에 적혀 있다. (`AnUnsupportedPlatformStillBoots` 검사가
그 갈래에 `throw`가 없음을 확인한다.)

Android Development Build도 **실제 Play Integrity 대상**이다. 개발 빌드라고 Debug
provider로 빠지는 갈래는 없다 — `#if UNITY_EDITOR`가 먼저 걸리는 경우는 에디터뿐이고,
실기 빌드는 개발 빌드든 아니든 같은 `#elif UNITY_ANDROID` 갈래를 탄다.
`AndroidUsesPlayIntegrity` 검사가 그 갈래 안에 `DebugAppCheckProviderFactory`가
없음을 확인한다.

### 2.2 debug token 비밀 관리

| 규칙 | 어떻게 지켰는가 |
|------|----------------|
| 코드에 문자열 상수로 넣지 않는다 | 토큰 설정 호출에 문자열 리터럴이 붙은 자리가 추적 파일 어디에도 없다 (검사) |
| git 추적 파일에 저장하지 않는다 | 값 검사 — 환경 변수가 설정된 기계에서는 그 **값**을 전 파일에서 찾는다 (검사) |
| 씬·프리팹·ProjectSettings에 직렬화하지 않는다 | 같은 값 검사가 `.unity` · `.prefab` · `.asset`까지 훑는다 |
| 로그와 보고서에 원문을 출력하지 않는다 | 진단기가 `AppCheckToken`의 토큰 프로퍼티를 **읽지 않는다** (검사) |
| 환경 변수에서 읽는다 | `ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN` 하나뿐 |
| 값이 없으면 Editor 실서버 호출 차단 | `EditorServerCallsAllowed == false` → 세 게이트가 모두 막는다 |

토큰이 없을 때 **factory를 그래도 붙이지 않는** 것이 설계의 요점이다. 붙이면 SDK가
새 토큰을 스스로 만들어 콘솔 로그에 찍는데, 그 값은 등록돼 있지 않아 어차피 거부되고
대신 로그에 비밀이 하나 굴러다니게 된다.

`DebugTokenVariable` 상수 자체도 `#if UNITY_EDITOR` 안에 있다 — 출시 컴파일에는
debug token을 읽는 코드도, 그 자리를 가리키는 이름도 남지 않는다.

### 2.3 진단 가능한 것 / 아닌 것

진단기가 보여주는 것: **provider 종류 · 초기화 성공/실패 · token 요청 성공 여부 ·
만료 시각 · 오류 코드.**
보여주지 않는 것: **토큰 원문.** 그 문자열 하나면 누구나 이 프로젝트의 App Check를
통과하고, 새어 나간 뒤에는 콘솔에서 앱을 갈아야 회수된다.

### 2.4 현재 이 기계의 상태

```
ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN   = (설정되지 않음)
FirebaseAppCheckBootstrap.Setup          = MissingDebugToken
FirebaseAppCheckBootstrap.Provider       = None
EditorServerCallsAllowed                 = false
```

**그래서 §5·§8의 Editor 쪽 실측을 밟지 못했다.** 남은 절차는 §10에 적었다.

---

## 3. Android Play Integrity — **미완료, 그리고 그 이유**

SDK는 이미 있다. 중복 설치도, 버전 인상도 하지 않았다.

```
Assets/Firebase/Editor/AppCheckDependencies.xml
    com.google.firebase:firebase-appcheck:19.4.0
    com.google.firebase:firebase-appcheck-debug:19.4.0
    com.google.firebase:firebase-appcheck-playintegrity:19.4.0
    com.google.firebase:firebase-appcheck-unity:13.15.0
Assets/Firebase/Plugins/Firebase.AppCheck.dll   (Unity SDK 13.15.0)
```

### 3.1 확인한 것

| 항목 | 결과 | 확인 방법 |
|------|------|----------|
| Firebase 프로젝트 | `onikiri-9cc18` | `firebase apps:list` |
| Android 앱 등록 | ✔ `ONIKIRI Android` / `1:598938331304:android:852dd6f01371909135ba9d` | 같음 |
| 등록된 SHA-1 | `2086E9D2 53382AD6 1007AE5A 171B71A3 025B8C86` | `firebase apps:android:sha:list` |
| 등록된 SHA-256 | `02D1401C F68B08D7 49B44298 3BEDFB18 B54BEE05 41813CE9 065341CE 9E95E43B` | 같음 |

### 3.2 ★ 그 인증서는 **디버그 keystore**다

로컬 `~/.android/debug.keystore`의 지문이 위 두 값과 **정확히 일치한다**
(`keytool -list -v`). 그리고 `ProjectSettings.asset`에는

```
androidUseCustomKeystore: 0
AndroidKeystoreName:      (비어 있음)
AndroidKeyaliasName:      (비어 있음)
```

즉 **이 프로젝트는 아직 production 서명을 갖고 있지 않다.** Unity가 만드는 APK는
전부 디버그 키로 서명된다.

### 3.3 콘솔 실측 (2026-08-24)

**★ 이 프로젝트의 App Check는 지금까지 한 번도 설정된 적이 없었다.** 콘솔에
들어가니 온보딩 화면("시작하기")이 떠 있었고, `ONIKIRI Android`의 증명 제공업체는
`–`, 상태는 `등록되지 않음`이었다.

그 말은 **57~61단계 내내 Firestore 요청이 App Check 토큰을 아예 달지 않고
나가고 있었다**는 뜻이다. enforcement를 켰다면 전부 거부됐을 상태다 — §1.2가
말한 "증상 없는 실패"의 실물이 여기 있었다.

| 확인 대상 | 상태 |
|-----------|------|
| ① Firebase App Check에 Android 앱 등록 | ✔ **이번에 등록**(Play Integrity) — 그 전까지 `등록되지 않음` |
| ② 사용 중인 서명 인증서 SHA-256 등록 | **개발 인증서만** (§3.2) — 콘솔이 그 값을 자동 채웠다 |
| ③ Play Console ↔ 같은 Google Cloud 프로젝트 연결 | ✔ **실기 로그로 확인** (아래) |
| ④ Play 외부 설치 시 advanced 설정 | ✔ `PLAY_RECOGNIZED 필요` 해제 (§3.4) |
| ⑤ PLAY_RECOGNIZED / LICENSED 정책 정합 | **판정 불가** — (A) 경로라 둘 다 해제 상태다 |
| ⑥ Galaxy Note 20에서 토큰 발급 성공 | **진행 중** — Play Integrity 요청까지 확인, 발급 확정은 §3.6 |
| ⑦ Firestore 요청이 Metrics에서 Verified | **미확정** — 측정항목이 아직 "데이터 없음"(§3.7) |

③의 근거는 실기 logcat이다. Play Integrity가 요청한 `cloudProjectNumber`가
Firebase 앱 ID(`1:598938331304:android:…`)의 프로젝트 번호와 **정확히 일치**한다 —
Play Integrity API가 이 Firebase 프로젝트를 향해 돌고 있다는 뜻이고, 두 콘솔이
같은 프로젝트에 묶여 있지 않으면 나올 수 없는 값이다.

```
I PlayCore: IntegrityService : requestIntegrityToken(
              nonce=AaJqjyuC…, cloudProjectNumber=598938331304, network=null)
I Finsky  : Integrity key attestation record generated successfully.
I Finsky  : requestIntegrityToken() finished for com.studio202.onikiri.
I PlayCore: OnRequestIntegrityTokenCallback : onRequestIntegrityToken
```

⚠️ 같은 구간에 경고 하나가 섞여 있다:

```
E Finsky : com.google.android.finsky.integrityservice.IntegrityException:
           User needs to (re)enter credentials.
```

요청은 그 뒤 `finished`로 끝나고 콜백도 왔지만, **이 로그만으로는 토큰이 정상
발급됐는지 빈손으로 끝났는지 갈리지 않는다.** 폰의 Play 스토어 계정 재인증이
필요한 상태일 수 있다. 그래서 §3.6의 클라이언트 진단이 필요하다.

### 3.6 초기화 순서 — **실기에서 증명됨**

logcat이 호출 스택째 남겼다. 부트스트랩이 `InitializeCoreAsync` **안에서**,
`초기화 완료`(= CheckAndFixDependencies + DefaultInstance + GetAuth + GetInstance가
모두 끝난 시점)보다 **1초 앞서** 돌았다.

```
13:15:43.491  [AppCheck] provider = Play Integrity
              Onikiri.Cloud.FirebaseAppCheckBootstrap:EnsureConfigured()
              Onikiri.Cloud.CloudScores:InitializeCoreAsync()      ← 첫 줄
13:15:44.475  [CloudScores] 초기화 완료. projectId = onikiri-9cc18
13:15:44.477  [CloudScores] 기존 로그인 재사용. uid = A9aMgx…
13:16:02.042  [CloudScores] write 성공. scores/A9aMgx… maxStage = 43
```

Firestore 오류·거부 0건. 다만 **write 성공은 아직 증거가 아니다** — enforcement가
꺼져 있어 토큰이 없어도 통과한다(§8).

### 3.6.1 ★★ 토큰 발급 성공 — Galaxy Note 20, 2026-08-24 13:24 KST

실기 오버레이의 `App Check 진단`(§3.8)이 `GetAppCheckTokenAsync(forceRefresh: true)`를
때린 결과다. **캐시가 아니라 새로 받은 토큰이다.**

```
---- App Check 진단 ----
uid       = A9aMgx…(28자)
device    = a44069…(32자)
session   = d99ec8…(32자)
base rev  = 24
pending   = 없음
state hash= 7cd7895f
maxStage  = 43 / gems = 1052 / evolutionTier = 5
saveVer   = v21
상태      = LocalOnly · 기기에 저장됨 · 연결되면 동기화
provider = PlayIntegrity (계획 PlayIntegrity) / 배선 = Configured
    Play Integrity provider 배선됨

토큰 성공 (만료 2026-08-24 05:24:46Z, provider PlayIntegrity)
정본 읽기 -> Found
    서버 revision = 24 (base 23)
    state hash    = 06edac58
    요약          = 43층 · 보석 1052 · 전직 5
    세션/기기     = 8b0bc6…(32자) / a44069…(32자)
```

읽는 법 셋:

- **만료가 정확히 1시간 뒤**(13:24:46 KST = 04:24:46Z → 05:24:46Z)다. 콘솔에
  설정된 토큰 TTL(1시간)과 일치한다 — 이 토큰이 **우리가 방금 등록한 그 설정**에서
  나왔다는 뜻이다.
- **`Source.Server` 정본 읽기가 Found**로 돌아왔다. Firestore 왕복이 토큰을 달고
  나갔다 왔다.
- §3.3의 `IntegrityException: User needs to (re)enter credentials`는 **치명적이지
  않았다.** 그 경고가 있어도 토큰은 나온다 — Play 쪽 내부 재시도로 보인다.

따라서 §3.3의 ⑥은 **✔ 확인**이다. 사이드로드 개발 인증서 조건에서 Play Integrity
provider가 실제로 동작한다.

로컬 state hash(`7cd7895f`)와 서버(`06edac58`)가 다른 것은 정상이다 - 진단 직전
방치 보상(+349.6B 골드)을 받아 로컬이 앞서 있고, 상태가 `LocalOnly`(= 올릴 것이
있음)로 그것을 정확히 적고 있다.

### 3.7 Metrics — ★ **530/530 Verified (100%)** (2026-08-27 확인)

8/24에는 "데이터 없음"이었다(App Check 측정항목은 최대 24시간 지연). 3일 뒤 다시 보니
채워져 있었다. 콘솔 → App Check → API → Cloud Firestore, **지난 7일(8/20~8/28)**:

| 측정항목 | 값 |
|---|---|
| **확인된 요청** | **100%** — 총 530 중 **530** |
| 확인되지 않음: 오래된 클라이언트 요청 | 총 530 중 **0** |
| 확인되지 않음: 알 수 없는 출처 요청 | 총 530 중 **0** |
| Cloud Firestore 적용 상태 | 🕐 **모니터링** (enforcement OFF — 의도대로) |

그래프는 8/24와 8/27 두 봉우리를 그린다 - 실측한 두 날이다. Authentication API도
100% / 0%로 같이 찍혔다(이번 스텝에서 enforcement 대상은 아니다).

**§3.3 ⑦이 닫힌다.** 클라이언트가 토큰을 받는 것(§3.6.1)과 서버가 그 토큰을 인정하는
것은 별개의 사실이고, 이 표가 후자다.

> 다만 이 100%는 **`PLAY_RECOGNIZED` 요구를 해제한 상태**에서 나온 값이다(§3.4).
> 그 설정을 되돌리면 사이드로드 개발 빌드는 여기서 빠진다 - 출시 인증서로 다시
> 재야 하는 이유가 그것이다.

### 3.7.1 (이전 관측) 8/24 시점 — 데이터 없음

콘솔 App Check → API → Cloud Firestore, 지난 7일(8/17~8/25):

| 측정항목 | 값 |
|---|---|
| 확인된 요청 | 총 0 중 0 |
| 확인되지 않음: 오래된 클라이언트 | 총 0 중 0 |
| 확인되지 않음: 알 수 없는 출처 | 총 0 중 0 |
| Cloud Firestore 적용 상태 | 🔒 **적용되지 않음** (enforcement OFF — 의도대로) |

App Check 측정항목은 반영이 느리다(최대 24시간). 그래서 §3의 ⑦은 **지금 판정하지
않는다.** 더 빠른 대체 증거는 클라이언트에서 토큰 발급을 직접 요청하는 것이고,
그것이 §4.2의 실기 진단이다.

### 3.8 실기 진단 창구를 뒤늦게 붙였다 (설계 빚)

진단기를 **에디터 테스트 패널에만** 붙여 놓은 것이 실기 실측을 시작하자마자
드러났다 — Play Integrity가 도는 곳은 실기뿐인데, 실기에서 진단을 부를 방법이
없었다. `CloudSpikeOverlay`(이미 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`인 실기
IMGUI 오버레이)에 같은 절을 붙였다: `App Check 진단` 버튼 + `응답유실 1회` 토글.

이것은 53단계에 스파이크를 오버레이로 옮긴 것과 **정확히 같은 이유**다 -
에디터의 Firestore는 데스크톱 네이티브이고 기기의 것은 Play 서비스 위의 안드로이드
SDK다. 두 경로는 실패 지점이 서로 다르고, 한쪽 통과가 다른 쪽의 증거가 못 된다.

### 3.4 이번 검증 경로 — **(A) 사이드로드 허용, 개발 인증서** (2026-08-24 결정)

디버그 서명 APK로 오늘 §3·§6·§7을 밟기 위해 Firebase 콘솔 App Check 고급 설정의
**"인식되지 않는 출처의 앱 허용"을 켜기로 했다.** 이것으로 얻는 것과 잃는 것을
분명히 적는다.

- **얻는 것**: Play Console·릴리스 keystore 없이 Play Integrity provider 자체가
  도는지, 토큰이 나오는지, Firestore 요청이 Verified로 찍히는지를 오늘 관측할 수 있다.
- **잃는 것**: 이 설정이 켜져 있는 동안 App Check는 **Play가 배포하지 않은 설치본도
  통과시킨다.** 즉 App Check가 막으려던 것(변조된 APK·에뮬레이터·재패키징)을 그동안은
  막지 않는다.

> ## ⚠️ 출시 전 해제 — 이 스텝이 남기는 임시 조치
>
> **Firebase 콘솔 → App Check → `ONIKIRI Android` → Play Integrity → 고급 설정
> → "인식되지 않는 출처의 앱 허용" = 끄기.**
>
> 이 항목이 켜진 채로 출시되면 App Check는 **켜져 있으나 아무것도 막지 않는
> 상태**가 된다 — enforcement까지 켜 두면 "보호되고 있다"고 읽히기 때문에 가장
> 나쁜 모양이다. 해제는 §3.5의 production 인증서 작업과 **같은 날 함께** 한다.

따라서 이번 실측이 성공해도 보고서에 적는 것은 **"개발 인증서·사이드로드 허용
상태에서 Play Integrity provider 동작 확인"**까지다. `PLAY_RECOGNIZED` /
`LICENSED` 정합은 (B) 경로에서만 증명된다.

### 3.5 production에 남은 일

1. **릴리스 keystore를 만들고** Unity `Publishing Settings`에 물린다 — 또는 Play App
   Signing을 켜고 Google이 발급한 **앱 서명 키**의 SHA-256을 받는다.
2. 그 SHA-256을 Firebase Android 앱에 **추가** 등록한다 (디버그 것은 개발용으로 남긴다).
3. Google Cloud 콘솔에서 **Play Integrity API**를 켜고 Play Console의 앱과 같은
   프로젝트에 연결한다.
4. Firebase 콘솔 → App Check → Android 앱 → **Play Integrity** provider 등록.
5. 내부 테스트 트랙으로 올려 **Play가 배포한 설치본**에서 토큰이 나오는지 본다.
   사이드로드 APK로 통과시키려면 App Check advanced 설정을 풀어야 하는데,
   **그 설정을 출시용으로 남기면 안 된다.**

> 사이드로드 개발 빌드가 Play Integrity에서 떨어지면 그것은 **코드 실패가 아니다.**
> 그 경우에도 production 코드에 Debug Provider를 넣지 않는다 — 테스트를 통과시키려고
> 넣는 순간 이 스텝이 증명하려던 것이 통째로 사라진다.

---

## 4. App Check 진단 도구

`Assets/_Project/Scripts/Cloud/AppCheckDiagnostics.cs` (신규, **파일 전체가**
`#if UNITY_EDITOR || DEVELOPMENT_BUILD`)

| 함수 | 하는 일 |
|------|--------|
| `ProviderLine()` | provider 종류 + 배선 판정 (SDK 무접촉) |
| `RefreshTokenAsync()` | `GetAppCheckTokenAsync(forceRefresh: true)` → 성공 여부·만료 시각·오류 코드 |
| `ReadCanonicalAsync()` | `Source.Server` 정본 읽기 → revision · state hash 앞 8자 · 요약 |
| `Snapshot()` | 두 기기 실측 기록표 한 줄 (서버 왕복 없음) |
| `RunAsync()` | provider → 토큰 → 정본을 한 번에 |

`forceRefresh: true`인 이유: 캐시된 토큰이 한 시간을 산다. 콘솔에서 디버그 토큰을
지우거나 Play Integrity 설정을 고친 직후에도 캐시가 성공을 돌려주면 무엇을 고쳤는지
알 수 없다.

### 4.1 테스트 패널

`OnikiriTestPanel` → **"크로스 저장 (6단계 - App Check)"** 절 추가.
스크립트 기반 확장만 썼다 — **Main.unity를 재생성하지 않았다.**

- provider / 배선 상태 표시, 막혔으면 원인을 경고 상자로
- 버튼: `실측 기록표` · `토큰 강제 갱신` · `정본 읽기(Source.Server)` · `전체 진단`
- 토글: **"다음 커밋 한 번을 '응답 유실'로 만든다"** (§7)
- 토큰 원문은 이 창에도 뜨지 않는다

배선이 안 된 상태에서는 토큰·정본 버튼이 비활성이다 — 누를 수 있게 두면 거부되는
요청이 운영 프로젝트로 나간다.

---

## 5. 두 기기 실측 — **다섯 통과 · 하나 결함** (2026-08-27)

기기 B가 Unity Editor가 아니라 **두 번째 실기**가 됐다. 원래 계획보다 정직한 구성이다 -
"실제 클라이언트 둘의 세션 경합과 revision 경쟁"을 대역 없이 그대로 잰다.

| | 기기 A | 기기 B |
|---|--------|--------|
| 모델 | Galaxy Note 20 (`SM-N981N`) | Galaxy Z Flip 3 (`SM-F711N`) |
| 주소 | `192.168.0.26` | `192.168.0.15` |
| provider | Play Integrity · Configured | Play Integrity · Configured |
| deviceId | `a44069…` | `0f1346…` |

**두 기종에서 각각 Play Integrity 토큰이 발급됐다** — §3.6.1이 한 기기의 우연이
아니었음을 이 줄이 받친다.

### 5.0 시작 기록표

| | 기기 A | 기기 B |
|---|--------|--------|
| uid | `A9aMgx…(28)` | `A9aMgx…(28)` ← **수렴** |
| base rev | 88 | 89 |
| pending | 없음 | 없음 |
| syncSha | `f6db6cc0` | `670a46ad` |
| save | v21 · 43층 · Lv61 · 보석 647 · 전직 5 | v21 · 43층 · Lv58 · 보석 647 · 전직 5 |

### 5.A  A 진행 → B 로그인 ✔

B는 신규 설치 → 익명 uid `jj09eU…` → `구글로 로그인`(사용자가 계정 선택).

```
13:55:01.162  [AccountLink] 연동 실패 -> AlreadyInUse
13:55:01.163  [AccountLink] 연동 실패 = AlreadyInUse -> 계획 Recover
13:55:02.406  [AccountLink] ★ 복구 성공 - uid 교체 jj09eU… -> A9aMgx…
13:55:02.524  [CloudSave] ★ 복구 세이브 판정 = AskTheHuman
              (서버 Found / 로컬 920d84d4 / 서버지문 f6db6cc0)
13:55:02.626  [AccountLink] 병합 - 로컬 1 / 버린 문서 1 / 복구된 문서 43 -> 43
13:55:02.626  [AccountLink] 계정을 복구했습니다 - 이어갈 기록을 선택하세요
```

**조용한 덮어쓰기 없음.** B에 로컬 진행(1층)이 있었으므로 자동 채택 대신 사람에게
물었다(설계 §8.2 ⑤). 랭킹은 max로 43 병합, 세이브는 선택 대기 - "랭킹 max ≠ 세이브
선택" 경계가 실기에서 그대로 지켜졌다. 이름도 복구됐다(`랑무사`).

충돌 화면은 **화면을 가로채지 않는다** - 설정에 `기록 선택 필요 / [기록 선택]` 줄로
올라온다(61단계 계약).

### 5.B  B 진행 → A 재실행 ✗ — **부팅이 서버를 보지 않는다** _(62단계 당시)_

> **⚠️ 이 아래는 62단계 당시의 관측이자 원인 분석이다.** 62.1에서 **코드를
> 고쳤다**(§11.1~§11.5) - 사이드카가 있는 기존 사용자는 인증 준비를 제한 시간
> 안에서 기다린다. 다만 **실기 재검증은 아직 못 했다**(§11.9). 현재 상태는
> §0.1 표가 정본이다.

기기 A를 다시 띄웠다. 기대: A가 B의 새 revision을 내려받는다. 실제:

```
14:01:13.023  [CloudSave] 부팅 선택: 서버 확인 없음 - 로컬 사용
14:01:13.098  [Onikiri] Boot apply #1 - 서버 확인 없음 - 로컬 사용 (방치 보상 1회)
14:01:14.082  [CloudScores] 기존 로그인 재사용. uid = A9aMgx…
```

**부팅 판정이 로그인보다 1초 앞선다.** `ShouldCheckServer(uid)`는 uid가 비어 있으면
false를 돌려주므로, 실기에서는 **부팅 서버 확인이 사실상 한 번도 돌지 않는다.**

이것은 59단계가 알면서 남긴 자리다 - 부팅을 코루틴으로 미루면 귀문 PlayMode 검사
다섯이 깨져서(그 검사들이 첫 프레임에 GameSession을 파괴한다) `ChooseLocally`로
같은 프레임에 끝내기로 했고, 그 대가가 이것이다. 당시 메모가 "서버 확인을 켜는
단계에서 이 취약점을 다시 봐야 한다"고 적어 뒀다. **지금이 그 단계다.**

증상은 조용하다: A는 갈라진 사실을 부팅이 아니라 **다음 커밋에서** 알게 된다(§5.C).
데이터가 상하지는 않지만, 설계가 약속한 "재실행하면 최신을 받는다"는 성립하지 않는다.

> **판정: §5-B 미충족.** 고치려면 부팅 서버 확인을 로그인 완료 뒤로 미루거나,
> 로그인 완료 시점에 한 번 더 정본을 확인하는 창구가 필요하다. 이번 스텝에서는
> 원인만 확정하고 손대지 않았다 - 귀문 PlayMode 다섯과 얽힌 자리라 별도 스텝이 맞다.

### 5.C  A 오프라인 진행 + B 온라인 진행 → A에서 Conflict ✔

B가 89→91까지 올리는 동안 A는 base 88에 로컬 진행이 쌓였다. B를 HOME으로 보내
세션을 놓게 하자:

```
14:07:46.059  W [CloudSave] 서버와 갈라졌습니다 (서버 rev 91). 기록 선택이 필요합니다.
```

**자동 추천·자동 병합 없음.** 선택 화면이 세 갈래를 그대로 보여준다:

| | 내용 |
|---|---|
| 현재 기기 기록 | 최고 43층 · Lv.61 · 데몬사무라이 · 보석 647 |
| 클라우드 기록 | 최고 43층 · Lv.58 · 데몬사무라이 · 보석 647 (서버 저장 8월 27일 14:04) |
| | 나중에 결정 |

### 5.D  충돌에서 클라우드 선택 (기기 B) ✔

```
13:59:42.315  클라우드 적용 전 로컬 백업 (3벌 순환): …onikiri_save.json.precloud.1
13:59:42.316  충돌 해결: 클라우드 기록 사용 (rev 88)
13:59:42.620  부팅 선택: 같은 기록 - 로컬 사용 (서버 rev 88)
13:59:42.628  [Onikiri] Boot apply #1 - … (방치 보상 1회)
13:59:43.168  주기 동기화 완료 (rev 89)
```

| 요구 | 결과 |
|------|------|
| 클라우드 정본 적용 | ✔ 43층·Lv58·보석 647이 B로 |
| 로컬 이전 기록 precloud 백업 | ✔ `onikiri_save.json.precloud.1` |
| Apply 1회 | ✔ `Boot apply #1` |
| 방치 보상 1회 | ✔ |
| 씬 재로드 후 InSync | ✔ `같은 기록 - 로컬 사용` |

sidecar도 갈아탔다: `jj09eU…/rev 1/device 7c6430…` → `A9aMgx…/rev 89/device 0f1346…`
(새 uid의 사슬이므로 deviceId를 새로 발급하는 것이 맞다).

### 5.E  충돌에서 현재 기기 선택 (기기 A) ✔

```
BEFORE  base rev=88  pending=b51f82…      ← 충돌로 실패한 커밋의 pending이 남아 있었다
14:10:43.278  충돌 해결: 현재 기기 기록 사용 (서버 rev 91 위에 올린다)
14:10:43.592  [Onikiri] Boot apply #1 - 로컬이 정본 - 업로드 대기 (방치 보상 1회)
14:10:44.089  주기 동기화 완료 (rev 92)
AFTER   base rev=92  pending=없음
```

| 요구 | 결과 |
|------|------|
| 현재 서버 정본이 `playerSaveBackups`에 보존 | ✔ 아래 |
| 현재 기기 기록이 server head 위에 revision+1 | ✔ 91 → **92** |
| stale revision 덮어쓰기 없음 | ✔ base 88이 아니라 **head 91 위**에 올렸다 |

백업 문서를 콘솔에서 직접 읽어 확인했다 — **B의 정본이 그대로 남아 있다**:

```
playerSaveBackups/A9aMgx…
  revision:     91                                   ← A가 덮어쓴 head
  baseRevision: 90
  deviceId:     "0f1346073baf42dd91b199be4f3be2b8"   ← 기기 B
  sessionId:    "5be4b7c96c4a490dbc04ca06491ea058"   ← 기기 B
  stateSha256:  "670a46adcf628ebd…"                  ← B의 syncSha와 일치
  summary.characterLevel: 58                         ← B의 Lv
  saveVersion:  21
```

### 5.F  활성 세션 충돌 ✔

A가 켜져 있는 동안 B가 세션을 쥐고 있었고, A의 커밋은 **조용히 막혔다**. 진단이
이유를 정확히 적었다:

```
상태          = LocalOnly · 다른 기기에서 플레이 중
A: session 9f80ef… / device a44069… / base rev 88
서버 revision = 91 (base 90)
서버 세션/기기 = 5be4b7… / 0f1346…      ← 기기 B
```

B를 HOME으로 보내 세션을 놓자 A가 곧바로 진행했다(§5.C). 180초 만료 인수도 §6.7에서
별도로 관측했다.

> ⚠️ Busy 갈래는 **로그를 남기지 않는다**(`CloudSaveSync.CommitAsync`의 `HoldsWrite`
> 분기). 실측에서 "커밋이 왜 안 나가는지" 알아내는 데 진단 한 번이 더 필요했다.
> 상태 문장에는 정확히 적히므로 사용자에게는 문제가 없지만, 다음에 이 경로를
> 디버깅할 사람을 위해 로그 한 줄이 있으면 좋다.

### 5.G 종합

| 시나리오 | 결과 |
|----------|------|
| A. A 진행 → B 로그인 | ✔ |
| B. B 진행 → A 재실행 | ✗ **부팅이 서버를 안 본다** (원인 확정, 미수정) |
| C. 양쪽 진행 → A에서 Conflict | ✔ |
| D. 클라우드 선택 | ✔ |
| E. 현재 기기 선택 | ✔ |
| F. 활성 세션 Busy · 인수 | ✔ |

**필드별 병합은 한 번도 일어나지 않았다.** 모든 해결이 "한 벌 선택"이었고, 버려진
쪽은 precloud(로컬) 또는 playerSaveBackups(서버)에 남았다.

---

## 5-A. (구) 두 기기 실측 계획

기기 A(Galaxy Note 20)와 기기 B(Unity Editor)를 동시에 잡을 수 없어 A~F 여섯
시나리오를 하나도 밟지 못했다. **결과를 추정해서 적지 않는다.**

| 시나리오 | 상태 |
|----------|------|
| A. A 진행 → B 로그인 (Apply 1회 · 방치 보상 1회) | 미실측 |
| B. B 진행 → A 재실행 (조용한 덮어쓰기·필드 병합 없음) | 미실측 |
| C. A 오프라인 + B 온라인 → A에서 Conflict, 자동 병합 없음 | 미실측 |
| D. 충돌 → 클라우드 선택 (precloud 백업 · Apply 1회 · 재로드 후 InSync) | 미실측 |
| E. 충돌 → 현재 기기 선택 (백업 보존 · head 위 revision+1) | 미실측 |
| F. 활성 세션 충돌 (Busy → release 또는 180초 만료 뒤 인수) | 미실측 |

실측 준비물은 섰다. 각 실측 전에 기록할 한 줄을 `AppCheckDiagnostics.Snapshot()`이
그대로 만든다 — uid·deviceId·sessionId 마스킹(앞 6자 + 길이), local baseRevision,
서버 revision, state hash 앞 8자, maxStage, gems, evolutionTier, saveVersion, 상태 문장.
두 기기가 같은 형식으로 찍으므로 두 줄을 나란히 놓고 비교할 수 있다.

관련 계약 중 **Firebase 없이 잴 수 있는 것**은 자동 검사가 이미 덮고 있다:
D·E의 "Apply 1회 · 방치 보상 1회"는 PlayMode `CloudConflictPlayTests`가,
C의 "충돌에서 자동 쓰기 없음"은 `AConflictStopsAllCloudWrites`가,
F의 세션 인수·만료는 Emulator 규칙 검사 47개가 잰다. 남은 것은 **두 기기가 실제로
같은 문서를 두고 부딪히는 그 순간**이고, 그것만은 대역이 없다.

---

## 6. urgent 동기화 실측 — ★ **결함 하나를 잡아 고쳤다**

### 6.1 실측이 드러낸 것: urgent이 **지불 전 스냅샷**을 올리고 있었다

Galaxy Note 20, 2026-08-24. 요도 화면의 `파편 조달`(보석 30)을 눌러 재는 방식이다 -
`GemWallet.GemsChanged` 감소가 urgent 트리거이므로 §6이 요구하는 경로를 그대로 지난다.

| # | 로컬 소비 | 커밋 | 서버 payload 보석 | 판정 |
|---|---|------|------------------|------|
| 1 | 1022 → 992 | rev 44 | **1022** | ✗ 지불 전 |
| 2 | 992 → 962 | rev 45 | **992** | ✗ 지불 전 |

**2/2 재현.** revision은 정확히 +1이었고 유예도 2.50초로 맞았지만, **올라간 한 벌에
방금 낸 지불이 없었다.**

### 6.2 원인 — 저장과 커밋이 서로를 기다리지 않았다

```
GameSession.Save()  →  자동 저장 30초 · pause · focus 상실 · 종료  ← 여기서만 돈다
                       (보석 소비·뽑기에는 저장이 없다)
        ↓ NoteSaved
CloudSaveSync.pendingData   ← urgent이 올리는 것은 이것이다
```

urgent 유예는 2초, 자동 저장 주기는 30초다. 둘이 겹칠 확률은 약 7%. 나머지 93%는
**최대 30초 전 스냅샷**이 올라간다.

그리고 고치는 과정에서 **더 나쁜 갈래**가 하나 더 드러났다:

> `ShouldCommit`의 첫 줄이 `dirtySeconds < 0 → false`("올릴 것이 없다")다. 성공한
> 커밋은 `pendingData = null · dirtySince = -1`로 끝난다. 그래서 **커밋 직후에 낸
> 지불은 자동 저장 30초가 올 때까지 urgent이 아무 일도 못 한다** - 실기에서
> `urgent 동기화 예약` 한 줄만 찍히고 커밋이 영영 안 나가는 것을 그대로 관측했다.

**재화 복제는 없었다.** 전체 스냅샷을 올리므로 지불과 상품이 **함께** 빠진다 -
설계 §3의 "필드별 병합 금지"가 여기서 방벽 노릇을 했다. 그래도 urgent이 존재하는
이유("잃으면 지불이 사라지는 창을 닫는다")는 지켜지지 않고 있었다.

### 6.3 수정 — urgent이 자기 스냅샷을 스스로 세운다

`CloudSaveSync.SaveRequested`(Action) 창구를 두고 `GameSession`이 자기 `Save`를
건다. `Tick()`에서 **유예가 찬 urgent**은 커밋 전에 그 창구를 부른다.

순서가 계약이다 — 저장 요청은 `pendingData == null` 가드보다 **앞**에 있어야 한다.
뒤에 두면 §6.2의 두 번째 갈래(커밋 직후 지불이 영영 안 올라감)가 그대로 남는다.
실제로 처음엔 뒤에 뒀다가 실기에서 커밋이 안 나가는 것을 보고 옮겼다.

주기 동기화에는 부르지 않는다 - 이미 저장된 것을 올리는 일이라 120초마다 디스크
쓰기를 하나 더 붙일 이유가 없다.

### 6.4 수정 후 재실측

| 항목 | 값 |
|------|-----|
| BEFORE | `baseRevision=51` · pending 없음 · 로컬 보석 **902** |
| 16:43:09.550 | `[CloudSave] urgent 동기화 예약: 보석 소비 (902 -> 872)` |
| 16:43:12.133 | `[CloudSave] urgent 동기화 완료 (rev 52)` ← **2.583초** |
| AFTER | `baseRevision=52` · pending 없음 · 로컬 보석 **872** |
| 서버 rev 52 요약 | 43층 · **보석 872** · 전직 5 |
| 상태 | `InSync · 클라우드 저장 완료` |

§6의 여섯 항목 대조 (보석 소비 경로):

| # | 요구 | 결과 |
|---|------|------|
| ① | 로컬 저장이 먼저 완료 | ✔ 수정으로 **보장**된다 (전에는 우연에 맡겼다) |
| ② | urgent 예약 로그 | ✔ |
| ③ | 약 2초 후 클라우드 트랜잭션 | ✔ 2.583초 |
| ④ | revision 정확히 +1 | ✔ 51 → 52 |
| ⑤ | 재화가 같은 스냅샷에 존재 | ✔ 로컬 872 = 서버 872 |
| ⑥ | pending 저장 성공 전 서버 write 없음 | ✔ 58단계 `MayStartServerWrite` 무변, pending 정리됨 |

로그 문구도 갈랐다: 전에는 urgent도 `주기 동기화 완료`로 찍혀 실측에서 둘을
구분할 수 없었다. 이제 `urgent 동기화 완료`다.

### 6.5 트리거별 실측표 (수정 후, 2026-08-24)

| 트리거 | 로그 | 유예 | revision | 로컬 → 서버 대조 |
|--------|------|------|----------|------------------|
| 보석 소비 (파편 조달 30) | `urgent 동기화 예약: 보석 소비 (902 -> 872)` | 2.583초 | 51 → **52** | 보석 872 = 872 ✔ |
| 일반 뽑기 (무료 단연) | `urgent 동기화 예약: 요도 뽑기` | 2.504초 | 53 → **54** | `gachaTotalPulls` 1 → 2 ✔ |
| **10연 (보석 225)** | 예약 **2건**, 커밋 **1건** (아래) | 2.479초 | 55 → **56** | 보석 647 = 647 · pulls 2 → **12** ✔ |
| 스킬 뽑기 (무료 오의) | `urgent 동기화 예약: 오의 뽑기` | 2.498초 | 57 → **58** | `skillGachaTotalPulls` 10 → 11 ✔ |

넷 모두 **revision 정확히 +1**, pending 없음(= `MarkSynced` 성공), 유예 2.5초 안팎.

#### ★★ 10연 — 한 프레임의 이벤트 둘이 커밋 **하나**로 모였다

```
16:48:39.660  [CloudSave] urgent 동기화 예약: 보석 소비 (872 -> 647)
16:48:39.662  [CloudSave] urgent 동기화 예약: 요도 뽑기      ← 2ms 뒤, 같은 프레임
16:48:42.139  [CloudSave] urgent 동기화 완료 (rev 56)        ← 커밋은 하나뿐
```

`RequestUrgent`가 시계를 처음 한 번만 거는 계약(`urgentSince < 0f`일 때만 대입)이
실기에서 그대로 확인됐다. revision 폭증 없음 - 55 → 56, 딱 하나.

그리고 **지불(225)과 상품(10회)이 같은 스냅샷에 있다**: 서버 rev 56의 요약이
`보석 647`이고 로컬도 647이다. §6 ⑤가 이 줄로 닫힌다.

### 6.6 귀문 승리/전직 — **측정 불가 (사유 확정)**

`EvolutionSystem.Evolved`를 실기에서 밟으려 했으나 **조건이 닿지 않는다.** 캐릭터 →
전직 화면이 그대로 말한다:

```
데몬사무라이  →  진 데몬사무라이
   공격 ×1.75      공격 → ×2.09
   체력 ×1.61      체력 → ×1.77

        [ 경지 ]          ← 비활성
      귀문 st150 돌파
```

계정은 **43층**이고 다음 전직은 **귀문 st150 돌파**를 요구한다. st150까지 진행하는
것은 이 실측 회차에서 할 수 있는 일이 아니다.

**대역으로 무엇이 남는가**: 이 트리거는 `EvolutionSystem.Evolved` → `RequestUrgent`
한 줄이고, 그 뒤는 §6.5에서 네 번 실측한 경로와 **완전히 같다**(같은 `urgentSince`,
같은 `Tick`, 같은 커밋). EditMode `ManyUrgentEventsConvergeOnOneCommit`이 규칙 수준을
잡고 있다. 그래도 **밟은 것은 아니므로 통과로 적지 않는다.**

st150에 도달한 계정이 생기면 그때 한 번 밟으면 된다 - 볼 것은 §6.5 표의 다섯 번째
줄 하나뿐이다.

### 6.7 곁가지로 확인된 것 — 세션 인수 (§5-F의 일부)

측정 중 앱을 `force-stop`으로 죽이고 다시 깔았더니, 다음 실행의 커밋이 조용히
막혔다. 진단이 이유를 정확히 적었다:

```
상태 = LocalOnly · 다른 기기에서 플레이 중
```

Firestore `playerSaveSessions/{uid}`를 콘솔에서 직접 읽어 확인했다 - 죽은 프로세스의
`sessionId`가 `released: false`로 남아 있었고, 180초가 지나서야 인수됐다. **의도한
동작이다**(설계 §7). §5-F의 "다른 활성 sessionId의 write는 Busy · 만료 뒤 인수"가
실기에서 우연히 증명된 셈이다 - 다만 두 기기가 아니라 한 기기의 두 세션이므로
§5-F를 통과로 적지는 않는다.

---

## 6-A. (구) urgent 실측 계획 — 수렴 검사

| 트리거 | 실기 실측 |
|--------|----------|
| 일반 뽑기 · 스킬 뽑기 · 무료 10연 · 보석 소비 · 귀문 승리/전직 | 미실측 |

각 행동에서 확인해야 할 여섯(로컬 저장 선행 → urgent 예약 로그 → 약 2초 뒤 트랜잭션
→ revision 정확히 +1 → payload에 재화·천장·획득이 같은 스냅샷 → pending sidecar 저장
성공 전 서버 write 없음)은 실기에서만 관측된다.

**한 가지는 이번에 자동 검사로 못 박았다.** "같은 프레임에 이벤트가 여러 번 발생해도
불필요한 revision 폭증이 없는가":

```
ManyUrgentEventsConvergeOnOneCommit
    RequestUrgent 12회 (10연의 뽑기 결과 11 + 보석 소비 1)
    → urgent 시계는 **처음 한 번만** 걸린다 (UrgentSinceForTests 불변)
    → 2초 유예 하나 뒤에 커밋 하나
```

시계가 매번 새로 걸리면 유예가 계속 밀리거나(늦은 업로드) 이벤트마다 커밋이 하나씩
나간다(revision 폭증 → 백업이 그만큼씩 밀려남). 이제 그 회귀는 검사가 잡는다.

"pending sidecar 저장 성공 전에는 서버 write 없음"은 58단계
`CloudSavePolicy.MayStartServerWrite`가 이미 강제하고 있고 그 경로는 이번에 손대지
않았다.

---

## 7. 응답 유실 idempotency — **seam 완성 / 실기 미실측**

### 7.1 seam

`CloudSaveStore.DropNextCommitResponseForTests` (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`)

트랜잭션이 **성공한 직후**, `AdoptRevision` 바로 앞에서 끊는다:

```
[7]  트랜잭션 커밋 성공 (서버 rev N)
[8b] ← seam: 응답이 유실된 것처럼 군다. 한 번 쓰면 스스로 꺼진다
        · sidecar를 앞으로 밀지 않는다  → pendingMutationId 유지, base rev N-1
        · Failed 를 돌려준다             → 로컬 dirty 유지
[9]  (건너뜀)
```

재시작 뒤의 경로는 이미 58단계에 있다:

```
재시도는 **같은 id로** 간다 (sidecar.HasPending ? pendingMutationId : New())
   → 트랜잭션 [4]에서 서버 lastMutationId == pendingMutationId
   → AlreadyApplied (IsSynced = true, revision = 서버 값 그대로)
   → revision을 추가로 올리지 않고 pending 정리
```

### 7.2 자동 검사

- `AReplayedMutationDoesNotAdvanceTheRevision` — `WasCommitAlreadyApplied` 판정과
  `AlreadyApplied` 결과의 revision이 **서버 값 그대로**임
- `AFailedCommitKeepsThePendingMutation` — 실패한 커밋이 pending을 남기고,
  `MarkSynced` 뒤에야 base revision이 앞으로 감
- `TheResponseLossSeamIsCompiledOutOfReleaseBuilds` — 임의 실패 스위치가 출시
  컴파일에 **존재하지 않음** (가드 카운팅)

### 7.3 실기 실측 ✔ (Galaxy Note 20, 2026-08-27)

**두 갈래를 모두 밟았다** — 같은 프로세스 안의 재시도, 그리고 앱이 죽었다 살아난 뒤의
재시도. 후자가 지시서의 시나리오(“4. 재시작”)다.

#### ① 같은 프로세스 재시도

```
14:20:43.886  W [CloudSave] [진단] 응답 유실 모의 - 서버는 rev 96 로 커밋됐지만
                 sidecar를 밀지 않습니다.
14:21:14.335  I [CloudSave] 주기 동기화 완료 (rev 96)      ← 31초 뒤(재시도 유예 30초)
```

| | 값 |
|---|---|
| sidecar (후) | base rev **96** · pending **없음** |
| 서버 | revision **96** (base 95) — **97로 오르지 않았다** |
| 상태 | `InSync · 클라우드 저장 완료` |
| 서버 요약 | 43층 · 보석 **617** ← 응답이 유실된 그 커밋이 정본이고, 지불이 실려 있다 |

#### ② 앱 강제 종료 후 재시작 — 지시서 시나리오 그대로

```
14:25:48.794  W [진단] 응답 유실 모의 - 서버는 rev 98 로 커밋됐지만 sidecar를 밀지 않습니다.
              → 즉시 am force-stop  (MarkSynced 직전에 앱이 죽은 것과 같은 상태)

  디스크의 sidecar:  base rev 97  ·  pending = 5645b54d457041bebc1d6883869596e6

14:26:16.631  I [CloudSave] 부팅 선택: … (재시작)
14:29:17.653  I [CloudSave] 주기 동기화 완료 (rev 98)      ← 세션 180초 만료 뒤 재시도
```

| | 값 |
|---|---|
| sidecar (후) | base rev **98** · pending **없음** |
| 서버 | revision **98** (base 97) — **99로 오르지 않았다** |
| 상태 | `InSync · 클라우드 저장 완료` |

재시작 뒤의 3분 공백은 죽은 프로세스가 놓지 못한 세션의 **180초 만료**다(설계대로).
그 뒤 재시도가 **디스크에서 읽은 같은 mutation id**로 나가 `AlreadyApplied`로 떨어졌다.

#### §7 요구 대조

| # | 요구 | 결과 |
|---|------|------|
| 1 | 서버 transaction 성공 | ✔ rev 96 / rev 98 |
| 2 | 로컬 MarkSynced 직전 응답 유실 모의 | ✔ seam이 `[8b]`에서 끊었다 |
| 3 | sidecar의 pendingMutationId 유지 | ✔ `5645b54d…`가 디스크에 남았다 |
| 4 | 재시작 | ✔ ② |
| 5 | 서버 lastMutationId == pendingMutationId | ✔ (AlreadyApplied가 그 판정이다) |
| 6 | AlreadyApplied 판정 | ✔ `IsSynced`로 처리돼 "완료"로 찍힌다 |
| 7 | revision을 추가로 올리지 않고 pending 정리 | ✔ **96→96 · 98→98**, pending 없음 |

**백업이 밀리지 않았다**는 것이 이 검사의 실질이다 - 재시도가 새 revision을 만들었다면
`playerSaveBackups`의 한 벌이 그만큼 밀려나고, 밀려난 것은 되찾을 방법이 없다.

> ⚠️ 관측 편의 문제 하나: `AlreadyApplied`도 로그가 그냥 `동기화 완료 (rev N)`다.
> "새로 썼다"와 "이미 있던 것을 확인했다"가 로그에서 구분되지 않아, revision을
> 앞뒤로 대조해야만 판정할 수 있었다. 로그에 갈래를 적으면 다음 실측이 짧아진다.

운영 정본을 손상시키는 future-version 문서 삽입은 하지 않았다. 미래 saveVersion 검사는
Emulator에만 있고 이번에 건드리지 않았다.

---

## 8. App Check enforcement — **켜지 않았다**

검증 전에 켜지 않는다는 것이 이번 스텝의 명시적 경계다. 켜기 위한 선행 조건
(Editor Debug Verified · Android Play Integrity Verified · 미등록 토큰 거부 확인)이
**하나도 충족되지 않았으므로** 활성화 승인을 요청할 단계도 아니다.

- Cloud Firestore enforcement: **비활성 (변경 없음)**
- Authentication enforcement: **비활성 (이번 범위 아님, 변경 없음)**
- 활성화 승인 요청: **하지 않음** — 선행 검증이 끝난 뒤에 한다

§3·§5·§6·§7이 끝나면 그때 "Firestore App Check enforcement를 활성화해도 되는가"
한 건만 승인을 받고, 활성화 직전 규칙·App Check 상태를 기록한 뒤 켠다. 적용까지
최대 15분을 고려하고, 그 뒤 Editor·Android·미검증 요청·Link/Recover·리더보드·크로스
저장 회귀를 다시 확인한다.

---

## 9. 자동 테스트

| 스위트 | 이전 | 이번 | 결과 |
|--------|------|------|------|
| EditMode | 897 | **918** (+21) | **918/918 통과** (223.3초) |
| PlayMode | 44 | 44 | **미완주 — 에디터 인-프로세스 실행이 멈춰 있다 (§9.3)** |
| Firestore Emulator | 47 | 47 | **47/47 통과** (7초) |
| 컴파일 오류 | — | — | **0** |
| 신규 경고 | — | — | **0** |
| SaveData | v21 | v21 | **유지** |

### 9.1 신규 검사 19개 (`AppCheckPolicyTests`)

| 검사 | 무엇을 못 박는가 |
|------|-----------------|
| `TheBootstrapRunsBeforeEveryFirebaseHandle` | ★★ bootstrap이 Firebase 핸들 다섯 **전부**보다 앞 |
| `OnlyTwoFilesEverCreateFirebaseHandles` | 초기화 경로가 늘면 먼저 빨개진다 |
| `TheBootstrapIsIdempotent` | 중복 호출이 판정을 바꾸지 않는다 |
| `TheEditorUsesTheDebugProvider` | 에디터 = Debug |
| `AndroidUsesPlayIntegrity` | 실기 갈래에 Debug provider가 **없다** |
| `AnUnsupportedPlatformStillBoots` | 미지원 플랫폼에서 부팅을 깨지 않는다 |
| `TheDebugTokenOnlyEverComesFromTheEnvironment` | 토큰 리터럴이 추적 파일에 없다 (모양) |
| `TheLiveDebugTokenIsNotCommittedAnywhere` | 지금 환경의 토큰 **값**이 소스·씬·프리팹·ProjectSettings·docs에 없다 |
| `TheDiagnosticsNeverTouchTheRawToken` | 진단기가 토큰 원문 프로퍼티를 읽지 않는다 |
| `TheDebugProviderIsCompiledOutOfReleaseBuilds` | Debug 경로 6개 토큰이 전부 가드 안 |
| `TheDiagnosticsAreCompiledOutOfReleaseBuilds` | 진단기는 **파일 전체가** 가드 안 |
| `TheResponseLossSeamIsCompiledOutOfReleaseBuilds` | 임의 실패 스위치가 출시에 없다 |
| `AMissingTokenClosesTheEditorServerGate` | 토큰 없으면 에디터 실서버 차단 + 원인 표시 |
| `EveryEditorServerGateChecksAppCheck` | 세 게이트가 모두 App Check를 본다 |
| `ABlockedAppCheckNeverStopsTheLocalSave` | ★★ App Check가 죽어도 로컬 저장은 산다 |
| `ABlockedAppCheckOnlyMovesTheCloudState` | 막히는 것은 클라우드 상태뿐 |
| `ManyUrgentEventsConvergeOnOneCommit` | ★★ urgent 여럿 → 커밋 하나 |
| `AReplayedMutationDoesNotAdvanceTheRevision` | ★★ 재시도가 revision을 안 올린다 |
| `AFailedCommitKeepsThePendingMutation` | 실패한 커밋이 pending을 남긴다 |

소스 검사가 많은 이유는 이 스텝이 막으려는 실패 셋이 **런타임에 관측되지 않기**
때문이다 — 순서는 enforcement 전까지 무증상, 비밀은 새고 나서야 알고, "릴리스에서는
안 돈다"는 플래그로 지킬 수 없다.

### 9.2 세이브 격리

실사용 세이브를 만지는 검사는 전부 `SaveSandbox` 안에서 돈다. `SaveSandbox`는 루트를
바꾸기 전에 실사용 파일 전부의 바이트를 떠 두고, Dispose에서 **달라진 파일이 있으면
예외를 던진다.**

`%LOCALAPPDATA%\..\LocalLow\Onikiri\ONIKIRI\`

⚠️ **정정**: 아래 표는 2026-08-23 회차의 값이다. `onikiri_save.json`은 그 뒤
**2026-08-24 11:21에 한 번 바뀌었다**(`ce1bb8ca…` → `1be951ec…`). 원인은 검사가
아니라 **사람이 에디터를 다시 켜고 플레이 모드에 들어간 것**이다 - 같은 시각에
새 `Logs/Editor.log`가 시작되고, `GameSession`의 30초 자동 저장이 돌았다.

근거 둘:

- 그 뒤 mtime이 **8/24 11:21에서 멈춰 있다.** 8/24 오후의 실측도, 오늘(8/27)의
  EditMode 918개도 이 파일을 건드리지 않았다.
- PC에는 **`onikiri_cloud_state.json`(sidecar)이 없다.** 에디터가 정본 왕복을 한
  번도 하지 않았다는 뜻이고, 에디터 게이트 + 디버그 토큰 부재와 일치한다(§2.4).

즉 **검사 격리는 깨지지 않았다.** `SaveSandbox`가 Dispose마다 실사용 파일의 바이트를
대조하므로, 한 검사라도 새어 나갔다면 그 TearDown이 터졌을 것이다 - 918개 전량 통과가
그 사실을 매 실행 증명한다.

백업 두 벌은 **8/9 · 8/14 이후 한 바이트도 안 바뀌었다**:

| 파일 | SHA-256 | 수정 시각 |
|------|---------|----------|
| `onikiri_save.json` | `ce1bb8caaae3be109c0bec5a21929edca72b2b2269538bf04e1e1eccd5eb6e14` | 2026-08-19 21:48 |
| `onikiri_save.backup.json` | `ed789730f82b94e17ad6640478d5525f2766f58b63b5b47b63e5caeab421fc61` | 2026-08-09 02:05 |
| `onikiri_save.json.v20-backup-step3` | `82d7b335632d4ed961a3b8dcda67e217351a1694d78f99369337ff11e4e66a15` | 2026-08-14 19:49 |

(위 표는 8/23 회차 값 — `onikiri_save.json`의 이후 변화는 이 절 첫머리의 정정 참고.)

최종 확인 (2026-08-27, EditMode 918개 직후):

| 파일 | SHA-256 앞 16자 | 수정 시각 | 판정 |
|------|----------------|----------|------|
| `onikiri_save.json` | `1be951ecd04c5447` | 2026-08-24 11:21 | 사람의 플레이로 1회 변경, 이후 불변 |
| `onikiri_save.backup.json` | `ed789730f82b94e1` | 2026-08-09 02:05 | **불변** |
| `onikiri_save.json.v20-backup-step3` | `82d7b335632d4ed9` | 2026-08-14 19:49 | **불변** |

크로스 저장 sidecar 파일은 이 기계에 **없다** — 에디터 게이트가 계속 꺼져 있어
정본 왕복을 한 번도 하지 않았다는 뜻이고, §2.4·§5의 상태와 일치한다.

### 9.3 PlayMode — 62단계 당시의 관측 _(현재 상태 아님)_

> **⚠️ 이 절은 62단계(2026-08-23~24) 당시의 기록이다. 지금은 해소됐다.**
>
> 62.1에서 배치로 전량 실행해 **44/44 통과**를 확인했다(§11.7).
>
> 그리고 아래의 원인 설명("unity-mcp WebSocket 교착") **자체가 근거가 약했다.**
> 62.1에서 MCP 없는 배치도 같은 지점에서 멈추는 것을 보았고, bisect로 확정한
> 실제 원인은 그때그때 달랐다 - 62.1 회차의 매달림은 **62.1이 새로 넣은 부팅
> 대기** 때문이었다(§11.8). 62단계 회차의 매달림이 무엇 때문이었는지는
> **끝내 확정하지 못했다** — 대조군을 만들지 않았기 때문이다.
>
> 남겨 두는 이유는 두 가지다: ① MCP로 PlayMode 전량을 돌리지 말라는 규칙은
> 여전히 유효하고, ② "같은 스택이 보였다"를 원인으로 적었던 것이 어떻게 틀릴
> 수 있는지가 이 절에 남아 있다.

60단계가 세 번의 행 끝에 못 박은 규칙이 있다:

> **MCP로 PlayMode 전량 실행 금지** — unity-mcp 플러그인 WebSocket이 도메인 리로드와
> 교착한다. PlayMode 검증은 **배치 모드**(`-batchmode -runTests -testPlatform PlayMode`)로.

이번에 그 규칙을 어기고 MCP `run_tests(PlayMode)`로 44개를 돌렸다. 그리고 정확히
기록된 그대로 멈췄다. **이것은 이번 코드 변경의 증상이 아니라 실행 방법을 잘못
고른 결과다.**

```
21:54  run_tests(PlayMode) 시작
20:58  마지막 테스트 출력:
       [CloudSave] 충돌 해결: 클라우드 기록 사용 (rev 6)
       ← CloudConflictPlayTests.ChoosingCloudAdoptsTheServerBranch:135 (씬 재로드)
21:13  이후 15분간 새 테스트 출력 0줄.
       Logs/Editor.log 에 늘어난 것은 MCP WebSocket keep-alive 실패뿐
```

**에디터를 죽이거나 다시 켜지 않았다.** 지금도 그 상태로 있다 — 어떻게 할지는
사용자가 정한다.

#### 이 멈춤이 이번 변경 때문인가 — **아니다**

가장 큰 근거는 위다: 같은 멈춤이 60단계에 이미 세 번 기록됐고, 그때는 이 코드가
없었다. 로그 꼬리도 그때와 같은 스택(unity-mcp WebSocket)이다. 그 밖에도 셋:

1. **막힌 지점이 App Check보다 한참 뒤다.** 로그에서 `[AppCheck] 환경 변수 …가
   비어 있다`(라인 5595)가 먼저 찍히고, 그 뒤로 `부팅 선택`(5768·5842) →
   `클라우드 적용 전 로컬 백업`(5908) → `충돌 해결: 클라우드 기록 사용 rev 6`(5936)이
   정상으로 지나갔다. 멈춘 곳은 그 다음의 **씬 재로드**다.

2. **기본 상태에서 새 코드가 하는 일이 없다.** `EditorNetworkAllowed` ·
   `EditorServerCheckAllowed`가 기본 꺼짐이고, C#의 `||` 단축 평가 때문에 App Check
   조건은 **평가되지도 않는다.** `Tick()`이 매 프레임 늘린 일이 0이다.

3. **EditMode 918개가 같은 코드로 전량 통과했다.** 클라우드 부팅·동기화·복구·
   충돌의 순수 판정은 전부 그쪽에 있다.

그래도 **44개가 초록으로 끝나는 것을 보지 못했으므로 PlayMode 무회귀는 증명되지
않았다.** 추정으로 채우지 않는다.

#### 다음에 할 일

에디터를 닫은 뒤 61단계와 같은 방식으로 배치 실행:

```
Unity.exe -batchmode -runTests -projectPath C:\UnityProject\Onikiri ^
          -testPlatform PlayMode -testResults <경로>\playmode.xml
```

(61단계 기록대로, 배치가 지우는 Sentis define은 끝나고 `git checkout ProjectSettings`로
복원한다.)

#### 부수 소득 — bootstrap이 실제 부팅 경로를 지났다는 증거

멈추기 전까지의 로그가 한 가지를 확인해 준다: **씬 부팅이 실제로 App Check
부트스트랩을 지났고**, 토큰이 없다는 사실을 정확히 한 번 보고했다. 배선이 죽은
코드가 아니라는 뜻이다.

```
[AppCheck] 환경 변수 ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN 가 비어 있다.
           Firebase 콘솔 > App Check > 앱 > 디버그 토큰 관리에 등록한 값을
           환경 변수로 넣고 에디터를 다시 켜면 된다
```

그리고 그 뒤로 부팅·충돌·백업이 전부 평소대로 지나갔다 — **App Check가 막혀도
로컬 진행과 충돌 해결은 그대로 돈다**는 §9.1의 계약이 실제 씬에서도 그렇게 보였다.

---

## 9-A. 실측이 연 빚과 그 처리 (2026-08-27 판단)

실측 과정에서 코드 결함 하나와 관측 결함 둘이 드러났다. **셋을 같이 고치지 않았다** -
검증 가능성이 서로 다르기 때문이다.

| 빚 | 성격 | 이번 처리 | 이유 |
|----|------|-----------|------|
| urgent이 지불 전 스냅샷을 올린다 (§6.2) | **데이터 결함** | ✔ **고쳤다** | 실기에서 전후를 직접 잴 수 있었다(§6.4) |
| 부팅이 서버를 안 본다 (§5.B) | **데이터 결함** | ✗ **안 고쳤다** | 아래 |
| `AlreadyApplied`·`Busy`가 로그에서 안 보인다 | 관측 결함 | ✔ 고쳤다 | 로그 문자열뿐, 동작 무변 |

### 왜 §5.B를 이번에 안 고쳤는가

고치려면 **부팅 프레임의 순서**나 **로그인 완료 후 정본 재확인 창구**를 건드려야 한다.
그 자리는 59단계가 귀문 PlayMode 검사 다섯과 맞바꿔 정한 자리다(그 검사들이 첫
프레임에 `GameSession`을 파괴하므로, 부팅을 한 프레임이라도 미루면 세이브가 통째로
안 얹힌다).

그런데 **이 회차에서 PlayMode를 돌릴 수 없다**(§9.3 - 배치 실행은 에디터를 닫아야 하고,
그 판단은 사용자 몫이다). 즉 지금 그 코드를 고치면 **깨지는지 아닌지 모르는 채로**
크로스 저장의 부팅 경로를 바꾸는 일이 된다.

검증 못 하는 변경을 이 코어에 넣는 것은, 이 설계 전체가 막으려는 사고("조용히 갈리고
나중에 드러남")를 스스로 만드는 일이다. **원인·재현·권장 수정을 §5.B에 적어 두고
넘긴다** - 다음 스텝에서 PlayMode와 함께 처리하는 것이 맞다.

### 이번에 넣은 로그 두 줄

```
[CloudSave] urgent 동기화 확인 (rev 98 - 이미 적용됨, 응답 유실 복구)
[CloudSave] 주기 동기화 보류 - 다른 기기가 세션을 쥐고 있습니다 (놓거나 180초 만료 뒤 인수).
```

- **AlreadyApplied**: 전에는 `동기화 완료 (rev N)`으로만 찍혀 새로 쓴 것과 구분되지
  않았다. §7 실측에서 revision을 앞뒤로 대조해야만 판정할 수 있었던 이유다.
- **Busy**: 전에는 **아무것도 안 적고 돌아갔다.** §5.F에서 "커밋이 왜 안 나가는가"를
  알아내는 데 진단을 한 번 더 돌려야 했다. 상태가 바뀔 때만 적어 30초 재시도가
  로그를 덮지 않게 했다.

---

## 10. 사람이 이어서 할 일

### 10.1 Editor Debug Provider를 살리려면

1. Firebase 콘솔 → App Check → `ONIKIRI Android` → **디버그 토큰 관리**에서 토큰 발급
2. 그 값을 사용자 환경 변수 `ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN`에 넣는다
3. **Unity Editor를 다시 켠다** (환경 변수는 프로세스 시작 시 읽힌다)
4. 테스트 패널 → "크로스 저장 (6단계 - App Check)" → `전체 진단`
   → `provider = Debug` / `배선 = Configured` / `토큰 성공 (만료 …Z)`

> 토큰 값을 채팅·커밋·보고서 어디에도 붙여 넣지 않는다. 값 검사
> (`TheLiveDebugTokenIsNotCommittedAnywhere`)가 실수를 잡지만, 잡히기 전에 이미
> git에 들어갔다면 콘솔에서 토큰을 폐기해야 한다.

### 10.2 Android Play Integrity를 살리려면

§3.5의 다섯 단계. (이번 회차는 §3.4의 (A) 경로로 진행하고, 그 경우 "출시 전 해제" 항목이 하나 남는다.)

### 10.3 그다음

§5 A~F → §6 다섯 트리거 → §7 응답 유실 → 전부 통과한 뒤에야 §8 enforcement 승인 요청.

---

## 11. 변경 요약

| 파일 | 변경 |
|------|------|
| `Scripts/Cloud/FirebaseAppCheckBootstrap.cs` | **신규** — provider 단일 진입점 |
| `Scripts/Cloud/AppCheckDiagnostics.cs` | **신규** — 진단기 (파일 전체 가드) |
| `Tests/EditMode/AppCheckPolicyTests.cs` | **신규** — 검사 19개 |
| `Scripts/Cloud/CloudScores.cs` | `InitializeCoreAsync` 첫 줄에 bootstrap |
| `Scripts/Cloud/FirebaseRuntime.cs` | `EnsureReadyAsync` 첫 줄에 bootstrap |
| `Scripts/Cloud/CloudSaveCoordinator.cs` | 부팅 서버 확인 게이트에 App Check 조건 |
| `Scripts/Cloud/CloudSaveRecovery.cs` | 복구 읽기 게이트에 App Check 조건 |
| `Scripts/Cloud/CloudSaveSync.cs` | 게이트 셋 → `EditorBlocksNetwork()` · `UrgentSinceForTests` |
| `Scripts/Cloud/CloudSaveStore.cs` | 응답 유실 seam (가드 안) |
| `Editor/OnikiriTestPanel.cs` | App Check 절 추가 (스크립트 확장만) |

**손대지 않은 것**: 밸런스 · 전투 · 승급/전직 · 세이브 데이터 구조 · `firestore.rules` ·
`firestore.indexes.json` · `Main.unity` · Firebase SDK 버전 · Remote Config · Apple/iOS ·
Cloud Functions · IAP.

**커밋·푸시 없음.**

---

# 11. Step 62.1 — 부팅 크로스 저장 P0 폐쇄

62단계 실측이 남긴 P0 둘을 닫는다.

- **P0-1** 기존 기기의 부팅이 서버 저장을 보지 못한다 (§5.B)
- **P0-2** App Check 토큰 실패 뒤에도 진단이 Firestore를 읽는다

## 11.1 P0-1의 실제 원인

`GameSession.Start()`가 `CloudSaveCoordinator.WillCheckServer`를 **한 번** 묻는다.
그 값은 `ShouldCheckServer(uid)`이고, 마지막 줄이 `return !string.IsNullOrEmpty(uid)`다.

부팅 시점에는 Firebase 로그인이 아직 안 끝나 **uid가 비어 있다.** 그래서 값은 false가
되고, 부팅은 같은 프레임에 `ChooseLocally`로 확정된다. 약 1초 뒤 로그인이 끝나도
**이미 고른 뒤다.**

62단계 실기 로그가 그 1초를 그대로 찍었다:

```
14:01:13.023  [CloudSave] 부팅 선택: 서버 확인 없음 - 로컬 사용
14:01:13.098  [Onikiri] Boot apply #1 - 서버 확인 없음 - 로컬 사용 (방치 보상 1회)
14:01:14.082  [CloudScores] 기존 로그인 재사용. uid = A9aMgx…      ← 1.06초 늦다
```

**증상이 조용하다는 것이 이 결함의 성질이다.** 데이터가 상하지는 않는다 - 기기는
갈라짐을 부팅이 아니라 **다음 커밋에서** 알게 되고(§5.C), 거기서 정상적으로 선택
화면이 뜬다. 그러나 설계가 약속한 "재실행하면 최신을 받는다"는 성립하지 않고,
사용자는 겪지 않아도 될 충돌 선택을 하나 더 겪는다.

### 왜 예전에는 이렇게 뒀는가

59단계가 알면서 남긴 자리다. 부팅을 코루틴으로 미루면 **귀문 PlayMode 검사 다섯이
깨진다** - 그 검사들이 세이브 오염을 막으려고 첫 프레임에 `GameSession`을 파괴하고,
한 프레임 뒤로 밀린 부팅 코루틴이 함께 죽어 세이브가 통째로 안 얹힌다. 56단계 인트로도
"세이브 로드는 같은 프레임"을 가정한다.

그래서 62.1의 수정은 **"코루틴으로 바꾼다"가 아니라 "기다릴 값이 있는 기기만
기다린다"**여야 했다.

## 11.2 바뀐 부팅 상태 흐름

```
GameSession.Start()
   │
   ├─ WillCheckServer      … uid가 이미 있는가 (기존 조건)
   ├─ WillAwaitIdentity    … ★ 신규: 사이드카가 있는데 uid가 아직 없는가
   │
   ├─ 둘 다 false ─────────→ ChooseLocally  (같은 프레임, 지연 0)
   │                          └ 신규·로컬 전용·서버확인 꺼짐
   │
   └─ 하나라도 true ───────→ ChooseBootSave (코루틴)
                              │
                              │  예산 = BootServerCheckSeconds (6초) **하나**
                              │
                              ├─[0] 인증 대기   uid가 올 때까지 / 마감까지
                              │      └ 못 오면 → LocalOnly로 종료 (Offline 기록)
                              │
                              ├─[1] 사이드카 확정  uid가 정해진 **뒤에** OwnSidecar()
                              │      └ 주인이 다르면 사이드카는 없는 것으로 친다
                              │
                              ├─[2] 서버 조회   남은 마감까지
                              │      └ 못 끝내면 → serverChecked=false → LocalOnly
                              │
                              └─[3] ChooseFrom → Finish → onDone (**정확히 한 번**)
```

**예산이 하나인 이유**: 인증 대기와 서버 조회가 각자 6초를 쓰면 최악에 12초가 되고,
그것은 오프라인에서 첫 화면이 12초 늦는다는 뜻이다. 마감을 코루틴 진입에서 한 번
정하고 두 단계가 나눠 쓴다.

## 11.3 사이드카 유무별 동작

기다림의 조건은 **디스크에 성립하는 사이드카가 있는가** 하나다
(`CloudSavePolicy.ShouldAwaitIdentity`, 순수 함수).

| 사용자 | 사이드카 | uid | 동작 |
|--------|---------|-----|------|
| 신규 설치 | 없음 | 없음 | **지연 0** — 같은 프레임 로컬 부팅 |
| 로컬 전용 (연동 안 함) | 없음 | 없음 | **지연 0** |
| 기존 연동 | **있음** | 없음 | **기다린다** (최대 6초) ← P0가 여기 있었다 |
| 기존 연동 (로그인 빠름) | 있음 | 있음 | 기다리지 않고 곧바로 서버 조회 |
| 서버 확인 꺼짐 | 무관 | 무관 | **지연 0** |

`WillAwaitIdentity`가 보는 사이드카는 `OwnSidecar()`가 아니라 **`DiskSidecar()`**다.
전자는 uid로 거르므로 로그인 전에는 언제나 null이고, 그것으로는 "기다릴 값이 있는
기기인가"를 물을 수 없다. 여기서 묻는 것은 소유권이 아니라 **존재**다.

## 11.4 UID 불일치 · 타임아웃 · 오프라인

### UID 불일치

기다리는 동안 계정이 갈릴 수 있다(복구). uid가 정해진 **뒤에** `OwnSidecar()`로
사이드카를 확정하므로, 주인이 다르면 그 사이드카는 **없는 것으로 친다.**

사이드카가 없으면 `CloudSavePolicy.Decide`에서 `localChanged`·`serverMoved`가 둘 다
true가 되어 **`Conflict`**로 떨어진다 - 서버 기록이 조용히 적용되는 길이 없다.
경고도 한 줄 남긴다(uid는 마스킹).

### 타임아웃

| 못 끝낸 것 | 결과 |
|-----------|------|
| 인증 | `LastServerStatus = Offline` · `LocalOnly` · 로컬 그대로 |
| 서버 조회 | `serverChecked = false` → `Decide` 첫 줄이 `LocalOnly` |

둘 다 **로컬 한 벌을 들고 게임에 들어간다.** 부팅이 안 끝나는 것이 최악이다 -
방치형에서 첫 화면이 안 뜨면 그 실행이 통째로 사라진다.

### 오프라인

`Fetch`가 `Offline`을 돌려주면 `serverChecked`는 false다("서버를 못 봤다"이지
"서버에 없다"가 아니다). 마감 뒤 로컬로 진입한다. 검사
`OfflineAlwaysEntersTheGame`이 이것을 잰다.

### 전투 중 핫스왑 없음

**로그인 완료 후 뒤늦게 서버 저장을 얹는 경로를 만들지 않았다.** 서버/로컬 선택은
부팅 코루틴 안에서만 끝난다. 마감을 넘긴 뒤 서버 응답이 돌아와도 그 Task는
`FirebaseRuntime.Observe`로 흘려보내고 부팅 선택에 반영하지 않는다(기존 동작 유지).

## 11.5 `ApplyBoot` · 방치 보상 1회 보장 근거

갈래가 셋(인증 타임아웃 · 서버 타임아웃 · 정상)으로 늘면서 `onDone`을 부르는 자리가
흩어질 위험이 생겼다. **두 번 부르면 `BootWith`가 두 번 돌고, 그것은 곧 방치 보상
두 번이다** - 59단계가 없앤 사고 그대로다.

근거 셋:

1. **나가는 문이 하나다.** `Finish(choice, sidecar, fetch, onDone)` 오버로드를 두고
   모든 갈래가 그리로 모인다. 인증 타임아웃 갈래는 `yield break`로 끝나 아래를
   지나지 않는다.
2. **검사가 센다.** `EveryPathReportsExactlyOneChoice`가 네 갈래(지연 로그인 ·
   인증 타임아웃 · 서버 타임아웃 · 오프라인)를 각각 돌려 `onDone` 호출 수가
   정확히 1인지 잰다.
3. **지급 자리가 하나다.** `TheOfflineRewardStillHasExactlyOneGrantSite`가
   `GameSession.cs`에서 `GrantOfflineReward(` 등장 수를 센다(정의 1 + 호출 1 = 2).
   `GameSession.BootApplyCount`·`OfflineRewardGrants`는 PlayMode가 잰다.

## 11.6 P0-2 — App Check 실패 후 서버 요청 0회

`AppCheckDiagnostics.RunAsync()`가 토큰 갱신 실패 뒤에도 `ReadCanonicalAsync()`를
불렀다. 나쁜 이유가 둘이다:

1. **미검증 요청을 우리 손으로 만든다.** enforcement 뒤에는 거부되고, 그 거부가
   App Check Metrics의 "확인되지 않음"에 쌓인다 - 진단 도구가 진단 대상을 오염시킨다.
2. **읽히면 통과한 것처럼 읽힌다.** enforcement가 꺼져 있는 동안에는 토큰 없이도
   정본이 돌아온다. 그 성공을 "App Check가 된다"로 적으면 거짓 보고가 된다.

### 바뀐 계약

```
RunAsync()
   ├─ Snapshot()                     (서버 왕복 없음)
   ├─ RefreshTokenAsync()
   │     └ 실패 → AbortedMarker + 원인 + 이유 설명, **여기서 return**
   └─ ReadCanonicalAsync()           ← 토큰이 성공했을 때만 도달한다
```

- 실패 원인은 화면(오버레이 한 줄)과 로그(`Debug.LogWarning`) 양쪽에 남는다.
- **미검증 요청을 일부러 보내는 창구는 두지 않았다.** 그런 메서드가 출시 빌드에
  남으면 그 자체가 미검증 트래픽 생성기다. 음성 확인은 콘솔의 enforcement 단계에서
  한다. 검사 `NoDeliberateUnverifiedRequestPathExists`가 그런 이름의 창구가 생기는
  것을 막는다.
- App Check enforcement · Authentication enforcement **둘 다 건드리지 않았다.**

### 서버 요청 0회의 증거

`AppCheckDiagnostics.ServerCallsForTests`를 두고 **진단이 Firestore를 만지는 유일한
자리**(`ReadCanonicalAsync`의 `FetchAsync` 직전)에서 증가시킨다. "안 부른다"는 계약은
주석으로 지켜지지 않으므로 세는 수를 둔다.

`AFailedTokenStopsTheDiagnosticsBeforeAnyServerCall`이 이 기계(디버그 토큰 없음)에서
`RunAsync()`를 실제로 돌려 잰다:

| 잰 것 | 결과 |
|-------|------|
| `ServerCallsForTests` | **0** |
| `LastTokenOk` | false |
| 보고서에 `AbortedMarker` | 있음 |
| 보고서에 `"정본 읽기"` | **없음** |

순서까지 소스로 못 박는다 - `TheDiagnosticsAbortBranchGuardsTheCanonicalRead`가
`if (!LastTokenOk)`가 `await ReadCanonicalAsync()`보다 **앞**에 있는지 잰다(주석은
제외하고 호출만 센다).


## 11.7 검증 결과

전부 **명령행 배치**로 돌렸다. Unity Editor는 완전히 종료한 상태였고, PlayMode에
MCP `run_tests`를 쓰지 않았다.

| 스위트 | total | passed | failed | skipped | inconclusive | XML |
|--------|-------|--------|--------|---------|--------------|-----|
| **EditMode** | **938** | **938** | 0 | 0 | 0 | `Builds/TestResults/editmode-62_1.xml` |
| **PlayMode** | **44** | **44** | 0 | 0 | 0 | `Builds/TestResults/playmode-62_1.xml` |
| **Firestore Emulator** | **47** | **47** | 0 | — | — | `npm --prefix tools/firestore-rules-tests test` |

- 컴파일 오류 0 · 신규 경고 0
- 62단계 종료 시 918 → **938** (+20: 부팅 대기 14 + App Check 진단 3 + 기존 보완)
- 배치가 지운 Sentis define은 `git checkout ProjectSettings`로 복원했다(60단계 규칙)
- 로그: `Builds/TestResults/editmode-62_1.log` · `playmode-62_1.log`

### 실행 명령

```
Unity.exe -batchmode -runTests -projectPath C:\UnityProject\Onikiri ^
          -testPlatform {EditMode|PlayMode} ^
          -testResults <XML> -logFile <LOG>
```

## 11.8 ★ 이번 회차가 스스로 만든 회귀 (bisect로 잡음)

**정직하게 적는다: 62.1의 첫 구현이 PlayMode 전량을 매달았다.**

전량 배치가 `CloudConflictPlayTests`의 씬 재로드 뒤에서 멈췄다. 처음에는 62단계
보고서(§9.3)에 적어 둔 대로 "unity-mcp WebSocket 교착"을 의심했으나, **MCP가 없는
배치에서도 같은 지점에서 멈췄으므로 그 설명은 틀렸다.**

격리해서 확인했다:

| 실행 | 결과 |
|------|------|
| `CloudConflictPlayTests` **제외** 전량 | 37/37 통과 |
| `CloudConflictPlayTests` **단독** | 7/7 통과 |
| 전량 (37 + 7 = 44) | **매달림** |

각각은 멀쩡한데 합치면 매다는 모양이라, 원인을 추측하지 않고 **bisect**했다 -
`WillAwaitIdentity`를 `return false`로 임시 고정하고 전량을 돌리니 **44/44 통과**.
원인이 62.1의 부팅 대기임이 확정됐다.

### 왜 매달렸는가

PlayMode 검사들은 `fetchOverride`로 가짜 서버를 쓰면서 **로그인은 하지 않는다.**
그래서 에디터에서는 uid가 영영 오지 않는다. 여기에 사이드카가 있으면 부팅이
코루틴으로 빠지고, **첫 프레임에 `GameSession`을 파괴하는 검사들**과 얽혀 59단계가
적어 둔 함정(부팅 코루틴이 함께 죽어 세이브가 안 얹힘)에 그대로 빠진다.

### 고친 방법

`AwaitIdentityAllowed` 게이트를 하나 세웠다 - **에디터에서는 검사가 uid를 직접
줄 때만 기다린다.** 실기(`!UNITY_EDITOR`)에서는 언제나 기다린다.

근거: 실기에는 부팅 직후 시작되는 실제 로그인이 있어 기다림에 값이 있지만,
에디터에는 그 로그인이 아예 없다(운영 프로젝트로 나가지 않도록 게이트가 막는다).
오지 않을 것을 6초 기다리는 것은 이득 없이 PlayMode만 망가뜨리는 일이다.

검사 `TheEditorNeverWaitsWithoutAnIdentitySeam`이 이 회귀를 다시 열지 못하게 막는다.

> **여기서 배운 것**: 62단계 보고서 §9.3의 "MCP WebSocket 교착" 결론은 근거가
> 약했다(같은 스택이 보였을 뿐 대조군이 없었다). 이번에는 대조군을 만들어
> 확정했다 - 매달림의 원인은 추측이 아니라 bisect로 정한다.

## 11.9 실기 시나리오 B — **미검증**

**이번 회차에서 밟지 못했다.** 두 폰의 무선 디버깅이 끊겼다(13:50경 연결 →
19:30 확인 시 mDNS 광고 없음, `adb devices` 빈 목록). 폰이 절전으로 들어가면
무선 디버깅 세션이 끊기고, 재연결에는 기기 조작이 필요하다.

**따라서 P0-1은 "코드 수정 + 자동 검사 통과"까지이고, 완료가 아니다.**

핵심 완료 조건은 **"Firebase 인증이 늦게 준비되는 실제 기존 기기에서도 부팅 중
최신 서버 저장을 선택하는가"**이며, 그것은 아직 눈으로 보지 못했다.

### 남은 절차 (폰 재연결 뒤)

1. 기기 B에서 진행해 서버 revision을 올린다.
2. 기기 A를 완전 종료(`am force-stop`) 후 재시작한다.
3. logcat에서 확인할 것:
   - `사이드카가 있습니다. 인증 준비를 기다립니다 (최대 6초).`
   - 그 뒤 `부팅 선택: …` 이 **서버를 본 판정**이어야 한다
     (예전에는 여기가 `서버 확인 없음 - 로컬 사용`이었다)
   - `Boot apply #1` 이 정확히 한 번
4. 전투 진입 후 저장이 뒤늦게 교체되지 않는지 본다(핫스왑 없음).
5. 비행기 모드에서 재시작 → 6초 뒤 로컬로 정상 진입하는지 본다.

### 미검증인 채로 남는 위험

- 실기의 로그인 지연이 6초를 넘는 경우가 있는지 모른다. 넘으면 예전과 같은
  결과(로컬 부팅)가 되지만 **6초를 기다린 뒤** 그렇게 된다 - 첫 화면이 그만큼 늦는다.
- 실기에서 `DiskSidecar()`가 매 부팅 파일을 한 번 더 읽는다. 비용은 작지만 재지 않았다.


## 11.10 ★ 실기 시나리오 B — **통과** (2026-08-27 19:45, Galaxy Z Flip 3)

폰이 다시 올라와 밟았다. 조건은 만들어 낸 것이 아니라 **이미 성립해 있었다** -
기기 B는 사슬이 rev 91에서 멈춰 있었고 그동안 기기 A가 서버를 124까지 올려 뒀다.

| | 기기 B (Flip 3) | 서버 |
|---|---|---|
| revision | base **91** | **124** |
| deviceId | `0f1346…` | `a44069…` (기기 A가 올림) |
| 진행 | 43층 · **Lv58** · 보석 647 | 43층 · **Lv61** · 보석 557 |

62.1 빌드를 올리고 앱을 시작한 첫 부팅:

```
19:45:21.025  [CloudSave] 사이드카가 있습니다. 인증 준비를 기다립니다 (최대 6초).   ← 신규
19:45:21.038  [AppCheck] provider = Play Integrity
19:45:21.907  [CloudScores] 기존 로그인 재사용. uid = A9aMgx…                     ← 0.88초
19:45:24.420  [CloudSave] 클라우드 적용 전 로컬 백업 (3벌 순환): …precloud.1
19:45:24.420  [CloudSave] 부팅 선택: 클라우드 채택 (rev 124, 43층 · Lv.61)        ★★
19:45:24.485  [Onikiri] Boot apply #1 - 클라우드 채택 (rev 124 …) (방치 보상 1회)
```

**62.1 이전이면 이 자리가 `부팅 선택: 서버 확인 없음 - 로컬 사용`이었다.** P0-1이
실기에서 닫혔다.

| 시나리오 B 요구 | 결과 |
|-----------------|------|
| 서버보다 오래된 로컬 + 더 최신 서버 revision | ✔ base 91 vs 서버 124 |
| 완전 종료 뒤 재시작 | ✔ 설치 후 새 프로세스 |
| 인증이 늦게 끝나는 조건에서 **부팅 화면에서 서버 저장 선택** | ✔ 0.88초 대기 후 rev 124 채택 |
| 전투 진입 후 뒤늦은 저장 교체 없음 | ✔ 선택은 부팅에서 끝났고 이후 교체 로그 없음 |
| 오프라인 타임아웃 뒤 로컬 진입 | ✔ **검증 완료** (§11.16) |
| `Boot apply` 1회 · 방치 보상 1회 | ✔ `Boot apply #1 … (방치 보상 1회)` |

## 11.11 그 부팅이 곧바로 드러낸 다음 결함 — 사슬이 안 따라간다

같은 실행에서 2분 뒤 이 줄이 났다:

```
19:47:25.265  W [CloudSave] 서버와 갈라졌습니다 (서버 rev 124). 기록 선택이 필요합니다.
```

기기를 열어 보니 원인이 분명했다:

| | 값 |
|---|---|
| 로컬 세이브 | 43층 · **Lv61** · 보석 557 ← 서버 rev 124 내용, **채택은 성공** |
| sidecar `baseRevision` | **91** ← 옮겨지지 않았다 |

`Finish`가 사슬을 옮기는 갈래를 **`InSync` 하나로만** 두고 있었다. `DownloadCloud`는
빠져 있었다. 그래서 채택 직후의 커밋이 base 91 vs 서버 124를 보고 **방금 채택한 그
기록을 두고** 충돌을 냈다.

**62.1 이전에는 드러날 수 없는 결함이었다** - 실기 부팅이 클라우드를 채택하는 일이
아예 없었기 때문이다(그것이 P0-1이다). P0를 닫으니 그 경로의 다음 칸이 나왔다.

### 고침

`InSync`와 `DownloadCloud`를 **같이** 다룬다. 둘 다 "지금 로컬은 서버 rev N에서
왔다"는 뜻이다. 사슬이 없던 기기(재설치·복구 직후)에는 사슬을 새로 세운다 - 안
세우면 첫 커밋이 base 0으로 나가 같은 충돌이 난다.

검사 둘을 세웠다: `AdoptingTheCloudAlsoMovesTheChain`(채택 뒤 base가 서버 자리로
옮겨졌는가) · `AChainlessDeviceGetsAChainWhenItMatchesTheServer`.

> 최신을 받아 놓고 곧바로 다시 고르라고 묻는 것은 P0를 고친 의미를 없앤다. 그래서
> 이것은 "다음 스텝"이 아니라 P0-1 폐쇄의 일부로 봤다.

## 11.12 이번 회차에 **검증하지 못한 것**

정직하게 가른다.

| 항목 | 상태 |
|------|------|
| P0-1 (부팅이 서버를 본다) | ✔ **실기 통과** (§11.10) |
| P0-2 (App Check 실패 후 서버 요청 0회) | ✔ EditMode 실측 (§11.6) |
| §11.11 사슬 이동 고침 | ✔ **실기 검증 완료** (§11.13) |
| 오프라인 타임아웃 실기 | ✔ **실기 검증 완료** (§11.16) |

### 왜 사슬 고침을 실기에서 못 봤는가

그 갈래(`DownloadCloud`)를 다시 만들려면 **서버를 앞세울 두 번째 기기**가 필요한데,
기기 A(Note 20)가 끝내 붙지 않았다(mDNS 광고가 사라지고 `adb connect` 거부). 기기 B
혼자서는 자기가 올린 revision보다 앞선 서버를 만들 수 없다.

운영 정본을 손으로 고쳐 조건을 만드는 방법은 **쓰지 않았다** - 봉투 검증 11겹과 해시
사슬을 손계산으로 맞춰야 하고, 틀리면 실사용 세이브가 상한다.

### 기기 B가 지금 놓인 상태 (사용자가 알아야 할 것)

수정 **전** 빌드가 rev 124를 채택하면서 사슬을 91에 남겼고, 그 뒤 20분간 진행이
쌓였다. 그래서 지금 기기 B는 **`기록 선택 필요`** 상태이고 클라우드 쓰기가 멈춰 있다.
이것은 정상 동작이다(갈라진 상태에서 자동으로 쓰지 않는다).

**해소 방법**: 설정 → `기록 선택` → 두 기록 중 하나를 고른다. 고르면 사슬이 그
자리로 맞춰지고 동기화가 재개된다. 로컬 43층 Lv61이 최신이므로 `현재 기기 기록 사용`이
맞다(서버 rev 124는 `playerSaveBackups`에 남는다).

### 남은 실기 절차

1. 기기 A를 다시 붙인다(무선 디버깅 껐다 켜면 포트가 바뀌므로 mDNS로 다시 찾는다).
2. 기기 B의 충돌을 해소하고 InSync로 만든다.
3. 기기 A에서 진행해 서버를 앞세운다.
4. 기기 B를 완전 종료 후 재시작 → `부팅 선택: 클라우드 채택` 확인.
5. **그 직후 sidecar `baseRevision`이 서버 revision과 같은지 확인** ← §11.11의 실기 증거
6. 비행기 모드로 재시작 → 6초 뒤 로컬 진입 확인.


## 11.13 ★ 사슬 이동 고침 — **실기 검증 완료** (2026-08-27 21:53, Galaxy Note 20)

§11.12에서 "실기 미검증"으로 남겼던 항목이다. 기기 A를 다시 페어링해 밟았다.

### 조건을 만든 순서

`DownloadCloud`는 **로컬이 마지막 동기화 그대로**일 때만 나온다(`localChanged == false`).
방치형은 골드가 계속 쌓여 그 조건이 쉽게 깨지므로, 커밋 직후 기기를 얼려서 만들었다.

1. 기기 B를 rev **128**까지 올리고 **즉시 종료** (디스크 저장 = 업로드한 스냅샷)
2. 기기 A(사슬 **127**, 로컬은 127 동기화 이후 안 변함)를 시작

### 결과

```
21:52:59.218  [CloudSave] 사이드카가 있습니다. 인증 준비를 기다립니다 (최대 6초).
21:53:02.503  [CloudSave] 부팅 선택: 클라우드 채택 (rev 128, 43층 · Lv.61)   ← DownloadCloud
21:53:02.586  [Onikiri] Boot apply #1 - … (방치 보상 1회)
```

**채택 직후 사이드카:**

| 항목 | 값 | 판정 |
|------|-----|------|
| `baseRevision` | **128** | ✔ 서버 채택본과 일치 (**수정 전이면 127에 남았다**) |
| `lastSyncedStateSha256` | `af2634c6` | ✔ 서버 rev 128의 지문과 동일 |
| `pending` | 없음 | ✔ |

### 그 뒤 3.5분 관찰 — **충돌 0건**

```
21:55:02.912  [CloudSave] 주기 동기화 보류 - 다른 기기가 세션을 쥐고 있습니다
                          (놓거나 180초 만료 뒤 인수).
21:55:33.504  [CloudSave] 주기 동기화 완료 (rev 129)
21:57:57.641  [CloudSave] 주기 동기화 완료 (rev 130)
```

`서버와 갈라졌습니다` 로그 **0건**. 채택 → 129 → 130으로 사슬이 그대로 이어졌다.
§11.11에서 관측한 "채택 직후 곧바로 나던 충돌"이 사라졌다.

> 덤으로 62단계에서 넣은 **`Busy` 로그가 실기에서 제 역할을 했다.** 예전에는 이
> 갈래가 아무것도 안 적고 돌아가 "커밋이 왜 안 나가는가"를 진단으로 따로 알아내야
> 했다(§5.F). 이제 로그 한 줄이 이유와 해소 조건을 같이 적는다.


## 11.14 부팅 대기 실측값 (온라인 3회)

인증 대기가 실제로 얼마나 걸리는지 - 6초 예산이 적절한지 재는 값이다.

| 시각 | 대기 시작 → 부팅 선택 | 결과 |
|------|----------------------|------|
| 19:45:21.025 → 19:45:21.907 | **0.88초** (uid 도착까지) | 클라우드 채택 (rev 124) |
| 21:41:22.894 → 21:41:26.603 | **3.71초** | 충돌 (사슬 갈라짐) |
| 21:52:59.218 → 21:53:02.503 | **3.29초** | 클라우드 채택 (rev 128) |

세 번 다 **6초 예산 안에서 끝났다.** 0.88초는 인증만 기다린 경우이고, 3.3~3.7초는
인증 + 서버 조회를 합친 값이다(예산 하나를 두 단계가 나눠 쓰는 설계, §11.2).

62단계가 관측한 로그인 지연이 **1.06초**였으므로(§11.1), 6초는 그 3배 이상의 여유다.
다만 **네트워크가 느린 환경의 표본은 없다** - 지연이 6초를 넘으면 예전과 같은 결과
(로컬 부팅)가 되지만 그만큼 첫 화면이 늦는다.

## 11.15 오프라인 타임아웃 — 시도와 막힘 _(§11.16에서 해소됨)_

> **⚠️ 이 절은 첫 시도가 막혔던 기록이다. 결과는 §11.16을 보라** - 데이터 전송이
> 되는 USB로 다시 연결하니 잡혔고, 검증을 마쳤다. 남겨 두는 이유는 "USB가 안
> 잡힌다"의 원인 판별 과정이 다음에도 쓰이기 때문이다.

USB 연결을 시도했으나 **Windows가 기기를 USB로 인식하지 못했다**(`Get-PnpDevice`에
Samsung 휴대전화 항목 없음, SSD만). 충전 전용 케이블이나 포트·드라이버 문제로
보이며 adb 이전 단계라 이 자리에서 풀 수 없다.

무선 디버깅은 Wi-Fi를 타므로 **Wi-Fi를 끄면 adb도 함께 끊긴다** - 오프라인 부팅을
원격으로 관측할 방법이 없다.

### 무엇이 검증됐고 무엇이 안 됐나

| | 상태 |
|---|---|
| 인증이 오면 서버를 본다 | ✔ 실기 3회 (§11.14) |
| 인증·조회가 **제한 시간 안에 끝난다** | ✔ 3회 모두 6초 이내 |
| 제한 시간을 **넘겼을 때** 로컬로 진입한다 | **미검증(실기)** — EditMode 3개 검사가 규칙 수준에서 잰다 |

EditMode가 덮는 것: `AnIdentityThatNeverArrivesFallsBackToLocal` ·
`AServerFetchThatNeverCompletesFallsBackToLocal` · `OfflineAlwaysEntersTheGame`.
셋 다 시계를 손에 쥐고 마감을 넘겨 `LocalOnly`로 끝나는지 확인한다.

**그래도 실기에서 밟은 것은 아니다.** 남은 절차는 데이터 전송이 되는 USB 케이블로
연결한 뒤:

```
adb -s <USB> shell svc wifi disable     # USB로 붙어 있으므로 adb는 산다
adb -s <USB> shell am force-stop com.studio202.onikiri
adb -s <USB> shell monkey -p com.studio202.onikiri -c android.intent.category.LAUNCHER 1
#   → "인증 준비를 기다립니다" 뒤 6초 안팎에 "로컬 사용" + "Boot apply #1"
adb -s <USB> shell svc wifi enable
```


## 11.16 ★ 오프라인 타임아웃 — **실기 검증 완료** (2026-08-27 22:15, Galaxy Z Flip 3, USB)

§11.15에서 "끝내 미검증"으로 적었던 항목이다. **데이터 전송이 되는 USB로 다시
연결하니 잡혔다**(`R5CR81SQTXB` 시리얼 직결). USB로 붙어 있으므로 Wi-Fi를 꺼도
adb가 살아 원격 관측이 가능해졌다.

### 절차

```
adb -s R5CR81SQTXB shell svc wifi disable      → wifi_on = 0, adb 살아있음 확인
adb -s R5CR81SQTXB shell monkey ... LAUNCHER 1
```

기준선: `baseRevision = 128` · pending 없음 · 앱 미실행

### 결과

```
22:15:33.721  [CloudSave] 사이드카가 있습니다. 인증 준비를 기다립니다 (최대 6초).
22:15:33.736  [AppCheck] provider = Play Integrity
22:15:34.358  [CloudScores] 기존 로그인 재사용. uid = A9aMgx…      ← 0.64초
22:15:34.499  [CloudSave] 부팅 선택: 서버 확인 없음 - 로컬 사용     ← 로컬 진입
22:15:34.574  [Onikiri] Boot apply #1 - … (방치 보상 1회)
```

**부팅 시작 → 적용까지 0.85초.** 오프라인에서도 게임에 정상 진입했고, 로컬 세이브
(43층 · Lv61 · 보석 557)가 그대로 얹혔다. 사이드카는 128 그대로, pending 없음.

| 요구 | 결과 |
|------|------|
| 오프라인에서 **반드시 로컬로 진입** | ✔ 0.85초 만에 진입 |
| 제한 시간(6초)을 넘겨 매달리지 않는다 | ✔ 6초를 **쓰지도 않았다** |
| 로컬 저장이 그대로 얹힌다 | ✔ 43층 · Lv61 · 보석 557 |
| `Boot apply` 1회 · 방치 보상 1회 | ✔ |

### 왜 6초를 다 쓰지 않았나 — 설계대로다

두 단계 모두 **빠르게 실패**했기 때문이다:

1. **인증**: Firebase 세션이 기기에 캐시돼 있어 오프라인에서도 즉시 복구된다
   (`기존 로그인 재사용`). 기다림이 0.64초에 끝났다.
2. **서버 조회**: `Source.Server`는 오프라인을 **즉시** 알아차린다. 6초를 기다리지
   않고 `Offline`을 돌려주므로 `serverChecked = false` → `LocalOnly`.

22:15:54의 로그가 그 사실을 그대로 적는다:

```
W [CloudScores] 제출 전 조회 실패: FirestoreException: Failed to get document from
  server. (However, this document does exist in the local cache. …)
```

즉 **6초는 상한이지 대기 시간이 아니다.** 오프라인 사용자가 6초를 손해 보지 않는다.

### Wi-Fi 복구

```
adb -s R5CR81SQTXB shell svc wifi enable      → wifi_on = 1
22:17:37.337  [CloudSave] 주기 동기화 보류 - 다른 기기가 세션을 쥐고 있습니다
                          (놓거나 180초 만료 뒤 인수).
```

네트워크가 돌아오자 동기화가 재개를 시도했고, 다른 기기(Note 20)가 세션을 쥐고
있어 정상적으로 보류됐다. **네트워크 복구 후 자동으로 이어진다**는 것까지 확인됐다.


---

# 12. Step 62.1.1 — 저장 확정 순서 및 App Check 우회 경로 폐쇄

62.1 검토에서 나온 **P1 두 개**를 닫는다. 새 기능은 없다.

## 12.1 P1-1의 실제 원인 — 사슬이 디스크보다 먼저 확정됐다

`CloudSaveCoordinator.Finish()`가 `DownloadCloud`/`InSync` 판정 **직후** 사이드카를
서버 revision으로 옮겼다. 그런데 고른 클라우드 한 벌이 디스크에 들어가는 것은
그 뒤 `GameSession.BootWith()` → `Save()`다.

### 수정 전 순서

```
클라우드 선택 → sidecar를 서버 revision으로 이동 → GameSession 적용 → SaveSystem.Save
                └── 여기서 앱이 죽거나 Save가 실패하면 ↓
```

남는 상태:

```
로컬 세이브 파일 = 옛 기록
sidecar base     = 최신 서버 revision
sidecar syncSha  = 최신 서버 지문
```

다음 동기화의 `localChanged` 판정은 `lastSyncedStateSha`를 기준으로 한다. 위 상태에서
옛 로컬은 그 지문과 다르므로 **"서버에서 파생된 변경분"**으로 읽히고, 그대로 올라간다 -
다른 기기의 최신 진행 위에 옛 기록이 얹히는 길이 열린다. 이 설계 전체가 막으려는
사고가 정확히 그것이다.

`CloudConflictPanel.ChooseCloud()`에도 **같은 결함**이 있었다: `SaveSystem.Save(cloud)`의
결과를 보지 않고 곧바로 `AdoptServerHead()`를 불렀다.

### 수정 후 순서 (계약)

```
클라우드 선택
  → 메모리 적용
  → 방치 보상 1회
  → 로컬 세이브 저장 성공          ← 여기가 사실이 된 뒤에야
  → sidecar 서버 사슬 확정
  → 이후 자동 동기화 허용
```

구현:

| 자리 | 무엇을 했나 |
|------|-------------|
| `SaveSystem.Save` | `void` → **`bool`** (성공 여부를 값으로 돌려준다) |
| `CloudSaveCoordinator.Finish` | 확정 대신 **`ArmChain`** — revision·지문·uid를 "확정 대기"로 보관 |
| `CloudSaveCoordinator.NoteLocalSaveCommitted` | 대기를 꺼내 확정. **예약을 먼저 지워** 정확히 한 번만 옮긴다 |
| `GameSession.Save` | `if (SaveSystem.Save(data)) CloudSaveCoordinator.NoteLocalSaveCommitted();` |
| `CloudConflictPanel.ChooseCloud` | 저장 실패 시 `AdoptServerHead()`를 부르지 않고 선택 화면을 유지 |

**"사이드카 저장 위치만 몇 줄 아래로 옮기는" 방식은 쓰지 않았다** - 저장 실패를
값으로 받아 분기한다. 실패하면 예약이 살아남아 **다음 자동 저장(30초) 성공**이
마저 확정한다.

## 12.2 저장 실패·재시도 시 sidecar 상태표

`rev 5`에서 동기화된 기기가 서버 `rev 9`를 채택하는 경우:

| 시점 | 로컬 세이브 파일 | sidecar base | 확정 대기 | 판정 근거 |
|------|-----------------|--------------|-----------|-----------|
| 채택 직후 | 옛 기록 | **5** | 9 | 디스크가 아직 사실이 아니다 |
| 저장 성공 | 서버 기록 | **9** | 없음 | 디스크와 사슬이 같은 것을 가리킨다 |
| 저장 **실패** | 옛 기록 | **5** | 9 | 옛 로컬 ↔ 옛 사슬이 짝이 맞는다 |
| 실패 후 재저장 성공 | 서버 기록 | **9** | 없음 | 한 번만 옮긴다 |
| 실패 상태로 앱 종료 | 옛 기록 | **5** | (사라짐) | 다음 부팅이 옛 짝을 그대로 읽는다 |

마지막 줄이 요점이다. 옛 로컬과 옛 사슬이 **짝이 맞으므로** 다음 부팅은 "로컬은
변한 적 없다"로 읽고, 서버가 앞섰다면 조용히 받아온다. 사슬만 앞서 있었다면 그
자리에서 옛 기록을 올렸을 것이다.

사슬이 **없던** 기기도 같다 - 저장 성공 전에는 사이드카를 **만들지 않는다.**
만들어 두면 그 사슬이 가리키는 기록이 디스크에 없다.

## 12.3 P1-2 — App Check 직접 읽기 우회 경로

`RunAsync()`의 조기 중단만으로는 부족했다. 테스트 패널의 `정본 읽기` 버튼이
`ReadCanonicalAsync()`를 **직접** 부르고, 그 버튼의 활성 조건이
`FirebaseAppCheckBootstrap.IsConfigured` 하나였다.

**provider가 붙었다**와 **토큰이 나왔다**는 다른 사실이다. 갱신이 실패한 상태에서
그 버튼을 누르면 미검증 요청이 그대로 나갔다.

### 고친 자리 셋

1. **`ReadCanonicalAsync()` 안쪽** — `LastTokenOk == false`면 `TokenlessAbort`를
   돌려주고 **Firestore를 부르지 않는다.** 방어를 여기 둔 이유는 호출자가 하나
   늘 때 같은 구멍이 다시 열리지 않게 하기 위해서다.
2. **`RefreshTokenAsync()` 진입부** — `LastTokenOk = false`를 **먼저** 내린다.
   갱신이 어디서 끝나든 옛 성공이 살아남지 않는다.
3. **에디터 버튼** — `DisabledScope`를 갈라 정본 읽기는 `LastTokenOk`를 본다.
   (눌리지 않는 버튼이 설명이 된다 - 진짜 방어는 ①이다)

미검증 요청을 일부러 보내는 신규 버튼·우회 메서드는 만들지 않았다. 개발 빌드
오버레이의 전체 진단 정상 경로와 출시 빌드 컴파일 가드는 그대로다.

## 12.4 신규·갱신 테스트

### 저장·사슬 순서 (`CloudChainCommitOrderTests`, 신규 9개)

| 이름 | 검증 계약 |
|------|-----------|
| `AdoptingTheCloudDoesNotMoveTheChainYet` | 채택 직후 base가 그대로, 확정 대기만 선다 |
| `TheChainMovesOnlyAfterTheLocalSaveSucceeds` | 저장 성공 뒤 base·payloadSha·stateSha가 서버 값으로 |
| `AFailedSaveLeavesTheChainWhereItWas` | 저장 실패 시 base 유지, 예약 살아 있음 |
| `ARetriedSaveCompletesThePendingChainExactlyOnce` | 실패 → 성공에서 한 번만 이동, 재호출은 무해 |
| `AKilledAppLeavesTheOldSaveAndOldChainMatching` | 종료 모사 뒤 디스크 세이브 지문 == 사슬 지문 |
| `AChainlessDeviceCreatesNoSidecarBeforeTheSaveSucceeds` | 저장 전 사이드카 **없음** |
| `AChainlessDeviceGetsItsChainAfterTheSaveSucceeds` | 저장 뒤 서버 revision으로 생성 |
| `TheNextSyncAfterAdoptionUploadsInsteadOfConflicting` | 방치 보상 변경분이 `UploadLocal`(가짜 충돌 아님) |
| `TheFailureSeamDisarmsItself` / `…CompiledOutOfReleaseBuilds` | seam이 1회성이고 출시에 없다 |

`ApplyBoot`·방치 보상 1회는 기존 `EveryPathReportsExactlyOneChoice` ·
`TheOfflineRewardStillHasExactlyOneGrantSite`가 계속 잰다.

### App Check (`AppCheckPolicyTests`, 신규 4개)

| 이름 | 검증 계약 |
|------|-----------|
| `ADirectCanonicalReadWithoutATokenNeverReachesTheServer` | 직접 호출해도 **`ServerCallsForTests == 0`** |
| `AFailedRefreshDoesNotLeaveAStaleSuccess` | 갱신 실패 후 `LastTokenOk == false`, 직접 읽기도 0회 |
| `AVerifiedTokenPassesTheTokenGate` | 토큰 확인 시 토큰 게이트를 통과(막지 않음) |
| `TheEditorCanonicalButtonChecksTheToken` | 버튼 활성 조건에 `LastTokenOk` 포함 |

기존 `AFailedTokenStopsTheDiagnosticsBeforeAnyServerCall`(RunAsync 조기 중단)도 유지·통과.

### 갱신한 기존 테스트 (계약이 **강해져서**)

62.1의 두 검사는 "판정 직후 사슬이 옮겨진다"를 재고 있었다. 62.1.1에서 그 계약이
**"저장 성공 뒤에 옮긴다"**로 강해졌으므로 같은 이름으로 새 계약을 재게 고쳤다 -
약화도 삭제도 아니다.

- `BootIdentityWaitTests.AdoptingTheCloudAlsoMovesTheChain`
- `BootIdentityWaitTests.AChainlessDeviceGetsAChainWhenItMatchesTheServer`

## 12.5 검증 결과

전부 **명령행 배치**, Unity Editor 종료 상태. PlayMode에 MCP `run_tests` 미사용.

| 스위트 | total | passed | failed | skipped | inconclusive | XML |
|--------|-------|--------|--------|---------|--------------|-----|
| **EditMode** | **952** | **952** | 0 | 0 | 0 | `Builds/TestResults/editmode-62_1_1.xml` |
| **PlayMode** | **44** | **44** | 0 | 0 | 0 | `Builds/TestResults/playmode-62_1_1.xml` |
| **Firestore Emulator** | **47** | **47** | 0 | — | — | `npm --prefix tools/firestore-rules-tests test` |

- 컴파일 오류 0 · 신규 경고 0
- 62.1 종료 시 938 → **952** (+14)
- 로그: `Builds/TestResults/editmode-62_1_1.log` · `playmode-62_1_1.log` ·
  `build-62_1_1.log`
- `git diff --check` — 공백 오류 0 (CRLF 경고는 이 저장소의 기존 설정)
- 배치가 지운 Sentis define은 `git checkout ProjectSettings`로 복원

### 사용자 실사용 세이브 (전후 동일)

| 파일 | SHA-256 |
|------|---------|
| `onikiri_save.json` | `1be951ecd04c5447b698a16d4e6317604538e3387e17267849b7b9df3ed15f30` |
| `onikiri_save.backup.json` | `ed789730f82b94e17ad6640478d5525f2766f58b63b5b47b63e5caeab421fc61` |
| `onikiri_save.json.v20-backup-step3` | `82d7b335632d4ed961a3b8dcda67e217351a1694d78f99369337ff11e4e66a15` |

셋 다 62.1 종료 시점과 같다. `SaveSandbox`가 Dispose마다 바이트를 대조하므로
952개 전량 통과가 매 실행 그것을 증명한다.

## 12.6 실기 재검증 (2026-08-27 23:54, Galaxy Z Flip 3, USB)

기기 B의 사슬은 **128**, 서버는 **156**(기기 A가 올려 둔 것). 클라우드를 채택하는
경로를 밟아 **새 순서가 로그에 그대로 찍히는지** 봤다.

```
23:54:18.087  [CloudSave] 충돌 해결: 클라우드 기록 사용 (rev 156)
23:54:18.391  [CloudSave] 서버 사슬 rev 156 확정 대기 - 로컬 저장이 성공해야 옮긴다.   ★ 예약
23:54:18.392  [CloudSave] 부팅 선택: 같은 기록 - 로컬 사용 (서버 rev 156)
23:54:18.397  [Onikiri]   Boot apply #1 - … (방치 보상 1회)                          ← 적용+보상
23:54:18.402  [CloudSave] 서버 사슬 확정: rev 156 (로컬 저장 완료 후).                ★ 확정
23:54:19.059  [CloudSave] 주기 동기화 완료 (rev 157)                                 ← 정상 업로드
```

| 요구 | 결과 |
|------|------|
| 서버 최신 기록을 채택 | ✔ rev 156 |
| **로컬 저장 완료 후** sidecar가 서버 revision으로 이동 | ✔ `확정 대기` → `Boot apply` → `확정` 순서 |
| 이후 자동 동기화에서 즉시 충돌하지 않음 | ✔ rev 157 정상 업로드 |
| `Boot apply` 1회 · 방치 보상 1회 | ✔ |

채택 후 사슬: `base = 157` · pending 없음.

> 저장 실패 자체는 실기에서 억지로 만들지 않았다(지시서대로). 그 갈래는
> `SaveSystem.FailNextSaveForTests` seam으로 EditMode 5개가 잰다(§12.4).

## 12.7 남은 출시 차단 요소

62.1.1로 바뀐 것은 없다. 그대로 넷이다.

1. **production 서명 인증서 없음** — 등록된 SHA-256은 디버그 keystore 것(§3.2)
2. **`PLAY_RECOGNIZED` 요구 해제 상태** — 출시 전 되돌려야 한다(§3.4)
3. **Editor Debug Provider 미등록** — 콘솔에서 디버그 토큰 발급 필요(§2.4)
4. **Firestore enforcement 미활성**(§8) — 이번 단계에서도 켜지 않았다


---

# §13 Step 62.1.2 — 저장 실패가 업로드를 막는다

- **회차** 62.1.2 (2026-08-28)
- **기준** 62.1.1 종료 상태 (커밋 없음, 같은 작업 트리)
- **세이브 버전** SaveData v21 유지 · **firestore.rules 변경 없음**
- **금지 항목 준수** enforcement 미활성 · Auth 미변경 · 씬/프리팹 미변경 · 커밋·푸시 없음

## 13.1 무엇이 부러져 있었는가

62.1.1이 사슬을 디스크 뒤로 미뤘지만, **업로드 자체는 그대로였다.**
`GameSession.Save()`가 이런 모양이었다:

```csharp
if (SaveSystem.Save(data)) CloudSaveCoordinator.NoteLocalSaveCommitted();
CloudSaveSync.NoteSaved(data);          // ← 저장이 실패해도 돌았다
```

`NoteSaved`는 "이 스냅샷을 클라우드로 올려라"는 등록이다. 저장이 실패한 뒤에도
그것이 돌았으므로 **디스크에 없는 메모리 한 벌이 서버의 정본이 될 수 있었다.**

배선 쪽에도 같은 구멍이 있었다. `CloudSaveSync.SaveRequested`가 `Action`이라
urgent 커밋이 부르는 저장의 **성공 여부가 `Tick`으로 돌아오지 않았다.**

```csharp
var save = SaveRequested;
if (save != null) save();               // 실패해도 아래로 흘러갔다
if (pendingData == null) return;
… CommitAsync(…)
```

### 두 번째 갈래가 더 조용하다

저장이 실패하면 `pendingData`에는 **실패 전의 더 낡은 스냅샷**이 남아 있다.
위 흐름은 그것을 대신 올린다. 로그에는 정상 업로드와 똑같이 `주기 동기화 완료
(rev N)`이 찍히고, 서버 revision은 정상적으로 하나 오른다 - **로그만 봐서는
구분되지 않는다.** 그리고 다음 부팅은 그 서버 기록을 근거로 판정한다.

재화가 복제되지는 않는다(전체 스냅샷이라 지불과 상품이 함께 빠진다, 설계 §3).
사라지는 것은 **지불 이후의 진행**이다.

## 13.2 계약

> **저장이 실패하면 그 커밋은 없다. urgent 표시는 지우지 않는다.**

지우면 안 되는 이유가 계약의 절반이다. 지불은 여전히 서버에 없다 - urgent을
지우면 그 지불은 다음 자동 저장(30초)과 디바운스(120초)를 지나서야 올라가고,
그 사이에 앱이 죽으면 그대로 사라진다. urgent은 **살아남아 다음 저장 성공에서
최신 한 벌로** 올라가야 한다.

| 사건 | 로컬 세이브 | `pendingData` | urgent | 커밋 |
|------|------------|---------------|--------|------|
| 저장 성공 | 새 기록 | 새 기록 | 유지 | **1회** |
| 저장 실패 | 옛 기록 그대로 | **옛 기록 그대로** | **유지** | **0회** |
| 실패 후 재시도 성공 | 새 기록 | 새 기록 | 유지 | **1회 (최신)** |
| 커밋 성공 | — | 비움 | 해제 | — |

## 13.3 고친 자리 넷

### ① `GameSession.Save()` — `bool`을 돌려주고, 실패에서 끝난다

```csharp
public bool Save()
{
    if (!loaded) return false;
    …
    if (!SaveSystem.Save(data)) return false;   // ★ NoteSaved·사슬 확정 둘 다 안 한다

    CloudSaveCoordinator.NoteLocalSaveCommitted();
    CloudSaveSync.NoteSaved(data);
    return true;
}
```

사슬 확정 예약도 그대로 둔다 - 로컬이 아직 사실이 아니기 때문이다.

### ② `CloudSaveSync.SaveRequested` — `Action` → `Func<bool>`

```csharp
if (save != null && !save())
{
    MarkFailure();                              // 재시도 유예 30초를 건다
    Debug.LogWarning(Tag + " 로컬 저장이 실패해 urgent 커밋을 멈춥니다 "
                     + "(urgent 예약은 유지 - 다음 저장 성공이 올립니다).");
    return;                                     // ★ pendingData 가드보다 앞이다
}
```

`return`이 `if (pendingData == null) return;`보다 **앞에** 있는 것이 13.1의
두 번째 갈래를 막는다 - 낡은 후보가 남아 있어도 여기서 끝난다.

### ③ `NoteLocalSaveCommitted` — sidecar가 디스크에 남은 뒤에만 예약을 푼다

```csharp
if (chain != null
    && chain.MarkSynced(chainRevision, chainPayloadSha, chainStateSha, chainUpdatedAt)
    && CloudSaveSidecar.Save(chain))        // ★ 저장 성공까지 확인한 뒤
{
    chainArmed = false;
    …
    return;
}
Debug.LogWarning(… "예약을 유지하고 다음 저장 성공에서 다시 시도합니다.");
```

62.1.1은 들어오자마자 `chainArmed = false`를 했다. sidecar 쓰기가 실패하면
**아무도 다시 시도하지 않고**, 로컬은 서버 기록인데 사슬은 옛 자리에 남는다 -
62.1이 실기에서 본 그 증상(§11.11)이 그대로 재현되는 상태다.

### ④ pause 경로 — `OnAppPaused(bool localSaveOk)`

지시서가 "자동 저장·pause·focus·quit 경로도 같은 성공 계약"을 요구해 다시 보니
①②③만으로는 **pause가 떨어져 있었다.**

```csharp
private void OnApplicationPause(bool paused)
{
    Save();                                  // 실패해도 결과를 안 봤다
    if (paused) CloudSaveSync.OnAppPaused();  // ← 낡은 pendingData를 그대로 올린다
    …
}
```

`OnAppPaused`는 `pendingData != null && dirtySince >= 0f`만 보고 `CommitThenReleaseAsync`를
띄우므로, urgent 갈래에서 막은 사고가 **pause 갈래로 그대로 나간다.**
실제로 더 쉽게 밟힌다 - 모바일에서는 홈 버튼 한 번이 이 경로다.

```csharp
bool saved = Save();
if (paused) CloudSaveSync.OnAppPaused(saved);
```

`localSaveOk == false`면 커밋을 건너뛰고 **release만** 한다. 세션을 쥐고 나가면
다른 기기가 180초 만료까지 묶인다 - **올리지 않는 것과 잡고 있는 것은 다른 문제다.**

`OnApplicationFocus(false)`와 `OnApplicationQuit`은 클라우드를 건드리지 않고
`Save()`만 부르므로 ①으로 이미 닫혔다. 자동 저장(30초)도 같다.

> **충돌 화면은 이번에 안 건드렸다.** `CloudConflictPanel.ChooseCloud`는
> 62.1.1에서 이미 `SaveSystem.Save(cloud)` 실패를 보고 `AdoptServerHead()` 앞에서
> 돌아가게 고쳤다(§12.3 옆). 이번 회차가 닫은 것은 그 밖의 세 자리다.

## 13.4 검사 seam (출시 빌드에 없다)

전부 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안이다. **플래그는 뒤집혀도
컴파일에서 빠진 코드는 뒤집히지 않는다** — 이 원칙은 62단계와 같다.

| seam | 파일 | 하는 일 |
|------|------|---------|
| `SaveSystem.FailNextSaveForTests` | `SaveSystem.cs` | 다음 세이브 **한 번만** 실패 (스스로 꺼진다) |
| `CloudSaveSidecar.FailNextSaveForTests` | `CloudSaveSidecar.cs` | 다음 sidecar 저장 **한 번만** 실패 |
| `CloudSaveSync.CommitAttemptsForTests` | `CloudSaveSync.cs` | `Tick`이 커밋을 띄운 횟수 |
| `CloudSaveSync.SuppressCommitForTests` | `CloudSaveSync.cs` | 세기만 하고 실제로 안 띄운다 |
| `CloudSaveSync.HasPendingSnapshotForTests` | `CloudSaveSync.cs` | 올릴 후보가 등록됐는가 |
| `CloudSaveSync.UseClockForTests` | `CloudSaveSync.cs` | 재시도 유예 30초를 실제로 안 기다린다 |

### 억제 모드가 건드리는 두 줄

`Tick`의 에디터 네트워크 게이트는 억제 중에 지나간다:

```csharp
if (!SuppressCommitForTests && EditorBlocksNetwork()) return;
```

게이트가 막는 것은 **에디터에서 나가는 실서버 왕복**인데, 억제 중에는 나갈 수
있는 요청 자체가 없다. 그리고 게이트가 여기서 돌아가면 이 회차가 재려는 계약
("저장 실패 뒤 커밋 0회")을 성공 갈래와 구분할 수 없다 - **둘 다 0**이기 때문이다.

억제 갈래는 실제 커밋과 같은 자리에 `committing = true`를 세운다. 없으면 다음
`Tick`이 같은 지불로 커밋을 한 번 더 띄우고, "정확히 한 번"이 **검사에서만**
두 번으로 보인다(실기에서는 `committing`이 막는다).

## 13.5 신규 테스트 — `SaveFailureBlocksUploadTests` (EditMode 11개)

| 지시서 요구 | 검사 |
|-------------|------|
| 1. 저장 실패 시 `NoteSaved` 미호출 | `TheSessionSaveReturnsEarlyWhenTheDiskWriteFails` (소스 순서) + `TheSaveHookReportsSuccess` |
| 2. urgent 저장 실패 시 커밋 0회 | `AFailedSaveStopsTheUrgentCommit` |
| 3. 낡은 `pendingData`도 안 올라간다 | `AStalePendingSnapshotIsNotUploadedInstead` |
| 4. urgent 상태 유지 | `TheUrgentRequestSurvivesAFailedSave` |
| 5. 다음 성공에서 최신 한 벌 **정확히 1회** | `TheNextSuccessfulSaveCommitsTheLatestSnapshotExactlyOnce` |
| 6. 실패 구간에서 revision 미증가 | 같은 검사의 ① 단계 (커밋 0회 = 트랜잭션 없음 = revision 그대로) |
| 7. sidecar 실패 시 예약 유지 | `ASidecarWriteFailureKeepsTheChainReservation` |
| 8. 재시도 성공 후 **한 번만** 해제 | `ARetriedSidecarWriteClearsTheReservationOnce` |
| 9. seam이 Editor/Dev Build 전용 | `TheFailureSeamsAreCompiledOutOfReleaseBuilds` (3개 파일 × 3개 이름 전수) |
| (과방어 회귀) 성공하면 여전히 올라간다 | `ASuccessfulSaveStillCommits` |
| (pause 경로) 저장 결과를 넘긴다 | `ThePausePathCarriesTheSaveResult` |

> **6번을 "커밋 0회"로 잰 이유.** revision은 서버 트랜잭션 안에서만 오른다
> (`CloudSaveStore.CommitAsync`). 커밋을 **띄우지 않았다**를 증명하면 revision이
> 오를 자리가 없다는 것이 따라온다. Emulator로 다시 재지 않은 것은 그래서다 -
> 같은 사실을 두 번 재는 대신, 재는 자리를 하나로 둔다.

62.1.1의 `CloudChainCommitOrderTests`는 그대로 둔다. 이번 두 검사(7·8)는
**sidecar 쓰기가 실패하는 갈래**로, 그 파일이 다루지 않던 자리다.

## 13.6 검증 결과

| 스위트 | total | passed | failed | skipped |
|--------|-------|--------|--------|---------|
| **EditMode** | **963** | **963** | 0 | 0 |
| **PlayMode** | **44** | **44** | 0 | 0 |
| **Firestore Emulator** | **47** | **47** | 0 | — |

- 컴파일 오류 0 · 신규 경고 0 (`read_console` types=error → 0건)
- 62.1.1 종료 시 952 → **963** (+11)
- `git diff --check` — 공백 오류 0 (CRLF 경고는 이 저장소의 기존 설정)

### 사용자 실사용 세이브 (전후 동일)

| 파일 | SHA-256 |
|------|---------|
| `onikiri_save.json` | `1be951ecd04c5447b698a16d4e6317604538e3387e17267849b7b9df3ed15f30` |
| `onikiri_save.backup.json` | `ed789730f82b94e17ad6640478d5525f2766f58b63b5b47b63e5caeab421fc61` |
| `onikiri_save.json.v20-backup-step3` | `82d7b335632d4ed961a3b8dcda67e217351a1694d78f99369337ff11e4e66a15` |

셋 다 §12.5와 같다. 실사용 루트에 `onikiri_cloud_state.json`이 생기지 않은 것도
확인했다 - `SaveSandbox`가 sidecar까지 함께 격리한다.

- 로그: `Builds/TestResults/editmode-62_1_2.xml` · `playmode-62_1_2.xml` (+ 각 `.log`)
- ⚠️ **배치에 `-nographics`를 붙이면 EditMode가 하나 진다** — `SkillVfxTests.PozacEffects_AreRecoloredIntoTheBloodPalette`가 `Graphics.Blit` → `RenderTexture`로 픽셀을 읽는다(임포터가 읽기를 막은 텍스처). GPU가 없으면 불투명 픽셀은 세지지만 R-G가 전부 0이라 "붉은 계열 0.0%"로 읽힌다. **클라우드와 무관**하며 `-nographics` 없이 돌리면 963/963이다.
- 배치가 지운 Android 측 `SENTIS_ANALYTICS_ENABLED` define은
  `git checkout ProjectSettings`로 복원했다(작업 트리 깨끗함 확인)

### 실행 경로 — EditMode만 MCP였다

처음에는 Unity Editor가 열려 있어 배치가 `another Unity instance`로 거부됐다.
**에디터를 임의로 종료하지 않는다**(60단계 규칙)라 거기서 멈추고 사용자에게
물은 뒤, 승인을 받아 `CloseMainWindow`로 **정상 종료**하고 배치를 돌렸다.

| 스위트 | 중간 확인 | **최종 수치** |
|--------|-----------|------------|
| EditMode | MCP `run_tests` 962/962 (에디터 열린 동안) | **명령행 배치 963/963** |
| PlayMode | — | **명령행 배치 44/44** (MCP 미사용 — 지시서 준수) |
| Emulator | — | `npm --prefix tools/firestore-rules-tests test` **47/47** |

표의 수치는 전부 **에디터 종료 상태의 배치**이다. 중간 MCP 실행 962와 최종 963의
차이는 그 사이에 pause 경로(§13.3 ④)를 닫으며 늘린 검사 하나다.

> Firebase Emulator는 Java를 찾지 못해 한 번 멈춘다. 이 기계에는 독립 JDK가
> 없고 Unity Android 모듈의 OpenJDK 17만 있다 —
> `PATH=.../AndroidPlayer/OpenJDK/bin`을 앞에 두면 그대로 돌아간다.

## 13.7 이번 회차가 **재지 않은 것**

| 항목 | 상태 |
|------|------|
| 실기에서 저장 실패를 만들어 본 것 | **없음** — 지시서가 실사용 세이브 훼손을 금지한다. seam으로 EditMode가 잰다 |
| Emulator로 revision 미증가를 다시 잰 것 | **안 했다** — 커밋 0회로 이미 증명된다(§13.5 각주) |
| App Check enforcement | 여전히 **꺼짐** (§8) — 이번 단계도 금지 항목 |
| firestore.rules | **변경 없음** → 재배포 없음 |

§12.7의 출시 차단 요소 넷은 그대로다. 62.1.2가 그중 어느 것도 건드리지 않았다.
