using System.Threading.Tasks;

namespace Onikiri.Cloud
{
    /**
     * @brief 구글 공급자. 네이티브에서 ID 토큰을 받아오는 것까지만 한다.
     *
     * 이름이 GoogleAuthProvider가 아닌 이유는 Firebase에 같은 이름의 타입이
     * 이미 있어서다(Firebase.Auth.GoogleAuthProvider - 자격 증명을 만드는 쪽).
     * 둘이 한 파일에서 만나는 자리가 실제로 있으므로(AccountLink) 이름을
     * 갈라 두지 않으면 매번 전체 이름을 적어야 한다.
     */
    public sealed class GoogleSignInProvider : IAuthProvider
    {
        /**
         * @brief Firebase 콘솔이 Google 공급자를 켤 때 자동 생성한 **웹 클라이언트 ID**.
         *
         * 안드로이드 클라이언트 ID(client_type 1)가 아니다. 네이티브 로그인에
         * 넘기는 serverClientId는 "이 토큰을 받을 서버가 누구인가"를 가리키고,
         * 우리 서버는 Firebase다 - 그 자리에 안드로이드 클라이언트 ID를 넣으면
         * 토큰은 발급되지만 audience가 어긋나 Firebase가 조용히 거절한다.
         *
         * ⚠️ 이 상수는 `Assets/google-services.json`의 `client_type: 3` 항목과
         * **같은 값이어야 한다.** 사람이 다른 Firebase 프로젝트의 설정 파일을
         * 받아 덮으면 여기만 옛 값으로 남고, 그 실패는 빌드가 아니라 기기에서
         * 나온다. 그래서 `AccountLinkTests`가 그 파일을 읽어 이 상수와 대조한다
         * (54단계에서 규칙 파일의 상수를 대조한 것과 같은 처리).
         */
        public const string WebClientId =
            "598938331304-nt0tg1rlqnflnn2evnga4ffck698c88v.apps.googleusercontent.com";

        /** Firebase가 아는 공급자 id. FirebaseUser.ProviderData와 이 값을 비교한다 */
        public string Id { get { return "google.com"; } }

        public string DisplayName { get { return "구글"; } }

        public bool IsAvailable { get { return GoogleIdTokenBridge.IsSupported; } }

        public Task<AuthAttempt> AcquireAsync()
        {
            return GoogleIdTokenBridge.RequestAsync(WebClientId);
        }
    }
}
