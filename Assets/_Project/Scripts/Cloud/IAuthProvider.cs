using System.Threading.Tasks;

namespace Onikiri.Cloud
{
    /**
     * @brief 로그인 공급자가 실패하는 방식. **Firebase 타입이 한 줄도 안 들어온다.**
     *
     * 네이티브(구글·애플)와 Firebase가 각자 다른 오류 체계로 실패하는데, 이
     * 스텝의 판단(연동할까 / 복구할까 / 포기할까)은 그 둘 중 어느 것도 아니라
     * **이 여섯 갈래**만 본다. 갈래를 여기서 한 번 좁혀 두면 규칙 계층
     * (AccountLinkPolicy)이 Firebase 없이 EditMode에서 검사된다.
     */
    public enum AuthFailure
    {
        None = 0,

        /**
         * @brief 이 자격 증명이 **이미 다른 계정에 붙어 있다.**
         *
         * 55단계의 알맹이가 이 값이다. 재설치한 기기가 예전 구글 계정으로
         * 연동을 시도할 때 오는 것이고, 실패가 아니라 **"복구하라"는 신호**다.
         */
        AlreadyInUse,

        /** 사람이 계정 선택창을 닫았다. 조용히 끝나야 한다 - 오류 문구를 띄우면 안 된다 */
        Cancelled,

        /** 기기에 쓸 수 있는 구글 계정이 없다. 사람이 설정에서 추가해야 한다 */
        NoCredential,

        /** 네트워크·시간 초과. 다시 눌러보면 되는 종류 */
        Network,

        /** 이 플랫폼에 구현이 없다 (에디터·애플 스텁). 버튼 자체를 감추는 근거 */
        Unsupported,

        Other,
    }

    /**
     * @brief 네이티브 로그인 한 번의 결과. Firebase에 넘길 **재료**이지 계정이 아니다.
     *
     * struct인 이유는 이 값이 한 번의 버튼 누름 동안만 살기 때문이다. 그리고
     * 여기에 uid가 없다는 것이 중요하다 - 네이티브 로그인은 "이 사람이 이
     * 구글 계정의 주인이다"까지만 증명하고, 그것을 **어느 uid에 붙일지**는
     * 그 다음 단계(연동이냐 복구냐)가 정한다.
     */
    public struct AuthAttempt
    {
        public bool Ok;

        /** Firebase 자격 증명을 만들 ID 토큰 (JWT) */
        public string IdToken;

        /**
         * @brief 애플이 쓰는 원본 nonce. 구글 경로에서는 늘 null.
         *
         * 지금 아무도 안 쓰는 필드를 미리 두는 이유는 애플의 자격 증명이
         * (idToken, rawNonce) 쌍이라서다 - 다음 스텝에서 이 struct를 고치면
         * 그때 IAuthProvider를 구현한 모든 곳이 함께 깨진다.
         */
        public string RawNonce;

        public AuthFailure Failure;

        /** 로그·화면에 그대로 적는 한 줄. 사람이 읽을 문장이어야 한다 */
        public string Message;

        public static AuthAttempt Success(string idToken, string rawNonce)
        {
            return new AuthAttempt { Ok = true, IdToken = idToken, RawNonce = rawNonce };
        }

        public static AuthAttempt Fail(AuthFailure failure, string message)
        {
            return new AuthAttempt { Ok = false, Failure = failure, Message = message };
        }
    }

    /**
     * @brief 계정 하나를 증명하는 방법. **애플이 두 번째로 들어올 자리다.**
     *
     * 지금 구현이 하나(구글)뿐인데 인터페이스를 두는 이유는 앱스토어 규정
     * 4.8 때문이다 - 구글 로그인을 제공하는 앱은 iOS에서 "Apple로 로그인"을
     * 반드시 함께 제공해야 한다. 즉 **두 번째 공급자가 오는 것이 이미 정해져
     * 있고**, 그때 고쳐야 할 것이 연동 흐름(AccountLink)·계정 UI·테스트까지
     * 번지면 그 스텝이 통째로 재작업이 된다.
     *
     * 그래서 이 인터페이스가 가르는 것은 딱 하나다 - **"네이티브 창을 띄워
     * ID 토큰을 받아온다"는 부분만 공급자마다 다르고, 그 뒤(연동·복구·병합)는
     * 전부 같다.** AppleSignInProvider가 지금 스텁으로 서 있는 것이 그 계약의
     * 증인이다(테스트가 스텁의 존재를 검사한다).
     */
    public interface IAuthProvider
    {
        /**
         * @brief Firebase가 아는 공급자 id. "google.com" / "apple.com".
         *
         * 우리가 지어낸 이름이 아니라 Firebase의 문자열을 그대로 쓴다 -
         * 연동 여부를 볼 때 FirebaseUser.ProviderData의 ProviderId와 이 값을
         * 비교하기 때문이다. 다른 이름을 쓰면 표를 하나 더 들고 다녀야 한다.
         */
        string Id { get; }

        /** 버튼에 적히는 이름. "구글" / "Apple" */
        string DisplayName { get; }

        /**
         * @brief 지금 이 기기에서 쓸 수 있는가.
         *
         * 에디터에서 false다(네이티브가 없다). 계정 UI는 이 값으로 버튼을
         * 감춘다 - 눌러도 아무 일이 안 일어나는 버튼은 20단계의 함정 버튼과
         * 같은 종류의 거짓말이다.
         */
        bool IsAvailable { get; }

        /**
         * @brief 네이티브 로그인 창을 띄우고 ID 토큰을 받아온다.
         *
         * **예외를 던지지 않는다.** 실패는 전부 AuthAttempt.Failure로 온다 -
         * 이 경로는 사람이 계정 선택창을 닫는 것이 정상 동작이라, 그것이
         * 예외로 올라오면 호출부가 "취소"와 "고장"을 구분하려고 예외 타입을
         * 뒤져야 한다.
         */
        Task<AuthAttempt> AcquireAsync();
    }

    /**
     * @brief 이 게임이 아는 공급자 전부. 계정 UI가 여기를 훑어 버튼을 만든다.
     *
     * 목록이 코드에 있고 화면에 없는 이유는, 공급자가 늘어나는 것이 기획이
     * 아니라 **플랫폼 규정**이기 때문이다(4.8). 화면은 이 목록에서 IsAvailable
     * 인 것만 그린다.
     */
    public static class AuthProviders
    {
        public static readonly IAuthProvider Google = new GoogleSignInProvider();

        /** 다음 스텝(Apple Developer 등록 + iOS 브링업)에서 속이 찬다 */
        public static readonly IAuthProvider Apple = new AppleSignInProvider();

        public static readonly IAuthProvider[] All = { Google, Apple };
    }
}
