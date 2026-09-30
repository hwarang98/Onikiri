using System;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 이 기기가 클라우드에 대해 아는 것. **세이브 옆에 따로 사는 파일이다.**
     *
     * ## 왜 SaveData에 넣지 않는가
     *
     * 동기화는 게임 상태가 아니다. `baseRevision`을 세이브 안에 적으면 그 순간
     * `SaveData.CurrentVersion`을 22로 올려야 하고, 그러면 **크로스 저장을 켜는
     * 일이 모든 라이브 세이브의 형식 변경**이 된다 - 되돌릴 수 없는 변경이다
     * (v22 세이브를 v21 클라이언트가 읽으면 새 게임이 된다).
     *
     * 더 나쁜 것은 payload에 섞이는 것이다. 세이브가 통째로 payload가 되므로,
     * 동기화 메타가 그 안에 있으면 **자기 자신을 담은 해시**를 계산하게 된다 -
     * 올릴 때마다 지문이 달라져 "상태가 안 바뀌었다"를 영원히 말할 수 없다.
     *
     * ## 없어도 게임은 돈다. 그러나 **반만 있으면 안 된다**
     *
     * 이 파일이 사라지면 이 기기는 "서버에 대해 아무것도 모르는 상태"가 될
     * 뿐이다 - 로컬 세이브는 그대로 열리고, 서버와 갈리면 충돌 화면이 뜬다.
     * 모르는 상태에서 자동으로 한쪽을 고르지 않는 것이 정확히 맞는 동작이다.
     *
     * 위험한 것은 **반쯤 맞는 상태**다. "revision 41까지 동기화했다"고 적혀
     * 있는데 지문 자리가 비어 있으면, 다음 부팅은 그 41을 믿고 로컬이 안 변한
     * 것으로 읽어 다른 기기의 진행을 조용히 내려받거나 덮는다. 그래서
     * `IsWellFormed`를 통과하지 못한 sidecar는 **없는 것으로 친다.**
     */
    [Serializable]
    public sealed class CloudSaveLocalState
    {
        /** 이 sidecar 형식의 버전. 봉투(CloudSaveEnvelope)와도, 세이브와도 다른 축이다 */
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;

        /**
         * @brief 이 상태가 누구 것인가.
         *
         * 계정 복구로 uid가 바뀌면 여기 적힌 revision은 **다른 계정의 사슬**이다.
         * 그 값을 새 계정에 그대로 쓰면 낙관적 잠금이 엉뚱한 번호를 들고 출발한다.
         * uid가 다르면 sidecar가 없는 것으로 친다 (CloudSavePolicy.SidecarAppliesTo).
         *
         * 형식은 검사하지 않는다 - Firebase가 만드는 값이라 우리 규격이 아니다.
         */
        public string ownerUid = string.Empty;

        /** 이 기기가 마지막으로 확인한 서버 정본의 revision. 0 = 아직 없음 */
        public long baseRevision;

        public string lastSyncedPayloadSha256 = string.Empty;

        /** 마지막 동기화 시점의 **상태** 지문. 로컬이 그 뒤로 변했는지를 이것으로 잰다 */
        public string lastSyncedStateSha256 = string.Empty;

        /**
         * @brief 지금 서버로 보내는 중인 쓰기의 id. 성공하면 지운다.
         *
         * 비어 있지 않은 채로 앱이 다시 뜨면 셋 중 하나다 - 안 갔거나, 갔는데
         * 실패했거나, **갔는데 응답만 유실됐거나.** 서버의 lastMutationId가
         * 이 값과 같으면 세 번째이고, 그때 재시도하면 같은 커밋이 두 번 올라간다.
         *
         * 짝인 `pendingPayloadSha256`과 **둘 다 있거나 둘 다 없어야 한다.**
         * 한쪽만 남으면 "무엇을 보내는 중이었는가"를 말할 수 없는 상태가 된다.
         */
        public string pendingMutationId = string.Empty;

        public string pendingPayloadSha256 = string.Empty;

        /**
         * @brief 이 설치의 id. **정체성이 아니라 이름표다.**
         *
         * 충돌 화면의 "현재 기기"와 서버 문서의 deviceId를 대조해 사람이
         * 어느 쪽이 자기 앞의 기기인지 알아보게 하는 용도다. 권한은 이 값이
         * 아니라 uid가 정한다 - 여기 무엇이 적혀 있어도 남의 문서는 못 읽는다.
         */
        public string deviceId = string.Empty;

        /** 마지막으로 본 서버 갱신 시각. 충돌 화면의 한 줄용 - 판정에는 안 쓴다 */
        public long lastKnownServerUpdatedAtUtcTicks;

        public bool HasPending
        {
            get { return !string.IsNullOrEmpty(pendingMutationId); }
        }

        /**
         * @brief 새 sidecar. **id 형식이 맞을 때만 만들어진다.**
         *
         * @return 만들 수 없으면 null (uid 없음 · deviceId 형식)
         */
        public static CloudSaveLocalState NewFor(string uid, string deviceId)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            if (!CloudSaveIds.IsValid(deviceId)) return null;

            return new CloudSaveLocalState
            {
                formatVersion = CurrentFormatVersion,
                ownerUid = uid,
                deviceId = deviceId,
                baseRevision = 0L
            };
        }

        /**
         * @brief 이 sidecar가 스스로 일관된가.
         *
         * 통과하지 못하면 읽는 쪽이 **없는 것으로 친다**(CloudSaveSidecar.Load).
         * 반쯤 맞는 상태를 믿는 것보다 아무것도 모르는 편이 안전하다 - 후자의
         * 결과는 충돌 화면이고, 전자의 결과는 조용한 덮어쓰기다.
         */
        public bool IsWellFormed()
        {
            if (formatVersion != CurrentFormatVersion) return false;
            if (baseRevision < 0L) return false;

            if (string.IsNullOrEmpty(ownerUid)) return false;
            if (!CloudSaveIds.IsValid(deviceId)) return false;

            if (lastKnownServerUpdatedAtUtcTicks < 0L) return false;

            // 동기화한 적이 있다면 **그때의 지문 둘이 있어야 한다.** 없으면
            // 다음 부팅이 "로컬은 안 변했다"를 근거 없이 말하게 된다
            if (baseRevision > 0L)
            {
                if (!CloudSaveFingerprint.IsHash(lastSyncedPayloadSha256)) return false;
                if (!CloudSaveFingerprint.IsHash(lastSyncedStateSha256)) return false;
            }
            else
            {
                if (!IsEmptyOrHash(lastSyncedPayloadSha256)) return false;
                if (!IsEmptyOrHash(lastSyncedStateSha256)) return false;
            }

            bool hasId = !string.IsNullOrEmpty(pendingMutationId);
            bool hasSha = !string.IsNullOrEmpty(pendingPayloadSha256);
            if (hasId != hasSha) return false;

            if (hasId)
            {
                if (!CloudSaveIds.IsValid(pendingMutationId)) return false;
                if (!CloudSaveFingerprint.IsHash(pendingPayloadSha256)) return false;
            }

            return true;
        }

        /**
         * @brief 서버로 보내기 **직전에** 적는다. 순서가 뒤집히면 복구 근거가 사라진다.
         *
         * 잘못된 값은 받지 않는다. 형식이 아닌 mutation id를 적어 두면 응답
         * 유실 복구가 서버의 id와 영원히 안 맞고, 그 상태는 "복구 장치가 있는데
         * 작동하지 않는" 가장 나쁜 모양이다.
         *
         * @return 적었으면 true
         */
        public bool MarkPending(string mutationId, string payloadSha)
        {
            if (!CloudSaveIds.IsValid(mutationId))
            {
                Debug.LogWarning("[Onikiri] Refused a malformed cloud mutation id.");
                return false;
            }

            if (!CloudSaveFingerprint.IsHash(payloadSha))
            {
                Debug.LogWarning("[Onikiri] Refused a malformed pending payload hash.");
                return false;
            }

            pendingMutationId = mutationId;
            pendingPayloadSha256 = payloadSha;
            return true;
        }

        /**
         * @brief 트랜잭션 성공을 **확인한 뒤에만** 부른다.
         *
         * revision은 1부터다 - 0을 받아들이면 "동기화했는데 아직 정본이 없다"는
         * 있을 수 없는 상태가 기록되고, 그 sidecar는 다음 부팅에서 신규 업로드를
         * 막지도(MissingServerAfterSync) 이어가지도 못한다.
         *
         * @return 적었으면 true
         */
        public bool MarkSynced(long revision, string payloadSha, string stateSha,
                               long serverUpdatedAtUtcTicks)
        {
            if (revision < CloudSaveEnvelope.FirstRevision) return false;
            if (!CloudSaveFingerprint.IsHash(payloadSha)) return false;
            if (!CloudSaveFingerprint.IsHash(stateSha)) return false;
            if (serverUpdatedAtUtcTicks < 0L) return false;

            baseRevision = revision;
            lastSyncedPayloadSha256 = payloadSha;
            lastSyncedStateSha256 = stateSha;
            lastKnownServerUpdatedAtUtcTicks = serverUpdatedAtUtcTicks;
            ClearPending();
            return true;
        }

        public void ClearPending()
        {
            pendingMutationId = string.Empty;
            pendingPayloadSha256 = string.Empty;
        }

        private static bool IsEmptyOrHash(string value)
        {
            return string.IsNullOrEmpty(value) || CloudSaveFingerprint.IsHash(value);
        }
    }
}
