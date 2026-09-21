using System;
using Firebase.AppCheck;
using UnityEngine;

namespace Onikiri.Cloud
{
    /** 이 실행이 실제로 쓰는 App Check provider */
    public enum AppCheckProvider
    {
        /** 아직 정하지 않았다 (부트스트랩 전) */
        None,

        /** 에디터. 콘솔에 등록된 debug token으로 통과한다 */
        Debug,

        /** 안드로이드 실기. Play Integrity가 기기·앱을 증명한다 */
        PlayIntegrity,

        /** 이 플랫폼에는 아직 provider를 두지 않았다 */
        Unsupported
    }

    /** 부트스트랩이 남긴 한 줄 판정 */
    public enum AppCheckSetup
    {
        NotAttempted,

        /** provider factory가 붙었다 */
        Configured,

        /** 에디터인데 환경 변수에 debug token이 없다 - 실서버 왕복을 막는다 */
        MissingDebugToken,

        /** provider를 두지 않았다. Firebase는 그대로 부팅한다 */
        UnsupportedPlatform,

        /** SDK 호출이 예외를 냈다 */
        Failed
    }

    /**
     * @brief App Check provider를 **Firebase보다 먼저** 붙이는 단일 진입점 (62단계).
     *
     * ## 왜 순서가 전부인가
     *
     * `FirebaseAppCheck.SetAppCheckProviderFactory`는 **아직 만들어지지 않은**
     * FirebaseApp에 대해서만 온전히 먹는다. `CheckAndFixDependenciesAsync` ->
     * `FirebaseApp.DefaultInstance` -> `FirebaseAuth.GetAuth` -> `GetInstance`
     * 중 어느 하나라도 먼저 지나가면 그 인스턴스는 provider 없이 서고, 그
     * 뒤에 factory를 붙여도 이미 만들어진 핸들은 토큰을 달지 않는다. enforcement가
     * 켜진 순간 그 상태는 **전 요청 401**이다 - 그리고 그것은 코드가 아니라
     * 순서 때문에 나는 실패라, 로그만 봐서는 규칙이 틀린 것과 구분되지 않는다.
     *
     * 그래서 이 클래스는 초기화를 하지 않는다. **앞자리를 잡을 뿐이다.**
     * 부르는 곳은 둘뿐이고(CloudScores.InitializeCoreAsync ·
     * FirebaseRuntime.EnsureReadyAsync), 둘 다 자기 첫 줄에서 부른다.
     *
     * ## 멱등이 계약이다
     *
     * 두 경로가 각자 부르고, 계정 복구는 한 실행 안에서 초기화 경로를 다시
     * 밟는다. 두 번째 호출부터는 **아무 일도 하지 않는다** - factory를 두 번
     * 갈아 끼우면 이미 발급된 토큰의 출처가 흐려진다.
     *
     * ## 플랫폼 표
     *
     *   UNITY_EDITOR                    Debug        환경 변수의 token
     *   UNITY_ANDROID (실기)            PlayIntegrity 기기가 증명한다
     *   그 밖                            Unsupported  provider 없이 부팅은 계속
     *
     * 마지막 줄이 중요하다. iOS·데스크톱을 여기서 "지원 안 함"으로 **깨는** 것이
     * 아니라, provider를 안 붙이고 그대로 둔다 - 그쪽은 enforcement를 켜기 전까지
     * 지금처럼 돌고, 켜는 순간 거부된다는 사실이 `Setup`에 적혀 있다.
     *
     * ## Debug token은 코드에 없다
     *
     * 상수도, 씬도, ProjectSettings도 아니다. `ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN`
     * 환경 변수 하나뿐이고, 없으면 에디터의 실서버 왕복을 **막는다**(열어 두면
     * enforcement 뒤에 조용히 거부되는 요청이 나가고, 그것이 규칙 문제로 읽힌다).
     * 토큰 원문은 로그·화면·보고서 어디에도 찍지 않는다 - 그 문자열 하나면
     * 누구나 이 프로젝트의 App Check를 통과한다.
     */
    public static class FirebaseAppCheckBootstrap
    {
        private const string Tag = "[AppCheck]";

        private static bool attempted;

        /** 이 실행이 붙인 provider */
        public static AppCheckProvider Provider { get; private set; }

        /** 부트스트랩 판정 한 줄 */
        public static AppCheckSetup Setup { get; private set; }

        /** 사람이 읽는 원인. **토큰 원문은 절대 들어가지 않는다** */
        public static string Detail { get; private set; }

        /** 부트스트랩을 이미 지났는가 (순서 검사가 보는 값) */
        public static bool HasRun
        {
            get { return attempted; }
        }

        public static bool IsConfigured
        {
            get { return Setup == AppCheckSetup.Configured; }
        }

        /**
         * @brief 이 플랫폼이 쓸 provider. **부수효과가 없다.**
         *
         * 테스트와 진단 화면이 SDK를 건드리지 않고 같은 답을 봐야 해서 갈라 뒀다 -
         * 실제 배선(EnsureConfigured)도 이 값을 보고 갈래를 고른다.
         */
        public static AppCheckProvider PlannedProvider
        {
            get
            {
#if UNITY_EDITOR
                return AppCheckProvider.Debug;
#elif UNITY_ANDROID
                return AppCheckProvider.PlayIntegrity;
#else
                return AppCheckProvider.Unsupported;
#endif
            }
        }

        /**
         * @brief provider factory를 붙인다. **Firebase를 만지기 전에** 불러야 한다.
         *
         * 예외를 밖으로 내보내지 않는다. App Check 배선이 실패해도 게임은
         * 그대로 돌아야 하고(로컬 저장·전투는 Firebase와 무관하다), 클라우드만
         * 조용히 막히는 것이 정답이다 - 방치형에서 부팅 예외 하나는 몇 시간을
         * 날린다.
         */
        public static void EnsureConfigured()
        {
            if (attempted) return;
            attempted = true;

            try
            {
                Configure();
            }
            catch (Exception exception)
            {
                Provider = AppCheckProvider.None;
                Setup = AppCheckSetup.Failed;
                Detail = exception.GetType().Name + ": " + exception.Message;
                UnityEngine.Debug.LogWarning(Tag + " provider 배선 실패 - 클라우드만 막힙니다. "
                                             + Detail);
            }
        }

        private static void Configure()
        {
#if UNITY_EDITOR
            ConfigureDebugProvider();
#elif UNITY_ANDROID
            FirebaseAppCheck.SetAppCheckProviderFactory(PlayIntegrityProviderFactory.Instance);

            Provider = AppCheckProvider.PlayIntegrity;
            Setup = AppCheckSetup.Configured;
            Detail = "Play Integrity provider 배선됨";
            UnityEngine.Debug.Log(Tag + " provider = Play Integrity");
#else
            Provider = AppCheckProvider.Unsupported;
            Setup = AppCheckSetup.UnsupportedPlatform;
            Detail = "이 플랫폼(" + Application.platform + ")에는 provider를 두지 않았다. "
                     + "Firebase는 그대로 부팅하고, enforcement를 켜면 거부된다";
            UnityEngine.Debug.Log(Tag + " " + Detail);
#endif
        }

#if UNITY_EDITOR
        /**
         * @brief debug token을 읽는 **유일한** 곳. 파일도 상수도 씬도 아니다.
         *
         * 이 상수가 담는 것은 **변수 이름**이지 토큰이 아니다. 그리고 이 상수
         * 자체가 `#if UNITY_EDITOR` 안에 있다 - 출시 컴파일에는 debug token을
         * 읽는 코드도, 그 자리를 가리키는 이름도 남지 않는다.
         */
        public const string DebugTokenVariable = "ONIKIRI_FIREBASE_APPCHECK_DEBUG_TOKEN";

        /**
         * @brief 에디터의 Debug provider. 토큰은 환경 변수에서만 온다.
         *
         * 토큰이 없을 때 factory를 그래도 붙이면 SDK가 **새 토큰을 만들어 로그에
         * 찍는다** - 그 값은 콘솔에 등록돼 있지 않아 어차피 거부되고, 대신
         * 콘솔 로그에 비밀이 하나 굴러다니게 된다. 그래서 안 붙이고 막는다.
         */
        private static void ConfigureDebugProvider()
        {
            string token = ReadDebugToken();

            if (string.IsNullOrEmpty(token))
            {
                Provider = AppCheckProvider.None;
                Setup = AppCheckSetup.MissingDebugToken;
                Detail = "환경 변수 " + DebugTokenVariable + " 가 비어 있다. "
                         + "Firebase 콘솔 > App Check > 앱 > 디버그 토큰 관리에 등록한 값을 "
                         + "환경 변수로 넣고 에디터를 다시 켜면 된다";
                UnityEngine.Debug.LogWarning(Tag + " " + Detail);
                return;
            }

            DebugAppCheckProviderFactory.Instance.SetDebugToken(token);
            FirebaseAppCheck.SetAppCheckProviderFactory(DebugAppCheckProviderFactory.Instance);

            Provider = AppCheckProvider.Debug;
            Setup = AppCheckSetup.Configured;
            Detail = "Debug provider 배선됨 (토큰은 환경 변수에서 읽었고 여기 적지 않는다)";
            UnityEngine.Debug.Log(Tag + " provider = Debug (환경 변수 토큰)");
        }

        /** 환경 변수 한 줄. 값을 돌려주기만 하고 어디에도 남기지 않는다 */
        private static string ReadDebugToken()
        {
            string token = Environment.GetEnvironmentVariable(DebugTokenVariable);
            return token == null ? string.Empty : token.Trim();
        }

        /**
         * @brief 에디터에서 실서버 왕복을 시작해도 되는가.
         *
         * 기존 게이트(EditorServerCheckAllowed · EditorNetworkAllowed)를 대신하지
         * 않는다 - **그 위에 하나 더 얹는다.** 사람이 스위치를 켰더라도 토큰이
         * 없으면 나가는 요청은 enforcement 뒤에 전부 거부되고, 그 거부는 규칙
         * 문제와 구분되지 않는다.
         */
        public static bool EditorServerCallsAllowed
        {
            get
            {
                EnsureConfigured();
                return Setup == AppCheckSetup.Configured;
            }
        }

        /** 에디터에서 왜 막혔는지 화면에 적는 한 줄 */
        public static string EditorBlockReason
        {
            get
            {
                EnsureConfigured();
                return Setup == AppCheckSetup.Configured ? string.Empty : Detail;
            }
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /**
         * @brief 정적 상태를 처음으로 되돌린다. **테스트 전용이다.**
         *
         * SDK에 이미 붙은 factory는 되돌리지 않는다(SDK가 해제를 노출하지 않는다) -
         * 되돌리는 것은 이 클래스의 판정뿐이고, 그래서 테스트는 실제 배선이 아니라
         * `PlannedProvider`와 소스를 본다.
         */
        public static void ResetForTests()
        {
            attempted = false;
            Provider = AppCheckProvider.None;
            Setup = AppCheckSetup.NotAttempted;
            Detail = string.Empty;
        }
#endif
    }
}
