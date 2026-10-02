namespace Onikiri.Progression
{
    /**
     * @brief 스탯 포인트 보석 초기화의 한 문 (66단계).
     *
     * 버튼(StatPointResetButton)도 테스트 패널도 PlayMode 검사도 이것 하나를
     * 부른다. 셋이 각자 "보석을 빼고 포인트를 되돌리는" 두 줄을 적으면 언젠가
     * 하나가 순서를 바꾼다 - 포인트를 먼저 되돌리고 보석 차감이 실패하면
     * 공짜 초기화가 생긴다.
     *
     * ## 순서
     *
     *   1. 되돌릴 것이 있는가(찍은 합 > 0)   없으면 아무것도 안 뺀다
     *   2. 무료 1회가 남았는가               남았으면 보석 없이 3으로
     *   3. 보석 차감(GemWallet.TrySpend)     실패하면 거기서 멈춘다
     *   4. 포인트 되돌림(ResetAllPoints)
     *
     * 3이 4보다 앞이다. 1을 지났으면 4는 실패하지 않으므로 "보석만 빠지고
     * 포인트는 그대로"가 성립하지 않는다.
     *
     * 보석은 GemWallet 한 문으로만 빠진다 - 60단계 urgent 동기화가
     * GemsChanged를 듣는다(Cloud/ 무수정으로 잡힌다).
     */
    public static class StatPointReset
    {
        public enum Result
        {
            /** 무료 1회로 되돌렸다 */
            ResetFree,

            /** 보석을 내고 되돌렸다 */
            ResetPaid,

            /** 찍은 포인트가 없다 */
            NothingToReset,

            /** 보석이 모자란다 */
            NotEnoughGems,

            /** 시스템이 없거나 다른 기기가 인수했다(CloudSavePlayLock) */
            Unavailable
        }

        /** 다음 초기화의 비용. 무료면 0 */
        public static int NextCost(CharacterLevel character)
        {
            if (character != null && character.IsNextResetFree) return 0;
            return StatPointCurve.ResetGemCost;
        }

        public static Result TryReset(CharacterLevel character, GemWallet gems)
        {
            if (character == null) return Result.Unavailable;

            // 63단계: 다른 기기가 인수했으면 진행을 더 바꾸지 않는다. 무료 쪽은
            // 보석 문(TrySpend의 같은 검사)을 안 지나므로 여기서 직접 본다
            if (Onikiri.Cloud.CloudSavePlayLock.Locked) return Result.Unavailable;

            if (character.SpentPoints <= 0) return Result.NothingToReset;

            if (character.IsNextResetFree)
                return character.ResetAllPoints(true) ? Result.ResetFree : Result.Unavailable;

            if (gems == null) return Result.Unavailable;
            if (!gems.TrySpend(StatPointCurve.ResetGemCost)) return Result.NotEnoughGems;

            character.ResetAllPoints(false);
            return Result.ResetPaid;
        }
    }
}
