using System;
using System.Collections.Generic;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 봉인한 혼을 잠깐 불러낸다. **요도의 정체성 기능.**
     *
     * ## 오의처럼 자동으로 나간다
     *
     * 쿨다운이 차면 알아서 소환된다. 방치형에서 "눌러야 나오는 것"은 화면을
     * 보고 있는 사람에게만 주는 보상이라, 자리를 비운 플레이어의 실제 DPS가
     * 시뮬레이션과 갈린다 - 26단계가 오의 자동 시전으로 확정한 규칙 그대로다.
     *
     * **쿨다운은 벨 것이 있을 때만 돈다.** SkillSystem과 같은 조건
     * (PlayerCombat.HasTargetInRange)을 읽는다. 보스에게 달려가는 5.3초 동안
     * 차오르면 도착하자마자 영체가 튀어나오고, 제한 시간 30초의 밸런스가
     * 그만큼 어긋난다.
     *
     * ## 새 아트가 0장이다
     *
     * 대요괴의 스프라이트를 그대로 쓴다 - 로스터가 이미 들고 있는 것
     * (BossConfig.Definition)이고, 영체가 "그 요괴"로 읽히려면 오히려 같은
     * 그림이어야 한다. 다른 것은 셋이다: **반투명**, **먹빛 틴트**, 그리고
     * **로닌 뒤에 선다**(SortingOrders.Spirit). 34~35단계가 배경과 아이콘에서
     * 확인한 "곱연산 틴트는 없는 채널을 못 만들지만 줄이는 것은 언제나 된다"를
     * 세 번째로 쓰는 자리다.
     *
     * ## 데미지는 오의와 같은 통로로 들어간다
     *
     * PlayerCombat.DeliverSkillHit이다. 치명타·초월·연격·불꽃·숫자·소리가
     * 전부 오의와 같은 코드에서 나야 시뮬레이션의 기대값(괄호 안에 더해지는
     * 항)과 화면이 같은 값을 낸다 - 펫이 자기 통로(ApplyHit)를 갖는 것과
     * 반대의 판단이고, 반대인 이유는 펫이 괄호 **밖**이기 때문이다
     * (CombatStats.SpiritRate 주석).
     *
     * ## 45b - 넷이 같은 연출이던 것을 갈랐다
     *
     * 45단계의 영체는 **데미지 통로만** 오의와 같았다. 화면에서는 반투명
     * 스프라이트가 솟아 떠 있고 숫자가 네 번 뜨는 것이 전부였다 - 참격도
     * 플래시도 히트스톱도 흔들림도 없었다. 30초에 한 번 나오는 요도의 정체성
     * 기능이 평타 네 대보다 조용했다.
     *
     * 부품은 전부 이미 있었다. 오의(SkillPerformer)가 쓰는 참격 풀·잔상 풀,
     * 평타(PlayerCombat)가 쓰는 불꽃 풀, 귀참이 쓰는 화면 번쩍, 그리고
     * SkillFeedback. 영체만 그 어느 것도 부르지 않고 있었다. 새로 만든 것은
     * 없고 **입구 셋만 열었다**(SpawnSlashAt / SpawnAfterimageAt / SpawnSparkAt).
     *
     * 갈라 놓는 것은 {@link Signature}다. 넷이 같은 연출을 받으면 "어느 영체가
     * 나왔는가"가 스프라이트로만 갈리는데, 30초에 한 번 나오는 것을 스프라이트로
     * 구분하라는 것은 사실상 구분하지 말라는 뜻이다.
     *
     * **흑야가 정점이다.** 넷 중 유일하게 자기도 검을 쓰는 요괴(다크 사무라이)라
     * 발도 연출이 거짓말이 되지 않는 유일한 자루이고, 상성이 "전 오의 소폭"이라
     * 파워 최적해가 아닌 것도 맞아떨어진다 - **제일 세서가 아니라 제일 멋있어서**
     * 고르는 자루가 하나쯤 있어야 몰아주기가 유일한 답이 아니게 된다.
     *
     * ## 밸런스는 한 줄도 안 바뀐다
     *
     * 배율·타수·쿨다운·지속은 YodoSpiritCurve 그대로다. SkillFeedback이 내는
     * 히트스톱은 스케일 타임을 세우는데, 영체의 진행(Advance)도 쿨다운도 이미
     * 스케일 타임이라 **타격 수도 총량도 그대로**다 - 오의가 26단계부터 같은
     * 자리에 서 있고, 그 예산은 CombatFeel이 초당으로 재고 있다(30초에 한 번
     * 0.24초면 평타 예산 0.3초/초의 2.7%다).
     */
    public sealed class SpiritSummon : MonoBehaviour
    {
        /**
         * @brief 자루 하나의 **연출 프로필**. 오의의 Choreography와 같은 자리다.
         *
         * 빌더가 YodoCatalog와 팩 시트에서 적는다(YodoPanelBuilder). 없어도
         * 동작한다 - 프로필이 안 잡히면 45단계의 연출(솟-떠-짐 + 숫자)이 그대로
         * 남는다. 폴백을 남기는 이유는 배선이 빠진 씬에서 영체가 아예 안 나오는
         * 것보다 조용히 수수한 편이 낫기 때문이다.
         *
         * 값을 스크립트 기본값이 아니라 빌더가 적는 것은 이 프로젝트의 규칙이다 -
         * 컴포넌트가 이미 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않는다.
         */
        [Serializable]
        public sealed class Signature
        {
            [Tooltip("어느 자루인지. YodoCatalog의 Id와 같아야 한다")]
            public string id;

            [Tooltip("영체 몸 크기. 최종 보스일수록 크다. 1이면 원본")]
            public float bodyScale = 1f;

            [Header("참격 (팩 애니)")]
            [Tooltip("타격마다 팩 참격을 띄우는가")]
            public bool usesSlash;

            [Tooltip("팩 시트에서 잘라낸 프레임들. 128x128 열 장")]
            public Sprite[] slashFrames;

            public float slashFrameRate = 30f;

            [Tooltip("원본 픽셀 대비 배수. **정수만** (PackSlash 주석)")]
            public float slashScale = 2f;

            [Tooltip("회전각 (도). 팩 원본 방향을 화면 방향으로 돌린다")]
            public float slashAngle;

            [Tooltip("영체 기준 참격 중심의 전방 거리 (월드 단위)")]
            public float slashForwardOffset = 1.2f;

            [Tooltip("참격 중심의 높이. **영체 발밑에서** 잰다. " +
                     "스프라이트 칸 한가운데가 아니다 - 칸은 요괴마다 여백이 달라 " +
                     "기준이 못 된다(SpawnSlash 주석)")]
            public float slashHeightOffset;

            [Tooltip("마지막 타격에만 벤다. 처형인의 '한 번에 끝낸다'")]
            public bool slashOnLastHitOnly;

            [Header("강림")]
            [Tooltip("소환 순간 화면 가장자리가 번쩍하는가. 귀참의 것을 빌린다")]
            public bool summonFlash;

            [Tooltip("그 번쩍의 색. 귀참과 갈라야 '또 귀참인가'가 되지 않는다")]
            public Color summonFlashTint = Color.white;

            [Tooltip("소환 자리에 불꽃 한 발. 평타 불꽃을 빌린다")]
            public bool summonSpark;

            [Tooltip("불꽃의 자리 보정. 영체 발치가 아니라 몸통에 찍히도록")]
            public Vector2 summonSparkOffset = new Vector2(0f, 0.35f);

            [Tooltip("강림 잔상 장수. 0이면 안 남긴다")]
            public int arrivalGhosts;

            [Tooltip("잔상이 뒤로 뻗는 거리 (월드 단위)")]
            public float arrivalGhostSpan = 0.9f;

            [Tooltip("잔상의 색. 알파가 가장 진한 한 장의 진하기다")]
            public Color arrivalGhostTint = new Color(0.42f, 0.36f, 0.62f, 0.5f);

            [Header("무게")]
            [Tooltip("마지막 타격의 히트스톱 배수. 0이면 안 멈춘다")]
            public float hitStopMultiplier;

            public float shakeMultiplier;

            [Tooltip("마지막이 아닌 타격의 흔들림 배수. 히트스톱은 주지 않는다 - " +
                     "다타 중간에 정지를 주면 '연속'이 아니라 '느려짐'이 된다")]
            public float perHitShakeMultiplier;
        }

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private BossFight bossFight;

        [Tooltip("영체 아트를 꺼내 올 곳. 대요괴 넷이 여기 있다")]
        [SerializeField] private BossRoster roster;

        [SerializeField] private SpriteRenderer spiritRenderer;
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private Onikiri.UI.SkillNameFlash nameFlash;

        [Tooltip("영체의 색. 먹빛 - 살아 있는 요괴와 갈라야 한다")]
        [SerializeField] private Color spiritTint = new Color(0.62f, 0.58f, 0.86f, 0.72f);

        [Tooltip("영체 데미지 숫자의 색. 플레이어 오의 셋·동료와 갈라야 한다")]
        [SerializeField] private Color numberTint = new Color32(0xC9, 0xA8, 0xFF, 0xFF);

        /**
         * @brief 로닌 기준 소환 자리. **어깨 뒤 위쪽이다.**
         *
         * 처음에 뒤쪽 지면(-1.35, 0.15)에 뒀다가 실기에서 물렸다 - 동료 셋이
         * 정확히 그 줄(x -1.9 / -2.5 / -2.9)에 서 있어서, 대요괴 아트가
         * 판다 위에 겹쳐 **누가 소환된 것인지 읽히지 않았다.**
         *
         * 위로 올린 것이 처방이고, 올리고 나니 두 가지가 함께 좋아졌다.
         * 지면에서 떨어지면 "서 있는 것"이 아니라 **떠 있는 것**으로 읽히고,
         * 그것이 영체다 - 같은 아트로 산 요괴와 죽은 요괴를 가르는 것이
         * 반투명·먹빛에 이어 세 번째 신호가 됐다.
         */
        [Tooltip("로닌 기준 소환 위치. 어깨 뒤 위쪽 - 동료 줄과 겹치지 않는다")]
        [SerializeField] private Vector2 summonOffset = new Vector2(-0.55f, 0.95f);

        [Tooltip("솟아오르는 시간. 이 동안은 때리지 않는다")]
        [SerializeField] private float riseSeconds = 0.35f;

        [Tooltip("사라지는 시간. 마지막 타격 뒤")]
        [SerializeField] private float fadeSeconds = 0.5f;

        [Tooltip("데미지 숫자 크기 배수. 정수만 (래스터 폰트)")]
        [SerializeField] private int numberSizeMultiple = 2;

        [Header("연출 (45b)")]
        [Tooltip("참격·잔상 풀의 주인. 영체는 자기 풀을 갖지 않고 여기서 빌린다")]
        [SerializeField] private SkillPerformer performer;

        [Tooltip("화면 가장자리 번쩍. 귀참이 쓰는 그것이다")]
        [SerializeField] private Onikiri.UI.ScreenFlash screenFlash;

        [Tooltip("자루별 연출 프로필. 비어 있으면 45단계의 수수한 연출로 돈다")]
        [SerializeField] private Signature[] signatures;

        /**
         * @brief 영체가 한 대에 쓸어내는 전방 거리 (45c).
         *
         * 일섬의 관통(4.6)과 같은 자리에 둔다. 영체가 더 멀리 벨 이유가 없고,
         * 한 화면에 두 종류의 "앞"이 있으면 어느 쪽이 맞는지 눈으로 못 가린다.
         */
        [Header("타격 범위 (45c)")]
        [Tooltip("전방으로 쓸어내는 거리 (월드 단위). 일섬 관통과 같은 값")]
        [SerializeField] private float sweepRange = 4.6f;

        [Tooltip("훑는 세로 폭. 떠 있는 도깨비불까지 닿아야 한다")]
        [SerializeField] private float sweepHeight = 2.8f;

        /** 다음 소환까지 찬 시간. 저장하지 않는다 - 오의 쿨다운과 같은 규칙 */
        private float timer;

        /** 몇 번째 소환인가. 로테이션 순번이고 역시 저장하지 않는다 */
        private int turn;

        private bool active;
        private float elapsed;
        private int nextHit;
        private int bladeIndex = -1;
        private double totalMultiplier;

        /** 이번 세션의 소환 횟수. 테스트 패널의 계측값이다 (SkillSystem.castCount와 같은 성질) */
        public int SummonCount { get; private set; }

        /** 지금 나와 있는가. 테스트 패널과 연출 검사가 읽는다 */
        public bool IsSummoned { get { return active; } }

        /** 지금 나와 있는(또는 다음에 나올) 영체의 카탈로그 번호. 없으면 -1 */
        public int CurrentBlade { get { return bladeIndex; } }

        /**
         * @brief 직전 타격이 **몇을 벴는가** (45c 진단).
         *
         * 라인 쓸기가 실제로 여럿을 때리는지는 화면의 숫자를 세는 것 말고는
         * 확인할 방법이 없었다. 보스전에서 1이 나오고 파밍에서 2~4가 나오면
         * 밸런스 근거("보스는 단일이라 밴드 불변")가 화면에서 확인된다.
         */
        public int LastSweepHits { get; private set; }

        public float CooldownFraction
        {
            get { return Mathf.Clamp01(timer / (float)YodoSpiritCurve.CooldownSeconds); }
        }

        private void Awake()
        {
            if (spiritRenderer == null) spiritRenderer = GetComponent<SpriteRenderer>();
            if (spiritRenderer != null)
            {
                spiritRenderer.sortingOrder = SortingOrders.Spirit;
                spiritRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (combat == null) return;

            if (active) { Advance(); return; }

            // 전투 정지 상태 존중. 등장 연출·클리어·실패 중에는 나오지 않고
            // 타이머도 쉰다 - PetCombat과 같은 처리다
            var phase = bossFight != null ? bossFight.Current : BossFight.Phase.Farming;
            if (phase == BossFight.Phase.Intro || phase == BossFight.Phase.Cleared
                || phase == BossFight.Phase.Failed) return;

            // 벨 것이 없으면 쿨다운도 멈춘다. SkillSystem.Update와 같은 문이고
            // 같은 이유다(달려가는 동안 차면 도착하자마자 몰아친다)
            if (!combat.HasTargetInRange) return;

            // 스케일 타임이다. 히트스톱 중에는 쿨다운도 언다
            timer += Time.deltaTime;
            if (timer < (float)YodoSpiritCurve.CooldownSeconds) return;

            if (TrySummon())
            {
                // 남은 시간을 이월한다. 0으로 되돌리면 프레임 경계에서 조금씩
                // 새어 실제 소환 횟수가 설계값보다 적어진다 - 오의와 같은 처리
                timer -= (float)YodoSpiritCurve.CooldownSeconds;
            }
            else
            {
                // 봉인한 요도가 없거나 아트가 없다. 쿨다운을 다 찬 채로
                // 붙들어 둔다 - 첫 봉인 직후에 곧바로 나오게
                timer = (float)YodoSpiritCurve.CooldownSeconds;
            }
        }

        // ---------------------------------------------------------------- 소환

        private bool TrySummon()
        {
            var yodo = YodoSystem.Instance;
            if (yodo == null) return false;

            int index = yodo.SpiritBladeForTurn(turn);
            if (index < 0) return false;

            if (!Summon(index)) return false;

            // 순번은 **실제로 나온 뒤에** 넘긴다. 아트가 빠진 요도에서
            // 미리 넘기면 그 자루가 로테이션에서 조용히 빠지고, 증상은
            // "특정 영체만 안 나온다"로 나타난다
            turn++;
            return true;
        }

        /**
         * @brief 이 자루를 실제로 무대에 올린다. **로테이션을 모른다.**
         *
         * 순번을 밖에 남긴 이유는 손님이 둘이기 때문이다 - 정상 경로
         * (TrySummon)는 순번대로 꺼내고, 테스트 패널(DebugSummonBlade)은
         * 지목해서 꺼내되 순번을 흐트러뜨리면 안 된다. 무대에 올리는 일과
         * 누구 차례인가는 다른 질문이다.
         */
        private bool Summon(int index)
        {
            var yodo = YodoSystem.Instance;
            if (yodo == null) return false;

            double multiplier = yodo.SpiritMultiplierOf(index);
            if (multiplier <= 0d) return false;

            // 강림할 때마다 새로 고른다. 로테이션이라 직전 영체가 남긴 값이
            // 다음 자루로 넘어가면 안 된다
            lastAttack = null;
            var frames = PickAttackFor(index);
            if (frames == null || frames.Length == 0) return false;

            SummonCount++;

            bladeIndex = index;
            totalMultiplier = multiplier;
            active = true;
            elapsed = 0f;
            nextHit = 0;

            var signature = SignatureFor(index);

            var position = combat.transform.position;
            transform.position = new Vector3(position.x + summonOffset.x,
                                             position.y + summonOffset.y, position.z);

            // 몸 크기는 자루가 정한다. 프로필이 없으면 원본 크기다 - 매번 다시
            // 쓰는 이유는 로테이션이라, 큰 흑야가 지나간 뒤 다음 영체가 그
            // 크기를 물려받으면 안 되기 때문이다
            transform.localScale = Vector3.one * BodyScaleOf(signature);

            if (spiritRenderer != null)
            {
                spiritRenderer.enabled = true;
                spiritRenderer.color = Transparent(spiritTint, 0f);

                // **영체는 아군이다 - 적(오른쪽)을 본다.**
                //
                // 이 한 줄을 적 쪽 규칙으로 적었다가 물렸다. 아트는 대요괴의
                // 것이 맞지만 **지금 어느 편에서 싸우는가**가 다르다 - 같은
                // 스프라이트를 두 진영이 나눠 쓰는 첫 자리라, 아트의 출처를
                // 따라가면 방향이 뒤집힌다. 화면에서는 소환된 영체가 요괴에게
                // 등을 돌리고 선다.
                //
                // 그래서 규칙을 이름으로 고른다(Facing 머리 주석) - 느낌표
                // 하나는 안 보이지만 Facing.Enemy라고 적힌 아군은 보인다
                spiritRenderer.flipX = Facing.Ally(ArtFacesLeft(index));
            }

            if (animator != null) animator.Play(frames, FrameRateFor(index), true);

            if (nameFlash != null)
                nameFlash.Play(YodoCatalog.Blades[index].SpiritName, numberTint);

            // 강림은 **애니메이터 뒤에** 온다. 잔상이 지금 그려진 스프라이트를
            // 복사하는데(SpawnArrival), Play가 첫 프레임을 꽂기 전에 부르면
            // 직전 영체의 마지막 프레임이 복사된다 - 28단계에 일섬 잔상이
            // 평타 프레임을 복사해 허공에 흰 칼궤적이 떠 있던 것과 같은 사고다
            PlayArrival(signature);

            return true;
        }

        // ---------------------------------------------------------------- 연출

        /** 자루의 연출 프로필. 없으면 null이고, 부르는 쪽이 45단계 연출로 떨어진다 */
        private Signature SignatureFor(int index)
        {
            if (signatures == null || index < 0 || index >= YodoCatalog.Count) return null;

            string id = YodoCatalog.Blades[index].Id;
            for (int i = 0; i < signatures.Length; i++)
                if (signatures[i] != null && signatures[i].id == id) return signatures[i];

            return null;
        }

        private static float BodyScaleOf(Signature signature)
        {
            return signature != null && signature.bodyScale > 0f ? signature.bodyScale : 1f;
        }

        /**
         * @brief 소환 순간의 세 박자 - 번쩍 · 불꽃 · 잔상.
         *
         * 셋 다 프로필이 켠 것만 난다. 30초에 한 번이라 남발이 아니고, 그
         * 빈도가 이 연출들을 여기서 쓸 수 있게 하는 유일한 근거다 - 평타에
         * 화면 번쩍을 붙일 수는 없다.
         */
        private void PlayArrival(Signature signature)
        {
            if (signature == null) return;

            if (signature.summonFlash && screenFlash != null)
                screenFlash.Play(signature.summonFlashTint);

            if (signature.summonSpark && combat != null)
                combat.SpawnSparkAt(new Vector3(
                    transform.position.x + signature.summonSparkOffset.x,
                    transform.position.y + signature.summonSparkOffset.y, 0f), false);

            SpawnArrival(signature);
        }

        /**
         * @brief 강림 잔상. **몸이 온 쪽(적의 반대편)에 자국을 남긴다.**
         *
         * 솟는 동안 몸은 알파 0에서 시작하므로(Advance의 rise), 소환 프레임에
         * 화면에 있는 것은 이 자국뿐이다 - 그래서 "본체보다 잔상이 진하다"가
         * 문제가 아니라 **그것이 곧 강림의 그림**이다. 몸이 다 뜰 무렵 자국은
         * 사라진다(Afterimage.lifetime 0.16초 < riseSeconds 0.35초).
         *
         * 세로가 아니라 가로로 뻗는다. 몸은 아래에서 솟는데 자국까지 아래에
         * 깔면 둘이 같은 축에서 겹쳐 "두 겹으로 솟는다"로 읽힌다.
         *
         * 층은 영체보다 **한 칸 더 뒤**다. Afterimage의 기본값(Player-1=49)은
         * 영체(48) 앞이라 그대로 두면 자국이 몸을 덮는다.
         */
        private void SpawnArrival(Signature signature)
        {
            if (performer == null || signature.arrivalGhosts <= 0) return;
            if (spiritRenderer == null || spiritRenderer.sprite == null) return;

            // 적이 있는 쪽의 반대로 뻗는다. 오의의 참격과 같은 판정을 쓴다
            var target = combat.FindTarget();
            bool mirror = target != null && target.FacingDirection > 0;
            float back = mirror ? signature.arrivalGhostSpan : -signature.arrivalGhostSpan;

            var position = transform.position;
            float scale = BodyScaleOf(signature);

            for (int i = 0; i < signature.arrivalGhosts; i++)
            {
                // 1이 가장 먼 자국이다. 멀수록 옅다 - 같은 진하기로 두면
                // 잔상이 아니라 영체가 여럿 선 그림이 된다
                float t = (i + 1) / (float)signature.arrivalGhosts;

                var tint = signature.arrivalGhostTint;
                tint.a *= Mathf.Lerp(1f, 0.4f, t);

                performer.SpawnAfterimageAt(spiritRenderer.sprite,
                    new Vector3(position.x + back * t, position.y, position.z),
                    spiritRenderer.flipX, tint, SortingOrders.Spirit - 1, scale);
            }
        }

        /**
         * @brief 타격의 참격. **영체 기준이다** - 사무라이가 아니다.
         *
         * SkillPerformer.SpawnSlash와 같은 산수인데 원점만 다르다. 영체는
         * 로닌의 어깨 뒤 위쪽에 따로 서 있으므로(summonOffset), 사무라이
         * 기준으로 놓으면 참격이 영체와 1u 가까이 어긋난다.
         *
         * 참격은 Vfx(100) 층이라 영체 몸(48) 앞에 뜬다. 의도대로다 - 베는
         * 것은 몸이 아니라 칼이고, 칼은 몸 앞에 있다.
         */
        private void SpawnSlash(Signature signature)
        {
            if (performer == null || signature == null) return;
            if (signature.slashFrames == null || signature.slashFrames.Length == 0) return;

            var target = combat.FindTarget();
            bool mirror = target != null && target.FacingDirection > 0;

            /**
             * 원점은 영체의 **발밑**이다. 한때 `spiritRenderer.bounds.center.y`,
             * 즉 스프라이트 칸의 한가운데를 썼는데 그것이 틀린 자리였다.
             *
             * 칸은 그려진 그림이 아니라 **여백까지 포함한 상자**다. 시트마다
             * 여백이 다르므로 칸 한가운데는 요괴마다 아무 데나 찍힌다:
             *
             *   처형인   (92/2 - 16)/32 x 1.00 = 발밑 +0.94u   <- 몸통쯤
             *   붉은눈   (108/2 - 12)/32 x 1.00 = 발밑 +1.31u   <- 몸통쯤
             *   흑야     (192/2 -  0)/32 x 1.25 = 발밑 +3.75u   <- **머리 위 하늘**
             *
             * 흑야만 튄 이유는 그 시트를 굽는 칸이 다섯 클립의 합집합이고
             * (YokaiSheetBaker), 그중 소멸이 옆으로 크게 흩어져 칸이 192px까지
             * 커졌기 때문이다. 그려진 요괴는 104px뿐인데 칸이 그 두 배다.
             *
             * 발밑은 여백과 무관하다 - 시트를 다시 굽든 클립을 더하든 안 움직인다.
             * 높이는 시그니처가 "발밑에서 칼날까지"로 들고 있다.
             */
            float originY = transform.position.y;

            // 전방 오프셋은 바라보는 쪽으로 간다. 부호를 안 뒤집으면 왼쪽을
            // 벨 때 참격이 등 뒤에 뜬다
            float forward = mirror ? -signature.slashForwardOffset : signature.slashForwardOffset;

            var anchor = new Vector3(transform.position.x + forward,
                                     originY + signature.slashHeightOffset, 0f);

            performer.SpawnSlashAt(signature.slashFrames, signature.slashFrameRate, anchor,
                                   signature.slashAngle, signature.slashScale, mirror);
        }

        /**
         * @brief 소환된 뒤의 진행. 솟고 - 때리고 - 사라진다.
         *
         * 타격은 **시각에 걸려 있다.** SkillPerformer가 안무를 시각으로
         * 다루는 것과 같은 규칙이고 같은 이유다 - 프레임이 몇 장 뜨든 타격
         * 수가 같아야 총량이 설계값과 어긋나지 않는다.
         */
        private void Advance()
        {
            elapsed += Time.deltaTime;

            while (nextHit < YodoSpiritCurve.SummonHits && elapsed >= HitTime(nextHit))
            {
                Deliver(nextHit);
                nextHit++;
            }

            float duration = (float)YodoSpiritCurve.DurationSeconds;

            if (elapsed < riseSeconds)
            {
                // 솟아오른다 - 알파와 높이가 함께 온다
                float t = riseSeconds > 0f ? elapsed / riseSeconds : 1f;
                SetAlpha(spiritTint.a * t);
                SetLift(Mathf.Lerp(-0.35f, 0f, t));
                return;
            }

            if (elapsed < duration)
            {
                SetAlpha(spiritTint.a);
                SetLift(0f);
                return;
            }

            float fade = fadeSeconds > 0f ? (elapsed - duration) / fadeSeconds : 1f;
            if (fade >= 1f) { Dismiss(); return; }

            SetAlpha(spiritTint.a * (1f - fade));
            SetLift(Mathf.Lerp(0f, 0.4f, fade));
        }

        /**
         * @brief index번째 타격이 나는 시각.
         *
         * 솟는 동안은 때리지 않으므로 그 뒤의 구간을 균등하게 나눈다.
         * 마지막 타격이 지속 시간의 **끝에 닿지 않게** 하나를 더 나눈 자리에
         * 두는 이유는, 끝에 붙이면 사라지기 시작하는 프레임에 타격이 들어가
         * "때리고 있는데 이미 없다"로 보이기 때문이다.
         */
        private float HitTime(int index)
        {
            float from = riseSeconds;
            float to = (float)YodoSpiritCurve.DurationSeconds;
            float span = Mathf.Max(0.05f, to - from);
            return from + span * (index + 1) / (YodoSpiritCurve.SummonHits + 1f);
        }

        /**
         * @brief 한 대. 데미지 - 참격 - 무게 순으로 난다.
         *
         * **연출은 대상이 없어도 난다.** 45단계에는 대상이 없으면 이 함수가
         * 통째로 빠져나갔는데, 연출이 붙은 지금은 그러면 안 된다 - 마지막
         * 요괴가 셋째 타격에 죽으면 넷째에서 참격도 히트스톱도 사라져 영체가
         * 허공에서 조용히 꺼진다. SkillPerformer.Deliver가 같은 자리에서 같은
         * 판단을 한다("데미지만 0으로 지나간다").
         */
        private void Deliver(int hitIndex)
        {
            var signature = SignatureFor(bladeIndex);
            bool last = hitIndex == YodoSpiritCurve.SummonHits - 1;

            // 이번 타격의 그림으로 넘어간다. 첫 타격은 강림 때 고른 것을
            // 그대로 쓰지 않고 여기서 한 번 더 고른다 - 솟는 0.35초 동안
            // 이미 그 그림이 돌았으므로, 벨 때 같은 것이 또 오면 "베었다"가
            // 아니라 "계속 같은 자세"로 읽힌다
            AdvanceAttackClip();

            double share = YodoSpiritCurve.HitShare(hitIndex, totalMultiplier);

            // **전방 일렬을 벤다** (45c). 45단계에는 FindTarget으로 한 마리만
            // 찍었는데, 최종 보스가 강림해 앞의 하나만 콕 찌르는 그림이었다.
            // 발도는 무리를 가르는 동작이고, 화면의 참격도 이미 그 폭으로
            // 그려진다 - 맞는 것이 하나뿐이면 그림과 결과가 어긋난다.
            //
            // **대상당 배율은 그대로다.** 나눠 갖지 않는 것이 오의 광역과 같은
            // 규칙이고(SkillPerformer 머리 주석), 그래서 보스 밴드가 움직이지
            // 않는다 - 보스전은 언제나 대상이 하나라 라인이든 단일이든 보스가
            // 받는 총량이 같다. 잡몹 쪽은 여럿 맞지만 파밍 속도는 DPS가 아니라
            // 요괴 공급(SpawnPacing 하한)에 묶여 있어 폭주하지 않는다.
            //
            // 기준점은 **사무라이**다. 영체 자신이 아니다 - 영체는 어깨 뒤
            // 위쪽에 떠 있어서 자기 자리로 재면 앞이 0.55u 짧아지고 세로 창은
            // 1u 높이 뜬다(그러면 지면의 요괴를 지나친다). "앞"은 부대의 앞이지
            // 영체의 앞이 아니다.
            // 출처를 **Special**로 밝힌다. 영체는 오의가 아니라 요도의 특수
            // 규칙이고, `TrialPowerScore`가 둘을 다른 항으로 센다
            // (`Skills` / `BossApplicableSpecials`) - 감사 표가 점수 표와 같은
            // 칸을 세게 하려면 여기서 갈라야 한다
            LastSweepHits = combat.DeliverSkillLane(
                combat.transform.position.x,
                performer != null ? performer.LaneOriginY : combat.transform.position.y,
                sweepRange, sweepHeight,
                BigDouble.FromDouble(share), numberTint, numberSizeMultiple,
                Onikiri.Progression.TrialDamageScale.Source.Special);

            if (signature == null) return;

            if (signature.usesSlash && (last || !signature.slashOnLastHitOnly))
                SpawnSlash(signature);

            // 무게는 마지막 타격에만. 중간 타격에 정지를 주면 5초 동안 화면이
            // 네 번 끊기고, 그것은 '몰아친다'가 아니라 '느려진다'이다 -
            // 오의가 26단계에 확정한 규칙 그대로다
            if (last)
                combat.SkillFeedback(signature.hitStopMultiplier, signature.shakeMultiplier);
            else
                combat.SkillFeedback(0f, signature.perHitShakeMultiplier);
        }

        private void Dismiss()
        {
            active = false;
            elapsed = 0f;
            nextHit = 0;

            // 크기를 되돌린다. 다음 소환이 어차피 다시 쓰지만, 꺼진 채로
            // 큰 크기를 들고 있으면 인스펙터가 거짓말을 한다
            transform.localScale = Vector3.one;

            if (animator != null) animator.Stop();
            if (spiritRenderer != null) spiritRenderer.enabled = false;
        }

        // ---------------------------------------------------------------- 아트

        private BossConfig ConfigFor(int index)
        {
            if (roster == null || index < 0 || index >= YodoCatalog.Count) return null;

            // 그 혼을 남기는 대요괴가 서는 스테이지에서 꺼낸다. 애셋을 직접
            // 참조하지 않는 이유는 42단계의 세계 순환 때문이다 - 로스터가
            // 단일 출처이고, 여기서 다시 매핑하면 같은 산수가 두 곳에 산다
            return roster.BossForStage(YodoCurve.FirstDropStage(index));
        }

        private Sprite[] FramesFor(int index)
        {
            var config = ConfigFor(index);
            var definition = config != null ? config.Definition : null;
            if (definition == null) return null;

            // 공격 클립이 있으면 그것을 쓴다. 영체는 때리러 나온 것이므로
            // idle로 서 있으면 "나타났는데 아무것도 안 한다"로 읽힌다
            if (definition.attackFrames != null && definition.attackFrames.Length > 0)
                return definition.attackFrames;
            return definition.idleFrames;
        }

        /** 직전에 쓴 공격. 연속으로 같은 것이 나오지 않게 하는 데만 쓴다 */
        private Sprite[] lastAttack;

        /**
         * @brief 이 영체의 공격 그림 하나를 고른다.
         *
         * 원본 보스가 공격을 여러 벌 들고 있으면(다크 사무라이) 그중 하나다.
         * 한 벌뿐인 영체는 늘 같은 것이 나오고, 그것이 지금까지의 동작이다.
         *
         * 고르는 규칙은 보스와 같다 - **직전 것은 후보에서 뺀다**(Enemy 주석).
         */
        private Sprite[] PickAttackFor(int index)
        {
            var config = ConfigFor(index);
            var definition = config != null ? config.Definition : null;
            if (definition == null) return null;

            var clips = new List<Sprite[]>();
            if (definition.attackFrames != null && definition.attackFrames.Length > 0)
                clips.Add(definition.attackFrames);

            if (definition.attackVariants != null)
            {
                foreach (var variant in definition.attackVariants)
                    if (variant != null && variant.frames != null && variant.frames.Length > 0)
                        clips.Add(variant.frames);
            }

            if (clips.Count == 0) return FramesFor(index);
            if (clips.Count == 1) { lastAttack = clips[0]; return clips[0]; }

            // 직전 것을 뺀 나머지에서
            var pool = new List<Sprite[]>();
            foreach (var clip in clips) if (clip != lastAttack) pool.Add(clip);
            if (pool.Count == 0) pool = clips;

            var picked = pool[UnityEngine.Random.Range(0, pool.Count)];
            lastAttack = picked;
            return picked;
        }

        /**
         * @brief 타격마다 다음 공격 그림으로 넘어간다.
         *
         * 한 소환에 네 번 베는데(YodoSpiritCurve.SummonHits) 타격 간격이
         * 0.93초이고 공격 클립은 0.5~0.67초다 - 한 번씩 끝까지 돌고 다음으로
         * 넘어갈 여유가 있다. 그래서 다크 사무라이 영체는 **한 번 강림에
         * 서로 다른 공격 넷**을 보여준다.
         *
         * 공격이 한 벌뿐인 영체(등롱·처형인·적안)에서는 고른 것이 지금 도는
         * 것과 같으므로 아무 일도 하지 않는다 - 같은 클립을 다시 걸면 루프가
         * 처음으로 튀어 멀쩡하던 연출이 끊긴다.
         */
        private void AdvanceAttackClip()
        {
            if (animator == null) return;

            var next = PickAttackFor(bladeIndex);
            if (next == null || next.Length == 0) return;
            if (next == animator.CurrentClip) return;

            animator.Play(next, FrameRateFor(bladeIndex), true);
        }

        private float FrameRateFor(int index)
        {
            var config = ConfigFor(index);
            var definition = config != null ? config.Definition : null;
            return definition != null && definition.frameRate > 0f ? definition.frameRate : 12f;
        }

        private bool ArtFacesLeft(int index)
        {
            var config = ConfigFor(index);
            var definition = config != null ? config.Definition : null;
            return definition != null && definition.artFacesLeft;
        }

        // ---------------------------------------------------------------- 조각

        private void SetAlpha(float alpha)
        {
            if (spiritRenderer == null) return;
            spiritRenderer.color = Transparent(spiritTint, alpha);
        }

        private void SetLift(float lift)
        {
            if (combat == null) return;

            var position = combat.transform.position;
            transform.position = new Vector3(position.x + summonOffset.x,
                                             position.y + summonOffset.y + lift, position.z);
        }

        private static Color Transparent(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        // ---------------------------------------------------------------- 테스트 패널

        /**
         * @brief 쿨다운을 무시하고 지금 소환한다. **테스트 패널 전용.**
         *
         * 상태를 직접 바꾸지 않고 **실제 소환 경로를 그대로 태운다** -
         * SkillSystem.DebugCastNow와 같은 규칙이다. 이 버튼으로 본 화면이
         * 실제 플레이의 화면과 같다고 말할 수 있어야 한다.
         *
         * @return 실제로 나왔으면 true. 봉인한 요도가 없으면 false다
         */
        public bool DebugSummonNow()
        {
            if (active) return false;
            if (!TrySummon()) return false;

            timer = 0f;
            return true;
        }

        /** 쿨다운을 채워둔다. 다음 프레임에 나간다 */
        public void DebugFillCooldown()
        {
            timer = (float)YodoSpiritCurve.CooldownSeconds;
        }

        /**
         * @brief **이 자루**를 지금 소환한다. 테스트 패널 전용.
         *
         * 45b의 연출 차등 때문에 필요해졌다. "지금 소환"은 로테이션의 다음
         * 순번을 내는데, 넷이 돌아가므로 흑야를 보려면 최악의 경우 네 번을
         * 눌러 셋을 흘려보내야 한다 - 그 사이 각 영체가 5초를 쓰므로 확인
         * 하나에 20초가 든다.
         *
         * 로테이션 순번(turn)은 **건드리지 않는다.** 여기서 밀면 이 버튼을
         * 누른 뒤의 실제 로테이션이 어긋나서, 치트로 본 것과 플레이의 순서가
         * 갈린다. 소환 경로 자체는 TrySummon과 같은 코드를 지난다.
         *
         * @return 실제로 나왔으면 true. 봉인 전이거나 아트가 없으면 false다
         */
        public bool DebugSummonBlade(int index)
        {
            if (active) return false;
            if (index < 0 || index >= YodoCatalog.Count) return false;
            if (!Summon(index)) return false;

            timer = 0f;
            return true;
        }
    }
}
