using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격마다 터지는 벚꽃잎. 참격 방향으로 흩어진다.
     *
     * 참격 이펙트가 "무엇이 일어났는가"를 말한다면 꽃잎은 "베였다"를 말한다. 흰 호
     * 하나는 사물에 닿았다는 느낌이 약하고, 조각이 떨어져 나가야 대상에서 뭔가가
     * 실제로 떨어져 나갔다고 읽힌다. 색을 피가 아니라 벚꽃으로 잡은 것은 이 게임이
     * 요괴를 베는 서정적인 화면을 목표로 하기 때문이고, 분홍은 사무라이의 붉은
     * 갑옷·흰 참격과 같은 계열 안에서 유일하게 비어 있던 자리다.
     *
     * **CombatFeel 예산을 그대로 받는다.** 공격속도가 초당 네 번에 가까워지면 꽃잎이
     * 화면에 계속 떠 있게 되는데, 그 상태에서는 개별 타격이 구분되지 않고 화면
     * 아래쪽이 분홍 얼룩이 된다. 참격과 같은 규칙으로 수명을 줄여서, 아무리 빨리
     * 공격해도 꽃잎이 차지하는 시간의 총량이 한계를 넘지 않게 한다.
     */
    public sealed class SakuraBurst : MonoBehaviour
    {
        [SerializeField] private SakuraPetal petalPrefab;
        [SerializeField] private Transform petalParent;

        [Header("아트")]
        [Tooltip("1~2px 꽃잎 스프라이트. 여러 개면 타격마다 섞어 쓴다")]
        [SerializeField] private Sprite[] petalSprites;

        [Tooltip("연한 분홍. 사양서의 #D9A7B0")]
        [SerializeField] private Color paleColor = new Color32(0xD9, 0xA7, 0xB0, 0xFF);

        [Tooltip("밝은 분홍. 사양서의 #F0CDD3. 두 색을 섞어야 조각들이 한 덩어리로 " +
                 "뭉치지 않고 깊이가 생긴다")]
        [SerializeField] private Color brightColor = new Color32(0xF0, 0xCD, 0xD3, 0xFF);

        [Header("발생")]
        [SerializeField] private int minPetals = 6;
        [SerializeField] private int maxPetals = 10;

        [Tooltip("튀어나가는 속도 범위 (world units/초)")]
        [SerializeField] private Vector2 speedRange = new Vector2(1.6f, 3.4f);

        [Tooltip("참격 방향을 중심으로 벌어지는 각도 (도). 부채꼴의 반각이다")]
        [SerializeField] private float spreadDegrees = 42f;

        [Tooltip("발생 지점의 랜덤 산포 (world units)")]
        [SerializeField] private float positionJitter = 0.18f;

        [Header("수명")]
        [Tooltip("보정 전 수명 범위 (초). 사양서는 0.4~0.6초")]
        [SerializeField] private Vector2 lifetimeRange = new Vector2(0.4f, 0.6f);

        [Tooltip("플레이 1초당 허용되는 꽃잎 표시 시간. 참격의 예산과 같은 규칙이다")]
        [SerializeField] private float lifetimeBudgetPerSecond = 0.5f;

        /**
         * @brief 수명의 하한. 예산이 이보다 짧게 요구해도 여기서 멈춘다.
         *
         * 참격은 수명만 줄여도 됐지만 꽃잎은 다르다. 참격은 큰 흰 호 하나라 0.13초만
         * 떠 있어도 "무언가 번쩍였다"로 읽히는데, 1~2px 조각은 그 시간에 눈이 따라갈
         * 수 없다. 상한 공격속도에서 예산대로 0.129초까지 줄이면 꽃잎이 흩날리는 것이
         * 아니라 타격 지점에서 분홍색이 점멸하는 것으로 보인다. 흩날림은 궤적이
         * 보여야 성립하고, 궤적에는 최소한의 시간이 필요하다.
         *
         * 0.25초는 60fps에서 15프레임이다. 조각이 화면을 가로지르며 몇 번 회전하기에
         * 충분한 최소치다.
         */
        [Tooltip("수명의 하한 (초). 이보다 짧으면 흩날림이 점멸로 보인다")]
        [SerializeField] private float minLifetime = 0.25f;

        [Tooltip("예산을 개수로 흡수할 때도 이보다 적게 내지는 않는다. 타격 자체가 " +
                 "안 보이는 것보다는 예산을 조금 넘기는 편이 낫다")]
        [SerializeField] private int minPetalsFloor = 3;

        [SerializeField] private Vector2 spinRange = new Vector2(-320f, 320f);

        [Header("풀")]
        [Tooltip("동시에 살아 있을 수 있는 꽃잎 수. 최대 발생 수의 서너 배가 필요하다 " +
                 "- 앞선 타격의 꽃잎이 아직 떠 있는 동안 다음 타격이 온다")]
        [SerializeField] private int prewarm = 40;

        private ObjectPool<SakuraPetal> pool;

        public int PoolGrowthCount { get { return pool != null ? pool.GrowthCount : 0; } }

        // ------------------------------------------------------------ 예산 진단
        //
        // 아래 셋은 화면을 보지 않고 예산이 실제로 어떻게 나뉘는지 읽기 위한
        // 것이다. 꽃잎은 1~2px이라 "지금 몇 개가 얼마나 떠 있는가"를 눈으로 셀 수
        // 없고, 그래서 수명이 점멸 수준으로 내려간 것도 한동안 알아채지 못했다.

        /** 이 공격속도에서 꽃잎 하나가 사는 시간. 수명 범위의 중앙값 기준 */
        public float LifetimeAt(float attacksPerSecond)
        {
            float requested = (lifetimeRange.x + lifetimeRange.y) * 0.5f;
            return Mathf.Max(minLifetime,
                CombatFeel.ScaledDuration(requested, lifetimeBudgetPerSecond, attacksPerSecond));
        }

        /** 이 공격속도에서 한 타격이 내는 꽃잎 수. 개수 범위의 중앙값 기준 */
        public int PetalsAt(float attacksPerSecond)
        {
            float requested = (lifetimeRange.x + lifetimeRange.y) * 0.5f;
            float budgeted = CombatFeel.ScaledDuration(requested, lifetimeBudgetPerSecond, attacksPerSecond);
            float lifetime = Mathf.Max(minLifetime, budgeted);
            float scale = lifetime > 0f ? Mathf.Clamp01(budgeted / lifetime) : 1f;

            int nominal = Mathf.RoundToInt((minPetals + maxPetals) * 0.5f);
            return Mathf.Max(minPetalsFloor, Mathf.RoundToInt(nominal * scale));
        }

        /**
         * @brief 화면에 동시에 떠 있는 꽃잎의 기대 개수.
         *
         * 이것이 예산이 실제로 지키는 값이다. 수명이든 개수든 어느 쪽으로 줄여도
         * 이 수가 유지되어야 한다.
         */
        public float ConcurrentPetalsAt(float attacksPerSecond)
        {
            return PetalsAt(attacksPerSecond) * LifetimeAt(attacksPerSecond) * attacksPerSecond;
        }

        private void Awake()
        {
            if (petalParent == null) petalParent = transform;
            if (petalPrefab != null) pool = new ObjectPool<SakuraPetal>(petalPrefab, petalParent, prewarm);
        }

        /**
         * @brief 타격 지점에서 꽃잎을 터뜨린다.
         *
         * direction은 참격이 지나간 방향이다. 사무라이는 오른쪽을 향해 베므로 보통
         * 오른쪽 위인데, 고정값 대신 받는 이유는 방향이 고정되면 여덟 번 연속 타격이
         * 같은 모양을 여덟 번 찍은 것으로 보이기 때문이다.
         *
         * attacksPerSecond는 수명 예산 계산에 쓴다. CombatFeel 참고.
         */
        public void Play(Vector3 position, Vector2 direction, float attacksPerSecond)
        {
            if (pool == null || petalSprites == null || petalSprites.Length == 0) return;

            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.right;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 예산은 "화면에 떠 있는 꽃잎의 총량"이다. 그 총량은 수명 x 개수이므로
            // 둘 중 아무 쪽으로 줄여도 예산은 지켜진다.
            //
            // 처음에는 참격과 같이 수명만 줄였는데, 상한 공격속도에서 0.129초까지
            // 내려가 흩날림이 점멸로 보였다(minLifetime 주석 참고). 그래서 수명은
            // 하한에서 멈추고, 남은 초과분을 개수로 흡수한다. 조각이 적어도 각각은
            // 눈으로 따라갈 수 있고, 총량은 그대로다.
            float requested = Random.Range(lifetimeRange.x, lifetimeRange.y);
            float budgeted = CombatFeel.ScaledDuration(requested, lifetimeBudgetPerSecond, attacksPerSecond);
            float lifetime = Mathf.Max(minLifetime, budgeted);

            // 수명을 하한에서 붙든 만큼 개수를 줄인다. budgeted == lifetime 이면 1이라
            // 낮은 공격속도에서는 아무것도 달라지지 않는다
            float countScale = lifetime > 0f ? Mathf.Clamp01(budgeted / lifetime) : 1f;

            int nominal = Random.Range(minPetals, maxPetals + 1);
            int count = Mathf.Max(minPetalsFloor, Mathf.RoundToInt(nominal * countScale));

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + Random.Range(-spreadDegrees, spreadDegrees);
                float speed = Random.Range(speedRange.x, speedRange.y);

                var velocity = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * speed,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * speed);

                var origin = position + new Vector3(
                    Random.Range(-positionJitter, positionJitter),
                    Random.Range(-positionJitter, positionJitter), 0f);

                var petal = pool.Get();
                petal.Play(petalSprites[Random.Range(0, petalSprites.Length)],
                           origin, velocity,
                           Random.Range(spinRange.x, spinRange.y),
                           lifetime,
                           Random.value < 0.5f ? paleColor : brightColor,
                           Release);
            }
        }

        private void Release(SakuraPetal petal)
        {
            pool.Release(petal);
        }
    }
}
