#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.AppCheck;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief App Check·정본 상태를 **한 화면 분량으로** 찍는 진단기 (62단계).
     *
     * ## 왜 파일 전체가 컴파일 가드 안인가
     *
     * 이 클래스가 하는 일은 토큰을 강제로 갱신하고 서버 정본을 읽는 것이다.
     * 출시 빌드에 남을 이유가 없고, 남으면 누구나 부를 수 있는 "지금 토큰
     * 받아와" 창구가 된다. 56·58단계가 seam에 세운 규칙과 같다: **플래그는
     * 뒤집히지만 컴파일에서 지운 코드는 뒤집히지 않는다.**
     *
     * ## 절대 찍지 않는 것
     *
     * App Check 토큰 원문. 그 문자열 하나면 이 프로젝트의 App Check를 통과한다 -
     * 스크린샷 한 장, 보고서 한 줄, logcat 한 줄로 새어 나가고, 새어 나간 뒤에는
     * 콘솔에서 앱을 갈아야 회수된다. 찍는 것은 **성공 여부·만료 시각·오류
     * 코드**뿐이고, 그 셋이면 진단에 모자람이 없다.
     *
     * ## 두 기기 실측이 쓰는 한 줄
     *
     * `Snapshot()`이 설계 §10의 기록표를 그대로 만든다 - uid·deviceId·sessionId는
     * 마스킹, baseRevision·서버 revision·state hash 앞 8자·maxStage·gems·
     * evolutionTier는 원문. 기기 A와 B가 같은 형식으로 찍어야 두 줄을 나란히
     * 놓고 비교할 수 있다.
     */
    public static class AppCheckDiagnostics
    {
        private const string Tag = "[AppCheck]";

        /** 토큰 요청이 이보다 오래 걸리면 진단을 포기한다 - 화면이 멈추면 안 된다 */
        private const int TokenTimeoutMs = 15000;

        /** 마지막 토큰 요청이 성공했는가 */
        public static bool LastTokenOk { get; private set; }

        /** 마지막으로 받은 토큰의 만료 시각 (UTC). 원문은 어디에도 없다 */
        public static DateTime LastTokenExpiry { get; private set; }

        /** 마지막 토큰 요청의 오류. 성공이면 빈 문자열 */
        public static string LastTokenError { get; private set; } = string.Empty;

        /**
         * @brief 진단이 Firestore로 **실제로 나간** 횟수 (62.1단계 검사용).
         *
         * 토큰이 실패한 뒤에는 이 값이 **늘면 안 된다.** "안 부른다"는 계약은
         * 주석으로는 지켜지지 않고, 세는 수가 있어야 검사가 잰다.
         */
        public static int ServerCallsForTests { get; private set; }

        /**
         * @brief 토큰 확인 상태를 손에 쥔다. **검사 전용.**
         *
         * 정상 경로(토큰이 있을 때 정본 읽기가 나가는가)를 재려면 성공 상태가
         * 필요한데, 그것을 만들려면 실제 App Check 토큰이 있어야 한다. 이
         * seam이 없으면 "막는다"만 검사하고 "통과시킨다"는 못 재게 되고,
         * 그러면 방어가 과해져 정상 경로까지 막혀도 아무도 모른다.
         */
        public static void SetTokenOkForTests(bool ok)
        {
            LastTokenOk = ok;
        }

        /** 정적 상태를 처음으로 되돌린다. **검사 전용** */
        public static void ResetForTests()
        {
            ServerCallsForTests = 0;
            LastTokenOk = false;
            LastTokenExpiry = default(DateTime);
            LastTokenError = string.Empty;
        }

        // ---------------------------------------------------------------- provider

        /** provider 종류와 배선 판정. SDK를 건드리지 않는다 */
        public static string ProviderLine()
        {
            FirebaseAppCheckBootstrap.EnsureConfigured();

            return "provider = " + FirebaseAppCheckBootstrap.Provider
                   + " (계획 " + FirebaseAppCheckBootstrap.PlannedProvider + ")"
                   + " / 배선 = " + FirebaseAppCheckBootstrap.Setup
                   + (string.IsNullOrEmpty(FirebaseAppCheckBootstrap.Detail)
                       ? string.Empty : "\n    " + FirebaseAppCheckBootstrap.Detail);
        }

        // ---------------------------------------------------------------- 토큰

        /**
         * @brief App Check 토큰을 **강제로 다시 받는다.**
         *
         * forceRefresh가 true여야 하는 이유는 캐시된 토큰이 한 시간을 산다는
         * 것이다 - 콘솔에서 디버그 토큰을 지우거나 Play Integrity 설정을 고친
         * 직후에도 캐시가 그대로 성공을 돌려주면, 무엇을 고쳤는지 알 수 없다.
         *
         * @return 사람이 읽는 결과 한 줄. **토큰 원문은 들어가지 않는다.**
         */
        public static async Task<string> RefreshTokenAsync()
        {
            FirebaseAppCheckBootstrap.EnsureConfigured();

            // ★ 62.1.1: **먼저 내린다.** 이 호출이 어디서 끝나든 옛 성공이
            // 살아남으면 안 된다 - 갱신이 실패했는데 `LastTokenOk`가 true로
            // 남으면 그 뒤의 정본 읽기가 "확인된 토큰"이라고 믿고 나간다.
            LastTokenOk = false;

            if (!FirebaseAppCheckBootstrap.IsConfigured)
            {
                LastTokenError = FirebaseAppCheckBootstrap.Setup.ToString();
                return "토큰 요청 안 함 - provider가 없다: "
                       + FirebaseAppCheckBootstrap.Detail;
            }

            try
            {
                Task<AppCheckToken> task =
                    FirebaseAppCheck.DefaultInstance.GetAppCheckTokenAsync(true);

                if (!await FirebaseRuntime.Completes(task, TokenTimeoutMs, "App Check 토큰"))
                {
                    LastTokenOk = false;
                    LastTokenError = task.Exception == null
                        ? "응답 없음 (" + (TokenTimeoutMs / 1000) + "초)"
                        : FirebaseRuntime.Flatten(task.Exception);
                    return "토큰 실패 -> " + LastTokenError;
                }

                // ★ 토큰 원문 프로퍼티는 **읽지 않는다.** 한 번 읽으면 문자열이
                //   되고, 문자열이 되면 로그·화면·예외 메시지 어딘가에 찍힌다.
                //   여기서 가져가는 것은 만료 시각뿐이다
                LastTokenOk = true;
                LastTokenExpiry = task.Result.ExpireTime.ToUniversalTime();
                LastTokenError = string.Empty;

                return "토큰 성공 (만료 " + LastTokenExpiry.ToString("yyyy-MM-dd HH:mm:ss")
                       + "Z, provider " + FirebaseAppCheckBootstrap.Provider + ")";
            }
            catch (Exception exception)
            {
                LastTokenOk = false;
                LastTokenError = exception.GetType().Name + ": " + exception.Message;
                return "토큰 예외 -> " + LastTokenError;
            }
        }

        // ---------------------------------------------------------------- 정본

        /**
         * @brief 서버 정본을 `Source.Server`로 읽고 revision을 적는다.
         *
         * 캐시가 아니라는 것이 중요하다 - App Check가 요청을 막고 있어도
         * Firestore 캐시는 옛 문서를 그대로 돌려주고, 그러면 "읽혔으니 통과했다"가
         * 거짓이 된다. 여기서 실패가 나야 enforcement가 실제로 작동하는 것이다.
         */
        public static async Task<string> ReadCanonicalAsync()
        {
            // ★★ 62.1.1 P1: **방어는 여기 있어야 한다.**
            //
            // `RunAsync`의 조기 중단만으로는 부족했다 - 테스트 패널의 `정본 읽기`
            // 버튼이 이 메서드를 **직접** 부르고, 그 버튼의 활성 조건은
            // `IsConfigured`뿐이었다. provider는 붙었는데 토큰 갱신이 실패한
            // 상태에서 누르면 미검증 요청이 그대로 나갔다.
            //
            // UI 한 곳만 고치면 호출자가 하나 늘 때 같은 구멍이 다시 열린다.
            // 그래서 나가는 문 안쪽에 둔다.
            if (!LastTokenOk)
                return TokenlessAbort;

            string uid = CloudScores.Uid;
            if (string.IsNullOrEmpty(uid)) return "정본 읽기 안 함 - 로그인 전이다";

            // 62.1단계: **여기가 진단이 Firestore를 만지는 유일한 자리다.**
            // 토큰 실패 뒤에 이 수가 늘면 §2 계약이 깨진 것이고, 검사가 그것을 잰다
            ServerCallsForTests++;

            CloudSaveFetchResult result = await CloudSaveStore.FetchAsync(uid);

            if (result.envelope == null)
                return "정본 읽기 -> " + result.status
                       + (result.fault == CloudSaveEnvelopeFault.None
                           ? string.Empty : " (" + result.fault + ")");

            CloudSaveEnvelope envelope = result.envelope;

            return "정본 읽기 -> " + result.status
                   + "\n    서버 revision = " + envelope.revision
                   + " (base " + envelope.baseRevision + ")"
                   + "\n    state hash    = " + Head(envelope.stateSha256)
                   + "\n    요약          = " + envelope.summary.maxStageReached + "층 · 보석 "
                   + envelope.summary.gems + " · 전직 " + envelope.summary.evolutionTier
                   + "\n    세션/기기     = " + Mask(envelope.sessionId) + " / "
                   + Mask(envelope.deviceId);
        }

        // ---------------------------------------------------------------- 실측 기록표

        /**
         * @brief 두 기기 실측 전에 남기는 한 줄 (설계 §10 · 62단계 5절).
         *
         * 서버 왕복이 없다. **로컬이 지금 무엇을 믿고 있는가**를 적는 것이고,
         * 그래야 왕복 뒤의 값과 비교해 무엇이 움직였는지 말할 수 있다.
         */
        public static string Snapshot()
        {
            var text = new StringBuilder();

            CloudSaveLocalState sidecar = CloudSaveSidecar.Load();
            SaveData local = SaveSystem.Load();

            text.AppendLine("uid       = " + Mask(CloudScores.Uid));
            text.AppendLine("device    = " + Mask(sidecar == null ? string.Empty : sidecar.deviceId));
            text.AppendLine("session   = " + Mask(CloudSaveSession.CurrentId));
            text.AppendLine("base rev  = " + (sidecar == null ? "(sidecar 없음)"
                                                              : sidecar.baseRevision.ToString()));
            text.AppendLine("pending   = " + (sidecar != null && sidecar.HasPending
                ? Mask(sidecar.pendingMutationId) : "없음"));

            if (local == null)
            {
                text.AppendLine("local     = (세이브 없음)");
            }
            else
            {
                text.AppendLine("state hash= " + Head(CloudSaveFingerprint.StateHashOf(local)));
                text.AppendLine("maxStage  = " + local.maxStageReached
                                + " / gems = " + local.gems
                                + " / evolutionTier = " + local.evolutionTier);
                text.AppendLine("saveVer   = v" + local.version);
            }

            text.AppendLine("상태      = " + CloudSaveCoordinator.State
                            + " · " + CloudSaveSync.StatusLine);
            text.Append(ProviderLine());

            return text.ToString();
        }

        /**
         * @brief provider -> 토큰 -> 정본까지 한 번에. 실측의 한 칸을 통째로 찍는다.
         */
        /** 토큰이 없어 진단을 멈췄을 때 보고서에 남는 표식. 검사가 이 문자열을 본다 */
        public const string AbortedMarker = "★ 진단 중단 - App Check 토큰 없음";

        /** 토큰이 확인되지 않아 정본 읽기를 거른 결과. **Firestore를 부르지 않았다** */
        public const string TokenlessAbort =
            "정본 읽기 안 함 - App Check 토큰이 확인되지 않았다 "
            + "(먼저 '토큰 강제 갱신'이 성공해야 합니다)";

        /**
         * @brief provider → 토큰 → 정본까지 한 번에.
         *
         * ## 토큰이 없으면 **여기서 끝난다** (62.1단계)
         *
         * 예전에는 토큰 요청이 실패해도 그 다음 줄에서 `Source.Server` 정본을
         * 읽었다. 그것이 나쁜 이유가 둘이다:
         *
         *   ① **미검증 요청을 우리 손으로 만든다.** enforcement를 켠 뒤에는 그
         *      요청이 거부되고, 거부된 요청은 App Check Metrics의 "확인되지 않음"에
         *      쌓인다 - 진단하려고 켠 도구가 진단 대상을 오염시킨다.
         *
         *   ② **읽히면 통과한 것처럼 읽힌다.** enforcement가 꺼져 있는 동안에는
         *      토큰 없이도 정본이 돌아온다. 그 성공을 보고 "App Check가 된다"고
         *      적으면 그것이 거짓 보고가 된다(62단계에서 실제로 조심해야 했던 자리다).
         *
         * 그래서 토큰이 실패하면 **Firestore를 한 번도 부르지 않고** 원인만 적고 만다.
         */
        public static async Task<string> RunAsync()
        {
            var text = new StringBuilder();

            text.AppendLine("---- App Check 진단 ----");
            text.AppendLine(Snapshot());
            text.AppendLine();

            string token = await RefreshTokenAsync();
            text.AppendLine(token);

            if (!LastTokenOk)
            {
                // ★ 여기서 끝. ReadCanonicalAsync를 부르지 않는다
                text.AppendLine(AbortedMarker);
                text.AppendLine("    " + (string.IsNullOrEmpty(LastTokenError)
                    ? FirebaseAppCheckBootstrap.Detail : LastTokenError));
                text.AppendLine("    토큰이 없는 요청은 enforcement 뒤에 거부되고, "
                                + "꺼져 있는 동안에는 통과한 것처럼 읽힙니다. 둘 다 진단을 흐립니다.");

                string aborted = text.ToString();
                Debug.LogWarning(Tag + "\n" + aborted);
                return aborted;
            }

            text.AppendLine(await ReadCanonicalAsync());

            string report = text.ToString();
            Debug.Log(Tag + "\n" + report);
            return report;
        }

        // ---------------------------------------------------------------- 가리기

        /**
         * @brief id를 앞 6자만 남긴다.
         *
         * 전부 지우면 두 기기의 줄이 같은 것인지 다른 것인지 못 본다. 전부 남기면
         * 보고서에 계정 식별자가 그대로 실린다 - 앞 6자는 실측 두 줄을 구분하기에
         * 충분하고, 그것으로 남의 문서를 찾아갈 수는 없다.
         */
        public static string Mask(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(없음)";
            if (id.Length <= 6) return id;
            return id.Substring(0, 6) + "…(" + id.Length + "자)";
        }

        /** 해시 앞 8자. 같은 기록인지 아닌지는 이만큼이면 갈린다 */
        public static string Head(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return "(없음)";
            return hash.Length <= 8 ? hash : hash.Substring(0, 8);
        }
    }
}
#endif
