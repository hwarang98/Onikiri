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
        Invalid,

        /** 인수 요청을 남겼다 (63단계). owner는 아직 상대다 */
        TakeoverRequested,

        /** 인수를 마쳤다 (63단계). generation이 1 올랐다 */
        TookOver
    }

    /**
     * @brief 단일 작성 세션. **63단계부터 세대(generation)를 들고 있다.**
     *
     * 마지막 방어선은 여전히 revision 트랜잭션이다(CloudSaveStore). 세션이 하는
     * 일은 두 기기가 동시에 켜져 있을 때 **미리** 알려 주는 것이고, 63단계부터는
     * 거기에 하나가 더 붙는다 - **한쪽을 실제로 멈춘다**(CloudSavePlayLock).
     *
     * ## 62단계와 무엇이 달라졌는가
     *
     * 옛 계약에서 "다른 기기가 살아 있다"는 `Busy`였고, 그 기기는 **커밋만 못 할
     * 뿐 계속 놀았다.** 두 기기가 각자 진행을 쌓다가 나중에 하나가 세션을 얻으면
     * 나머지 한 벌은 충돌 화면에서 버려진다. 63단계는 그 갈라짐을 만들지 않는다:
     * 새 기기는 **사람에게 묻고**, 승인되면 이전 기기가 정지한다.
     *
     * 다섯 모드다. 앞의 셋은 62단계 그대로이고 뒤의 둘이 63단계다.
     *
     *   Acquire          빈 자리·놓아준 자리·만료된 자리를 잡는다
     *   Heartbeat        내 세션의 시계를 감는다
     *   Release          내 세션을 놓는다
     *   RequestTakeover  **살아 있는 남의 자리에 요청만 남긴다** (owner 불변)
     *   Takeover         **owner를 바꾼다.** generation이 정확히 1 오른다
     *
     * 판정은 전부 트랜잭션 안에서 최신 문서를 읽고 내린다. 최종 판정은 언제나
     * `firestore.rules`이고, 여기 있는 검사는 거부될 쓰기를 안 보내기 위한 것이다.
     */
    public static class CloudSaveSession
    {
        private const string Tag = "[CloudSave]";

        public const string FieldSessionId = "sessionId";
        public const string FieldDeviceId = "deviceId";
        public const string FieldHeartbeatAt = "heartbeatAt";
        public const string FieldReleased = "released";

        // ---- 63단계
        public const string FieldGeneration = "generation";
        public const string FieldTakeoverSessionId = "takeoverSessionId";
        public const string FieldTakeoverDeviceId = "takeoverDeviceId";
        public const string FieldTakeoverAt = "takeoverAt";

        private enum Mode { Acquire, Heartbeat, Release, RequestTakeover, Takeover }

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

        /**
         * @brief 마지막 세션 요청의 결과. **시작값이 `Offline`이어야 한다.**
         *
         * ## 실기가 잡은 것 (63단계 첫 부팅)
         *
         * `Acquired`가 열거형의 0이라, 자동 속성의 기본값이 그것이었다. 즉
         * **한 번도 세션을 잡지 않은 기기가 `HoldsWrite == true`로 시작했다.**
         * 그 상태에서 감시가 첫 폴링을 돌고, 서버에 남아 있던 이전 실행의
         * 세션 문서를 보고 "다른 기기가 인수했다"로 읽었다 - 부팅 3초 만에
         * 자기 자신에게 회수당했다("세대 0 -> 0").
         *
         * 열거형의 순서를 바꾸지 않는 이유는 그 값들이 로그와 검사에 이름으로
         * 박혀 있기 때문이다. 시작값을 명시하는 쪽이 눈에 보인다.
         */
        public static CloudSaveSessionStatus LastStatus { get; private set; }
            = CloudSaveSessionStatus.Offline;

        /**
         * @brief 내가 owner가 됐을 때의 세대. 아직 못 잡았으면 0.
         *
         * 감시(`CloudSaveSessionWatch`)가 이 값과 서버 값을 대조해 **작성권을
         * 잃었는지** 판정한다. 내 세션 id 그대로 세대만 오른 문서는 규칙이 만들지
         * 못하지만, 그래도 본다 - 모르는 문서를 보면 쓰지 않는 쪽이 이 계약의
         * 방향이다.
         */
        public static long MyGeneration { get; private set; }

        /**
         * @brief 이 실행이 **한 번이라도 자리를 잡았는가.**
         *
         * `HoldsWrite`와 다른 물건이다 - 그쪽은 "지금 들고 있는가"이고 이쪽은
         * "가진 적이 있는가"다. 감시가 보는 것은 **이쪽**이어야 한다:
         * 세션 호출이 한 번 `Busy`를 받으면 `HoldsWrite`가 false가 되고, 거기서
         * 감시를 멈추면 **자리를 잃은 사실을 영영 못 본다.** 실기에서 그렇게 됐다 -
         * 오프라인에서 돌아온 기기가 회수되지 않은 채 계속 놀았다.
         */
        public static bool HasHeldSeat
        {
            get { return MyGeneration >= CloudSaveTakeoverPolicy.FirstGeneration; }
        }

        /**
         * @brief 지금 이 실행이 작성권을 들고 있는가.
         *
         * **세대를 함께 본다.** 상태 하나만으로는 "잡은 적 없음"과 "잡았음"이
         * 구분되지 않던 자리가 있었고(위 `LastStatus` 주석), 그 구분이 없으면
         * 감시가 부팅 직후부터 돌아 스스로를 회수한다. 세대는 owner가 된
         * 갈래에서만 기록되므로 **잡았다는 사실의 유일한 증거**다.
         */
        public static bool HoldsWrite
        {
            get
            {
                if (!CloudSavePlayLock.AllowsFinalWrite) return false;   // 63단계: 회수됐다
                if (MyGeneration < CloudSaveTakeoverPolicy.FirstGeneration) return false;

                return LastStatus == CloudSaveSessionStatus.Acquired
                       || LastStatus == CloudSaveSessionStatus.Renewed
                       || LastStatus == CloudSaveSessionStatus.TookOver;
            }
        }

        /**
         * @brief 이 설치의 기기 id. **사이드카에서 읽고, 없으면 만든다.**
         *
         * 세션 판정이 기기 단위로 서려면(같은 기기의 이전 실행에는 묻지 않는다)
         * 부르는 자리마다 같은 값이 나와야 한다. 사이드카가 없는 첫 실행에서
         * 매번 새로 만들면 그 기기는 **자기 자신과도 다른 기기**가 된다.
         */
        public static string DeviceIdFor(string uid)
        {
            CloudSaveLocalState sidecar = CloudSaveSidecar.Load();

            if (CloudSavePolicy.SidecarAppliesTo(sidecar, uid)
                && CloudSaveIds.IsValid(sidecar.deviceId))
                return sidecar.deviceId;

            if (!CloudSaveIds.IsValid(pendingDeviceId)) pendingDeviceId = CloudSaveSidecar.NewDeviceId();
            return pendingDeviceId;
        }

        /** 사이드카가 아직 없을 때 이 실행이 쓰는 기기 id (한 번만 만든다) */
        private static string pendingDeviceId;

        /**
         * @brief **다음 실행을 새로 시작한다.** 회수 뒤 타이틀로 나갈 때 부른다.
         *
         * ## 왜 필요한가 - 실기가 잡았다
         *
         * 씬을 다시 열어도 **정적 상태는 살아남는다.** 회수당한 기기가 확인을
         * 눌러 타이틀로 나가면 화면은 새 실행처럼 보이지만, `currentId`와
         * `MyGeneration`은 방금 자리를 잃은 그 실행의 것 그대로다. 그래서:
         *
         *   ① 감시가 첫 폴링에서 **또** 상실을 보고 잠금을 다시 건다
         *      (종료 팝업과 인수 확인이 겹쳐 뜬다)
         *   ② 그 상태에서 인수에 성공해도 `Save()`와 동기화가 잠겨 있어
         *      **다시는 저장하지 못한다**
         *
         * 세션 id는 "실행마다 하나"가 계약이다. 타이틀로 나가는 것이 곧 그
         * 실행의 끝이므로, 여기서 다음 실행의 몫을 비워 둔다.
         */
        public static void BeginNewRun()
        {
            currentId = null;                                   // 다음 사용에서 새로 만든다
            MyGeneration = 0L;
            LastStatus = CloudSaveSessionStatus.Offline;
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

        /**
         * @brief "이 기기로 이어하기"를 **남기기만** 한다 (63단계).
         *
         * owner도 heartbeat도 바꾸지 않는다. 상대가 이것을 보고 스스로 정리하면
         * 그때 `TakeoverAsync`가 자리를 받는다. 상대가 응답하지 않으면
         * `TakeoverWaitSeconds` 뒤에 강제 인수가 열린다.
         */
        public static Task<CloudSaveSessionStatus> RequestTakeoverAsync(string uid, string deviceId)
        {
            return WriteAsync(uid, deviceId, Mode.RequestTakeover);
        }

        /** owner를 이 기기로 바꾼다. generation이 정확히 1 오른다 (63단계) */
        public static Task<CloudSaveSessionStatus> TakeoverAsync(string uid, string deviceId)
        {
            return WriteAsync(uid, deviceId, Mode.Takeover);
        }

        /**
         * @brief 세션 문서를 **읽기만** 한다. 아무것도 쓰지 않는다 (63단계).
         *
         * 두 쪽이 쓴다: 새 기기가 팝업을 띄울지 정할 때, 그리고 작성권을 든
         * 기기가 자기 자리를 뺏겼는지 볼 때. 쓰기가 없으므로 규칙의 인수 조건과
         * 무관하고, 오프라인에서는 `exists = false`로 돌아온다 - 그 값을 상실로
         * 읽지 않는 것이 `CloudSaveTakeoverPolicy.LostWrite`의 계약이다.
         */
        public static async Task<CloudSaveSessionSnapshot> PeekAsync(string uid)
        {
            var empty = default(CloudSaveSessionSnapshot);
            empty.secondsSinceTakeoverRequest = -1f;

            if (string.IsNullOrEmpty(uid)) return empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 시늉이 **Firebase 준비보다 앞이다.** 검사에는 Firebase가 없고,
            // 뒤에 두면 이 seam은 영영 안 불린다
            if (peekOverride != null) return peekOverride(uid);
#endif

            if (!await FirebaseRuntime.EnsureReadyAsync()) return empty;

            DocumentReference reference = FirebaseRuntime.Db
                .Collection(CloudSaveEnvelope.SessionCollection).Document(uid);

            Task<DocumentSnapshot> task = reference.GetSnapshotAsync(Source.Server);

            if (!await FirebaseRuntime.Completes(task, FirebaseRuntime.RequestTimeoutMs, "세션 읽기"))
                return empty;

            return SnapshotFrom(task.Result);
        }

        private static async Task<CloudSaveSessionStatus> WriteAsync(string uid, string deviceId, Mode mode)
        {
            if (string.IsNullOrEmpty(uid) || !CloudSaveIds.IsValid(deviceId))
                return Finish(CloudSaveSessionStatus.Invalid);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (writeOverride != null) return FinishWrite(writeOverride(mode.ToString()));
#endif

            if (!await FirebaseRuntime.EnsureReadyAsync())
                return Finish(CloudSaveSessionStatus.Offline);

            FirebaseFirestore db = FirebaseRuntime.Db;
            DocumentReference reference = db.Collection(CloudSaveEnvelope.SessionCollection).Document(uid);
            string sessionId = CurrentId;

            long wonGeneration = 0L;

            Task<CloudSaveSessionStatus> task = db.RunTransactionAsync(async transaction =>
            {
                DocumentSnapshot document = await transaction.GetSnapshotAsync(reference);
                CloudSaveSessionSnapshot snapshot = SnapshotFrom(document);

                CloudSaveSessionStatus verdict = Verdict(snapshot, sessionId, deviceId, mode);
                if (verdict != CloudSaveSessionStatus.Acquired
                    && verdict != CloudSaveSessionStatus.Renewed
                    && verdict != CloudSaveSessionStatus.Released
                    && verdict != CloudSaveSessionStatus.TakeoverRequested
                    && verdict != CloudSaveSessionStatus.TookOver)
                    return verdict;

                Dictionary<string, object> fields = Fields(snapshot, sessionId, deviceId, mode, verdict);

                // 요청을 남기는 것은 owner 쪽 값을 하나도 안 바꾼다. 그래서
                // 트랜잭션 안에서 읽은 그 문서의 값을 그대로 되쓴다
                transaction.Set(reference, fields);

                wonGeneration = (long)fields[FieldGeneration];
                return verdict;
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

            CloudSaveSessionStatus result = task.Result;

            // 내가 owner가 된 갈래에서만 세대를 기억한다. 요청만 남긴 것은
            // owner가 아니므로 여기 들어오지 않는다 - 그것이 중요하다:
            // 요청 뒤에 감시가 "세대 불일치"로 스스로를 멈추면 안 된다
            if (result == CloudSaveSessionStatus.Acquired
                || result == CloudSaveSessionStatus.Renewed
                || result == CloudSaveSessionStatus.TookOver)
                MyGeneration = wonGeneration;

            if (result == CloudSaveSessionStatus.TookOver)
                Debug.Log(Tag + " 세션을 인수했습니다 (세대 " + wonGeneration + ").");

            return Finish(result);
        }

        /**
         * @brief 이 모드가 지금 문서에서 성립하는가. **규칙과 같은 판단이다.**
         */
        private static CloudSaveSessionStatus Verdict(CloudSaveSessionSnapshot snapshot,
                                                      string sessionId, string deviceId, Mode mode)
        {
            CloudSaveTakeoverStep step =
                CloudSaveTakeoverPolicy.StepFor(snapshot, sessionId, deviceId);

            switch (mode)
            {
                case Mode.Acquire:
                    if (step == CloudSaveTakeoverStep.Renew) return CloudSaveSessionStatus.Renewed;
                    if (step == CloudSaveTakeoverStep.Claim) return CloudSaveSessionStatus.Acquired;
                    return CloudSaveSessionStatus.Busy;

                // heartbeat와 release는 **내 세션일 때만** 쓴다. 만료된 남의 자리를
                // heartbeat로 조용히 가져가면, 코디네이터는 자기가 작성권을 언제
                // 얻었는지 모르는 채로 쓰기를 시작한다
                case Mode.Heartbeat:
                    return step == CloudSaveTakeoverStep.Renew
                        ? CloudSaveSessionStatus.Renewed : CloudSaveSessionStatus.Busy;

                case Mode.Release:
                    return step == CloudSaveTakeoverStep.Renew
                        ? CloudSaveSessionStatus.Released : CloudSaveSessionStatus.Busy;

                case Mode.RequestTakeover:
                    return CloudSaveTakeoverPolicy.MayRequestTakeover(snapshot, sessionId)
                        ? CloudSaveSessionStatus.TakeoverRequested : CloudSaveSessionStatus.Busy;

                default:
                    // 빈 자리·놓아준 자리·만료는 인수가 아니라 그냥 잡는 것이다.
                    // 그래도 세대는 오른다(owner가 바뀌므로) - 규칙의 takeoverCommit
                    if (step == CloudSaveTakeoverStep.Renew) return CloudSaveSessionStatus.Renewed;
                    if (step == CloudSaveTakeoverStep.Claim
                        || CloudSaveTakeoverPolicy.MayCommitTakeover(snapshot, sessionId, deviceId))
                        return CloudSaveSessionStatus.TookOver;
                    return CloudSaveSessionStatus.Busy;
            }
        }

        /**
         * @brief 문서에 쓸 여덟 필드. **모드마다 무엇이 그대로인지가 계약이다.**
         */
        private static Dictionary<string, object> Fields(CloudSaveSessionSnapshot snapshot,
                                                         string sessionId, string deviceId,
                                                         Mode mode, CloudSaveSessionStatus verdict)
        {
            bool keepsOwner = mode == Mode.RequestTakeover;

            // owner를 바꾸는 갈래에서만 세대가 오른다. 갱신은 그대로 둔다
            long generation;
            if (keepsOwner || verdict == CloudSaveSessionStatus.Renewed
                || verdict == CloudSaveSessionStatus.Released)
                generation = snapshot.generation >= CloudSaveTakeoverPolicy.FirstGeneration
                    ? snapshot.generation
                    : CloudSaveTakeoverPolicy.FirstGeneration;
            else
                generation = CloudSaveTakeoverPolicy.NextGeneration(snapshot.generation);

            var fields = new Dictionary<string, object>
            {
                { FieldSessionId, keepsOwner ? snapshot.ownerSessionId : sessionId },
                { FieldDeviceId, keepsOwner ? snapshot.ownerDeviceId : deviceId },
                { FieldReleased, keepsOwner ? snapshot.released : mode == Mode.Release },
                { FieldGeneration, generation }
            };

            if (keepsOwner)
            {
                // 요청은 상대의 만료 시계를 감지 않는다. 감으면 강제 인수가
                // 영영 안 된다 - 규칙도 heartbeatAt 불변을 요구한다
                fields[FieldHeartbeatAt] = snapshot.heartbeatAt;
                fields[FieldTakeoverSessionId] = sessionId;
                fields[FieldTakeoverDeviceId] = deviceId;
                fields[FieldTakeoverAt] = FieldValue.ServerTimestamp;
                return fields;
            }

            // 서버 시각이어야 한다. 기기 시각을 적으면 만료가 조작 가능해지고,
            // 미래로 적힌 heartbeat는 그 세션을 영원히 살아 있게 만든다
            fields[FieldHeartbeatAt] = FieldValue.ServerTimestamp;

            if (verdict == CloudSaveSessionStatus.Renewed
                || verdict == CloudSaveSessionStatus.Released)
            {
                // 내 세션의 갱신은 **요청을 지우지 않는다.** 지울 수 있으면
                // 응답하지 않는 기기가 heartbeat만으로 상대를 영원히 막는다
                fields[FieldTakeoverSessionId] = snapshot.takeoverSessionId ?? string.Empty;
                fields[FieldTakeoverDeviceId] = snapshot.takeoverDeviceId ?? string.Empty;
                fields[FieldTakeoverAt] = snapshot.takeoverAt;
                return fields;
            }

            // owner가 됐다. 처리된 요청은 지운다
            fields[FieldTakeoverSessionId] = string.Empty;
            fields[FieldTakeoverDeviceId] = string.Empty;
            fields[FieldTakeoverAt] = FieldValue.ServerTimestamp;
            return fields;
        }

        private static CloudSaveSessionSnapshot SnapshotFrom(DocumentSnapshot document)
        {
            var snapshot = default(CloudSaveSessionSnapshot);
            snapshot.secondsSinceTakeoverRequest = -1f;
            snapshot.takeoverSessionId = string.Empty;
            snapshot.takeoverDeviceId = string.Empty;

            if (document == null || !document.Exists) return snapshot;

            Dictionary<string, object> data = document.ToDictionary();

            snapshot.exists = true;
            snapshot.ownerSessionId = Read(data, FieldSessionId) as string;
            snapshot.ownerDeviceId = Read(data, FieldDeviceId) as string;

            object released = Read(data, FieldReleased);
            snapshot.released = released is bool && (bool)released;

            snapshot.generation = LongOf(Read(data, FieldGeneration));

            snapshot.takeoverSessionId = (Read(data, FieldTakeoverSessionId) as string) ?? string.Empty;
            snapshot.takeoverDeviceId = (Read(data, FieldTakeoverDeviceId) as string) ?? string.Empty;

            object heartbeat = Read(data, FieldHeartbeatAt);
            snapshot.heartbeatAt = heartbeat;
            snapshot.takeoverAt = Read(data, FieldTakeoverAt);

            // heartbeat가 없거나 못 읽는 문서는 **만료로 치지 않는다.** 규칙도
            // 그 비교에서 오류를 내 거부하므로, 여기서 가져갈 수 있다고 답하면
            // 반드시 거부되는 쓰기를 보내게 된다
            snapshot.secondsSinceHeartbeat = AgeOf(heartbeat);

            snapshot.secondsSinceTakeoverRequest =
                CloudSaveIds.IsValid(snapshot.takeoverSessionId) ? AgeOf(snapshot.takeoverAt) : -1f;

            return snapshot;
        }

        /** 서버 시각에서 지금까지 몇 초인가. 못 읽으면 0(=살아 있는 것으로 본다) */
        private static float AgeOf(object timestamp)
        {
            long ticks = FirebaseRuntime.TicksOf(timestamp);
            if (ticks <= 0L) return 0f;

            return (float)((DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond);
        }

        private static long LongOf(object value)
        {
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            return 0L;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static Func<string, CloudSaveSessionSnapshot> peekOverride;
        private static Func<string, CloudSaveSessionStatus> writeOverride;

        /**
         * @brief 세션 쓰기를 손에 쥔다. 모드 이름을 받아 결과를 돌려준다.
         *
         * 실제 쓰기와 **같은 뒷정리를 지난다**(FinishWrite) - 세대를 기억하는
         * 자리가 갈리면 검사는 통과하는데 실기가 다르게 도는 계약이 된다.
         */
        public static void UseWriteForTests(Func<string, CloudSaveSessionStatus> write)
        {
            writeOverride = write;
        }

        /** 시늉한 결과도 실제와 같은 자리에서 세대를 기억한다 */
        private static CloudSaveSessionStatus FinishWrite(CloudSaveSessionStatus status)
        {
            if (status == CloudSaveSessionStatus.Acquired
                || status == CloudSaveSessionStatus.Renewed
                || status == CloudSaveSessionStatus.TookOver)
                MyGeneration = MyGeneration < CloudSaveTakeoverPolicy.FirstGeneration
                    ? CloudSaveTakeoverPolicy.FirstGeneration
                    : MyGeneration;

            return Finish(status);
        }

        /** 세션 문서를 손에 쥔다. Firebase 없이 감시·인수 계약을 잰다 */
        public static void UsePeekForTests(Func<string, CloudSaveSessionSnapshot> peek)
        {
            peekOverride = peek;
        }

        public static void UseSessionIdForTests(string sessionId)
        {
            currentId = sessionId;
        }

        public static void UseGenerationForTests(long generation)
        {
            MyGeneration = generation;
        }

        public static void ResetForTests()
        {
            peekOverride = null;
            writeOverride = null;
            currentId = null;
            pendingDeviceId = null;
            MyGeneration = 0L;
            LastStatus = CloudSaveSessionStatus.Offline;
        }
#endif
    }
}
