using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 익명 계정을 구글에 붙이고, 이미 있는 계정이면 그리로 **돌아간다**.
     *
     * 54단계에서 랭킹이 섰지만 정체성이 익명 uid 하나에 묶여 있었다 - 앱을
     * 지우면 새 uid가 생기고 그 사람의 기록은 주인 없는 문서로 남는다. 이
     * 클래스가 그 구멍을 막는다.
     *
     * ## 세 갈래가 전부다
     *
     *   Link      익명 uid에 구글을 붙인다. **uid가 그대로다** → scores/{uid}
     *             문서도 순위도 통째로 이어진다. 처음 연동하는 사람의 경로
     *
     *   Recover   그 구글 계정에 이미 uid가 있다(`credential-already-in-use`).
     *             연동을 포기하고 **로그인**해서 그 uid로 돌아간다. 지금 기기의
     *             임시 익명 uid는 버린다. **재설치·다기기의 경로이고 이 스텝의
     *             존재 이유다**
     *
     *   Nothing   이미 붙어 있다 / 사람이 취소했다 / 이 플랫폼에 구현이 없다
     *
     * 어느 갈래를 밟았는지가 logcat 한 줄로 읽혀야 한다. 세 경로가 전부
     * 정상 경로라서, "실패 로그가 없다"로는 무엇이 일어났는지 알 수 없다.
     *
     * ## 게임에 대한 계약: 영향 0
     *
     * 밸런스·세이브·전투 루프를 건드리지 않는다. 도달층은 읽기만 한다.
     * 모든 경로가 try/catch이고 예외는 여기서 죽는다 - 연동은 **선택**이고,
     * 선택 기능이 본체를 멈추면 그건 기능이 아니라 사고다(CloudScores와
     * 같은 규칙).
     */
    public static class AccountLink
    {
        /** logcat에서 이 한 단어로 거른다: `adb logcat -s Unity | grep AccountLink` */
        private const string Tag = "[AccountLink]";

        private const int AuthTimeoutMs = 30000;

        /** 지금 연동이 도는 중인가. 버튼 연타를 막는다 */
        public static bool IsBusy { get; private set; }

        /** 화면에 그대로 띄우는 한 줄 */
        public static string Status { get; private set; } = string.Empty;

        /** 마지막으로 밟은 갈래. 테스트 패널·보고가 읽는다 */
        public static LinkPlan LastPlan { get; private set; } = LinkPlan.Nothing;

        /** 계정 상태가 바뀌었다(연동됨·복구됨). 계정 UI가 다시 그리는 신호 */
        public static event Action Changed;

        // ---------------------------------------------------------------- 상태 읽기

        /**
         * @brief 지금 정체성이 어떤 상태인가.
         *
         * Firebase가 아직 안 떴으면 Unknown이다. Guest로 두지 않는 이유는
         * 그 둘이 화면에서 다른 문장이어야 하기 때문이다 - "게스트"는 확정된
         * 사실이고 Unknown은 아직 모르는 것이라, 부팅 직후에 "게스트"를
         * 띄우면 이미 연동한 사람이 연동이 풀린 줄 안다.
         */
        public static AccountState State
        {
            get
            {
                var user = CurrentUser;
                if (user == null) return AccountState.Unknown;
                return user.IsAnonymous ? AccountState.Guest : AccountState.Linked;
            }
        }

        /**
         * @brief 연동된 계정의 표시 이름. 없으면 빈 문자열.
         *
         * 이름을 먼저, 이메일은 **주소 앞부분만**. 랭킹 화면은 스크린샷·방송에
         * 그대로 실리는 자리라 전체 메일 주소가 늘 떠 있을 이유가 없고,
         * 앞부분만으로도 "내가 어느 계정에 붙였는지"는 충분히 갈린다.
         *
         * ⚠️ **실기에서 드러난 것**: 연동 직후 ProviderData의 DisplayName이
         * 비어 있어 화면에 `snow2271@gmail.com`이 통째로 떴다. 서버에는 이름
         * ("이화랑")이 있는데 클라의 사용자 객체가 아직 안 받아온 상태다 -
         * 그래서 연동 뒤에 ReloadAsync를 한 번 돌린다(Refresh).
         */
        public static string LinkedLabel
        {
            get
            {
                var user = CurrentUser;
                if (user == null || user.IsAnonymous) return string.Empty;

                foreach (var profile in user.ProviderData)
                {
                    if (profile == null) continue;
                    if (!string.IsNullOrEmpty(profile.DisplayName)) return profile.DisplayName;
                }

                if (!string.IsNullOrEmpty(user.DisplayName)) return user.DisplayName;

                foreach (var profile in user.ProviderData)
                    if (profile != null && !string.IsNullOrEmpty(profile.Email))
                        return LocalPart(profile.Email);

                return LocalPart(user.Email);
            }
        }

        /** `snow2271@gmail.com` -> `snow2271`. @가 없으면 그대로 (public = 검사 대상) */
        public static string LocalPart(string email)
        {
            if (string.IsNullOrEmpty(email)) return string.Empty;

            int at = email.IndexOf('@');
            return at > 0 ? email.Substring(0, at) : email;
        }

        /** 이 공급자가 이미 붙어 있는가 */
        public static bool HasProvider(IAuthProvider provider)
        {
            var user = CurrentUser;
            if (user == null || provider == null) return false;

            foreach (var profile in user.ProviderData)
                if (profile != null && profile.ProviderId == provider.Id) return true;

            return false;
        }

        private static FirebaseUser CurrentUser
        {
            get
            {
                var auth = CloudScores.Auth;
                return auth != null ? auth.CurrentUser : null;
            }
        }

        // ---------------------------------------------------------------- 진입점

        /**
         * @brief 연동(또는 복구)을 한 번 시도한다. **예외를 던지지 않는다.**
         *
         * @return 실제로 정체성이 바뀌었으면 true (Link 성공 · Recover 성공)
         */
        public static async Task<bool> LinkAsync(IAuthProvider provider)
        {
            if (IsBusy) return false;
            if (provider == null) return false;

            IsBusy = true;
            try
            {
                return await RunAsync(provider);
            }
            catch (Exception e)
            {
                Fail("연동 중 문제가 생겼습니다", e);
                return false;
            }
            finally
            {
                IsBusy = false;
                Raise();
            }
        }

        private static async Task<bool> RunAsync(IAuthProvider provider)
        {
            if (!provider.IsAvailable)
            {
                LastPlan = LinkPlan.Nothing;
                Set(provider.DisplayName + " 로그인은 이 기기에서 쓸 수 없습니다");
                return false;
            }

            // [1] 익명 로그인까지는 서 있어야 한다. 붙일 대상이 없으면 연동이
            //     아니라 그냥 로그인이 되어 버리고, 그러면 이 기기의 진행이
            //     어느 계정에도 안 붙은 채 남는다
            Set("준비 중...");
            if (!await CloudScores.InitializeAsync()) return false;
            if (!await CloudScores.SignInAnonymouslyAsync()) return false;

            var user = CurrentUser;
            bool signedIn = user != null;
            bool anonymous = signedIn && user.IsAnonymous;
            bool already = HasProvider(provider);

            LastPlan = AccountLinkPolicy.PlanFor(signedIn, anonymous, already);
            Debug.Log(Tag + " 계획 = " + LastPlan + " (로그인 " + signedIn
                      + " / 익명 " + anonymous + " / 이미붙음 " + already + ")");

            if (LastPlan == LinkPlan.Nothing)
            {
                Set(already
                    ? provider.DisplayName + " 계정이 이미 연동돼 있습니다"
                    : "지금은 연동할 수 없습니다");
                return false;
            }
            if (LastPlan == LinkPlan.Retry)
            {
                Set("연결을 확인하고 다시 시도하세요");
                return false;
            }

            // [2] 네이티브 계정 선택창. 여기서 사람이 취소하는 것이 정상 경로다
            Set(provider.DisplayName + " 계정을 고르는 중...");
            var attempt = await provider.AcquireAsync();

            if (!attempt.Ok)
            {
                LastPlan = AccountLinkPolicy.PlanAfterAcquireFailure(attempt.Failure);
                Debug.Log(Tag + " 자격 증명 실패 = " + attempt.Failure + " / " + attempt.Message);
                Set(TextFor(attempt.Failure, provider));
                return false;
            }

            var credential = Firebase.Auth.GoogleAuthProvider.GetCredential(attempt.IdToken, null);

            // [3] 갈아타기 전에 **버려질 문서의 값을 먼저 읽어 둔다.**
            //     uid를 바꾼 뒤에는 그 문서를 읽을 이유를 잃는다(내 것이 아니게
            //     된다). 병합은 이 값이 있어야 성립한다
            string abandonedUid = user.UserId;
            int localStage = CloudScores.CurrentReach();
            int abandonedStage = 0;

            // [4] 연동을 먼저 시도한다. 성공하면 uid가 유지되고 이 스텝은 끝이다
            Set("계정을 연결하는 중...");
            var link = user.LinkWithCredentialAsync(credential);
            var linkFailure = await Await(link, "연동");

            if (linkFailure == AuthFailure.None)
            {
                string after = CurrentUser != null ? CurrentUser.UserId : abandonedUid;

                LastPlan = LinkPlan.Link;
                CloudScores.AdoptUser(CurrentUser);
                await RefreshUserAsync();

                Debug.Log(Tag + " ★ 연동 성공 - uid 유지 " + abandonedUid
                          + " -> " + after + " (같은가: " + (abandonedUid == after) + ")");
                Set(provider.DisplayName + " 계정이 연결되었습니다 - 기록이 이 계정에 남습니다");

                // 이름이 아직 서버에 안 갔을 수 있다. 같은 문서라 조건부 제출로
                // 충분하다(도달층이 안 올랐으면 이름 때문에만 나간다)
                var _ = CloudScores.SubmitIfHigherAsync(localStage);
                return true;
            }

            LastPlan = AccountLinkPolicy.PlanAfterLinkFailure(linkFailure);
            Debug.Log(Tag + " 연동 실패 = " + linkFailure + " -> 계획 " + LastPlan);

            if (LastPlan != LinkPlan.Recover)
            {
                Set(TextFor(linkFailure, provider));
                return false;
            }

            // [5] **복구.** 이 구글 계정에는 이미 uid가 있다 - 그리로 돌아간다
            Set("기존 계정을 찾았습니다 - 기록을 복구하는 중...");

            // ① uid를 갈아타기 **전에** 현재 로컬 세이브를 복구 후보로 보존한다
            //    (61단계, 설계 §8.2). 교체 뒤에 하면 "어느 세이브가 이 기기의
            //    것이었나"를 말할 근거가 흐려진다
            SaveData saveCandidate = CloudSaveRecovery.PrepareCandidate();

            abandonedStage = await CloudScores.FetchStageOfAsync(abandonedUid);

            var auth = CloudScores.Auth;
            var signIn = auth.SignInAndRetrieveDataWithCredentialAsync(credential);
            var signInFailure = await Await(signIn, "복구 로그인");

            if (signInFailure != AuthFailure.None)
            {
                Set(TextFor(signInFailure, provider));
                return false;
            }

            var recoveredUser = signIn.Result != null ? signIn.Result.User : CurrentUser;
            if (recoveredUser == null)
            {
                Fail("복구된 계정을 읽지 못했습니다", null);
                return false;
            }

            CloudScores.AdoptUser(recoveredUser);
            await RefreshUserAsync();

            Debug.Log(Tag + " ★ 복구 성공 - uid 교체 " + abandonedUid
                      + " -> " + recoveredUser.UserId);

            // ②~⑤ **게임 세이브**를 잇는다 (61단계). 기존 계정의 playerSaves를
            //    읽어 없으면 로컬이 첫 정본, 같으면 조용히, 다르면 충돌 화면이다.
            //    아래의 랭킹 max 병합과는 **다른 규칙**이다 - 랭킹은 값 하나의
            //    최고 기록이고, 세이브는 한 벌 통째 선택이다(설계 §8.2 꼬리).
            //    ⑥ 버려진 익명 save 문서는 어느 갈래도 지우지 않는다(규칙 4-B)
            CloudRecoveryPlan savePlan =
                await CloudSaveRecovery.RunAsync(recoveredUser.UserId, saveCandidate);
            Debug.Log(Tag + " 세이브 복구 갈래 = " + savePlan);

            int recoveredStage = await CloudScores.FetchReachAsync();
            if (recoveredStage < 0) recoveredStage = 0;

            // **이름도 기록의 일부다.** 지운 기기에는 이름이 없는데 돌아온
            // 문서에는 그 사람이 정한 이름이 적혀 있다 - 되살리지 않으면
            // 랭킹표에서는 "랑무사"인데 자기 화면에서는 "이름없는 무사"가 되어
            // 복구가 절반만 된 것으로 보인다.
            //
            // 스스로 정한 이름이 이미 있으면 **건드리지 않는다**. 그 값은
            // 이 기기의 사람이 방금 고른 것이고, 서버의 옛 이름으로 덮는 것은
            // 복구가 아니라 되돌리기다.
            if (AccountLinkPolicy.ShouldRestoreName(PlayerProfile.HasChosenName,
                                                    CloudScores.LastReadName))
            {
                PlayerProfile.Restore(CloudScores.LastReadName);
                Debug.Log(Tag + " 이름 복구: " + PlayerProfile.Name);
            }

            int merged = AccountLinkPolicy.MergedStage(localStage, abandonedStage, recoveredStage);
            Debug.Log(Tag + " 병합 - 로컬 " + localStage + " / 버린 문서 " + abandonedStage
                      + " / 복구된 문서 " + recoveredStage + " -> " + merged);

            if (AccountLinkPolicy.ShouldMergeAfterRecovery(merged, recoveredStage))
            {
                // 조건부 제출을 그대로 쓴다. 후퇴 방어·이름 갱신·범위 검사가
                // 전부 그 한 경로에 모여 있어서, 병합만 다른 문을 쓰면 그
                // 검사들이 병합에서만 빠진다
                bool wrote = await CloudScores.SubmitIfHigherAsync(merged);
                Debug.Log(Tag + " 병합 기록 " + (wrote ? "완료" : "생략(서버가 이미 높음)"));
            }

            // ⚠️ 버려진 익명 문서는 **지우지 않는다**. 규칙 4-B가 클라이언트
            //    delete를 막아 두었고, 그 판단은 이 스텝에서도 유효하다
            //    (AccountLinkPolicy.ShouldDeleteAbandonedDocument 주석)
            //
            // 세이브가 갈라졌으면 그 사실이 문장에 먼저 온다 - "복구했습니다"라고
            // 말해 놓고 진행이 옛것이면, 선택이 남았다는 것을 아무도 모른다
            Set(savePlan == CloudRecoveryPlan.AskTheHuman
                ? "계정을 복구했습니다 - 이어갈 기록을 선택하세요"
                : recoveredStage > 0
                    ? "기록을 복구했습니다 - 최고 " + Math.Max(merged, recoveredStage) + "층"
                    : provider.DisplayName + " 계정으로 로그인했습니다");

            return true;
        }

        /**
         * @brief 사용자 정보를 서버에서 한 번 다시 받아온다. **실패해도 그냥 넘어간다.**
         *
         * 연동 직후의 사용자 객체에는 공급자의 표시 이름이 아직 안 들어와 있다
         * (실기에서 이름 대신 메일 주소가 떴다). 이 호출이 그것을 채운다.
         *
         * 결과를 검사하지 않는 이유는 이것이 **표시의 문제**이기 때문이다 -
         * 연동은 이미 성공했고, 이름을 못 받아왔다고 그것을 실패로 되돌리면
         * 성공한 연결에 실패 문구가 붙는다.
         */
        private static async Task RefreshUserAsync()
        {
            try
            {
                var user = CurrentUser;
                if (user == null) return;

                await Await(user.ReloadAsync(), "사용자 정보 갱신");
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + " 사용자 정보 갱신 실패(무시): " + e.Message);
            }
        }

        // ---------------------------------------------------------------- 공통

        /**
         * @brief Firebase Task를 기다리고 실패를 AuthFailure로 좁힌다.
         *
         * Firebase는 예외를 AggregateException으로 싸서 주고, 그 안의
         * FirebaseException.ErrorCode가 진짜 원인이다. 그대로 문자열로 찍으면
         * `credential-already-in-use`가 껍질에 묻혀, **이 스텝에서 가장 중요한
         * 분기 신호**가 "그냥 실패"로 읽힌다.
         */
        private static async Task<AuthFailure> Await(Task task, string label)
        {
            var finished = await Task.WhenAny(task, Task.Delay(AuthTimeoutMs));

            if (finished != task)
            {
                Observe(task);
                Debug.LogWarning(Tag + " " + label + " 응답 없음 ("
                                 + (AuthTimeoutMs / 1000) + "초)");
                return AuthFailure.Network;
            }

            if (task.IsCanceled) return AuthFailure.Cancelled;
            if (!task.IsFaulted) return AuthFailure.None;

            var failure = Classify(task.Exception);
            Debug.LogWarning(Tag + " " + label + " 실패 -> " + failure
                             + "\n" + Describe(task.Exception));
            return failure;
        }

        // Firebase의 AuthError 값을 int로 꺼내 둔다. 테스트 어셈블리는
        // Firebase.Auth를 참조하지 않으므로(overrideReferences) 그쪽에서
        // AuthError를 직접 쓸 수 없다 - 분류 규칙을 검사하려면 이 창구가 필요하다
        public static readonly int ErrorCredentialAlreadyInUse = (int)AuthError.CredentialAlreadyInUse;
        public static readonly int ErrorEmailAlreadyInUse = (int)AuthError.EmailAlreadyInUse;
        public static readonly int ErrorAccountExistsWithDifferentCredentials =
            (int)AuthError.AccountExistsWithDifferentCredentials;
        public static readonly int ErrorNetworkRequestFailed = (int)AuthError.NetworkRequestFailed;
        public static readonly int ErrorProviderAlreadyLinked = (int)AuthError.ProviderAlreadyLinked;
        public static readonly int ErrorCancelled = (int)AuthError.Cancelled;

        /**
         * @brief 예외의 **종류와 코드**에서 갈래를 정한다.
         *
         * ⚠️ **실기에서 잡은 결함이 여기 있었다.** 처음에는 `FirebaseException`으로
         * 캐스팅해 ErrorCode만 봤는데, `LinkWithCredentialAsync`가 실패할 때
         * Firebase 유니티 SDK가 던지는 것은 **`FirebaseAccountLinkException`**이고
         * 그 타입은 `FirebaseException`을 **상속하지 않는다**. 그래서 캐스팅이
         * null이 되어 검사를 통째로 빠져나갔고, "이미 다른 계정에 연결됨"이
         * `Other`로 분류되어 **복구 갈래가 영영 안 열렸다**.
         *
         * 증상이 특히 나쁘다 - 에디터 테스트는 전부 통과하고(순수 규칙은
         * 멀쩡하다), 첫 연동도 성공하고, **재설치한 사람만** 조용히 실패한다.
         * 즉 이 스텝이 존재하는 이유가 되는 그 경로만 죽는다.
         *
         * 파일에서 갈라 둔 이유는 검사 때문이다. Firebase 예외 객체는 테스트에서
         * 만들 수 없지만(생성자가 SDK 내부), 이 함수는 bool과 int만 받는다.
         *
         * @param accountLinkException FirebaseAccountLinkException 이었는가
         */
        public static AuthFailure ClassifyError(bool accountLinkException, int errorCode)
        {
            // 셋 다 "이 구글 계정은 이미 임자가 있다"는 같은 사실의 다른
            // 표현이다. 어느 것이 오는지는 계정에 이메일이 붙어 있는지 등으로 갈린다
            if (errorCode == ErrorCredentialAlreadyInUse
                || errorCode == ErrorEmailAlreadyInUse
                || errorCode == ErrorAccountExistsWithDifferentCredentials)
                return AuthFailure.AlreadyInUse;

            if (errorCode == ErrorNetworkRequestFailed) return AuthFailure.Network;
            if (errorCode == ErrorCancelled) return AuthFailure.Cancelled;
            if (errorCode == ErrorProviderAlreadyLinked) return AuthFailure.Other;

            // 코드가 위 어디에도 안 맞는데 **타입이 AccountLink 예외**라면
            // 그 타입 자체가 답이다 - 이 예외는 "붙이려는 자격 증명에 다른
            // 주인이 있어서, 대신 로그인할 수 있도록 그쪽 정보를 함께 준다"는
            // 뜻으로만 던져진다(UserInfo를 들고 오는 이유가 그것이다)
            if (accountLinkException) return AuthFailure.AlreadyInUse;

            return AuthFailure.Other;
        }

        /** 예외 뭉치에서 첫 Firebase 예외를 찾아 갈래로 옮긴다 */
        internal static AuthFailure Classify(AggregateException aggregate)
        {
            if (aggregate == null) return AuthFailure.Other;

            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                var link = inner as FirebaseAccountLinkException;
                if (link != null) return ClassifyError(true, link.ErrorCode);

                var firebase = inner as FirebaseException;
                if (firebase != null) return ClassifyError(false, firebase.ErrorCode);
            }

            return AuthFailure.Other;
        }

        /** 사람이 읽을 한 줄. 취소는 오류처럼 보이면 안 된다 */
        private static string TextFor(AuthFailure failure, IAuthProvider provider)
        {
            switch (failure)
            {
                case AuthFailure.Cancelled:
                    // 취소는 아무 문구도 안 남긴다. 안 하겠다고 한 사람에게
                    // 결과를 통보할 이유가 없다
                    return string.Empty;
                case AuthFailure.NoCredential:
                    return "기기에 " + provider.DisplayName + " 계정이 없습니다 - 설정에서 추가하세요";
                case AuthFailure.Network:
                    return "연결을 확인하고 다시 시도하세요";
                case AuthFailure.Unsupported:
                    return provider.DisplayName + " 로그인은 이 기기에서 쓸 수 없습니다";
                default:
                    return provider.DisplayName + " 로그인에 실패했습니다";
            }
        }

        private static void Observe(Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                    Debug.LogWarning(Tag + " (늦게 도착한 실패) " + Describe(t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static string Describe(AggregateException aggregate)
        {
            if (aggregate == null) return "알 수 없는 오류";

            var flat = aggregate.Flatten();
            if (flat.InnerExceptions.Count == 0) return flat.Message;

            var first = flat.InnerExceptions[0];

            // 타입 이름과 ErrorCode를 **함께** 찍는다. 이 스텝의 결함이
            // "타입이 예상과 달랐다"였고, 타입만 있고 코드가 없으면(또는 그
            // 반대면) 그때 logcat만 보고는 원인을 못 짚는다
            var link = first as FirebaseAccountLinkException;
            if (link != null)
                return "FirebaseAccountLinkException(" + (AuthError)link.ErrorCode
                       + "/" + link.ErrorCode + "): " + link.Message;

            var firebase = first as FirebaseException;
            return firebase != null
                ? "FirebaseException(" + (AuthError)firebase.ErrorCode
                  + "/" + firebase.ErrorCode + "): " + firebase.Message
                : first.GetType().Name + ": " + first.Message;
        }

        private static void Set(string status)
        {
            Status = status;
            if (!string.IsNullOrEmpty(status)) Debug.Log(Tag + " " + status);
            Raise();
        }

        private static void Fail(string status, Exception e)
        {
            Status = status;
            // CloudScores와 같은 이유로 LogWarning이다 - 연동이 안 되는 것은
            // 이 게임에서 오류가 아니라 상태다
            Debug.LogWarning(Tag + " " + status + (e != null ? "\n" + e : string.Empty));
        }

        private static void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
