using System;
using System.Collections.Generic;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 오의 한 번이 타격을 **시간과 공간에 어떻게 뿌리는가**.
     *
     * ## 왜 PlayerCombat에서 갈라졌는가
     *
     * 26단계에는 `PlayerCombat.CastSkill` 하나가 겨냥·한 방·정지·흔들림을 전부
     * 했다. 셋 다 단일 대상 한 방이라 그것으로 충분했고, 그 결과가 "색만 다른
     * 같은 아크"였다.
     *
     * 오의마다 타격이 시간(연참 세 대)과 공간(일섬 관통, 귀참 광역)에 펴지면서
     * 세 연출의 주기가 서로 갈라졌다 - 연참은 타격이 셋인데 정지는 마지막 한 번,
     * 귀참은 타격이 여럿인데 화면 정지는 한 번이다. 한 함수에 두면 "한 대"와
     * "한 시전"이 섞인다.
     *
     *   PlayerCombat    한 대가 무엇을 하는가 (피해·불꽃·꽃잎·숫자·소리)
     *   SkillPerformer  한 시전이 그 대를 어떻게 뿌리는가 (안무·정지·흔들림·화면)
     *
     * ## 밸런스 가드
     *
     * **총 데미지는 바뀌지 않는다.** 배율·쿨다운·상한은 26단계 값 그대로다.
     *
     *   연참  배율을 셋으로 나눈다. 마지막이 나머지를 받아 합이 **정확히** 같다
     *   일섬  경로의 각 대상이 총 배율. 보스는 단일 대상이라 한 번
     *   귀참  화면의 각 대상이 총 배율. 보스는 단일 대상이라 한 번
     *
     * 보스전은 언제나 대상이 하나이므로 **어느 거동이든 보스에게 들어가는 총량이
     * 같다.** 그래서 보스 여유 밴드가 불변이고, StageSimulation을 고칠 필요가 없다.
     *
     * 잡몹 쪽은 관통·광역이 여럿을 때리므로 파밍이 빨라질 수 있다. 잡몹은 이미
     * 거의 즉사라 실제 영향은 미미해야 하고, 그것을 시뮬레이션이 아니라
     * **처치 속도의 상한**이 보증한다 - 파밍 속도는 요괴 공급(SpawnPacing 하한
     * 0.4초)에 묶여 있어서 DPS가 아무리 높아도 그 아래로 내려가지 않는다.
     *
     * ## 히트스톱 중에는 안무도 멈춘다
     *
     * 경과 시간을 스케일 타임으로 잰다. 애니메이션·불꽃·숫자가 모두 스케일
     * 타임이므로(23단계), 안무만 unscaled로 돌면 정지 중에 타격이 들어가고
     * 화면에는 아무 일도 일어나지 않는다.
     */
    public sealed class SkillPerformer : MonoBehaviour
    {
        /**
         * @brief 무기 티어 하나의 참격 프레임 (51단계).
         *
         * Sprite[][]는 유니티가 직렬화하지 못해서 한 겹 싼 것이 전부다.
         * 배열의 인덱스 + 1이 곧 티어다 - WeaponVfxTier.SlashColorOf의 램프
         * 순서대로 빌더가 적는다.
         */
        [Serializable]
        public sealed class TierSlash
        {
            public Sprite[] frames;
        }

        /**
         * @brief 오의 하나의 안무. 빌더가 SkillCatalog와 클립 실측에서 적는다.
         *
         * 타격 프레임을 초가 아니라 **클립 프레임 번호**로 두는 것이 요점이다.
         * 원화가가 그린 참격이 몇 번째 프레임에 있는지가 정답이고(23단계), 초로
         * 적어두면 재생 속도를 바꾸는 순간 타격이 그림에서 떨어진다.
         */
        [Serializable]
        public sealed class Choreography
        {
            [Tooltip("어느 오의인지. SkillCatalog의 id와 같아야 한다")]
            public string id;

            [Tooltip("이 오의가 재생할 클립. 연참은 ATTACK 1/2/3을 이어 붙인 20프레임")]
            public Sprite[] clip;

            public float clipFrameRate = 24f;

            [Tooltip("타격이 나는 클립 프레임 번호. 그려진 참격 프레임과 같아야 한다")]
            public int[] hitFrames;

            [Header("참격 (팩 애니)")]
            [Tooltip("팩 참격을 재생하는가. **연참은 쓰지 않는다** - 클립에 이미 " +
                     "궤적이 세 번 그려져 있고, 그 위에 얹으면 23단계의 이중 참격이다")]
            public bool usesSlash;

            [Tooltip("팩 시트에서 잘라낸 프레임들. 128x128 열 장")]
            public Sprite[] slashFrames;

            [Tooltip("무기 티어(1~5)별 참격 프레임 (51단계). 비어 있으면 위의 " +
                     "slashFrames를 티어와 무관하게 쓴다. 순서가 곧 티어다")]
            public TierSlash[] slashTierFrames;

            [Tooltip("참격 재생 속도. 클립 길이 안에서 끝나야 한다")]
            public float slashFrameRate = 30f;

            [Tooltip("원본 픽셀 대비 배수. **정수만** - 소수 배율은 점 필터링에서 " +
                     "픽셀 크기가 들쭉날쭉해진다. 2.5를 넘기면 27단계의 네모가 돌아온다")]
            public float slashScale = 2f;

            [Tooltip("회전각 (도). 팩 원본 방향을 화면 방향으로 돌린다")]
            public float slashAngle;

            /**
             * @brief 참격 조각에 곱할 색. **기존 여덟은 흰색이라 안 바뀐다.**
             *
             * 기존 오의의 조각은 혈 램프를 지나 붉게 구워져 있어 색이 이미
             * 그림 안에 있다. 신규 일곱은 검식(청)과 귀오의(금)가 섞여 한
             * 램프로 못 덮으므로 **은백으로 굽고 여기서 물들인다**
             * (PozacVfxBaker의 신규 일곱 주석).
             *
             * 흰색이 기본값인 것이 요점이다 - 곱연산에서 흰색은 항등원이라,
             * 안무 표에서 이 줄을 안 적으면 기존 동작 그대로다.
             */
            public Color slashTint = Color.white;

            [Tooltip("사무라이 기준 참격 중심의 전방 거리 (월드 단위)")]
            public float slashForwardOffset = 1.6f;

            [Tooltip("참격 중심의 높이 보정. 사무라이 그려진 중심에서 위로")]
            public float slashHeightOffset;

            [Header("돌진 섬광 (일섬)")]
            [Tooltip("얇은 수평 섬광을 그리는가. 돌진과 함께 자란다")]
            public bool usesStreak;

            [Tooltip("두께. **얇게** - 굵으면 다시 덩어리다")]
            public float streakThickness = 0.12f;

            [Tooltip("섬광의 높이. 사무라이 그려진 중심에서. 요괴 몸통을 지나야 한다")]
            public float streakHeightOffset;

            [Tooltip("섬광 색. 흰빛에 붉은 기가 도는 쪽")]
            public Color streakColor = Color.white;

            [Tooltip("다 자라는 시간. **돌진이 나가는 시간과 같아야** 머리가 칼끝에 붙는다")]
            public float streakRevealSeconds = 0.09f;

            public float streakHoldSeconds = 0.05f;
            public float streakFadeSeconds = 0.12f;

            [Tooltip("돌진 중 남길 잔상 수. 0이면 안 남긴다")]
            public int afterimageCount;

            [Header("거동")]
            [Tooltip("관통 사거리 (월드 단위). Pierce만 쓴다")]
            public float pierceRange = 4.6f;

            [Tooltip("관통이 훑는 세로 폭. 떠 있는 도깨비불까지 닿아야 한다")]
            public float pierceHeight = 2.4f;

            [Tooltip("돌진 거리. Pierce만 쓴다. 앵커로 돌아온다")]
            public float lungeDistance;

            [Tooltip("돌진해 나가는 시간")]
            public float lungeOutSeconds = 0.10f;

            [Tooltip("앵커로 돌아오는 시간. 나가는 것보다 길어야 '돌아왔다'로 읽힌다")]
            public float lungeBackSeconds = 0.16f;

            [Header("무게")]
            [Tooltip("마지막 타격의 히트스톱 배수")]
            public float hitStopMultiplier = 1.6f;

            public float shakeMultiplier = 1.6f;

            [Tooltip("마지막이 아닌 타격의 흔들림 배수. 다타가 끊겨 보이지 않게 " +
                     "히트스톱은 주지 않고 흔들림만 짧게 준다")]
            public float perHitShakeMultiplier = 0.5f;

            [Tooltip("데미지 숫자의 크기 배수. 정수만 (래스터 폰트)")]
            public int numberSizeMultiple = 2;

            // ------------------------------------------------------ 15종 재설계: 신규 거동

            [Tooltip("Around의 판정 반경 (월드 단위). 원이라 시전자 뒤쪽도 든다")]
            public float aroundRadius = 3.0f;

            [Tooltip("Field 장판의 가로 사거리. 관통과 같은 창이지만 자리가 굳는다")]
            public float fieldRange = 3.6f;

            [Tooltip("Field 장판의 세로 폭")]
            public float fieldHeight = 2.6f;

            [Tooltip("Pull이 대상을 찾는 전방 거리. 화면 우단까지가 4.575u다")]
            public float pullRange = 5.0f;

            [Tooltip("Pull이 대상을 옮길 자리 (시전자 기준 전방 오프셋). " +
                     "평타 사거리 2.9u 안이라 끌어모은 뒤 모든 오의가 닿는다")]
            public float pullDestinationOffset = 1.6f;

            [Tooltip("Pull이 대상을 끌어오는 데 걸리는 시간. 첫 타격보다 짧아야 " +
                     "폭발이 '모인 뒤에' 터진다")]
            public float pullSeconds = 0.25f;

            [Header("화면 번쩍")]
            [Tooltip("풀스크린 번쩍을 내는가. 귀참·혈폭·귀왕강림")]
            public bool screenFlash;

            /**
             * @brief 번쩍의 가장자리 색.
             *
             * 셋이 같은 흰 번쩍을 쓰면 세 개의 큰 사건이 화면에서 한 연출로
             * 읽힌다. 귀참·혈폭은 흰색 그대로 두고(기존 동작 불변) 귀왕강림만
             * 자기 참격 색으로 물들여 가른다.
             */
            public Color flashTint = Color.white;

            /**
             * @brief 번쩍의 **지속** 배수. 세기는 안 건드린다.
             *
             * 세기(edgePeak 0.85 / vignettePeak 0.88)에 곱하면 1을 넘어 잘리고,
             * 잘리면 "더 세게"가 화면에서 안 읽힌다. 세기의 차이는 색이 말하고
             * 이 값은 시간만 늘린다.
             */
            public float flashScale = 1f;

            /** 클립 전체 길이 (초) */
            public float ClipSeconds
            {
                get
                {
                    if (clip == null || clip.Length == 0 || clipFrameRate <= 0f) return 0f;
                    return clip.Length / clipFrameRate;
                }
            }

            /** index번째 타격이 나는 시각 (초) */
            public float HitTime(int index)
            {
                if (hitFrames == null || hitFrames.Length == 0 || clipFrameRate <= 0f) return 0f;
                int clamped = Mathf.Clamp(index, 0, hitFrames.Length - 1);
                return hitFrames[clamped] / clipFrameRate;
            }

            public int HitCount { get { return hitFrames != null ? hitFrames.Length : 0; } }
        }

        [SerializeField] private PlayerCombat combat;
        [SerializeField] private SpriteRenderer samuraiRenderer;
        [SerializeField] private Transform vfxParent;

        [SerializeField] private PackSlash slashPrefab;
        [SerializeField] private int slashPrewarm = 3;

        [SerializeField] private DashStreak streakPrefab;
        [SerializeField] private int streakPrewarm = 2;

        [SerializeField] private Afterimage afterimagePrefab;
        [SerializeField] private int afterimagePrewarm = 4;

        [Tooltip("잔상의 색. 알파가 가장 진한 한 장의 진하기다")]
        [SerializeField] private Color afterimageTint = new Color(0.75f, 0.85f, 1f, 0.5f);

        [SerializeField] private Onikiri.UI.SkillNameFlash nameFlash;
        [SerializeField] private Onikiri.UI.ScreenFlash screenFlash;

        [Tooltip("무기 티어 스파크가 든 라이브러리 (51단계). 은백으로 구운 " +
                 "Pozac 스파크 두 조각 - 티어 색은 재생할 때 물든다")]
        [SerializeField] private VfxLibrary glowLibrary;

        [SerializeField] private Choreography[] choreographies;

        private ObjectPool<PackSlash> slashPool;
        private ObjectPool<DashStreak> streakPool;
        private ObjectPool<Afterimage> afterimagePool;

        /** 진행 중인 시전. 오의당 하나뿐이다 - 쿨다운이 클립보다 길다 */
        private readonly List<ActiveCast> active = new List<ActiveCast>();

        private sealed class ActiveCast
        {
            public int skillIndex;
            public Choreography choreography;
            public double totalMultiplier;
            public Color tint;
            public float elapsed;
            public int nextHit;

            /** 다음에 흩뿌릴 잔상 번호. 타격과 같은 방식으로 시각에 걸어 둔다 */
            public int nextGhost;

            /** 시전 순간의 사무라이 X. 돌진 오프셋이 얹히기 전 값이라 경로의 기준이다 */
            public float baseX;

            // -------------------------------------------------- 15종 재설계: 신규 거동

            /**
             * @brief 장판이 깔린 자리. **시전 순간에 굳는다.**
             *
             * 매 틱 사무라이의 지금 위치를 쓰면 장판이 따라다니고, 그러면 그것은
             * 장판이 아니라 오라다. "위치에 걸린다"가 남은 틱 규칙(대상이 죽어도
             * 재타깃 안 함)의 근거이므로 자리부터 굳혀야 앞뒤가 맞는다.
             */
            public float fieldOriginX;
            public float fieldOriginY;

            /**
             * @brief 흡인이 확정한 대상 목록. **시전 시각에 굳는다.**
             *
             * 목록을 안 굳히고 폭발 시점에 다시 고르면 흡인 도중에 스폰된 적이
             * 끼어들고, 그러면 "끌어모은 것만 맞는다"가 거짓이 된다. 상한
             * (PullTargetCount)도 그 순간의 수라 뜻이 흐려진다.
             */
            public readonly List<Enemy> captured = new List<Enemy>();

            /** 끌어오기가 끝나는 시각 (초). 이 시각까지 대상이 도착점으로 흐른다 */
            public float pullEndsAt;

            /** 각 대상의 출발 X. 도착점까지 보간한다 */
            public readonly List<float> capturedFromX = new List<float>();

            /** 흡인 도착점의 월드 X */
            public float pullDestinationX;
        }

        public int SlashPoolGrowthCount
        {
            get
            {
                return (slashPool != null ? slashPool.GrowthCount : 0)
                     + (streakPool != null ? streakPool.GrowthCount : 0)
                     + (afterimagePool != null ? afterimagePool.GrowthCount : 0);
            }
        }

        /** 지금 안무가 돌고 있는 시전 수. 테스트 패널이 읽는다 */
        public int ActiveCastCount { get { return active.Count; } }

        private void Awake()
        {
            if (combat == null) combat = GetComponent<PlayerCombat>();
            if (samuraiRenderer == null) samuraiRenderer = GetComponent<SpriteRenderer>();
            if (vfxParent == null) vfxParent = transform;

            if (slashPrefab != null)
                slashPool = new ObjectPool<PackSlash>(slashPrefab, vfxParent, slashPrewarm);

            if (streakPrefab != null)
                streakPool = new ObjectPool<DashStreak>(streakPrefab, vfxParent, streakPrewarm);

            if (afterimagePrefab != null)
                afterimagePool = new ObjectPool<Afterimage>(afterimagePrefab, vfxParent, afterimagePrewarm);
        }

        private Choreography Find(string id)
        {
            if (choreographies == null) return null;
            foreach (var c in choreographies)
                if (c != null && c.id == id) return c;
            return null;
        }

        // ---------------------------------------------------------------- 시전

        /**
         * @brief 오의 하나를 시전한다.
         *
         * 첫 타격은 **클립의 타격 프레임에서** 나므로 이 함수는 즉시 피해를 주지
         * 않는다. 그래도 사거리 확인은 지금 한다 - 허공에 클립만 재생하고 쿨다운을
         * 돌리는 것이 이 게임에서 가장 나쁜 손해다(26단계 주석).
         *
         * @return 시전이 시작됐으면 true. 사거리가 비어 있으면 false이고, 부르는
         *         쪽(SkillSystem)은 쿨다운을 되돌린다
         */
        public bool Cast(int skillIndex, double totalMultiplier, Color tint, string displayName)
        {
            if (combat == null) return false;
            if (skillIndex < 0 || skillIndex >= SkillCatalog.Count) return false;

            var spec = SkillCatalog.Skills[skillIndex];
            var choreography = Find(spec.Id);

            // 안무가 없으면 시전하지 않는다. 조용히 단일 대상 한 방으로 떨어뜨리는
            // 대신 거절하는 이유는, 그 폴백이 있으면 배선이 빠진 것을 화면에서
            // 알아챌 수 없기 때문이다 - 26단계와 똑같이 보인다
            if (choreography == null || choreography.HitCount == 0)
            {
                Debug.LogWarning("[Onikiri] '" + spec.DisplayName + "'의 안무가 배선되지 않았다. "
                                 + "Build Combat Content 를 실행하라.");
                return false;
            }

            if (combat.FindTarget() == null) return false;

            // 같은 오의가 아직 돌고 있으면 새로 시작하지 않는다. 쿨다운이 클립보다
            // 길므로 정상 경로에서는 일어나지 않고, 테스트 패널의 "지금 시전"을
            // 연달아 누를 때만 온다
            for (int i = 0; i < active.Count; i++)
                if (active[i].skillIndex == skillIndex) return false;

            var started = new ActiveCast
            {
                skillIndex = skillIndex,
                choreography = choreography,
                totalMultiplier = totalMultiplier,
                tint = tint,
                elapsed = 0f,
                nextHit = 0,
                nextGhost = 0,
                baseX = combat.transform.position.x,

                // 장판의 자리는 여기서 굳는다. 돌진 오프셋이 얹히기 전 값이라
                // 사무라이가 이후 어디로 움직이든 장판은 깔린 곳에 남는다
                fieldOriginX = combat.transform.position.x,
                fieldOriginY = LaneOriginY
            };

            // 흡인은 **시전 순간에** 목록을 굳힌다. 폭발(타격 프레임)보다 먼저다 -
            // 그 사이에 스폰된 적이 목록에 끼면 "끌어모은 것만 맞는다"가 거짓이 된다
            if (spec.Special == SkillSpecial.Pull) CapturePullTargets(started, spec, choreography);

            active.Add(started);

            combat.PlaySkillClip(choreography.clip, choreography.clipFrameRate);

            // **섬광은 타격이 아니라 시전 순간에 나간다.** 거합은 지나가는 것이
            // 먼저고 베이는 것이 나중이다 - 타격 프레임에 맞춰 띄우면 이미
            // 지나간 자리에 뒤늦게 선이 그어진다
            if (choreography.usesStreak) SpawnStreak(active[active.Count - 1], choreography);

            if (nameFlash != null) nameFlash.Play(displayName, tint);

            return true;
        }

        // ---------------------------------------------------------------- 안무 진행

        private void Update()
        {
            if (active.Count == 0) return;

            float lunge = 0f;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cast = active[i];
                var c = cast.choreography;

                // 스케일 타임이다. 히트스톱 중에는 안무도 멈춘다 - 애니메이션과
                // 이펙트가 전부 스케일 타임이므로(23단계), 여기만 unscaled로 돌면
                // 정지 중에 타격이 들어가고 화면에는 아무 일도 일어나지 않는다
                cast.elapsed += Time.deltaTime;

                // 흡인은 타격이 아니라 **시간에 걸린 이동**이라 타격 루프 밖에서
                // 매 프레임 흐른다. 끌어오기가 끝난 뒤에도 한 번 더 돌지만
                // 보간이 1에서 멈추므로 자리가 안 흔들린다
                if (cast.captured.Count > 0) AdvancePull(cast);

                while (cast.nextHit < c.HitCount && cast.elapsed >= c.HitTime(cast.nextHit))
                {
                    Deliver(cast, cast.nextHit);
                    cast.nextHit++;
                }

                // 잔상도 타격과 같은 방식으로 시각에 걸려 있다. 걸린 시각을
                // 지날 때마다 하나씩 나가므로, 프레임이 몇 장이 뜨든 개수가 같다
                while (cast.nextGhost < c.afterimageCount
                       && cast.elapsed >= GhostTime(c, cast.nextGhost))
                {
                    SpawnAfterimage(cast, c, cast.nextGhost);
                    cast.nextGhost++;
                }

                // 돌진은 타격과 별개로 클립 길이에 걸쳐 흐른다
                if (c.lungeDistance > 0f)
                    lunge = Mathf.Max(lunge, LungeAt(c, cast.elapsed));

                // 클립이 끝나면 놓아준다. 마지막 타격이 아니라 클립 끝을 기준으로
                // 하는 이유는 돌진 복귀가 타격보다 뒤에 있기 때문이다
                if (cast.nextHit >= c.HitCount && cast.elapsed >= c.ClipSeconds)
                    active.RemoveAt(i);
            }

            // 돌진 오프셋은 매 프레임 다시 쓴다. 시전이 끝나면 0이 들어가 앵커로
            // 돌아간다 - PlayerCombat이 기준 X에서 다시 놓으므로 누적되지 않는다
            combat.LungeOffsetX = lunge;
        }

        /**
         * @brief 돌진 곡선. 나갈 때 빠르고 돌아올 때 느리다.
         *
         * 등속으로 왕복하면 "미끄러진다"로 읽힌다. 나가는 쪽에 가속을 몰아주면
         * 순간 이동에 가까워지고, 그것이 거합의 성질이다.
         */
        private static float LungeAt(Choreography c, float elapsed)
        {
            float out_ = Mathf.Max(0.0001f, c.lungeOutSeconds);
            float back = Mathf.Max(0.0001f, c.lungeBackSeconds);

            if (elapsed <= out_)
            {
                // ease-out: 처음이 가장 빠르다
                float t = elapsed / out_;
                return c.lungeDistance * (1f - (1f - t) * (1f - t));
            }

            float bt = (elapsed - out_) / back;
            if (bt >= 1f) return 0f;

            // ease-in-out: 천천히 떼고 천천히 붙는다
            return c.lungeDistance * (1f - bt * bt * (3f - 2f * bt));
        }

        // ---------------------------------------------------------------- 타격 분배

        private void Deliver(ActiveCast cast, int hitIndex)
        {
            var c = cast.choreography;
            var spec = SkillCatalog.Skills[cast.skillIndex];
            bool last = hitIndex == c.HitCount - 1;

            double share = SkillCatalog.HitDamageShare(cast.skillIndex, hitIndex, cast.totalMultiplier);
            var damage = BigDouble.FromDouble(share);

            int hits;
            switch (spec.Area)
            {
                case SkillArea.Pierce:
                    hits = DeliverLane(cast, damage, c.pierceRange, c.pierceHeight);
                    break;

                case SkillArea.Screen:
                    // 화면 전체다. 관통과 같은 코드를 아주 큰 사거리로 쓰지 않는
                    // 이유는 뜻이 다르기 때문이다 - 관통은 '경로'이고 광역은
                    // '살아 있는 전부'다. 사거리로 흉내내면 화면 밖의 요괴가
                    // 사거리에 들어오는 날 조용히 뜻이 달라진다
                    hits = DeliverAll(cast, damage);
                    break;

                case SkillArea.Around:
                    // 시전자 중심 원. 관통과 달리 **뒤쪽도** 든다
                    hits = combat.DeliverSkillAround(combat.transform.position.x, LaneOriginY,
                                                     c.aroundRadius, damage, cast.tint,
                                                     c.numberSizeMultiple);
                    break;

                case SkillArea.Field:
                    // 장판. 판정 모양은 관통과 같지만 **자리가 시전 순간에 굳는다** -
                    // 사무라이가 움직여도(돌진·넉백) 장판은 깔린 곳에 남는다.
                    // 그래서 원점을 cast에 적어 두고 매 틱 그 값을 쓴다
                    hits = combat.DeliverSkillLane(cast.fieldOriginX, cast.fieldOriginY,
                                                   c.fieldRange, c.fieldHeight, damage,
                                                   cast.tint, c.numberSizeMultiple);
                    break;

                case SkillArea.Captured:
                    // 흡인이 시전 시각에 확정한 목록. 그 밖은 안 맞는다
                    hits = DeliverCaptured(cast, damage);
                    break;

                default:
                    hits = combat.DeliverSkillHit(combat.FindTarget(), damage, cast.tint,
                                                  c.numberSizeMultiple) ? 1 : 0;
                    break;
            }

            if (c.usesSlash) SpawnSlash(cast, c);

            // 무기 티어의 스파크 겹 (51단계). 마지막 타격에만 - 다타의 매
            // 타격마다 얹으면 연참·혈륜에서 화면이 스파크로 덮인다
            if (last) SpawnTierSparks(c);

            // 번쩍은 색과 지속으로 갈린다. 귀참·혈폭은 흰색 x1.0이라 기존과
            // 같은 연출이고, 귀왕강림만 자기 참격 색으로 40% 길게 뜬다
            if (c.screenFlash && screenFlash != null)
                screenFlash.Play(c.flashTint, c.flashScale);

            // 무게는 마지막 타격에만. 다타의 중간 타격에 정지를 주면 0.5초 동안
            // 화면이 세 번 끊기고, 그것은 '연속 베기'가 아니라 '느려짐'이다
            if (last)
                combat.SkillFeedback(c.hitStopMultiplier, c.shakeMultiplier);
            else
                combat.SkillFeedback(0f, c.perHitShakeMultiplier);

            // 벨 것이 사라져도 안무는 끝까지 간다. 클립을 중간에 끊으면 사무라이가
            // 칼을 뻗은 자세로 얼어붙고, 그것이 화면에서 가장 어색한 상태다.
            // 데미지만 0으로 지나간다
            if (hits == 0 && hitIndex == 0)
                lastCastMissed = true;
        }

        /** 마지막 시전이 허공을 갈랐는지. 테스트 패널의 진단용 */
        private bool lastCastMissed;
        public bool LastCastMissed { get { return lastCastMissed; } }

        /**
         * @brief 세로 창의 중심. **사무라이의 그려진 중심이지 발이 아니다.**
         *
         * 관통·광역이 쓰고, 45c부터 영체(SpiritSummon)도 같은 값을 빌려 간다 -
         * 영체는 어깨 뒤 위쪽에 떠 있어서 자기 중심으로 창을 잡으면 1u 높은
         * 곳을 훑고, 지면의 요괴를 통째로 지나친다.
         */
        public float LaneOriginY
        {
            get
            {
                return samuraiRenderer != null
                    ? samuraiRenderer.bounds.center.y
                    : combat.transform.position.y;
            }
        }

        /** 전방 일렬. 산수는 PlayerCombat이 갖고 있다 - 손님이 둘이 됐다 */
        private int DeliverLane(ActiveCast cast, BigDouble damage, float range, float height)
        {
            return combat.DeliverSkillLane(combat.transform.position.x, LaneOriginY,
                                           range, height, damage, cast.tint,
                                           cast.choreography.numberSizeMultiple);
        }

        // ------------------------------------------------------------ 흡인 (SkillSpecial.Pull)

        /**
         * @brief 끌어올 대상을 **시전 시각에 확정**한다.
         *
         * 목록을 굳히는 것이 이 거동의 계약 전부다. 폭발 시점에 다시 고르면
         * 흡인 도중 스폰된 적이 끼어들어 "끌어모은 것만 맞는다"가 거짓이 되고,
         * 상한(PullTargetCount)도 그 순간의 수가 되어 뜻이 흐려진다.
         */
        private void CapturePullTargets(ActiveCast cast, SkillSpec spec, Choreography c)
        {
            cast.captured.Clear();
            cast.capturedFromX.Clear();

            float originX = combat.transform.position.x;
            cast.pullDestinationX = originX + c.pullDestinationOffset;
            cast.pullEndsAt = Mathf.Max(0f, c.pullSeconds);

            int limit = Mathf.Max(0, spec.PullTargetCount);
            if (limit == 0) return;

            var enemies = combat.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsTargetable) continue;

                // **보스는 안 끌린다.** 보스전의 거리 설계를 이 오의 하나가 바꾸면
                // 30초 창의 산수가 통째로 갈린다
                if (enemy.IsBoss) continue;

                float dx = enemy.CurrentX - originX;
                if (dx < 0f || dx > c.pullRange) continue;

                cast.captured.Add(enemy);
            }

            // 가까운 순으로 자른다. 먼 것을 골라 끌어오면 앞의 적을 통과해 지나가고,
            // 화면에서 그것은 흡인이 아니라 순간이동으로 보인다
            cast.captured.Sort(CompareByX);
            if (cast.captured.Count > limit)
                cast.captured.RemoveRange(limit, cast.captured.Count - limit);

            for (int i = 0; i < cast.captured.Count; i++)
                cast.capturedFromX.Add(cast.captured[i].CurrentX);
        }

        private static int CompareByX(Enemy a, Enemy b)
        {
            return a.CurrentX.CompareTo(b.CurrentX);
        }

        /**
         * @brief 끌어오기를 한 프레임 진행한다. **데미지는 안 준다** - 위치만이다.
         *
         * ease-out인 이유는 돌진(LungeAt)과 같다 - 처음이 가장 빨라야 "빨려
         * 들어간다"로 읽힌다. 등속이면 미끄러지는 것으로 보인다.
         */
        private static void AdvancePull(ActiveCast cast)
        {
            float t = cast.pullEndsAt <= 0f
                ? 1f : Mathf.Clamp01(cast.elapsed / cast.pullEndsAt);
            float eased = 1f - (1f - t) * (1f - t);

            for (int i = 0; i < cast.captured.Count && i < cast.capturedFromX.Count; i++)
            {
                var enemy = cast.captured[i];
                if (enemy == null || !enemy.IsAlive) continue;

                enemy.PullTo(Mathf.Lerp(cast.capturedFromX[i], cast.pullDestinationX, eased));
            }
        }

        /**
         * @brief 포획 목록에만 피해를 준다.
         *
         * 흡인 중에 죽은 대상은 조용히 빠진다. **총량을 남은 대상에 몰아주지
         * 않는다** - 몰아주면 적이 적을수록 세지는 오의가 되고, 그것은 총량
         * 계약이 아니라 그 반대다.
         */
        private int DeliverCaptured(ActiveCast cast, BigDouble damage)
        {
            int hits = 0;
            for (int i = 0; i < cast.captured.Count; i++)
            {
                var enemy = cast.captured[i];
                if (enemy == null || !enemy.IsTargetable) continue;

                if (combat.DeliverSkillHit(enemy, damage, cast.tint,
                                           cast.choreography.numberSizeMultiple)) hits++;
            }
            return hits;
        }

        /** 화면 광역. 살아 있는 요괴 전부 */
        private int DeliverAll(ActiveCast cast, BigDouble damage)
        {
            return combat.DeliverSkillAll(damage, cast.tint,
                                          cast.choreography.numberSizeMultiple);
        }

        // ---------------------------------------------------------------- 참격

        /**
         * @brief 팩 참격을 사무라이 앞에 놓는다. **요괴가 아니라 사무라이 기준이다.**
         *
         * 26단계에는 요괴의 피격점에 놓았고, 그래서 요괴가 여럿일 때 어느 하나에
         * 붙은 것으로 보였다. 관통과 광역은 **여럿을 한 번에 베는 것**이므로
         * 참격도 그 범위 한가운데에 있어야 한다 - 한 요괴에 붙으면 "여럿을 벴다"가
         * 아니라 "저 하나를 벴다"로 읽힌다.
         *
         * **참격의 크기와 타격 범위는 별개다.** 귀참은 화면의 모든 요괴를 때리지만
         * (`DeliverAll`) 그림은 사무라이 앞을 가르는 한 번뿐이다 - 이펙트가 맞는
         * 것들을 물리적으로 덮을 필요가 없다. 덮으려다 화면을 가린 것이 28단계다.
         *
         * 반전은 여전히 요괴에서 끌어온다(23단계 규칙). 대상이 없으면 오른쪽을
         * 기본으로 둔다 - 지금 요괴는 전부 오른쪽에서 온다.
         */
        /**
         * @brief 이 안무가 지금 무기 티어에서 재생할 참격 프레임 (51단계).
         *
         * 티어 변형이 배선돼 있으면(귀참 - 팩에 5색이 있다) 등급을 따라가고,
         * 없으면(신규 다섯 - 혈 램프로 한 벌만 구웠다) 기존 프레임 그대로다.
         * 폴백이 조용한 것이 여기서는 옳다 - 배선이 빠져도 참격 자체는 뜨므로
         * 화면이 깨지지 않고, 배선 여부는 테스트가 잡는다.
         */
        private static Sprite[] SlashFramesFor(Choreography c)
        {
            var tiers = c.slashTierFrames;
            if (tiers == null || tiers.Length == 0) return c.slashFrames;

            int index = Mathf.Clamp(WeaponVfxTier.CurrentTier(), 1, tiers.Length) - 1;
            var tier = tiers[index];
            if (tier == null || tier.frames == null || tier.frames.Length == 0)
                return c.slashFrames;

            return tier.frames;
        }

        private void SpawnSlash(ActiveCast cast, Choreography c)
        {
            var frames = SlashFramesFor(c);
            if (slashPool == null || frames == null || frames.Length == 0) return;

            float originY = samuraiRenderer != null
                ? samuraiRenderer.bounds.center.y
                : combat.transform.position.y;

            var target = combat.FindTarget();
            bool mirror = target != null && target.FacingDirection > 0;

            // 전방 오프셋은 바라보는 쪽으로 간다. 부호를 안 뒤집으면 왼쪽을 벨 때
            // 참격이 등 뒤에 뜬다
            float forward = mirror ? -c.slashForwardOffset : c.slashForwardOffset;

            var anchor = new Vector3(
                combat.transform.position.x + forward,
                originY + c.slashHeightOffset,
                0f);

            SpawnSlashAt(frames, c.slashFrameRate, anchor,
                         c.slashAngle, c.slashScale, mirror, c.slashTint);
        }

        // ---------------------------------------------------------------- 티어 스파크

        /**
         * @brief 무기 티어의 스파크 겹 (51단계). 참격 앵커 언저리에 은백
         *        스파크를 티어 색으로 물들여 겹친다.
         *
         * ## 자리 - 무작위가 아니라 표다
         *
         * 겹마다 고정 오프셋이다. 무작위로 뿌리면 같은 오의가 시전마다 다른
         * 그림이 되고, "등급이 오르면 이렇게 변한다"를 전/후로 비교할 수 없다 -
         * 이 스텝의 존재 이유가 그 비교다.
         *
         * ## 풀은 참격 것을 그대로 쓴다
         *
         * 스파크도 PackSlash가 재생한다(프레임 갈아 끼우고 끝나면 꺼지는 일이
         * 같다 - VfxBurst 주석). 전용 풀을 만들면 화면의 참격 수를 세는 진단
         * (SlashPoolGrowthCount)이 갈라진다. 최대 겹(3)만큼 프리웜을 늘렸다.
         */
        private void SpawnTierSparks(Choreography c)
        {
            if (slashPool == null || glowLibrary == null) return;

            int tier = WeaponVfxTier.CurrentTier();
            bool premium = WeaponVfxTier.IsPremium();
            int layers = WeaponVfxTier.SparkLayers(tier, premium);
            if (layers <= 0) return;

            float originY = samuraiRenderer != null
                ? samuraiRenderer.bounds.center.y
                : combat.transform.position.y;

            var target = combat.FindTarget();
            bool mirror = target != null && target.FacingDirection > 0;
            float sign = mirror ? -1f : 1f;

            // 참격이 있으면 그 앞, 없으면(연참·일섬) 사무라이 바로 앞이다
            float forward = c.usesSlash ? c.slashForwardOffset : 1.2f;
            float height = c.usesSlash ? c.slashHeightOffset : 0.2f;

            for (int i = 0; i < layers && i < SparkOffsets.Length; i++)
            {
                // 마지막 겹이 프리미엄 자리다 - 오니키리 완성만 이 겹을 가진다
                bool premiumLayer = premium && i == layers - 1;
                var tint = WeaponVfxTier.SparkTint(tier, premiumLayer);

                // 두 조각을 번갈아 쓴다. 같은 그림 세 장이 겹치면 겹이 아니라
                // 한 장이 진해진 것으로 읽힌다
                var clip = glowLibrary.Find(i % 2 == 0
                    ? WeaponGlowSparkBurst : WeaponGlowSparkRay);
                if (clip == null || clip.frames == null || clip.frames.Length == 0) continue;

                var position = new Vector3(
                    combat.transform.position.x + (forward + SparkOffsets[i].x) * sign,
                    originY + height + SparkOffsets[i].y,
                    0f);

                var slash = slashPool.Get();
                slash.Play(clip.frames, clip.frameRate, position, clip.angle,
                           clip.scale, mirror, tint, ReleaseSlash);
            }
        }

        /** 겹마다 고정 자리 (전방·높이, 월드 단위). 위 주석 참고 */
        private static readonly Vector2[] SparkOffsets =
        {
            new Vector2(0f, 0f),
            new Vector2(0.55f, 0.5f),
            new Vector2(-0.4f, 0.75f)
        };

        /** 은백 스파크 클립의 이름. PozacVfxBaker의 실버 굽기와 같아야 한다 */
        public const string WeaponGlowSparkBurst = "pozac_spark_burst";
        public const string WeaponGlowSparkRay = "pozac_spark_ray";

        /**
         * @brief 참격 한 장을 **자리를 지정해** 띄운다. 풀은 오의 것을 그대로 쓴다.
         *
         * 영체(SpiritSummon)가 부르는 입구다. 45단계까지 영체는 이 게임의 연출
         * 시스템을 하나도 타지 않았다 - 반투명 스프라이트 하나와 데미지 숫자가
         * 전부였고, 참격·플래시·히트스톱·흔들림이 죄다 없었다.
         *
         * 그 배선을 여기로 끌어오는 것이 프리팹과 풀을 두 벌로 만드는 것보다
         * 싸다. 참격 프리팹·프리웜·해제 콜백이 한 곳에 남고, 화면에 참격이 몇
         * 장 떠 있는지도 한 숫자로 세어진다(SlashPoolGrowthCount) - 영체가
         * 자기 풀을 따로 가지면 그 진단이 둘로 갈라진다.
         *
         * 자리를 밖에서 받는 것이 SpawnSlash와의 유일한 차이다. 저쪽은 사무라이
         * 기준으로 계산하는데, 영체는 사무라이 뒤 위쪽에 따로 서 있다.
         */
        public void SpawnSlashAt(Sprite[] frames, float fps, Vector3 anchor,
                                 float angle, float scale, bool flip)
        {
            SpawnSlashAt(frames, fps, anchor, angle, scale, flip, Color.white);
        }

        /**
         * @brief 색을 지정해 띄우는 판. 신규 일곱의 은백 조각이 이 경로를 지난다.
         *
         * 흰색을 넘기면 위 판과 **같은 결과**다 - PackSlash가 곱연산으로
         * 물들이므로 흰색은 항등원이다. 그래서 기존 호출부(영체·티어 스파크)를
         * 한 줄도 안 고쳐도 된다.
         */
        public void SpawnSlashAt(Sprite[] frames, float fps, Vector3 anchor,
                                 float angle, float scale, bool flip, Color tint)
        {
            if (slashPool == null || frames == null || frames.Length == 0) return;

            var slash = slashPool.Get();
            slash.Play(frames, fps, anchor, angle, scale, flip, tint, ReleaseSlash);
        }

        private void ReleaseSlash(PackSlash slash)
        {
            slashPool.Release(slash);
        }

        /**
         * @brief 돌진 경로에 얇은 섬광을 눕힌다.
         *
         * 길이가 **돌진 거리와 같다.** 사거리(pierceRange)가 아니다 - 섬광은
         * 사무라이가 실제로 지나간 자리이고, 그보다 길면 다시 "저 앞에 뜬 이펙트"가
         * 된다. 타격이 닿는 범위는 히트박스가 따로 말한다.
         */
        private void SpawnStreak(ActiveCast cast, Choreography c)
        {
            if (streakPool == null) return;

            float originY = samuraiRenderer != null
                ? samuraiRenderer.bounds.center.y
                : combat.transform.position.y;

            var target = combat.FindTarget();
            bool mirror = target != null && target.FacingDirection > 0;

            var origin = new Vector3(cast.baseX, originY + c.streakHeightOffset, 0f);

            // 무기 티어가 섬광을 물들인다 (51단계). 흰 심은 텍스처에 구워져
            // 있어 살아남고, 곱색만 티어 쪽으로 기운다 - 티어1은 그대로다
            int tier = WeaponVfxTier.CurrentTier();
            bool premium = WeaponVfxTier.IsPremium();
            var color = Color.Lerp(c.streakColor,
                                   WeaponVfxTier.SparkTint(tier, premium),
                                   WeaponVfxTier.StreakBlend(tier));

            var streak = streakPool.Get();
            streak.Play(origin, mirror ? -1f : 1f, c.lungeDistance, c.streakThickness,
                        color, c.streakRevealSeconds, c.streakHoldSeconds,
                        c.streakFadeSeconds, ReleaseStreak);
        }

        private void ReleaseStreak(DashStreak streak)
        {
            streakPool.Release(streak);
        }

        /**
         * @brief 잔상 하나가 놓일 자리 (돌진 거리 대비 비율).
         *
         * **시간이 아니라 거리를 균등하게 나눈다.** 시간을 나누면 돌진 곡선이
         * ease-out(처음이 가장 빠르다)이라 잔상이 도착 지점 쪽에 뭉친다 -
         * 실측으로 세 장이 0.83 / 1.43 / 1.78u에 놓여 뒤 두 장이 0.36u 안에
         * 붙었다. 거리를 나누면 0.475 / 0.95 / 1.425로 고르게 선다.
         */
        private static float GhostFraction(Choreography c, int index)
        {
            return (index + 1) / (c.afterimageCount + 1f);
        }

        /**
         * @brief 그 자리를 본체가 실제로 지나는 시각.
         *
         * 돌진 곡선 `1-(1-x)^2 = f`를 x에 대해 푼 것이다. 거리에서 시각을
         * 역산해야 **잔상이 본체보다 앞서 나타나지 않는다** - 자리만 고르게
         * 잡고 시각을 그대로 두면 아직 가지 않은 곳에 잔상이 먼저 뜬다.
         */
        private static float GhostTime(Choreography c, int index)
        {
            float f = Mathf.Clamp01(GhostFraction(c, index));
            return c.lungeOutSeconds * (1f - Mathf.Sqrt(1f - f));
        }

        /**
         * @brief 돌진이 지나간 자리에 사무라이 잔상을 하나 남긴다.
         *
         * ## 시전 순간에 한꺼번에 뿌리지 않는다
         *
         * 처음에는 Cast에서 세 장을 한 번에 만들었다. 개수가 프레임률에
         * 좌우되지 않는다는 점은 좋았지만 **그리는 것이 틀렸다** - 그 시점의
         * `samuraiRenderer.sprite`는 아직 DASH 클립이 아니라 직전 평타 프레임이라,
         * 화면에는 돌진하는 사무라이가 아니라 **평타의 흰 칼궤적이 허공에
         * 한 장 떠 있는 그림**이 나왔다. 28단계 스크린샷에서 실제로 그렇게 보였다.
         *
         * 그래서 타격과 같은 방식으로 바꿨다. 잔상마다 시각이 걸려 있고
         * (GhostTime), 그 시각을 지날 때 하나가 나간다. 시각이 고정이므로 개수는
         * 여전히 프레임률과 무관하고, 그리는 그림은 그 순간의 DASH 프레임이 된다.
         *
         * 위치는 실제 위치가 아니라 **걸린 시각의 돌진 곡선 값**을 쓴다. 프레임이
         * 늦게 떠서 조금 지나친 자리에 남기면 잔상이 본체보다 앞에 놓인다.
         */
        private void SpawnAfterimage(ActiveCast cast, Choreography c, int index)
        {
            if (afterimagePool == null) return;
            if (samuraiRenderer == null || samuraiRenderer.sprite == null) return;

            float offset = c.lungeDistance * GhostFraction(c, index);

            var tint = afterimageTint;
            // 뒤쪽(먼저 지나간 자리)일수록 옅다. 같은 진하기로 두면 잔상이
            // 아니라 여러 명이 서 있는 그림이 된다.
            //
            // 다만 0에서 시작하지 않는다. 1/N로 나눴더니 첫 장이 알파 0.17로
            // 떠서 수명 감쇠까지 겹치면 화면에 아무것도 안 보였다 - 가장 옅은
            // 장도 0.45는 남겨야 "지나간 자리"로 읽힌다
            tint.a *= c.afterimageCount > 1
                ? Mathf.Lerp(0.45f, 1f, index / (float)(c.afterimageCount - 1))
                : 1f;

            var position = combat.transform.position;

            SpawnAfterimageAt(samuraiRenderer.sprite,
                              new Vector3(cast.baseX + offset, position.y, position.z),
                              samuraiRenderer.flipX, tint, SortingOrders.Player - 1, 1f);
        }

        /**
         * @brief 잔상 한 장을 자리·층·크기를 지정해 남긴다. 영체의 강림이 쓴다.
         *
         * 층과 크기를 밖에서 받는 이유는 부르는 쪽이 사무라이가 아닐 수 있기
         * 때문이다 - 영체는 Spirit(48) 층에 서고 자루에 따라 몸이 커진다.
         * 풀에서 꺼낸 인스턴스는 앞사람이 쓰던 크기를 그대로 들고 있으므로
         * **매번 다시 써야 한다** - 안 쓰면 큰 영체가 한 번 지나간 뒤 사무라이
         * 잔상까지 커진다.
         */
        public void SpawnAfterimageAt(Sprite sprite, Vector3 position, bool flip,
                                      Color tint, int sortingOrder, float scale)
        {
            if (afterimagePool == null || sprite == null) return;

            var ghost = afterimagePool.Get();
            ghost.transform.localScale = Vector3.one * (scale > 0f ? scale : 1f);
            ghost.Play(sprite, position, flip, tint, sortingOrder, ReleaseAfterimage);
        }

        private void ReleaseAfterimage(Afterimage ghost)
        {
            afterimagePool.Release(ghost);
        }
    }
}
