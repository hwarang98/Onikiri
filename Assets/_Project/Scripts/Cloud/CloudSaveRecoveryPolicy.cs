namespace Onikiri.Cloud
{
    /**
     * @brief 복구 로그인 뒤 세이브를 어떻게 이을 것인가. **다섯 갈래가 전부다.**
     *
     * 여섯 번째로 "병합"을 만들고 싶어지는 자리가 정확히 여기다 - 두 계정의
     * 세이브가 눈앞에 있고, 둘 다 아까운 값이 들어 있다. 만들지 않는다
     * (CloudSavePolicy 머리 주석). 세이브는 한 벌 통째 선택이지 필드 합치기가
     * 아니고, `AccountLink`의 `maxStage` 병합은 **랭킹 문서**의 규칙이지
     * 세이브의 규칙이 아니다(설계 §8.2 꼬리).
     */
    public enum CloudRecoveryPlan
    {
        /**
         * @brief 기존 계정에 세이브가 없다. 현재 로컬이 그 계정의 첫 정본이 된다.
         *
         * 새 uid의 sidecar를 revision 0으로 세우고 Dirty로 들어간다 - 다음
         * 커밋이 revision 1을 만든다 (설계 §8.2 ③).
         */
        KeepLocalAsFirstRevision = 0,

        /**
         * @brief 두 세이브의 상태 지문이 같다. **조용히** 서버 head를 채택한다.
         *
         * 같은 기록을 두고 사람에게 고르라고 묻는 것은 질문이 아니라 소음이다
         * (설계 §8.2 ④).
         */
        AdoptServerQuietly,

        /**
         * @brief 둘 다 있고 서로 다르다. **사람이 고른다** (설계 §8.2 ⑤).
         *
         * 60단계 충돌 화면(CloudConflictPanel)을 그대로 재사용한다 - 자동 병합
         * 없음, 필드별 병합 금지.
         */
        AskTheHuman,

        /**
         * @brief 서버를 못 봤다 (오프라인·실패). **지금 아무것도 정하지 않는다.**
         *
         * 못 본 것과 없는 것을 섞으면 오프라인 복구가 "세이브 없음 -> 로컬을
         * 첫 정본으로"로 읽혀, 기존 계정의 세이브 위에 revision 1 사슬을 새로
         * 얹으려는 쓰기가 나간다(트랜잭션이 막긴 하지만, 보내지 않는 것이 먼저다).
         * 로컬로 계속 놀고, 다음 부팅·다음 커밋이 서버를 다시 본다.
         */
        WaitForServer,

        /** 서버 문서가 성립하지 않는다 (봉투 손상·미래 형식). 적용도 덮어쓰기도 없다 */
        Blocked
    }

    /**
     * @brief 복구의 **규칙**. Firebase도 파일 입출력도 한 줄 없다.
     *
     * CloudSavePolicy·AccountLinkPolicy와 같은 이유로 갈라져 있다 - EditMode가
     * 설계 §8.2의 여섯 순서를 Firestore 없이 전부 검사한다.
     */
    public static class CloudSaveRecoveryPolicy
    {
        /**
         * @brief 기존 uid의 `playerSaves` 읽기 결과 -> 갈래.
         *
         * @param status        서버 읽기의 결과
         * @param localStateSha 복구 후보(현재 로컬)의 상태 지문
         * @param cloudStateSha 기존 계정 세이브의 상태 지문 (Found일 때만 의미)
         */
        public static CloudRecoveryPlan PlanFor(CloudSaveStoreStatus status,
                                               string localStateSha, string cloudStateSha)
        {
            switch (status)
            {
                case CloudSaveStoreStatus.Missing:
                    return CloudRecoveryPlan.KeepLocalAsFirstRevision;

                case CloudSaveStoreStatus.Found:
                    // 지문 비교는 부팅 판정과 **같은 함수**다. 빈 지문끼리를
                    // 같다고 하지 않는 것까지 그대로 물려받는다 - 계산 실패가
                    // "같은 기록"으로 읽히면 남의 진행을 조용히 덮는다
                    return CloudSavePolicy.SameState(localStateSha, cloudStateSha)
                        ? CloudRecoveryPlan.AdoptServerQuietly
                        : CloudRecoveryPlan.AskTheHuman;

                case CloudSaveStoreStatus.Invalid:
                    return CloudRecoveryPlan.Blocked;

                default:
                    // Offline·Failed - 못 본 것이지 없는 것이 아니다
                    return CloudRecoveryPlan.WaitForServer;
            }
        }

        /**
         * @brief 이 갈래가 버려진 익명 문서를 지워도 되는가. **언제나 아니오다.**
         *
         * 규칙 4-B(`allow delete: if false`)의 판단을 클라이언트에서도 한 번 더
         * 적는다(설계 §8.2 ⑥). 삭제 권한을 열면 첫 피해자는 실수로 자기 기록을
         * 날린 사람이다 - AccountLinkPolicy.ShouldDeleteAbandonedDocument와
         * 같은 자리, 같은 답.
         */
        public static bool MayDeleteAbandonedSave(CloudRecoveryPlan plan)
        {
            return false;
        }
    }
}
