using UnityEngine;

namespace Onikiri.Progression
{
    /** 온보딩이 지금 무엇을 가리키는가 */
    public enum SkillIntroHint
    {
        /** 가리킬 것이 없다. 강조를 끈다 */
        None,

        /** 혈조 강화 버튼. 아직 교체할 만큼 안 세다 */
        Upgrade,

        /** 교체 대상 슬롯. 혈조가 그 자리보다 세졌다 - 눌러서 비운다 */
        Swap,

        /**
         * @brief 혈조 장착 버튼. **빈 자리가 있다.**
         *
         * 교체 안내를 따라 자리를 비운 **직후**가 여기다. Swap을 그대로
         * 두면 방금 비운 빈 칩이 계속 깜빡이는데, 그 칩에는 이제 할 일이
         * 없다 - 눌러야 하는 것은 혈조 줄의 장착 버튼이다.
         */
        Equip
    }

    /** 강조 한 건. 어느 오의의 무엇을 가리키는가 */
    public struct SkillIntroAdvice
    {
        public SkillIntroHint Hint;

        /** 혈조의 카탈로그 번호. Hint가 None이면 -1 */
        public int SkillIndex;

        /** 교체로 밀려날 자리. Swap일 때만 0 이상 */
        public int SlotIndex;

        /** 그 자리에 든 오의의 카탈로그 번호. Swap일 때만 0 이상 */
        public int WeakestIndex;
    }

    /**
     * @brief 온보딩 10연으로 받은 **혈조를 어떻게 쓰는가** 한 걸음.
     *
     * ## 왜 "받았다"에서 안 끝나는가
     *
     * 무료 10연은 혈조를 준다(SkillGachaCurve.StandardUnlockOrder의 머리).
     * 그런데 받은 것과 세지는 것은 다르다 - **Lv.1 혈조를 그냥 끼우면
     * 기여가 오히려 줄어든다.**
     *
     *   혈조 Lv.1   0.45 / 2.5초 = **0.180**
     *   연참 Lv.5   0.54 x 1.12^4 / 3초 = **0.283**
     *
     * 36%가 깎인다. 새 오의를 받은 플레이어가 가장 먼저 하는 행동이
     * "끼운다"인데 그것이 손해라면, 선물이 함정이 된다. 그래서 순서를
     * 뒤집는다 - **먼저 올리고, 넘어선 다음에 끼운다.**
     *
     * ## 자리가 비어 있으면 대개 이 화면이 아예 안 뜬다
     *
     * `GrantGachaSkill`이 `FillEmptySlots`를 부르므로 빈 칸이 있으면
     * 혈조는 이미 들어가 있다. 그 순간 온보딩은 끝난 것이고
     * (`SkillGachaSystem.MarkIntroEquipDone`), 여기는 None을 돌려준다.
     *
     * **그런데 빈 칸이 뒤늦게 생기는 길이 하나 있다** - 교체 안내를 따라
     * 플레이어가 자리를 비운 직후다(SkillSlotChip이 `Equip(slot, -1)`을
     * 부른다). 그때는 Equip을 돌려준다. 이 게임에 "밀어내며 끼우기"는
     * 없고 비우기와 끼우기가 **두 동작**이므로, 안내도 두 걸음이어야 한다.
     *
     * ## 한 번만이다
     *
     * "보유 && 미장착"으로 판정하면 1회성이 아니다 - 나중에 혈조를 빼면
     * 온보딩 강조가 되살아난다. 판정에 `introEquipDone`(세이브 v20의 세
     * 번째 필드)을 넣는 이유가 그것이다. 혈조가 처음 장착된 순간 참이
     * 되고, 이후 무엇을 빼든 여기는 다시 안 뜬다.
     *
     * ## 계산은 CastRate와 같은 것을 쓴다
     *
     * 기여 = `MultiplierOf(i) / cooldownSeconds`다. 상성을 포함하는 것은
     * 그것이 **실제로 화면에 나가는 힘**이기 때문이고, `SkillSystem.CastRate`가
     * 합을 낼 때 쓰는 식과 글자 그대로 같다. 위 실측 예의 숫자는 상성이
     * 1.0인 st14 시점 값이다.
     *
     * **상한값(CeilingRateOf)으로 비교하지 않는다.** 상한은 만렙의 이야기고
     * 지금 물어보는 것은 "지금 끼우면 세지는가"다.
     */
    public static class SkillIntroGuide
    {
        /**
         * @brief 지금 가리킬 것을 고른다. **문자열을 안 만든다.**
         *
         * 화면이 이것을 이벤트마다 부르므로(SkillPanel), 비용은 정수·실수
         * 비교 몇 개여야 한다 - GuideQuestLine.Resolve와 같은 규칙이다.
         *
         * @param skills         오의 상태
         * @param introClaimed   무료 10연을 받았는가
         * @param introEquipDone 혈조를 이미 한 번 장착했는가
         */
        public static SkillIntroAdvice Resolve(SkillSystem skills,
                                               bool introClaimed, bool introEquipDone)
        {
            var advice = new SkillIntroAdvice();
            advice.Hint = SkillIntroHint.None;
            advice.SkillIndex = -1;
            advice.SlotIndex = -1;
            advice.WeakestIndex = -1;

            if (skills == null || !introClaimed || introEquipDone) return advice;

            int intro = SkillCatalog.IndexOf(SkillCatalog.BloodWhipId);
            if (intro < 0) return advice;

            // 안 받았거나(뽑기가 다른 것을 줬다) 이미 끼워져 있으면 할 말이
            // 없다. 후자는 곧 introEquipDone이 되지만, 그 표식을 세우는 것은
            // 여기가 아니라 SkillSystem을 만지는 쪽이다
            if (!skills.IsUnlocked(intro) || skills.IsEquipped(intro)) return advice;

            advice.SkillIndex = intro;

            // 가장 약한 자리를 고른다. 빈 칸이 먼저다 - 비어 있으면 밀어낼
            // 것이 없으므로 물어볼 것도 "지금 끼우면 세지는가"가 아니라
            // 그냥 "끼워라"다
            int capacity = skills.SlotCapacity;
            int weakestSlot = -1;
            int weakestIndex = -1;
            double weakestRate = double.MaxValue;

            for (int s = 0; s < capacity; s++)
            {
                if (skills.IsSlotLocked(s)) continue;

                int i = skills.EquippedAt(s);
                if (i < 0)
                {
                    advice.SlotIndex = s;
                    advice.Hint = SkillIntroHint.Equip;
                    return advice;
                }

                double rate = RateOf(skills, i);
                if (rate >= weakestRate) continue;

                weakestRate = rate;
                weakestSlot = s;
                weakestIndex = i;
            }

            // 열린 자리가 하나도 없다(전부 잠김). 가리킬 것이 없으므로
            // SkillIndex도 되돌린다 - 화면이 힌트만 보고 그리므로 남겨 두면
            // 안 쓰이지만, 값이 서로 안 맞는 구조체를 돌려주지 않는다
            if (weakestSlot < 0)
            {
                advice.SkillIndex = -1;
                return advice;
            }

            advice.SlotIndex = weakestSlot;
            advice.WeakestIndex = weakestIndex;

            // 지금 끼워서 세지는가. 같아도 끼운다 - 온보딩을 무한히 붙잡아
            // 두는 것보다 동률에서 새 오의를 쥐여주는 쪽이 낫고, 새 오의는
            // 앞으로 올릴 칸이 더 남아 있다
            advice.Hint = RateOf(skills, intro) >= weakestRate
                ? SkillIntroHint.Swap
                : SkillIntroHint.Upgrade;

            return advice;
        }

        /**
         * @brief 지금 이 오의가 초당 내는 배율. `SkillSystem.CastRate`의 한 항이다.
         *
         * 쿨타임이 0 이하면 나눌 수가 없다. 그런 자리는 비교에서 빠지도록
         * 0을 돌려준다 - 무한대를 돌려주면 "가장 약한 자리"가 영영 그것을
         * 안 고르는 대신, 0이면 그 자리가 먼저 밀려난다. 데이터가 깨진
         * 쪽을 밀어내는 것이 맞다.
         */
        public static double RateOf(SkillSystem skills, int index)
        {
            if (skills == null || index < 0) return 0d;

            var slot = skills.GetSlot(index);
            if (slot == null || slot.cooldownSeconds <= 0f) return 0d;

            return skills.MultiplierOf(index) / slot.cooldownSeconds;
        }

        /** 한 칸 더 올렸을 때의 기여. 강화 버튼이 목표에 닿았는지 재는 값 */
        public static double NextRateOf(SkillSystem skills, int index)
        {
            if (skills == null || index < 0) return 0d;

            var slot = skills.GetSlot(index);
            if (slot == null || slot.cooldownSeconds <= 0f) return 0d;

            return skills.NextMultiplierOf(index) / slot.cooldownSeconds;
        }
    }
}
