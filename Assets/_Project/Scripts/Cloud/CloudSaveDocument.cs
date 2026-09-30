using System.Collections.Generic;

namespace Onikiri.Cloud
{
    /**
     * @brief 봉투와 Firestore 문서(필드 맵) 사이의 변환. **Firebase 타입이 없다.**
     *
     * 갈라 둔 이유가 둘이다.
     *
     * 하나는 검사 가능성이다. `CloudSaveStore`는 살아 있는 Firestore가 있어야
     * 한 줄도 못 돌지만, "무엇을 어떤 이름으로 적는가"는 그것과 무관하게 참이다.
     * 여기 있어야 EditMode가 필드 이름·타입·누락을 Firestore 없이 잰다 -
     * 53~55단계에서 정책을 갈라낸 것과 같은 이유이고, 그 규칙이 여기서도 그대로다.
     *
     * 다른 하나는 **규칙과의 대조**다. 보안 규칙이 `hasOnly`로 허용 목록을 들고
     * 있는데(firestore.rules), 그 목록과 여기 `Fields`가 갈리면 클라가 만든
     * 문서를 서버가 거부한다 - 그 증상은 "저장이 조용히 안 된다"이고 원인은
     * 필드 이름 하나다. 두 곳에 적힌 목록이 같은지는 사람이 아니라 테스트가 본다.
     *
     * `updatedAt`만 여기서 만들지 않는다. **서버 타임스탬프여야 하기 때문이다** -
     * 그 값은 Firestore가 커밋 시각으로 채우고, 규칙이 `request.time`과 대조한다.
     * 클라가 만든 시각을 넣는 순간 "언제 저장됐는가"가 조작 가능해진다.
     */
    public static class CloudSaveDocument
    {
        public const string FieldFormatVersion = "formatVersion";
        public const string FieldSaveVersion = "saveVersion";
        public const string FieldRevision = "revision";
        public const string FieldBaseRevision = "baseRevision";
        public const string FieldPayload = "payload";
        public const string FieldPayloadSha = "payloadSha256";
        public const string FieldStateSha = "stateSha256";
        public const string FieldMutationId = "lastMutationId";
        public const string FieldSessionId = "sessionId";
        public const string FieldDeviceId = "deviceId";
        public const string FieldUpdatedAt = "updatedAt";
        public const string FieldSummary = "summary";

        public const string SummaryMaxStage = "maxStageReached";
        public const string SummaryCharacterLevel = "characterLevel";
        public const string SummaryEvolutionTier = "evolutionTier";
        public const string SummaryGems = "gems";
        public const string SummaryGachaPulls = "gachaTotalPulls";
        public const string SummarySkillGachaPulls = "skillGachaTotalPulls";

        /** 정본·백업 문서에 존재해야 하는 필드 전부. 규칙의 hasOnly/hasAll과 같은 목록 */
        public static readonly string[] Fields =
        {
            FieldFormatVersion, FieldSaveVersion, FieldRevision, FieldBaseRevision,
            FieldPayload, FieldPayloadSha, FieldStateSha,
            FieldMutationId, FieldSessionId, FieldDeviceId, FieldUpdatedAt, FieldSummary
        };

        public static readonly string[] SummaryFields =
        {
            SummaryMaxStage, SummaryCharacterLevel, SummaryEvolutionTier,
            SummaryGems, SummaryGachaPulls, SummarySkillGachaPulls
        };

        // ---------------------------------------------------------------- 쓰기

        /**
         * @brief 봉투를 필드 맵으로. **`updatedAt`은 빠져 있다.**
         *
         * 부르는 쪽(CloudSaveStore)이 `FieldValue.ServerTimestamp`를 그 자리에
         * 넣는다. 여기서 넣을 수 없는 것이 아니라 **넣으면 안 되는** 값이다.
         *
         * @return 만들 수 없으면 null (봉투 없음)
         */
        public static Dictionary<string, object> ToFields(CloudSaveEnvelope envelope)
        {
            if (envelope == null) return null;
            if (envelope.summary == null) return null;

            return new Dictionary<string, object>
            {
                { FieldFormatVersion, envelope.formatVersion },
                { FieldSaveVersion, envelope.saveVersion },
                { FieldRevision, envelope.revision },
                { FieldBaseRevision, envelope.baseRevision },
                { FieldPayload, envelope.payload },
                { FieldPayloadSha, envelope.payloadSha256 },
                { FieldStateSha, envelope.stateSha256 },
                { FieldMutationId, envelope.lastMutationId },
                { FieldSessionId, envelope.sessionId },
                { FieldDeviceId, envelope.deviceId },
                { FieldSummary, ToFields(envelope.summary) }
            };
        }

        public static Dictionary<string, object> ToFields(CloudSaveSummary summary)
        {
            if (summary == null) return null;

            return new Dictionary<string, object>
            {
                { SummaryMaxStage, summary.maxStageReached },
                { SummaryCharacterLevel, summary.characterLevel },
                { SummaryEvolutionTier, summary.evolutionTier },
                { SummaryGems, summary.gems },
                { SummaryGachaPulls, summary.gachaTotalPulls },
                { SummarySkillGachaPulls, summary.skillGachaTotalPulls }
            };
        }

        // ---------------------------------------------------------------- 읽기

        /**
         * @brief 서버 문서를 봉투로.
         *
         * `updatedAtUtcTicks`를 따로 받는 이유는 그 값이 Firestore `Timestamp`라
         * 이 파일이 다룰 수 없기 때문이다 - 부르는 쪽이 변환해서 넘긴다.
         *
         * 여기서 하는 것은 **모양 맞추기까지**다. 이 결과가 쓸 수 있는 것인지는
         * `CloudSaveEnvelope.Validate()`가 답하고, 그 검사를 지나기 전에는
         * 누구도 이 봉투를 적용하지 않는다.
         *
         * @return 필드가 모자라거나 타입이 다르면 null
         */
        public static CloudSaveEnvelope FromFields(IDictionary<string, object> fields,
                                                   long updatedAtUtcTicks)
        {
            if (fields == null) return null;

            for (int i = 0; i < Fields.Length; i++)
            {
                // updatedAt은 밖에서 받는다. 나머지는 전부 문서 안에 있어야 한다
                if (Fields[i] == FieldUpdatedAt) continue;
                if (!fields.ContainsKey(Fields[i])) return null;
            }

            var summary = SummaryFromFields(fields[FieldSummary] as IDictionary<string, object>);
            if (summary == null) return null;

            return new CloudSaveEnvelope
            {
                formatVersion = AsInt(fields[FieldFormatVersion]),
                saveVersion = AsInt(fields[FieldSaveVersion]),
                revision = AsLong(fields[FieldRevision]),
                baseRevision = AsLong(fields[FieldBaseRevision]),
                payload = AsString(fields[FieldPayload]),
                payloadSha256 = AsString(fields[FieldPayloadSha]),
                stateSha256 = AsString(fields[FieldStateSha]),
                lastMutationId = AsString(fields[FieldMutationId]),
                sessionId = AsString(fields[FieldSessionId]),
                deviceId = AsString(fields[FieldDeviceId]),
                updatedAtUtcTicks = updatedAtUtcTicks,
                summary = summary
            };
        }

        public static CloudSaveSummary SummaryFromFields(IDictionary<string, object> fields)
        {
            if (fields == null) return null;

            for (int i = 0; i < SummaryFields.Length; i++)
                if (!fields.ContainsKey(SummaryFields[i])) return null;

            return new CloudSaveSummary
            {
                maxStageReached = AsInt(fields[SummaryMaxStage]),
                characterLevel = AsInt(fields[SummaryCharacterLevel]),
                evolutionTier = AsInt(fields[SummaryEvolutionTier]),
                gems = AsLong(fields[SummaryGems]),
                gachaTotalPulls = AsInt(fields[SummaryGachaPulls]),
                skillGachaTotalPulls = AsInt(fields[SummarySkillGachaPulls])
            };
        }

        /**
         * @brief Firestore의 정수는 전부 int64로 온다. int 칸에 넣으려면 좁혀야 한다.
         *
         * 범위를 넘으면 0을 돌려준다 - 그 값은 `Validate()`의 범위 검사에 걸려
         * 봉투 전체가 거부된다. 여기서 예외를 던지지 않는 이유는 손상된 문서
         * 하나가 부팅을 멈추면 안 되기 때문이다(SaveSystem과 같은 규칙).
         */
        private static int AsInt(object value)
        {
            if (value is int) return (int)value;

            if (value is long)
            {
                long wide = (long)value;
                if (wide < int.MinValue || wide > int.MaxValue) return 0;
                return (int)wide;
            }

            return 0;
        }

        private static long AsLong(object value)
        {
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            return 0L;
        }

        private static string AsString(object value)
        {
            return value as string ?? string.Empty;
        }
    }
}
