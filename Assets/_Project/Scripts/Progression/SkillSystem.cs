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

            [Tooltip("이 캐릭터 레벨부터 열린다")]
            public int unlockLevel = 10;

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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var character = CharacterLevel.Instance;
            if (character != null) character.Changed += Raise;
            Raise();
        }

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

        public bool IsUnlocked(int index)
        {
            var slot = GetSlot(index);
            return slot != null && CharacterLevelNow >= slot.unlockLevel;
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

        // ---------------------------------------------------------------- 값

        public double MultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0d;
            return SkillCurve.CappedMultiplierAtLevel(slot.baseMultiplier, slot.level);
        }

        public double NextMultiplierOf(int index)
        {
            var slot = GetSlot(index);
            if (slot == null) return 0d;
            return SkillCurve.CappedMultiplierAtLevel(slot.baseMultiplier, slot.level + 1);
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
        public double CastRate
        {
            get
            {
                if (!autoCast) return 0d;

                double rate = 0d;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (!IsUnlocked(i)) continue;

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
         * @brief 세 오의의 레벨 총합. 업적이 읽는다.
         *
         * 잠긴 오의도 레벨 1로 들어오므로 새 게임의 총합은 3이다.
         * 업적 목표(12)가 그 기준이다.
         */
        public int TotalLevels
        {
            get
            {
                if (slots == null) return 0;

                int total = 0;
                foreach (var slot in slots)
                    if (slot != null) total += slot.level;
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

            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsUnlocked(i)) continue;

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
            }

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
