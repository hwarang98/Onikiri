namespace Onikiri.Cloud
{
    /**
     * @brief 언제 클라우드로 쓰는가의 **규칙**. Firebase도 유니티도 한 줄 없다.
     *
     * LeaderboardPolicy와 같은 이유로 갈라져 있다 - 여기 적힌 것은 네트워크의
     * 성질이 아니라 이 게임의 성질이고, EditMode가 시계를 손에 쥐고 검사한다.
     *
     * ## 로컬 저장 30초 != 클라우드 쓰기
     *
     * 로컬 저장을 곧바로 Firestore write로 바꾸지 않는다. 방치형은 몇 시간씩
     * 켜져 있고, 서버가 붙잡아야 하는 것은 매 순간의 상태가 아니라 **기기를
     * 바꿔도 이어지는 지점**이다(설계 §9). 그래서 dirty가 된 뒤 120초를 묶는다.
     *
     * ## urgent가 따로 있는 이유
     *
     * 귀문 승리·보석을 쓴 뽑기·무료 10연 수령 같은 순간은 **잃으면 지불이
     * 사라지는** 순간이다. 그 직후 앱이 죽고 다음 부팅이 다른 기기에서 일어나면,
     * 120초 안의 진행은 서버에 없다 - 그 창을 줄이는 것이 urgent다.
     */
    public static class CloudSaveSyncPolicy
    {
        /** urgent 표시 뒤 실제 쓰기까지의 짧은 유예 (초). 연속 뽑기가 한 번에 묶인다 */
        public const float UrgentDelaySeconds = 2f;

        /** 실패 뒤 재시도까지 기다리는 시간 (초). 즉시 재시도는 같은 실패를 반복한다 */
        public const float RetryDelaySeconds = 30f;

        /**
         * @brief 지금 클라우드로 쓸 것인가.
         *
         * @param dirtySeconds  로컬이 서버보다 앞서 있은 지 몇 초인가 (dirty 아님 = 음수)
         * @param urgentSeconds urgent 표시 뒤 몇 초인가 (표시 없음 = 음수)
         * @param retrySeconds  마지막 실패 뒤 몇 초인가 (실패 없음 = 음수 아님 크게)
         */
        public static bool ShouldCommit(float dirtySeconds, float urgentSeconds, float retrySeconds)
        {
            if (dirtySeconds < 0f) return false;                    // 올릴 것이 없다
            if (retrySeconds >= 0f && retrySeconds < RetryDelaySeconds) return false;

            if (urgentSeconds >= 0f) return urgentSeconds >= UrgentDelaySeconds;
            return dirtySeconds >= CloudSavePolicy.DebounceSeconds;
        }

        /**
         * @brief 이 상태에서 클라우드 쓰기가 허용되는가.
         *
         * `Conflict`가 핵심이다 - 갈라진 상태에서 자동으로 쓰면 그것이 곧
         * 한쪽 브랜치의 자동 선택이다. 사람이 고르기 전까지 쓰기는 멈춘다
         * ("나중에 결정"이 이 상태를 유지한다).
         */
        public static bool MayWrite(CloudSaveState state)
        {
            switch (state)
            {
                case CloudSaveState.Conflict:
                case CloudSaveState.Blocked:
                case CloudSaveState.Bootstrapping:
                    return false;
                default:
                    return true;
            }
        }

        /**
         * @brief 커밋 결과 -> 다음 상태.
         *
         * 성공 둘만 InSync다. 나머지는 로컬 dirty가 유지되고(57단계 pending 계약),
         * 그 사실이 상태에 그대로 읽혀야 한다.
         */
        public static CloudSaveState StateAfterCommit(CloudSaveStoreStatus status)
        {
            switch (status)
            {
                case CloudSaveStoreStatus.Committed:
                case CloudSaveStoreStatus.AlreadyApplied:
                    return CloudSaveState.InSync;

                case CloudSaveStoreStatus.Conflict:
                    return CloudSaveState.Conflict;

                case CloudSaveStoreStatus.Invalid:
                    return CloudSaveState.Blocked;

                default:
                    // Busy·Offline·Failed - 올릴 것이 그대로 남아 있다
                    return CloudSaveState.Dirty;
            }
        }

        /**
         * @brief 설정 화면의 네 문장 (설계 §9). **다섯 번째는 없다.**
         *
         * 상태기계는 여섯 칸이지만 사람에게 필요한 답은 넷이다 - "내 기록이
         * 지금 어디에 있는가". Bootstrapping과 LocalOnly·Dirty가 같은 문장인
         * 이유다: 셋 다 "기기에는 있고 서버에는 아직"이다.
         */
        public static string StatusLine(CloudSaveState state, bool otherDeviceActive)
        {
            if (otherDeviceActive) return "다른 기기에서 플레이 중";

            switch (state)
            {
                case CloudSaveState.InSync:
                    return "클라우드 저장 완료";

                case CloudSaveState.Conflict:
                case CloudSaveState.Blocked:
                    return "기록 선택 필요";

                default:
                    return "기기에 저장됨 · 연결되면 동기화";
            }
        }
    }
}
