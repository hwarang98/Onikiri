using System;
using Onikiri.Progression;

namespace Onikiri.Cloud
{
    /**
     * @brief 내려받은 봉투가 **왜** 못 쓰는가. 하나씩 이름이 있다.
     *
     * bool 하나로 두지 않는 이유는 화면과 로그가 갈리기 때문이다 - 미래 버전은
     * "앱을 업데이트하세요"이고 해시 불일치는 "클라우드 백업에서 복구하세요"이며,
     * revision 사슬이 끊긴 것은 둘 다 아니고 **서버 메타가 손상된 것**이다.
     * 실기에서 어느 자리에서 멈췄는지가 logcat 한 줄로 읽혀야 한다.
     */
    public enum CloudSaveEnvelopeFault
    {
        None = 0,

        /** 문서 자체가 없다 */
        NoEnvelope,

        /** 봉투 형식이 이 앱이 아는 것과 다르다 (높든 낮든) */
        FormatVersionMismatch,

        /** revision이 1보다 작다. 정본은 1부터다 */
        RevisionOutOfRange,

        /** baseRevision != revision - 1. 사슬이 끊겼다 */
        RevisionChainBroken,

        /** lastMutationId가 id 규격이 아니다. 응답 유실 복구가 성립하지 않는다 */
        MutationIdMalformed,

        /** sessionId가 id 규격이 아니다. 세션 일치 규칙이 무의미해진다 */
        SessionIdMalformed,

        /** deviceId가 id 규격이 아니다 */
        DeviceIdMalformed,

        /** payload가 비었다 */
        PayloadEmpty,

        /** payload가 200KB를 넘는다 */
        PayloadTooLarge,

        /** payloadSha256이 실제 바이트와 다르다 */
        PayloadHashMismatch,

        /** JSON이 안 읽힌다 */
        PayloadUnreadable,

        /** 읽히기만 하는 껍데기다 ("{}" 같은 것) */
        EmptySave,

        /** saveVersion이 1..CurrentVersion 밖이다 */
        SaveVersionOutOfRange,

        /** 봉투가 말한 버전과 payload 안 버전이 다르다 */
        SaveVersionMismatch,

        /** stateSha256이 payload에서 다시 계산한 값과 다르다 */
        StateHashMismatch,

        /** 요약 여섯 값이 payload와 다르다 */
        SummaryMismatch
    }

    /**
     * @brief 충돌 화면이 두 기록을 나란히 세우기 위한 **최소 요약**.
     *
     * payload를 열지 않고도 "어느 쪽이 무엇인가"를 그릴 수 있어야 하기 때문에
     * 따로 적는다. 세이브 전체를 파싱해서 그려도 되지만, 그러면 손상된 payload를
     * 가진 문서는 **충돌 화면조차 그릴 수 없다** - 그때가 사람에게 선택지를
     * 보여줘야 하는 바로 그 순간이다.
     *
     * **복원 계산에는 쓰지 않는다.** 여기 적힌 여섯 값은 화면용 사본이고,
     * 실제로 적용되는 것은 언제나 payload 한 벌이다. 이 구분이 무너지면
     * (예: 요약의 gems를 읽어 지갑을 채우는 코드) 그 순간 필드별 병합이
     * 뒷문으로 들어온 것이 된다 - 이 설계가 금지한 바로 그것이다.
     *
     * 사본이므로 **원본과 대조할 수 있어야 한다**(Matches). 대조하지 않으면
     * 화면이 payload와 다른 숫자를 그릴 수 있고, 사람은 그 숫자를 보고 브랜치를
     * 고른다 - 고른 결과가 화면에서 본 것과 다르면 그것은 데이터 손실과 같다.
     */
    [Serializable]
    public sealed class CloudSaveSummary
    {
        public int maxStageReached;
        public int characterLevel;
        public int evolutionTier;
        public long gems;
        public int gachaTotalPulls;
        public int skillGachaTotalPulls;

        public static CloudSaveSummary Of(SaveData data)
        {
            if (data == null) return new CloudSaveSummary();

            return new CloudSaveSummary
            {
                maxStageReached = data.maxStageReached,
                characterLevel = data.characterLevel,
                evolutionTier = data.evolutionTier,
                gems = data.gems,
                gachaTotalPulls = data.gachaTotalPulls,
                skillGachaTotalPulls = data.skillGachaTotalPulls
            };
        }

        public bool Matches(SaveData data)
        {
            if (data == null) return false;

            return maxStageReached == data.maxStageReached
                   && characterLevel == data.characterLevel
                   && evolutionTier == data.evolutionTier
                   && gems == data.gems
                   && gachaTotalPulls == data.gachaTotalPulls
                   && skillGachaTotalPulls == data.skillGachaTotalPulls;
        }
    }

    /**
     * @brief 서버에 놓이는 세이브 한 벌의 **봉투**. Firebase 타입이 한 줄도 없다.
     *
     * LeaderboardPolicy·AccountLinkPolicy와 같은 이유로 갈라져 있다 - 여기 적힌
     * 것은 네트워크의 성질이 아니라 **이 게임이 클라우드에 무엇을 약속하는가**이고,
     * 그래서 EditMode가 Firestore 없이 검사할 수 있다. 2단계의 `CloudSaveStore`가
     * 이 객체를 Firestore 필드 맵으로 옮긴다.
     *
     * ## 왜 payload가 문자열 한 덩어리인가
     *
     * 세이브를 Firestore 필드로 펼치면 **필드별 병합이 가능해진다.** 가능해지면
     * 언젠가 누군가 그것을 한다 - `gems`만 max로 합치거나 보유 목록만 합집합으로
     * 두는 코드가 들어오고, 그 순간 지불 전 지갑과 지불 후 상품이 동시에 남는다.
     * 문자열 한 덩어리는 그 유혹을 **형식 차원에서** 없앤다. 세이브는 하나의
     * 경제 트랜잭션이고(GameSession.Save), 트랜잭션은 쪼개서 반만 적용할 수 없다.
     *
     * ## revision이 시각보다 강한 증거다
     *
     * `updatedAt`으로 최신을 가리지 않는다. 기기 시각은 사용자가 바꿀 수 있고
     * 서버 시각은 오프라인 구간을 설명하지 못한다. 어느 쪽이 어느 쪽에서
     * 이어졌는가는 `revision`/`baseRevision` 사슬만이 말한다.
     *
     * ## 검증은 **내려받은 쪽**이 한다
     *
     * 2단계의 보안 규칙이 같은 것들을 한 겹 더 검사하겠지만, 규칙은 우리가
     * 쓴 문서만 지킨다 - 콘솔에서 손으로 고친 문서, 옛 버전의 앱이 남긴 문서,
     * 전송 중 잘린 문서는 규칙을 지나온 적이 없다. `Validate()`를 통과하지 못한
     * 봉투는 **적용도 덮어쓰기도 하지 않는다.**
     */
    [Serializable]
    public sealed class CloudSaveEnvelope
    {
        /** 클라우드 봉투의 형식. payload 안의 SaveData 버전과 **다른 축이다** */
        public const int CurrentFormatVersion = 1;

        /** 정본·백업·세션. 랭킹(scores)과 갈라 두는 이유는 읽기 권한이 다르기 때문이다 */
        public const string Collection = "playerSaves";
        public const string BackupCollection = "playerSaveBackups";
        public const string SessionCollection = "playerSaveSessions";

        /** 첫 정본의 revision. 규칙이 create에서 정확히 이 값만 허용한다 */
        public const long FirstRevision = 1L;

        public int formatVersion = CurrentFormatVersion;

        /** payload 안 SaveData의 버전. 이 앱보다 높으면 **읽지도 쓰지도 않는다** */
        public int saveVersion;

        public long revision;

        /**
         * @brief 이 쓰기가 읽고 출발한 revision. 규칙이 `resource.revision`과 대조한다.
         *
         * revision 하나만으로도 낙관적 잠금은 성립하지만, 이 값을 함께 적으면
         * 사슬이 문서 안에 남는다 - 사고가 났을 때 "무엇에서 이어졌는가"를
         * 백업 문서 한 벌만 보고도 말할 수 있다. 그래서 **정확히 revision - 1**
         * 이어야 하고, 아니면 그 문서는 우리가 쓴 것이 아니다.
         */
        public long baseRevision;

        public string payload = string.Empty;
        public string payloadSha256 = string.Empty;

        /**
         * @brief 시간성 필드를 뺀 게임 상태의 지문.
         *
         * 자동 저장만으로 매번 충돌하는 것을 막는다. 30초마다 `lastQuitUtcTicks`가
         * 바뀌므로 payload 해시는 아무것도 안 해도 계속 달라지는데, 그것을 변경으로
         * 세면 **가만히 있어도 충돌이 뜬다.**
         *
         * 이 값도 payload에서 **다시 계산해 대조한다**. payload 해시만 맞추고
         * 상태 지문을 다른 값으로 적으면, 같은 기록이 영원히 충돌로 읽히거나
         * (더 나쁘게) 다른 기록이 InSync로 읽혀 한쪽이 조용히 버려진다.
         */
        public string stateSha256 = string.Empty;

        /**
         * @brief 이 쓰기의 고유 id. **응답이 유실된 커밋을 두 번 올리지 않기 위한 값이다.**
         *
         * 서버는 커밋했는데 응답 전에 앱이 죽으면, 다음 실행의 로컬은 "안 올라갔다"고
         * 믿는다. 그 상태에서 재시도하면 revision이 하나 더 오르고 백업이 한 칸
         * 밀린다. 서버의 이 값이 로컬 pending과 같으면 **이미 성공한 것**이다.
         */
        public string lastMutationId = string.Empty;

        /** 이 쓰기를 만든 실행 세션. 소프트 단일 작성 세션의 열쇠 */
        public string sessionId = string.Empty;

        /** 진단·충돌 화면용 설치 id. **인증 수단이 아니다** - 소유권은 uid가 정한다 */
        public string deviceId = string.Empty;

        /**
         * @brief 서버가 확정한 갱신 시각.
         *
         * **업로드는 이 값을 채우지 않는다.** 2단계의 store가 Firestore 서버
         * 타임스탬프로 바꿔 넣고, 규칙이 `request.time`과 대조한다. 여기 있는
         * 것은 읽어 온 값을 담아 두는 자리이고, 쓰이는 곳은 충돌 화면의 한 줄
         * ("서버 저장 8월 18일 06:12")뿐이다 - **판정에는 쓰지 않는다.**
         */
        public long updatedAtUtcTicks;

        public CloudSaveSummary summary = new CloudSaveSummary();

        /**
         * @brief 올릴 봉투를 만든다. 서버 왕복이 없는 순수 함수다.
         *
         * `baseRevision`이 0이면 첫 정본이고 revision은 1이 된다 - 규칙의
         * create 분기와 정확히 같은 값이다.
         *
         * **잘못된 봉투는 만들지 않는다.** 만들어 두면 어딘가에서 올라가려
         * 시도하고, 거부되는 쓰기를 반복하는 상태가 된다. 특히 id 셋은 형식까지
         * 본다 - 임의 문자열을 허용하면 `sessionId = "editor-session"` 같은 값이
         * 서버에 올라가 2단계의 세션 일치 규칙을 무의미하게 만든다.
         *
         * @return 만들 수 없으면 null (데이터 없음 · 200KB 초과 · id 형식 · revision 상한)
         */
        public static CloudSaveEnvelope ForUpload(SaveData data, long baseRevision,
                                                  string sessionId, string deviceId,
                                                  string mutationId)
        {
            if (data == null) return null;
            if (baseRevision < 0L) return null;

            // long.MaxValue에서 +1 하면 음수로 감긴다. 도달할 수 없는 값이지만
            // **도달할 수 없다는 이유로 검사를 빼면** 그 자리는 검사가 없는 자리로
            // 남고, 손상된 sidecar 하나가 그리로 들어온다
            if (baseRevision == long.MaxValue) return null;

            if (!CloudSaveIds.IsValid(sessionId)) return null;
            if (!CloudSaveIds.IsValid(deviceId)) return null;
            if (!CloudSaveIds.IsValid(mutationId)) return null;

            string payload = CloudSaveFingerprint.Serialize(data);
            if (payload == null) return null;

            // 상한을 넘는 봉투는 **만들지 않는다.** 만들어 두면 어딘가에서
            // 올라가려 시도하고, 규칙이 거부하는 쓰기를 반복하는 상태가 된다
            if (!CloudSaveFingerprint.IsWithinLimit(payload)) return null;

            var envelope = new CloudSaveEnvelope
            {
                formatVersion = CurrentFormatVersion,
                saveVersion = data.version,
                revision = baseRevision + 1L,
                baseRevision = baseRevision,
                payload = payload,
                payloadSha256 = CloudSaveFingerprint.HashOf(payload),
                stateSha256 = CloudSaveFingerprint.StateHashOf(data),
                lastMutationId = mutationId,
                sessionId = sessionId,
                deviceId = deviceId,
                updatedAtUtcTicks = 0L,
                summary = CloudSaveSummary.Of(data)
            };

            // ---- **만든 것을 우리가 먼저 검사한다.**
            //
            // 내려받는 쪽에만 검사가 있으면 잘못된 것을 만들어 놓고 서버가
            // 거부하기를 기다리는 코드가 남는다. 여기서 걸러야 하는 것은 id나
            // revision이 아니라 **세이브 자체가 성립하지 않는 경우**다 -
            // version 0, stage 0, 레벨 0 같은 값은 위 검사들을 다 지나서
            // payload가 된 뒤에야 드러난다(Validate의 EmptySave·SaveVersion 검사).
            //
            // 비용은 직렬화 몇 번이고, 업로드는 120초에 한 번이다. 잘못된
            // 정본을 서버에 올리는 값과 비교할 것이 못 된다.
            if (envelope.Validate() != CloudSaveEnvelopeFault.None) return null;

            return envelope;
        }

        /**
         * @brief 내려받은 봉투를 **적용 전에** 전부 검사한다.
         *
         * 순서가 곧 우선순위다. 껍데기(형식·사슬)부터 보고, 바이트가 온전한지,
         * 그 다음에야 안을 연다 - 못 읽는 payload에서 요약을 대조하려 하면
         * 진짜 이유가 뒤의 검사에 가려진다.
         *
         * 검사가 이만큼인 이유는 **규칙이 지키지 못하는 문서가 존재하기**
         * 때문이다: 콘솔에서 손으로 고친 문서, 옛 앱이 남긴 문서, 전송 중
         * 잘린 문서. 그 셋은 보안 규칙을 지나온 적이 없다.
         */
        public CloudSaveEnvelopeFault Validate()
        {
            if (formatVersion != CurrentFormatVersion) return CloudSaveEnvelopeFault.FormatVersionMismatch;

            if (revision < FirstRevision) return CloudSaveEnvelopeFault.RevisionOutOfRange;
            if (baseRevision != revision - 1L) return CloudSaveEnvelopeFault.RevisionChainBroken;

            // ---- id 셋. **우리가 쓴 문서라면 셋 다 우리 규격이다**
            //
            // 규격 밖의 값이 있다는 것은 이 문서를 우리 코드가 쓰지 않았다는
            // 뜻이거나(콘솔·다른 도구) 필드가 잘렸다는 뜻이다. 그대로 두면
            // 응답 유실 복구가 서버의 id와 영원히 안 맞고(mutation), 세션 일치
            // 규칙이 임의 문자열을 진짜 세션으로 받아들인다(session).
            if (!CloudSaveIds.IsValid(lastMutationId)) return CloudSaveEnvelopeFault.MutationIdMalformed;
            if (!CloudSaveIds.IsValid(sessionId)) return CloudSaveEnvelopeFault.SessionIdMalformed;
            if (!CloudSaveIds.IsValid(deviceId)) return CloudSaveEnvelopeFault.DeviceIdMalformed;

            int bytes = CloudSaveFingerprint.ByteCount(payload);
            if (bytes <= 0) return CloudSaveEnvelopeFault.PayloadEmpty;
            if (bytes > CloudSaveFingerprint.MaxPayloadBytes) return CloudSaveEnvelopeFault.PayloadTooLarge;

            if (!CloudSaveFingerprint.Matches(payload, payloadSha256))
                return CloudSaveEnvelopeFault.PayloadHashMismatch;

            var parsed = CloudSaveFingerprint.Deserialize(payload);
            if (parsed == null) return CloudSaveEnvelopeFault.PayloadUnreadable;

            // **읽히는 것과 세이브인 것은 다르다.** "{}"는 JsonUtility가 군말 없이
            // 객체 하나로 만들어 주는데, 그것을 적용하면 진행이 통째로 사라진다.
            // 뼈대(버전 키 + 1 이상이어야 하는 네 값)를 갖췄는지 본다
            if (!LooksLikeASave(payload, parsed)) return CloudSaveEnvelopeFault.EmptySave;

            if (saveVersion < 1 || saveVersion > SaveData.CurrentVersion)
                return CloudSaveEnvelopeFault.SaveVersionOutOfRange;

            // 봉투가 말한 버전과 안의 버전이 다르면 둘 중 하나는 거짓이다.
            // 어느 쪽이 거짓인지 알 방법이 없으므로 적용하지 않는다
            if (parsed.version != saveVersion) return CloudSaveEnvelopeFault.SaveVersionMismatch;

            if (!CloudSavePolicy.SameState(CloudSaveFingerprint.StateHashOf(parsed), stateSha256))
                return CloudSaveEnvelopeFault.StateHashMismatch;

            if (summary == null || !summary.Matches(parsed)) return CloudSaveEnvelopeFault.SummaryMismatch;

            return CloudSaveEnvelopeFault.None;
        }

        public bool IsUsable()
        {
            return Validate() == CloudSaveEnvelopeFault.None;
        }

        public int PayloadBytes()
        {
            return CloudSaveFingerprint.ByteCount(payload);
        }

        /**
         * @brief 세이브의 뼈대를 갖췄는가.
         *
         * 두 겹이다. **원문에 버전 키가 있는가**(JsonUtility가 없는 필드를
         * 기본값으로 채우므로, 파싱된 객체만 봐서는 "적혀 있지 않았다"를 알 수
         * 없다) 그리고 **1 이상이어야 하는 네 값이 1 이상인가.**
         *
         * 네 값은 새 게임에서도 1이다(SaveData.NewGame) - 즉 이 검사는 진행이
         * 있는지를 묻는 것이 아니라, 이 JSON이 세이브의 형태인지를 묻는다.
         */
        private static bool LooksLikeASave(string rawPayload, SaveData parsed)
        {
            if (rawPayload.IndexOf("\"version\"", StringComparison.Ordinal) < 0) return false;

            if (parsed.version < 1) return false;
            if (parsed.stage < 1) return false;
            if (parsed.characterLevel < 1) return false;
            if (parsed.maxStageReached < 1) return false;

            return true;
        }
    }
}
