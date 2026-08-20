using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 서버 왕복 하나의 결과. **성공/실패 두 갈래로 두지 않는다.**
     *
     * 부르는 쪽이 해야 할 일이 갈래마다 다르기 때문이다 - Conflict는 사람에게
     * 물어야 하고, Busy는 잠시 뒤 다시, Offline은 로컬 dirty로 두고 조용히,
     * Invalid는 아무것도 하지 말아야 한다. bool 하나면 그 넷이 같은 자리에 섞인다.
     */
    public enum CloudSaveStoreStatus
    {
        /** 서버 정본을 읽었다 */
        Found = 0,

        /** 서버에 문서가 없다 */
        Missing,

        /** 새 정본을 올렸다 */
        Committed,

        /**
         * @brief 이 mutation은 서버에 **이미 적용돼 있었다.**
         *
         * 응답이 유실된 커밋의 재시도가 여기 온다. revision을 하나 더 올리지
         * 않는 것이 이 갈래의 전부다(57단계 계약).
         */
        AlreadyApplied,

        /** 서버 revision이 이 기기의 baseRevision과 다르다. 사람이 고른다 */
        Conflict,

        /** 다른 기기가 작성권을 들고 있다 */
        Busy,

        /** 보낼 수 없는 상태다 (sidecar 불일치·봉투 생성 실패·서버 문서 소실) */
        Invalid,

        /** 네트워크. 실패가 아니라 정상 경로다 */
        Offline,

        /** 규칙 거부·예외 */
        Failed
    }

    public struct CloudSaveFetchResult
    {
        public CloudSaveStoreStatus status;

        /** Found일 때만 채워진다. **Validate를 통과한 것만 여기 온다** */
        public CloudSaveEnvelope envelope;

        /** Invalid일 때의 이유 */
        public CloudSaveEnvelopeFault fault;

        public override string ToString()
        {
            return fault == CloudSaveEnvelopeFault.None ? status.ToString() : status + " (" + fault + ")";
        }
    }

    public struct CloudSaveCommitResult
    {
        public CloudSaveStoreStatus status;

        /** 서버 정본의 revision (Committed·AlreadyApplied·Conflict에서 의미 있다) */
        public long revision;

        /** Invalid일 때 사람에게 보일 이유 */
        public CloudSaveBlock block;

        public bool IsSynced
        {
            get
            {
                return status == CloudSaveStoreStatus.Committed
                       || status == CloudSaveStoreStatus.AlreadyApplied;
            }
        }

        public override string ToString()
        {
            return block == CloudSaveBlock.None
                ? status + " rev" + revision
                : status + " (" + block + ")";
        }
    }

    /**
     * @brief 세이브 정본의 서버 저장소. **일반 SetAsync를 쓰지 않는다.**
     *
     * ## 왜 트랜잭션이어야 하는가
     *
     * Firestore의 오프라인 동기화는 같은 문서에 대해 last-write-wins다. 그
     * 경로로 정본을 쓰면, 오프라인으로 논 오래된 기기가 나중에 연결되는 순간
     * 최신 진행을 조용히 덮는다 - 그 사고는 로그도 안 남기고 되돌릴 수도 없다.
     *
     * 트랜잭션은 **오프라인에서 실패한다.** 그것이 이 설계에서는 기능이다:
     * 실패하면 로컬 dirty로 남고, 게임은 그대로 돌고, 온라인이 되면 같은
     * mutation id로 다시 시도한다(CloudScores의 리더보드 큐와 정반대 정책이고,
     * 정반대여야 하는 이유가 이것이다).
     *
     * ## 쓰기 한 번의 순서
     *
     *     세션 확인 -> 정본 읽기 -> 같은 mutation? -> revision 일치? ->
     *     기존 정본을 백업으로 복사 -> 새 정본 기록(revision + 1)
     *
     * 여섯 단계가 **한 트랜잭션 안**에 있다. 백업 복사가 밖에 있으면 그 사이에
     * 앱이 죽었을 때 백업 없는 덮어쓰기가 성립하고, 보안 규칙도 같은 이유로
     * 둘을 한 요청에서만 허용한다(firestore.rules `getAfter`).
     *
     * ## 서버로 나가기 전에 로컬이 먼저다
     *
     * pending을 디스크에 남기는 데 실패하면 **트랜잭션을 시작하지 않는다**
     * (CloudSavePolicy.MayStartServerWrite). 서버는 커밋했는데 그 mutation id가
     * 로컬 어디에도 없으면, 다음 실행이 같은 쓰기를 다시 올려 백업 한 벌을
     * 밀어낸다 - 57단계가 못 박은 계약이다.
     */
    public static class CloudSaveStore
    {
        private const string Tag = "[CloudSave]";

        /**
         * @brief 서버 정본을 읽는다. **캐시가 아니라 서버에서.**
         *
         * `Source.Server`가 아니면 오프라인에서 캐시가 조용히 돌아오고, 그러면
         * "서버를 확인했다"가 거짓이 된다 - 그 거짓 위에서 판정하면 오래된
         * 캐시를 기준으로 업로드가 나간다.
         */
        public static async Task<CloudSaveFetchResult> FetchAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return Fetch(CloudSaveStoreStatus.Invalid);

            if (!await FirebaseRuntime.EnsureReadyAsync())
                return Fetch(CloudSaveStoreStatus.Offline);

            DocumentReference reference = FirebaseRuntime.Db
                .Collection(CloudSaveEnvelope.Collection).Document(uid);

            Task<DocumentSnapshot> task = reference.GetSnapshotAsync(Source.Server);

            if (!await FirebaseRuntime.Completes(task, FirebaseRuntime.RequestTimeoutMs, "정본 읽기"))
                return Fetch(Classify(task));

            DocumentSnapshot snapshot = task.Result;
            if (!snapshot.Exists) return Fetch(CloudSaveStoreStatus.Missing);

            CloudSaveEnvelope envelope = EnvelopeFrom(snapshot);
            if (envelope == null)
                return Fetch(CloudSaveStoreStatus.Invalid, null, CloudSaveEnvelopeFault.NoEnvelope);

            // **검사를 통과하기 전에는 아무도 이 봉투를 쓰지 못한다.**
            // 규칙은 우리가 쓴 문서만 지킨다 - 콘솔에서 고친 문서, 옛 앱이 남긴
            // 문서, 전송 중 잘린 문서는 규칙을 지나온 적이 없다
            CloudSaveEnvelopeFault fault = envelope.Validate();
            if (fault != CloudSaveEnvelopeFault.None)
            {
                Debug.LogWarning(Tag + " 서버 정본을 쓸 수 없습니다: " + fault);
                return Fetch(CloudSaveStoreStatus.Invalid, null, fault);
            }

            return Fetch(CloudSaveStoreStatus.Found, envelope);
        }

        public static Task<CloudSaveCommitResult> CommitAsync(string uid, SaveData data,
                                                              CloudSaveLocalState sidecar)
        {
            return CommitAsync(uid, data, sidecar, CloudSaveSession.CurrentId);
        }

        /**
         * @brief 세이브 한 벌을 정본으로 올린다.
         *
         * 실패하면 **pending을 지우지 않는다.** 그 상태가 곧 "로컬은 있고 서버는
         * 아직"이고, 다음 시도가 같은 mutation id로 이어진다.
         */
        public static async Task<CloudSaveCommitResult> CommitAsync(string uid, SaveData data,
                                                                    CloudSaveLocalState sidecar,
                                                                    string sessionId)
        {
            if (string.IsNullOrEmpty(uid) || data == null || sidecar == null)
                return Commit(CloudSaveStoreStatus.Invalid);

            // 다른 계정의 사슬로 쓰지 않는다 (복구로 uid가 바뀐 직후)
            if (!CloudSavePolicy.SidecarAppliesTo(sidecar, uid))
                return Commit(CloudSaveStoreStatus.Invalid);

            if (!CloudSaveIds.IsValid(sessionId) || !CloudSaveIds.IsValid(sidecar.deviceId))
                return Commit(CloudSaveStoreStatus.Invalid);

            // 재시도는 **같은 id로** 간다. 새로 만들면 응답 유실 복구가 성립하지 않는다
            string mutationId = sidecar.HasPending ? sidecar.pendingMutationId : CloudSaveIds.New();

            CloudSaveEnvelope envelope = CloudSaveEnvelope.ForUpload(
                data, sidecar.baseRevision, sessionId, sidecar.deviceId, mutationId);

            if (envelope == null)
                return Commit(CloudSaveStoreStatus.Invalid, 0L, CloudSaveBlock.CorruptPayload);

            // ---- 로컬이 먼저다. 여기서 실패하면 서버로 나가지 않는다
            if (!sidecar.MarkPending(mutationId, envelope.payloadSha256))
                return Commit(CloudSaveStoreStatus.Invalid);

            bool persisted = CloudSaveSidecar.Save(sidecar);
            if (!CloudSavePolicy.MayStartServerWrite(sidecar, persisted))
            {
                Debug.LogWarning(Tag + " pending을 디스크에 남기지 못해 서버 쓰기를 멈춥니다.");
                return Commit(CloudSaveStoreStatus.Failed);
            }

            if (!await FirebaseRuntime.EnsureReadyAsync())
                return Commit(CloudSaveStoreStatus.Offline);

            FirebaseFirestore db = FirebaseRuntime.Db;
            DocumentReference canonical = db.Collection(CloudSaveEnvelope.Collection).Document(uid);
            DocumentReference backup = db.Collection(CloudSaveEnvelope.BackupCollection).Document(uid);
            DocumentReference session = db.Collection(CloudSaveEnvelope.SessionCollection).Document(uid);

            long baseRevision = sidecar.baseRevision;

            Task<CloudSaveCommitResult> task = db.RunTransactionAsync(async transaction =>
            {
                // [1] 작성권. 남의 세션이 살아 있으면 여기서 끝난다
                DocumentSnapshot sessionSnapshot = await transaction.GetSnapshotAsync(session);
                if (!HoldsWrite(sessionSnapshot, sessionId)) return Commit(CloudSaveStoreStatus.Busy);

                // [2] 서버 정본
                DocumentSnapshot canonicalSnapshot = await transaction.GetSnapshotAsync(canonical);

                if (!canonicalSnapshot.Exists)
                {
                    // [3] **동기화 이력이 있는데 문서가 없다.** 새로 만들지 않는다 -
                    // 사슬이 1로 리셋되면 다른 기기의 base가 갈 곳을 잃는다
                    if (!CloudSavePolicy.CanCreateFirstRevision(true, baseRevision))
                        return Commit(CloudSaveStoreStatus.Invalid, 0L,
                                      CloudSaveBlock.MissingServerAfterSync);

                    transaction.Set(canonical, Fields(envelope));
                    return Commit(CloudSaveStoreStatus.Committed, envelope.revision);
                }

                Dictionary<string, object> serverFields = canonicalSnapshot.ToDictionary();

                // [4] 응답이 유실된 커밋의 재시도. revision을 더 올리지 않는다
                string serverMutation = Read(serverFields, CloudSaveDocument.FieldMutationId) as string;
                if (CloudSavePolicy.WasCommitAlreadyApplied(mutationId, serverMutation))
                    return Commit(CloudSaveStoreStatus.AlreadyApplied,
                                  AsLong(Read(serverFields, CloudSaveDocument.FieldRevision)));

                // [5] 낙관적 잠금. 서버가 우리가 두고 온 자리에 없으면 사람이 고른다
                long serverRevision = AsLong(Read(serverFields, CloudSaveDocument.FieldRevision));
                if (serverRevision != baseRevision)
                    return Commit(CloudSaveStoreStatus.Conflict, serverRevision);

                // [6] 교체되는 정본을 **그대로** 백업으로. 규칙이 같은 요청 안에서
                //     이 복사를 확인한다(getAfter) - 백업 없는 덮어쓰기는 거부된다
                transaction.Set(backup, serverFields);

                // [7] 새 정본
                transaction.Set(canonical, Fields(envelope));
                return Commit(CloudSaveStoreStatus.Committed, envelope.revision);
            });

            if (!await FirebaseRuntime.Completes(task, FirebaseRuntime.RequestTimeoutMs, "정본 쓰기"))
                return Commit(Classify(task));

            CloudSaveCommitResult result = task.Result;

            // [9] **성공을 확인한 뒤에만** sidecar를 앞으로 민다
            if (result.IsSynced) AdoptRevision(sidecar, envelope, result.revision);

            // [10] 그 밖의 갈래는 pending을 그대로 둔다 - 로컬 dirty가 정답이다
            return result;
        }

        /**
         * @brief 커밋이 확인된 뒤 sidecar를 서버 상태로 맞춘다.
         *
         * 서버 시각(updatedAt)은 0으로 둔다. 그 값을 알려면 방금 쓴 문서를 다시
         * 읽어야 하는데, 그것은 쓰기마다 읽기 하나를 더 붙이는 일이고 쓰이는 곳은
         * 충돌 화면의 한 줄뿐이다 - 다음 Fetch가 채운다.
         *
         * 여기서 디스크 저장이 실패해도 진행은 잃지 않는다. 다음 실행은 옛
         * pending을 들고 재시도하고, 서버의 lastMutationId가 같아 AlreadyApplied로
         * 복구된다 - 그 갈래가 존재하는 이유가 정확히 이것이다.
         */
        private static void AdoptRevision(CloudSaveLocalState sidecar, CloudSaveEnvelope envelope,
                                          long revision)
        {
            if (!sidecar.MarkSynced(revision, envelope.payloadSha256, envelope.stateSha256, 0L))
            {
                Debug.LogWarning(Tag + " 커밋은 성공했지만 sidecar를 갱신하지 못했습니다.");
                return;
            }

            if (!CloudSaveSidecar.Save(sidecar))
                Debug.LogWarning(Tag + " 커밋은 성공했지만 sidecar를 저장하지 못했습니다 "
                                 + "(다음 실행이 AlreadyApplied로 복구합니다).");
        }

        /** 봉투 + 서버 타임스탬프. `updatedAt`은 여기서만 붙는다 */
        private static Dictionary<string, object> Fields(CloudSaveEnvelope envelope)
        {
            Dictionary<string, object> fields = CloudSaveDocument.ToFields(envelope);
            fields[CloudSaveDocument.FieldUpdatedAt] = FieldValue.ServerTimestamp;
            return fields;
        }

        private static bool HoldsWrite(DocumentSnapshot snapshot, string sessionId)
        {
            if (snapshot == null || !snapshot.Exists) return false;

            Dictionary<string, object> data = snapshot.ToDictionary();

            string owner = Read(data, CloudSaveSession.FieldSessionId) as string;
            object released = Read(data, CloudSaveSession.FieldReleased);

            if (released is bool && (bool)released) return false;
            return owner == sessionId && CloudSaveIds.IsValid(sessionId);
        }

        private static CloudSaveEnvelope EnvelopeFrom(DocumentSnapshot snapshot)
        {
            Dictionary<string, object> fields = snapshot.ToDictionary();
            long ticks = FirebaseRuntime.TicksOf(Read(fields, CloudSaveDocument.FieldUpdatedAt));

            return CloudSaveDocument.FromFields(fields, ticks);
        }

        /** 오프라인과 그 밖의 실패를 가른다 - 사람에게 할 말이 다르다 */
        private static CloudSaveStoreStatus Classify(Task task)
        {
            if (task == null) return CloudSaveStoreStatus.Failed;

            if (!task.IsFaulted) return CloudSaveStoreStatus.Offline;   // 시간 초과
            if (FirebaseRuntime.LooksOffline(task.Exception)) return CloudSaveStoreStatus.Offline;

            Debug.LogWarning(Tag + " 서버 왕복 실패: " + FirebaseRuntime.Flatten(task.Exception));
            return CloudSaveStoreStatus.Failed;
        }

        private static object Read(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) ? value : null;
        }

        private static long AsLong(object value)
        {
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            return 0L;
        }

        private static CloudSaveFetchResult Fetch(CloudSaveStoreStatus status,
                                                  CloudSaveEnvelope envelope = null,
                                                  CloudSaveEnvelopeFault fault = CloudSaveEnvelopeFault.None)
        {
            return new CloudSaveFetchResult { status = status, envelope = envelope, fault = fault };
        }

        private static CloudSaveCommitResult Commit(CloudSaveStoreStatus status, long revision = 0L,
                                                    CloudSaveBlock block = CloudSaveBlock.None)
        {
            return new CloudSaveCommitResult { status = status, revision = revision, block = block };
        }
    }
}
