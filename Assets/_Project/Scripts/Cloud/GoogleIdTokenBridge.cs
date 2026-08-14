using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 안드로이드 네이티브(OnikiriGoogleAuth.java)로 가는 **유일한 통로**.
     *
     * 이 파일 밖에는 AndroidJavaObject가 한 줄도 없다. 그렇게 가둔 이유는
     * 플랫폼 코드가 새면 그 순간 계정 흐름 전체가 안드로이드 전용이 되기
     * 때문이다 - 다음 스텝에서 iOS가 오고, 그때 갈아 끼울 것은 **이 파일
     * 하나**여야 한다(IAuthProvider가 그 계약이다).
     *
     * ## 콜백은 유니티 스레드가 아니다
     *
     * Credential Manager는 결과를 자기 Executor 스레드에서 준다. 그래서
     * Listener 안에서는 유니티 API를 한 줄도 부르지 않고, 값만
     * TaskCompletionSource에 넣는다. await 하던 쪽은 유니티가 깔아 둔
     * SynchronizationContext 덕분에 **메인 스레드에서** 이어진다 - 이 규칙을
     * 어기면 증상이 "가끔 멈춘다"로 나와 원인을 찾기가 아주 어렵다.
     */
    public static class GoogleIdTokenBridge
    {
        private const string Tag = "[GoogleAuth]";

        private const string JavaClass = "com.studio202.onikiri.auth.OnikiriGoogleAuth";
        private const string JavaListener = JavaClass + "$Listener";

        /**
         * @brief 계정 선택창을 여기까지 기다린다 (초).
         *
         * 무한정 기다리지 않는 이유는 이 Task 뒤에 버튼의 활성 상태가 걸려
         * 있어서다 - 영영 안 끝나면 버튼이 영영 죽는다. 3분은 사람이 계정을
         * 고르고 비밀번호까지 넣기에 넉넉하고, 넘어가면 그건 사람이 창을
         * 덮어둔 채 다른 일을 하는 상황이라 실패로 접는 편이 맞다.
         */
        private const int TimeoutMs = 180000;

        /**
         * @brief 진행 중인 콜백 수신자. **GC 방지용으로 붙잡아 둔다.**
         *
         * 자바 쪽이 이 프록시를 들고 있는 동안 C# 쪽에서 참조가 끊기면
         * 수집 대상이 되고, 그때 콜백이 오면 이미 없는 객체를 부른다.
         */
        private static object pending;

        /** 이 플랫폼에서 네이티브 로그인이 가능한가 */
        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /**
         * @brief 계정 선택창을 띄우고 ID 토큰을 받아온다. **예외를 던지지 않는다.**
         *
         * @param serverClientId 웹 클라이언트 ID (client_type 3)
         */
        public static async Task<AuthAttempt> RequestAsync(string serverClientId)
        {
            if (!IsSupported)
                return AuthAttempt.Fail(AuthFailure.Unsupported,
                    "이 플랫폼에서는 구글 로그인을 쓸 수 없습니다 (안드로이드 실기 전용)");

            if (string.IsNullOrEmpty(serverClientId))
                return AuthAttempt.Fail(AuthFailure.Other, "웹 클라이언트 ID가 비었습니다");

#if UNITY_ANDROID && !UNITY_EDITOR
            var source = new TaskCompletionSource<AuthAttempt>();

            try
            {
                var listener = new Listener(source);
                pending = listener;

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var auth = new AndroidJavaClass(JavaClass))
                {
                    auth.CallStatic("requestIdToken", activity, serverClientId, listener);
                }
            }
            catch (Exception e)
            {
                pending = null;
                // 플러그인이 빌드에 안 실렸을 때 여기로 온다(클래스 없음).
                // 흔한 원인은 EDM4U resolve를 안 돌린 것이다
                Debug.LogWarning(Tag + " 네이티브 호출 실패: " + e);
                return AuthAttempt.Fail(AuthFailure.Other, "구글 로그인 모듈을 불러오지 못했습니다");
            }

            var finished = await Task.WhenAny(source.Task, Task.Delay(TimeoutMs));
            pending = null;

            if (finished != source.Task)
                return AuthAttempt.Fail(AuthFailure.Network, "로그인 응답이 없습니다 (3분 초과)");

            return source.Task.Result;
#else
            await Task.CompletedTask;
            return AuthAttempt.Fail(AuthFailure.Unsupported, "안드로이드 실기 전용");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /**
         * @brief 자바 인터페이스를 C#으로 구현한 것. 값만 옮기고 아무것도 안 한다.
         *
         * 메서드 이름이 자바 쪽과 **글자 그대로** 같아야 한다(AndroidJavaProxy가
         * 이름으로 찾는다). onToken/onError는 자바 관례의 소문자 시작이라
         * C# 관례와 어긋나 보이지만, 여기서 C# 관례를 따르면 호출이 조용히
         * 안 온다.
         */
        private sealed class Listener : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<AuthAttempt> source;

            public Listener(TaskCompletionSource<AuthAttempt> source)
                : base(JavaListener)
            {
                this.source = source;
            }

            public void onToken(string idToken)
            {
                // 여기서 Debug.Log를 부르지 않는다 - 유니티 스레드가 아니다
                source.TrySetResult(AuthAttempt.Success(idToken, null));
            }

            public void onError(string code, string message)
            {
                source.TrySetResult(AuthAttempt.Fail(Translate(code), message));
            }
        }
#endif

        /**
         * @brief 자바가 준 짧은 코드를 AuthFailure로 옮긴다. **자바 쪽 상수와 짝이다.**
         *
         * public인 이유는 테스트가 이 표를 자바 파일과 대조하기 때문이다.
         * 두 언어에 같은 문자열이 두 벌 적혀 있고, 갈리면 **취소가 오류로**
         * 읽힌다 - 그것은 기기에서만 드러나는 종류의 어긋남이다
         * (54단계가 규칙 파일의 상수를 같은 방식으로 대조한 것과 같은 처리).
         */
        public static AuthFailure Translate(string code)
        {
            if (code == "cancelled") return AuthFailure.Cancelled;
            if (code == "no_credential") return AuthFailure.NoCredential;
            return AuthFailure.Other;
        }
    }
}
