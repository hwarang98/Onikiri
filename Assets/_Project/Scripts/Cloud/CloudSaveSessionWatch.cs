using System;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 작성권을 든 기기가 **자기 자리를 뺏겼는지** 지켜본다 (63단계).
     *
     * `GameSession.Update`가 매 프레임 `Tick()`을 부르고, 여기가 스스로
     * `SessionPollSeconds`(5초)를 잰다 - `CloudSaveSync`와 같은 모양이다.
     *
     * ## 리스너가 아니라 폴링인 이유
     *
     * 이 프로젝트의 Firestore 접근은 전부 단발 요청이다. 스냅샷 구독 하나를
     * 들여오면 수명 관리가 따라온다 - 씬 재로드(충돌 화면), 계정 교체(61단계
     * Recover), 앱 pause/resume, 그리고 도메인 리로드. 그 넷을 새로 다루는 값이
     * **최대 5초 지연**보다 크지 않다고 봤다. 실제 감지 지연은 보고서가 실측한다.
     *
     * 지연이 위험하지 않은 이유가 하나 더 있다: 이 5초 동안 이 기기가 서버에
     * 쓸 수 있는 것은 없다. 인수가 커밋되는 순간 세션 문서의 owner가 바뀌고,
     * `firestore.rules`의 `writerHoldsSession`이 그 시점부터 이 기기의 커밋을
     * 거부한다. **폴링이 늦는 것은 화면이 늦게 멈추는 것이지, 서버가 늦게
     * 잠기는 것이 아니다.**
     *
     * ## 무엇을 하지 않는가
     *
     * 세션을 다시 잡지 않는다. 뺏겼으면 뺏긴 것이고, 이 실행에서 되찾는 길은
     * 없다 - 되찾으려면 사람이 이 기기에서 다시 "이어하기"를 눌러야 하고,
     * 그것은 새 실행의 일이다. 자동으로 되찾으면 두 기기가 서로 뺏는 고리가 된다.
     */
    public static class CloudSaveSessionWatch
    {
        private const string Tag = "[CloudSave]";

        private static float lastPollAt = float.NegativeInfinity;
        private static float lastBeatAt = float.NegativeInfinity;
        private static bool polling;

        /** 마지막 폴링이 본 문서. 진단 화면이 읽는다 */
        public static CloudSaveSessionSnapshot LastSeen { get; private set; }

        /** 상실을 감지한 시각(realtime). 진단이 지연을 재는 데 쓴다 */
        public static float LostAt { get; private set; }

        /**
         * @brief 매 프레임 부른다. 주기는 스스로 잰다.
         *
         * 잠긴 뒤에는 아무것도 안 한다 - 상실은 한 번 일어나는 사건이고,
         * 이미 멈춘 기기가 5초마다 서버를 두드릴 이유가 없다.
         */
        public static void Tick()
        {
            if (CloudSavePlayLock.Locked) return;
            if (polling) return;

            // ★★ `HoldsWrite`가 아니라 **`HasHeldSeat`**를 본다.
            //
            // 세션 호출이 한 번 `Busy`를 받으면 `LastStatus`가 그것으로 바뀌고
            // `HoldsWrite`는 false가 된다. 거기서 감시를 멈추면 **자리를 잃은
            // 사실을 영영 못 본다** - 그 기기는 회수되지 않은 채 계속 놀고,
            // 쌓은 진행은 올라가지 못한다(63단계가 없애려는 갈라짐 그대로).
            // 실기에서 오프라인 복귀 기기가 정확히 그랬다.
            if (!CloudSaveSession.HasHeldSeat) return;

            string uid = CloudScores.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            float now = Now;
            if (now - lastPollAt < CloudSaveTakeoverPolicy.SessionPollSeconds) return;

            lastPollAt = now;

#if UNITY_EDITOR
            // 에디터에서 실서버로 나가는 것은 sync와 같은 게이트를 지난다.
            // 60단계 행 3회가 이 게이트가 없어 생긴 일이다
            if (!CloudSaveSync.EditorNetworkAllowed) return;
#endif
            polling = true;
            FirebaseRuntime.Observe(PollAsync(uid, now - lastBeatAt >= CloudSaveTakeoverPolicy.HeartbeatSeconds));
        }

        private static async System.Threading.Tasks.Task PollAsync(string uid, bool beat)
        {
            try
            {
                CloudSaveSessionSnapshot snapshot = await CloudSaveSession.PeekAsync(uid);
                LastSeen = snapshot;

                Examine(snapshot);

                // ★★ **남이 이어하기를 눌렀다. 스스로 물러난다** (설계 §4.1).
                //
                // 순서가 계약이다: 새 변경을 먼저 막고(BeginSeal) -> 지금 상태를
                // 로컬에 저장하고 -> 마지막 커밋을 시도하고 -> 세션을 놓고 ->
                // 그때 종료 팝업을 띄운다(CompleteSeal). 팝업이 앞에 오면
                // 화면은 "종료합니다"인데 뒤에서 몇 초 더 서버와 주고받는다.
                if (CloudSaveTakeoverPolicy.ShouldYieldSeat(snapshot, CloudSaveSession.CurrentId))
                {
                    await YieldAsync(uid);
                    return;
                }

                // ★ **시계를 감는 것이 여기여야 한다.** 62단계까지 heartbeat는
                // 커밋 경로에만 있었고, 그래서 "살아 있다"가 "최근에 썼다"와
                // 같은 뜻이었다. 갈라진 상태(Conflict)의 기기는 영영 안 쓰므로
                // 사람이 화면 앞에 있는데도 180초 뒤 자리를 잃는다 - 실측이 그것을
                // 잡았다. 잃은 뒤에는(위 Examine) 감지 않는다
                if (!beat || CloudSavePlayLock.Locked) return;

                lastBeatAt = Now;
                await CloudSaveSession.HeartbeatAsync(uid, CloudSaveSession.DeviceIdFor(uid));
            }
            finally
            {
                polling = false;
            }
        }

        /** 요청을 보고 자리를 넘긴다. 이 코루틴이 끝나면 이 기기는 타이틀로 간다 */
        private static async System.Threading.Tasks.Task YieldAsync(string uid)
        {
            if (!CloudSavePlayLock.BeginSeal("다른 기기에서 접속했습니다")) return;

            Debug.Log(Tag + " 인수 요청을 확인했습니다. 저장하고 자리를 넘깁니다.");

            LostAt = Now;

            // 봉인 중이라 이 한 벌은 지나간다(CloudSavePlayLock.AllowsFinalWrite)
            var save = CloudSaveSync.SaveRequested;
            bool saved = save == null || save();

            if (!saved)
                Debug.LogWarning(Tag + " 인계 직전 로컬 저장이 실패했습니다. "
                                 + "올릴 것은 직전 스냅샷뿐입니다.");

            await CloudSaveSync.YieldSeatAsync();

            Debug.Log(Tag + " 자리를 넘겼습니다. 이 기기의 플레이를 종료합니다.");
            CloudSavePlayLock.CompleteSeal();
        }

        /**
         * @brief 문서 하나를 보고 판정한다. **순수하게 나눠 둔 자리다** - 검사가
         *        Firebase 없이 이 한 줄을 지난다.
         */
        public static void Examine(CloudSaveSessionSnapshot snapshot)
        {
            if (CloudSavePlayLock.Locked) return;

            if (!CloudSaveTakeoverPolicy.LostWrite(snapshot, CloudSaveSession.CurrentId,
                                                   CloudSaveSession.MyGeneration))
                return;

            LostAt = Now;

            Debug.LogWarning(Tag + " 다른 기기가 세션을 인수했습니다 (세대 "
                             + CloudSaveSession.MyGeneration + " -> " + snapshot.generation
                             + "). 이 기기의 플레이를 멈춥니다.");

            CloudSavePlayLock.Engage("다른 기기에서 접속했습니다");
        }

        private static float Now
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (clockOverride != null) return clockOverride();
#endif
                return Time.realtimeSinceStartup;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static Func<float> clockOverride;

        public static void UseClockForTests(Func<float> clock)
        {
            clockOverride = clock;
        }

        public static void ResetForTests()
        {
            clockOverride = null;
            lastPollAt = float.NegativeInfinity;
            lastBeatAt = float.NegativeInfinity;
            polling = false;
            LostAt = 0f;
            LastSeen = default(CloudSaveSessionSnapshot);
        }
#endif
    }
}
