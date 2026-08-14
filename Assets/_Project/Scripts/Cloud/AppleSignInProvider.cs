using System.Threading.Tasks;

namespace Onikiri.Cloud
{
    /**
     * @brief 애플 공급자 **스텁**. 시그니처만 있고 속이 없다.
     *
     * ## 왜 빈 클래스를 지금 만드나
     *
     * 앱스토어 심사 규정 4.8은 제3자 로그인(구글)을 제공하는 앱에 "Apple로
     * 로그인"을 **동반 제공하도록** 요구한다. 즉 이 클래스가 채워지는 것은
     * 기획 항목이 아니라 **출시 조건**이고, 언제 오느냐만 남은 일이다.
     *
     * 그 사실을 코드에 적어 두지 않으면 다음 스텝이 "구글 전용으로 짜인 흐름을
     * 두 공급자용으로 고치는 일"부터 시작하게 된다. 지금 스텁을 세워 두면
     * AccountLink·계정 UI·테스트가 처음부터 **공급자를 인자로 받는 모양**으로
     * 지어지고, 다음 스텝은 이 파일 하나만 채우면 된다.
     *
     * ## 지금 무엇이 없나
     *
     *   - Apple Developer 등록($99)이 없어 Service ID·키를 만들 수 없다
     *   - Firebase 콘솔에 Apple 공급자가 꺼져 있다
     *   - iOS 앱 등록·GoogleService-Info.plist가 없다
     *   - 맥/Xcode 빌드를 한 번도 안 했다
     *
     * 그래서 IsAvailable이 **늘 false**다. 계정 UI는 이 값을 보고 버튼을
     * 아예 안 그린다 - 눌리지 않는 회색 버튼으로 두면 "곧 나옴"을 약속하는
     * 것이 되고, 그 약속의 기한을 우리가 못 정한다.
     *
     * ## 채울 때 할 일 (다음 스텝)
     *
     * 안드로이드에서도 애플 로그인은 가능하다 - Firebase의
     * FederatedOAuthProvider("apple.com")가 웹 흐름으로 처리한다. iOS는
     * 네이티브 ASAuthorizationAppleIDProvider가 필요하고, 그때 AuthAttempt의
     * RawNonce 필드가 쓰인다(애플 자격 증명은 idToken 하나가 아니라
     * idToken + 원본 nonce 쌍이다 - 그래서 그 필드를 지금 비워 둔 채로 뒀다).
     */
    public sealed class AppleSignInProvider : IAuthProvider
    {
        public string Id { get { return "apple.com"; } }

        public string DisplayName { get { return "Apple"; } }

        /** 구현이 없다. 이 값이 true가 되는 날이 4.8을 충족하는 날이다 */
        public bool IsAvailable { get { return false; } }

        public Task<AuthAttempt> AcquireAsync()
        {
            return Task.FromResult(AuthAttempt.Fail(AuthFailure.Unsupported,
                "Apple 로그인은 아직 준비되지 않았습니다"));
        }
    }
}
