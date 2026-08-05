using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 사무라이의 체력. 보스만 이것을 깎는다.
     *
     * **잡몹은 플레이어를 공격하지 않는다.** 설계 결정이고 이유는 두 가지다.
     *
     *  - 파밍은 방치(AFK) 골드 수도꼭지다. 거기에 사망이 섞이면 화면을 보지 않는
     *    동안 수입이 새고, 플레이어는 왜 골드가 예상보다 적은지 알 수 없다.
     *  - 보스 도전은 수동 버튼이다. 그래서 사망은 **항상 플레이어가 보고 있는
     *    순간에만** 일어난다. 죽는 장면을 못 보면 "체력을 올려야 한다"는 신호가
     *    전달되지 않는다.
     *
     * 그 결과 성장 축의 역할도 깨끗하게 갈린다. 공격 계열은 파밍 속도와 보스
     * 화력을, 체력 계열은 보스 생존을 담당한다.
     *
     * 체력은 보스전 시작마다 가득 찬 상태로 시작한다. 전투 사이에 체력을
     * 들고 다니게 하면 "지금 도전하면 안 되는 상태"가 생기고, 그것을 알려면
     * 파밍 화면에 체력 바가 상시 떠 있어야 한다 - 아무 일도 일어나지 않는
     * 화면에 정보를 하나 더 얹는 것은 손해다.
     */
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator animator;

        [Header("애니메이션")]
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField] private float hurtFrameRate = 14f;
        [SerializeField] private float deathFrameRate = 10f;

        [Header("연출")]
        [Tooltip("피격 시 붉게 물드는 시간")]
        [SerializeField] private float hurtFlashSeconds = 0.14f;

        [Tooltip("쓰러질 때 흩날리는 꽃잎. 타격용 발생기를 그대로 쓴다")]
        [SerializeField] private SakuraBurst sakura;

        [SerializeField] private int deathPetalBursts = 3;

        private double maxHealth = 100d;

        /** 초당 회복 **비율** (최대 체력 대비). 절대량이 아니다 */
        private double regenFraction = 0.01d;

        private double current;
        private float flashRemaining;

        /** 지금 보스전 중이라 체력이 깎이는 상태인가 */
        private bool engaged;

        /** 체력이 0이 되는 순간 한 번 */
        public event Action Died;

        /** 체력이 바뀔 때마다. HUD가 듣는다 */
        public event Action Changed;

        public double MaxHealth { get { return maxHealth; } }
        public double Current { get { return current; } }

        /** 초당 회복 비율. 강화가 밀어넣는 값 */
        public double RegenFraction { get { return regenFraction; } }

        /** 지금 최대 체력 기준의 초당 절대 회복량. HUD와 실패 문구가 쓴다 */
        public double RegenPerSecond { get { return maxHealth * regenFraction; } }
        public bool IsAlive { get { return current > 0d; } }
        public bool IsEngaged { get { return engaged; } }

        public float Fraction
        {
            get { return maxHealth <= 0d ? 0f : Mathf.Clamp01((float)(current / maxHealth)); }
        }

        /**
         * @brief 이 전투에서 받은 총 피해.
         *
         * 실패 문구가 "몇 배 모자랐는가"를 계산할 때 쓴다. 남은 체력이 아니라
         * 받은 피해라야 죽은 뒤에도 값이 남는다.
         */
        public double DamageTaken { get; private set; }

        // ---------------------------------------------------------------- 스탯

        /** 강화가 스탯을 밀어넣는 진입점. 전투 중이면 비율을 유지한다 */
        public double MaxHealthStat
        {
            get { return maxHealth; }
            set
            {
                double next = System.Math.Max(1d, value);
                // 전투 중에 최대 체력이 오르면 남은 비율을 유지한다. 절대값을
                // 유지하면 강화가 회복처럼 동작하고, 0으로 리셋하면 강화가
                // 사망 원인이 된다
                double ratio = maxHealth > 0d ? current / maxHealth : 1d;
                maxHealth = next;
                current = engaged ? next * ratio : next;
                Raise();
            }
        }

        /** 강화가 밀어넣는 값. 초당 회복 **비율**이다 */
        public double RegenStat
        {
            get { return regenFraction; }
            set { regenFraction = System.Math.Max(0d, value); }
        }

        // ---------------------------------------------------------------- 전투

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
            current = maxHealth;
        }

        /** 보스전 시작. 가득 찬 상태로 연다 */
        public void BeginFight()
        {
            engaged = true;
            current = maxHealth;
            DamageTaken = 0d;
            flashRemaining = 0f;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            Raise();
        }

        /**
         * @brief 보스전 종료. 파밍으로 돌아가며 체력이 가득 찬다.
         *
         * 회복에 시간을 들이지 않는 이유는 그 시간이 플레이어에게 아무것도
         * 주지 않기 때문이다. 죽어서 돌아왔든 이겨서 돌아왔든 다음에 할 일은
         * 강화이고, 그 사이에 기다림을 끼워 넣으면 실패의 대가가 시간이 된다.
         */
        public void EndFight()
        {
            engaged = false;
            current = maxHealth;
            flashRemaining = 0f;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            Raise();
        }

        public void TakeDamage(double amount)
        {
            if (!engaged || amount <= 0d || !IsAlive) return;

            current -= amount;
            DamageTaken += amount;

            if (current <= 0d)
            {
                current = 0d;
                Die();
                return;
            }

            // 붉은 플래시 + 피격 애니메이션. **히트스톱은 걸지 않는다.**
            //
            // 히트스톱은 "내가 때렸다"를 무게로 바꾸는 장치다. 맞을 때도 걸면
            // 두 사건이 같은 언어로 말하게 되어, 화면만 봐서는 지금 때린 것인지
            // 맞은 것인지 구분되지 않는다. 보스전은 초당 네 번 때리는 중이라
            // 정지가 하나 더 끼면 그냥 끊김으로 읽힌다.
            flashRemaining = hurtFlashSeconds;

            if (hurtFrames != null && hurtFrames.Length > 0 && animator != null)
                animator.Play(hurtFrames, hurtFrameRate, false, null);

            Raise();
        }

        private void Die()
        {
            Raise();

            // 쓰러짐 연출은 새 아트를 만들지 않고 있는 것을 겹쳐 쓴다 - 사망
            // 애니메이션과 벚꽃 꽃잎이다. 타격마다 흩날리던 그 꽃잎이 이번에는
            // 플레이어에게서 진다
            if (sakura != null)
            {
                for (int i = 0; i < deathPetalBursts; i++)
                {
                    float angle = 90f + (i - deathPetalBursts * 0.5f) * 40f;
                    var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    sakura.Play(transform.position + Vector3.up * 0.6f, direction, 1f);
                }
            }

            if (deathFrames != null && deathFrames.Length > 0 && animator != null)
                animator.Play(deathFrames, deathFrameRate, false, null);

            var handler = Died;
            if (handler != null) handler();
        }

        private void Update()
        {
            if (flashRemaining > 0f && spriteRenderer != null)
            {
                flashRemaining -= Time.deltaTime;
                float t = Mathf.Clamp01(flashRemaining / Mathf.Max(0.0001f, hurtFlashSeconds));
                spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), t);
            }

            if (!engaged || !IsAlive) return;

            // 비율을 절대량으로 바꿔 적용한다. 체력을 올리면 회복량도 함께 오른다
            double perSecond = maxHealth * regenFraction;
            if (perSecond > 0d && current < maxHealth)
            {
                current = System.Math.Min(maxHealth, current + perSecond * Time.deltaTime);
                Raise();
            }
        }

        private void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
