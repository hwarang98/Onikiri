using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 전직(사무라이 진화). 현재 티어 하나를 들고 있다.
     *
     * ## 왜 UpgradeSystem에 넣지 않았는가
     *
     * 장비(EquipmentSystem)와 같은 이유다 - **재화가 둘**(보석 + 골드)이라
     * "골드를 받고 스탯을 올린다"는 UpgradeSystem의 한 줄 설명을 깨고, 화면에서
     * 두 재화가 같은 목록에 서게 된다.
     *
     * ## 진화이지 리셋이 아니다
     *
     * 티어업은 스탯·진행·재화를 아무것도 건드리지 않는다. 배수 하나가 오르고
     * 스프라이트가 바뀔 뿐이다. 환생(프레스티지)은 별도 후속 스텝이고, 그때도
     * 이 시스템이 아니라 자기 시스템으로 온다 - "다 잃고 더 빨리 다시 오르기"와
     * "쌓은 것 위에 한 칸 더"는 화면에서 같은 버튼처럼 보이면 안 되는 두 계약이다.
     *
     * ## 배수가 도달하는 곳
     *
     * UpgradeSystem.Apply가 읽어 간다(CurrentAttackMultiplier / CurrentHealthMultiplier).
     * 무기·스탯 증폭과 같은 곱 자리이고, 그래서 스킬 데미지에도 상속된다.
     */
    public sealed class EvolutionSystem : MonoBehaviour
    {
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private GemWallet gems;
        [SerializeField] private CharacterLevel character;

        [Tooltip("현재 티어. 0 = 로닌")]
        [SerializeField] private int tier;

        /** 티어·잔액이 바뀌면 발생. 화면과 배지, 외형(EvolutionAppearance)이 듣는다 */
        public event Action Changed;

        /**
         * @brief 티어가 **올랐을 때만** 발생. 진화 연출이 듣는다.
         *
         * Changed와 나눈 이유는 Changed가 세이브 복원에서도 발생하기 때문이다.
         * 복원은 상태 동기화이지 사건이 아니다 - 접속할 때마다 진화 연출이
         * 터지면 연출은 축하가 아니라 로딩 화면이 된다.
         */
        public event Action<int> Evolved;

        public static EvolutionSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second EvolutionSystem appeared; keeping the first.");
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // 티어 0의 배수가 1배라 여기서 아무것도 바뀌지 않는다. 그래도 부르는
            // 이유는 세이브가 먼저 복원된 경우 그 값이 스탯에 도달해 있어야
            // 하기 때문이다 - EquipmentSystem.Start와 같은 처리다
            ApplyToStats();
            Raise();
        }

        // ---------------------------------------------------------------- 상태

        public int Tier { get { return Mathf.Clamp(tier, 0, EvolutionCurve.MaxTier); } }

        /** 현재 티어의 이름. 화면 머리글이 쓴다 */
        public string TierName { get { return EvolutionCatalog.NameOf(Tier); } }

        /** 다음 티어의 이름. 상한이면 현재 이름 */
        public string NextTierName { get { return EvolutionCatalog.NameOf(Tier + 1); } }

        /**
         * @brief 해금됐는가. **캐릭터 레벨로 잠근다. 스테이지가 아니다.**
         *
         * 대장간(스테이지)과 반대인 이유는 EvolutionCurve.UnlockLevel 주석에
         * 있다 - 전직은 지역의 랜드마크가 아니라 캐릭터 자신의 성장이다.
         */
        public bool IsUnlocked
        {
            get
            {
                if (character == null) character = CharacterLevel.Instance;
                return character != null && EvolutionCurve.IsUnlockedAt(character.Level);
            }
        }

        /** 마지막 티어인가. 화면이 MASTER 대신 "최종 진화"로 그린다 */
        public bool IsMaxTier { get { return Tier >= EvolutionCurve.MaxTier; } }

        // ---------------------------------------------------------------- 값

        public double AttackMultiplier { get { return EvolutionCurve.AttackMultiplierAt(Tier); } }
        public double HealthMultiplier { get { return EvolutionCurve.HealthMultiplierAt(Tier); } }

        /** 다음 티어의 배수. 화면의 "전 -> 후"가 쓴다 */
        public double NextAttackMultiplier { get { return EvolutionCurve.AttackMultiplierAt(Tier + 1); } }
        public double NextHealthMultiplier { get { return EvolutionCurve.HealthMultiplierAt(Tier + 1); } }

        /** 씬에 EvolutionSystem이 없으면 1. UpgradeSystem.Apply가 참조 없이 읽어 간다 */
        public static double CurrentAttackMultiplier
        {
            get { return Instance != null ? Instance.AttackMultiplier : 1d; }
        }

        public static double CurrentHealthMultiplier
        {
            get { return Instance != null ? Instance.HealthMultiplier : 1d; }
        }

        // ---------------------------------------------------------------- 비용

        public int GemCostNow { get { return EvolutionCurve.GemCost(Tier); } }

        public BigDouble GoldCostNow
        {
            get { return BigDouble.FromDouble(EvolutionCurve.GoldCost(Tier)); }
        }

        // ---------------------------------------------------------------- 구매

        /** 다음 티어가 남아 있고 해금됐는가. 재화는 보지 않는다 */
        public bool CanEvolve
        {
            get { return IsUnlocked && EvolutionCurve.CanEvolve(Tier); }
        }

        /**
         * @brief 지금 진화 버튼이 실제로 눌리는가. **진입 탭 배지가 본다.**
         *
         * 장비 배지와 같은 규칙 - 배지는 "가서 할 일이 있다"는 뜻이므로 재화까지
         * 본다. 골드와 보석 **둘 다** 듣는다.
         */
        public bool AnyAffordable
        {
            get
            {
                if (!CanEvolve) return false;

                var wallet = PlayerWallet.Instance;
                if (gems == null) gems = GemWallet.Instance;

                return wallet != null && wallet.CanAfford(GoldCostNow)
                    && gems != null && gems.CanAfford(GemCostNow);
            }
        }

        /**
         * @brief 진화. **보석 + 골드다.**
         *
         * 골드를 먼저 빼고 보석이 실패하면 골드를 되돌린다 - 장비 등급업
         * (EquipmentSystem.TryUpgradeGrade)과 같은 순서, 같은 이유다. 재화가
         * 조용히 사라지는 경로를 남기지 않는다.
         */
        public bool TryEvolve()
        {
            if (!CanEvolve) return false;

            if (gems == null) gems = GemWallet.Instance;

            int gemCost = GemCostNow;
            var goldCost = GoldCostNow;

            var wallet = PlayerWallet.Instance;
            if (wallet == null || !wallet.CanAfford(goldCost)) return false;
            if (gems == null || !gems.CanAfford(gemCost)) return false;

            if (!wallet.TrySpend(goldCost)) return false;
            if (!gems.TrySpend(gemCost))
            {
                wallet.Add(goldCost);
                return false;
            }

            tier = Tier + 1;

            ApplyToStats();

            // 연출이 스탯 반영 **뒤**다. 연출 중에 화면의 DPS가 이미 올라 있어야
            // "진화가 실제로 무언가 했다"가 그 자리에서 보인다
            var evolved = Evolved;
            if (evolved != null) evolved(tier);

            Raise();
            return true;
        }

        /**
         * @brief 전직 배수를 전투 스탯에 다시 먹인다.
         *
         * 곱셈이라 자기 자리에 저장되지 않고 강화 값 위에 얹히므로, 강화 적용을
         * 통째로 다시 돌린다 - EquipmentSystem.ApplyToStats와 같은 판단이다.
         */
        private void ApplyToStats()
        {
            if (upgrades == null) upgrades = UpgradeSystem.Instance;
            if (upgrades != null) upgrades.ApplyAll();
        }

        // ---------------------------------------------------------------- 세이브

        public int CollectTier()
        {
            return Tier;
        }

        /**
         * @brief 세이브 복원.
         *
         * 위는 상한에서 자른다 - 티어는 화면에 **이름으로** 뜨므로 표에 없는
         * 티어가 들어오면 이름이 없다(장비 등급 복원과 같은 이유). 아래는 0에서
         * 자른다 - 음수 티어는 어떤 화면에서도 뜻이 없다.
         *
         * Evolved는 부르지 않는다. 복원은 사건이 아니다(Evolved 주석).
         */
        public void Restore(int savedTier)
        {
            tier = Mathf.Clamp(savedTier, 0, EvolutionCurve.MaxTier);

            ApplyToStats();
            Raise();
        }

        // ---------------------------------------------------------------- 테스트 패널

        /**
         * @brief 티어를 직접 놓는다. **테스트 패널 전용.**
         *
         * 재화를 무시한다. 연출(Evolved)도 부른다 - 이 치트의 절반은 진화
         * 연출을 눈으로 확인하는 용도이기 때문이다. 내릴 때는 부르지 않는다.
         */
        public void DebugSetTier(int value)
        {
            int before = Tier;
            tier = Mathf.Clamp(value, 0, EvolutionCurve.MaxTier);

            ApplyToStats();

            if (tier > before)
            {
                var evolved = Evolved;
                if (evolved != null) evolved(tier);
            }

            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
