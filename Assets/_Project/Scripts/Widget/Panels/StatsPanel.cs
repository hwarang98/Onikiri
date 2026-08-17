using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 캐릭터 종합 스탯 창 (37단계). 읽기 전용.
     *
     * 스탯이 강화/성장/장비/전직/동료 다섯 화면에 흩어져 있어서, "지금 내
     * 공격력이 얼마인가"에 답하는 화면이 없었다. 여기는 **합쳐진 최종 값**만
     * 보여준다 - 올리는 것은 각 화면이 하고, 여기서는 아무것도 사지 않는다.
     *
     * ## 값의 출처는 기존 계산 경로 그대로다
     *
     * PlayerCombat/PlayerHealth의 프로퍼티는 UpgradeSystem.Apply가 증폭·장비·
     * 전직을 **이미 곱해 넣은** 최종값이다. 여기서 배수를 다시 곱하면 화면과
     * 실제 전투가 갈린다 - 그래서 이 컴포넌트에는 곱셈이 두 곳뿐이다:
     * 동료 몫(스탯이 아니라 별도 전투체라 합산 DPS에만 곱한다. PetCombat이
     * ExpectedDps x 보너스로 때리는 것과 같은 식)과 백분율 표기 변환.
     *
     * ## 폴링인 이유
     *
     * PlayerCombat에는 Changed 이벤트가 없다(매 타마다 값이 바뀌는 것도
     * 아니고, 지금까지 아무도 구독할 일이 없었다). 이 창이 열려 있는 동안만
     * 1초에 네 번 다시 읽는다 - 라벨 아홉 개라 비용이 없다.
     */
    public sealed class StatsPanel : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;

        [Header("최종 값")]
        [Tooltip("경험치 수치. 얇은 스트립(2a 후속)에는 숫자가 없어서, " +
                 "정확한 값이 필요한 사람은 레벨 칩을 눌러 여기서 본다")]
        [SerializeField] private TMP_Text expValue;
        [SerializeField] private TMP_Text damageValue;
        [SerializeField] private TMP_Text attackSpeedValue;
        [SerializeField] private TMP_Text critValue;
        [SerializeField] private TMP_Text healthValue;
        [SerializeField] private TMP_Text regenValue;
        [SerializeField] private TMP_Text goldGainValue;
        [SerializeField] private TMP_Text petValue;
        [SerializeField] private TMP_Text dpsValue;

        [Tooltip("공격력에 곱해져 있는 배수의 출처별 내역")]
        [SerializeField] private TMP_Text multiplierDetail;

        private float nextRefresh;

        private void OnEnable()
        {
            // 꺼진 채 저장되는 판이라 Start는 첫 활성화 다음에야 돈다.
            // 열리는 순간의 화면이 옛 값이면 안 된다 (LockedTab과 같은 함정)
            Refresh();
            nextRefresh = Time.unscaledTime + 0.25f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            if (combat == null) return;

            double petBonus = PetSystem.CurrentTotalBonus;
            double skillRate = SkillSystem.CurrentCastRate;
            var character = CharacterLevel.Instance;

            if (expValue != null && character != null)
                expValue.text = "경험치  " + NumberFormatter.Format(character.Exp)
                                + " / " + NumberFormatter.Format(character.ExpRequired);

            if (damageValue != null)
                damageValue.text = NumberFormatter.Format(combat.Damage);

            if (attackSpeedValue != null)
            {
                string speed = combat.AttacksPerSecond.ToString("F2") + "/초";
                // 오의 자동 시전은 공격 횟수로 환산돼 더해진다(EffectiveAttacksPerSecond).
                // 기본값과 합쳐 적으면 "강화로 올린 속도"가 화면에서 사라진다
                if (skillRate > 0d) speed += "  +오의 " + skillRate.ToString("F2");

                // 영체도 같은 자리에 더해진다(PlayerCombat.EffectiveAttacksPerSecond).
                // 오의와 나눠 적는 이유는 나눠 둔 이유와 같다 - 한 숫자로
                // 합치면 어느 쪽이 세졌는지 화면에서 읽히지 않는다
                double spiritRate = YodoSystem.CurrentSpiritRate;
                if (spiritRate > 0d) speed += "  +영체 " + spiritRate.ToString("F2");

                attackSpeedValue.text = speed;
            }

            if (critValue != null)
                critValue.text = (combat.CritChance * 100f).ToString("F1") + "%  x"
                                 + combat.CritMultiplier.ToString("F2");

            if (health != null && healthValue != null)
                healthValue.text = NumberFormatter.Format(health.MaxHealth);

            if (health != null && regenValue != null)
                regenValue.text = NumberFormatter.Format(health.RegenPerSecond) + "/초";

            if (goldGainValue != null)
                goldGainValue.text = "x" + UpgradeSystem.CurrentGoldGain.ToString("F2");

            if (petValue != null)
                petValue.text = "+" + (petBonus * 100d).ToString("F0") + "%";

            // 동료 몫을 곱한 값이 시뮬레이션의 CombatStats.PetFactor와 같은 식이다.
            // combat.ExpectedDps에는 동료가 없다 - 동료는 스탯이 아니라 별도 전투체다
            if (dpsValue != null)
                dpsValue.text = NumberFormatter.Format(
                    combat.ExpectedDps * BigDouble.FromDouble(1d + petBonus));

            if (multiplierDetail != null)
            {
                double amp = character != null ? character.AttackMultiplier : 1d;
                double equip = EquipmentSystem.CurrentMultiplierFor(EquipmentStat.AttackPower);
                double evolution = EvolutionSystem.CurrentAttackMultiplier;

                multiplierDetail.text = "공격 배수  증폭 x" + amp.ToString("F2")
                                        + " · 장비 x" + equip.ToString("F2")
                                        + " · 전직 x" + evolution.ToString("F2");
            }
        }
    }
}
