# ONIKIRI 55단계 — 계정 연동 (익명 → 구글, 정체성 영속화 + 기록 복구)

> 54단계에서 랭킹이 섰지만 정체성이 **익명 uid = 기기 하나**에 묶여 있었다.
> 앱을 지우면 새 uid가 생기고 그 사람의 기록은 주인 없는 문서로 남는다.
> 이 스텝은 그 구멍을 막는다.

---

## 0. 한 줄 요약

익명 계정에 구글을 **붙이고**(uid 유지 → 문서·순위 그대로), 이미 있는 계정이면
연동을 포기하고 **로그인으로 갈아타** 원래 uid로 돌아간다(복구). 그리고 두
문서의 도달층 중 **높은 쪽을 남긴다**(병합).

밸런스·게임 로직 **영향 0**. 도달층은 읽기만. 세이브 **v19 그대로**(새 필드 0).

---

## 1. Part A — 콘솔 셋업

| 항목 | 처리 |
|---|---|
| 디버그 SHA-1/SHA-256 추출 | 자동 (Unity 동봉 OpenJDK의 keytool) |
| SHA를 Firebase 앱에 등록 | 자동 (Firebase Management API) |
| Google 공급자 ON | **사용자 수동** — MCP에 provider 토글 툴이 없다 |
| google-services.json 갱신 | 자동 |
| Web Client ID 확보 | 자동 |

```
SHA1   : 20:86:E9:D2:53:38:2A:D6:10:07:AE:5A:17:1B:71:A3:02:5B:8C:86
SHA256 : 02:D1:40:1C:F6:8B:08:D7:49:B4:42:98:3B:ED:FB:18:B5:4B:EE:05:41:81:3C:E9:06:53:41:CE:9E:95:E4:3B
Web Client ID : 598938331304-nt0tg1rlqnflnn2evnga4ffck698c88v.apps.googleusercontent.com
```

`google-services.json`의 `oauth_client`에 두 항목이 생겼고, 그중 안드로이드
클라이언트(`client_type: 1`)의 `certificate_hash`가 위 SHA-1과 **정확히
일치**한다 — SHA 등록이 실제로 반영됐다는 교차 증명이다.

---

## 2. 플러그인 선택 — 관례를 따르지 않았다

유니티에서 구글 로그인의 관례적 답은 **google-signin-unity**(googlesamples)다.
쓰지 않았다. 근거 셋:

1. **아카이브됐다.** 2021년 이후 커밋이 없다.
2. **감싸고 있는 API가 폐기됐다.** legacy Google Sign-In(`GoogleSignInClient`)은
   구글이 명시적으로 폐기하고 Credential Manager로 옮기라고 안내하는 경로다.
   출시할 앱의 로그인을 *폐기된 API를 감싼 방치된 래퍼* 위에 올릴 수는 없다.
3. **Play Games 플러그인은 답이 아니다.** 안드로이드 전용이고, Firebase에서
   `playgames.google.com`이라는 **다른 공급자**가 된다 — iOS 재사용이 0이고
   정체성 모델도 갈린다.

대신 **얇은 네이티브 플러그인을 직접 썼다**(`OnikiriGoogleAuth.java`, 약 160줄).
가능한 이유는 하는 일이 적어서다 — 계정 선택창을 띄우고 **ID 토큰 문자열 하나**를
돌려주는 것이 전부이고, 연동·복구·병합은 전부 C# 쪽에 있다.

의존성은 EDM4U로 선언한다(`OnikiriAuthDependencies.xml`) — Firebase가 자기
것을 넣는 것과 **같은 경로**다. gradle 템플릿을 손으로 고치면 다음 resolve에서
그 블록이 통째로 다시 쓰이며 조용히 사라진다.

```
androidx.credentials:credentials:1.3.0
androidx.credentials:credentials-play-services-auth:1.3.0
com.google.android.libraries.identity.googleid:googleid:1.1.1
```

**`GetGoogleIdOption`이 아니라 `GetSignInWithGoogleOption`**을 쓴다. 전자는
"이미 기기에 있는 계정"을 바텀시트로 제안하는 흐름이라 조건이 안 맞으면
`NoCredentialException`으로 조용히 끝난다. 우리 자리는 사람이 버튼을 **명시적으로
누른** 뒤이므로 언제나 계정 선택창이 떠야 한다.

### iOS 재사용성

네이티브 코드는 재사용되지 않는다(당연히 — 안드로이드 API다). 재사용되는 것은
**그 위의 전부**다: `IAuthProvider` 인터페이스, `AccountLink`의 연동·복구·병합
흐름, 계정 UI, 테스트. 다음 스텝은 `AppleSignInProvider` 하나만 채우면 된다.

---

## 3. 구조 — 다섯 겹

```
IAuthProvider          공급자 하나를 증명하는 방법. **애플이 두 번째로 들어올 자리**
├ GoogleSignInProvider 구글 구현 + 웹 클라이언트 ID
└ AppleSignInProvider  스텁 (IsAvailable = false)

GoogleIdTokenBridge    안드로이드 JNI. **이 파일 밖에 AndroidJavaObject가 한 줄도 없다**
AccountLinkPolicy      순수 규칙 (Firebase·유니티 0줄) ← EditMode 테스트 대상
AccountLink            연동·복구·병합 오케스트레이션
CloudScores            (수정) 세션 공유 · uid 교체 훅 · 병합 전 조회
```

`AccountLinkPolicy`를 가른 이유는 `LeaderboardPolicy`와 같다 — 여기 적힌 것은
네트워크의 성질이 아니라 이 게임의 성질이고, 그래서 Firebase 없이 검사된다.
이 스텝의 실체가 네트워크 왕복이라 **증명 가능한 부분을 최대한 이쪽으로
끌어내는 것**이 그 파일의 존재 이유다.

---

## 4. 세 갈래 — 이 스텝의 전부

| 갈래 | 언제 | 무엇을 하나 | uid |
|---|---|---|---|
| **Link** | 처음 연동 | `LinkWithCredentialAsync` | **유지** → 문서·순위 그대로 |
| **Recover** | `credential-already-in-use` | `SignInAndRetrieveDataWithCredentialAsync` | **교체** → 원래 계정으로 |
| **Nothing** | 이미 붙음 / 취소 / 미지원 | 아무것도 | - |

### AlreadyInUse만이 Recover로 간다

이것이 가장 중요한 판단이다. 복구는 **지금 기기의 익명 uid를 버리는** 동작이다.
"아무 실패나 일단 로그인으로 갈아탄다"로 만들면, 네트워크가 한 번 흔들린 사람이
자기 진행이 붙어 있던 uid를 잃는다. 테스트가 다섯 가지 실패 전부에 대해
`AreNotEqual(Recover)`를 검사한다.

Firebase가 주는 오류 셋(`CredentialAlreadyInUse` ·
`AccountExistsWithDifferentCredentials` · `EmailAlreadyInUse`)을 함께 받는다.
셋은 "이 구글 계정은 이미 임자가 있다"는 같은 사실의 다른 표현이고, 어느 것이
오는지는 계정에 이메일이 붙어 있는지 등으로 갈린다.

### 취소는 실패가 아니다

사람이 계정 선택창을 닫는 것은 이 기능에서 **가장 흔한 종료 경로**다. 자바가
`GetCredentialCancellationException`을 `"cancelled"`로 좁히고, C#이 그것을
`AuthFailure.Cancelled`로 옮기고, `TextFor`가 **빈 문자열**을 돌려준다 — 안
하겠다고 한 사람에게 결과를 통보하지 않는다.

⚠️ 이 문자열이 두 언어에 두 벌 적혀 있다. 갈리면 취소가 오류로 읽힌다.
테스트가 자바 파일을 읽어 C#의 번역표와 대조한다(54단계에서 규칙 파일의 상수를
대조한 것과 같은 처리).

---

## 5. 병합 — 높은 쪽이 남는다

```
merged = max(local, abandoned, recovered)
```

| 상황 | local | 버릴 문서 | 복구 문서 | 결과 | 쓰는가 |
|---|---|---|---|---|---|
| 재설치 직후 | 1 | 0 | 171 | **171** | 아니오 (이미 최고) |
| 익명으로 더 감 | 500 | 500 | 171 | **500** | 예 |
| 백업 복원 | 12 | 500 | 171 | **500** | 예 |

`abandoned`를 굳이 세는 이유는 `local`과 늘 같지는 않아서다 — 세이브를 백업에서
되돌린 기기는 로컬이 서버보다 낮다. 그 경우에도 자기가 올렸던 기록은 자기 것이다.
그래서 갈아타기 **전에** 그 문서를 먼저 읽는다(`FetchStageOfAsync`) — uid를 바꾼
뒤에는 읽을 자격을 잃는다.

병합 쓰기는 **조건부 제출(`SubmitIfHigherAsync`)을 그대로 쓴다.** 후퇴 방어·이름
갱신·범위 검사가 전부 그 한 경로에 모여 있어서, 병합만 다른 문을 쓰면 그 검사들이
병합에서만 빠진다.

### 버려진 익명 문서는 지우지 않는다

규칙 4-B가 클라이언트 delete를 막아 두었다(`allow delete: if false`). 그것을 이
스텝 때문에 여는 것은 거래가 맞지 않는다 — 삭제 권한이 열리면 가장 먼저 생기는
피해자는 "남의 uid를 알아낸 사람"이 아니라 **자기 기록을 실수로 날린 사람**이라고
그 규칙에 이미 적어 두었다.

남는 문서가 랭킹을 더럽히지 않는가? 더럽히지 않는다 — 그 값은 병합으로 이미
복구된 계정에 옮겨졌고, 그 uid로 다시 로그인할 방법이 없으므로 더 자라지 않는다.
죽은 줄 하나가 남는데 그것은 이 사람의 예전 기록이라 잘못된 값도 아니다.

⚠️ **출시 전 교체**: 계정 삭제 요청(GDPR·앱스토어 계정 삭제 의무)이 서면 서버
쪽(Cloud Functions)이 이 정리를 맡아야 한다. 클라이언트에 권한을 주는 방식으로는
풀지 않는다.

---

## 6. 계정 UI — 랭킹 화면에 넣었다

설정이 아니라 **랭킹 화면**에 뒀다. 자리 부족 때문이 아니다. 연동이 실제로 하는
일이 "이 순위표에 선 내가 누구인가"를 정하는 것이고, **복구의 결과 — 내 순위가
1층에서 원래 기록으로 돌아오는 것 — 가 보이는 화면이 여기뿐**이다. 설정에 두면
성공 문구 한 줄만 보고 나와야 한다.

세 문장으로 갈린다:

```
Unknown  "계정 확인 중..."              ← 부팅 직후. 게스트와 다른 말이어야 한다
Guest    "게스트 · 앱을 지우면 사라집니다"  ← 무엇을 잃는지가 읽혀야 버튼을 누른다
Linked   "구글 · 홍길동"
```

⚠️ 상태 라벨은 **동적 폰트**를 쓴다. 연동되면 구글 계정의 표시 이름이 들어오는데
그것은 54단계의 플레이어 이름과 똑같이 **남이 지은 글자**라 정적 아틀라스로
덮을 수 없다. 이름 라벨만 동적이던 규칙에 이 자리가 두 번째로 추가된다.

⚠️ **버튼 문구는 "구글 로그인"이지 "구글로 로그인"이 아니다.** 캡션 33pt에서
한글 한 자가 33px이라 조사 하나가 버튼 폭을 넘긴다(54단계 무료 줄 문구가 잘린
것과 같은 자리).

**로그아웃은 두지 않았다.** 방치형에서 계정 전환은 거의 없고, 익명으로 되돌아가는
것은 애초에 불가능하다(그 uid는 이미 버려졌다). "로그아웃"이 실제로 할 수 있는
일은 *다른 구글 계정으로 갈아타기*뿐인데, 그건 기록을 잃는 것처럼 보이는 동작이라
버튼으로 둘 이유가 없다.

### 치른 값

목록이 **66px 짧아졌다** (뷰포트 392 → **330px**, 보이는 줄 6.3 → 5.3). 판을
늘릴 수는 없다 — 성장 패널 띠가 고정 비율이다. 목록은 어차피 100줄짜리 스크롤이고,
이 스텝의 값어치가 그 한 줄보다 크다고 판단했다.

---

## 7. 세이브 — v19 그대로 (새 필드 0)

연동 상태의 원본은 **Firebase Auth**이지 우리 파일이 아니다. 세이브에 적어 두면
두 곳이 갈릴 수 있고(연동은 됐는데 세이브엔 게스트), 그때 어느 쪽이 참인지 정할
근거가 없다. 테스트가 `CurrentVersion == 19`를 못 박는다 — 다른 이유로 오르면
그 검사가 함께 걸린다.

---

## 8. 실패·오프라인 — 게임은 안 죽는다

전 경로가 try/catch이고 예외는 여기서 죽는다. 자바 쪽도 `catch (Throwable)`로
감싼다 — 거기서 새는 예외는 유니티까지 못 올라가고 **앱을 내린다**.

- 취소 → 조용히 끝 (문구 없음)
- 계정 없음 → "기기에 구글 계정이 없습니다 - 설정에서 추가하세요"
- 네트워크 → "연결을 확인하고 다시 시도하세요" (Retry 갈래)
- 3분 무응답 → 실패로 접는다 (버튼이 영영 죽는 것을 막는다)
- 에디터 → `IsAvailable = false`, **버튼 자체를 감춘다**

⚠️ 콜백은 **유니티 스레드가 아니다**(Credential Manager의 Executor 스레드).
`Listener` 안에서 유니티 API를 한 줄도 부르지 않고 값만
`TaskCompletionSource`에 넣는다. 어기면 증상이 "가끔 멈춘다"로 나와 원인 추적이
아주 어렵다.

---

## 9. 애플 자리 (규정 4.8)

앱스토어 심사 규정 4.8은 제3자 로그인(구글)을 제공하는 앱에 "Apple로 로그인"을
**동반 제공하도록** 요구한다. 즉 두 번째 공급자는 기획 항목이 아니라 **출시
조건**이다.

`AppleSignInProvider`가 스텁으로 서 있고, 테스트가 그 자리를 지킨다 — 스텁이
"안 쓰는 클래스"로 지워지면 다음 스텝은 구글 전용으로 굳은 흐름을 두 공급자용으로
되돌리는 일부터 시작한다.

`AuthAttempt.RawNonce`가 지금 아무도 안 쓰는 채로 있는 이유도 같다 — 애플 자격
증명은 `idToken` 하나가 아니라 `(idToken, rawNonce)` 쌍이다.

---

## 10. 테스트 — 570 (신규 31)

전부 통과. 분류:

- **예외 분류** 6 — ★ 아래 11절의 실기 결함이 남긴 것들. AccountLink 예외 인식 /
  AlreadyInUse의 세 얼굴 / 네트워크가 충돌로 안 새는 것 / 이미 붙은 공급자 /
  분류기와 분기표가 실제로 이어져 있는지 / 메일 주소는 앞부분만
- **복구가 깬 전제** 2 — 내 순위는 로컬이 아니라 **기록**으로 잰다 /
  이름도 복구하되 스스로 정한 이름은 안 덮는다

- **분기** 8 — 익명이면 Link / 이미 붙었으면 Nothing / 세션 없으면 Retry /
  **AlreadyInUse → Recover** / **나머지 다섯 실패는 Recover로 안 감** /
  취소는 Retry가 아님
- **병합** 4 — 셋 중 최대 / 재설치가 복구하러 온 기록을 지우지 않음 /
  익명으로 더 간 진행이 넘어감 / 병합값이 후퇴 방어와 어긋나지 않음
- **상수 대조** 5 — 웹 클라이언트 ID ↔ google-services.json /
  안드로이드 ID를 잘못 넣지 않음 / 자바 에러 코드 ↔ C# 번역표 /
  폐기 API로 안 돌아감 / EDM4U 의존성 선언 존재
- **자리·정직** 4 — 애플 스텁 / 구글 공급자 id / 에디터에서 IsAvailable=false /
  삭제 금지가 규칙과 일치
- **세이브·문구** 2 — v19 불변 / 계정 문구 18개가 아틀라스에 있음

⚠️ 자기 함정 하나를 밟았다: "폐기 API로 안 돌아감" 검사가 처음에 클래스 이름
`GoogleSignInClient`를 찾았는데, **왜 그것을 안 쓰는지 적어 둔 주석**에 걸렸다.
지금은 import 패키지(`com.google.android.gms.auth.api.signin`)를 본다.

---

## 11. 실기 검증 (G-8) — **세 갈래 전부 실측 완료**

빌드·설치·실행: 개발 빌드 **86.4MB / 295초**, 무선 adb 설치 성공
(`192.168.0.26:33667`, Galaxy Note20, Android 13).

### 확인된 것

**① 네이티브 플러그인이 실제로 산다.** 이것이 이 스텝에서 가장 크게 의심스러웠던
부분이다 — 직접 쓴 자바가 컴파일되는지, EDM4U가 넣은 androidx.credentials가
링크되는지, JNI 프록시가 자바 인터페이스를 찾는지가 전부 **기기에서만** 드러난다.
logcat이 답했다:

```
ActivityRecord{... com.google.android.gms/.auth.api.credentials.assistedsignin.ui.GoogleSignInActivity}
BoundBrokerSvc: onUnbind: Intent { act=com.google.android.gms.auth.api.identity.service.signin.START ... }
```

구글의 계정 선택 액티비티가 **실제로 떴다.** 즉 자바 컴파일 · 의존성 링크 ·
`CredentialManager.create` · `GetSignInWithGoogleOption` · JNI 호출까지 전부
통과했다. 실패했다면 이 액티비티는 아예 안 뜬다.

**② 계정 계층이 상태를 바르게 읽는다.** 개발 오버레이:

```
도달층 3   uid A9aMgxR5qNSmuR9tmobfs4PT8St1
계정 Guest / Nothing
제출 생략: 로컬 3 <= 서버 3
```

**③ 첫 계획이 Link로 갈린다.**

```
[AccountLink] 준비 중...
[CloudScores] 기존 로그인 재사용. uid = A9aMgxR5qNSmuR9tmobfs4PT8St1
[AccountLink] 계획 = Link (로그인 True / 익명 True / 이미붙음 False)
[AccountLink] 구글 계정을 고르는 중...
```

### ④ Link 갈래 실측 성공 — uid가 유지된다

```
[AccountLink] 계정을 연결하는 중...
[CloudScores] 사용자 채택. uid = A9aMgxR5qNSmuR9tmobfs4PT8St1 / 익명 = False / 교체됨 = False
[AccountLink] ★ 연동 성공 - uid 유지 A9aMgxR5... -> A9aMgxR5... (같은가: True)
[CloudScores] 제출 생략(후퇴 방지). 로컬 3 / 서버 3
```

서버 교차 확인(Firebase MCP) — **같은 uid에 공급자만 붙었다**:

```json
{ "uid": "A9aMgxR5qNSmuR9tmobfs4PT8St1",
  "createdAt": "1786564663307",          // ← 익명일 때와 동일. 새 계정이 아니다
  "providerUserInfo": [{ "providerId": "google.com", "displayName": "이화랑",
                         "email": "snow2271@gmail.com" }] }
```

랭킹 화면도 그대로 따라온다 — 계정 줄 `구글 · snow2271@gmail.com`, 로그인 버튼
사라짐, 내 순위 2위(3층), 내 줄 강조.

### ⑤ ★ 실기에서 잡은 결함 — **복구 갈래가 영영 안 열린다**

앱 데이터를 지우고(`pm clear`) 다시 연동했다. 새 익명 uid
`xDN51vPYiThcRYvwoF0f3f570Zw2`가 생겼고, 예상대로 링크가 거부됐다. 그런데:

```
[AccountLink] 연동 실패 -> Other
[AccountLink] 연동 실패 = Other -> 계획 Nothing
FirebaseAccountLinkException: This credential is already associated with a different user account.
```

메시지는 정확히 "이미 다른 계정에 연결됨"인데 갈래가 **Other**로 떨어졌다.
원인은 **예외 타입**이다 — `LinkWithCredentialAsync`가 던지는 것은
`FirebaseAccountLinkException`이고, 그 타입은 **`FirebaseException`을 상속하지
않는다**. 분류기가 `as FirebaseException`으로만 걸러서 이 예외를 통째로
놓쳤고, ErrorCode를 볼 기회조차 없었다.

**증상이 특히 나쁘다**:

- 에디터 테스트 562개가 전부 통과한다 (순수 규칙은 멀쩡하다 — 규칙에
  `AlreadyInUse`를 주면 정확히 `Recover`를 돌려준다)
- **첫 연동도 성공한다** (그 경로는 예외가 안 난다)
- **재설치한 사람만** 조용히 실패한다 — 즉 *이 스텝이 존재하는 이유가 되는
  그 경로 하나만* 죽는다

고침: `ClassifyError(bool accountLinkException, int errorCode)`로 분류를 **순수
함수로 빼고**, 두 예외 타입을 모두 받는다. 코드가 낯설어도 타입이
`FirebaseAccountLinkException`이면 `AlreadyInUse`다 — 이 예외는 "붙이려는 자격
증명에 다른 주인이 있어서, 대신 로그인하도록 그쪽 정보를 함께 준다"는 뜻으로만
던져진다(`UserInfo`를 들고 오는 이유가 그것이다). 단, **코드가 네트워크면
네트워크다**(흔들린 사람이 익명 uid를 잃으면 안 된다).

⚠️ 이 결함은 **테스트가 잡을 수 없는 종류가 아니었다** — 잡을 수 있게 짜여
있지 않았을 뿐이다. 분류를 Firebase 타입에 묶어 두어서 검사할 수 없었고,
`bool + int`로 빼자마자 여섯 개의 검사가 생겼다. `ClassifiedFailures_
FeedTheBranchThatMatters`가 분류기와 분기표가 **실제로 이어져 있는지**까지 본다.

### ⑥ 실기에서 드러난 것 하나 더 — 이름 대신 메일 주소

연동 직후 계정 줄에 `snow2271@gmail.com`이 통째로 떴다. 서버에는 표시 이름
("이화랑")이 있는데 클라의 사용자 객체가 아직 안 받아온 상태다. 고침 둘:
연동·복구 뒤 `ReloadAsync()` 한 번, 그리고 메일로 떨어지더라도 **주소
앞부분만**(`snow2271`) 적는다 — 랭킹 화면은 스크린샷·방송에 그대로 실리는
자리다.

### ⑦ ★ 복구 갈래 실측 성공 — **이 스텝의 존재 이유**

`pm clear`로 앱 데이터를 지우고(= 재설치와 같은 상태: Lv.1, 지역1 1/10, 보석 0)
다시 연동했다. 새 익명 uid `9RCnHNvXpaW359zW9tfxtaqKGUu1`이 생긴 상태에서:

```
[AccountLink] 계획 = Link (로그인 True / 익명 True / 이미붙음 False)
[AccountLink] 구글 계정을 고르는 중...
FirebaseAccountLinkException(CredentialAlreadyInUse/10): This credential is already
                                associated with a different user account.
[AccountLink] 연동 실패 = AlreadyInUse -> 계획 Recover
[AccountLink] 기존 계정을 찾았습니다 - 기록을 복구하는 중...
[CloudScores] 병합 전 조회. scores/9RCnHNvXpaW359zW9tfxtaqKGUu1 = 1
[CloudScores] 사용자 채택. uid = A9aMgxR5... / 익명 = False / 교체됨 = True
[AccountLink] ★ 복구 성공 - uid 교체 9RCnHNvXpaW... -> A9aMgxR5...
[CloudScores] read 성공. maxStage = 3 / updatedAt = 2026-08-13 07:27:45 / 캐시에서 옴 = False
[AccountLink] 이름 복구: 랑무사
[AccountLink] 병합 - 로컬 1 / 버린 문서 1 / 복구된 문서 3 -> 3
[AccountLink] 기록을 복구했습니다 - 최고 3층
```

세 갈래가 전부 실측됐다. 그리고 **병합 쓰기는 정확히 생략됐다** — 복구된 값
3이 이미 최고라 `ShouldMergeAfterRecovery(3, 3) = false`다(로그에 "병합 기록"
줄이 없는 것이 그 증거).

화면도 전부 일치한다:

```
구글 · 이화랑                            ← 표시 이름 (ReloadAsync 효과)
랑무사                     [이름 변경]   ← 이름 복구, 입력 줄 닫힘
  1  보나무사                    171층
  2  랑무사                        3층   ← 내 줄 강조
  3  이름없는 무사                 1층   ← 버려진 익명 문서
  4  이름없는 무사                 1층   ← 버려진 익명 문서
내 순위  2위  (3층)                      ← 목록과 일치
```

3·4위의 "이름없는 무사 1층" 두 줄이 **버려진 익명 문서 정책의 실물**이다 —
지우지 않으므로 순위표에 남고, 그 uid로 다시 로그인할 방법이 없으므로 더
자라지 않는다(5절).

### ⑧ 실기에서 잡은 결함 셋 — 요약

| # | 증상 | 원인 | 고침 |
|---|---|---|---|
| 1 | **복구 갈래가 안 열림** | `FirebaseAccountLinkException`이 `FirebaseException`을 상속하지 않아 분류를 빠져나감 | `ClassifyError(bool, int)` 순수 함수 + 두 타입 모두 수용 |
| 2 | 계정 줄에 메일 주소가 통째로 | 연동 직후 ProviderData의 DisplayName이 빔 | `ReloadAsync()` + 메일이면 앞부분만 |
| 3 | **"내 순위 3위 (1층)"** 인데 목록은 2위 3층 | 내 순위를 **로컬** 도달층으로 잼. 복구가 "로컬 ≥ 서버" 전제를 깸 | 병합과 같은 함수(`MergedStage`)로 셋 중 최댓값 |
| 3b | 랭킹표는 "랑무사"인데 내 화면은 "이름없는 무사" | 복구가 이름을 안 되살림 | `ShouldRestoreName` — 스스로 정한 이름이 없을 때만 복원 |

⚠️ 셋 다 **에디터 테스트를 전부 통과한 채로** 살아 있었다. 1번은 순수 규칙이
멀쩡했고(규칙에 AlreadyInUse를 주면 정확히 Recover를 낸다) 첫 연동도 성공해서,
**재설치한 사람만** 조용히 실패했다. 3번은 55단계가 **자기 전제를 깬 것**을
아무도 다시 안 물어봐서 생겼다 — "로컬 ≥ 서버"는 기기 하나에 계정 하나일 때만
참이었다.

### ⑨ 작은 관찰 — 초기화 전 몇 초

앱을 켠 직후 몇 초간 계정 상태가 `Unknown`이라 연동 버튼이 비활성이다
(Firebase 의존성 확인 + 익명 로그인이 끝나야 `Guest`가 된다). 실측 약 2초.
버튼이 아니라 **문구가 답한다** — 그 동안 계정 줄은 "계정 확인 중..."이고,
이것이 "게스트"와 다른 문장이어야 하는 이유가 바로 이 구간이다.

---

## 12. 남은 빚 / 출시 전 교체

- **릴리스 keystore SHA-1** 미등록. 지금 등록된 것은 디버그뿐이라, 스토어
  빌드에서만 구글 로그인이 실패한다. Play 앱 서명을 쓰면 Play Console이 발급하는
  **앱 서명 키 SHA-1**도 함께 등록해야 한다.
- **프로젝트 공개용 이름**이 `project-598938331304`면 동의 화면에 그대로 뜬다.
- **버려진 익명 문서 정리**가 없다(위 5절).
- **클라우드 세이브는 이 스텝의 범위가 아니다.** 재설치로 복구되는 것은 **랭킹
  기록(서버 문서)**이지 게임 진행이 아니다. 지금도 재설치하면 로컬 세이브는
  사라진다 — 사람은 그 둘을 구분하지 않으므로, 이것은 다음 스텝 후보 중
  우선순위가 높다.
- **값의 진위 검증**은 여전히 없다(Cloud Functions, Blaze 필요).
- ⚠️ **난독화를 켜면 keep 규칙이 필요하다.** 지금은 `AndroidMinifyRelease: 0` ·
  `AndroidMinifyDebug: 0`이라 문제가 없지만, R8을 켜면
  `OnikiriGoogleAuth$Listener`가 이름만 남기고 지워질 수 있다 — C# 쪽
  `AndroidJavaProxy`가 그 인터페이스를 **문자열 이름으로** 찾기 때문이다.
  켜는 날 `proguard-user.txt`에 `-keep class com.studio202.onikiri.auth.** { *; }`
  를 함께 넣어야 한다.

---

## 13. 다음

1. **애플 로그인 + iOS 브링업** — Apple Developer 등록 → Firebase iOS 앱 →
   Apple 공급자 → `AppleSignInProvider` 구현 → 맥/Xcode 첫 빌드 (규정 4.8)
2. **클라우드 세이브** — 정체성이 생겼으므로 이제 가능해진 것
3. **Blaze 계열** — Cloud Functions 서버 재검증 · App Check Play Integrity
