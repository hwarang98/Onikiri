using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

namespace Onikiri.Cloud
{
    /** 세션 요청의 결과. 화면과 logcat이 이 이름을 그대로 쓴다 */
    public enum CloudSaveSessionStatus
    {
        /** 빈 자리·놓아준 자리·만료된 자리를 가져왔다 */
        Acquired = 0,

        /** 이미 내 세션이었다. heartbeat만 갱신했다 */
        Renewed,

        /** 내가 놓아줬다 */
        Released,

        /** 다른 기기가 살아 있다 - "다른 기기에서 플레이 중" */
        Busy,

        /** 네트워크. 실패가 아니라 정상 경로다 */
        Offline,

        /** 규칙 거부·예외 */
        Failed,

        /** 부를 자격이 없다 (uid·기기 id 형식) */
        Invalid
    }

    /**
     * @brief 소프트 단일 작성 세션. **정합성의 방어선이 아니다.**
     *
     * 마지막 방어선은 언제나 revision 트랜잭션이다(CloudSaveStore). 오프라인
     * 기기의 세션은 어차피 만료되고, 그 상태에서 두 기기가 갈라지는 것을 막을
     * 방법은 없다 - 그때 갈라짐은 충돌 화면으로 **드러나야** 하지, 세션이
     * 막았다고 믿으면 안 된다.
     *
     * 이 문서가 하는 일은 하나다: 두 기기가 동시에 켜져 있을 때 **미리** 알려
     * 주는 것. 그것만으로 충돌 화면을 보는 횟수가 크게 준다.
     *
     * ## 인수 규칙은 세 갈래뿐이다
     *
     *     자리가 비었다 · 놓아줬다 · 180초 동안 조용했다
     *
     * 같은 판단을 보안 규칙이 서버에서 한 번 더 한다(firestore.rules
     * `mayTakeSession`). **최종 판정은 규칙**이다 - 만료는 서버 시각으로만
     * 정확하고, 기기 시계는 어긋나거나 사용자가 바꿀 수 있다. 여기 있는 검사는
     * 거부될 쓰기를 아예 안 보내기 위한 것이다.
     */
    public static class CloudSaveSession
    {
        private const string Tag = "[CloudSave]";

        public const string FieldSessionId = "sessionId";
        public const string FieldDeviceId = "deviceId";
        public const string FieldHeartbeatAt = "heartbeatAt";
        public const string FieldReleased = "released";

        private enum Mode { Acquire, Heartbeat, Release }

        private static string currentId;

        /**
         * @brief 이 앱 실행의 세션 id. **실행마다 하나**, 처음 쓸 때 만든다.
         *
         * 기기 id와 다른 물건이다 - 기기는 설치마다 하나이고 세션은 실행마다
         * 하나다. 앱을 껐다 켜면 새 세션이고, 그래서 이전 실행이 놓아주지 못한
         * 세션(강제 종료)은 만료로만 풀린다.
         */
        public static string CurrentId
        {
            get
            {
                if (!CloudSaveIds.IsValid(currentId)) currentId = CloudSaveIds.New();
                return currentId;
            }
        }

        public static CloudSaveSessionStatus LastStatus { get; private set; }

        /** 지금 이 실행이 작성권을 들고 있는가 (마지막 결과 기준) */
        public static bool HoldsWrite
        {
            get
            {
                return LastStatus == CloudSaveSessionStatus.Acquired
                       || LastStatus == CloudSaveSessionStatus.Renewed;
            }
        }

        public static Task<CloudSaveSessionStatus> AcquireAsync(string uid, string deviceId)
        {
            return WriteAsync(uid, deviceId, Mode.Acquire);
        }

        public static Task<CloudSaveSessionStatus> HeartbeatAsync(string uid, string deviceId)
        {
            return WriteAsync(uid, deviceId, Mode.Heartbeat);
        }

        /**
         * @brief 작성권을 놓는다. 앱이 뒤로 갈 때 부른다.
         *
         * 놓아주면 다른 기기가 **만료를 기다리지 않고** 곧바로 이어받는다.
         * 놓지 못한 채 죽어도(강제 종료·배터리) 180초 뒤 자동으로 풀리므로
         * 이 호출은 최선의 노력이지 계약이 아니다.
         */
        public static Task<CloudSaveSessionStatus> ReleaseAsync(string uid, string deviceId)
        {
            return WriteAsync(uid, deviceId, Mode.Release);
        }

        private static async Task<CloudSaveSessionStatus> WriteAsync(string uid, string deviceId, Mode mode)
        {
            if (string.IsNullOrEmpty(uid) || !CloudSaveIds.IsValid(deviceId))
                return Finish(CloudSaveSessionStatus.Invalid);

            if (!await FirebaseRuntime.EnsureReadyAsync())
                return Finish(CloudSaveSessionStatus.Offline);

            FirebaseFirestore db = FirebaseRuntime.Db;
            DocumentReference reference = db.Collection(CloudSaveEnvelope.SessionCollection).Document(uid);
            string sessionId = CurrentId;

            Task<CloudSaveSessionStatus> task = db.RunTransactionAsync(async transaction =>
            {
                DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(reference);
                CloudSaveSessionClaim claim = ClaimFrom(snapshot, sessionId);

                if (claim == CloudSaveSessionClaim.Busy) return CloudSaveSessionStatus.Busy;

                // heartbeat와 release는 **내 세션일 때만** 쓴다. 만료된 남의 자리를
                // heartbeat로 조용히 가져가면, 코디네이터는 자기가 작성권을 언제
                // 얻었는지 모르는 채로 쓰기를 시작한다
                if (mode != Mode.Acquire && claim != CloudSaveSessionClaim.Renew)
                    return CloudSaveSessionStatus.Busy;

                transaction.Set(reference, Fields(sessionId, deviceId, mode == Mode.Release));

                if (mode == Mode.Release) return CloudSaveSessionStatus.Released;
                return claim == CloudSaveSessionClaim.Renew
                    ? CloudSaveSessionStatus.Renewed
                    : CloudSaveSessionStatus.Acquired;
            });

            if (!await FirebaseRuntime.Completes(task, FirebaseRuntime.RequestTimeoutMs, "세션 " + mode))
            {
                if (task.IsFaulted && !FirebaseRuntime.LooksOffline(task.Exception))
                {
                    Debug.LogWarning(Tag + " 세션 " + mode + " 실패: "
                                     + FirebaseRuntime.Flatten(task.Exception));
                    return Finish(CloudSaveSessionStatus.Failed);
                }

                return Finish(CloudSaveSessionStatus.Offline);
            }

            return Finish(task.Result);
        }

        private static Dictionary<string, object> Fields(string sessionId, string deviceId, bool released)
        {
            return new Dictionary<string, object>
            {
                { FieldSessionId, sessionId },
                { FieldDeviceId, deviceId },
                { FieldReleased, released },

                // 서버 시각이어야 한다. 기기 시각을 적으면 만료가 조작 가능해지고,
                // 미래로 적힌 heartbeat는 그 세션을 영원히 살아 있게 만든다
                { FieldHeartbeatAt, FieldValue.ServerTimestamp }
            };
        }

        private static CloudSaveSessionClaim ClaimFrom(DocumentSnapshot snapshot, string sessionId)
        {
            if (snapshot == null || !snapshot.Exists)
                return CloudSavePolicy.ClaimFor(false, null, false, 0f, sessionId);

            Dictionary<string, object> data = snapshot.ToDictionary();

            string owner = Read(data, FieldSessionId) as string;
            object releasedValue = Read(data, FieldReleased);
            bool released = releasedValue is bool && (bool)releasedValue;

            long ticks = FirebaseRuntime.TicksOf(Read(data, FieldHeartbeatAt));

            // heartbeat가 없거나 못 읽는 문서는 **만료로 치지 않는다.** 규칙도
            // 그 비교에서 오류를 내 거부하므로, 여기서 가져갈 수 있다고 답하면
            // 반드시 거부되는 쓰기를 보내게 된다
            float seconds = ticks > 0L
                ? (float)((DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond)
                : 0f;

            return CloudSavePolicy.ClaimFor(true, owner, released, seconds, sessionId);
        }

        private static object Read(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) ? value : null;
        }

        private static CloudSaveSessionStatus Finish(CloudSaveSessionStatus status)
        {
            LastStatus = status;
            return status;
        }
    }
}
