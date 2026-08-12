using System;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 발도 오의 셋을 들고 있고, 쿨다운마다 스스로 시전한다.
     *
     * ## 왜 UpgradeSystem에 넣지 않았는가
     *
     * 스킬도 골드로 사는 축이고 효율도 같은 자로 잰다. 그런데도 별개인 이유는
     * 두 가지다.
     *
     * **하나. 스킬에는 시간이 있다.** 여섯 축은 사면 끝이지만 오의는 쿨다운을
     * 돌리고 사거리를 보고 시전한다 - `Update`가 필요하다. UpgradeSystem은
     * 프레임과 무관한 순수 계산 덩어리이고, 거기에 타이머가 들어오면 "강화를
     * 적용하는 곳"이라는 한 줄 설명이 깨진다.
     *
     * **둘. 화면이 다르다.** 18단계에 성장 패널의 탭 줄이 "캐릭터 성장"으로
     * 정리되면서 스킬만 하단에 남았고, 그때 이미 별개 시스템으로 결정됐다
     * (BattleContentBuilder.LockedTabs 주석).
     *
     * ## 자동 시전
     *
     * 쿨다운이 차면 알아서 나간다. 방치형에서 "눌러야 나가는 오의"는 화면을
     * 보는 사람에게만 주는 보상이라, 자리를 비우는 플레이어의 실제 DPS가
     * 시뮬레이션과 갈린다.
     *
     * **쿨다운은 벨 것이 있을 때만 돈다.** 이유가 둘이다 - 보스에게 달려가는
     * 5.3초 동안 쿨다운이 차면 도착하자마자 오의가 몰아치고(제한 시간 30초의
     * 밸런스가 무너진다), 시뮬레이션은 "때릴 수 있는 시간"으로만 DPS를 재므로
     * 대기 중에 도는 쿨다운은 계산에 없는 이득이 된다.
     */
    public sealed class SkillSystem : MonoBehaviour
    {
        /**
         * @brief 오의 한 자리. 곡선은 SkillCurve가 공유하고 여기 있는 것은 이
         *        스킬의 시작점뿐이다.
         *
         * 값을 코드(SkillCatalog)에서 읽지 않고 직렬화해 두는 이유는 이 프로젝트의
         * 규칙이다 - 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지
         * 않으므로, 빌더가 단일 출처로서 씬에 명시적으로 기록한다.
         * `SkillPanelBuilder`가 SkillCatalog에서 여기로 옮겨 적고, 테스트가 둘이
         * 같은지 검사한다.
         */
        [Serializable]
        public sealed class Slot
        {
            public string id;
            public string displayName;

            [Tooltip("현재 레벨. 1부터")]
            public int level = 1;

            [Tooltip("레벨 1의 배율. 공격력에 곱해진다")]
            public double baseMultiplier = 1d;

            [Tooltip("시전 간격 (초). **성장 축이 아니다** - 끝까지 고정")]
            public float cooldownSeconds = 10f;

            [Tooltip("이 캐릭터 레벨부터 열린다. **0이면 아래 스테이지 게이트를 쓴다**")]
            public int unlockLevel = 10;

            [Tooltip("최전선이 이 스테이지에 닿으면 열린다 (unlockLevel이 0일 때)")]
            public int unlockStage = 1;

            [Tooltip("진행이 아니라 **뽑기**가 여는 오의인가 (50단계)")]
            public bool gachaGated;

            /**
             * @brief 뽑기로 얻었는가. **세이브에 들어간다** (v18).
             *
             * `level`과 나란히 서는 두 번째 상태다. 레벨은 "얼마나 벼렸는가"
             * 이고 이쪽은 "손에 넣었는가"인데, 둘을 한 값으로 접을 수 없다 -
             * 뽑기로 막 얻은 오의의 레벨이 1이고 그것은 아직 못 얻은 오의의
             * 레벨과 같은 숫자다. 44단계 요도가 발견(discovered)을 티어와
             * 따로 적은 것과 같은 구분이다.
             *
             * gachaGated가 아닌 오의에서는 읽지 않는다.
             */
            public bool gachaOwned;

            [Tooltip("첫 구매 비용. 해금 시점의 골드 규모에 맞춰 빌더가 적는다")]
            public double baseCost = 100d;

            [Tooltip("대형 참격의 색. 세 오의를 가르는 유일한 표시다")]
            public Color slashTint = Color.white;

            /** 다음 시전까지 남은 시간. 저장하지 않는다 - 아래 주석 참고 */
            [NonSerialized] public float timer;

            /**
             * @brief 이번 세션에 실제로 시전된 횟수.
             *
             * 저장하지 않는다. 통계가 아니라 **계측**이다 - "자동 시전을 켰는데
             * 나가는지 모르겠다"에 답하는 것이 이 값의 유일한 일이고, 그 질문은
             * 언제나 지금 이 세션에 대한 것이다.
             *
             * 쿨다운 타이머만으로는 부족하다. 타이머는 도는데 사거리가 계속 비어
             * 시전이 거절되는 상태(CastSkill이 false)와, 정상 시전이 화면에서
             * 안 읽히는 상태를 구분해주지 못한다. 이 값이 오르면 두 번째다.
             */
            [NonSerialized] public int castCount;
        }

        [SerializeField] private PlayerCombat combat;

        /**
         * @brief 시전 안무. 27단계에 PlayerCombat에서 갈라졌다.
         *
         * 이쪽으로 부르는 이유는 오의마다 타격 분배가 다르기 때문이다 - 연참은
         * 세 번, 일섬은 경로의 전부, 귀참은 화면의 전부다. 그 분배를 여기(진행
         * 시스템)가 알아야 할 이유가 없고, PlayerCombat이 알면 "한 대"와 "한
         * 시전"이 한 함수에 섞인다.
         *
         * 비어 있으면 시전하지 않는다. 폴백으로 단일 대상 한 방을 내지 않는
         * 이유는, 그러면 배선이 빠진 것이 화면에서 26단계와 똑같이 보이기 때문이다.
         */
        [SerializeField] private SkillPerformer performer;

        [SerializeField] private Slot[] slots;

        /**
         * @brief 최전선을 읽는다. **신규 오의와 4번 슬롯의 게이트다**(49단계).
         *
         * EquipmentSystem·PetSystem·YodoSystem과 같은 패턴이고 같은 이유다 -
         * 지금 서 있는 스테이지가 아니라 최전선이어야 재선택으로 아래에 내려가도
         * 오의가 안 잠긴다.
         */
        [SerializeField] private StageProgress stage;

        /**
         * @brief 장착 구성. 값은 `slots`의 인덱스이고 -1은 빈 자리다.
         *
         * ## 왜 슬롯 배열이 따로 있는가 - Slot에 bool을 안 붙인 이유
         *
         * `Slot.equipped` 하나면 될 것 같지만 그러면 **자리의 순서**가 사라진다.
         * 화면의 네 칸은 왼쪽부터 순서가 있고 플레이어가 그 자리에 무엇을 넣을지
         * 정한다 - bool로 두면 순서가 카탈로그 순서로 고정되고, "2번 칸의 오의를
         * 바꾼다"는 조작이 표현되지 않는다.
         *
         * 길이는 언제나 SkillCurve.MaxSlots다. 열린 슬롯 수(SlotCapacity)는 그중
         * 앞쪽 몇 칸인지를 말하고, 잠긴 칸의 값은 **지우지 않는다** - 되돌아가서
         * 잠겼다가 다시 열릴 때 플레이어가 짜둔 구성이 남아 있어야 한다.
         */
        [SerializeField] private int[] equipped;

        /**
         * @brief 자동 시전 토글. 세이브에 들어간다(v7).
         *
         * 끄면 오의가 나가지 않고 DPS에서도 빠진다. 화면과 계산이 같은 값을
         * 쓰는 것이 이 프로젝트의 규칙이라, 토글이 CastRate에도 걸린다 - 꺼둔
         * 플레이어의 방치 보상이 켜둔 것처럼 계산되면 안 된다.
         */
        [SerializeField] private bool autoCast = true;

        /** 레벨·잔액·해금 중 무엇이든 바뀌면 발생 */
        public event Action Changed;

        public static SkillSystem Instance { get; private set; }

        public int SlotCount { get { return slots != null ? slots.Length : 0; } }

        public Slot GetSlot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length) return null;
            return slots[index];
        }

        public bool AutoCast
        {
            get { return autoCast; }
            set
            {
                if (autoCast == value) return;
                autoCast = value;
                Raise();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Onikiri] A second SkillSystem appeared; keeping the first.");
                return;
            }
            Instance = this;
            EnsureEquippedArray();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var character = CharacterLevel.Instance;

            // **레벨도 구독한다.** 기존 셋은 Lv.10/15/20에 열리는데, 그때 자리에
            // 안 앉으면 새 플레이어는 첫 오의를 얻고도 아무 일이 안 일어난다 -
            // 실기에서 실제로 그랬다(Lv.22인데 세 자리가 전부 비어 있었다)
            if (character != null) character.Changed += OnUnlockMayHaveChanged;

            // 4번 슬롯과 신규 오의는 최전선으로 열린다. 구독하지 않으면 st51에
            // 닿아도 화면이 그대로이고, 플레이어는 탭을 다시 열어야 알아챈다
            if (stage != null) stage.Changed += OnUnlockMayHaveChanged;

            RememberOpenState();
            FillEmptySlots();
            Raise();
        }

        private void OnDisable()
        {
            var character = CharacterLevel.Instance;
            if (character != null) character.Changed -= OnUnlockMayHaveChanged;
            if (stage != null) stage.Changed -= OnUnlockMayHaveChanged;
        }

        /**
         * @brief 레벨이나 최전선이 움직였다. **새로 열린 것이 있을 때만** 메운다.
         *
         * ## 왜 "있을 때만"인가 - 뺀 것을 도로 채우면 안 된다
         *
         * 빈 자리는 언제나 손해라 매번 채워 주는 편이 친절해 보인다. 그런데
         * 자리를 비우는 유일한 이유가 **바꾸려는 것**이라(칩을 눌러 빼고 목록에서
         * 다른 것을 끼운다), 매번 채우면 빼는 순간 같은 것이 도로 들어와 바꿀
         * 수가 없다.
         *
         * 그래서 "무언가 새로 열렸는가"만 본다. 자리 수와 열린 오의 수를 기억해
         * 두고 그중 하나라도 늘었을 때만 메운다 - 그 순간에는 플레이어가 방금
         * 뺀 자리가 있을 수 없다(해금은 플레이어의 조작이 아니다).
         */
        private void OnUnlockMayHaveChanged()
        {
            int capacity = SlotCapacity;
            int unlocked = UnlockedCount;

            if (capacity > filledCapacity || unlocked > filledUnlocked)
            {
                RememberOpenState();

                // **손대지 않은 구성은 기준 구성으로 다시 짓는다.**
                //
                // 빈 자리만 메우는 것으로는 모자란다는 것이 실기에서 드러났다:
                // 자리가 하나뿐인 동안(Lv.10 전) 신규 오의가 그 자리에 앉고,
                // 나중에 귀참이 열려도 자리가 이미 차 있어 **더 센 것이 영원히
                // 벤치에 남았다.** 화면에는 "레벨을 올렸는데 귀참이 안 나간다"로
                // 나온다.
                //
                // 손댄 구성은 안 건드린다 - 그때는 벤치에 있는 것이 플레이어의
                // 결정이고, 자동으로 되돌리는 것은 그 결정을 무시하는 일이다.
                if (loadoutIsCustom) FillEmptySlots();
                else RebuildDefaultLoadout();
            }

            Raise();
        }

        /**
         * @brief 기준 구성으로 다시 짓는다. **손대지 않은 구성에만 쓴다.**
         *
         * 시뮬레이션의 기준 구성(SkillCatalog.ReferenceLoadout)과 같은 규칙이라
         * 화면의 기본값과 밴드가 가정하는 구성이 갈리지 않는다.
         */
        private void RebuildDefaultLoadout()
        {
            EnsureEquippedArray();
            for (int slot = 0; slot < equipped.Length; slot++) equipped[slot] = -1;
            FillEmptySlots();
        }

        /**
         * @brief 플레이어가 구성을 손댔는가. **저장하지 않는다.**
         *
         * 저장할 필요가 없다 - 구성 자체(skillEquipped)가 저장되므로, 손댄
         * 플레이어의 세이브에는 id가 들어 있고 안 손댄 플레이어의 세이브는
         * 비어 있다. 복원이 그 둘을 구분해 이 값을 다시 세운다(RestoreEquipped).
         */
        [NonSerialized] private bool loadoutIsCustom;

        private int UnlockedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SlotCount; i++) if (IsUnlocked(i)) count++;
                return count;
            }
        }

        private void RememberOpenState()
        {
            filledCapacity = SlotCapacity;
            filledUnlocked = UnlockedCount;
        }

        /** 마지막으로 메운 시점의 상태. 저장하지 않는다 - 레벨과 최전선에서 유도된다 */
        [NonSerialized] private int filledCapacity;
        [NonSerialized] private int filledUnlocked;

        // ---------------------------------------------------------------- 해금

        /**
         * @brief 캐릭터 레벨. 없으면 1.
         *
         * 1로 떨어지는 것이 중요하다. 전투 전용 테스트 씬에는 CharacterLevel이
         * 없는데, 거기서 0이 되면 아무것도 안 열리는 것이 아니라 **음수 비교로
         * 전부 열릴 수도** 있다. 명시적으로 최솟값을 준다.
         */
        private static int CharacterLevelNow
        {
            get
            {
                var character = CharacterLevel.Instance;
                return character != null ? Mathf.Max(1, character.Level) : 1;
            }
        }

        /**
         * @brief 최전선 스테이지. 없으면 1.
         *
         * 1로 떨어지는 것이 CharacterLevelNow와 같은 이유다 - 전투 전용 테스트
         * 씬에는 StageProgress가 없는데, 0이 되면 st51 게이트가 음수 비교로
         * 뒤집힐 여지가 생긴다. 명시적으로 최솟값을 준다.
         */
        private int FrontierNow
        {
            get { return stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1; }
        }

        /**
         * @brief 이 오의가 열려 있는가. **레벨 게이트와 스테이지 게이트 둘 다 본다.**
         *
         * 어느 게이트를 쓰는지는 슬롯이 스스로 말한다(`unlockLevel <= 0`이면
         * 스테이지). 빌더가 카탈로그에서 옮겨 적으므로 두 표가 갈리지 않는다.
         */
        public bool IsUnlocked(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return false;

            // 50단계: 뽑기 게이트가 가장 바깥이다. 최전선을 아무리 밀어도
            // 안 열리는 것이 이 둘의 정의이고, 그래서 스테이지 검사보다
            // 먼저 답한다 - 순서가 반대면 unlockStage(비용 기준점 st41)가
            // 게이트처럼 동작해 뽑지 않은 오의가 상점 해금과 함께 열린다
            if (slot.gachaGated) return slot.gachaOwned;

            if (slot.unlockLevel <= 0) return FrontierNow >= slot.unlockStage;
            return CharacterLevelNow >= slot.unlockLevel;
        }

        /**
         * @brief 카탈로그가 읽는 보유 비트마스크. 기준 구성이 이 값을 지난다.
         *
         * `SkillCatalog.ReferenceLoadout`과 같은 세계를 보게 하려고 만든다 -
         * 화면의 기본 구성과 밴드가 가정하는 구성이 갈리면 아무것도 안 만진
         * 플레이어가 밴드 밖에 선다(FillEmptySlots 주석).
         */
        public int GachaOwnedMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    var slot = slots[i];
                    if (slot != null && slot.gachaGated && slot.gachaOwned) mask |= 1 << i;
                }
                return mask;
            }
        }

        /** 하나라도 열렸는가. 하단 "스킬" 버튼이 이 값으로 켜진다 */
        public bool AnyUnlocked
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                    if (IsUnlocked(i)) return true;
                return false;
            }
        }

        // ---------------------------------------------------------------- 장착 (49단계)

        /**
         * @brief 지금 열려 있는 장착 자리 수.
         *
         * **레벨과 최전선을 둘 다 본다**(49b). 앞의 셋은 기본 오의를 배울 때마다
         * 하나씩 열리고(Lv.10/15/20) 넷째는 최전선 st51이다 - 자리를 셋으로
         * 고정하면 코리더에 빈 자리가 생기고, 거기 신규 오의가 그냥 들어가
         * 공짜 DPS가 된다(SkillCurve.SlotsFor 주석).
         */
        public int SlotCapacity { get { return SkillCurve.SlotsFor(CharacterLevelNow, FrontierNow); } }

        /** 이 자리가 아직 잠겨 있는가. 화면이 잠긴 칸을 그릴 때 쓴다 */
        public bool IsSlotLocked(int slot)
        {
            return slot >= SlotCapacity;
        }

        /**
         * @brief 다음에 열릴 오의와 그 조건. 없으면 빈 문자열 둘 (41b 잠긴 미리보기).
         *
         * 화면이 "다음에 무엇이 언제 오는가"를 말할 수 있어야 해금이 사건이
         * 된다 - 41단계가 잠긴 탭을 감추지 않고 조건을 적기로 한 것과 같은
         * 판단이다. 이 값이 없으면 신규 오의가 여섯 번 열리는 동안 화면은
         * 매번 "갑자기 하나 늘었다"만 말한다.
         *
         * 가장 가까운 것 하나만 고른다. 목록을 다 적으면 그것은 예고가 아니라
         * 로드맵이고, 로드맵은 이 화면이 할 일이 아니다.
         */
        public bool TryNextUnlock(out string displayName, out string gate)
        {
            displayName = string.Empty;
            gate = string.Empty;

            int bestStage = int.MaxValue, bestLevel = int.MaxValue, best = -1;

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = GetSlot(i);
                if (slot == null || IsUnlocked(i)) continue;

                // 뽑기가 여는 오의는 예고하지 않는다 (50단계). 이 줄이 하는
                // 일은 "다음에 무엇이 **언제**"인데 확률에는 언제가 없고,
                // 상점의 배너가 그 자리를 이미 맡고 있다 - 두 화면이 같은
                // 것을 다른 말로 예고하면 하나는 반드시 거짓이 된다
                if (slot.gachaGated) continue;

                // 레벨 게이트와 스테이지 게이트는 서로 비교할 수 없는 값이라,
                // **스테이지 게이트를 먼저** 본다 - 신규 오의가 그쪽이고 이
                // 예고의 목적이 그 여섯 번을 사건으로 만드는 것이다
                if (slot.unlockLevel <= 0)
                {
                    if (slot.unlockStage >= bestStage) continue;
                    bestStage = slot.unlockStage;
                    best = i;
                }
                else if (bestStage == int.MaxValue && slot.unlockLevel < bestLevel)
                {
                    bestLevel = slot.unlockLevel;
                    best = i;
                }
            }

            if (best < 0) return false;

            var found = GetSlot(best);
            displayName = found.displayName;
            gate = found.unlockLevel > 0 ? "Lv." + found.unlockLevel : found.unlockStage + "스테이지";
            return true;
        }

        /** slot번 자리에 끼운 오의의 인덱스. 비었으면 -1 */
        public int EquippedAt(int slot)
        {
            if (equipped == null || slot < 0 || slot >= equipped.Length) return -1;

            int index = equipped[slot];
            if (index < 0 || index >= SlotCount) return -1;

            // 잠긴 오의는 자리에 남아 있어도 안 나간다. 지우지 않는 이유는
            // 위 `equipped` 주석과 같다 - 구성은 플레이어의 것이다
            return IsUnlocked(index) ? index : -1;
        }

        /** 이 오의가 열린 자리에 끼워져 있는가. 화면·DPS·시전이 함께 읽는다 */
        public bool IsEquipped(int index)
        {
            for (int slot = 0; slot < SlotCapacity; slot++)
                if (EquippedAt(slot) == index) return true;
            return false;
        }

        /** 이 오의가 몇 번 자리에 있는가. 없으면 -1 */
        public int SlotOf(int index)
        {
            for (int slot = 0; slot < SlotCapacity; slot++)
                if (EquippedAt(slot) == index) return slot;
            return -1;
        }

        /**
         * @brief slot번 자리에 index번 오의를 끼운다.
         *
         * **이미 다른 자리에 있으면 두 자리를 맞바꾼다.** 지우고 넣으면 빈 자리가
         * 생기는데, 네 칸이 전부 채워져 있는 것이 정상 상태라(같은 오의를 두 번
         * 끼울 수 없으므로 자리 수 = 장착 수) 빈 칸은 실수로만 나온다. 맞바꾸면
         * 실수 자체가 없어진다.
         *
         * @param index -1이면 비운다
         * @return 구성이 실제로 바뀌었으면 true
         */
        public bool Equip(int slot, int index)
        {
            if (equipped == null || slot < 0 || slot >= equipped.Length) return false;
            if (IsSlotLocked(slot)) return false;
            if (index >= SlotCount) return false;
            if (index >= 0 && !IsUnlocked(index)) return false;
            if (equipped[slot] == index) return false;

            if (index >= 0)
            {
                int previous = SlotOf(index);
                if (previous >= 0) equipped[previous] = equipped[slot];
            }

            equipped[slot] = index;

            // 여기서부터는 플레이어의 구성이다. 해금이 일어나도 자동으로
            // 다시 짓지 않는다
            loadoutIsCustom = true;

            Raise();
            return true;
        }

        /**
         * @brief 빈 자리와 잠긴 오의를 메운다. **구성이 비어 있을 때만 채운다.**
         *
         * 세이브 복원 직후와 4번 슬롯이 처음 열릴 때 불린다. 채우는 순서는
         * 시뮬레이션의 기준 구성(SkillCatalog.ReferenceLoadout)과 같다 - 화면의
         * 기본값과 밴드가 가정하는 구성이 갈리면, 아무것도 안 만진 플레이어가
         * 밴드 밖에 서게 된다.
         *
         * 플레이어가 이미 넣어둔 것은 건드리지 않는다. 새 오의가 열렸다고
         * 구성을 다시 짜 주면 그것은 편의가 아니라 **선택을 빼앗는 것**이다.
         */
        public void FillEmptySlots()
        {
            if (equipped == null) return;

            int capacity = SlotCapacity;
            bool changed = false;

            for (int slot = 0; slot < capacity && slot < equipped.Length; slot++)
            {
                if (EquippedAt(slot) >= 0) continue;

                int best = -1;
                double bestRate = 0d;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (!IsUnlocked(i) || IsEquipped(i)) continue;

                    // 동률 판정은 카탈로그와 **같은 함수**를 지난다. 두 곳이
                    // 각자 부등호를 쓰면 화면의 기본 구성과 밴드가 가정하는
                    // 구성이 부동소수점 잡음으로 갈린다
                    double rate = SkillCatalog.CeilingRateOf(i);
                    if (!SkillCatalog.IsBetterRate(rate, bestRate)) continue;

                    best = i;
                    bestRate = rate;
                }

                if (best < 0) break;
                equipped[slot] = best;
                changed = true;
            }

            if (changed) Raise();
        }

        // ---------------------------------------------------------------- 값

        /**
         * @brief 이 오의가 **지금 실제로 내는** 배율. 45단계부터 상성이 곱해진다.
         *
         * 상성을 여기서 곱하는 것이 요점이다. 이 값 하나가 세 곳으로 간다 -
         * 화면의 배율(SkillButton), 데미지(SkillPerformer가 받는 총 배율),
         * 초당 환산 기여(CastRate). 한 자리에서 곱하면 셋이 영원히 같은
         * 값을 말하고, 나누면 "패널에는 올랐는데 데미지는 그대로"가 된다.
         *
         * **상한 뒤에 곱한다.** 상성이 상한 안쪽에 들어가면 오의 배율 상한
         * (SkillCurve.CeilingRatio)이 상성만큼 낮아진 것과 같아져, 요도를
         * 벼릴수록 오의 레벨의 값이 줄어든다 - 두 축이 서로를 갉아먹는 상태다.
         * 상성은 상한 **위에** 얹히는 별개의 층이고, 그래서 곡선도 자기 상한
         * (요도 티어 10)에서 따로 닫힌다.
         */
        public double MultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0d;
            return SkillCurve.CappedMultiplierAtLevel(slot.baseMultiplier, slot.level)
                   * YodoSystem.CurrentAffinityForSkill(index);
        }

        public double NextMultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0d;
            return SkillCurve.CappedMultiplierAtLevel(slot.baseMultiplier, slot.level + 1)
                   * YodoSystem.CurrentAffinityForSkill(index);
        }

        /** 상성을 걷어낸 순수 오의 배율. 상성 표기가 "얼마가 얹혔는가"를 적을 때 쓴다 */
        public double BaseMultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0d;
            return SkillCurve.CappedMultiplierAtLevel(slot.baseMultiplier, slot.level);
        }

        public bool IsMaxed(int index)
        {
            var slot = GetSlot(index);
            return slot != null && slot.level >= SkillCurve.MaxLevel;
        }

        public BigDouble CostOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return BigDouble.Zero;
            return BigDouble.FromDouble(SkillCurve.CostAtLevel(slot.baseCost, slot.level));
        }

        /**
         * @brief 지금 오의들이 만드는 초당 환산 공격 횟수.
         *
         * CombatStats.SkillRate와 같은 정의다. 잠긴 것과 자동 시전이 꺼진 상태는
         * 0으로 센다 - 화면에서 안 나가는 것이 계산에는 들어가 있으면, 그것이
         * 9단계에 겪은 "계산상 통과, 화면은 실패"다.
         */
        /**
         * ## 49단계 - **장착한 것만 센다**
         *
         * 화면에서 안 나가는 것이 계산에 들어가 있으면 안 된다는 위 규칙이
         * 슬롯에도 그대로 걸린다. 보유했지만 안 끼운 오의는 쿨다운도 안 돌고
         * 방치 보상에도 안 들어간다 - 그것이 슬롯의 정의이고, 밴드가 풀 크기가
         * 아니라 슬롯 수만 보는 근거다.
         */
        public double CastRate
        {
            get
            {
                if (!autoCast) return 0d;

                double rate = 0d;
                int capacity = SlotCapacity;
                for (int s = 0; s < capacity; s++)
                {
                    int i = EquippedAt(s);
                    if (i < 0) continue;

                    var slot = slots[i];
                    if (slot.cooldownSeconds <= 0f) continue;
                    rate += MultiplierOf(i) / slot.cooldownSeconds;
                }
                return rate;
            }
        }

        /** 씬에 SkillSystem이 없으면 0. PlayerCombat이 참조 없이 읽어 간다 */
        public static double CurrentCastRate
        {
            get { return Instance != null ? Instance.CastRate : 0d; }
        }

        // ---------------------------------------------------------------- 구매

        public bool TryPurchase(int index)
        {
            var slot = GetSlot(index);
            if (slot == null || !IsUnlocked(index) || IsMaxed(index)) return false;

            var wallet = PlayerWallet.Instance;
            if (wallet == null || !wallet.TrySpend(CostOf(index))) return false;

            slot.level++;
            Raise();
            return true;
        }

        // ---------------------------------------------------------------- 스킬 XP (50단계)

        /**
         * @brief 아직 레벨로 바뀌지 않은 스킬 XP. **하나의 풀이다 - 오의별이 아니다.**
         *
         * ## 왜 오의마다 나눠 담지 않는가
         *
         * 오의별로 쌓으면 뽑기가 "어느 오의의 XP인가"를 정해야 하고, 그
         * 순간 뽑기가 **타겟팅**을 갖는다 - 46단계가 혼 정수에서 거부한 바로
         * 그것이고(GachaCurve.EssenceTargetFor), 거부한 이유도 그대로다:
         * 지목할 수 있으면 과금은 언제나 한 곳에 몰아준다.
         *
         * 풀 하나로 두고 **쓰는 순서를 규칙이 정하면**(SpendXp) 뽑기는
         * 무엇을 주는지 알 필요가 없고, 플레이어는 장착을 바꾸는 것으로만
         * 그 순서에 개입한다. 고르는 자리가 뽑기가 아니라 슬롯이라는 뜻이고,
         * 그것이 49단계가 만든 화면 그대로다.
         *
         * 잔액이 남는 것은 낭비가 아니라 **다음 칸의 진행도**다 - 화면이
         * "12 / 16"으로 적는다.
         */
        [SerializeField] private long skillXp;

        public long SkillXp { get { return skillXp; } }

        /** 지금 XP가 들어가고 있는 오의. 없으면 -1 (전부 상한) */
        public int XpTargetIndex { get { return FindXpTarget(); } }

        /** 그 오의의 다음 칸에 필요한 XP. 대상이 없으면 0 */
        public long XpToNextLevel
        {
            get
            {
                int target = FindXpTarget();
                if (target < 0) return 0L;
                return SkillGachaCurve.XpToNextLevel(GetSlot(target).level);
            }
        }

        /**
         * @brief XP를 넣는다. 넣는 즉시 살 수 있는 칸을 전부 산다.
         *
         * **상한을 절대 넘지 않는다.** 이 한 줄이 이 스텝의 유일한 안전선이다 -
         * 45단계가 못 박은 오의 몫 계약(49.0% / 한계 50%)은 배율 상한
         * (SkillCurve.CeilingRatio) 위에서 유도된 값이고, XP가 그 상한을
         * 넘기는 순간 계약이 그 자리에서 깨진다. 그래서 XP가 하는 일은
         * **상한에 더 빨리 닿는 것**뿐이고, 넘는 경로는 코드에 존재하지 않는다
         * (SpendXp의 정지 조건이 곧 SkillCurve.MaxLevel이다).
         *
         * @return 실제로 오른 레벨 수. 화면의 연출이 이 값을 읽는다
         */
        public int GrantXp(long amount)
        {
            if (amount <= 0L) return 0;

            skillXp += amount;
            int gained = SpendXp();

            Raise();
            return gained;
        }

        /**
         * @brief 풀이 닿는 만큼 레벨을 산다.
         *
         * ## 순서 - 장착이 먼저, 그다음 벤치
         *
         * **장착한 오의 중 레벨이 가장 낮은 것**부터다. 가속이 화면에서
         * 읽히려면 지금 나가고 있는 오의가 자라야 하고, 낮은 것부터인 이유는
         * 곡선이 그쪽에서 가장 싸기 때문이다(XpToNextLevel이 지수라 한 칸의
         * 값이 레벨에 따라 열넷 배까지 갈린다) - 같은 XP로 가장 많은 칸을 산다.
         *
         * 장착이 전부 상한이면 **벤치로 넘어간다.** 버리지 않는 이유는 44단계의
         * "버려지는 드랍 0" 그대로이고, 여기서는 값도 있다 - 방금 뽑아서 얻은
         * 오의는 Lv.1로 벤치에 앉으므로, 그것이 자라 있어야 플레이어가 실제로
         * 바꿔 끼워 볼 수 있다. **뽑기가 파는 폭이 벤치에서 완성된다.**
         *
         * 동률은 표 순서로 갈린다 - 뜻이 있는 규칙이 뜻이 없는 순서를 이기는
         * 것은 49단계의 기준 구성과 같은 처리다.
         */
        private int SpendXp()
        {
            int gained = 0;

            for (int guard = 0; guard < 1000; guard++)
            {
                int target = FindXpTarget();
                if (target < 0) break;

                long cost = SkillGachaCurve.XpToNextLevel(GetSlot(target).level);
                if (cost <= 0L || skillXp < cost) break;

                skillXp -= cost;
                GetSlot(target).level++;
                gained++;
            }

            return gained;
        }

        /** 지금 XP가 들어갈 오의. 장착 -> 벤치 순, 각 구간에서 최저 레벨 */
        private int FindXpTarget()
        {
            int equippedBest = -1, benchBest = -1;

            for (int i = 0; i < SlotCount; i++)
            {
                if (!IsUnlocked(i) || IsMaxed(i)) continue;

                if (IsEquipped(i))
                {
                    if (equippedBest < 0 || slots[i].level < slots[equippedBest].level)
                        equippedBest = i;
                }
                else if (benchBest < 0 || slots[i].level < slots[benchBest].level)
                {
                    benchBest = i;
                }
            }

            return equippedBest >= 0 ? equippedBest : benchBest;
        }

        // ---------------------------------------------------------------- 뽑기 (50단계)

        /**
         * @brief 뽑기가 이 오의를 열었다. 이미 열려 있으면 false.
         *
         * **자리에 앉히지 않는다.** 열린 것은 풀에 들어올 뿐이고 넣는 것은
         * 플레이어의 조작이다 - 49단계가 "새 오의가 열렸다고 구성을 다시 짜
         * 주면 그것은 편의가 아니라 선택을 빼앗는 것"이라고 적은 규칙 그대로다.
         *
         * 손대지 않은 구성은 예외인데(RebuildDefaultLoadout), 그 경로에서도
         * 결과가 안 바뀐다 - 가챠 몫 둘은 신규 셋과 동률이고 동률은 표
         * 순서로 갈리므로, 표에서 뒤에 선 이 둘은 기준 구성에 못 들어간다.
         * **뽑기로 얻는 것이 진행으로 얻는 것보다 세지 않다**는 49단계의
         * 규칙이 여기서 자동으로 성립한다.
         */
        public bool GrantGachaSkill(int index)
        {
            var slot = GetSlot(index);
            if (slot == null || !slot.gachaGated || slot.gachaOwned) return false;

            slot.gachaOwned = true;

            // 방금 열린 오의는 벤치에 Lv.1로 앉는다. 빈 자리가 있으면(코리더
            // 아래로 되돌아간 경우 등) 기준 구성 규칙대로 메운다
            RememberOpenState();
            FillEmptySlots();

            Raise();
            return true;
        }

        /**
         * @brief 개안(★5). 장착 오의 하나를 **즉시 상한까지** 민다.
         *
         * 상한 **까지**이지 넘어서가 아니다. 열한 칸을 한 번에 건너뛰지만
         * 도달하는 곳은 골드로 도달하는 곳과 같은 자리이고, 그것이 이 축의
         * 천장이 안 움직이는 이유다(SkillGachaCurve 머리 주석).
         *
         * 장착한 것만 고르는 이유는 이 결과가 **가속**이기 때문이다 - 벤치의
         * 오의를 상한으로 만들어도 지금 나가는 DPS는 한 톨도 안 변하고,
         * 그러면 200회에 한 번의 결과가 화면에서 아무 일도 아니게 된다.
         *
         * @return 실제로 오른 오의. 대상이 없으면 -1 (사다리를 미끄러진다)
         */
        public int AwakenEquipped()
        {
            int best = -1;
            int capacity = SlotCapacity;

            for (int s = 0; s < capacity; s++)
            {
                int i = EquippedAt(s);
                if (i < 0 || IsMaxed(i)) continue;
                if (best < 0 || slots[i].level < slots[best].level) best = i;
            }

            if (best < 0) return -1;

            slots[best].level = SkillCurve.MaxLevel;
            Raise();
            return best;
        }

        /**
         * @brief 이 뽑기의 재고가 남아 있는가. 상점 배너가 이 값으로 열리고 닫힌다.
         *
         * ## 재고가 유한하다는 것을 화면이 말한다
         *
         * 47단계는 요도 쪽에서 반대 선택을 했다 - 혼격 상한을 바퀴로 늘려
         * 재고를 계속 만들었고, 그 대가로 심층 천장을 +28% 재기준해야 했다.
         * 이 축에서는 그 길이 막혀 있다: 재고를 늘리는 유일한 방법이 **오의
         * 레벨 상한을 올리는 것**인데, 그 상한이 오의 몫 계약의 유일한
         * 안전선이다.
         *
         * 그래서 재고를 유한하게 두고 **다 팔리면 배너를 닫는다.** 41단계의
         * "눌리는데 아무 일도 안 일어나는 것보다 안 눌리는 편이 정직하다"를
         * 상품에 적용한 자리이고, 46단계가 준비 중 두 줄에 한 것과 같은
         * 처방이다. 풀을 넓히는 것(가챠 전용 신규 오의)이 이 배너의 다음
         * 재고이고 그것은 다음 스텝의 몫이다.
         *
         * 벤치의 미상한 오의는 재고에 **안 센다.** 그것으로 뽑을 이유를
         * 만들면 "안 쓰는 오의를 위해 뽑는다"가 되고, 그러면 뽑기가 파는
         * 것이 가속이 아니라 수집이 된다 - 그 자리는 도감의 것이다.
         */
        public bool HasStock
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    var slot = slots[i];
                    if (slot == null) continue;

                    // 아직 못 얻은 가챠 몫이 있다
                    if (slot.gachaGated && !slot.gachaOwned) return true;
                }

                // 장착 오의 중 상한에 안 닿은 것이 있다
                int capacity = SlotCapacity;
                for (int s = 0; s < capacity; s++)
                {
                    int i = EquippedAt(s);
                    if (i >= 0 && !IsMaxed(i)) return true;
                }

                return false;
            }
        }

        // ---------------------------------------------------------------- 자동 시전

        /**
         * @brief 안무가에게 시전을 넘긴다. 총 배율과 색만 건네고 분배는 그쪽이 정한다.
         *
         * 자동 시전과 테스트 패널의 강제 시전이 **같은 함수를 지난다.** 두 경로가
         * 갈리면 패널로 본 화면이 실제 플레이 화면과 같다고 말할 수 없다.
         */
        private bool Perform(int index)
        {
            if (performer == null) return false;

            var slot = GetSlot(index);
            if (slot == null) return false;

            if (!performer.Cast(index, MultiplierOf(index), slot.slashTint, slot.displayName))
                return false;

            // 퀘스트 카운터. **실제로 나간 것만 센다** - Cast가 false면 사거리가
            // 비어 시전되지 않은 것이고, 그것까지 세면 "오의 20회 시전"이 요괴가
            // 없는 동안에도 채워진다.
            //
            // 자동 시전과 테스트 패널이 이 함수를 함께 지나므로 세는 자리도 하나다
            var quests = QuestSystem.Instance;
            if (quests != null) quests.ReportSkillCast();

            return true;
        }

        /**
         * @brief 오의에 **실제로 산 칸**의 합. 업적이 읽는다.
         *
         * ## 49단계에 "레벨 총합"에서 바뀌었다
         *
         * 26단계에는 레벨 총합이었다(잠긴 것도 1로 세어 새 게임이 3, 목표 12).
         * 오의가 여덟이 되면 새 게임의 총합이 8이 되어 같은 목표가 네 번의
         * 구매로 채워진다 - 업적이 갑자기 싸진다.
         *
         * 목표를 17로 올려 맞추는 안도 있었지만, 그러면 **다음에 오의가 늘 때
         * 또 올려야 한다.** 산 칸만 세면 풀 크기와 무관해진다 - 슬롯 예산이
         * 밴드를 풀 크기에서 떼어놓은 것과 같은 수법이고, 구매 횟수도 그대로다
         * (3->12 아홉 번 = 0->9 아홉 번).
         */
        public int TotalLevels
        {
            get
            {
                if (slots == null) return 0;

                int total = 0;
                foreach (var slot in slots)
                    if (slot != null) total += Mathf.Max(0, slot.level - 1);
                return total;
            }
        }

        private void Update()
        {
            if (!autoCast || combat == null || slots == null) return;

            // 벨 것이 없으면 쿨다운도 멈춘다. PlayerCombat이 이미 매 프레임
            // 사거리를 확인하고 있으므로 그 결과를 읽는다 - 여기서 다시 찾으면
            // 사거리가 두 곳에 적히고, 한쪽만 고쳐지는 날 "때리는데 오의는
            // 안 나가는" 상태가 된다
            if (!combat.HasTargetInRange) return;

            // 장착한 자리만 돈다. 안 끼운 오의의 쿨다운이 돌면 끼우는 순간
            // 한꺼번에 터져 나오고, 그것은 슬롯을 "고르는 것"이 아니라
            // "모아 뒀다 쓰는 것"으로 바꾼다
            int capacity = SlotCapacity;
            for (int s = 0; s < capacity; s++)
            {
                int i = EquippedAt(s);
                if (i < 0) continue;

                var slot = slots[i];
                if (slot.cooldownSeconds <= 0f) continue;

                // 스케일 타임이다. 히트스톱 중에는 쿨다운도 언다 - 정지가
                // 길어질수록 오의가 빨라지면 타격감 예산과 밸런스가 서로를
                // 밀어내게 된다
                slot.timer += Time.deltaTime;
                if (slot.timer < slot.cooldownSeconds) continue;

                if (Perform(i))
                {
                    // 남은 시간을 이월한다. 0으로 되돌리면 프레임 경계에서
                    // 조금씩 새어 실제 시전 횟수가 설계값보다 적어진다 -
                    // 12단계에 평타에서 겪은 것과 같은 문제다
                    slot.timer -= slot.cooldownSeconds;
                    slot.castCount++;
                }
                else
                {
                    // 벨 것이 사라졌다. 쿨다운을 다 찬 상태로 붙들어 두면
                    // 다음 요괴가 들어오는 순간 곧바로 나간다
                    slot.timer = slot.cooldownSeconds;
                }
            }
        }

        // ---------------------------------------------------------------- 계측 / 치트

        /**
         * @brief 쿨다운 진행률 (0~1). 1이면 다음 프레임에 나간다.
         *
         * 테스트 패널이 읽는다. 화면에 쿨다운 표시가 없는 동안 **"돌고 있는가"를
         * 확인할 수 있는 유일한 자리**다.
         */
        public float CooldownFraction(int index)
        {
            var slot = GetSlot(index);
            if (slot == null || slot.cooldownSeconds <= 0f) return 0f;
            return Mathf.Clamp01(slot.timer / slot.cooldownSeconds);
        }

        public float SecondsUntilCast(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0f;
            return Mathf.Max(0f, slot.cooldownSeconds - slot.timer);
        }

        /** 이번 세션에 실제로 시전된 횟수 */
        public int CastCountOf(int index)
        {
            var slot = GetSlot(index);
            return slot != null ? slot.castCount : 0;
        }

        /**
         * @brief 쿨다운을 무시하고 지금 시전한다. **테스트 패널 전용.**
         *
         * `Debug` 접두사는 BossFight.DebugExpireTimer와 같은 규칙이다 - 게임 진행
         * 경로에서 부르면 안 되는 것을 이름으로 말한다.
         *
         * 상태를 직접 바꾸지 않고 **실제 시전 경로를 그대로 태운다.** 참격·정지·
         * 흔들림·데미지·소리가 전부 자동 시전과 같은 코드에서 나야, 이 버튼으로 본
         * 화면이 실제 플레이의 화면과 같다고 말할 수 있다. 보스 실패를 상태 조작이
         * 아니라 시계 소진으로 재현한 것과 같은 이유다.
         *
         * @return 실제로 벤 것이 있으면 true. 사거리가 비었으면 false다 -
         *         "눌렀는데 아무 일도 없다"의 원인이 그것일 수 있으므로 구분해서
         *         돌려준다
         */
        public bool DebugCastNow(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return false;

            if (!Perform(index)) return false;

            slot.timer = 0f;
            slot.castCount++;
            return true;
        }

        /** 전부 쿨다운을 채워둔다. 다음 프레임에 셋이 한꺼번에 나간다 */
        public void DebugFillCooldowns()
        {
            for (int i = 0; i < SlotCount; i++)
                slots[i].timer = slots[i].cooldownSeconds;
        }

        /**
         * @brief 오의를 **새 게임 상태로** 되돌린다. 테스트 패널 전용.
         *
         * ## 왜 세이브 삭제로는 안 되는가
         *
         * 패널에 이미 "세이브 삭제"가 있지만 그것은 전부를 지운다 - 스테이지·
         * 레벨·강화·골드까지. 오의 곡선을 다시 보려면 그 전부를 다시 만들어야 하고,
         * 캐릭터 레벨이 1로 돌아가므로 **오의가 잠겨서 살 수조차 없다.**
         *
         * 여기서 되돌리는 것은 오의 축 하나뿐이다. 레벨 47·골드 1.1T를 그대로 둔
         * 채 "12레벨까지 사는 과정"을 몇 번이고 다시 볼 수 있다.
         *
         * ## 무엇을 되돌리는가
         *
         *   레벨      1로. 곡선을 다시 밟는 것이 이 버튼의 목적이다
         *   쿨다운    0으로. 남겨두면 첫 시전이 언제 나갈지가 직전 상태에 좌우된다
         *   시전 수   0으로. 계측값이라 "지금부터"의 기준이 필요하다
         *   자동 시전  켬. 새 게임의 상태이고(SaveData.NewGame), 꺼진 채로 두면
         *             "초기화"라는 말이 거짓이 된다
         *
         * **골드는 돌려주지 않는다.** 환불은 초기화가 아니라 별개의 치트이고,
         * 골드는 패널 위쪽에 이미 자기 버튼이 있다.
         */
        public void DebugResetLevels()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                slot.level = 1;
                slot.timer = 0f;
                slot.castCount = 0;

                // 뽑기로 얻은 것도 되돌린다 (50단계). 새 게임에는 가챠 몫이
                // 없고, 남겨두면 "초기화"라는 말이 거짓이 되는 것은 자동
                // 시전 토글과 같은 이유다. **보석은 안 돌려준다** - 환불은
                // 초기화가 아니라 별개의 치트다(위 골드 주석과 같은 규칙)
                slot.gachaOwned = false;
            }

            skillXp = 0L;

            // 장착 구성도 새 게임 상태로. 남겨두면 "초기화"라는 말이 거짓이
            // 되는 것은 자동 시전 토글과 같은 이유다
            RebuildDefaultLoadout();
            loadoutIsCustom = false;

            autoCast = true;
            Raise();
        }

        // ---------------------------------------------------------------- 세이브

        public string[] CollectIds()
        {
            var ids = new string[SlotCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = slots[i] != null ? slots[i].id : string.Empty;
            return ids;
        }

        public int[] CollectLevels()
        {
            var levels = new int[SlotCount];
            for (int i = 0; i < levels.Length; i++) levels[i] = slots[i] != null ? slots[i].level : 1;
            return levels;
        }

        /**
         * @brief 세이브 복원. **레벨을 자르지 않는다.**
         *
         * UpgradeTrack.SetLevel과 같은 규칙이다 - 상한이 내려간 업데이트에서
         * 플레이어가 산 레벨이 영구히 사라지면 안 된다. 값은 SkillCurve의
         * 배율 상한에서 막히고, 상한이 다시 오르면 잠든 레벨이 깨어난다.
         *
         * 세이브에 있지만 지금 없는 id는 조용히 건너뛴다. 스킬 목록이 바뀌어도
         * 예전 세이브를 계속 읽을 수 있어야 한다.
         */
        /**
         * @brief 장착 구성을 id로 적는다. **인덱스가 아니라 id인 이유는 세이브의 규칙이다.**
         *
         * 카탈로그에 오의가 하나 끼어들면 인덱스는 통째로 밀리는데, 그러면
         * 세이브를 읽는 순간 플레이어의 구성이 다른 오의로 바뀐다. 빈 자리는
         * 빈 문자열이다.
         */
        public string[] CollectEquipped()
        {
            var ids = new string[SkillCurve.MaxSlots];
            for (int slot = 0; slot < ids.Length; slot++)
            {
                int index = equipped != null && slot < equipped.Length ? equipped[slot] : -1;
                var s = GetSlot(index);
                ids[slot] = s != null ? s.id : string.Empty;
            }
            return ids;
        }

        /**
         * @brief 장착 구성 복원. **모르는 id는 빈 자리로 떨어뜨린다.**
         *
         * RestoreLevels의 "세이브에 있지만 지금 없는 id는 건너뛴다"와 같은
         * 규칙이다. 빈 자리는 FillEmptySlots가 기준 구성으로 메우므로, 오의
         * 하나가 표에서 빠져도 플레이어는 빈 칸이 아니라 채워진 칸을 본다.
         *
         * 빈 배열(구세이브)이면 전부 빈 자리가 되고, 그때도 같은 경로로 메워져
         * **기존 오의 셋이 기본 장착된다** - 마이그레이션이 값을 지어내지 않고
         * 구조가 답을 낸다.
         */
        public void RestoreEquipped(string[] ids)
        {
            EnsureEquippedArray();

            loadoutIsCustom = false;

            for (int slot = 0; slot < equipped.Length; slot++)
            {
                string id = ids != null && slot < ids.Length ? ids[slot] : null;
                equipped[slot] = string.IsNullOrEmpty(id) ? -1 : IndexOf(id);

                // 세이브에 id가 하나라도 있으면 그것은 플레이어가 짠 구성이다.
                // 빈 배열(구세이브·새 게임)이면 기준 구성으로 짓는다
                if (equipped[slot] >= 0) loadoutIsCustom = true;
            }

            // 같은 오의가 두 자리에 들어 있으면 뒤쪽을 비운다. 정상 경로로는
            // 나올 수 없지만(Equip이 맞바꾼다) 손으로 고친 세이브가 있을 수 있고,
            // 그대로 두면 한 오의의 쿨다운이 두 번 돌아 DPS가 새어 나간다
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                if (equipped[slot] < 0) continue;
                for (int other = 0; other < slot; other++)
                    if (equipped[other] == equipped[slot]) { equipped[slot] = -1; break; }
            }

            RememberOpenState();
            FillEmptySlots();
            Raise();
        }

        private void EnsureEquippedArray()
        {
            if (equipped != null && equipped.Length >= SkillCurve.MaxSlots) return;

            var grown = new int[SkillCurve.MaxSlots];
            for (int i = 0; i < grown.Length; i++)
                grown[i] = equipped != null && i < equipped.Length ? equipped[i] : -1;
            equipped = grown;
        }

        public void RestoreLevels(string[] ids, int[] levels, bool savedAutoCast)
        {
            autoCast = savedAutoCast;

            if (ids != null && levels != null)
            {
                int count = Mathf.Min(ids.Length, levels.Length);
                for (int i = 0; i < count; i++)
                {
                    int index = IndexOf(ids[i]);
                    if (index < 0) continue;
                    slots[index].level = Mathf.Max(1, levels[i]);
                }
            }

            // 쿨다운 타이머는 저장하지 않는다. 저장하면 껐다 켜기로 쿨다운을
            // 되돌리는 경로가 생기고(나가기 전에 시전 -> 재시작하면 0), 그것을
            // 막으려면 저장 시각까지 함께 봐야 한다. 재시작마다 한 사이클을
            // 다시 기다리는 쪽이 규칙이 하나뿐이라 낫다
            for (int i = 0; i < SlotCount; i++) slots[i].timer = 0f;

            Raise();
        }

        // ---------------------------------------------------------------- 세이브 (50단계)

        /**
         * @brief 뽑기로 얻은 오의의 id들. **보유한 것만 적는다.**
         *
         * 미보유를 0으로 함께 적는 방식(petUnlocked·yodoDiscovered)도 있지만
         * 여기서는 목록이 둘뿐이고 값이 bool 하나라, 있는 것만 적으면 세이브가
         * 스스로 설명된다. 47단계의 legendaryYodoIds가 같은 자리에서 반대
         * 선택을 한 이유는 그쪽에 **사본 수**라는 값이 함께 붙기 때문이다.
         */
        public string[] CollectGachaSkillIds()
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] != null && slots[i].gachaGated && slots[i].gachaOwned) count++;

            var ids = new string[count];
            int at = 0;
            for (int i = 0; i < SlotCount && at < count; i++)
                if (slots[i] != null && slots[i].gachaGated && slots[i].gachaOwned)
                    ids[at++] = slots[i].id;
            return ids;
        }

        public long CollectSkillXp() { return skillXp; }

        /**
         * @brief 보유와 XP 복원. **모르는 id는 조용히 건너뛴다.**
         *
         * RestoreLevels·RestoreEquipped와 같은 규칙이다 - 오의 목록이 바뀌어도
         * 예전 세이브를 계속 읽을 수 있어야 한다.
         *
         * **복원 뒤에 곧바로 쓴다**(SpendXp). v18 이전의 세이브에는 XP가
         * 없어 할 일이 없지만, 상한이 오르는 업데이트가 오면 잠들어 있던
         * 잔액이 그 순간 레벨이 된다 - RestoreLevels가 "상한이 다시 오르면
         * 잠든 레벨이 깨어난다"고 적은 것과 같은 성질이고, 여기서는 잔액이
         * 그 역할을 한다.
         */
        public void RestoreGacha(string[] ownedIds, long savedXp)
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] != null) slots[i].gachaOwned = false;

            if (ownedIds != null)
            {
                foreach (var id in ownedIds)
                {
                    int index = IndexOf(id);
                    if (index < 0) continue;

                    // 가챠 게이트가 아닌 오의에 보유 표시가 들어 있으면 무시한다.
                    // 표가 바뀌는 날 그 값이 조용히 다른 오의를 열면 안 된다
                    if (!slots[index].gachaGated) continue;
                    slots[index].gachaOwned = true;
                }
            }

            skillXp = savedXp < 0L ? 0L : savedXp;
            SpendXp();

            RememberOpenState();
            FillEmptySlots();
            Raise();
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < SlotCount; i++)
                if (slots[i] != null && slots[i].id == id) return i;
            return -1;
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
