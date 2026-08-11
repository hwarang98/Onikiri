using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 대장간. 장비 두 슬롯의 등급과 단련 레벨을 들고 있다.
     *
     * ## 왜 UpgradeSystem에 넣지 않았는가
     *
     * 장비도 골드로 사는 축이고 자도 같다(%DPS / %EHP). 그런데도 별개인 이유는
     * SkillSystem이 별개인 이유와 다르다 - 저쪽은 시간(쿨다운)이 있어서였고,
     * 이쪽은 **재화가 둘**이기 때문이다.
     *
     * UpgradeSystem의 한 줄 설명은 "골드를 받고 스탯을 올린다"이고, 그 안의
     * 일곱 축은 전부 같은 버튼 한 종류다. 여기에 보석을 받는 버튼을 섞으면
     * 그 설명이 깨지고, 더 나쁘게는 화면에서 두 재화가 같은 목록에 서게 된다 -
     * E-3이 "화면에서 둘이 섞이지 않게"라고 못 박은 것이 정확히 그 상태다.
     *
     * ## 두 슬롯은 언제나 장착 상태다
     *
     * 인벤토리가 없다. 슬롯이 곧 장비이고, 등급업은 새 물건을 얻는 것이 아니라
     * **그 자리의 물건이 바뀌는 것**이다. 그래서 "장착/해제"라는 상태가 없고,
     * 세이브에도 장착 플래그가 없다(SaveData 주석).
     *
     * 여벌 장비와 교체는 드랍 재료·제작서가 들어오는 다음 스텝의 일이다. 그때
     * 슬롯은 그대로 두고 "무엇이 꽂혀 있는가"가 늘어난다.
     */
    public sealed class EquipmentSystem : MonoBehaviour
    {
        /**
         * @brief 슬롯 하나. 값은 빌더가 카탈로그에서 옮겨 적는다.
         *
         * SkillSystem.Slot과 같은 규칙이다 - 컴포넌트가 이미 씬에 있으면
         * 스크립트 기본값을 고쳐도 반영되지 않으므로, 빌더가 단일 출처로서
         * 씬에 명시적으로 기록하고 테스트가 둘이 같은지 검사한다.
         */
        [Serializable]
        public sealed class Slot
        {
            public string id;

            [Tooltip("\"무기\" / \"방어구\". 등급 이름과 다르다")]
            public string slotName;

            [Tooltip("등급 이름 다섯. 화면에 뜨는 것은 이쪽이다")]
            public string[] gradeNames;

            [Tooltip("어느 스탯에 곱해지는가")]
            public EquipmentStat stat;

            [Tooltip("현재 등급. 1부터")]
            public int grade = 1;

            [Tooltip("현재 단련 레벨. 1부터, 등급이 상한을 연다")]
            public int level = 1;

            [Tooltip("단련 첫 칸의 비용. 빌더가 카탈로그에서 옮겨 적는다")]
            public double temperBaseCost = 100d;

            [Tooltip("단련 한 칸의 배수")]
            public double temperStep = 1.0128d;

            [Tooltip("등급 하나의 배수")]
            public double gradeStep = 1.075d;

            public double Multiplier { get { return EquipmentCurve.ValueAt(gradeStep, temperStep, grade, level); } }

            public string GradeName
            {
                get
                {
                    if (gradeNames == null || gradeNames.Length == 0) return slotName;
                    int index = Mathf.Clamp(grade - 1, 0, gradeNames.Length - 1);
                    return gradeNames[index];
                }
            }
        }

        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private GemWallet gems;
        [SerializeField] private StageProgress stage;

        [SerializeField] private Slot[] slots;

        /** 등급·레벨·잔액 중 무엇이든 바뀌면 발생. 화면과 배지가 듣는다 */
        public event Action Changed;

        public static EquipmentSystem Instance { get; private set; }

        public int SlotCount { get { return slots != null ? slots.Length : 0; } }

        public Slot GetSlot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length) return null;
            return slots[index];
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second EquipmentSystem appeared; keeping the first.");
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
            // 등급 1 · 단련 1의 배수가 1배라 여기서 아무것도 바뀌지 않는다. 그래도
            // 부르는 이유는 세이브가 먼저 복원된 경우(GameSession) 그 값이 스탯에
            // 도달해 있어야 하기 때문이다 - UpgradeSystem.Start와 같은 처리다
            ApplyToStats();
            Raise();
        }

        // ---------------------------------------------------------------- 해금

        /**
         * @brief 지금 스테이지에서 대장간이 열려 있는가.
         *
         * **스테이지로 잠근다. 레벨이 아니다.** 다른 탭은 전부 캐릭터 레벨로
         * 잠그는데 여기만 다른 이유는 EquipmentCurve.UnlockStage 주석에 있다 -
         * 대장간은 지역 1의 랜드마크이고, 화면에 서 있는 건물이 열리는 조건은
         * "그 지역을 지나왔는가"여야 말이 된다.
         */
        public bool IsUnlocked
        {
            get { return EquipmentCurve.IsUnlockedAt(StageNow); }
        }

        private int StageNow
        {
            // 현재 스테이지가 아니라 최전선이다(37단계 재선택). "그 지역을
            // 지나왔는가"는 되돌아가도 참으로 남아야 한다
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        // ---------------------------------------------------------------- 값

        /** 이 슬롯이 지금 곱하고 있는 배수 */
        public double MultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 1d;
            return slot.Multiplier;
        }

        /** 단련을 한 칸 올렸을 때의 배수. 화면의 "전 -> 후"가 쓴다 */
        public double NextTemperMultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 1d;
            return EquipmentCurve.NextTemperValue(slot.gradeStep, slot.temperStep, slot.grade, slot.level);
        }

        /** 등급을 한 칸 올렸을 때의 배수 */
        public double NextGradeMultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 1d;
            return EquipmentCurve.NextGradeValue(slot.gradeStep, slot.temperStep, slot.grade, slot.level);
        }

        /**
         * @brief 이 스탯에 곱해질 총 배수. 슬롯이 없으면 1.
         *
         * **1로 떨어지는 것이 중요하다.** 전투 전용 테스트 씬은 EquipmentSystem
         * 없이 전투만 세우는데, 거기서 0이 되면 공격력이 통째로 사라진다.
         * UpgradeSystem.CurrentGoldGain과 같은 규칙이다.
         */
        public double MultiplierFor(EquipmentStat stat)
        {
            if (slots == null) return 1d;

            double product = 1d;
            foreach (var slot in slots)
            {
                if (slot == null || slot.stat != stat) continue;
                product *= slot.Multiplier;
            }
            return product;
        }

        /** 씬에 EquipmentSystem이 없으면 1. UpgradeSystem.Apply가 참조 없이 읽어 간다 */
        public static double CurrentMultiplierFor(EquipmentStat stat)
        {
            return Instance != null ? Instance.MultiplierFor(stat) : 1d;
        }

        // ---------------------------------------------------------------- 비용

        public BigDouble TemperCostOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return BigDouble.Zero;
            return BigDouble.FromDouble(EquipmentCurve.TemperCost(slot.temperBaseCost, slot.level));
        }

        public BigDouble GradeGoldCostOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return BigDouble.Zero;
            return BigDouble.FromDouble(EquipmentCurve.GradeGoldCost(slot.temperBaseCost, slot.grade));
        }

        public int GradeGemCostOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0;
            return EquipmentCurve.GradeGemCost(slot.grade);
        }

        // ---------------------------------------------------------------- 상태

        public bool CanTemper(int index)
        {
            var slot = GetSlot(index);
            return slot != null && IsUnlocked && EquipmentCurve.CanTemper(slot.grade, slot.level);
        }

        public bool CanUpgradeGrade(int index)
        {
            var slot = GetSlot(index);
            return slot != null && IsUnlocked && EquipmentCurve.CanUpgradeGrade(slot.grade, slot.level);
        }

        /** 등급도 단련도 끝까지 갔다. 화면이 MASTER로 그린다 */
        public bool IsMaxed(int index)
        {
            var slot = GetSlot(index);
            return slot != null && !EquipmentCurve.CanTemper(slot.grade, slot.level)
                                && !EquipmentCurve.CanUpgradeGrade(slot.grade, slot.level);
        }

        /**
         * @brief 지금 누를 수 있는 버튼이 하나라도 있는가. **진입 탭 배지가 본다.**
         *
         * 재화가 충분한 것만으로는 부족하다. 성장 탭·퀘스트 탭의 배지 규칙과
         * 같다 - 배지는 "가서 할 일이 있다"는 뜻이고, 눌러도 아무것도 안
         * 바뀌는 버튼이 있는 화면은 할 일이 있는 화면이 아니다.
         */
        public bool AnyAffordable
        {
            get
            {
                if (!IsUnlocked) return false;

                var wallet = PlayerWallet.Instance;
                if (gems == null) gems = GemWallet.Instance;

                for (int i = 0; i < SlotCount; i++)
                {
                    if (CanTemper(i) && wallet != null && wallet.CanAfford(TemperCostOf(i))) return true;

                    if (CanUpgradeGrade(i)
                        && wallet != null && wallet.CanAfford(GradeGoldCostOf(i))
                        && gems != null && gems.CanAfford(GradeGemCostOf(i))) return true;
                }
                return false;
            }
        }

        /** 배지에 적는 개수. 지금 누를 수 있는 버튼 수다 */
        public int AffordableCount
        {
            get
            {
                if (!IsUnlocked) return 0;

                var wallet = PlayerWallet.Instance;
                if (gems == null) gems = GemWallet.Instance;

                int count = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (CanTemper(i) && wallet != null && wallet.CanAfford(TemperCostOf(i))) count++;

                    if (CanUpgradeGrade(i)
                        && wallet != null && wallet.CanAfford(GradeGoldCostOf(i))
                        && gems != null && gems.CanAfford(GradeGemCostOf(i))) count++;
                }
                return count;
            }
        }

        // ---------------------------------------------------------------- 구매

        /**
         * @brief 단련. **골드만 든다.**
         *
         * 강화 구매 카운터(QuestSystem.ReportUpgradePurchase)를 올리지 않는다.
         * 일일 퀘스트 "강화 15회 구매"와 업적 "강화 총합 50/150"이 그 카운터를
         * 읽는데, 업적 쪽은 **골드를 주므로 보스 여유 밴드를 건드린다**. 장비를
         * 세면 업적이 예정보다 일찍 열리고 밴드가 시뮬레이션과 갈린다.
         *
         * 그래서 화면에서도 이 버튼을 "강화"라고 부르지 않는다 - 단련이다.
         * 이름이 같으면 세지 않는 것이 화면에서 거짓말이 된다.
         */
        public bool TryTemper(int index)
        {
            if (!CanTemper(index)) return false;

            var wallet = PlayerWallet.Instance;
            if (wallet == null || !wallet.TrySpend(TemperCostOf(index))) return false;

            slots[index].level++;

            ApplyToStats();
            Raise();
            return true;
        }

        /**
         * @brief 등급업. **보석 + 골드다.**
         *
         * 보석을 먼저 확인하고 골드를 먼저 뺀다. 순서가 중요하다 - 골드를 뺀
         * 뒤 보석이 모자라면 골드만 사라진다. 두 지갑을 건드리는 유일한 자리라
         * 여기 한 곳만 지키면 된다.
         */
        public bool TryUpgradeGrade(int index)
        {
            if (!CanUpgradeGrade(index)) return false;

            if (gems == null) gems = GemWallet.Instance;

            int gemCost = GradeGemCostOf(index);
            var goldCost = GradeGoldCostOf(index);

            var wallet = PlayerWallet.Instance;
            if (wallet == null || !wallet.CanAfford(goldCost)) return false;
            if (gems == null || !gems.CanAfford(gemCost)) return false;

            if (!wallet.TrySpend(goldCost)) return false;
            if (!gems.TrySpend(gemCost))
            {
                // 여기 오면 두 확인 사이에 잔액이 바뀐 것이다. 골드를 되돌린다 -
                // 재화가 조용히 사라지는 경로를 남기지 않는다
                wallet.Add(goldCost);
                return false;
            }

            slots[index].grade++;

            ApplyToStats();
            Raise();
            return true;
        }

        /**
         * @brief 장비 배수를 전투 스탯에 다시 먹인다.
         *
         * 장비는 곱셈이라 자기 자리에 저장되지 않고 강화 값 위에 얹힌다. 그래서
         * 등급/레벨이 바뀌면 강화 적용을 통째로 다시 돌리는 것이 가장 단순하다 -
         * 스탯이 반영되는 경로가 UpgradeSystem 하나로 남는다.
         * CharacterLevel.ApplyToStats와 같은 판단이고 같은 이유다.
         */
        private void ApplyToStats()
        {
            if (upgrades == null) upgrades = UpgradeSystem.Instance;
            if (upgrades != null) upgrades.ApplyAll();
        }

        // ---------------------------------------------------------------- 세이브

        public string[] CollectIds()
        {
            var ids = new string[SlotCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = slots[i] != null ? slots[i].id : string.Empty;
            return ids;
        }

        public int[] CollectGrades()
        {
            var values = new int[SlotCount];
            for (int i = 0; i < values.Length; i++) values[i] = slots[i] != null ? slots[i].grade : 1;
            return values;
        }

        public int[] CollectLevels()
        {
            var values = new int[SlotCount];
            for (int i = 0; i < values.Length; i++) values[i] = slots[i] != null ? slots[i].level : 1;
            return values;
        }

        /**
         * @brief 세이브 복원.
         *
         * **레벨을 자르지 않는다.** UpgradeTrack.SetLevel·SkillSystem과 같은
         * 규칙이다 - 등급 상한이 내려간 업데이트에서 플레이어가 산 레벨이 영구히
         * 사라지면 안 된다. 값은 EquipmentCurve.ValueAt이 자르고, 등급이 다시
         * 오르면 잠든 레벨이 깨어난다.
         *
         * 등급은 반대로 자른다. 등급은 화면에 **이름으로** 뜨므로(오니키리 등)
         * 표에 없는 등급이 들어오면 이름이 없다. 값이 아니라 표시가 깨지는
         * 자리라 여기서 막는다.
         */
        public void Restore(string[] ids, int[] grades, int[] levels)
        {
            if (ids != null && grades != null && levels != null)
            {
                int count = Mathf.Min(ids.Length, Mathf.Min(grades.Length, levels.Length));
                for (int i = 0; i < count; i++)
                {
                    int index = IndexOf(ids[i]);

                    // 세이브에 있지만 지금은 없는 슬롯은 조용히 건너뛴다.
                    // 강화 축·오의 복원과 같은 안전장치다
                    if (index < 0) continue;

                    slots[index].grade = Mathf.Clamp(grades[i], 1, EquipmentCurve.GradeCount);
                    slots[index].level = Mathf.Max(1, levels[i]);
                }
            }

            ApplyToStats();
            Raise();
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] != null && slots[i].id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- 테스트 패널

        /**
         * @brief 장비를 새 게임 상태(1등급 Lv.1)로 되돌린다. **테스트 패널 전용.**
         *
         * 세이브 삭제로는 안 되는 이유가 다른 축들과 같다 - 그것은 스테이지까지
         * 지우는데, 스테이지가 1로 돌아가면 대장간이 잠겨서 살 수조차 없다.
         *
         * **골드와 보석은 돌려주지 않는다.** 환불은 초기화가 아니라 별개의
         * 치트이고, 둘 다 패널 위쪽에 이미 자기 버튼이 있다.
         */
        public void DebugResetEquipment()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null) continue;
                slots[i].grade = 1;
                slots[i].level = 1;
            }

            ApplyToStats();
            Raise();
        }

        /** 재화를 무시하고 한 칸 올린다. 곡선을 눈으로 훑는 경로 */
        public void DebugAdvance(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return;

            if (EquipmentCurve.CanTemper(slot.grade, slot.level)) slot.level++;
            else if (EquipmentCurve.CanUpgradeGrade(slot.grade, slot.level)) slot.grade++;
            else return;

            ApplyToStats();
            Raise();
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
