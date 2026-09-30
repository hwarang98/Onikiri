namespace Onikiri.Cloud
{
    /**
     * @brief 세션 문서를 읽은 한 장면. **순수 데이터다** (네트워크도 시계도 없다).
     *
     * `CloudSaveSession.PeekAsync`가 채우고 `CloudSaveTakeoverPolicy`가 읽는다.
     * 나이(초)를 미리 계산해 넣는 이유는 정책이 시계를 만지지 않게 하기 위해서다 -
     * 시계가 들어오는 순간 그 함수는 EditMode에서 잴 수 없는 것이 된다.
     */
    public struct CloudSaveSessionSnapshot
    {
        public bool exists;

        public string ownerSessionId;
        public string ownerDeviceId;
        public bool released;

        /** 인수마다 정확히 1씩 오른다. 첫 세션이 1 */
        public long generation;

        /** 누가 인수를 요청해 뒀는가. 없으면 빈 문자열 */
        public string takeoverSessionId;
        public string takeoverDeviceId;

        /** 기기 시계로 잰 추정값. 음수(미래)면 살아 있는 것으로 본다 */
        public float secondsSinceHeartbeat;

        /**
         * 서버가 적은 타임스탬프 원본. **그대로 되써야 하는 값이라** 들고 있다 -
         * 인수 요청은 owner의 heartbeat를 건드리지 않아야 하고(그러지 않으면 상대의
         * 만료 시계가 다시 감긴다), owner의 갱신은 인수 요청 시각을 건드리지 않아야
         * 한다(그러지 않으면 강제 인수가 영영 안 된다). 정책은 이것을 읽지 않는다 -
         * 위의 나이(초)만 읽는다.
         */
        public object heartbeatAt;
        public object takeoverAt;

        /** 인수 요청 뒤 몇 초인가. 요청이 없으면 음수 */
        public float secondsSinceTakeoverRequest;
    }

    /** 세션 문서를 보고 새 기기가 할 일 */
    public enum CloudSaveTakeoverStep
    {
        /** 빈 자리·놓아준 자리·만료된 자리. **묻지 않고** 잡는다 */
        Claim = 0,

        /** 이미 내 세션이다 */
        Renew,

        /** 다른 기기가 살아 있다. **사람에게 묻는다** - 자동으로 뺏지 않는다 */
        AskTheHuman
    }

    /**
     * @brief 단일 활성 기기와 **명시적 인수** (63단계). 전부 순수 함수다.
     *
     * ## 62단계까지의 정책과 무엇이 다른가
     *
     * | | 62단계까지 | 63단계 |
     * |---|---|---|
     * | 다른 기기가 살아 있을 때 | 커밋만 조용히 보류(`Busy`) | **확인 팝업** |
     * | 인수 조건 | 놓아줬거나 180초 만료 | + **사람이 누른 인수 요청** |
     * | 이전 기기 | 계속 논다. 나중에 충돌 화면 | **즉시 정지 + 종료 팝업** |
     * | 경쟁 판정 | sessionId 일치 | sessionId **+ generation** |
     *
     * 옛 정책의 구멍은 "살아 있는 기기 둘"이 **정상 상태로 취급됐다**는 것이다.
     * B는 커밋만 못 할 뿐 계속 놀았고, 그 사이 두 기기가 각자 진행을 쌓았다.
     * 나중에 하나가 세션을 얻으면 나머지 한 벌은 충돌 화면에서 **버려진다.**
     * 63단계는 그 갈라짐을 만들지 않는다 - 한쪽이 즉시 멈춘다.
     *
     * ## generation이 sessionId만으로 부족한 이유
     *
     * A가 B에게 자리를 넘긴 뒤, A의 **지연된 요청**이 도착할 수 있다. 예전
     * 계약에서는 그 요청이 `sessionId == 내 것`을 만족하지 못해 거부됐지만,
     * A가 다시 인수를 시도하면(앱 재실행) 같은 자리를 두고 두 기기가 번갈아
     * 뺏는 상태가 된다. generation은 **단조**라 늦게 온 것이 언제나 늦은 것으로
     * 판정된다 - 시계가 아니라 사슬로 순서를 정하는 것은 revision과 같은 이유다.
     *
     * **최종 판정은 언제나 Firestore Rules다.** 여기 있는 것은 거부될 쓰기를
     * 보내지 않고 사람에게 옳은 문장을 보여 주기 위한 것이다.
     */
    public static class CloudSaveTakeoverPolicy
    {
        /** 첫 세션의 세대. 0이 아닌 이유는 "없음"과 구분하기 위해서다 */
        public const long FirstGeneration = 1L;

        /**
         * @brief 인수 요청을 보낸 뒤 **강제 인수**까지 기다리는 시간 (초).
         *
         * 온라인 A는 이 시간을 다 쓰지 않는다 - 요청을 보고 저장·커밋·release를
         * 마치면 B가 그 자리에서 이어받는다. 이 값이 필요한 것은 **A가 오프라인
         * 이거나 응답하지 않는** 경우뿐이다.
         *
         * 규칙도 같은 값을 강제한다(`firestore.rules`). 갈리면 클라가 가능하다고
         * 판단한 순간 서버가 거부하고, 그 기기는 이유를 모른 채 못 들어간다.
         */
        public const float TakeoverWaitSeconds = 20f;

        /**
         * @brief 세션 문서를 다시 읽는 주기 (초).
         *
         * 두 쪽이 쓴다: 작성권을 든 기기가 **자기 자리를 뺏겼는지** 보고, 인수를
         * 요청한 기기가 **자리가 났는지** 본다. 리스너(스냅샷 구독) 대신 폴링인
         * 이유는 이 프로젝트의 Firestore 접근이 전부 단발 요청이기 때문이다 -
         * 리스너 하나를 위해 수명 관리(씬 재로드·계정 교체·앱 pause)를 새로
         * 들여오는 값이 5초 지연보다 크지 않다. **감지 지연은 보고서가 실측한다.**
         */
        public const float SessionPollSeconds = 5f;

        /**
         * @brief 작성권을 든 기기가 **자기 자리를 살려 두는** 주기 (초).
         *
         * ## 실기가 잡은 것 (63단계 두 기기 실측)
         *
         * 62단계까지 heartbeat는 **커밋 경로에서만** 나갔다
         * (`CloudSaveSync.CommitAsync`가 커밋 직전에 `AcquireAsync`를 부른다).
         * 그래서 세션 문서의 신선도는 "살아 있는가"가 아니라 **"최근에 서버로
         * 썼는가"**를 뜻했다. 둘이 대개 같이 가서 62단계에서는 드러나지 않았다.
         *
         * 63단계에서 그 차이가 사고가 된다. 부팅 판정이 `Conflict`인 기기는
         * `MayWrite`가 false라 **영영 커밋하지 않는다** - 화면 앞에서 사람이
         * 놀고 있는데 180초 뒤 자리가 만료되고, 다른 기기가 **묻지도 않고**
         * 가져간다. 실측에서 정확히 그렇게 됐다(A가 3분 33초 뒤 만료, B는
         * "만료된 자리"로 읽고 통과).
         *
         * 그래서 감시(`CloudSaveSessionWatch`)가 폴링을 도는 김에 시계도 감는다.
         * 만료(180초)의 3분의 1이면 한 번쯤 실패해도 자리를 잃지 않는다.
         */
        public const float HeartbeatSeconds = 60f;

        /**
         * @brief 새 기기가 무엇을 할 것인가.
         *
         * `CloudSavePolicy.ClaimFor`와 세 갈래가 같다. 다른 것은 이름이 말하는
         * 것뿐이다 - 62단계의 `Busy`는 "커밋을 미룬다"였고 여기 `AskTheHuman`은
         * **"사람에게 묻는다"**이다. 자동으로 넘어가는 갈래가 없다.
         */
        public static CloudSaveTakeoverStep StepFor(CloudSaveSessionSnapshot snapshot,
                                                    string mySessionId)
        {
            return StepFor(snapshot, mySessionId, null);
        }

        /**
         * @brief 기기 id까지 함께 본다. **자기 자신에게는 묻지 않는다.**
         *
         * ## 실기가 연 빚 (63단계 첫 부팅)
         *
         * 앱을 강제 종료하면 세션을 놓지 못하고 죽는다. 그 뒤 **같은 폰에서**
         * 다시 켜면 세션 id는 새것(실행마다 하나)이라 서버 문서의 owner와
         * 다르고, heartbeat는 아직 180초 안이다 - 그래서 판정이
         * `AskTheHuman`이 된다. 사람은 **자기 폰에게 "이 기기로 이어할까요"를
         * 묻는 창**을 보게 된다.
         *
         * 정책의 단위는 세션이 아니라 **기기**다("동일 계정 단일 활성 기기").
         * 같은 기기의 이전 실행은 물을 상대가 아니다 - 그 실행은 이미 죽었고,
         * 물어도 답할 수 있는 사람은 지금 이 화면 앞에 있는 그 사람뿐이다.
         *
         * @param myDeviceId 설치마다 하나(사이드카). null이면 옛 판정 그대로다
         */
        public static CloudSaveTakeoverStep StepFor(CloudSaveSessionSnapshot snapshot,
                                                    string mySessionId, string myDeviceId)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return CloudSaveTakeoverStep.AskTheHuman;

            if (!snapshot.exists) return CloudSaveTakeoverStep.Claim;

            if (snapshot.ownerSessionId == mySessionId) return CloudSaveTakeoverStep.Renew;

            if (snapshot.released) return CloudSaveTakeoverStep.Claim;

            // ★ 같은 기기의 이전 실행. 묻지 않고 잇는다
            if (CloudSaveIds.IsValid(myDeviceId) && snapshot.ownerDeviceId == myDeviceId)
                return CloudSaveTakeoverStep.Claim;

            if (CloudSavePolicy.IsSessionExpired(snapshot.secondsSinceHeartbeat))
                return CloudSaveTakeoverStep.Claim;

            return CloudSaveTakeoverStep.AskTheHuman;
        }

        /**
         * @brief 지금 owner를 **바꿔도 되는가** (인수 커밋).
         *
         * 세 갈래다:
         *
         *   놓아줬다        A가 요청을 보고 정리를 마쳤다. 곧바로 이어받는다
         *   만료됐다        180초 조용했다. 62단계부터 있던 갈래
         *   내 요청이 익었다 A가 응답하지 않았다. **강제 인수**
         *
         * 세 번째가 63단계의 새 갈래이고, 그것만이 **살아 있는 세션을 뺏는다.**
         * 그래서 두 조건을 함께 요구한다: 요청이 **내 것**이어야 하고(다른 기기의
         * 요청에 편승할 수 없다) 그 요청이 `TakeoverWaitSeconds`만큼 **익어야**
         * 한다. 규칙이 서버 시각으로 같은 것을 한 번 더 본다.
         */
        public static bool MayCommitTakeover(CloudSaveSessionSnapshot snapshot, string mySessionId)
        {
            return MayCommitTakeover(snapshot, mySessionId, null);
        }

        public static bool MayCommitTakeover(CloudSaveSessionSnapshot snapshot,
                                             string mySessionId, string myDeviceId)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return false;
            if (!snapshot.exists) return false;
            if (snapshot.ownerSessionId == mySessionId) return false;   // 이미 내 것이다

            if (snapshot.released) return true;

            // 같은 기기의 이전 실행은 인수가 아니라 이어받기다 (StepFor와 짝)
            if (CloudSaveIds.IsValid(myDeviceId) && snapshot.ownerDeviceId == myDeviceId)
                return true;

            if (CloudSavePolicy.IsSessionExpired(snapshot.secondsSinceHeartbeat)) return true;

            return MayForceTakeover(snapshot, mySessionId);
        }

        /** ★ 살아 있는 세션을 뺏는 **유일한** 갈래. 내 요청이 익었을 때만이다 */
        public static bool MayForceTakeover(CloudSaveSessionSnapshot snapshot, string mySessionId)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return false;
            if (!snapshot.exists) return false;
            if (snapshot.takeoverSessionId != mySessionId) return false;

            // 음수 = 요청이 없다. 0 이상이어야 나이를 잰 것이다
            if (snapshot.secondsSinceTakeoverRequest < 0f) return false;

            return snapshot.secondsSinceTakeoverRequest >= TakeoverWaitSeconds;
        }

        /**
         * @brief 인수 **요청**을 남겨도 되는가.
         *
         * 요청은 owner를 바꾸지 않는다. 살아 있는 남의 자리에만 남기고, 이미
         * 잡을 수 있는 자리(빈 자리·놓아준 자리·만료)에는 남기지 않는다 -
         * 그 자리는 요청 없이 곧바로 잡는 것이 맞고, 요청을 남기면 A가 존재하지도
         * 않는데 20초를 기다리는 일이 된다.
         */
        public static bool MayRequestTakeover(CloudSaveSessionSnapshot snapshot, string mySessionId)
        {
            return StepFor(snapshot, mySessionId) == CloudSaveTakeoverStep.AskTheHuman
                   && snapshot.takeoverSessionId != mySessionId;
        }

        /**
         * @brief 내가 owner인데 **남이 이어하기를 눌렀다.** 스스로 물러날 때다.
         *
         * ## 이것이 없으면 어떻게 되는가 - 실기가 보여 줬다
         *
         * 구현하지 않았을 때도 B는 결국 들어간다 - 요청이 20초 익으면 강제
         * 인수가 열리기 때문이다. 그래서 **동작은 하는 것처럼 보인다.** 잃는
         * 것은 둘이다: 사람이 매번 20초를 기다리고, **A의 마지막 몇 초가
         * 서버에 못 올라간다**(A는 자리를 잃는 순간까지 커밋할 이유가 없다).
         *
         * 그 몇 초가 정확히 사람이 마지막으로 한 일이다.
         */
        public static bool ShouldYieldSeat(CloudSaveSessionSnapshot snapshot, string mySessionId)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return false;
            if (!snapshot.exists) return false;
            if (snapshot.ownerSessionId != mySessionId) return false;   // 이미 내 자리가 아니다

            return CloudSaveIds.IsValid(snapshot.takeoverSessionId)
                   && snapshot.takeoverSessionId != mySessionId;
        }

        /** 인수 성공마다 **정확히 1** 오른다 */
        public static long NextGeneration(long current)
        {
            return current < FirstGeneration ? FirstGeneration : current + 1L;
        }

        /**
         * @brief 내가 **작성권을 잃었는가.**
         *
         * 두 가지 중 하나면 잃은 것이다: owner가 내가 아니거나, owner는 나인데
         * 세대가 내가 아는 값이 아니거나. 두 번째가 실제로 생기는 경우는
         * "내 세션 id를 그대로 둔 채 세대만 오른" 상태인데, 그런 문서는 규칙이
         * 만들지 못한다 - 그래도 본다. **모르는 문서를 보면 쓰지 않는 쪽**이
         * 이 계약의 방향이고, 여기서 관대해지면 그 관대함이 곧 이중 작성이다.
         *
         * 문서를 못 읽은 경우(exists=false)는 **잃은 것으로 치지 않는다.** 세션
         * 문서가 없는 상태는 오프라인·삭제 어느 쪽도 될 수 있고, 그것을 상실로
         * 읽으면 지하철에서 게임이 멈춘다. 오프라인은 정상 경로다.
         */
        public static bool LostWrite(CloudSaveSessionSnapshot snapshot, string mySessionId,
                                     long myGeneration)
        {
            if (!CloudSaveIds.IsValid(mySessionId)) return false;
            if (!snapshot.exists) return false;

            // ★★ **잡은 적이 없으면 잃을 것도 없다.** 이 줄이 owner 비교보다
            // 앞에 있어야 한다 - 뒤에 두면 부팅 직후, 아직 세션을 잡기 전의
            // 첫 폴링이 서버에 남은 **이전 실행의 문서**를 보고 "다른 기기가
            // 인수했다"로 읽는다. 실기 첫 부팅이 정확히 그것이었다
            // ("세대 0 -> 0", 부팅 3초 만에 자기 자신에게 회수).
            if (myGeneration < FirstGeneration) return false;

            if (snapshot.ownerSessionId != mySessionId) return true;

            return snapshot.generation != myGeneration;
        }

        /**
         * @brief 화면에 적을 한 줄. 진단과 팝업이 같은 문장을 쓴다.
         */
        public static string Describe(CloudSaveTakeoverStep step)
        {
            switch (step)
            {
                case CloudSaveTakeoverStep.Claim: return "빈 자리 - 바로 시작";
                case CloudSaveTakeoverStep.Renew: return "이 기기가 이미 활성";
                default: return "다른 기기에서 플레이 중";
            }
        }
    }
}
