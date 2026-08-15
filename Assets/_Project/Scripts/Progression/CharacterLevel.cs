using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 경험치, 레벨, 스탯 포인트.
     *
     * **레벨업은 수동이다.** 경험치가 찼다고 자동으로 오르지 않고 플레이어가 버튼을
     * 눌러야 한다. 자동이면 레벨업은 화면 구석에서 혼자 일어나는 일이 되고, 스탯
     * 포인트가 쌓여 있는데 안 쓰는 상태가 그대로 방치된다. 눌러야 오르면 누르는
     * 그 순간이 "포인트를 어디에 찍을까"를 보는 자리가 된다.
     *
     * 초과분은 사라지지 않고 다음 레벨로 넘어간다. 방치형에서 자리를 비운 사이
     * 여러 레벨분이 쌓이는 것은 정상이고, 그때 초과분을 버리면 오래 비울수록
     * 손해가 되어 게임이 자리를 지키라고 요구하게 된다.
     *
     * 스탯 포인트는 골드 강화와 **곱해진다.** StatPointCurve 참고.
     */
    public sealed class CharacterLevel : MonoBehaviour
    {
        public static CharacterLevel Instance { get; private set; }

        /** 스탯 포인트 축 식별자. 세이브와 UI가 이 문자열로 갈린다 */
        public const string AttackAmpId = "stat_attack";
        public const string HealthAmpId = "stat_health";

        [SerializeField] private UpgradeSystem upgrades;

        [SerializeField] private int level = 1;

        [Tooltip("현재 레벨에서 모은 경험치. 필요량을 넘으면 레벨업 버튼이 열린다")]
        [SerializeField] private BigDouble exp;

        [SerializeField] private int attackPoints;
        [SerializeField] private int healthPoints;

        /** 레벨/경험치/포인트 중 무엇이든 바뀌면 발생 */
        public event Action Changed;

        public int Level { get { return level; } }
        public BigDouble Exp { get { return exp; } }
        public BigDouble ExpRequired { get { return ExpCurve.RequiredForLevel(level); } }

        public int AttackPoints { get { return attackPoints; } }
        public int HealthPoints { get { return healthPoints; } }

        /**
         * @brief 아직 안 찍은 포인트.
         *
         * 저장하지 않고 매번 계산한다. 총 지급량은 레벨의 함수이고 쓴 양은 두 축의
         * 합이므로, 남은 양을 따로 들고 있으면 셋 중 하나가 어긋났을 때 어느 것이
         * 맞는지 알 수 없는 상태가 생긴다.
         */
        public int UnspentPoints
        {
            get
            {
                int total = StatPointCurve.TotalPointsAtLevel(level);
                int spent = attackPoints + healthPoints;
                return total - spent < 0 ? 0 : total - spent;
            }
        }

        public bool CanLevelUp { get { return exp >= ExpRequired; } }

        /** 지금 버튼을 몇 번 누를 수 있는가. 표시용 */
        public int PendingLevelUps { get { return ExpCurve.LevelsAffordable(level, exp); } }

        /** 0~1. 경험치 바가 쓴다 */
        public float ExpFraction
        {
            get
            {
                var need = ExpRequired;
                if (need <= BigDouble.Zero) return 1f;
                if (exp >= need) return 1f;
                return Mathf.Clamp01((float)(exp / need).ToDouble());
            }
        }

        public double AttackMultiplier { get { return StatPointCurve.Multiplier(attackPoints); } }
        public double HealthMultiplier { get { return StatPointCurve.Multiplier(healthPoints); } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
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
            Raise();
        }

        // ---------------------------------------------------------------- 획득

        public void AddExp(BigDouble amount)
        {
            if (amount <= BigDouble.Zero) return;

            exp = exp + amount;
            Raise();
        }

        // ---------------------------------------------------------------- 레벨업

        /**
         * @brief 밀린 레벨을 **전부** 올린다.
         *
         * 12단계에서는 버튼 한 번에 한 레벨이었다. 이유는 "한꺼번에 올리면 스탯
         * 포인트가 뭉텅이로 들어와 안 쓰는 상태가 된다"였는데, 실제로 돌려보니
         * 그 판단이 틀렸다.
         *
         * 15단계 화면에서 **레벨 2에 레벨업 16개가 밀려 있었다.** 그 상태에서
         * 한 번에 하나씩이면 플레이어가 하는 일은 "찍을 곳을 고르는 것"이 아니라
         * 같은 버튼을 열여섯 번 누르는 것이다. 보상이 잡일이 됐다.
         *
         * 포인트가 뭉텅이로 들어오는 문제는 여전히 있지만, 그것은 **레벨업
         * 버튼이 아니라 증폭 축이 풀 문제**다(16단계에서 증폭을 체감되는 크기로
         * 올렸다). 누르는 횟수를 늘려서 풀 일이 아니었다.
         *
         * 방치형에서 밀리는 것은 정상이다 - 8시간 자리를 비우면 여러 레벨분이
         * 쌓이고, 그때 열 번을 누르게 하는 UI는 방치를 벌하는 것이다.
         *
         * @return 실제로 오른 레벨 수. 0이면 경험치가 모자란다
         */
        public int ClaimLevelUps()
        {
            int gained = 0;

            // 상한을 둔다. 오프라인 보상이 비정상적으로 큰 값을 들고 오면
            // (시계가 앞으로 튄 기기 등) 여기서 프레임이 멈추는 것보다 낫다
            for (int guard = 0; guard < 100000; guard++)
            {
                var need = ExpRequired;
                if (exp < need) break;

                exp = exp - need;
                level++;
                gained++;
            }

            if (gained == 0) return 0;

            ApplyToStats();
            Raise();
            return gained;
        }

        /** 예전 이름. 한 번 누르면 밀린 것을 전부 처리한다 */
        public bool TryLevelUp()
        {
            return ClaimLevelUps() > 0;
        }

        // ---------------------------------------------------------------- 포인트

        public bool TrySpendPoint(string axisId)
        {
            if (!AddPoint(axisId)) return false;

            ApplyToStats();
            Raise();
            return true;
        }

        /**
         * @brief 한 점을 **더하기만** 한다. 알리지 않는다.
         *
         * 알림을 빼낸 이유는 여러 점을 찍을 때다 - 한 점마다 Changed를
         * 올리면 그 한 번이 오의 열다섯 줄·HUD·퀘스트·진화를 전부 다시
         * 그리고, 실측 **한 점당 0.646ms**다. 일흔여섯 점이면 49ms이고
         * 방치로 수천 점이 밀린 성장 탭에서는 초 단위로 화면이 멈춘다.
         *
         * 값이 바뀌는 규칙은 여기 하나뿐이므로 한 점을 찍든 천 점을 찍든
         * 상한도 잔량도 같은 문을 지난다.
         */
        private bool AddPoint(string axisId)
        {
            if (UnspentPoints <= 0) return false;

            if (axisId == AttackAmpId)
            {
                if (attackPoints >= StatPointCurve.MaxPoints) return false;
                attackPoints++;
                return true;
            }

            if (axisId == HealthAmpId)
            {
                if (healthPoints >= StatPointCurve.MaxPoints) return false;
                healthPoints++;
                return true;
            }

            Debug.LogWarning("[Onikiri] Unknown stat axis '" + axisId + "'.");
            return false;
        }

        /**
         * @brief 이 축에 **몇 점까지 찍을 수 있는가.** 찍지는 않는다.
         *
         * 남은 포인트와 축 상한 중 작은 쪽이다. "최대" 버튼의 수량과 실제로
         * 찍히는 수가 같아야 하므로 둘이 이 함수 하나를 본다(강화 배수의
         * UpgradeTrack.AffordableLevels와 같은 규칙).
         */
        public int SpendableInto(string axisId, int limit)
        {
            int room = StatPointCurve.MaxPoints - PointsIn(axisId);
            if (room <= 0) return 0;

            int affordable = Math.Min(UnspentPoints, room);
            if (limit > 0) affordable = Math.Min(affordable, limit);
            return affordable > 0 ? affordable : 0;
        }

        /**
         * @brief 한 축에 여러 점을 한 번에 찍는다 (#3 후속 - 성장 탭 배수).
         *
         * 실제로 찍힌 수를 돌려준다. 한 점씩 같은 문(AddPoint)을 도는 것뿐이라
         * 곡선도 상한도 그대로다 - 강화 배수 구매(UpgradeSystem.TryPurchaseMany)와
         * 같은 판단이고, 같은 이유로 새로 생기는 힘이 없다.
         *
         * ## 알림은 **끝에 한 번**이다
         *
         * 처음에는 `TrySpendPoint`를 그대로 돌렸다. 그런데 그 함수는 한 점마다
         * `Raise()`를 부르고, Changed 하나가 오의 열다섯 줄·HUD·퀘스트·진화를
         * 전부 다시 그린다 - 실측 **한 점당 0.646ms**다.
         *
         *   일흔여섯 점    49ms   한 번의 버벅임
         *   수천 점        초 단위로 화면이 멈춘다
         *
         * 포인트는 레벨업으로 쌓이고 레벨업은 방치로 밀리므로 뒤쪽이 실제로
         * 온다. 중간 상태를 화면에 알릴 이유도 없다 - 플레이어가 본 것은
         * "최대"를 한 번 누른 것이고, 사건도 하나다.
         *
         * ## 왜 이쪽에도 필요한가
         *
         * 포인트는 레벨업으로 쌓이고 레벨업은 방치로 밀린다. 오래 안 열어본
         * 사람의 성장 탭에는 수십 점이 쌓여 있는데, 한 점씩 찍는 규칙에서
         * 그것은 수십 번의 탭이다 - 강화 목록에서 배수 버튼을 만든 이유가
         * 여기서 그대로 성립한다.
         *
         * 되돌릴 수 없는 재화라(StatPointButton 주석) 배수는 **화면이 정확히
         * 몇 점을 쓰는지 보여준 뒤에** 눌려야 한다. 그 표시는 버튼 쪽 일이다.
         */
        public int TrySpendPoints(string axisId, int count)
        {
            if (count <= 0) return 0;

            int spent = 0;
            while (spent < count && AddPoint(axisId)) spent++;

            if (spent == 0) return 0;

            ApplyToStats();
            Raise();
            return spent;
        }

        public int PointsIn(string axisId)
        {
            if (axisId == AttackAmpId) return attackPoints;
            if (axisId == HealthAmpId) return healthPoints;
            return 0;
        }

        public bool IsAxisMaxed(string axisId)
        {
            return PointsIn(axisId) >= StatPointCurve.MaxPoints;
        }

        /**
         * @brief 찍은 스탯 포인트를 전부 회수한다. **테스트 패널 전용.**
         *
         * ## 이것만은 환불이 맞다
         *
         * 강화·오의 초기화는 골드를 돌려주지 않는데(그쪽 주석 참고) 여기는
         * 돌려준다. 재화의 성질이 다르기 때문이다.
         *
         * 골드는 파밍으로 다시 벌 수 있지만 **스탯 포인트는 레벨에서만 나온다.**
         * 회수 없이 0으로 만들면 레벨 74가 준 포인트가 영영 사라지고, 그것을
         * 되돌리려면 레벨을 올리는 수밖에 없다 - 초기화가 아니라 파괴다.
         *
         * 회수가 공짜인 것도 구조 덕분이다. 남은 포인트는 저장하지 않고
         * `총 지급량 - 쓴 양`으로 매번 계산하므로(UnspentPoints), 쓴 양을 0으로
         * 만들면 그 자리에서 전부 미사용으로 돌아온다.
         *
         * ## 왜 필요한가
         *
         * 증폭 축의 곡선을 다시 보려면 포인트를 다시 배분해야 하는데, 12단계
         * 이후 **되돌릴 방법이 없었다**(StatPointButton은 찍기만 한다). 지금까지는
         * 세이브를 지우는 것이 유일한 길이었고 그러면 레벨도 함께 사라졌다.
         */
        public void DebugRefundPoints()
        {
            attackPoints = 0;
            healthPoints = 0;

            // 증폭이 곱해진 스탯을 되돌린다. 이것을 잊으면 포인트는 0인데 공격력에
            // 배수가 남아 있는 상태가 되고, 다음 강화 구매 전까지 그대로 간다
            ApplyToStats();
            Raise();
        }

        // ---------------------------------------------------------------- 반영

        /**
         * @brief 증폭을 전투 스탯에 다시 먹인다.
         *
         * 증폭은 곱셈이라 자기 자리에 따로 저장되지 않고 골드 강화 값 위에
         * 얹힌다. 그래서 포인트가 바뀌면 강화 적용을 통째로 다시 돌리는 것이
         * 가장 단순하다 - 스탯이 반영되는 경로가 UpgradeSystem 하나로 남는다.
         */
        private void ApplyToStats()
        {
            if (upgrades != null) upgrades.ApplyAll();
        }

        // ---------------------------------------------------------------- 세이브

        public void Restore(int savedLevel, BigDouble savedExp, int savedAttackPoints, int savedHealthPoints)
        {
            level = savedLevel < 1 ? 1 : savedLevel;
            exp = savedExp < BigDouble.Zero ? BigDouble.Zero : savedExp;

            attackPoints = StatPointCurve.Clamp(savedAttackPoints);
            healthPoints = StatPointCurve.Clamp(savedHealthPoints);

            // 찍은 합이 지급량을 넘으면(상한이 내려갔거나 손상된 파일) 넘친 만큼
            // 뒤에서부터 깎는다. 그냥 두면 UnspentPoints가 계속 0이라 정상처럼
            // 보이는데 실제 스탯은 받지 않은 포인트를 반영하고 있다
            int total = StatPointCurve.TotalPointsAtLevel(level);
            int overflow = attackPoints + healthPoints - total;
            if (overflow > 0)
            {
                int fromHealth = Mathf.Min(healthPoints, overflow);
                healthPoints -= fromHealth;
                attackPoints -= overflow - fromHealth;
                if (attackPoints < 0) attackPoints = 0;

                Debug.LogWarning("[Onikiri] Save had more spent stat points than earned; trimmed "
                                 + overflow + ".");
            }

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
