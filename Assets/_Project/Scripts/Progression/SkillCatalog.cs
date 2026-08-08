using System;

namespace Onikiri.Progression
{
    /**
     * @brief 오의가 데미지를 **어떻게 뿌리는가**.
     *
     * 27단계에 생겼다. 26단계에는 셋 다 단일 대상 한 방이었고, 색만 다른 같은
     * 아크가 떴다 - 화면에서 "무엇이 나갔는지"가 구분되지 않았고 그것이 뽕맛이
     * 없다는 소감의 원인이었다.
     *
     * **총 데미지는 바뀌지 않는다.** 배율·쿨다운·상한은 26단계 값 그대로이고,
     * 이 enum이 정하는 것은 그 데미지를 시간(다타)과 공간(관통·광역)에 어떻게
     * 펴는가뿐이다. 그래서 밸런스 가드가 성립한다 - **보스는 언제나 단일
     * 대상**이므로 어느 거동이든 보스에게 들어가는 총량이 같다.
     */
    public enum SkillShape
    {
        /**
         * @brief 한 대상을 여러 번. 총 배율을 HitCount로 나눈다.
         *
         * 나눗셈이라 합이 원래 배율과 정확히 같아야 한다. 마지막 타격이
         * **나머지를 받는다**(SkillCatalog.HitDamageShare) - 배율을 셋으로
         * 나눠 셋을 더하면 부동소수점에서 원래 값과 미세하게 어긋나고,
         * 그 어긋남이 "총 데미지 불변"을 검사할 수 없게 만든다.
         */
        MultiHit,

        /** 전방 일렬 관통. 경로의 모든 대상이 **각자 총 배율**을 받는다 */
        Pierce,

        /** 화면 전체. 살아 있는 모든 대상이 각자 총 배율을 받는다 */
        Screen
    }

    /**
     * @brief 발도 오의 하나의 설계값.
     *
     * 곡선은 셋이 공유하고(SkillCurve), 여기 있는 것은 그 곡선의 **시작점과
     * 리듬**뿐이다. 스킬 사이의 차이는 전부 `BaseMultiplier / CooldownSeconds`
     * 하나에서 나온다 - 그것이 이 축이 DPS에 기여하는 전부이기 때문이다.
     */
    public struct SkillSpec
    {
        /** 세이브와 UI가 이 문자열로 갈린다. 순서가 바뀌어도 레벨이 섞이지 않는다 */
        public string Id;

        public string DisplayName;

        /** 레벨 1의 배율. 공격력에 곱해진다 */
        public double BaseMultiplier;

        /**
         * @brief 시전 간격 (초). **성장 축이 아니다 - 끝까지 고정이다.**
         *
         * 쿨다운을 레벨로 줄이면 9단계 공격속도와 같은 벽에 부딪힌다. 자세한
         * 이유는 SkillCurve 참고.
         */
        public double CooldownSeconds;

        /** 이 캐릭터 레벨부터 목록에 열린다 */
        public int UnlockLevel;

        /**
         * @brief 위 레벨에 실제로 닿는 스테이지. **실측값이고 비용의 기준점이다.**
         *
         * 해금은 레벨로 걸리는데 비용은 골드 규모에 맞춰야 한다. 골드는 스테이지마다
         * x1.72로 자라므로(StageCurve.GoldGrowth), 어느 스테이지에서 열리는지를
         * 모르면 "비싸다/싸다"를 말할 수 없다.
         *
         * 이 값이 틀리면 비용이 통째로 어긋난다 - 한 스테이지만 틀려도 1.72배다.
         * 그래서 선언으로 두지 않고 `Skills_UnlockWhereTheCostAssumes` 가
         * 시뮬레이션 실측과 대조한다. 골드 축의 StagesToCeiling과 같은 처리다.
         */
        public int UnlockStage;

        /** 아이콘 파일명 (KURAI 팩). 에디터 빌더만 쓴다 */
        public string IconFile;

        // ------------------------------------------------------------ 27단계: 거동

        /** 데미지를 어떻게 뿌리는가. 총량은 바꾸지 않는다 */
        public SkillShape Shape;

        /**
         * @brief MultiHit의 타격 수. 나머지 거동은 1이다.
         *
         * 클립에 실제로 그려진 참격 프레임 수와 같아야 한다 - 23단계가 확인한
         * 대로 원화가가 그린 궤적이 정답이고, 그 프레임보다 많이 때리면 그림 없는
         * 타격이 생긴다. 연참은 ATTACK 1/2/3을 이어 붙여 f4·f10·f16 세 곳이다.
         */
        public int HitCount;

        /**
         * @brief 무게 등급 0~2. 히트스톱·셰이크·숫자 크기의 단계를 정한다.
         *
         * 배율 순서(연참 < 일섬 < 귀참)와 같아야 한다. 다르면 화면에서 가장
         * 무겁게 느껴지는 오의가 실제로 가장 센 오의가 아니게 되고, 그것은
         * 숫자보다 강한 거짓말이다.
         */
        public int Weight;

        /**
         * @brief 이 오의의 색. 참격 아크 · 이름 플래시 · 데미지 숫자가 **함께** 쓴다.
         *
         * 셋이 같은 색이어야 한 사건으로 읽힌다. 각자 색을 갖고 있으면 화면이
         * 오의 하나에 세 가지 색을 쓰게 되고, 그러면 색이 아무것도 가리키지 않는다.
         *
         * ## 흰 -> 적 -> 금인데 값이 그 이름과 조금 다른 이유
         *
         * 무기 등급 톤(23단계에 `Slash_White.png`을 남겨둔 이유)이 설계이지만,
         * **화면에 이미 있는 색을 피해야 한다.** 27단계에 27단계 색을 그대로 잡았다가
         * 실측으로 물렸다:
         *
         * ```
         * 연참 #FFF4E4  vs  평타 데미지 #FFF4D6   거의 같다  -> 구분 불가
         * 일섬 #FF6A4A  vs  처치 데미지 #FF8A7A   가깝다
         * 귀참 #FFD34D  vs  치명타 데미지 #FFD34D  완전히 같다 -> 치명타율 60% 구간에서
         *                                          화면의 큰 숫자 대부분이 이 색이다
         * ```
         *
         * 그래서 같은 계열 안에서 밀었다. 흰은 **차가운 쪽**으로(창백한 청백),
         * 적은 **더 선명한 쪽**으로, 금은 **더 깊은 쪽**으로(호박빛). 등급 순서는
         * 그대로 읽히고, 기존 세 색과는 나란히 놓아도 갈린다.
         *
         * 한 칸만 미는 것으로는 부족했다 - 흰을 #E8F4FF로 잡았을 때도 평타와의
         * 거리가 0.18이라 테스트가 잡았다. 흰과 크림색은 RGB에서 원래 가까워서,
         * 청색을 실제로 섞어야(#A8D8FF) 갈린다. `SkillShapeTests`가 여섯 쌍의
         * 거리를 전부 재고, 최소 거리 0.25가 그 기준이다.
         *
         * ## 남는 것 하나 - 일섬과 타격 불꽃
         *
         * 일섬 #FF4A3A는 평타 불꽃 #FF453A와 거리 0.02다. 거의 같다. 그래도 두는
         * 이유는 두 신호가 다른 층위에 있기 때문이다 - 불꽃은 12px짜리로 "여기
         * 맞았다"를 찍고, 일섬은 3.0u 아크와 x2 숫자와 이름으로 나온다. 크기가
         * 스물다섯 배 다르면 색이 같아도 같은 것으로 읽히지 않는다.
         *
         * 데미지 숫자 팔레트만 검사에 넣은 것도 그래서다. 숫자는 불꽃과 같은
         * 크기·같은 자리에 뜨므로 색이 유일한 구분자이지만, 아크는 아니다.
         *
         * 값은 Color가 아니라 32비트 RGBA다. 이 어셈블리는 UnityEngine을 참조하지만
         * 그래도 밸런스 표에 색 구조체를 섞지 않는 편이 낫다 - 이 배열은
         * 시뮬레이션과 테스트가 함께 읽는 곳이다.
         */
        public uint SlashRgba;

        /** 초당 환산 기여. 이 축이 DPS에 하는 일의 전부다 */
        public double BaseRate { get { return CooldownSeconds > 0d ? BaseMultiplier / CooldownSeconds : 0d; } }

        /**
         * @brief 첫 구매 비용. 두 가지에 비례한다.
         *
         *   초당 기여    셋의 골드당 효율을 같게 만든다 (SkillCurve.CostPerRate)
         *   해금 시점의 골드 규모  셋이 각자 열리는 자리에서 같은 무게를 갖게 한다
         *
         * 두 번째가 빠져 있었을 때 무슨 일이 났는지 적어둔다. 세 스킬의 비용을
         * 42/60/78로 두고 돌렸더니, **일섬과 귀참이 해금된 스테이지에서 곧바로
         * 상한까지 팔렸다.** st15의 초당 수입은 st8의 서른여섯 배라 60골드짜리
         * 첫 칸이 사실상 공짜였기 때문이다. 화면에서는 "새 스킬이 열렸는데 열자마자
         * MASTER"로 나타난다.
         *
         * 절대 골드 액수는 게임 어디에서도 고정된 뜻이 없다. 뜻을 갖는 것은
         * **그 시점의 수입 대비 몇 초치인가**뿐이고, 수입은 스테이지마다 x1.72로
         * 자란다. 그래서 해금 스테이지의 골드 배수를 곱한다.
         */
        public double BaseCost
        {
            get
            {
                return BaseRate * SkillCurve.CostPerRate
                       * StageCurve.GoldMultiplier(UnlockStage).ToDouble();
            }
        }
    }

    /**
     * @brief 발도 오의 셋. 자동 시전이고 골드로 레벨을 올린다.
     *
     * ## 왜 셋인가, 왜 전부 공격형인가
     *
     * 셋이면 패널 한 화면에 들어가고(스크롤 없음), 짧은/중간/긴 쿨다운으로
     * 리듬의 세 자리가 채워진다. 넷째부터는 리듬이 아니라 목록이 된다.
     *
     * 버프나 생존기를 하나 섞는 안도 있었지만 택하지 않았다. 이유는 **자**다 -
     * 공격형은 전부 초당 환산 기여 하나로 잴 수 있어서 여섯 축과 같은 저울에
     * 올라가지만(UpgradeEfficiency), 버프는 %DPS로, 생존기는 %EHP로 재야 한다.
     * 11단계에 생존 축이 SurvivalEfficiency로 갈라지고 20단계에 골드 축이
     * GoldGainEfficiency로 또 갈라졌다. 자가 넷째로 갈라지는 값은 스킬 셋 중
     * 하나를 다르게 만드는 것보다 크다.
     *
     * 생존기는 전직이나 장비에서 다시 볼 자리다.
     *
     * ## 자동 시전
     *
     * 쿨다운이 돌면 알아서 나간다. 방치형에서 "눌러야 나가는 오의"는 화면을
     * 보고 있는 사람에게만 주는 보상이라, 자리를 비우는 플레이어의 DPS가
     * 시뮬레이션과 갈린다. 수동 탭 보너스는 그 위에 얹는 별개 층이고 이번
     * 범위 밖이다.
     *
     * ## 해금이 Lv.10 / 15 / 20 인 이유
     *
     * 하단 탭이 12단계부터 "스킬 Lv.10"으로 서 있었고 그것이 약속이다. 나머지
     * 둘을 같이 열지 않는 이유는 두 가지다 - 셋을 한꺼번에 열면 그 순간 DPS가
     * 한 번에 뛰어 보스 여유 밴드에 계단이 생기고, 열린 뒤에는 새로 열릴 것이
     * 30레벨(전직)까지 없다.
     *
     * 5레벨 간격은 실측으로 대략 3~4스테이지다. 전직(Lv.30)과 겹치지 않는다.
     */
    public static class SkillCatalog
    {
        public const string ChainSlashId = "skill_chain";
        public const string FlashId = "skill_flash";
        public const string OniCleaveId = "skill_oni";

        /**
         * 배율과 쿨다운의 크기를 고른 근거.
         *
         *   연참 x1.26 / 7초  = 0.180   자주 터지는 잔 오의. 상한에서 평타 4대 몫
         *   일섬 x3.25 /13초  = 0.250   중간.               상한에서 평타 10대 몫
         *   귀참 x7.04 /22초  = 0.320   가장 무겁다.        상한에서 평타 23대 몫
         *
         * **쿨다운을 20초 안팎에 묶어둔 것이 먼저다.** 방치형에서 오의는 화면을
         * 흘깃 볼 때 보여야 하고, 한 스테이지가 30~60초이므로 그보다 긴 쿨다운은
         * 스테이지 하나를 통째로 지나쳐버린다. 배율은 그 쿨다운에 맞춰 뒤에
         * 정해진 값이다 - 반대 순서로 잡으면 "배율은 큰데 아무도 못 보는 스킬"이
         * 나온다.
         *
         * 합이 0.75다. 해금 시점에는 공격속도가 이미 상한(3.88)이라 스킬 하나가
         * 열릴 때 DPS의 4~5%를 맡고, 그것이 **한 칸의 체감이 0.5%를 넘는 최소
         * 크기**다(SkillCurve.CeilingRatio 주석의 산수).
         *
         * 상한(x3.2)에서 합이 2.40이 되어 공격속도 상한 3.88 아래에 머문다.
         */
        public static readonly SkillSpec[] Skills =
        {
            new SkillSpec {
                Id = ChainSlashId, DisplayName = "연참",
                BaseMultiplier = 1.26d, CooldownSeconds = 7d,
                UnlockLevel = 10, UnlockStage = 8,
                IconFile = "Icon076",           // 붉은 타일 + 흰 삼연 참격
                SlashRgba = 0xA8D8FFFFu,        // 창백한 청백 (평타 #FFF4D6 과 거리 0.39)
                Shape = SkillShape.MultiHit, HitCount = 3, Weight = 0
            },
            new SkillSpec {
                Id = FlashId, DisplayName = "일섬",
                BaseMultiplier = 3.25d, CooldownSeconds = 13d,
                UnlockLevel = 15, UnlockStage = 15,
                IconFile = "Icon140",           // 어두운 타일 + 붉은 단발 참격
                SlashRgba = 0xFF4A3AFFu,        // 선명한 적 (처치 #FF8A7A 과 거리 0.36)
                Shape = SkillShape.Pierce, HitCount = 1, Weight = 1
            },
            new SkillSpec {
                Id = OniCleaveId, DisplayName = "귀참",
                BaseMultiplier = 7.04d, CooldownSeconds = 22d,
                UnlockLevel = 20, UnlockStage = 21,
                IconFile = "Icon118",           // 오니 뿔
                SlashRgba = 0xFF9500FFu,        // 깊은 호박빛 금 (치명타 #FFD34D 과 거리 0.39)
                Shape = SkillShape.Screen, HitCount = 1, Weight = 2
            }
        };

        /**
         * @brief MultiHit에서 index번째 타격이 받는 배율.
         *
         * **마지막 타격이 나머지를 받는다.** 총 배율을 N으로 나눠 N번 더하면
         * 부동소수점에서 원래 값과 미세하게 어긋나는데, 그러면 "총 데미지 불변"을
         * 검사할 수가 없다 - 오차인지 설계 변경인지 구분되지 않는다.
         *
         * 나눗셈을 한 번만 하고 마지막에서 빼면 합이 **정확히** 원래 배율이다.
         *
         * @param hitIndex 0부터. HitCount - 1 이 마지막
         */
        public static double HitDamageShare(int skillIndex, int hitIndex, double totalMultiplier)
        {
            if (skillIndex < 0 || skillIndex >= Skills.Length) return totalMultiplier;

            var spec = Skills[skillIndex];
            int hits = spec.Shape == SkillShape.MultiHit ? Math.Max(1, spec.HitCount) : 1;
            if (hits <= 1) return totalMultiplier;

            double share = totalMultiplier / hits;
            if (hitIndex < hits - 1) return share;

            // 마지막: 앞선 것들을 빼서 합을 정확히 맞춘다
            return totalMultiplier - share * (hits - 1);
        }

        /** 이 오의가 한 번 시전될 때 나가는 타격 수 (단일 대상 기준) */
        public static int HitsPerCast(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= Skills.Length) return 1;
            var spec = Skills[skillIndex];
            return spec.Shape == SkillShape.MultiHit ? Math.Max(1, spec.HitCount) : 1;
        }

        public static int Count { get { return Skills.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Skills.Length; i++)
                if (Skills[i].Id == id) return i;
            return -1;
        }

        /** 이 캐릭터 레벨에서 열려 있는가 */
        public static bool IsUnlockedAt(int index, int characterLevel)
        {
            if (index < 0 || index >= Skills.Length) return false;
            return characterLevel >= Skills[index].UnlockLevel;
        }

        /** 첫 스킬이 열리는 레벨. 하단 "스킬" 버튼이 켜지는 조건이다 */
        public static int PanelUnlockLevel
        {
            get
            {
                int lowest = int.MaxValue;
                foreach (var skill in Skills)
                    if (skill.UnlockLevel < lowest) lowest = skill.UnlockLevel;
                return lowest == int.MaxValue ? 1 : lowest;
            }
        }

        /** 이 레벨에서 이 스킬의 초당 환산 기여. 잠겨 있으면 0 */
        public static double RateAt(int index, int skillLevel, int characterLevel)
        {
            if (!IsUnlockedAt(index, characterLevel)) return 0d;

            var spec = Skills[index];
            if (spec.CooldownSeconds <= 0d) return 0d;

            return SkillCurve.CappedMultiplierAtLevel(spec.BaseMultiplier, skillLevel)
                   / spec.CooldownSeconds;
        }

        /**
         * @brief 지금 스킬 전체가 만드는 초당 환산 공격 횟수.
         *
         * CombatStats가 공격속도에 **더하는** 값이다. 자동 공격 한 대가 공격력
         * x1이고 스킬 한 번이 공격력 x배율이므로, 쿨다운으로 나누면 두 값이
         * 같은 단위가 된다 - 그래서 더할 수 있다.
         *
         * 치명타와 스탯 포인트 증폭은 여기 들어가지 않는다. 곱해지는 자리가
         * 괄호 밖(공격력, 치명타 계수)이라 자동으로 상속된다. 지시가 요구한
         * "치명타·증폭 상속"이 구조에서 나온다.
         */
        public static double CastRate(int[] skillLevels, int characterLevel)
        {
            if (skillLevels == null) return 0d;

            double rate = 0d;
            int count = Math.Min(skillLevels.Length, Skills.Length);
            for (int i = 0; i < count; i++)
                rate += RateAt(i, skillLevels[i], characterLevel);

            return rate;
        }
    }
}
