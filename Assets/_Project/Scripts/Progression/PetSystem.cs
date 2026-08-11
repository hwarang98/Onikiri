using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 동료(펫). 셋의 해금·레벨을 들고 있다. 보유 동료 전원이 출전한다.
     *
     * ## 왜 UpgradeSystem에 넣지 않았는가
     *
     * 장비·전직과 같은 이유다 - **재화가 둘**(해금 보석 / 레벨 골드)이라
     * "골드를 받고 스탯을 올린다"는 UpgradeSystem의 한 줄 설명을 깬다. 다만
     * 장비(한 버튼에 보석+골드)보다 분리가 강하다 - 해금 버튼에는 보석만,
     * 레벨 버튼에는 골드만 든다. 화면에서 두 재화가 같은 버튼에 서지 않는다.
     *
     * ## 스탯에 곱하지 않는다 - 전투원들이다
     *
     * 전직·장비 배수는 UpgradeSystem.Apply를 지나 플레이어 스탯에 곱해지지만,
     * 동료의 기여는 화면의 **별도 공격자들**(PetCombat 하나씩)이 실제 타격으로
     * 낸다. 각자의 한 타 = 자기 보너스 x 플레이어 기대 DPS x 자기 공격
     * 간격이라, 초당 기여의 합이 정확히 시뮬레이션의 (1 + 합산 보너스) 축과
     * 같다(PetCurve 주석). 그래서 ApplyToStats가 없다 - 여기 값이 바뀌면
     * 각 PetCombat이 Changed를 듣고 다음 타부터 새 크기로 때린다.
     *
     * ## 보유 동료 전원이 출전한다
     *
     * 처음의 "액티브 슬롯 하나"를 뒤집었다 - 동료답게 팀으로 싸운다. 슬롯
     * 제한/추가 동료 판매는 과금 훅으로 다음에 오고, 그날 합산 몫의 분배
     * (PetCatalog)를 다시 잰다.
     */
    public sealed class PetSystem : MonoBehaviour
    {
        /**
         * @brief 펫 하나. 값은 빌더가 카탈로그에서 옮겨 적는다.
         *
         * EquipmentSystem.Slot과 같은 규칙이다 - 컴포넌트가 이미 씬에 있으면
         * 스크립트 기본값을 고쳐도 반영되지 않으므로, 빌더가 단일 출처로서
         * 씬에 명시적으로 기록하고 테스트가 카탈로그와 같은지 검사한다.
         */
        [Serializable]
        public sealed class Slot
        {
            public string id;

            [Tooltip("화면에 뜨는 이름")]
            public string petName;

            [Tooltip("역할 한 줄 (근접/원거리/강타)")]
            public string role;

            [Tooltip("해금됐는가")]
            public bool unlocked;

            [Tooltip("현재 레벨. 1부터. 잠긴 동료도 1이다")]
            public int level = 1;

            [Tooltip("해금 보석 값. 빌더가 카탈로그에서 옮겨 적는다")]
            public int unlockGems;

            [Tooltip("공격 간격 (초). 한 타 크기 = 보너스 x 플레이어 DPS x 간격")]
            public double attackIntervalSeconds = 1d;

            [Tooltip("Lv.1의 DPS 보너스. 빌더가 카탈로그에서 옮겨 적는다")]
            public double firstBonus = 0.05d;

            [Tooltip("보너스 상한. 셋의 합이 예약 몫(0.5)이다")]
            public double bonusCeiling = 0.1d;

            [Tooltip("원거리인가 (궁수만 true)")]
            public bool ranged;
        }

        [SerializeField] private GemWallet gems;
        [SerializeField] private StageProgress stage;

        [SerializeField] private Slot[] pets;

        /** 해금·레벨 중 무엇이든 바뀌면 발생. 화면과 배지, PetCombat들이 듣는다 */
        public event Action Changed;

        /**
         * @brief 펫이 **처음 해금됐을 때만** 발생. 등장 연출이 듣는다.
         *
         * Changed와 나눈 이유는 EvolutionSystem.Evolved와 같다 - Changed는
         * 세이브 복원에서도 발생하고, 복원은 상태 동기화이지 사건이 아니다.
         */
        public event Action<int> Unlocked;

        public static PetSystem Instance { get; private set; }

        public int PetCount { get { return pets != null ? pets.Length : 0; } }

        public Slot GetPet(int index)
        {
            if (pets == null || index < 0 || index >= pets.Length) return null;
            return pets[index];
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second PetSystem appeared; keeping the first.");
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
            // 잠긴 상태의 기여가 0이라 여기서 아무것도 바뀌지 않는다. 그래도
            // 부르는 이유는 세이브가 먼저 복원된 경우 각 PetCombat이 자기
            // 동료를 이미 세워야 하기 때문이다 - EquipmentSystem.Start와 같다
            Raise();
        }

        // ---------------------------------------------------------------- 해금 게이트

        /**
         * @brief 펫 화면이 열려 있는가. **스테이지로 잠근다. 레벨이 아니다.**
         *
         * 대장간(st11)과 같은 규칙이다 - 동료는 캐릭터 자신의 성장이 아니라
         * 여정에서 만나는 존재이고, 지역 3 피날레(st30)를 넘긴 다음 칸(st31)에서
         * 합류한다. 이 값이 가속 구간의 첫 칸인 것이 코리더 불변의 구조적
         * 보장이다(PetCurve.UnlockStage 주석).
         */
        public bool IsUnlocked
        {
            get { return PetCurve.IsUnlockedAt(StageNow); }
        }

        private int StageNow
        {
            // 현재 스테이지가 아니라 최전선이다(37단계 재선택). 재선택으로
            // st31 아래에 내려간 순간 출전 중인 동료가 화면에서 사라지면 안 된다
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        // ---------------------------------------------------------------- 출전

        /**
         * @brief 출전 중인 동료들의 합산 보너스 (0~0.5). 화면 요약이 읽는다.
         *
         * 해금 안 된 동료의 몫은 0이다 - 시뮬레이션의 합산과 같은 식.
         */
        public double TotalBonus
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < PetCount; i++)
                {
                    var pet = pets[i];
                    if (pet != null && pet.unlocked) total += BonusOf(i);
                }
                return total;
            }
        }

        /** 씬에 PetSystem이 없으면 0. 화면 요약이 참조 없이 읽어 간다 */
        public static double CurrentTotalBonus
        {
            get { return Instance != null ? Instance.TotalBonus : 0d; }
        }

        /**
         * @brief 이 동료가 출전 중일 때의 보너스. PetCombat이 타격마다 읽는다.
         *
         * 잠겨 있으면 0 - 각 전투체가 자기 동료의 해금 여부를 이 값 하나로
         * 판정할 수 있게 한다.
         */
        public double FieldedBonusOf(int index)
        {
            var pet = GetPet(index);
            if (pet == null || !pet.unlocked) return 0d;
            return BonusOf(index);
        }

        // ---------------------------------------------------------------- 값

        /** 이 동료의 보너스 곡선값 (해금 여부와 무관). 화면이 쓴다 */
        public double BonusOf(int index)
        {
            var pet = GetPet(index);
            if (pet == null) return 0d;
            return PetCurve.BonusAt(pet.firstBonus, pet.bonusCeiling, pet.level);
        }

        /** 레벨을 한 칸 올렸을 때의 보너스. 화면의 "전 -> 후"가 쓴다 */
        public double NextBonusOf(int index)
        {
            var pet = GetPet(index);
            if (pet == null) return 0d;
            return PetCurve.BonusAt(pet.firstBonus, pet.bonusCeiling, pet.level + 1);
        }

        // ---------------------------------------------------------------- 비용

        public int UnlockGemCostOf(int index)
        {
            var pet = GetPet(index);
            return pet != null ? pet.unlockGems : 0;
        }

        public BigDouble LevelCostOf(int index)
        {
            var pet = GetPet(index);
            if (pet == null) return BigDouble.Zero;
            return BigDouble.FromDouble(PetCurve.CostAtLevel(pet.level));
        }

        // ---------------------------------------------------------------- 상태

        public bool CanUnlock(int index)
        {
            var pet = GetPet(index);
            return pet != null && IsUnlocked && !pet.unlocked;
        }

        public bool CanLevelUp(int index)
        {
            var pet = GetPet(index);
            return pet != null && IsUnlocked && pet.unlocked && PetCurve.CanLevelUp(pet.level);
        }

        /** 레벨 상한까지 갔다. 화면이 MASTER로 그린다 */
        public bool IsMaxed(int index)
        {
            var pet = GetPet(index);
            return pet != null && pet.unlocked && !PetCurve.CanLevelUp(pet.level);
        }

        /** 해금된 펫이 하나라도 있는가. 전투 등장과 화면 요약이 본다 */
        public bool AnyUnlocked
        {
            get
            {
                for (int i = 0; i < PetCount; i++)
                    if (pets[i] != null && pets[i].unlocked) return true;
                return false;
            }
        }

        /**
         * @brief 지금 누를 수 있는 버튼이 하나라도 있는가. **진입 탭 배지가 본다.**
         *
         * 장비 배지와 같은 규칙 - 배지는 "가서 할 일이 있다"는 뜻이므로 재화까지
         * 본다. 해금은 보석을, 레벨은 골드를 듣는다.
         */
        public bool AnyAffordable
        {
            get { return AffordableCount > 0; }
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
                for (int i = 0; i < PetCount; i++)
                {
                    if (CanUnlock(i) && gems != null && gems.CanAfford(UnlockGemCostOf(i))) count++;
                    if (CanLevelUp(i) && wallet != null && wallet.CanAfford(LevelCostOf(i))) count++;
                }
                return count;
            }
        }

        // ---------------------------------------------------------------- 구매

        /**
         * @brief 해금. **보석만 든다.**
         *
         * 해금 버튼을 누른 플레이어가 기대하는 것은 "동료가 걸어 들어오는
         * 것"이다 - 해금 즉시 출전하고(전원 출전), Unlocked를 들은 그 동료의
         * PetCombat이 등장 연출을 튼다.
         */
        public bool TryUnlock(int index)
        {
            if (!CanUnlock(index)) return false;

            if (gems == null) gems = GemWallet.Instance;
            if (gems == null || !gems.TrySpend(UnlockGemCostOf(index))) return false;

            pets[index].unlocked = true;

            // 연출이 상태 반영 뒤다. Unlocked를 듣는 쪽(전투 등장)이 이미
            // 해금된 상태를 읽어야 한다
            var handler = Unlocked;
            if (handler != null) handler(index);

            Raise();
            return true;
        }

        /**
         * @brief 레벨업. **골드만 든다.**
         *
         * 강화 구매 카운터(QuestSystem.ReportUpgradePurchase)를 올리지 않는다 -
         * 장비 단련과 같은 이유다. 업적 "강화 총합"이 골드를 주므로, 여기서
         * 세면 업적이 예정보다 일찍 열리고 밴드가 시뮬레이션과 갈린다.
         */
        public bool TryLevelUp(int index)
        {
            if (!CanLevelUp(index)) return false;

            var wallet = PlayerWallet.Instance;
            if (wallet == null || !wallet.TrySpend(LevelCostOf(index))) return false;

            pets[index].level++;
            Raise();
            return true;
        }

        // ---------------------------------------------------------------- 세이브

        public string[] CollectIds()
        {
            var ids = new string[PetCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = pets[i] != null ? pets[i].id : string.Empty;
            return ids;
        }

        public int[] CollectUnlocked()
        {
            var values = new int[PetCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = pets[i] != null && pets[i].unlocked ? 1 : 0;
            return values;
        }

        public int[] CollectLevels()
        {
            var values = new int[PetCount];
            for (int i = 0; i < values.Length; i++) values[i] = pets[i] != null ? pets[i].level : 1;
            return values;
        }

        /**
         * @brief 세이브 복원.
         *
         * **레벨을 자르지 않는다.** 상한이 내려간 업데이트에서 플레이어가 산
         * 레벨이 영구히 사라지면 안 된다 - 값은 PetCurve.BonusAt이 상한에서
         * 자르고, 상한이 다시 오르면 잠든 레벨이 깨어난다. EquipmentSystem.
         * Restore와 같은 규칙이다.
         *
         * savedActiveId는 **레거시다** - 단일 출전 시절의 세이브 필드
         * (SaveData.activePetId)로, 다중 출전에서는 뜻이 없어 읽지 않는다.
         * Unlocked는 부르지 않는다(복원은 사건이 아니다).
         */
        public void Restore(string[] ids, int[] unlocked, int[] levels, string savedActiveId)
        {
            if (ids != null && unlocked != null && levels != null)
            {
                int count = Mathf.Min(ids.Length, Mathf.Min(unlocked.Length, levels.Length));
                for (int i = 0; i < count; i++)
                {
                    int index = IndexOf(ids[i]);

                    // 세이브에 있지만 지금은 없는 펫은 조용히 건너뛴다.
                    // 강화 축·오의·장비 복원과 같은 안전장치다
                    if (index < 0) continue;

                    pets[index].unlocked = unlocked[i] != 0;
                    pets[index].level = Mathf.Max(1, levels[i]);
                }
            }

            Raise();
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < PetCount; i++)
                if (pets[i] != null && pets[i].id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- 테스트 패널

        /**
         * @brief 재화를 무시하고 해금한다. **테스트 패널 전용.**
         *
         * 연출(Unlocked)도 부른다 - 이 치트의 절반은 동료가 걸어 들어오는
         * 등장을 눈으로 확인하는 용도다(EvolutionSystem.DebugSetTier와 같은 결).
         */
        public void DebugUnlock(int index)
        {
            var pet = GetPet(index);
            if (pet == null || pet.unlocked) return;

            pet.unlocked = true;

            var handler = Unlocked;
            if (handler != null) handler(index);

            Raise();
        }

        /** 재화를 무시하고 레벨을 놓는다. 곡선을 눈으로 훑는 경로 */
        public void DebugSetLevel(int index, int level)
        {
            var pet = GetPet(index);
            if (pet == null) return;

            pet.level = Mathf.Clamp(level, 1, PetCurve.MaxLevel);
            Raise();
        }

        /**
         * @brief 동료를 새 게임 상태(전부 잠금, Lv.1)로 되돌린다.
         *
         * **보석과 골드는 돌려주지 않는다.** 환불은 초기화가 아니라 별개의
         * 치트이고, 둘 다 패널 위쪽에 이미 자기 버튼이 있다.
         */
        public void DebugResetPets()
        {
            for (int i = 0; i < PetCount; i++)
            {
                if (pets[i] == null) continue;
                pets[i].unlocked = false;
                pets[i].level = 1;
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
