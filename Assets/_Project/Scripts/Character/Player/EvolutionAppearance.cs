using System;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 전직 티어에 맞는 사무라이 클립 세트를 갈아 끼운다 (33단계).
     *
     * ## 프레임은 빌더가 굽는다
     *
     * 티어별 다섯 클립(idle/발도/달리기/피격/사망)을 EvolutionContentBuilder가
     * 카탈로그의 팩 폴더에서 잘라 씬에 명시적으로 기록한다. 런타임에 Resources
     * 로드를 하지 않는 이유는 다른 모든 아트와 같다 - 씬에 참조로 남아야
     * 빌드에 들어가고, 빠지면 빌더 검증이 잡는다.
     *
     * ## 반영 경로는 둘 뿐이다
     *
     * PlayerCombat.SetCharacterFrames / PlayerHealth.SetCharacterFrames.
     * 애니메이터를 직접 만지지 않는다 - 재생 주체가 둘이면 "진행 중인 클립을
     * 다시 재생하면 첫 프레임으로 튄다"는 26단계의 사고가 돌아온다.
     *
     * ## 진화 연출 (39단계에 다시 만듦)
     *
     * Evolved(티어가 오른 순간)에만 튄다. Changed(세이브 복원 포함)에는 모습만
     * 소리 없이 맞춘다 - 접속할 때마다 연출이 터지면 축하가 로딩 화면이 된다.
     *
     * 33단계에는 귀참의 ScreenFlash를 빌려 썼는데, 그 연출은 가운데를 일부러
     * 비워 둬서(귀참은 죽는 것이 보여야 한다) 화면 가장자리만 살짝 붉어졌고
     * 캡처에서 거의 보이지 않았다. 도약의 무게가 없었다.
     *
     * 지금은 세 박자다:
     *
     *   정지     HitStop이 게임 시간을 멈춘다. 전투가 멎고 이 순간만 남는다
     *   점멸     사무라이가 먹빛 실루엣으로 두어 번 깜박인다. 형태는 그대로인데
     *            색이 사라지는 것 - "무언가 일어나고 있다"의 그림이다
     *   백광     전면 백광(EvolveFlash)이 정점에서 시작해 걷히고, 걷힌 자리에
     *            새 모습이 서 있다. 꽃잎은 이때 터진다 - 정지가 풀리는 시점과
     *            맞물려 꽃잎이 곧바로 날린다
     *
     * 연출은 전부 unscaled로 돈다(정지 중에도 흘러야 한다). 게임플레이는
     * 건드리지 않는다 - HitStop은 23단계부터 쓰던 그 규칙 그대로다.
     */
    public sealed class EvolutionAppearance : MonoBehaviour
    {
        /** 티어 하나의 클립 세트. 빌더가 채운다 */
        [Serializable]
        public sealed class TierFrames
        {
            [Tooltip("표시용. 빌더가 카탈로그의 티어 이름을 적는다")]
            public string label;

            public Sprite[] idle;
            public Sprite[] attack;
            public Sprite[] run;
            public Sprite[] hurt;
            public Sprite[] death;
        }

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private PlayerHealth health;

        [Tooltip("인덱스 = 티어. 0(로닌)부터 6(진 데몬사무라이)까지 일곱 벌")]
        [SerializeField] private TierFrames[] tiers;

        [Header("진화 연출")]
        [SerializeField] private Onikiri.UI.ScreenFlash flash;

        [Tooltip("전면 백광 (39단계). 정점에서 시작해 걷히며 새 모습을 드러낸다")]
        [SerializeField] private Onikiri.UI.EvolveFlash whiteFlash;

        [Tooltip("점멸에 쓸 사무라이 렌더러. 비우면 자기 오브젝트에서 찾는다")]
        [SerializeField] private SpriteRenderer silhouetteRenderer;

        [Tooltip("진화 순간 흩날리는 꽃잎. 타격용 발생기를 그대로 쓴다")]
        [SerializeField] private SakuraBurst sakura;

        [Tooltip("꽃잎 버스트 횟수. 사망(3)보다 크게 - 축하가 죽음보다 작으면 안 된다")]
        [SerializeField] private int petalBursts = 5;

        /**
         * @brief 정지·점멸의 시간표. 직렬화하지 않는 이유는 세 값이 서로
         * 묶여 있어서다 - 정지는 점멸이 끝나고 백광이 터진 직후 풀려야 하고
         * (꽃잎이 곧바로 날리도록), 낱개로 조정하면 그 관계가 깨진다.
         */
        private const float BlinkOn = 0.07f;
        private const float BlinkOff = 0.05f;
        private const int BlinkCount = 3;
        private const float FreezeSeconds = BlinkCount * (BlinkOn + BlinkOff) + 0.04f;

        private EvolutionSystem evolution;
        private int appliedTier = -1;

        public int TierCount { get { return tiers != null ? tiers.Length : 0; } }

        public TierFrames GetTier(int index)
        {
            if (tiers == null || index < 0 || index >= tiers.Length) return null;
            return tiers[index];
        }

        private void Start()
        {
            evolution = EvolutionSystem.Instance;
            if (evolution != null)
            {
                evolution.Changed += Refresh;
                evolution.Evolved += PlayEvolveEffect;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (evolution != null)
            {
                evolution.Changed -= Refresh;
                evolution.Evolved -= PlayEvolveEffect;
            }
        }

        /** 현재 티어의 모습으로 맞춘다. 이미 그 모습이면 아무것도 안 한다 */
        private void Refresh()
        {
            int tier = evolution != null ? evolution.Tier : 0;
            if (tier == appliedTier) return;

            var frames = FramesFor(tier);
            if (frames == null) return;

            appliedTier = tier;

            if (combat != null) combat.SetCharacterFrames(frames.idle, frames.attack, frames.run);
            if (health != null) health.SetCharacterFrames(frames.hurt, frames.death);
        }

        /**
         * @brief 이 티어의 클립 세트. 비어 있으면 **아래 티어로 내려간다.**
         *
         * 빌더가 일곱 벌을 다 굽는 것이 정상이지만, 팩 하나가 빠져도 게임이
         * 투명 사무라이가 되면 안 된다 - 모습이 한 티어 뒤처지는 것과 캐릭터가
         * 사라지는 것 중 전자가 낫다.
         */
        private TierFrames FramesFor(int tier)
        {
            if (tiers == null) return null;

            for (int t = Mathf.Min(tier, tiers.Length - 1); t >= 0; t--)
            {
                var frames = tiers[t];
                if (frames != null && frames.idle != null && frames.idle.Length > 0) return frames;
            }
            return null;
        }

        private void PlayEvolveEffect(int newTier)
        {
            // 백광이 없으면(빌더를 아직 안 돌린 씬) 33단계 연출로 내려간다 -
            // 연출이 약한 것이 연출이 없는 것보다 낫다
            if (whiteFlash == null)
            {
                if (flash != null) flash.Play();
                BurstPetals();
                return;
            }

            HitStop.Request(FreezeSeconds);
            StartCoroutine(EvolveSequence());
        }

        /**
         * @brief 정지 → 실루엣 점멸 → 백광 + 꽃잎.
         *
         * Realtime 대기를 쓴다 - 이 코루틴은 자기가 건 정지(timeScale 0) 속에서
         * 돌기 때문에, 스케일 대기면 첫 프레임에서 영원히 멈춘다.
         */
        private System.Collections.IEnumerator EvolveSequence()
        {
            var renderer = silhouetteRenderer != null
                ? silhouetteRenderer : GetComponent<SpriteRenderer>();

            // 점멸: 먹빛 실루엣 <-> 원색. 새 모습은 Refresh가 이미 입혔으므로
            // 점멸이 그것을 가렸다 보였다 하는 것이 곧 "변신 중"이다
            for (int i = 0; i < BlinkCount; i++)
            {
                if (renderer != null) renderer.color = Color.black;
                yield return new WaitForSecondsRealtime(BlinkOn);

                if (renderer != null) renderer.color = Color.white;
                yield return new WaitForSecondsRealtime(BlinkOff);
            }

            if (renderer != null) renderer.color = Color.white;

            // 백광이 정점에서 시작하고, 그 뒤에서 엣지 번쩍이 여운을 잇는다.
            // 정지는 이 직후 풀리므로(FreezeSeconds) 꽃잎이 곧바로 날린다
            whiteFlash.Play();
            if (flash != null) flash.Play();
            BurstPetals();
        }

        private void BurstPetals()
        {
            if (sakura == null) return;

            // 사망 연출(PlayerHealth.deathPetalBursts)과 같은 다발 방식.
            // 방향을 위로 두는 이유는 진화가 상승이기 때문이다 - 베기(전방)
            // 도 죽음(사방)도 아닌 세 번째 방향이 이 연출의 서명이 된다
            for (int i = 0; i < petalBursts; i++)
                sakura.Play(transform.position, Vector2.up, 1f);
        }
    }
}
