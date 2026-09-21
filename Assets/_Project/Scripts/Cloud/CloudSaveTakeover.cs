using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Onikiri.Cloud
{
    /** 인수 흐름이 지금 어디에 있는가. 팝업이 이 값으로 무엇을 그릴지 정한다 */
    public enum CloudSaveTakeoverPhase
    {
        /** 아직 아무것도 안 했다 */
        Idle = 0,

        /** 세션 문서를 읽는 중 */
        Checking,

        /** ★ **사람의 승인을 기다린다.** 게임은 시작되지 않는다 */
        AwaitingConfirm,

        /** 승인을 받아 요청을 남기는 중 */
        Requesting,

        /** 요청을 남겼다. 이전 기기가 정리하기를 기다린다 */
        Waiting,

        /** 작성권을 얻었다. 이제 서버 정본을 다시 조회한다 */
        Acquired,

        /** 사람이 취소했다. **세션 문서도 게임 상태도 그대로다** */
        Cancelled,

        /** 서버에 닿지 못했다. 오프라인은 실패가 아니라 정상 경로다 */
        Offline,

        /** 기다렸는데 자리가 나지 않았다 */
        TimedOut
    }

    /**
     * @brief 새 기기(B)의 **부팅 게이트** (63단계).
     *
     * 순서가 이 파일의 전부다:
     *
     *     세션 확인 -> (다른 기기가 살아 있으면) **사람에게 묻는다** ->
     *     승인 -> 인수 요청 -> 이전 기기가 정리 -> 작성권 획득 ->
     *     **서버 정본 재조회**(CloudSaveCoordinator) -> 게임 시작
     *
     * ## 왜 부팅 앞이어야 하는가
     *
     * 62단계까지 세션은 **첫 커밋 직전**에 잡혔다(`CloudSaveSync.CommitAsync`).
     * 그래서 두 기기가 켜져 있으면 둘 다 놀았고, 늦게 커밋하는 쪽이 조용히
     * 보류됐다. 그 사이 두 기기가 각자 진행을 쌓고, 나중에 하나가 세션을 얻으면
     * **나머지 한 벌은 충돌 화면에서 버려진다.** 갈라짐을 뒤에서 처리하는 대신
     * 앞에서 만들지 않는 것이 63단계다.
     *
     * ## 취소는 게임을 시작하지 않는다
     *
     * "취소하고 로컬로 논다"는 갈래를 두지 않았다. 그것이 정확히 62단계의
     * 갈라짐이기 때문이다 - 로컬로 논 진행은 서버에 못 올라가고, 다음 부팅이
     * 충돌 화면에서 그것을 버리라고 묻는다. 취소하면 **대기 화면**에 머물고,
     * 사람이 "다시 확인"을 누르거나 앱을 끄면 된다.
     *
     * ## 오프라인은 막지 않는다
     *
     * 서버에 못 닿으면 게이트를 열고 지나간다. 방치형 게임의 오프라인 계약이
     * 세션보다 먼저다 - 지하철에서 게임이 안 열리는 것은 어떤 정합성보다 나쁘다.
     * 그 경우 이 기기는 작성권 없이 놀고, 커밋은 `writerHoldsSession`이 막는다.
     */
    public static class CloudSaveTakeover
    {
        private const string Tag = "[CloudSave]";

        /**
         * @brief 인수가 성립하지 않을 때 포기하는 시각 (초).
         *
         * 강제 인수가 익는 데 `TakeoverWaitSeconds`(20초)가 걸리고, 그 뒤로
         * 폴링 두어 번의 여유를 둔다. 무한히 기다리면 화면이 영영 안 열린다.
         */
        public const float GiveUpSeconds =
            CloudSaveTakeoverPolicy.TakeoverWaitSeconds + 3f * CloudSaveTakeoverPolicy.SessionPollSeconds;

        public static CloudSaveTakeoverPhase Phase { get; private set; }

        /** 마지막으로 읽은 세션 문서. 팝업이 "언제부터 켜져 있었는가"를 그린다 */
        public static CloudSaveSessionSnapshot LastSeen { get; private set; }

        /** 국면이 바뀌면 울린다. 팝업이 구독해 자기를 열고 닫는다 */
        public static event Action<CloudSaveTakeoverPhase> PhaseChanged;

        private static bool confirmed;
        private static bool cancelled;

        /**
         * @brief "이 기기로 이어하기". **연속으로 눌러도 한 번만 먹는다.**
         *
         * 승인을 기다리는 국면에서만 받는다. 요청이 이미 나간 뒤의 입력은
         * 조용히 무시된다 - 두 번 누른 사람에게 두 번째 요청을 보내면
         * `takeoverAt`이 갱신되어 강제 인수 시계가 처음부터 다시 간다.
         */
        public static void Confirm()
        {
            if (Phase != CloudSaveTakeoverPhase.AwaitingConfirm) return;
            confirmed = true;
        }

        /** "취소". 세션 문서를 건드리지 않았으므로 되돌릴 것이 없다 */
        public static void Cancel()
        {
            if (Phase != CloudSaveTakeoverPhase.AwaitingConfirm) return;
            cancelled = true;
        }

        /**
         * @brief 부팅에서 작성권을 확보한다. **게임 시작 전에 끝난다.**
         *
         * 이 코루틴이 끝난 뒤 `Phase`가 `Acquired`나 `Offline`이면 부팅이
         * 이어진다(서버 정본 재조회 -> 판정 -> 적용). `Cancelled`·`TimedOut`이면
         * 부팅은 **거기서 멈춘다** - 게임이 시작되지 않는다.
         */
        public static IEnumerator EnsureSession(string uid, string deviceId)
        {
            confirmed = false;
            cancelled = false;

            SetPhase(CloudSaveTakeoverPhase.Checking);

            if (string.IsNullOrEmpty(uid) || !CloudSaveIds.IsValid(deviceId))
            {
                SetPhase(CloudSaveTakeoverPhase.Offline);
                yield break;
            }

            // ---- [1] 지금 자리가 어떤가
            Task<CloudSaveSessionSnapshot> peek = CloudSaveSession.PeekAsync(uid);
            while (!peek.IsCompleted) yield return null;

            if (peek.IsFaulted || peek.IsCanceled)
            {
                Debug.LogWarning(Tag + " 세션 확인 실패 - 작성권 없이 진행합니다.");
                SetPhase(CloudSaveTakeoverPhase.Offline);
                yield break;
            }

            LastSeen = peek.Result;

            CloudSaveTakeoverStep step =
                CloudSaveTakeoverPolicy.StepFor(LastSeen, CloudSaveSession.CurrentId, deviceId);

            // 서버를 못 봤다(문서가 없다고 온 것과 구분되지 않는다). 오프라인
            // 계약이 먼저다 - 게이트를 열고 지나간다. 커밋은 규칙이 막는다
            if (!LastSeen.exists && !FirebaseRuntime.IsReady)
            {
                SetPhase(CloudSaveTakeoverPhase.Offline);
                yield break;
            }

            // ---- [2] 빈 자리·놓아준 자리·만료. **묻지 않고 잡는다**
            if (step != CloudSaveTakeoverStep.AskTheHuman)
            {
                yield return Acquire(uid, deviceId);
                yield break;
            }

            // ---- [3] 다른 기기가 살아 있다. **사람에게 묻는다**
            Debug.Log(Tag + " 다른 기기가 세션을 쥐고 있습니다. 인수 확인을 띄웁니다.");
            SetPhase(CloudSaveTakeoverPhase.AwaitingConfirm);

            while (!confirmed && !cancelled) yield return null;

            if (cancelled)
            {
                // **세션 문서에 아무것도 안 썼다.** 되돌릴 것이 없다
                Debug.Log(Tag + " 인수를 취소했습니다. 세션 문서는 그대로입니다.");
                SetPhase(CloudSaveTakeoverPhase.Cancelled);
                yield break;
            }

            // ---- [4] 요청을 남긴다. owner는 아직 상대다
            SetPhase(CloudSaveTakeoverPhase.Requesting);

            Task<CloudSaveSessionStatus> request =
                CloudSaveSession.RequestTakeoverAsync(uid, deviceId);
            while (!request.IsCompleted) yield return null;

            if (request.IsFaulted || request.IsCanceled
                || request.Result == CloudSaveSessionStatus.Offline)
            {
                SetPhase(CloudSaveTakeoverPhase.Offline);
                yield break;
            }

            // ---- [5] 자리가 나기를 기다린다.
            //
            // 온라인 A는 요청을 보고 저장·커밋·release를 마친다 - 그러면 첫
            // 폴링에서 곧바로 이어받는다. 오프라인 A는 응답하지 않고, 요청이
            // `TakeoverWaitSeconds`만큼 익으면 **강제 인수**가 열린다.
            // 두 갈래가 `TakeoverAsync` 하나로 모인다(정책이 판정한다).
            SetPhase(CloudSaveTakeoverPhase.Waiting);

            float started = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - started < GiveUpSeconds)
            {
                float waited = 0f;
                while (waited < CloudSaveTakeoverPolicy.SessionPollSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                Task<CloudSaveSessionStatus> attempt = CloudSaveSession.TakeoverAsync(uid, deviceId);
                while (!attempt.IsCompleted) yield return null;

                if (attempt.IsFaulted || attempt.IsCanceled) continue;

                if (attempt.Result == CloudSaveSessionStatus.TookOver
                    || attempt.Result == CloudSaveSessionStatus.Renewed
                    || attempt.Result == CloudSaveSessionStatus.Acquired)
                {
                    Debug.Log(Tag + " 인수 완료 (세대 " + CloudSaveSession.MyGeneration + ").");
                    SetPhase(CloudSaveTakeoverPhase.Acquired);
                    yield break;
                }
            }

            Debug.LogWarning(Tag + " 인수가 " + GiveUpSeconds + "초 안에 성립하지 않았습니다.");
            SetPhase(CloudSaveTakeoverPhase.TimedOut);
        }

        private static IEnumerator Acquire(string uid, string deviceId)
        {
            Task<CloudSaveSessionStatus> task = CloudSaveSession.AcquireAsync(uid, deviceId);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted || task.IsCanceled)
            {
                SetPhase(CloudSaveTakeoverPhase.Offline);
                yield break;
            }

            SetPhase(task.Result == CloudSaveSessionStatus.Acquired
                     || task.Result == CloudSaveSessionStatus.Renewed
                     || task.Result == CloudSaveSessionStatus.TookOver
                ? CloudSaveTakeoverPhase.Acquired
                : CloudSaveTakeoverPhase.Offline);
        }

        /** 부팅이 이 게이트를 지나 **게임을 시작해도 되는가** */
        public static bool MayStartGame
        {
            get
            {
                return Phase == CloudSaveTakeoverPhase.Idle
                       || Phase == CloudSaveTakeoverPhase.Acquired
                       || Phase == CloudSaveTakeoverPhase.Offline;
            }
        }

        private static void SetPhase(CloudSaveTakeoverPhase next)
        {
            if (Phase == next) return;

            Phase = next;

            var handler = PhaseChanged;
            if (handler != null) handler(next);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ResetForTests()
        {
            Phase = CloudSaveTakeoverPhase.Idle;
            LastSeen = default(CloudSaveSessionSnapshot);
            PhaseChanged = null;
            confirmed = false;
            cancelled = false;
        }

        /** 사람이 누른 것과 같은 자리로 들어간다. PlayMode가 버튼 없이 잰다 */
        public static bool ConfirmedForTests { get { return confirmed; } }
        public static bool CancelledForTests { get { return cancelled; } }
#endif
    }
}
