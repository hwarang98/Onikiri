namespace Onikiri.Cloud
{
    /**
     * @brief 부팅에서 무엇을 할 것인가. **여섯 갈래가 전부다.**
     *
     * 일곱 번째로 "병합"을 만들고 싶어지는 자리가 반드시 오는데, 그것이
     * 이 설계가 금지한 하나다 - CloudSavePolicy 머리 주석 참고.
     */
    public enum CloudSaveDecision
    {
        /** 서버를 확인하지 못했다. 로컬로 논다 - **게임은 절대 안 멈춘다** */
        LocalOnly = 0,

        /** 같은 기록이다. 서버 revision만 sidecar에 채택한다 */
        InSync,

        /** 로컬이 정본이다. 온라인이면 올린다 */
        UploadLocal,

        /** 클라우드가 정본이다. 로컬 백업을 남기고 적용한다 */
        DownloadCloud,

        /** 두 곳에서 갈라졌다. **사람이 고른다** */
        Conflict,

        /** 어느 쪽도 건드리면 안 된다 */
        Blocked
    }

    /** Blocked의 이유. 화면 문구가 갈리므로 이유를 함께 낸다 */
    public enum CloudSaveBlock
    {
        None = 0,

        /** 봉투 형식이 이 앱보다 새롭다 */
        FutureFormatVersion,

        /** payload 안 SaveData가 이 앱보다 새롭다. **앱 업데이트 안내** */
        FutureSaveVersion,

        /** 해시 불일치 · JSON 손상 · 껍데기 세이브 · 요약 위조 */
        CorruptPayload,

        /** 200KB 상한 초과 */
        PayloadTooLarge,

        /** 서버 revision이 이 기기가 아는 것보다 낮다. 롤백/메타 손상 */
        ServerRollback,

        /**
         * @brief 봉투의 메타가 성립하지 않는다 (revision 0·음수·사슬 불일치·옛 형식).
         *
         * 손상(CorruptPayload)과 갈라 둔 이유는 **고치는 사람이 다르기** 때문이다.
         * payload 손상은 백업 복구의 문제이고, 이쪽은 서버 문서의 메타가 우리가
         * 쓴 모양이 아니라는 뜻이라 - 콘솔에서 손댔거나 다른 코드가 썼다.
         */
        InvalidEnvelope,

        /**
         * @brief 동기화 이력이 있는데 서버 문서가 없다.
         *
         * **신규 업로드로 덮지 않는다.** revision 41까지 올린 기기가 문서가
         * 사라진 것을 보고 revision 1로 새로 쓰면, 그 순간 사슬이 리셋되어
         * 다른 기기의 base 41이 영원히 맞을 곳을 잃는다. 문서가 없는 진짜 이유는
         * 대개 **다른 uid로 보고 있는 것**(로그인이 갈렸다)이거나 서버 사고다.
         */
        MissingServerAfterSync
    }

    /**
     * @brief 작성 세션을 가져올 수 있는가 (58단계).
     *
     * 세션은 **정합성의 방어선이 아니다.** 마지막 방어선은 언제나 revision
     * 트랜잭션이고, 이것은 "다른 기기에서 플레이 중"을 미리 알려 충돌 화면을
     * 덜 보게 하는 UX 장치다.
     */
    public enum CloudSaveSessionClaim
    {
        /** 빈 자리이거나 놓아준 자리이거나 만료된 자리다. 가져온다 */
        Claim = 0,

        /** 이미 내 세션이다. heartbeat만 갱신한다 */
        Renew,

        /** 다른 기기가 살아 있다 */
        Busy
    }

    /** 판정과 이유 한 쌍 */
    public struct CloudSaveVerdict
    {
        public CloudSaveDecision decision;
        public CloudSaveBlock block;

        public bool IsBlocked
        {
            get { return decision == CloudSaveDecision.Blocked; }
        }

        public override string ToString()
        {
            return block == CloudSaveBlock.None
                ? decision.ToString()
                : decision + " (" + block + ")";
        }
    }

    /**
     * @brief 판정에 쓰이는 **사실만** 모은 것. 시각도 이름도 여기 없다.
     *
     * 기기 시각·서버 시각이 빠져 있는 것이 이 구조의 요점이다. 어느 쪽이
     * 최신인지 시계로 정하면 시간대를 바꾼 기기 하나가 남의 진행을 덮을 수
     * 있다 - 사슬은 revision만이 말한다.
     */
    public struct CloudSaveFacts
    {
        /** 서버를 실제로 확인했는가. 오프라인·시간 초과면 false */
        public bool serverChecked;

        /** 적용 가능한 로컬 세이브가 있는가 (없음·손상 = false) */
        public bool hasLocal;

        public string localStateSha;

        public bool hasCloud;
        public int cloudFormatVersion;
        public int cloudSaveVersion;
        public long cloudRevision;

        /** 서버 봉투가 적어 둔 baseRevision. 정확히 `cloudRevision - 1`이어야 한다 */
        public long cloudBaseRevision;

        public string cloudStateSha;

        /** `CloudSaveEnvelope.Validate()`의 결과. None이 아니면 적용도 덮어쓰기도 없다 */
        public CloudSaveEnvelopeFault cloudEnvelopeFault;

        /** 이 기기가 서버에 대해 아는 것이 있는가. uid가 다르면 false다 */
        public bool hasSidecar;
        public long baseRevision;
        public string lastSyncedStateSha;
    }

    /**
     * @brief 크로스 저장의 **규칙**. Firebase도 파일 입출력도 한 줄 없다.
     *
     * LeaderboardPolicy·AccountLinkPolicy와 같은 이유로 갈라져 있다 - 여기
     * 적힌 것은 네트워크의 성질이 아니라 이 게임의 성질이고, 그래서 EditMode가
     * Firestore 없이 전부 검사할 수 있다.
     *
     * ## 이 파일에 **없는 것**이 이 설계의 알맹이다
     *
     * 필드별 병합 함수가 없다. 보석을 max로 합치거나 보유 목록을 합집합으로
     * 두는 함수는 **만들지 않는다.** 세이브 한 벌은 하나의 경제 트랜잭션이라
     * (GameSession.Save가 지불과 상품을 같은 스냅샷에 담는다) 반만 합치면
     * 지불 전 지갑과 지불 후 상품이 동시에 남는다:
     *
     *     기기 A  보석 1,000 -> 10연 -> 보석 775 + 신규 오의 + 천장 10
     *     기기 B  오래된 세이브 (보석 1,000 · 오의 없음 · 천장 0)
     *     max 병합 = 보석 1,000 + 신규 오의 + 천장 10   <- 뽑기가 공짜가 된다
     *
     * maxStageReached·evolutionTier처럼 단조로 보이는 값도 예외가 아니다.
     * 그 진행은 지나오며 받은 보상·쓴 재화와 묶여 있어서, 브랜치 밖에서
     * 합치는 순간 같은 복제가 생긴다.
     *
     * 그것을 실제로 막는 것은 이름 규칙이 아니라 **여기 있는 판정과 payload의
     * 형식**이다: 갈라지면 Conflict가 나오고, 서버에 놓이는 것은 필드가 아니라
     * 문자열 한 덩어리라 반만 가져올 방법이 없다.
     *
     * 랭킹의 도달층 max 병합(AccountLinkPolicy.MergedStage)은 그대로 두는데,
     * 그것은 세이브가 아니라 순위표의 기록이라 값이 하나뿐이고 지불과 묶여 있지
     * 않기 때문이다.
     */
    public static class CloudSavePolicy
    {
        /**
         * @brief 클라우드 저장 디바운스 (초).
         *
         * 로컬 저장(30초)마다 Firestore 쓰기를 내지 않는다. 방치형은 몇 시간씩
         * 켜져 있으므로 그러면 한 세션에 수백 번의 쓰기가 나가는데, 서버가
         * 붙잡아야 하는 것은 매 순간의 상태가 아니라 **기기를 바꿔도 이어지는
         * 지점**이다. 120초면 앱을 닫고 다른 기기를 켜는 사람의 손보다 빠르다.
         */
        public const float DebounceSeconds = 120f;

        /**
         * @brief 작성 세션이 죽었다고 보는 시간 (초). 마지막 heartbeat 기준.
         *
         * 디바운스(120초)보다 길어야 한다 - 짧으면 정상적으로 놀고 있는 기기가
         * 저장과 저장 사이에 스스로 만료된다. 180초는 그 여유를 한 번 더 준 값이다.
         *
         * 세션은 **정합성의 방어선이 아니다.** 오프라인 기기의 세션은 어차피
         * 만료되고, 그 상태에서 두 기기가 갈라지는 것을 막을 방법은 없다.
         * 마지막 방어선은 언제나 revision 트랜잭션이고, 세션은 "다른 기기에서
         * 플레이 중"을 **미리** 알려 충돌 화면을 덜 보게 하는 UX 장치다.
         */
        public const float SessionExpirySeconds = 180f;

        /**
         * @brief 부팅에서 서버를 기다리는 한계 (초).
         *
         * Firebase는 무한 로딩 게이트가 아니다. 이 시간을 넘기면 LocalOnly로
         * 게임에 들어가고, 서버는 나중에 돌아온다. 값 자체는 실기에서 조정할
         * 자리이고(3단계), 중요한 것은 **0이 아니고 유한하다**는 것뿐이다.
         */
        public const float BootServerCheckSeconds = 6f;

        /** 클라우드를 적용하기 전 남기는 로컬 백업의 개수 */
        public const int LocalBackupCount = 3;

        /**
         * @brief 부팅 판정. **적용 전에 딱 한 번 부른다.**
         *
         * 순서가 곧 우선순위다. 막을 이유가 있으면 다른 무엇보다 먼저 막는다 -
         * 미래 버전 세이브를 "로컬이 변했으니 업로드"로 덮으면, 다른 기기의
         * 최신 진행이 옛 앱에 의해 지워진다.
         */
        public static CloudSaveVerdict Decide(CloudSaveFacts facts)
        {
            // 서버를 못 봤다. 로컬로 논다 - 오프라인은 실패가 아니라 정상 경로다
            if (!facts.serverChecked) return Verdict(CloudSaveDecision.LocalOnly);

            if (!facts.hasCloud)
            {
                // **동기화한 적이 있는데 문서가 없다.** 신규 업로드로 덮으면
                // 사슬이 1로 리셋되어 다른 기기의 base가 맞을 곳을 잃는다
                if (!CanCreateFirstRevision(facts.hasSidecar, facts.baseRevision))
                    return Blocked(CloudSaveBlock.MissingServerAfterSync);

                // 서버에 아무것도 없다. 로컬이 첫 정본이 된다 (revision 1)
                return Verdict(facts.hasLocal
                    ? CloudSaveDecision.UploadLocal
                    : CloudSaveDecision.LocalOnly);
            }

            // ---- 미래 버전이 **언제나 먼저다.** 이 앱이 모르는 것을 덮지 않는다

            if (facts.cloudFormatVersion > CloudSaveEnvelope.CurrentFormatVersion)
                return Blocked(CloudSaveBlock.FutureFormatVersion);

            // 미래 세이브. 적용하면 이 앱이 모르는 필드가 잘리고, 올리면 그
            // 잘린 것이 서버의 정본이 된다. **양쪽 다 금지다**
            if (facts.cloudSaveVersion > Progression.SaveData.CurrentVersion)
                return Blocked(CloudSaveBlock.FutureSaveVersion);

            // ---- 봉투의 메타가 성립하는가

            if (facts.cloudRevision < CloudSaveEnvelope.FirstRevision)
                return Blocked(CloudSaveBlock.InvalidEnvelope);

            if (facts.cloudBaseRevision != facts.cloudRevision - 1L)
                return Blocked(CloudSaveBlock.InvalidEnvelope);

            if (facts.cloudEnvelopeFault != CloudSaveEnvelopeFault.None)
                return Blocked(BlockFor(facts.cloudEnvelopeFault));

            // 서버가 이 기기가 아는 것보다 뒤로 갔다. 정상 경로에는 없는
            // 상태(revision은 단조 증가한다)라 자동으로 쓰지 않는다 - 여기서
            // 올리면 롤백된 서버 위에 새 사슬을 얹어 사고를 굳힌다
            if (facts.hasSidecar && facts.cloudRevision < facts.baseRevision)
                return Blocked(CloudSaveBlock.ServerRollback);

            // 로컬이 없거나 손상됐다. 클라우드가 유일한 기록이다
            if (!facts.hasLocal) return Verdict(CloudSaveDecision.DownloadCloud);

            // 같은 기록이다. 쓰지도 받지도 않고 sidecar만 서버 revision으로 맞춘다
            if (SameState(facts.localStateSha, facts.cloudStateSha))
                return Verdict(CloudSaveDecision.InSync);

            // sidecar가 없으면 **아무것도 모르는 상태**다. 두 기록이 다른데
            // 어느 쪽이 어느 쪽에서 이어졌는지 말할 근거가 없으므로 둘 다
            // 변한 것으로 본다 - 그 답은 충돌 화면이다
            bool localChanged = !facts.hasSidecar
                                || !SameState(facts.localStateSha, facts.lastSyncedStateSha);
            bool serverMoved = !facts.hasSidecar || facts.cloudRevision != facts.baseRevision;

            if (serverMoved && localChanged) return Verdict(CloudSaveDecision.Conflict);
            if (serverMoved) return Verdict(CloudSaveDecision.DownloadCloud);
            if (localChanged) return Verdict(CloudSaveDecision.UploadLocal);

            // 같은 revision인데 내용이 다르다. 사슬이 설명하지 못하는 상태라
            // (서버 문서가 revision 없이 바뀌었다) 자동으로 한쪽을 고르지 않는다
            return Verdict(CloudSaveDecision.Conflict);
        }

        /**
         * @brief revision 1을 새로 만들어도 되는가.
         *
         * **한 번도 동기화한 적 없는 기기만** 첫 정본을 만든다. sidecar가
         * revision을 들고 있다는 것은 이 uid의 사슬이 이미 존재했다는 뜻이고,
         * 그 사슬을 1로 되돌리는 것은 복구가 아니라 다른 기기의 base를
         * 무효로 만드는 일이다.
         */
        public static bool CanCreateFirstRevision(bool hasSidecar, long baseRevision)
        {
            if (!hasSidecar) return true;
            return baseRevision == 0L;
        }

        /**
         * @brief 서버 트랜잭션을 시작해도 되는가. **2단계가 지켜야 할 계약이다.**
         *
         * pending이 **디스크에 남은 뒤에만** 서버로 나간다. 응답 유실 복구가
         * 그 파일 하나에 걸려 있기 때문이다 - 서버는 커밋했는데 그 커밋의
         * mutation id가 로컬 어디에도 없으면, 다음 실행은 같은 쓰기를 다시
         * 올려 revision을 하나 더 올리고 직전 백업을 한 칸 밀어낸다.
         *
         * 그래서 순서가 고정이다:
         *
         *     MarkPending -> CloudSaveSidecar.Save(true 확인) -> 서버 트랜잭션
         *
         * 저장이 실패했으면 **로컬 dirty를 유지한 채** 다음 디바운스를 기다린다.
         * 그동안 게임은 그대로 돈다 - 오프라인 계약과 같은 자리다.
         */
        public static bool MayStartServerWrite(CloudSaveLocalState state, bool sidecarPersisted)
        {
            if (!sidecarPersisted) return false;
            if (state == null) return false;
            if (!state.IsWellFormed()) return false;

            // 보낼 것을 가리키는 id가 없으면 보낼 것도 없다
            return state.HasPending;
        }

        /**
         * @brief 봉투 검증 결과를 화면이 읽는 이유로 옮긴다.
         *
         * 형식 불일치가 여기서 InvalidEnvelope인 것은 **미래 형식이 이미
         * 앞에서 걸러졌기** 때문이다 - 여기 남는 것은 옛 형식이거나 우리가
         * 쓴 적 없는 값이다.
         */
        public static CloudSaveBlock BlockFor(CloudSaveEnvelopeFault fault)
        {
            switch (fault)
            {
                case CloudSaveEnvelopeFault.None:
                    return CloudSaveBlock.None;

                case CloudSaveEnvelopeFault.NoEnvelope:
                case CloudSaveEnvelopeFault.FormatVersionMismatch:
                case CloudSaveEnvelopeFault.RevisionOutOfRange:
                case CloudSaveEnvelopeFault.RevisionChainBroken:
                case CloudSaveEnvelopeFault.MutationIdMalformed:
                case CloudSaveEnvelopeFault.SessionIdMalformed:
                case CloudSaveEnvelopeFault.DeviceIdMalformed:
                    // id 셋이 규격 밖인 것은 payload의 문제가 아니라 **이 문서를
                    // 우리 코드가 쓰지 않았다**는 뜻이다 - 백업 복구가 아니라
                    // 서버 메타를 봐야 하는 자리라 손상과 갈라 둔다
                    return CloudSaveBlock.InvalidEnvelope;

                case CloudSaveEnvelopeFault.PayloadTooLarge:
                    return CloudSaveBlock.PayloadTooLarge;

                default:
                    // 해시 불일치·못 읽는 JSON·껍데기 세이브·버전 불일치·
                    // 상태 지문 불일치·요약 위조. 전부 "이 payload를 믿을 수
                    // 없다"는 한 문장이고, 사람에게 할 안내도 하나다
                    return CloudSaveBlock.CorruptPayload;
            }
        }

        /**
         * @brief 서버에 이미 적용된 커밋인가. **응답 유실 복구의 전부다.**
         *
         * 같으면 재시도하지 않는다. 재시도하면 revision이 하나 더 오르고
         * 직전 백업이 한 칸 밀려, 잃을 것이 없는 상황에서 백업 한 벌을 잃는다.
         *
         * 빈 값은 절대 같다고 하지 않는다 - 둘 다 비어 있는 상태(첫 실행)를
         * 이미 성공으로 읽으면 첫 업로드가 영원히 안 나간다.
         */
        public static bool WasCommitAlreadyApplied(string pendingMutationId,
                                                   string serverLastMutationId)
        {
            if (!CloudSaveIds.IsValid(pendingMutationId)) return false;
            if (!CloudSaveIds.IsValid(serverLastMutationId)) return false;
            return pendingMutationId == serverLastMutationId;
        }

        /**
         * @brief 이 sidecar를 지금 uid에 써도 되는가.
         *
         * 계정 복구로 uid가 바뀌면 여기 적힌 revision은 다른 계정의 사슬이다.
         * 그것을 그대로 쓰면 새 계정에 엉뚱한 baseRevision으로 출발하는 쓰기가
         * 나가고, 운이 나쁘면 통과한다. 아니라고 답하면 판정은 sidecar가 없는
         * 경우로 떨어지고, 그 답은 충돌 화면이다 - 복구에서 원하는 바로 그것이다.
         *
         * 불변식을 어긴 sidecar도 아니라고 답한다 - 반쯤 맞는 상태를 믿는 것이
         * 모르는 것보다 위험하다(CloudSaveLocalState.IsWellFormed).
         */
        public static bool SidecarAppliesTo(CloudSaveLocalState state, string uid)
        {
            if (state == null) return false;
            if (string.IsNullOrEmpty(uid)) return false;
            if (!state.IsWellFormed()) return false;
            return state.ownerUid == uid;
        }

        /** 마지막 heartbeat 이후 이만큼 지났으면 그 세션은 죽은 것으로 본다 */
        public static bool IsSessionExpired(float secondsSinceHeartbeat)
        {
            return secondsSinceHeartbeat >= SessionExpirySeconds;
        }

        /**
         * @brief 세션 문서를 보고 무엇을 할지 정한다. **보안 규칙과 같은 세 갈래다.**
         *
         * 규칙(firestore.rules `mayTakeSession`)이 같은 판단을 서버에서 한 번 더
         * 한다. 두 곳에 적는 이유는 역할이 다르기 때문이다 - 여기는 "거부될 쓰기를
         * 보내지 않는다"이고 규칙은 "보내와도 안 받는다"다. **최종 판정은 규칙**이고,
         * 특히 만료 판정은 서버 시각(request.time)으로만 정확하다 - 기기 시계는
         * 어긋날 수 있고 사용자가 바꿀 수도 있다.
         *
         * @param secondsSinceHeartbeat 기기 시계로 잰 추정값. 음수(미래 heartbeat)면
         *                              살아 있는 것으로 본다 - 시계가 어긋난 쪽이
         *                              남의 세션을 뺏는 것보다 안전하다
         */
        public static CloudSaveSessionClaim ClaimFor(bool exists, string ownerSessionId,
                                                     bool released, float secondsSinceHeartbeat,
                                                     string mySessionId)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return CloudSaveSessionClaim.Busy;

            // 자리가 비어 있다
            if (!exists) return CloudSaveSessionClaim.Claim;

            // 이미 내 것이다. 놓아준 뒤에 돌아온 경우도 여기다(다시 잡는다)
            if (ownerSessionId == mySessionId) return CloudSaveSessionClaim.Renew;

            if (released) return CloudSaveSessionClaim.Claim;
            if (IsSessionExpired(secondsSinceHeartbeat)) return CloudSaveSessionClaim.Claim;

            return CloudSaveSessionClaim.Busy;
        }

        /**
         * @brief 두 상태 지문이 같은가.
         *
         * 빈 값끼리는 같다고 하지 않는다. 지문을 계산하지 못한 것과 같은
         * 기록인 것은 전혀 다른 말인데, 여기서 섞으면 계산 실패가 **InSync**로
         * 읽혀 아무 동기화도 안 하는 상태가 조용히 정상처럼 보인다.
         */
        public static bool SameState(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        private static CloudSaveVerdict Verdict(CloudSaveDecision decision)
        {
            return new CloudSaveVerdict { decision = decision, block = CloudSaveBlock.None };
        }

        private static CloudSaveVerdict Blocked(CloudSaveBlock reason)
        {
            return new CloudSaveVerdict { decision = CloudSaveDecision.Blocked, block = reason };
        }
    }
}
