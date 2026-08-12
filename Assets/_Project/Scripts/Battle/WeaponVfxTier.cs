using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 무기 등급이 참격을 얼마나 화려하게 만드는가 (51단계).
     *
     * ## 이 표가 답하는 질문
     *
     * 캐릭터가 든 칼은 무엇을 껴도 같은 그림이다. 재작화는 비싸고, 대신
     * **베는 순간**이 등급을 말한다 - 등급이 오를 때마다 참격의 색이 달아오르고
     * (청 -> 보라 -> 주황 -> 흑적), 고등급에서는 스파크가 겹으로 얹힌다.
     * 게임 전체에 등급업이 다섯 번뿐이라, 그 다섯 순간마다 화면에 보상이 뜬다.
     *
     * ## 순수 연출이다
     *
     * 여기 있는 것은 색·알파·겹 수뿐이고, 데미지·쿨다운·밴드 어디에도 닿지
     * 않는다. 상태도 없다 - 티어는 장비 등급(기존 세이브 상태)에서 매번
     * 파생되므로 세이브에 새 필드가 없고, 마이그레이션도 없다.
     *
     * ## 왜 static인가
     *
     * SkillCatalog·YodoCurve와 같은 이유다. 이 값들은 밸런스가 아니라 연출
     * 상수지만, 빌더(SkillPanelBuilder가 티어별 참격 시트를 자를 때)와
     * 테스트가 씬 없이 같은 표를 읽어야 한다. 씬 컴포넌트에 두면 표가
     * 두 벌이 되고, 두 벌은 언젠가 갈린다.
     *
     * ## 색 램프 - 온도가 오른다
     *
     * 참격 팩(Slashes)의 5색 중 넷을 쓴다. 초록(color1)은 안 쓴다 -
     * ImpactSpark가 못 박은 규칙대로 초록은 먹빛·적·벚꽃 팔레트와 충돌한다.
     *
     *   티어1 무쇠칼   청(color5)    수수한 한색. 시작점
     *   티어2 강철칼   보라(color3)  한색이지만 요괴 기운이 돈다
     *   티어3 요괴검   주황(color4)  난색 진입 - 칼이 달아오르기 시작
     *   티어4 귀살도   흑적(color2)  이 게임의 "진짜" 색에 도달
     *   티어5 명공검   흑적(color2)  색은 같고 **오라 겹**이 는다
     *
     * 티어4와 5의 색이 같은 것은 팩에 색이 넷뿐이라서가 아니라(하나 남는다),
     * 흑적 위가 없기 때문이다 - 명공검의 도약은 색이 아니라 겹으로 말한다.
     *
     * 데미지 팔레트와 안 겹친다: 평타 크림(#FFF4D6)·처치(#FF8A7A)·
     * 치명(#FFD34D)은 전부 밝은 난색이고, 이 램프는 어두운 한색에서 시작해
     * 흑적(검은 심)으로 끝난다. 주황(티어3)만 치명 금과 계열이 닿는데,
     * 귀참의 이름 플래시(#FF9500)가 이미 그 자리에 있어 새 충돌이 아니다.
     */
    public static class WeaponVfxTier
    {
        public const int MinTier = 1;
        public const int MaxTier = 5;

        /**
         * @brief 테스트 패널이 티어를 강제한다. 0이면 꺼짐(실제 등급을 읽는다).
         *
         * 등급 1->5를 눈으로 비교하려면 장비를 다섯 번 올렸다 되돌려야 하는데,
         * 그 등급은 스탯에도 곱해지므로(EquipmentSystem.ApplyToStats) 되돌리는
         * 것을 잊으면 밸런스 확인이 오염된다. 연출만 바꾸는 스위치가 따로
         * 있어야 확인이 안전하다. **연출 코드만 이 값을 본다** - 스탯 경로는
         * 이 파일을 모른다.
         */
        public static int DebugForcedTier = 0;

        /** 프리미엄(오니키리 완성)도 같은 이유로 강제할 수 있다. -1 꺼짐 / 0 미완성 / 1 완성 */
        public static int DebugForcedPremium = -1;

        // ---------------------------------------------------------------- 티어

        /**
         * @brief 지금 티어 (1~5). 무기 슬롯의 등급 그대로다.
         *
         * 씬에 EquipmentSystem이 없으면 1로 떨어진다 - 전투 전용 테스트 씬에서
         * 참격이 사라지는 대신 가장 수수한 것이 나온다.
         */
        public static int CurrentTier()
        {
            if (DebugForcedTier >= MinTier && DebugForcedTier <= MaxTier)
                return DebugForcedTier;

            var equipment = EquipmentSystem.Instance;
            if (equipment == null) return MinTier;

            int index = equipment.IndexOf(EquipmentCatalog.WeaponId);
            var slot = index >= 0 ? equipment.GetSlot(index) : null;
            if (slot == null) return MinTier;

            return Mathf.Clamp(slot.grade, MinTier, MaxTier);
        }

        /** 오니키리가 완성됐는가. 완성이면 참격에 프리미엄 겹이 하나 더 얹힌다 */
        public static bool IsPremium()
        {
            if (DebugForcedPremium == 0) return false;
            if (DebugForcedPremium == 1) return true;

            var yodo = YodoSystem.Instance;
            return yodo != null && yodo.IsOnikiriComplete;
        }

        // ---------------------------------------------------------------- 참격 색

        /**
         * @brief 티어가 쓰는 참격 팩의 색 번호. 빌더가 시트를 자를 때 읽는다.
         *
         * 색 번호는 팩의 파일 이름(`Slash_128x128_Slash3_colorN.png`)이다.
         * 위 머리 주석의 램프 - 청(5) -> 보라(3) -> 주황(4) -> 흑적(2) x2.
         */
        public static int SlashColorOf(int tier)
        {
            switch (Mathf.Clamp(tier, MinTier, MaxTier))
            {
                case 1: return 5;
                case 2: return 3;
                case 3: return 4;
                default: return 2;
            }
        }

        // ---------------------------------------------------------------- 스파크 겹

        /**
         * @brief 오의 참격에 얹는 스파크 겹 수. **마지막 타격에만 얹는다.**
         *
         * 저등급은 0이다 - "등급이 오르면 화려해진다"가 성립하려면 시작점이
         * 수수해야 한다. 지금까지의 화면(스파크 없음)이 곧 티어1~2다.
         */
        public static int SparkLayers(int tier, bool premium)
        {
            int layers;
            switch (Mathf.Clamp(tier, MinTier, MaxTier))
            {
                case 1: layers = 0; break;
                case 2: layers = 0; break;
                case 3: layers = 1; break;
                case 4: layers = 1; break;
                default: layers = 2; break;
            }
            return premium ? layers + 1 : layers;
        }

        /**
         * @brief 스파크·글로우의 곱 틴트. 시트가 은백으로 구워져 있어 어떤
         *        색이든 낼 수 있다 (47단계 "은색 바탕" 규칙).
         *
         * 램프는 참격 색과 같은 온도 순서다. 티어4부터는 심홍 - 흑적 참격의
         * 밝은 자리(#B32849 언저리)와 같은 계열이라 참격과 스파크가 한 벌로
         * 읽힌다. ImpactSpark의 평타 붉음(#FF453A)보다 어둡고 자줏빛이라
         * "평타 불꽃이 커졌다"로는 안 읽힌다.
         */
        public static Color SparkTint(int tier, bool premium)
        {
            if (premium) return PremiumTint;

            switch (Mathf.Clamp(tier, MinTier, MaxTier))
            {
                case 1: return new Color(0.55f, 0.80f, 1.00f);
                case 2: return new Color(0.75f, 0.55f, 1.00f);
                case 3: return new Color(1.00f, 0.62f, 0.25f);
                default: return new Color(0.85f, 0.20f, 0.35f);
            }
        }

        /**
         * @brief 오니키리 완성의 색. 깊은 자주 - 요도의 계열이다.
         *
         * 금(치명타 #FFD34D)도 흰(피격 플래시·평타 궤적)도 못 쓴다. 요도
         * 시스템이 요괴의 칼이므로 그 완성은 요괴의 자줏빛이 맞고, 티어2의
         * 연보라와는 밝기로 갈린다(이쪽이 훨씬 깊다).
         */
        public static Color PremiumTint
        {
            get { return new Color(0.62f, 0.20f, 0.85f); }
        }

        /**
         * @brief 일섬 섬광이 티어 색으로 기우는 정도 (Color.Lerp의 t).
         *
         * 섬광의 흰 심은 텍스처에 구워져 있어 곱색이 무엇이든 살아남는다.
         * 그래서 여기 값이 0.4를 넘지 않아도 옆면의 붉은 기가 또렷이 물든다 -
         * 더 올리면 "돌진의 잔광"이 아니라 "색칠한 막대"가 된다.
         */
        public static float StreakBlend(int tier)
        {
            switch (Mathf.Clamp(tier, MinTier, MaxTier))
            {
                case 1: return 0f;
                case 2: return 0.12f;
                case 3: return 0.2f;
                case 4: return 0.28f;
                default: return 0.38f;
            }
        }

        // ---------------------------------------------------------------- 평타 글로우

        /**
         * @brief 평타 타격 불꽃 뒤에 얹는 오라의 진하기. 0이면 안 얹는다.
         *
         * 평타는 초당 몇 번씩 나가는 상시 연출이라 **절제가 규칙이다** -
         * 티어1은 아예 없고, 끝까지 가도 0.6을 안 넘는다. 오라는 12px 불꽃의
         * 뒤(sortingOrder -1)에 2배로 서는 넓은 판이라, 알파가 이보다 올라가면
         * 타격점이 "찍힌다"가 아니라 "번진다"로 읽힌다.
         */
        public static float GlowAlpha(int tier, bool premium)
        {
            float alpha;
            switch (Mathf.Clamp(tier, MinTier, MaxTier))
            {
                case 1: alpha = 0f; break;
                case 2: alpha = 0.22f; break;
                case 3: alpha = 0.32f; break;
                case 4: alpha = 0.45f; break;
                default: alpha = 0.58f; break;
            }
            // 프리미엄은 진하기가 아니라 색으로 말한다. 여기서 알파까지 올리면
            // 상시 연출이 오의보다 시끄러워진다
            return alpha;
        }

        /** 평타 오라의 색. 스파크와 같은 램프를 쓴다 - 한 벌로 읽혀야 한다 */
        public static Color GlowTint(int tier, bool premium)
        {
            return SparkTint(tier, premium);
        }
    }
}
