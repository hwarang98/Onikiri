using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 계정 연동(55단계)에서 **Firebase 없이 증명할 수 있는 전부**.
     *
     * 이 스텝의 실체는 네트워크 왕복이라 대부분이 실기 logcat으로만 증명된다
     * (53단계 스파이크와 같은 사정). 그래서 여기서 하는 일은 증명 가능한
     * 부분을 최대한 끌어내는 것이고, 그 대상이 넷이다:
     *
     *   분기 규칙   연동이냐 복구냐 - **취소가 복구로 새지 않는 것**이 핵심.
     *               복구는 지금 기기의 익명 uid를 버리는 동작이라, 애매한
     *               실패에서 그것을 하면 멀쩡한 사람의 진행이 사라진다
     *
     *   병합        높은 쪽이 남는다. 후퇴 방어(54단계)와 같은 원칙이어야
     *               한다 - 아니면 "낮은 기기에서 로그인하기"가 기록을 지우는
     *               방법이 된다
     *
     *   상수 대조   웹 클라이언트 ID가 google-services.json과 같은가,
     *               자바의 에러 코드가 C#의 번역표와 같은가. 두 곳에 같은
     *               문자열이 두 벌 적혀 있고 **갈리면 기기에서만 드러난다**
     *
     *   자리        애플 스텁이 서 있는가. 규정 4.8이 요구하는 두 번째
     *               공급자가 들어올 자리가 비어 있어야 다음 스텝이 이 파일
     *               하나로 끝난다
     */
    public class AccountLinkTests
    {
        private static string RepoRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        // ---------------------------------------------------------------- 분기

        [Test]
        public void AnonymousUser_IsLinkedNotSignedIn()
        {
            // 익명에 붙이면 uid가 유지된다 = scores/{uid}가 통째로 이어진다.
            // 이것이 첫 연동의 정상 경로다
            Assert.AreEqual(LinkPlan.Link,
                AccountLinkPolicy.PlanFor(signedIn: true, anonymous: true, alreadyHas: false));
        }

        [Test]
        public void AlreadyLinkedProvider_DoesNothing()
        {
            Assert.AreEqual(LinkPlan.Nothing,
                AccountLinkPolicy.PlanFor(signedIn: true, anonymous: true, alreadyHas: true));
        }

        [Test]
        public void NonAnonymousUser_HasNothingToLink()
        {
            // 익명이 아닌데 로그인돼 있다 = 이미 구글이 붙은 계정이다
            Assert.AreEqual(LinkPlan.Nothing,
                AccountLinkPolicy.PlanFor(signedIn: true, anonymous: false, alreadyHas: false));
        }

        [Test]
        public void WithoutASession_ThereIsNothingToAttachTo()
        {
            // 여기서 Link를 돌려주면 로그인 없는 상태로 Link를 불러 터진다.
            // 그 순간은 네트워크가 이미 나쁜 순간이라 가장 안 좋은 자리다
            Assert.AreEqual(LinkPlan.Retry,
                AccountLinkPolicy.PlanFor(signedIn: false, anonymous: false, alreadyHas: false));
        }

        /** ★ 이 스텝의 중심. 재설치한 기기가 밟는 경로 */
        [Test]
        public void CredentialAlreadyInUse_TurnsIntoRecovery()
        {
            Assert.AreEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.AlreadyInUse));
        }

        /**
         * ★ 그 반대편. **취소·기타 실패는 절대 복구로 가지 않는다.**
         *
         * 복구는 지금 기기의 익명 uid를 버린다. "아무 실패나 일단 로그인으로
         * 갈아탄다"로 만들면, 네트워크가 한 번 흔들린 사람이 자기 진행이
         * 붙어 있던 uid를 잃는다.
         */
        [Test]
        public void OtherLinkFailures_NeverDiscardTheAnonymousAccount()
        {
            Assert.AreNotEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.Cancelled));
            Assert.AreNotEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.Other));
            Assert.AreNotEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.Network));
            Assert.AreNotEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.NoCredential));
            Assert.AreNotEqual(LinkPlan.Recover,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.Unsupported));
        }

        [Test]
        public void NetworkFailure_IsWorthRetrying()
        {
            Assert.AreEqual(LinkPlan.Retry,
                AccountLinkPolicy.PlanAfterLinkFailure(AuthFailure.Network));
            Assert.AreEqual(LinkPlan.Retry,
                AccountLinkPolicy.PlanAfterAcquireFailure(AuthFailure.Network));
        }

        /**
         * @brief 취소는 재시도가 아니다.
         *
         * 사람이 계정 선택창을 닫은 것은 "안 하겠다"이지 실패가 아니다.
         * 여기서 Retry를 돌려주면 닫은 창이 다시 뜨는 것처럼 읽힌다.
         */
        [Test]
        public void Cancelling_IsNotAFailureToRetry()
        {
            Assert.AreEqual(LinkPlan.Nothing,
                AccountLinkPolicy.PlanAfterAcquireFailure(AuthFailure.Cancelled));
        }

        // ---------------------------------------------------------------- 예외 분류

        /**
         * @brief ★ **실기에서 잡은 결함.** 링크 실패는 FirebaseException이 아니다.
         *
         * `LinkWithCredentialAsync`가 던지는 것은 `FirebaseAccountLinkException`
         * 이고, 그 타입은 `FirebaseException`을 **상속하지 않는다**. 처음 코드는
         * `as FirebaseException`으로만 걸러서 이 예외를 통째로 놓쳤고,
         * "이미 다른 계정에 연결됨"이 Other로 분류되어 **복구 갈래가 영영 안
         * 열렸다**. 에디터 테스트는 전부 통과했고 첫 연동도 성공했다 -
         * 재설치한 사람만 조용히 실패한다.
         */
        [Test]
        public void AccountLinkException_IsRecognisedEvenThoughItIsNotAFirebaseException()
        {
            Assert.AreEqual(AuthFailure.AlreadyInUse,
                AccountLink.ClassifyError(accountLinkException: true,
                                          errorCode: AccountLink.ErrorCredentialAlreadyInUse));

            // 코드가 낯설어도 **타입이 답이다** - 이 예외는 "붙이려는 자격
            // 증명에 다른 주인이 있다"는 뜻으로만 던져진다
            Assert.AreEqual(AuthFailure.AlreadyInUse,
                AccountLink.ClassifyError(accountLinkException: true, errorCode: 0));
        }

        [Test]
        public void AlreadyInUse_HasThreeFacesAndAllThreeCount()
        {
            Assert.AreEqual(AuthFailure.AlreadyInUse,
                AccountLink.ClassifyError(false, AccountLink.ErrorCredentialAlreadyInUse));
            Assert.AreEqual(AuthFailure.AlreadyInUse,
                AccountLink.ClassifyError(false, AccountLink.ErrorEmailAlreadyInUse));
            Assert.AreEqual(AuthFailure.AlreadyInUse,
                AccountLink.ClassifyError(false, AccountLink.ErrorAccountExistsWithDifferentCredentials));
        }

        /** 네트워크 실패가 AlreadyInUse로 새면, 흔들린 사람이 익명 uid를 잃는다 */
        [Test]
        public void NetworkError_NeverLooksLikeAnAccountCollision()
        {
            Assert.AreEqual(AuthFailure.Network,
                AccountLink.ClassifyError(false, AccountLink.ErrorNetworkRequestFailed));
            Assert.AreEqual(AuthFailure.Network,
                AccountLink.ClassifyError(true, AccountLink.ErrorNetworkRequestFailed),
                "AccountLink 예외라도 코드가 네트워크면 네트워크다");
        }

        [Test]
        public void ProviderAlreadyLinked_IsNotACollision()
        {
            // 이미 붙어 있는 것은 "남의 것"이 아니다. 복구로 가면 자기
            // 계정으로 다시 로그인하는 헛일을 한다
            Assert.AreEqual(AuthFailure.Other,
                AccountLink.ClassifyError(false, AccountLink.ErrorProviderAlreadyLinked));
        }

        [Test]
        public void ClassifiedFailures_FeedTheBranchThatMatters()
        {
            // 분류기와 분기표가 실제로 이어져 있는가. 둘 다 맞는데 사이가
            // 끊겨 있으면 이 스텝은 여전히 안 돈다
            var failure = AccountLink.ClassifyError(true, AccountLink.ErrorCredentialAlreadyInUse);
            Assert.AreEqual(LinkPlan.Recover, AccountLinkPolicy.PlanAfterLinkFailure(failure));
        }

        /** 화면에 전체 메일 주소를 띄우지 않는다 (실기에서 그렇게 떴다) */
        [Test]
        public void EmailFallback_ShowsOnlyTheLocalPart()
        {
            Assert.AreEqual("snow2271", AccountLink.LocalPart("snow2271@gmail.com"));
            Assert.AreEqual(string.Empty, AccountLink.LocalPart(null));
            Assert.AreEqual("@leading", AccountLink.LocalPart("@leading"));
        }

        // ---------------------------------------------------------------- 병합

        [Test]
        public void MergeKeepsTheHighestOfThree()
        {
            Assert.AreEqual(300, AccountLinkPolicy.MergedStage(local: 300, abandoned: 300, recovered: 171));
            Assert.AreEqual(171, AccountLinkPolicy.MergedStage(local: 1, abandoned: 0, recovered: 171));
            Assert.AreEqual(500, AccountLinkPolicy.MergedStage(local: 12, abandoned: 500, recovered: 171));
        }

        /**
         * @brief 재설치 직후. 로컬은 1층인데 서버에는 171층이 있다.
         *
         * 이 스텝의 존재 이유가 그대로 검사가 된 자리다 - 병합이 로컬을
         * 우선하면 복구가 곧 기록 삭제가 된다.
         */
        [Test]
        public void ReinstalledDevice_DoesNotWipeTheRecordItCameToRecover()
        {
            int merged = AccountLinkPolicy.MergedStage(local: 1, abandoned: 0, recovered: 171);

            Assert.AreEqual(171, merged);
            Assert.IsFalse(AccountLinkPolicy.ShouldMergeAfterRecovery(merged, recovered: 171),
                "서버가 이미 가장 높은데 쓰기를 보냈다 - 규칙이 거부할 뿐이고 요금만 쓴다");
        }

        [Test]
        public void PlayingAnonymouslyPastTheOldRecord_CarriesTheProgressOver()
        {
            // 이 기기에서 익명으로 500층까지 갔고, 붙이려는 구글 계정에는
            // 옛 폰의 171층이 있다. 500이 남아야 한다
            int merged = AccountLinkPolicy.MergedStage(local: 500, abandoned: 500, recovered: 171);

            Assert.AreEqual(500, merged);
            Assert.IsTrue(AccountLinkPolicy.ShouldMergeAfterRecovery(merged, recovered: 171));
        }

        [Test]
        public void MergeAgreesWithTheRetreatGuard()
        {
            // 병합값은 늘 제출 가능해야 한다. 여기가 갈리면 병합만 서버
            // 규칙에 막히고, 그 실패는 복구 성공 뒤에 조용히 일어난다
            int merged = AccountLinkPolicy.MergedStage(1, 0, 171);
            Assert.IsTrue(LeaderboardPolicy.IsSubmittable(merged));
            Assert.IsTrue(LeaderboardPolicy.ShouldSubmit(merged, 0, hasServerValue: false));
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(merged, 171, hasServerValue: true),
                "복구된 값과 같은 값을 다시 쓰면 updatedAt만 갱신되어 자기 순위를 내린다");
        }

        /**
         * @brief ★ **실기에서 잡은 결함 둘.** 복구가 "내 순위"의 전제를 깬다.
         *
         * 55단계 전까지는 "로컬 도달층 >= 서버 도달층"이 늘 참이었다 - 기기
         * 하나에 계정 하나였고 도달층은 안 내려간다. 그래서 내 순위를 로컬
         * 값으로 재는 것이 옳았다.
         *
         * 복구가 그 전제를 깬다. 지운 기기의 로컬은 1층인데 돌아온 문서는
         * 3층이라, 로컬로 재면 **한 화면이 자기모순에 빠진다** - 목록은 내
         * 줄을 2위(3층)로 강조하는데 아래 한 줄은 "내 순위 3위 (1층)"이라고
         * 적는다. 실기에서 정확히 그렇게 떴다.
         *
         * 화면은 이제 병합과 **같은 함수**를 쓴다. 묻는 질문이 같기 때문이다 -
         * "여러 출처가 말하는 내 도달층 중 무엇이 내 기록인가".
         */
        [Test]
        public void MyRankUsesTheRecordNotTheLocalSaveAfterRecovery()
        {
            // 로컬 1 / 목록에서 본 내 줄 3 / 아직 안 읽음(-1)
            Assert.AreEqual(3, AccountLinkPolicy.MergedStage(local: 1, abandoned: 3, recovered: -1),
                "복구 직후 로컬로 재면 내 순위가 목록의 내 줄과 어긋난다");

            // 평소(복구 없음): 로컬이 가장 높다. 규칙이 그대로 로컬을 낸다
            Assert.AreEqual(171, AccountLinkPolicy.MergedStage(local: 171, abandoned: 171, recovered: -1));
        }

        /** 이름도 기록의 일부다 - 다만 스스로 정한 이름은 안 덮는다 */
        [Test]
        public void RecoveryBringsTheNameBackUnlessOneWasChosenHere()
        {
            Assert.IsTrue(AccountLinkPolicy.ShouldRestoreName(hasChosenName: false, recoveredName: "랑무사"));

            Assert.IsFalse(AccountLinkPolicy.ShouldRestoreName(hasChosenName: true, recoveredName: "랑무사"),
                "이 기기의 사람이 방금 고른 이름을 서버의 옛 이름으로 덮었다");
            Assert.IsFalse(AccountLinkPolicy.ShouldRestoreName(false, string.Empty));
            Assert.IsFalse(AccountLinkPolicy.ShouldRestoreName(false, null));
        }

        /**
         * @brief 버려진 익명 문서는 지우지 않는다. **규칙 4-B와의 계약이다.**
         *
         * 이 값이 true가 되는 날은 `allow delete: if false`를 여는 날이고,
         * 그 규칙에 적어 둔 판단(먼저 생기는 피해자는 자기 기록을 실수로
         * 날린 사람이다)이 뒤집히는 날이다. 그래서 검사로 못 박는다.
         */
        [Test]
        public void AbandonedDocument_IsLeftAloneBecauseTheRulesForbidDelete()
        {
            Assert.IsFalse(AccountLinkPolicy.ShouldDeleteAbandonedDocument());

            string rules = File.ReadAllText(Path.Combine(RepoRoot, "firestore.rules"));
            StringAssert.Contains("allow delete: if false", rules,
                "규칙이 삭제를 열었다 - 정리 정책을 다시 판단해야 한다");
        }

        // ---------------------------------------------------------------- 상수 대조

        /**
         * @brief ★ 웹 클라이언트 ID가 google-services.json과 같은가.
         *
         * 사람이 다른 Firebase 프로젝트의 설정 파일을 받아 덮으면 상수만 옛
         * 값으로 남는다. 그 어긋남은 빌드에서 안 잡히고 **기기에서 로그인이
         * 조용히 실패하는 것**으로 나온다(토큰의 audience가 안 맞는다).
         */
        [Test]
        public void WebClientId_MatchesGoogleServicesJson()
        {
            string path = Path.Combine(RepoRoot, "Assets/google-services.json");
            Assert.IsTrue(File.Exists(path), "google-services.json이 없다: " + path);

            string json = File.ReadAllText(path);

            StringAssert.Contains(GoogleSignInProvider.WebClientId, json,
                "웹 클라이언트 ID 상수가 google-services.json에 없다 - "
                + "설정 파일을 다시 받았다면 상수도 함께 고칠 것");

            // client_type 3(웹)이 실제로 있는지. 없으면 Firebase 콘솔에서
            // Google 공급자가 꺼져 있다는 뜻이고, 그 상태로는 아무도 못 붙는다
            StringAssert.Contains("\"client_type\": 3", json,
                "google-services.json에 웹 클라이언트가 없다 - "
                + "콘솔에서 Google 로그인 공급자가 꺼져 있다");
        }

        /**
         * @brief 안드로이드 클라이언트 ID를 잘못 넣지 않았는가.
         *
         * 둘 다 `...apps.googleusercontent.com`으로 끝나서 눈으로는 안
         * 갈린다. 안드로이드 것(client_type 1)을 serverClientId로 넘기면
         * 토큰은 발급되는데 Firebase가 거절한다 - 가장 헷갈리는 실패다.
         */
        [Test]
        public void WebClientId_IsNotTheAndroidClientId()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, "Assets/google-services.json"));

            int androidBlock = json.IndexOf("\"client_type\": 1");
            Assert.Greater(androidBlock, 0, "안드로이드 클라이언트가 없다 - SHA-1이 등록 안 됐다");

            // 안드로이드 블록 바로 앞의 client_id가 우리 상수와 같으면 잘못 넣은 것이다
            int idBefore = json.LastIndexOf("\"client_id\"", androidBlock);
            int idEnd = json.IndexOf('\n', idBefore);
            string androidLine = json.Substring(idBefore, idEnd - idBefore);

            StringAssert.DoesNotContain(GoogleSignInProvider.WebClientId, androidLine,
                "웹 클라이언트 ID 자리에 안드로이드 클라이언트 ID가 들어 있다");
        }

        /**
         * @brief 자바의 에러 코드와 C#의 번역표가 같은가.
         *
         * 두 언어에 같은 문자열이 두 벌 적혀 있다. 갈리면 **취소가 오류로
         * 읽히고**, 안 하겠다고 한 사람에게 실패 문구가 뜬다.
         */
        [Test]
        public void JavaErrorCodes_MatchTheCSharpTable()
        {
            string java = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets/Plugins/Android/OnikiriGoogleAuth.java"));

            StringAssert.Contains("\"cancelled\"", java);
            StringAssert.Contains("\"no_credential\"", java);

            Assert.AreEqual(AuthFailure.Cancelled, GoogleIdTokenBridge.Translate("cancelled"));
            Assert.AreEqual(AuthFailure.NoCredential, GoogleIdTokenBridge.Translate("no_credential"));

            // 모르는 코드는 Other다. 새 코드가 자바에만 생겨도 조용히 삼켜지지
            // 않고 "실패"로는 보인다
            Assert.AreEqual(AuthFailure.Other, GoogleIdTokenBridge.Translate("something_new"));
        }

        /**
         * @brief 자바 플러그인이 **폐기된 API**로 돌아가지 않았는가.
         *
         * legacy Google Sign-In(GoogleSignInClient / play-services-auth의
         * GoogleSignInOptions)은 구글이 폐기한 경로다. 인터넷의 유니티 예제
         * 대부분이 아직 그것이라, 이 파일을 고치다 되돌아가기 쉽다.
         */
        [Test]
        public void NativePlugin_UsesCredentialManagerNotLegacySignIn()
        {
            string java = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets/Plugins/Android/OnikiriGoogleAuth.java"));

            StringAssert.Contains("androidx.credentials.CredentialManager", java);
            StringAssert.Contains("GetSignInWithGoogleOption", java);

            // import를 본다. 클래스 이름만 찾으면 **왜 그것을 안 쓰는지 적어
            // 둔 주석**에 걸린다(처음 이 검사를 쓸 때 실제로 그렇게 걸렸다).
            // 폐기된 경로는 반드시 이 패키지를 가져와야 쓸 수 있다
            StringAssert.DoesNotContain("com.google.android.gms.auth.api.signin", java,
                "폐기된 legacy Google Sign-In으로 되돌아갔다");
        }

        /**
         * @brief 그 API를 쓸 의존성이 선언돼 있는가.
         *
         * EDM4U가 이 파일을 읽어 gradle에 줄을 넣는다. 파일이 사라지면
         * 자바가 컴파일조차 안 되는데, 그 실패는 **안드로이드 빌드에서만**
         * 나온다 - 에디터에서는 아무 일도 없다.
         */
        [Test]
        public void CredentialManagerDependencies_AreDeclaredForTheAndroidBuild()
        {
            string path = Path.Combine(RepoRoot, "Assets/_Project/Editor/OnikiriAuthDependencies.xml");
            Assert.IsTrue(File.Exists(path), "EDM4U 의존성 파일이 없다: " + path);

            string xml = File.ReadAllText(path);
            StringAssert.Contains("androidx.credentials:credentials:", xml);
            StringAssert.Contains("androidx.credentials:credentials-play-services-auth:", xml);
            StringAssert.Contains("com.google.android.libraries.identity.googleid:googleid:", xml);
        }

        // ---------------------------------------------------------------- 애플 자리

        /**
         * @brief ★ 규정 4.8이 요구하는 두 번째 공급자의 자리가 비어 있는가.
         *
         * 스텁이 사라지면(“안 쓰는 클래스”로 지워지면) 다음 스텝은 구글
         * 전용으로 굳어진 흐름을 두 공급자용으로 되돌리는 일부터 시작한다.
         */
        [Test]
        public void AppleProviderKeepsItsSeatWithoutPretendingToWork()
        {
            var apple = AuthProviders.Apple;

            Assert.IsNotNull(apple, "애플 스텁이 사라졌다 - 규정 4.8의 자리다");
            Assert.AreEqual("apple.com", apple.Id, "Firebase가 아는 공급자 id여야 한다");
            Assert.IsFalse(apple.IsAvailable,
                "구현이 없는데 쓸 수 있다고 말한다 - 화면이 눌리지 않는 버튼을 그린다");

            var attempt = apple.AcquireAsync().Result;
            Assert.IsFalse(attempt.Ok);
            Assert.AreEqual(AuthFailure.Unsupported, attempt.Failure);
        }

        [Test]
        public void GoogleProviderUsesTheProviderIdFirebaseKnows()
        {
            Assert.AreEqual("google.com", AuthProviders.Google.Id);
            Assert.IsFalse(string.IsNullOrEmpty(AuthProviders.Google.DisplayName));
        }

        /** 에디터에는 네이티브가 없다. 여기서 true면 버튼이 눌려도 아무 일이 없다 */
        [Test]
        public void GoogleSignIn_IsHonestAboutTheEditor()
        {
            Assert.IsFalse(AuthProviders.Google.IsAvailable,
                "에디터에서 구글 로그인이 가능하다고 말한다");

            var attempt = AuthProviders.Google.AcquireAsync().Result;
            Assert.IsFalse(attempt.Ok);
            Assert.AreEqual(AuthFailure.Unsupported, attempt.Failure);
        }

        // ---------------------------------------------------------------- 세이브 · 문구

        /**
         * @brief 연동은 세이브를 늘리지 않는다.
         *
         * 연동 상태의 원본은 Firebase Auth이지 우리 파일이 아니다. 세이브에
         * 적어 두면 두 곳이 갈릴 수 있고(연동은 됐는데 세이브엔 게스트),
         * 그때 어느 쪽이 참인지 정할 근거가 없다.
         */
        [Test]
        public void LinkingAddsNoSaveField()
        {
            Assert.AreEqual(19, SaveData.CurrentVersion,
                "세이브 버전이 올랐다 - 계정 연동은 새 필드를 만들지 않기로 했다. "
                + "다른 이유로 올랐다면 이 검사를 함께 고칠 것");
        }

        /**
         * @brief 계정 줄의 문구가 아틀라스에 있는가.
         *
         * 상태줄은 **정적 폰트**를 쓴다(연동된 계정 이름만 동적 폰트다).
         * 54단계에서 "잠시 □ 랭킹에 반영됩니다"로 드러난 것과 같은 자리다.
         */
        [Test]
        public void AccountMessages_AreInTheBakedCharset()
        {
            string charset = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets/_Project/Data/FontCharset.txt"));

            string[] messages =
            {
                "구글 로그인",
                "게스트 · 앱을 지우면 사라집니다",
                "계정 확인 중...",
                "구글 계정을 고르는 중...",
                "계정을 연결하는 중...",
                "구글 계정이 연결되었습니다 - 기록이 이 계정에 남습니다",
                "기존 계정을 찾았습니다 - 기록을 복구하는 중...",
                "기록을 복구했습니다 - 최고 171층",
                "구글 계정으로 로그인했습니다",
                "구글 계정이 이미 연동돼 있습니다",
                "기기에 구글 계정이 없습니다 - 설정에서 추가하세요",
                "구글 로그인은 이 기기에서 쓸 수 없습니다",
                "구글 로그인에 실패했습니다",
                "구글 로그인 모듈을 불러오지 못했습니다",
                "연동 중 문제가 생겼습니다",
                "복구된 계정을 읽지 못했습니다",
                "지금은 연동할 수 없습니다",
                "준비 중...",
            };

            foreach (var message in messages)
            {
                foreach (char c in message)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    Assert.IsTrue(charset.IndexOf(c) >= 0, string.Format(
                        "'{0}'이 아틀라스에 없다 (문구 \"{1}\") - "
                        + "UIStrings.txt에 적고 Onikiri/Art/Build Pixel Font Assets를 실행할 것",
                        c, message));
                }
            }
        }
    }
}
