// using System 을 넣지 않는다. System.Object 와 UnityEngine.Object 가 충돌해서
// 이 파일 전체의 FindFirstObjectByType 호출이 모호해진다
using Onikiri.Battle;
using Onikiri.Cloud;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 개발용 테스트 패널.
     *
     * 이 창이 존재하는 이유는 이 게임의 값들이 대부분 눈으로 확인되지 않기 때문이다.
     * 소리가 실제로 출력에 도달하는지, 공격속도 스탯이 실제 공격 횟수로 이어지는지,
     * 풀이 조용히 증식하고 있는지는 화면만 봐서는 알 수 없다. 지금까지 그런 것을
     * 확인할 때마다 일회용 측정 코드를 매번 다시 짰다.
     *
     * 측정에서 반복해서 밟은 함정 두 가지를 이 창이 대신 피해준다:
     *
     *  - 공격 횟수를 실제 시간으로 나누면 안 된다. 전투는 Time.deltaTime으로 도는데
     *    그 값은 maximumDeltaTime으로 잘리므로, 에디터가 멈칫하면 게임 시간이 실제
     *    시간보다 뒤처져 실측값이 실제보다 낮게 나온다. 창 길이는 Time.time으로 잰다.
     *
     *  - AudioSource.isPlaying은 소리가 들린다는 증거가 아니다. 씬에 AudioListener가
     *    없어도 true를 반환한다. 리스너 출력 파형을 직접 읽어야 한다.
     *
     * ## 규칙 - 새 기능은 반드시 자기 절을 여기 얹는다 (33단계에 못 박음)
     *
     * 스킬(26) -> 퀘스트(31) -> 장비(32) -> 전직(33)이 전부 그렇게 들어왔다.
     * 새 시스템이 이 창에 없으면 그 기능의 상태를 만드는 방법이 "실제로
     * 플레이해서 도달"뿐이 되고, 확인 비용이 확인보다 커지는 순간 아무도
     * 확인하지 않게 된다. 절의 문법은 기존 것을 따른다: 실제 구매 경로 버튼 +
     * 재화 무시 치트(Debug*) + 초기화(환불 없음), 재화 소비처는 재화가 나오는
     * 절 바로 다음에 놓는다.
     */
    public sealed class OnikiriTestPanel : EditorWindow
    {
        [MenuItem("Onikiri/Test Panel %#t")]
        public static void Open()
        {
            var window = GetWindow<OnikiriTestPanel>("Onikiri");
            window.minSize = new Vector2(320f, 420f);
            window.Show();
        }

        // ---------------------------------------------------------------- 샘플링 상태

        private PlayerCombat combat;
        private EnemySpawner spawner;
        private UpgradeSystem upgrades;
        private Onikiri.UI.DamageNumberSpawner damageNumbers;
        private HitAudio hitAudio;
        private StageProgress stage;
        private GameSession session;
        private BossFight boss;
        private RegionMobSwitcher mobSwitcher;
        private SkillSystem skills;
        private SkillPerformer performer;
        private CharacterLevel character;
        private VfxBurst attackVfx;

        /** 방치 보상 확인용. 몇 시간 전에 종료한 것으로 꾸밀지 */
        private float offlineHours = 3f;

        /**
         * @brief 보석 주입량. **골드처럼 배수 버튼으로 못 때우는 재화다.**
         *
         * 골드는 +1K/+1M/+1T로 충분하다 - 쓰는 곳이 강화 하나라 자릿수만
         * 맞으면 된다. 보석은 다르다. 오의 10연이 정확히 얼마, 장비 파편이
         * 얼마, 요도 단연이 얼마인지가 **곡선이 정한 값**이고, 확인하고
         * 싶은 것은 대개 "그 값에서 하나 모자랄 때"와 "딱 맞을 때"다.
         * 배수 버튼으로는 그 경계에 못 선다.
         */
        private long debugGemAmount = 1000L;

        /** 랭킹 절(54단계)의 입력들. 조회 결과는 창 안에 그대로 적는다 */
        private string debugPlayerName = string.Empty;
        private int debugSubmitStage = 1;
        private string leaderboardPreview = string.Empty;

        /**
         * @brief 계정 절(55단계)의 병합 계산기 입력.
         *
         * 기본값이 "재설치 직후"다 - 로컬 1층, 버려질 문서 없음(0), 복구된
         * 문서 171층. 이 스텝이 존재하는 이유가 되는 바로 그 상황이라,
         * 창을 열면 그 답이 먼저 보이는 편이 맞다.
         */
        private int mergeLocal = 1;
        private int mergeAbandoned;
        private int mergeRecovered = 171;

        /** 공격속도 실측용. 창 길이는 게임 시간으로 잰다 */
        private float rateWindowStart;
        private int rateWindowAttacks;
        private float measuredRate;

        /** fps 실측용 */
        private float fpsWindowStart;
        private int fpsWindowFrames;
        private float measuredFps;

        /** 리스너에 도달한 출력의 최대 진폭. 창이 열려 있는 동안 서서히 감쇠한다 */
        private float audioPeak;
        private readonly float[] audioBuffer = new float[512];

        private Vector2 scroll;

        private void OnEnable()
        {
            EditorApplication.update += Sample;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Sample;
        }

        private void Sample()
        {
            if (!EditorApplication.isPlaying)
            {
                combat = null; spawner = null; upgrades = null;
                damageNumbers = null; hitAudio = null; stage = null; session = null; boss = null;
                mobSwitcher = null; skills = null; performer = null; character = null;
                attackVfx = null;
                measuredRate = 0f; measuredFps = 0f; audioPeak = 0f;

                // 측정 창도 함께 비운다. 이 창은 도메인 리로드를 넘어 살아남는데,
                // Time.time은 플레이 모드를 다시 시작할 때 0으로 돌아간다. 지난 세션의
                // 시작 시각을 들고 있으면 경과 시간이 계속 음수라 실측값이 영원히
                // 갱신되지 않고 0에 머문다
                rateWindowStart = 0f;
                fpsWindowStart = 0f;
                Repaint();
                return;
            }

            if (combat == null) combat = Object.FindFirstObjectByType<PlayerCombat>();
            if (spawner == null) spawner = Object.FindFirstObjectByType<EnemySpawner>();
            if (upgrades == null) upgrades = Object.FindFirstObjectByType<UpgradeSystem>();
            if (damageNumbers == null) damageNumbers = Object.FindFirstObjectByType<Onikiri.UI.DamageNumberSpawner>();
            if (hitAudio == null) hitAudio = Object.FindFirstObjectByType<HitAudio>();
            if (stage == null) stage = Object.FindFirstObjectByType<StageProgress>();
            if (session == null) session = Object.FindFirstObjectByType<GameSession>();
            if (boss == null) boss = Object.FindFirstObjectByType<BossFight>();
            if (mobSwitcher == null) mobSwitcher = Object.FindFirstObjectByType<RegionMobSwitcher>();
            if (skills == null) skills = Object.FindFirstObjectByType<SkillSystem>();
            if (performer == null) performer = Object.FindFirstObjectByType<SkillPerformer>();
            if (character == null) character = Object.FindFirstObjectByType<CharacterLevel>();
            if (attackVfx == null) attackVfx = Object.FindFirstObjectByType<VfxBurst>();

            SampleAttackRate();
            SampleFps();
            SampleAudio();

            Repaint();
        }

        /**
         * @brief 실제 공격 횟수를 게임 시간으로 나눈다.
         *
         * 실제 시간으로 나누면 에디터가 멈칫한 만큼 값이 낮게 나온다. 게임 시간과
         * 실제 시간이 어긋나는 것 자체는 버그가 아니라 maximumDeltaTime의 정상 동작이다.
         */
        private void SampleAttackRate()
        {
            if (combat == null) return;

            float now = Time.time;
            float elapsed = now - rateWindowStart;

            // 창이 비어 있거나 시간이 뒤로 갔으면(플레이 재시작) 다시 연다
            if (rateWindowStart <= 0f || elapsed < 0f)
            {
                rateWindowStart = now;
                rateWindowAttacks = combat.AttackCount;
                return;
            }

            if (elapsed < 1f) return;

            measuredRate = (combat.AttackCount - rateWindowAttacks) / elapsed;
            rateWindowStart = now;
            rateWindowAttacks = combat.AttackCount;
        }

        /** fps는 실제 시간 기준이 맞다. 게임 시간으로 재면 timeScale이 섞인다 */
        private void SampleFps()
        {
            float now = Time.unscaledTime;
            float elapsed = now - fpsWindowStart;

            if (fpsWindowStart <= 0f || elapsed < 0f)
            {
                fpsWindowStart = now;
                fpsWindowFrames = Time.frameCount;
                return;
            }

            if (elapsed < 0.5f) return;

            measuredFps = (Time.frameCount - fpsWindowFrames) / elapsed;
            fpsWindowStart = now;
            fpsWindowFrames = Time.frameCount;
        }

        private void SampleAudio()
        {
            AudioListener.GetOutputData(audioBuffer, 0);
            float peak = 0f;
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                float a = Mathf.Abs(audioBuffer[i]);
                if (a > peak) peak = a;
            }
            // 최댓값을 잡아두고 초당 일정 비율로 떨어뜨린다. 타격음은 0.2초 남짓이라
            // 순간값만 보면 대부분의 프레임에서 0이 찍히고, 소리가 나는데도 무음으로
            // 읽힌다. 프레임 수가 아니라 시간으로 감쇠시켜야 fps가 달라져도 같게 보인다
            float decayPerSecond = 0.6f;
            audioPeak = Mathf.Max(peak, audioPeak - decayPerSecond * Time.unscaledDeltaTime);
        }

        // ---------------------------------------------------------------- 그리기

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawPlayControls();
            EditorGUILayout.Space();

            if (EditorApplication.isPlaying)
            {
                DrawLiveStats();
                EditorGUILayout.Space();
                DrawCheats();
            }
            else
            {
                EditorGUILayout.HelpBox("플레이 모드에서 상태와 치트가 표시됩니다.", MessageType.Info);
            }

            EditorGUILayout.Space();
            DrawScreenTools();
            EditorGUILayout.Space();
            DrawBuilders();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(EditorApplication.isPlaying ? "정지" : "플레이", GUILayout.Height(26f)))
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;

                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(EditorApplication.isPaused ? "재개" : "일시정지", GUILayout.Height(26f)))
                        EditorApplication.isPaused = !EditorApplication.isPaused;

                    if (GUILayout.Button("한 프레임", GUILayout.Height(26f)))
                        EditorApplication.Step();
                }
            }
        }

        private void DrawLiveStats()
        {
            EditorGUILayout.LabelField("상태", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("화면", string.Format("{0}x{1}  ({2:F2}:1)  픽셀배율 {3}",
                    Screen.width, Screen.height,
                    Screen.height / (float)Mathf.Max(1, Screen.width), PixelRatio()));

                var safe = Screen.safeArea;
                Row("안전 영역", safe.width == Screen.width && safe.height == Screen.height
                    ? "전체 화면 (노치 없음)"
                    : string.Format("{0} (좌우 {1}, 상하 {2} 잘림)", safe,
                        Screen.width - (int)safe.width, Screen.height - (int)safe.height));

                Row("fps / timeScale", string.Format("{0:F0} / {1:F2}", measuredFps, Time.timeScale));

                if (combat != null)
                {
                    // 스탯 표기와 실제가 다르면 그 자리에서 보이게 나란히 놓는다.
                    // 공격 하나가 정수 개의 프레임을 차지하므로 완전히 일치하지는 않는다
                    Row("공격속도", string.Format("설정 {0:F2} -> 실측 {1:F2}회/초  ({2:P0})   상한 {3:F2}",
                        combat.AttacksPerSecond, measuredRate,
                        combat.AttacksPerSecond > 0f ? measuredRate / combat.AttacksPerSecond : 0f,
                        combat.MaxAttacksPerSecond));

                    // 스윙이 원속도의 몇 배로 재생되고 있는지. 2배가 한계이며 그 위는
                    // 픽셀 애니메이션이 깜빡임으로 읽힌다. CombatFeel 참고
                    Row("스윙 배속", string.Format("{0:F2}x  (클립 {1:F2}초)",
                        Mathf.Min(CombatFeel.MaxAnimationSpeed,
                                  combat.BaseAttackDuration * combat.AttacksPerSecond),
                        combat.BaseAttackDuration));

                    Row("데미지", NumberFormatter.Format(combat.Damage));
                    Row("누적 공격", combat.AttackCount.ToString());

                    // 26단계부터 초당 공격 횟수가 평타만이 아니다. 위 "공격속도"
                    // 줄은 평타만 재는데(AttackCount가 평타만 센다), 그것만 보면
                    // 오의가 만드는 출력이 통째로 관측 밖에 남는다
                    double castRate = skills != null ? skills.CastRate : 0d;
                    float effective = combat.EffectiveAttacksPerSecond;
                    Row("실효 공격", string.Format("{0:F2}회/초  = 평타 {1:F2} + 오의 {2:F2}   (오의 몫 {3:P0})",
                        effective, combat.AttacksPerSecond, castRate,
                        effective > 0f ? castRate / effective : 0d));
                }

                // 오의 해금이 캐릭터 레벨에 걸려 있는데 이 창에는 레벨 줄이 없었다.
                // "왜 스킬이 안 열리지"의 답이 화면 어디에도 없는 상태였다
                if (character != null)
                    Row("레벨", string.Format("Lv.{0}   EXP {1}/{2}   남은 포인트 {3}   (공격 {4} / 체력 {5})",
                        character.Level,
                        NumberFormatter.Format(character.Exp),
                        NumberFormatter.Format(character.ExpRequired),
                        character.UnspentPoints,
                        character.AttackPoints, character.HealthPoints));

                var wallet = PlayerWallet.Instance;
                if (wallet != null)
                    Row("골드", NumberFormatter.Format(wallet.Gold) +
                                "   (누적 " + NumberFormatter.Format(wallet.LifetimeGold) + ")");

                if (stage != null)
                {
                    Row("스테이지", string.Format("{0}   {1}/{2} 처치   체력 x{3}  골드 x{4}",
                        stage.Stage, stage.KillsThisStage, stage.KillsRequired,
                        NumberFormatter.Format(stage.HealthMultiplier, 2),
                        NumberFormatter.Format(stage.GoldMultiplier, 2)));

                    // 밸런스의 핵심 지표. 1로 내려앉으면 공격력 강화가 죽고,
                    // 계속 불어나면 후반이 늘어진다.
                    // 온보딩 완화(st1~5 잡몹 전용, E-3 후속)까지 지난 실제 값이다 -
                    // 완화 배율이 걸린 스테이지에서는 옆에 표시된다
                    if (combat != null && spawner != null)
                    {
                        var averageHealth = StageCurve.MobHealth(
                            spawner.AverageBaseHealth, stage.Stage);
                        double relief = StageCurve.MobHealthRelief(stage.Stage);
                        int hits = StageCurve.HitsToKill(averageHealth, combat.Damage);
                        Row("처치당 타격", hits + "대   (평균 체력 " +
                            NumberFormatter.Format(averageHealth) + " / 데미지 " +
                            NumberFormatter.Format(combat.Damage) +
                            (relief > 1d ? " / 온보딩 완화 ÷" + relief.ToString("F2") : "") + ")");
                    }
                }

                if (session != null)
                    Row("초당 골드", session.EstimateGoldPerSecond().ToString("F2") +
                                     "   (방치 시 절반)");

                if (spawner != null)
                {
                    int alive = 0;
                    foreach (var enemy in spawner.Active) if (enemy != null && enemy.IsAlive) alive++;
                    Row("요괴", alive + "마리 생존 / 활성 " + spawner.Active.Count);
                }

                // 풀 증식은 조용히 일어나고 폰에서 프레임 히칭으로만 나타난다.
                // 0이 아니면 prewarm이 부족하다는 뜻이다
                Row("풀 증식", string.Format("적 {0} / 불꽃 {1} / 데미지 {2} / 꽃잎 {3} / 참격 {4}",
                    spawner != null ? spawner.PoolGrowthCount : 0,
                    combat != null ? combat.SparkPoolGrowthCount : 0,
                    damageNumbers != null ? damageNumbers.PoolGrowthCount : 0,
                    combat != null ? combat.SakuraPoolGrowthCount : 0,
                    performer != null ? performer.SlashPoolGrowthCount : 0));

                bool hasListener = Object.FindFirstObjectByType<AudioListener>() != null;
                Row("오디오", hasListener
                    ? string.Format("리스너 있음, 출력 진폭 {0:F3}", audioPeak)
                    : "리스너 없음 - 무음");

                if (!hasListener)
                    EditorGUILayout.HelpBox(
                        "씬에 AudioListener가 없습니다. AudioSource.Play()는 성공하고 " +
                        "isPlaying도 true를 반환하지만 소리는 나지 않습니다.", MessageType.Error);
            }
        }

        /**
         * @brief 보석을 **입력한 값 그대로** 넣거나 잔액을 그 값으로 세운다.
         *
         * ## 왜 넣기와 세우기가 둘 다 있는가
         *
         * 묻는 것이 다르다. `+ 넣기`는 **획득 경로**를 탄다(`GemWallet.Add`) -
         * 넘침 방어까지 그대로 지나므로 "퀘스트 보상이 들어왔다"와 같은 길이다.
         * `= 세우기`는 **잔액 복원**이다(`SetBalance`) - 세이브에서 올라온
         * 것과 같은 문이고, 0으로 내려 "모자랄 때 버튼이 안 눌리는가"를
         * 확인할 수 있는 유일한 길이다. `Add`로는 못 내린다.
         *
         * ## 곡선이 정한 값을 그대로 집어 준다
         *
         * 오의 10연 값을 외워서 치게 하면 곡선이 바뀐 날 조용히 틀린 값을
         * 넣게 된다. 옆 버튼이 그 순간의 실제 비용을 읽어 칸에 적는다 -
         * 화면의 버튼과 같은 출처(`SkillGachaSystem.CostFor`)다.
         *
         * 확인하려는 자리는 대개 **경계**다. 딱 맞을 때와 하나 모자랄 때
         * 버튼이 다르게 서야 하는데, 배수 버튼으로는 그 두 자리에 못 선다.
         */
        private void DrawGemCheat()
        {
            var gems = Onikiri.Progression.GemWallet.Instance;

            using (new EditorGUI.DisabledScope(gems == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("보석", GUILayout.Width(64f));

                    EditorGUILayout.LabelField(
                        gems != null ? gems.Gems.ToString("N0") : "-",
                        EditorStyles.miniLabel, GUILayout.Width(80f));

                    // 음수는 안 받는다. Add가 조용히 무시하고 SetBalance는 0으로
                    // 깎으므로, 칸에 남은 숫자와 실제로 일어난 일이 갈린다
                    debugGemAmount = System.Math.Max(0L,
                        (long)EditorGUILayout.LongField(debugGemAmount, GUILayout.Width(90f)));

                    if (GUILayout.Button("+ 넣기", GUILayout.Width(56f)))
                    {
                        gems.Add(debugGemAmount);
                        Debug.Log("[Onikiri] 보석 " + debugGemAmount.ToString("N0")
                                  + "개를 넣었다. 잔액 " + gems.Gems.ToString("N0")
                                  + " - 획득 경로(Add)라 넘침 방어를 그대로 지난다.");
                    }

                    if (GUILayout.Button("= 세우기", GUILayout.Width(64f)))
                    {
                        gems.SetBalance(debugGemAmount);
                        Debug.Log("[Onikiri] 보석 잔액을 " + gems.Gems.ToString("N0")
                                  + "으로 세웠다. 세이브 복원과 같은 경로(SetBalance)다.");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(68f);

                    // 자주 서는 자리를 칸에 집어넣는다. 값을 외우게 하지 않는
                    // 이유는 곡선이 바뀌면 외운 값이 조용히 틀리기 때문이다
                    if (GUILayout.Button("0", GUILayout.Width(34f))) debugGemAmount = 0L;
                    if (GUILayout.Button("1K", GUILayout.Width(40f))) debugGemAmount = 1000L;
                    if (GUILayout.Button("10K", GUILayout.Width(46f))) debugGemAmount = 10000L;

                    var skillGacha = Onikiri.Progression.SkillGachaSystem.Instance;
                    var yodoGacha = Onikiri.Progression.GachaSystem.Instance;

                    using (new EditorGUI.DisabledScope(skillGacha == null))
                        if (GUILayout.Button("오의 10연 값", GUILayout.Width(92f)))
                            debugGemAmount = skillGacha.CostFor(
                                Onikiri.Progression.SkillGachaCurve.TenPullCount);

                    using (new EditorGUI.DisabledScope(yodoGacha == null))
                        if (GUILayout.Button("요도 10연 값", GUILayout.Width(92f)))
                            debugGemAmount = yodoGacha.CostFor(
                                Onikiri.Progression.GachaCurve.TenPullCount);

                    // 경계 확인용. 딱 맞을 때와 하나 모자랄 때 버튼이 다르게
                    // 서야 하고, 그 두 자리를 손으로 만드는 것이 이 칸의 목적이다
                    if (GUILayout.Button("-1", GUILayout.Width(34f)))
                        debugGemAmount = System.Math.Max(0L, debugGemAmount - 1L);
                }
            }

            if (gems == null)
                EditorGUILayout.HelpBox(
                    "GemWallet이 아직 없습니다. 플레이 모드에 들어가면 켜집니다.",
                    MessageType.None);
        }

        private void DrawCheats()
        {
            EditorGUILayout.LabelField("조작", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var wallet = PlayerWallet.Instance;
                using (new EditorGUI.DisabledScope(wallet == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("골드", GUILayout.Width(64f));
                    if (GUILayout.Button("+1K")) wallet.Add(BigDouble.FromDouble(1e3d));
                    if (GUILayout.Button("+1M")) wallet.Add(BigDouble.FromDouble(1e6d));
                    if (GUILayout.Button("+1T")) wallet.Add(BigDouble.FromDouble(1e12d));
                }

                DrawGemCheat();

                if (upgrades != null)
                {
                    for (int i = 0; i < upgrades.TrackCount; i++)
                    {
                        var track = upgrades.GetTrack(i);
                        if (track == null) continue;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                string.Format("{0} Lv.{1}", track.DisplayName, track.Level),
                                GUILayout.Width(150f));

                            // E-3 수정 확인용: 다음 칸의 비용. 강화 비용은 정수
                            // 골드(최소 1)가 규칙이라(UpgradeCost) 정수면 화면과
                            // 같은 축약 표기로, 소수가 새어 나오면 "?!"를 붙여
                            // 그대로 보인다 - 이 줄에 소수점이 보이면 규칙이 깨진 것
                            double rawCost = track.Cost.ToDouble();
                            bool whole = rawCost >= 1e15d
                                || System.Math.Abs(rawCost - System.Math.Round(rawCost)) < 1e-9d;
                            EditorGUILayout.LabelField(
                                whole ? NumberFormatter.Format(track.Cost)
                                      : rawCost.ToString("F2") + "?!",
                                GUILayout.Width(70f));

                            int index = i;
                            if (GUILayout.Button("+1")) upgrades.TryPurchase(index);
                            if (GUILayout.Button("+10"))
                                for (int n = 0; n < 10 && upgrades.TryPurchase(index); n++) { }
                            if (GUILayout.Button("최대"))
                                for (int n = 0; n < 10000 && upgrades.TryPurchase(index); n++) { }
                        }
                    }
                }

                DrawMasteryTools();
                DrawResetTools();

                using (new EditorGUI.DisabledScope(combat == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("공격속도", GUILayout.Width(64f));
                    // 슬라이더 상한이 곧 아트가 정한 공격속도 상한이다(AttackSpeedCurve).
                    // 예전에는 40까지 밀 수 있었는데, 그 구간에서 스윙이 4배속을 넘어
                    // 프레임 단위로 깜빡였고 그것을 "고속 공격"으로 착각한 채 밸런스를
                    // 잡았다. 넘길 수 없게 만드는 편이 경고 문구보다 확실하다
                    float max = combat != null ? combat.MaxAttacksPerSecond : 4f;
                    float value = EditorGUILayout.Slider(combat != null ? combat.AttacksPerSecond : 0f, 0.5f, max);
                    if (combat != null && !Mathf.Approximately(value, combat.AttacksPerSecond))
                        combat.AttacksPerSecond = value;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("시간 배속", GUILayout.Width(64f));
                    // Time.timeScale을 직접 쓰지 않는다. 히트스톱이 정지 직전 값을
                    // 기억했다가 되돌리는 구조라, 정지 중에 직접 바꾼 값은 기억에
                    // 반영되지 않아 다음 해제에서 지워진다. HitStop 참고
                    if (GUILayout.Button("0.25x")) HitStop.RequestBaseTimeScale(0.25f);
                    if (GUILayout.Button("1x")) HitStop.RequestBaseTimeScale(1f);
                    if (GUILayout.Button("4x")) HitStop.RequestBaseTimeScale(4f);
                }

                using (new EditorGUI.DisabledScope(spawner == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("요괴 전멸"))
                    {
                        foreach (var enemy in spawner.Active)
                            if (enemy != null && enemy.IsAlive)
                                enemy.TakeDamage(BigDouble.FromDouble(1e300d));
                    }

                    if (GUILayout.Button("타격음 재생") && hitAudio != null) hitAudio.PlayHit();
                    if (GUILayout.Button("처치음 재생") && hitAudio != null) hitAudio.PlayKill();
                }

                using (new EditorGUI.DisabledScope(stage == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("스테이지", GUILayout.Width(64f));
                    // 9단계부터 처치만으로는 스테이지가 오르지 않는다. "보스 열기"는
                    // 할당량을 채워 도전 버튼을 띄우고, "다음 스테이지"는 보스를
                    // 건너뛰고 직접 올린다 - 후반 밸런스를 확인할 때 필요하다
                    if (GUILayout.Button("보스 열기"))
                        for (int n = stage.KillsThisStage; n < stage.KillsRequired; n++) stage.RegisterKill();
                    if (GUILayout.Button("다음 스테이지")) stage.AdvanceStage();
                    if (GUILayout.Button("+10 스테이지"))
                        for (int n = 0; n < 10; n++) stage.AdvanceStage();
                    if (GUILayout.Button("1로")) stage.SetProgress(1, 0, 0);
                }

                DrawLevelTools();
                DrawBossTools();
                DrawRegionMobTools();
                DrawDeepZoneTools();
                DrawLockPreviewTools();
            }

            DrawPolishTools();

            DrawSkillTools();

            // 오의 바로 다음이다. 둘 다 참격을 PackSlash 풀에서 꺼내 쓰므로,
            // 두 절이 붙어 있으면 사무라이의 참격과 요괴의 참격을 한 화면에서
            // 번갈아 터뜨려 크기와 톤을 비교할 수 있다
            DrawEnemyAnimationTools();

            DrawQuestTools();

            // 가이드는 퀘스트 바로 다음이다. 가리키는 것이 위 절의 업적이고
            // 수령도 그쪽에서 일어나므로, 두 절이 붙어 있으면 "받는다 ->
            // 카드가 다음 칸으로 넘어간다"를 눈을 안 옮기고 본다
            DrawGuideQuestTools();

            // 장비는 퀘스트 바로 다음이다. 보석이 그쪽에서 나와 이쪽으로
            // 들어가므로, 두 절이 붙어 있으면 루프 전체를 한 화면에서 돌린다
            DrawEquipmentTools();

            // 무기 참격 티어는 장비 바로 다음이다 (51단계). 티어의 구동값이
            // 위 절의 무기 등급이라, 두 절이 붙어 있으면 "등급을 올린다 ->
            // 참격이 달라진다"를 한 화면에서 돌린다
            DrawWeaponVfxTierTools();

            // 요도는 장비 바로 다음이다. **같은 화면(대장간)의 옆 탭**이고,
            // 보석 소비처(파편 조달)도 하나 더 여기 있다 - 퀘스트 -> 장비 ->
            // 요도가 한 화면에서 도는 보석 루프다
            DrawYodoTools();

            // 뽑기·상점은 요도 바로 다음이다 (46단계). 파는 것이 전부 요도의
            // 재료(파편·혼 정수)라, 위 절의 자루 목록이 곧 이 절의 결과판이다 -
            // 뽑고 나서 눈을 옮기지 않고 티어가 오르는 것을 본다
            DrawGachaTools();

            // 오의 뽑기는 요도 뽑기 **바로 다음**이다 (50단계). 같은 상점의
            // 두 배너이고 같은 사다리를 쓰므로, 확률표 두 벌이 나란히 서면
            // "무엇이 갈렸는가"가 한눈에 읽힌다 - 갈리는 것은 결과의 이름뿐이다
            DrawSkillGachaTools();

            // 전직도 보석 소비처라 장비 바로 다음이다 (33단계)
            DrawEvolutionTools();

            // 동료도 보석 소비처(해금)라 전직 다음이다. 레벨은 골드다
            DrawPetTools();

            DrawSaveTools();

            // 클라우드는 세이브 바로 다음이다. 올라가는 값(도달층)이 세이브가
            // 들고 있는 값이고, 위 절의 "지금 저장"과 아래 절의 write가 같은
            // 숫자를 서로 다른 곳에 적는 일이라 나란히 두면 둘이 어긋나는 것이 보인다
            DrawCloudTools();

            // 인트로는 클라우드 다음이다 - 부팅 게이트가 보는 것(세이브 로드
            // 결과)과 타이틀 CTA가 부르는 것(계정 연동)이 바로 위 두 절이다
            DrawIntroTools();
        }

        /**
         * @brief 부팅 화면(인트로 스텝)의 상태와 재생.
         *
         * 인트로는 한 실행에 한 번이라, 고치고 확인하려면 매번 플레이를
         * 껐다 켜야 한다 - "다시 보기"가 그 비용을 없앤다. "계정 선택 기록
         * 지우기"는 첫 실행(계정 선택 화면)을 재현하는 스위치다.
         */
        private void DrawIntroTools()
        {
            EditorGUILayout.LabelField("인트로 (부팅 화면)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var flow = Object.FindFirstObjectByType<Onikiri.UI.IntroFlow>(
                    FindObjectsInactive.Include);

                bool chosen = PlayerPrefs.GetInt(Onikiri.UI.IntroFlow.AccountChosenKey, 0) == 1;
                Row("계정 선택 기록", chosen
                    ? "있음 (타이틀 = 터치하여 시작)"
                    : "없음 (타이틀 = 구글/게스트 선택)");
                Row("세이브 로드 결과", SaveSystem.LastOutcome.ToString());
                Row("오버레이", flow == null ? "(씬에 없음 - 빌더를 돌리세요)"
                    : flow.gameObject.activeInHierarchy ? "떠 있음" : "내려감 (게임 진입됨)");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("계정 선택 기록 지우기 (첫 실행 재현)"))
                    {
                        PlayerPrefs.DeleteKey(Onikiri.UI.IntroFlow.AccountChosenKey);
                        PlayerPrefs.Save();
                    }

                    using (new EditorGUI.DisabledScope(flow == null))
                    {
                        if (GUILayout.Button("다시 보기")) flow.Replay();
                        if (GUILayout.Button("건너뛰기")) flow.SkipToGame();
                    }
                }

                EditorGUILayout.HelpBox(
                    "부팅 순서: 스플래시(202 STUDIO -> ONIKIRI, 탭 스킵) -> 타이틀 -> 게임.\n"
                    + "게임 진입은 세이브 적용 뒤에만 열립니다(로딩 게이트). Firebase는 게이트에 없어\n"
                    + "오프라인·초기화 실패에도 게스트 진입이 됩니다. 구글 CTA는 실기 전용입니다.",
                    MessageType.Info);
            }
        }

        /**
         * @brief 퀘스트 상태와 강제 진행.
         *
         * 일일 리셋은 실제로 자정을 기다려야 확인할 수 있고, 반복 티어는 요괴
         * 백 마리를 잡아야 열린다. 방치 보상을 확인하려고 저장 시각을 과거로
         * 돌린 것과 같은 이유로, **기다리지 않고 그 상태를 만드는 경로**를 둔다.
         *
         * 카운터를 채우는 것과 리셋은 실제 코드 경로(QuestSystem)를 그대로
         * 지난다 - 상태를 직접 쓰면 이 패널로 본 화면이 실제 플레이의 화면과
         * 같다고 말할 수 없다.
         */
        private void DrawQuestTools()
        {
            EditorGUILayout.LabelField("퀘스트", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var quests = Onikiri.Progression.QuestSystem.Instance;
                var gems = Onikiri.Progression.GemWallet.Instance;

                if (quests == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 QuestSystem이 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("보석 {0}   받을 것 {1}개   초기화까지 {2:hh\\:mm\\:ss}",
                            gems != null ? gems.Gems : 0L,
                            quests.TotalClaimable,
                            quests.UntilDailyReset(System.DateTime.UtcNow)),
                        GUILayout.Width(340f));

                    if (GUILayout.Button("카운터 채우기")) quests.DebugFillCounters();

                    // 자정을 기다리지 않고 리셋을 본다. 오늘치만 비우고 누적은
                    // 그대로다 - 실제 자정과 같은 동작이다
                    if (GUILayout.Button("일일 초기화")) quests.DebugForceDailyReset();

                    if (GUILayout.Button("진행 초기화", GUILayout.Width(86f)))
                    {
                        quests.DebugResetProgress();
                        Debug.Log("[Onikiri] 퀘스트 진행·수령을 전부 비웠다. "
                                  + "보석 잔액은 건드리지 않는다 - 이미 받은 것을 "
                                  + "회수하는 것은 초기화가 아니라 몰수다.");
                    }
                }

                foreach (var kind in new[] { Onikiri.Progression.QuestKind.Daily,
                                             Onikiri.Progression.QuestKind.Repeat,
                                             Onikiri.Progression.QuestKind.Achievement })
                {
                    var specs = Onikiri.Progression.QuestCatalog.Of(kind);
                    int ready = 0;
                    for (int i = 0; i < specs.Length; i++) ready += quests.ClaimableCount(kind, i);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            string.Format("{0}  {1}줄  받을 것 {2}", kind, specs.Length, ready),
                            GUILayout.Width(220f));

                        using (new EditorGUI.DisabledScope(ready == 0))
                        {
                            if (GUILayout.Button("전부 받기", GUILayout.Width(80f)))
                            {
                                // 한 번에 하나씩 도는 것이 실제 버튼과 같은 경로다
                                for (int i = 0; i < specs.Length; i++)
                                    while (quests.TryClaim(kind, i)) { }
                            }
                        }
                    }
                }
            }
        }

        /**
         * @brief 플레이 화면 가이드 카드.
         *
         * ## 왜 이 절이 필요한가
         *
         * 이 카드가 말하는 네 상태 중 셋은 **기다려야만 오는 상태**다.
         * 완료 가능은 목표치를 채워야 오고, 수령 완료는 그다음 1.4초뿐이며,
         * "표시할 가이드 없음"은 열네 칸을 전부 받아야 온다. 눈으로 확인하려면
         * 실제로 그만큼 플레이해야 하는데, 그 준비가 확인보다 몇십 배 길다.
         *
         * `한 칸 넘기기`는 지금 카드가 가리키는 업적을 **정식 경로로 받는다**
         * (QuestSystem.TryClaim). 조건을 안 채웠으면 못 받으므로, 그때는
         * 위 절의 `카운터 채우기`나 아래 `조건 채우기`를 먼저 누른다 -
         * 치트가 가이드만의 뒷문을 여는 것이 아니라 퀘스트와 같은 문으로
         * 들어간다는 것이 이 절의 규칙이다.
         *
         * ## 표가 아니라 실물을 읽는다
         *
         * 어느 칸이 떠 있는지를 표에서 유추하지 않고 씬의 컴포넌트가 지금
         * 들고 있는 값(CurrentStep·CurrentState)을 읽는다. 카드가 화면에
         * 그린 것과 여기 뜨는 것이 어긋나면 그것이 곧 버그다.
         */
        private void DrawGuideQuestTools()
        {
            EditorGUILayout.LabelField("가이드 퀘스트 (플레이 화면 카드)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var quests = Onikiri.Progression.QuestSystem.Instance;
                if (quests == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 QuestSystem이 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                var card = Object.FindFirstObjectByType<Onikiri.UI.GuideQuestCard>(
                    FindObjectsInactive.Include);

                if (card == null)
                    EditorGUILayout.HelpBox(
                        "씬에 GuideQuestCard가 없습니다. Onikiri/Scene/Build Combat Content 를 "
                        + "실행하면 전투 화면 오른쪽(퀘스트 아이콘 아래)에 세워집니다.",
                        MessageType.Warning);

                // 진행선이 지금 고르는 칸. 카드와 같은 함수를 부르므로
                // 화면에 뜬 것과 여기 뜬 것이 다르면 카드 쪽이 안 듣고 있다는 뜻이다
                var view = Onikiri.Progression.GuideQuestLine.Resolve(quests);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        view.Step < 0
                            ? string.Format("진행선 {0}칸 · 남은 칸 없음", view.StepCount)
                            : string.Format("{0}/{1}번째 · {2} · {3} {4}/{5} · 보석 {6}",
                                view.Step + 1, view.StepCount, view.State,
                                view.Title,
                                Onikiri.Progression.GuideQuestLine.FormatCount(view.Progress),
                                Onikiri.Progression.GuideQuestLine.FormatCount(view.Target),
                                view.Gems),
                        EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        card != null
                            ? string.Format("카드 표시: {0}칸 · {1}", card.CurrentStep + 1, card.CurrentState)
                            : "카드 없음",
                        GUILayout.Width(220f));

                    using (new EditorGUI.DisabledScope(!Application.isPlaying || view.Step < 0))
                    {
                        // 지금 칸의 조건만 채운다. 카드가 "완료 가능"(금빛)으로
                        // 바뀌는 순간을 보는 경로다. 스테이지·레벨 지표는
                        // 카운터가 아니라 지금 상태를 읽으므로 여기서 못 민다 -
                        // 그때는 위쪽 치트(스테이지 이동·레벨업)를 쓴다
                        if (GUILayout.Button("조건 채우기"))
                        {
                            quests.DebugFillCounters();
                            Debug.Log("[Onikiri] 가이드: 카운터를 채웠다. 스테이지·레벨 지표는 "
                                      + "카운터가 아니라 지금 상태라 안 움직인다 - 그 칸은 "
                                      + "스테이지 이동/레벨업 치트로 넘긴다. 새로 완료된 "
                                      + "퀘스트는 퀘스트 아이콘의 빨간 배지가 알린다.");
                        }

                        // 정식 경로다. 못 받는 상태면 false가 돌아온다
                        if (GUILayout.Button("한 칸 넘기기"))
                        {
                            var current = Onikiri.Progression.GuideQuestCatalog.Steps[view.Step];
                            bool claimed = quests.TryClaim(current.Kind, current.Index);
                            Debug.Log(claimed
                                ? "[Onikiri] 가이드: " + view.Title + " 수령. 카드가 1.4초 동안 "
                                  + "'보상 수령 완료'를 세운 뒤 다음 칸으로 넘어간다."
                                : "[Onikiri] 가이드: " + view.Title + " 은 아직 조건 미달이라 "
                                  + "못 받는다. 조건을 먼저 채워라.");
                        }
                    }

                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        // "표시할 가이드 없음"(카드가 사라지는 상태)까지 한 번에 간다
                        if (GUILayout.Button("전부 받기", GUILayout.Width(80f)))
                        {
                            int claimed = 0;
                            foreach (var line in Onikiri.Progression.GuideQuestCatalog.Steps)
                            {
                                if (line.Index < 0) continue;
                                while (quests.TryClaim(line.Kind, line.Index)) claimed++;
                            }
                            Debug.Log("[Onikiri] 가이드: " + claimed + "칸 수령. 남은 칸이 없으면 "
                                      + "카드가 알파 0으로 사라진다(오브젝트는 켜져 있다).");
                        }

                        /**
                         * @brief 진행선을 처음으로 되감는다.
                         *
                         * 위 절의 `진행 초기화`(DebugResetProgress)와 다르다 -
                         * 그쪽은 일일·반복·업적을 통째로 비우고 **누적 카운터까지
                         * 0으로 만든다.** 가이드만 다시 보려는데 그렇게 하면 그
                         * 뒤에 뜨는 화면은 확인하려던 화면이 아니라 새 계정이다.
                         *
                         * 여기서는 **가이드가 가리키는 14칸의 수령 기록만** 지운다.
                         * 카운터·잔액·다른 퀘스트는 그대로다.
                         *
                         * ⚠️ 가이드는 자기 상태를 안 갖는다 - 수령 기록의 원본은
                         * 업적이다(GuideQuestCatalog 머리 주석). 그래서 이 버튼은
                         * **퀘스트 화면의 그 업적들도 함께 되돌린다.** 두 화면이
                         * 같은 사실을 보고 있다는 뜻이고, 그것이 이 설계의 요점이다.
                         * 되감은 보상은 다시 받을 수 있다 - 테스트 패널 전용인 이유다.
                         */
                        if (GUILayout.Button("가이드 초기화", GUILayout.Width(96f)))
                        {
                            int cleared = 0;
                            foreach (var line in Onikiri.Progression.GuideQuestCatalog.Steps)
                            {
                                if (line.Index < 0) continue;

                                // 반복은 수령 기록이 불리언이 아니라 티어 수다
                                bool had = line.Kind == Onikiri.Progression.QuestKind.Repeat
                                    ? quests.RepeatTier(line.Index) > 0
                                    : quests.IsClaimed(line.Kind, line.Index);
                                if (!had) continue;

                                quests.DebugUnclaim(line.Kind, line.Index);
                                cleared++;
                            }
                            Debug.Log("[Onikiri] 가이드: " + cleared + "칸의 수령 기록을 지웠다. "
                                      + "카드가 1번째 칸부터 다시 선다. 카운터·잔액은 안 건드렸고, "
                                      + "가리키는 업적이 퀘스트 화면에서도 함께 미수령으로 돌아간다 "
                                      + "- 가이드는 자기 상태를 안 갖는다.");
                        }
                    }
                }

                // 진행선 전체. 어느 칸이 막혀 있는지가 한눈에 보여야 순서를
                // 고친 결과를 확인할 수 있다
                foreach (var step in Onikiri.Progression.GuideQuestCatalog.Steps)
                {
                    if (step.Index < 0)
                    {
                        EditorGUILayout.LabelField(
                            "  ! " + step.QuestId + " - QuestCatalog에 없음",
                            EditorStyles.miniLabel);
                        continue;
                    }

                    var spec = Onikiri.Progression.QuestCatalog.Of(step.Kind)[step.Index];
                    bool done = quests.IsClaimed(step.Kind, step.Index);
                    bool ready = !done && quests.ClaimableCount(step.Kind, step.Index) > 0;

                    EditorGUILayout.LabelField(
                        string.Format("  {0} {1}  ({2})  {3}/{4}",
                            done ? "받음" : ready ? "받을 수 있음" : "진행 중",
                            spec.Title, step.Action,
                            Onikiri.Progression.GuideQuestLine.FormatCount(
                                quests.ProgressOf(step.Kind, step.Index)),
                            Onikiri.Progression.GuideQuestLine.FormatCount(spec.Target)),
                        EditorStyles.miniLabel);
                }
            }
        }

        /**
         * @brief 장비(대장간). **두 재화가 걸린 유일한 화면이라 둘 다 만질 수 있어야 한다.**
         *
         * 퀘스트 절과 나란히 둔다. 보석이 그쪽에서 나와 이쪽으로 들어가므로
         * 두 절이 붙어 있으면 루프 전체를 한 화면에서 돌려볼 수 있다 -
         * 카운터 채우기 -> 전부 받기 -> 등급업.
         *
         * `한 칸 올리기`는 재화를 무시한다. 다섯 등급 열 칸을 눈으로 훑는 데
         * 골드 수억과 보석 오백을 먼저 만들어야 하면, 그 준비가 확인보다 길다.
         */
        private void DrawEquipmentTools()
        {
            EditorGUILayout.LabelField("장비 (대장간)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var equipment = Onikiri.Progression.EquipmentSystem.Instance;
                if (equipment == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 EquipmentSystem이 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("{0}  ·  누를 수 있는 버튼 {1}개",
                            equipment.IsUnlocked
                                ? "개방"
                                : "잠김 (st" + Onikiri.Progression.EquipmentCurve.UnlockStage + " 필요)",
                            equipment.AffordableCount),
                        GUILayout.Width(300f));

                    if (GUILayout.Button("장비 초기화", GUILayout.Width(96f)))
                    {
                        equipment.DebugResetEquipment();
                        Debug.Log("[Onikiri] 장비를 1등급 Lv.1로 되돌렸다. 골드도 보석도 "
                                  + "돌려주지 않는다 - 환불은 초기화가 아니라 별개의 치트다.");
                    }
                }

                for (int i = 0; i < equipment.SlotCount; i++)
                {
                    var slot = equipment.GetSlot(i);
                    if (slot == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.Format(
                            "{0}  {1} {2}등급 Lv.{3}  ×{4:F3}",
                            slot.slotName, slot.GradeName, slot.grade, slot.level,
                            equipment.MultiplierOf(i)), GUILayout.Width(300f));

                        int index = i;

                        using (new EditorGUI.DisabledScope(!equipment.CanTemper(index)))
                            if (GUILayout.Button("단련", GUILayout.Width(60f)))
                                equipment.TryTemper(index);

                        using (new EditorGUI.DisabledScope(!equipment.CanUpgradeGrade(index)))
                            if (GUILayout.Button("등급업", GUILayout.Width(70f)))
                                equipment.TryUpgradeGrade(index);

                        // 재화를 무시하고 한 칸. 곡선을 훑는 경로다
                        using (new EditorGUI.DisabledScope(equipment.IsMaxed(index)))
                            if (GUILayout.Button("한 칸 올리기", GUILayout.Width(96f)))
                                equipment.DebugAdvance(index);
                    }
                }
            }
        }

        /**
         * @brief 무기 참격 티어 (51단계). **연출만 강제하는 스위치가 이 절의 이유다.**
         *
         * 위 장비 절의 "한 칸 올리기"로도 티어는 바뀌지만 그쪽은 스탯까지
         * 바꾼다 - 등급 1과 5의 참격을 나란히 비교하려고 장비를 다섯 번
         * 올렸다 되돌리면, 되돌리는 것을 잊는 순간 밸런스 확인이 오염된다.
         * WeaponVfxTier.DebugForcedTier는 연출 코드만 읽는 값이라 안전하다.
         *
         * 참격을 실제로 터뜨리는 버튼은 발도 오의 절(지금 시전)에 있다 -
         * 여기서 티어를 강제하고 그쪽에서 귀참을 쏘는 것이 확인 루프다.
         */
        private void DrawWeaponVfxTierTools()
        {
            EditorGUILayout.LabelField("무기 참격 티어 (연출)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int tier = Onikiri.Battle.WeaponVfxTier.CurrentTier();
                bool premium = Onikiri.Battle.WeaponVfxTier.IsPremium();
                bool forced = Onikiri.Battle.WeaponVfxTier.DebugForcedTier != 0
                              || Onikiri.Battle.WeaponVfxTier.DebugForcedPremium != -1;

                EditorGUILayout.LabelField(string.Format(
                    "지금 티어 {0} · 스파크 {1}겹 · 평타 오라 α{2:F2} · 오니키리 {3}{4}",
                    tier,
                    Onikiri.Battle.WeaponVfxTier.SparkLayers(tier, premium),
                    Onikiri.Battle.WeaponVfxTier.GlowAlpha(tier, premium),
                    premium ? "완성" : "미완성",
                    forced ? "  (강제 중)" : ""));

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("티어 강제", GUILayout.Width(70f));

                    for (int t = Onikiri.Battle.WeaponVfxTier.MinTier;
                         t <= Onikiri.Battle.WeaponVfxTier.MaxTier; t++)
                    {
                        bool on = Onikiri.Battle.WeaponVfxTier.DebugForcedTier == t;
                        if (GUILayout.Toggle(on, t.ToString(), "Button", GUILayout.Width(28f)) != on)
                            Onikiri.Battle.WeaponVfxTier.DebugForcedTier = on ? 0 : t;
                    }

                    if (GUILayout.Button("해제", GUILayout.Width(50f)))
                    {
                        Onikiri.Battle.WeaponVfxTier.DebugForcedTier = 0;
                        Onikiri.Battle.WeaponVfxTier.DebugForcedPremium = -1;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("오니키리 강제", GUILayout.Width(90f));

                    bool forcedOn = Onikiri.Battle.WeaponVfxTier.DebugForcedPremium == 1;
                    if (GUILayout.Toggle(forcedOn, "완성", "Button", GUILayout.Width(50f)) != forcedOn)
                        Onikiri.Battle.WeaponVfxTier.DebugForcedPremium = forcedOn ? -1 : 1;

                    bool forcedOff = Onikiri.Battle.WeaponVfxTier.DebugForcedPremium == 0;
                    if (GUILayout.Toggle(forcedOff, "미완성", "Button", GUILayout.Width(60f)) != forcedOff)
                        Onikiri.Battle.WeaponVfxTier.DebugForcedPremium = forcedOff ? -1 : 0;
                }

                EditorGUILayout.LabelField(
                    "참격은 발도 오의 절의 '지금 시전'으로, 평타 오라는 그냥 두면 뜬다. "
                    + "강제는 연출만 바꾼다 - 스탯·데미지는 실제 등급 그대로다.",
                    EditorStyles.miniLabel);
            }
        }

        /**
         * @brief 요도 (요괴 봉인 검). **이 축은 골드로 살 수 없어서 절이 다르게 생겼다.**
         *
         * 다른 절은 전부 "재화를 만들고 -> 버튼을 누른다"인데, 여기서 만들어야
         * 하는 것은 **혼**이고 혼은 40스테이지에 하나씩만 떨어진다. 그래서
         * 치트의 무게중심이 재화가 아니라 **드랍**에 있다:
         *
         *   혼 +1        대요괴를 실제로 벨 때와 같은 결과를 만든다. 도감도 켜진다
         *   파편 +100    정예 네 마리분. 합성이 막히는 자리를 건너뛴다
         *   봉인/합성    **실제 구매 경로**(혼 + 파편)를 그대로 탄다
         *   한 티어      재화를 무시한다. 열 티어를 훑는 데 열 바퀴(400스테이지)를
         *                돌아야 하면 그 준비가 확인보다 길다
         *
         * `요도 초기화`는 보석을 돌려주지 않는다 - 환불은 초기화가 아니라
         * 별개의 치트라는 규칙 그대로다.
         */
        private void DrawYodoTools()
        {
            EditorGUILayout.LabelField("요도 (요괴 봉인 검)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var yodo = Onikiri.Progression.YodoSystem.Instance;
                if (yodo == null)
                {
                    // 에디트 모드에서도 절이 보이게 - 씬의 컴포넌트를 직접 찾는다
                    yodo = Object.FindFirstObjectByType<Onikiri.Progression.YodoSystem>(
                        FindObjectsInactive.Include);
                }

                if (yodo == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 YodoSystem이 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "{0}  ·  파편 {1}  ·  봉인 {2}/{3}  ·  ×{4:F3}",
                        yodo.IsUnlocked
                            ? "개방"
                            : "잠김 (st" + Onikiri.Progression.YodoCurve.UnlockStage + " 필요)",
                        yodo.Shards, yodo.SealedCount,
                        Onikiri.Progression.YodoCatalog.Count, yodo.AttackMultiplier),
                        GUILayout.Width(330f));

                    if (GUILayout.Button("파편 +100", GUILayout.Width(84f)))
                        yodo.DebugGrantShards(100L);

                    // 실제 구매 경로. 보석이 모자라면 눌리지 않는다
                    using (new EditorGUI.DisabledScope(!yodo.CanBuyShards))
                        if (GUILayout.Button("파편 조달(보석)", GUILayout.Width(112f)))
                            yodo.TryBuyShards();

                    if (GUILayout.Button("요도 초기화", GUILayout.Width(96f)))
                    {
                        yodo.DebugReset();
                        Debug.Log("[Onikiri] 요도를 미봉인·혼 0·파편 0으로 되돌렸다. "
                                  + "보석은 돌려주지 않는다 - 환불은 초기화가 아니라 별개의 치트다.");
                    }
                }

                for (int i = 0; i < yodo.BladeCount; i++)
                {
                    var blade = yodo.GetBlade(i);
                    if (blade == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.Format(
                            "{0}  {1}  혼 {2}  ×{3:F3}",
                            blade.Sealed ? blade.bladeName : blade.soulName,
                            blade.Sealed
                                ? "티어 " + blade.tier + "/" + Onikiri.Progression.YodoCurve.MaxTier
                                : "미봉인",
                            blade.souls, blade.Multiplier), GUILayout.Width(300f));

                        int index = i;

                        if (GUILayout.Button("혼 +1", GUILayout.Width(60f)))
                            yodo.DebugGrantSoul(index);

                        using (new EditorGUI.DisabledScope(!yodo.CanForge(index)))
                            if (GUILayout.Button(blade.Sealed ? "합성" : "봉인", GUILayout.Width(60f)))
                                yodo.TryForge(index);

                        // 재화를 무시하고 한 티어. 곡선을 훑는 경로다
                        using (new EditorGUI.DisabledScope(yodo.IsMaxed(index)))
                            if (GUILayout.Button("한 티어", GUILayout.Width(72f)))
                                yodo.DebugForge(index);
                    }
                }

                DrawYodoPowerTools(yodo);
            }
        }

        /**
         * @brief 뽑기 · 상점 (46단계). **절을 따로 세운 이유는 재화가 있기 때문이다.**
         *
         * 45단계의 상성·영체는 요도 절 안의 소절로 넣었다 - 손잡이가 요도 티어
         * 하나뿐이라 절을 나누면 화면이 거짓말을 했다. 뽑기는 반대다. 자기
         * 재화(보석)와 자기 상태(천장 카운터·일일 무료 쿨)를 갖고, 그 상태에
         * 도달하는 비용이 실제로 크다 - 천장 하나를 눈으로 보려면 무료 뽑기로
         * 서른 날이다.
         *
         * 그래서 여기 있는 것은 셋이다:
         *
         *   실제 경로   보석을 내고 단연·10연. 잔액이 모자라면 눌리지 않는다
         *   치트        보석 없이 돌리기 · 천장 직전으로 밀기 · 무료 쿨 리셋
         *   보는 것     확률표(코드가 내는 값 그대로) · 리드 상한이 지금 몇인가
         *
         * 마지막 것이 이 절의 존재 이유에 가깝다. **리드 상한**은 이 스텝
         * 밸런스의 전부인데(GachaCurve.LeadTiers) 화면 어디에도 숫자로 뜨지
         * 않는다 - 뽑았는데 파편만 나오는 것이 확률 때문인지 상한 때문인지를
         * 가를 수 있는 곳이 여기뿐이다.
         */
        private void DrawGachaTools()
        {
            EditorGUILayout.LabelField("뽑기 · 상점", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var gacha = Onikiri.Progression.GachaSystem.Instance;
                if (gacha == null)
                {
                    gacha = Object.FindFirstObjectByType<Onikiri.Progression.GachaSystem>(
                        FindObjectsInactive.Include);
                }

                if (gacha == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 GachaSystem이 없습니다. Onikiri/Build Shop Panel 을 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "{0}  ·  천장까지 {1}회  ·  누적 {2}회  ·  무료 {3}",
                        gacha.IsUnlocked
                            ? "개방"
                            : "잠김 (st" + Onikiri.Progression.GachaCurve.UnlockStage + " 필요)",
                        gacha.PullsUntilPity, gacha.TotalPulls,
                        gacha.HasFreePull ? "가능" : "오늘 씀"), GUILayout.Width(330f));

                    // 실제 구매 경로. 보석이 모자라면 눌리지 않는다
                    using (new EditorGUI.DisabledScope(!gacha.CanPull(1)))
                        if (GUILayout.Button("단연(보석 " + gacha.CostFor(1) + ")",
                                             GUILayout.Width(112f)))
                            gacha.TryPull(1);

                    int ten = Onikiri.Progression.GachaCurve.TenPullCount;
                    using (new EditorGUI.DisabledScope(!gacha.CanPull(ten)))
                        if (GUILayout.Button(ten + "연(보석 " + gacha.CostFor(ten) + ")",
                                             GUILayout.Width(112f)))
                            gacha.TryPull(ten);

                    using (new EditorGUI.DisabledScope(!gacha.HasFreePull))
                        if (GUILayout.Button("무료 뽑기", GUILayout.Width(84f)))
                            gacha.TryFreePull();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("치트", GUILayout.Width(40f));

                    if (GUILayout.Button("공짜 1회", GUILayout.Width(72f))) gacha.DebugPull(1);
                    if (GUILayout.Button("공짜 10회", GUILayout.Width(80f)))
                        gacha.DebugPull(Onikiri.Progression.GachaCurve.TenPullCount);

                    if (GUILayout.Button("천장 직전", GUILayout.Width(80f)))
                        gacha.DebugPushToPity();

                    if (GUILayout.Button("무료 쿨 리셋", GUILayout.Width(96f)))
                        gacha.DebugResetFreePull();

                    if (GUILayout.Button("뽑기 초기화", GUILayout.Width(96f)))
                    {
                        gacha.DebugReset();
                        Debug.Log("[Onikiri] 뽑기를 천장 0 · 누적 0 · 무료 미사용으로 되돌렸다. "
                                  + "보석과 이미 들어간 파편·혼은 돌려주지 않는다 - 환불은 "
                                  + "초기화가 아니라 별개의 치트다.");
                    }
                }

                // 확률표. 손으로 적지 않고 코드가 내는 값을 그대로 그린다 -
                // 화면(상점 배너)과 여기가 갈리면 어느 쪽이 진짜인지 알 수 없다
                foreach (string line in GachaTableLines()) EditorGUILayout.LabelField(line);

                DrawEssenceCapTools(gacha);
                DrawLadderTools();
            }
        }

        /**
         * @brief 확률표. **표의 값과 실효 값을 나란히 적는다.**
         *
         * 47단계에 두 값이 갈렸다 - 천장이 ★4를 보장하므로 아래 등급은
         * 눌리고 ★4는 밀려 올라간다(GachaCurve.PityShare). 화면(상점 배너)은
         * **표의 값**만 공개하는데, 실제로 몇 번에 하나가 나오는지는 실효
         * 값이라, 뽑았는데 안 나오는 것이 확률 때문인지 천장 때문인지를
         * 가를 수 있는 곳이 여기뿐이다.
         *
         * 등급별로 줄을 나눈 이유는 여섯이 한 줄에 들어가면 46단계가 화면에서
         * 겪은 것("파편 674%")이 패널에서 재현되기 때문이다.
         */
        private static string[] GachaTableLines()
        {
            var lines = new System.Collections.Generic.List<string>();

            for (int i = 0; i < Onikiri.Progression.GachaCurve.OutcomeCount; i++)
            {
                var grade = Onikiri.Progression.GachaCurve.GradeOf[i];
                var outcome = (Onikiri.Progression.GachaCurve.Outcome)i;

                string reward;
                switch (outcome)
                {
                    case Onikiri.Progression.GachaCurve.Outcome.SoulEssence:
                        reward = "혼 정수"; break;
                    case Onikiri.Progression.GachaCurve.Outcome.SoulRarity:
                        reward = "상위 혼"; break;
                    case Onikiri.Progression.GachaCurve.Outcome.LegendaryBlade:
                        reward = "전설 요도"; break;
                    default:
                        reward = "파편 " + Onikiri.Progression.GachaCurve.ShardsOf[i]; break;
                }

                lines.Add(string.Format("    {0} {1,-10}  표 {2,5:0.0}%",
                    Onikiri.Progression.GachaCurve.StarsFor(grade), reward,
                    Onikiri.Progression.GachaCurve.Chances[i] * 100d));
            }

            lines.Add(string.Format(
                "    실효(천장 접힘)  파편 {0:F2}  ·  ★3 {1:F2}%  ·  ★4 {2:F2}%  ·  ★5 {3:F2}%",
                Onikiri.Progression.GachaCurve.ExpectedShardsPerPull,
                Onikiri.Progression.GachaCurve.EffectiveEssenceChance * 100d,
                Onikiri.Progression.GachaCurve.EffectiveRarityChance * 100d,
                Onikiri.Progression.GachaCurve.EffectiveLegendaryChance * 100d));

            lines.Add(string.Format(
                "    ★4+ 하나에 {0:F1}회  ·  ★5 하나에 {1:F0}회  ·  파편/보석 {2:F3}"
                + " (촉매 {3:F3})",
                Onikiri.Progression.GachaCurve.ExpectedPullsPerEpic,
                Onikiri.Progression.GachaCurve.ExpectedPullsPerLegendary,
                Onikiri.Progression.GachaCurve.ExpectedShardsPerPull
                    / Onikiri.Progression.GachaCurve.PullCostGems,
                (double)Onikiri.Progression.YodoCurve.ShardPackShards
                    / Onikiri.Progression.YodoCurve.ShardPackGems));

            return lines.ToArray();
        }

        /**
         * @brief 오의 뽑기 (50단계). **이 절이 답하는 질문은 "왜 안 나오는가"다.**
         *
         * 이 배너의 결과는 화면에서 **거의 안 보인다.** 스킬 XP는 게이지 안으로
         * 사라지고, 해금은 목록에 줄 하나가 늘 뿐이며, 개안은 레벨 숫자가
         * 바뀌는 것이 전부다. 그래서 "뽑았는데 아무 일도 안 일어난다"가 이
         * 배너의 기본 증상이고, 그 원인이 넷이나 된다:
         *
         *   재고 소진   장착이 전부 상한 + 둘 다 해금 -> 배너가 닫힌다
         *   벤치로 감   장착이 전부 상한이면 XP가 안 끼운 오의로 흐른다
         *   미끄러짐    ★5가 ★4로, ★4가 ★3으로 내려간다
         *   상한        XP가 아무리 쌓여도 MaxLevel에서 멈춘다 (**이것이 설계다**)
         *
         * 넷 중 마지막만 정상이고 앞의 셋은 상황에 따라 정상이거나 버그다.
         * 그것을 가를 수 있는 곳이 여기뿐이라, 47단계가 혼격 상한 표를 놓은
         * 것과 같은 자리에 **XP 목적지 표**를 놓는다.
         */
        private void DrawSkillGachaTools()
        {
            EditorGUILayout.LabelField("오의 뽑기", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var gacha = Onikiri.Progression.SkillGachaSystem.Instance;
                if (gacha == null)
                {
                    gacha = Object.FindFirstObjectByType<Onikiri.Progression.SkillGachaSystem>(
                        FindObjectsInactive.Include);
                }

                if (gacha == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 SkillGachaSystem이 없습니다. Onikiri/Build Shop Panel 을 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "{0}  ·  천장까지 {1}회  ·  누적 {2}회  ·  무료 {3}",
                        !gacha.IsUnlocked
                            ? "잠김 (st" + Onikiri.Progression.SkillGachaCurve.UnlockStage + " 필요)"
                            : gacha.HasStock ? "개방" : "재고 소진",
                        gacha.PullsUntilPity, gacha.TotalPulls,
                        gacha.HasFreePull ? "가능" : "오늘 씀"), GUILayout.Width(330f));

                    using (new EditorGUI.DisabledScope(!gacha.CanPull(1)))
                        if (GUILayout.Button("단연(보석 " + gacha.CostFor(1) + ")",
                                             GUILayout.Width(112f)))
                            gacha.TryPull(1);

                    int ten = Onikiri.Progression.SkillGachaCurve.TenPullCount;
                    using (new EditorGUI.DisabledScope(!gacha.CanPull(ten)))
                        if (GUILayout.Button(ten + "연(보석 " + gacha.CostFor(ten) + ")",
                                             GUILayout.Width(112f)))
                            gacha.TryPull(ten);

                    using (new EditorGUI.DisabledScope(!gacha.HasFreePull))
                        if (GUILayout.Button("무료 뽑기", GUILayout.Width(84f)))
                            gacha.TryFreePull();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("치트", GUILayout.Width(40f));

                    // **재고를 안 본다.** 재고 판정이 맞는지를 확인하려면 재고가
                    // 없는 상태에서도 굴려 볼 수 있어야 하고, 그때 결과가
                    // 사다리를 어떻게 미끄러지는지가 이 배너의 유일한 볼거리다
                    if (GUILayout.Button("공짜 1회", GUILayout.Width(72f))) gacha.DebugPull(1);
                    if (GUILayout.Button("공짜 10회", GUILayout.Width(80f)))
                        gacha.DebugPull(Onikiri.Progression.SkillGachaCurve.TenPullCount);

                    if (GUILayout.Button("천장 직전", GUILayout.Width(80f)))
                        gacha.DebugPushToPity();

                    if (GUILayout.Button("무료 쿨 리셋", GUILayout.Width(96f)))
                        gacha.DebugResetFreePull();

                    if (GUILayout.Button("뽑기 초기화", GUILayout.Width(96f)))
                    {
                        gacha.DebugReset();
                        Debug.Log("[Onikiri] 오의 뽑기를 천장 0 · 누적 0 · 무료 미사용으로 "
                                  + "되돌렸다. 이미 열린 오의와 들어간 XP는 그대로다 - "
                                  + "그쪽은 '발도 오의' 절의 초기화가 되돌린다.");
                    }
                }

                foreach (string line in SkillGachaTableLines()) EditorGUILayout.LabelField(line);

                DrawSkillXpTools();
            }
        }

        /**
         * @brief 오의 뽑기 확률표. **표의 값과 실효 값을 두 줄로 적는다.**
         *
         * 47단계가 요도 표에서 한 것과 같은 처리다 - 화면(상점 배너)은 표만
         * 공개하고 여기서는 천장에 눌린 실효 값도 함께 본다. 두 값이 갈리는
         * 이유는 천장이 "★4+가 아닌 굴림"을 덮어쓰기 때문이고, 그 사실을
         * 모르면 "표에 3%인데 왜 이만큼 나오나"를 답할 수 없다.
         */
        private static string[] SkillGachaTableLines()
        {
            var lines = new System.Collections.Generic.List<string>();

            for (int i = 0; i < Onikiri.Progression.SkillGachaCurve.OutcomeCount; i++)
            {
                var grade = Onikiri.Progression.GachaCurve.GradeOf[i];
                var outcome = (Onikiri.Progression.SkillGachaCurve.Outcome)i;

                lines.Add(string.Format("  {0}  {1,-14} {2,5:F1}%",
                    Onikiri.Progression.GachaCurve.StarsFor(grade),
                    Onikiri.UI.GachaResultPopup.NameOfOutcome(outcome),
                    Onikiri.Progression.SkillGachaCurve.Chances[i] * 100d));
            }

            lines.Add(string.Format(
                "  실효(천장 눌림)  XP {0:F2}/회  ·  해금 {1:F3}%  ·  개안 {2:F3}%",
                Onikiri.Progression.SkillGachaCurve.ExpectedXpPerPull,
                Onikiri.Progression.SkillGachaCurve.EffectiveUnlockChance * 100d,
                Onikiri.Progression.SkillGachaCurve.EffectiveAwakenChance * 100d));

            lines.Add(string.Format(
                "  ★4+ 하나에 {0:F1}회  ·  오의 하나 상한까지 {1} XP = {2:F1}회 "
                + "(천장 {3}회)",
                Onikiri.Progression.SkillGachaCurve.ExpectedPullsPerUnlock,
                Onikiri.Progression.SkillGachaCurve.TotalXpToCap,
                Onikiri.Progression.SkillGachaCurve.TotalXpToCap
                    / Onikiri.Progression.SkillGachaCurve.ExpectedXpPerPull,
                Onikiri.Progression.SkillGachaCurve.PityPulls));

            return lines.ToArray();
        }

        /**
         * @brief XP가 **지금 어디로 가는가**. 이 표가 이 절의 이유다.
         *
         * 뽑은 XP는 풀에 들어갔다가 규칙 하나로 흘러간다: 장착 중 최저 레벨,
         * 없으면 벤치 중 최저 레벨(SkillSystem.SpendXp). 화면에는 레벨 숫자가
         * 바뀌는 것만 보이므로, **어느 오의가 다음 목적지인지**를 볼 수 있는
         * 곳이 여기뿐이다 - "XP가 안 들어온다"와 "XP가 다른 데로 갔다"가
         * 화면에서 구분되지 않는다.
         *
         * 치트는 XP 덩어리 하나와 개안이다. 둘 다 **상한을 지킨다** - 지키지
         * 않으면 이 패널이 오의 몫 계약을 깨는 유일한 경로가 되고, 그러면
         * 여기서 본 값이 게임의 값이라고 말할 수 없다. 47단계가 혼격 치트에서
         * 반대 판단을 한 것과 갈리는 자리이고, 이유는 그쪽 상한이 재고이지
         * 계약이 아니었기 때문이다.
         */
        private void DrawSkillXpTools()
        {
            var system = skills;
            if (system == null)
                system = Object.FindFirstObjectByType<Onikiri.Progression.SkillSystem>(
                    FindObjectsInactive.Include);
            if (system == null) return;

            int target = system.XpTargetIndex;
            long need = system.XpToNextLevel;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(target >= 0
                    ? string.Format("스킬 XP {0} / {1}  ->  {2} Lv.{3} {4}",
                        system.SkillXp, need,
                        system.GetSlot(target).displayName, system.GetSlot(target).level,
                        system.IsEquipped(target) ? "(장착)" : "(벤치)")
                    : string.Format("스킬 XP {0}  ->  갈 곳 없음 (전부 상한)", system.SkillXp),
                    GUILayout.Width(330f));

                if (GUILayout.Button("XP +240", GUILayout.Width(72f)))
                {
                    int gained = system.GrantXp(
                        Onikiri.Progression.SkillGachaCurve.XpFor(
                            Onikiri.Progression.SkillGachaCurve.Outcome.XpSurge));
                    Debug.Log("[Onikiri] 스킬 XP 240을 넣었다 - 레벨 +" + gained + ". "
                              + "상한(Lv." + Onikiri.Progression.SkillCurve.MaxLevel
                              + ")을 넘지 않는다.");
                }

                if (GUILayout.Button("개안", GUILayout.Width(56f)))
                {
                    int index = system.AwakenEquipped();
                    Debug.Log(index >= 0
                        ? "[Onikiri] " + system.GetSlot(index).displayName + "을 상한까지 밀었다."
                        : "[Onikiri] 개안할 오의가 없다 - 장착이 전부 상한이다. "
                          + "게임에서는 이때 사다리를 미끄러져 해금 -> XP가 된다.");
                }
            }

            // 가챠 몫 둘의 보유. 배너의 재고가 이 두 줄에서 나온다
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("가챠 몫", GUILayout.Width(56f));

                foreach (var id in Onikiri.Progression.SkillGachaCurve.StandardUnlockOrder)
                {
                    int index = system.IndexOf(id);
                    if (index < 0) continue;

                    var slot = system.GetSlot(index);
                    bool owned = system.IsUnlocked(index);

                    using (new EditorGUI.DisabledScope(owned))
                        if (GUILayout.Button(
                                slot.displayName + (owned ? " ✓" : " 해금"), GUILayout.Width(90f)))
                            system.GrantGachaSkill(index);
                }

                EditorGUILayout.LabelField(
                    "재고 " + (system.HasStock ? "있음" : "없음 - 배너가 닫힌다"),
                    GUILayout.Width(180f));
            }
        }

        /**
         * @brief 리드 상한. **뽑기가 지금 무엇을 팔 수 있는가의 답이다.**
         *
         * 자루마다 "정수를 받을 수 있는가"와 그 이유를 적는다. 화면에서는
         * 정수가 파편으로 바뀐 것만 보이고(GachaResultPopup) 왜 바뀌었는지는
         * 안 보이므로, 그 답이 있는 유일한 자리다.
         */
        private void DrawEssenceCapTools(Onikiri.Progression.GachaSystem gacha)
        {
            var yodo = Onikiri.Progression.YodoSystem.Instance;
            if (yodo == null)
                yodo = Object.FindFirstObjectByType<Onikiri.Progression.YodoSystem>(
                    FindObjectsInactive.Include);
            if (yodo == null) return;

            var stage = Object.FindFirstObjectByType<Onikiri.Progression.StageProgress>(
                FindObjectsInactive.Include);
            int frontier = stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1;

            EditorGUILayout.LabelField(string.Format(
                "혼 정수 리드 상한 (최전선 {0} 기준)", frontier));

            for (int i = 0; i < yodo.BladeCount; i++)
            {
                var blade = yodo.GetBlade(i);
                if (blade == null) continue;

                int dropped = Onikiri.Progression.GachaCurve.SoulsDroppedThrough(i, frontier);
                int cap = Onikiri.Progression.GachaCurve.EssenceSoulCap(i, frontier);
                bool accepts = Onikiri.Progression.GachaCurve.AcceptsEssence(
                    i, frontier, blade.tier, blade.souls);

                EditorGUILayout.LabelField(string.Format(
                    "    {0}  일정 {1}  보유 {2}(티어 {3} + 혼 {4})  상한 {5}  →  {6}",
                    blade.soulName, dropped, blade.tier + blade.souls, blade.tier, blade.souls,
                    cap, accepts ? "받는다" : (blade.tier < 1 ? "미봉인" : "상한")));
            }
        }

        /**
         * @brief 희귀도 사다리 (47단계). **혼격 상한과 전설 보유가 여기 있다.**
         *
         * 46단계의 리드 상한 표(위)와 같은 자리, 같은 이유다 - 이 스텝
         * 밸런스의 전부가 **혼격 상한**인데(YodoRarityCurve.CapAt) 화면
         * 어디에도 그 숫자가 안 뜬다. ★4를 뽑았는데 별이 안 늘고 혼 정수가
         * 된 것이 상한 때문인지 확률 때문인지를 가를 수 있는 곳이 여기다.
         *
         * 치트가 상한을 안 지키는 것은 DebugForge와 같은 규칙이다 - 지키면
         * "혼격 4를 보려면 열다섯 바퀴를 돌아라"가 되어 패널이 그 상태에
         * 못 닿는다.
         */
        private void DrawLadderTools()
        {
            var yodo = Onikiri.Progression.YodoSystem.Instance;
            if (yodo == null)
                yodo = Object.FindFirstObjectByType<Onikiri.Progression.YodoSystem>(
                    FindObjectsInactive.Include);
            if (yodo == null) return;

            var stage = Object.FindFirstObjectByType<Onikiri.Progression.StageProgress>(
                FindObjectsInactive.Include);
            int frontier = stage != null ? Mathf.Max(1, stage.MaxStageReached) : 1;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(string.Format(
                "혼격 상한 (최전선 {0} 기준 · 계약 구간에서는 {1})",
                frontier, Onikiri.Progression.YodoRarityCurve.BaseCap),
                EditorStyles.miniBoldLabel);

            for (int i = 0; i < yodo.BladeCount; i++)
            {
                var blade = yodo.GetBlade(i);
                if (blade == null) continue;

                int cap = Onikiri.Progression.YodoRarityCurve.CapAt(i, frontier);
                bool accepts = Onikiri.Progression.YodoRarityCurve.Accepts(
                    i, frontier, blade.tier, blade.rarity);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "    {0}  혼격 {1} / {2} {3}  →  {4}",
                        blade.bladeName, blade.rarity, cap,
                        Onikiri.Progression.YodoRarityCurve.Stars(blade.rarity),
                        accepts ? "받는다" : (blade.tier < 1 ? "미봉인" : "상한")),
                        GUILayout.Width(380f));

                    int index = i;
                    using (new EditorGUI.DisabledScope(
                        blade.rarity >= Onikiri.Progression.YodoRarityCurve.MaxRarity))
                        if (GUILayout.Button("혼격 +1", GUILayout.Width(72f)))
                            yodo.DebugGrantRarity(index);
                }
            }

            EditorGUILayout.LabelField(string.Format(
                "전설 妖刀  {0} / {1} 보유",
                yodo.LegendaryOwnedCount, yodo.LegendaryCount), EditorStyles.miniBoldLabel);

            for (int i = 0; i < yodo.LegendaryCount; i++)
            {
                var blade = yodo.GetLegendary(i);
                if (blade == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "    {0}  {1}  ·  공격력 ×{2:F3}  ·  {3} ×{4:F3}",
                        blade.bladeName,
                        blade.Owned
                            ? "돌파 " + blade.Breakthrough + " / "
                              + (Onikiri.Progression.LegendaryYodoCurve.MaxCopies - 1)
                            : "미보유",
                        blade.Multiplier,
                        Onikiri.Progression.LegendaryYodoCurve.SkillName(i),
                        Onikiri.Progression.LegendaryYodoCurve.AffinityAt(i, blade.copies)),
                        GUILayout.Width(380f));

                    int index = i;
                    using (new EditorGUI.DisabledScope(
                        blade.copies >= Onikiri.Progression.LegendaryYodoCurve.MaxCopies))
                        if (GUILayout.Button("사본 +1", GUILayout.Width(72f)))
                            yodo.DebugGrantLegendary(index);
                }
            }
        }

        /**
         * @brief 상성 · 영체 (45단계). **요도 절 안에 있는 것이 요점이다.**
         *
         * 절을 따로 세우지 않았다. 두 축에는 자기 재화도 자기 레벨도 없고
         * 손잡이가 **요도 티어 하나**뿐이라, 위 자루 목록의 "한 티어" 버튼이
         * 곧 이 두 축의 치트다. 절을 나누면 화면이 "만질 것이 세 곳"이라고
         * 거짓말한다.
         *
         * 그래서 여기 있는 것은 만드는 버튼이 아니라 **보는 것과 지금
         * 터뜨리는 것**이다:
         *
         *   표      오의 셋이 지금 받는 상성 배수 (상성이 실제로 걸렸는가)
         *   영체    다음 순번·쿨다운·이번 세션 소환 횟수 (버스트가 도는가)
         *   지금 소환  쿨다운을 무시하고 **실제 소환 경로**를 그대로 탄다
         *
         * 마지막 것이 이 절의 존재 이유다. 쿨다운이 32초라 연출을 눈으로
         * 확인하려면 32초를 기다려야 하고, 그 기다림은 확인보다 길다 -
         * 오의의 "지금 시전"과 같은 판단이다(SkillSystem.DebugCastNow).
         */
        private void DrawYodoPowerTools(Onikiri.Progression.YodoSystem yodo)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("상성 · 영체", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                var text = new System.Text.StringBuilder("상성  ");
                for (int i = 0; i < Onikiri.Progression.SkillCatalog.Count; i++)
                {
                    if (i > 0) text.Append("  ·  ");
                    text.AppendFormat("{0} ×{1:F3}",
                        Onikiri.Progression.SkillCatalog.Skills[i].DisplayName,
                        yodo.AffinityForSkill(i));
                }
                EditorGUILayout.LabelField(text.ToString(), GUILayout.Width(330f));

                // 오의가 실제로 그 배수를 내고 있는지는 SkillSystem에서 읽는다.
                // 요도가 계산한 값과 오의가 내는 값이 갈리면 상성이 화면에만
                // 있고 데미지에는 없는 상태다 - 그 둘을 나란히 적는다
                var skills = Onikiri.Progression.SkillSystem.Instance;
                if (skills != null)
                {
                    var actual = new System.Text.StringBuilder("오의 배율  ");
                    for (int i = 0; i < skills.SlotCount; i++)
                    {
                        if (i > 0) actual.Append("  ·  ");
                        actual.AppendFormat("×{0:F2}", skills.MultiplierOf(i));
                    }
                    EditorGUILayout.LabelField(actual.ToString(), GUILayout.Width(240f));
                }
            }

            var summon = Object.FindFirstObjectByType<Onikiri.Battle.SpiritSummon>(
                FindObjectsInactive.Include);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (summon == null)
                {
                    EditorGUILayout.LabelField(
                        "씬에 SpiritSummon이 없습니다. Build Forge Panel 을 실행하세요.",
                        GUILayout.Width(400f));
                    return;
                }

                int next = yodo.SpiritBladeForTurn(summon.SummonCount);
                string who = next >= 0
                    ? Onikiri.Progression.YodoCatalog.Blades[next].SpiritName
                    : "없음 (봉인한 요도 0)";

                // 직전 타격이 몇을 벴는지가 45c의 확인 지점이다 - 보스전 1,
                // 파밍 여럿이면 "보스 밴드 불변"이 화면에서 확인된다
                EditorGUILayout.LabelField(string.Format(
                    "다음 {0}  ·  쿨 {1:P0}  ·  소환 {2}회  ·  초당환산 {3:F3}  ·  직전 쓸기 {4}마리",
                    who, summon.CooldownFraction, summon.SummonCount, yodo.SpiritRate,
                    summon.LastSweepHits),
                    GUILayout.Width(420f));

                using (new EditorGUI.DisabledScope(!Application.isPlaying || next < 0))
                {
                    if (GUILayout.Button("지금 소환", GUILayout.Width(84f)))
                        if (!summon.DebugSummonNow())
                            Debug.Log("[Onikiri] 영체가 나오지 않았다 - 봉인한 요도가 없거나 "
                                      + "그 대요괴의 아트가 로스터에 없다.");

                    if (GUILayout.Button("쿨다운 채우기", GUILayout.Width(104f)))
                        summon.DebugFillCooldown();
                }
            }

            DrawSpiritSignatureTools(yodo, summon);
        }

        /**
         * @brief 영체 연출 지목 소환 (45b). **로테이션을 건너뛴다.**
         *
         * 45b에 영체 연출이 자루별로 갈리면서 필요해졌다 - 흑야만 강림
         * 번쩍·발도 참격·마지막 히트스톱을 전부 받는 쇼피스이고, 나머지 셋은
         * 그 아래로 벌어진다(YodoPanelBuilder.WriteSignatures).
         *
         * 위의 "지금 소환"으로는 그것을 볼 수가 없다. 넷이 돌아가므로 흑야
         * 차례까지 최악 네 번을 눌러야 하고 각 영체가 5초를 쓰므로, 확인 한
         * 번에 20초가 든다 - 연출을 조율하는 동안 그 20초가 조율보다 길다.
         *
         * 봉인하지 않은 자루는 눌리지 않는다. 여기서 억지로 띄우면 배율 0인
         * 영체가 나와 데미지가 안 들어가고, 화면은 "연출은 나는데 안 맞는다"가
         * 된다 - 그 상태가 진짜 고장과 구분되지 않는다.
         */
        private void DrawSpiritSignatureTools(Onikiri.Progression.YodoSystem yodo,
                                              Onikiri.Battle.SpiritSummon summon)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("영체 지목", GUILayout.Width(64f));

                for (int i = 0; i < Onikiri.Progression.YodoCatalog.Count; i++)
                {
                    var blade = Onikiri.Progression.YodoCatalog.Blades[i];
                    bool sealed_ = yodo.SpiritMultiplierOf(i) > 0d;

                    // 흑야가 쇼피스라 이름 옆에 별을 단다. 넷을 나란히 두면
                    // 어느 것이 정점인지가 화면에서 사라진다
                    string label = blade.BladeName
                                 + (blade.Id == Onikiri.Progression.YodoCatalog.DarkSamuraiId
                                    ? " ★" : string.Empty);

                    using (new EditorGUI.DisabledScope(!Application.isPlaying || !sealed_))
                    {
                        if (GUILayout.Button(label, GUILayout.Width(84f)))
                            if (!summon.DebugSummonBlade(i))
                                Debug.Log("[Onikiri] " + blade.SpiritName + "이 나오지 않았다 - "
                                          + "이미 소환 중이거나 그 대요괴의 아트가 로스터에 없다.");
                    }
                }
            }
        }

        /**
         * @brief 전직 (사무라이 진화).
         *
         * `진화`는 실제 구매 경로(보석 + 골드)를 그대로 탄다. `한 칸 올리기`는
         * 재화를 무시한다 - 여섯 티어를 훑는 데 보석 삼천 개를 먼저 만들어야
         * 하면 그 준비가 확인보다 길다. **올릴 때는 진화 연출도 튄다** - 이
         * 치트의 절반은 연출을 눈으로 확인하는 용도다(EvolutionSystem.DebugSetTier).
         *
         * `티어 초기화`는 재화를 돌려주지 않는다. 환불은 초기화가 아니라 별개의
         * 치트라는 규칙 그대로다.
         */
        private void DrawEvolutionTools()
        {
            EditorGUILayout.LabelField("전직 (사무라이 진화)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 실행 전에는 Instance가 비어 있으므로 씬에서 직접 찾는다.
                // 그래야 에디트 모드에서도 절이 "없습니다"가 아니라 상태로 뜬다 -
                // 잠긴 것과 안 세워진 것이 같은 문구로 보이면 안 된다
                var evolution = Onikiri.Progression.EvolutionSystem.Instance;
                if (evolution == null)
                    evolution = Object.FindFirstObjectByType<Onikiri.Progression.EvolutionSystem>(
                        FindObjectsInactive.Include);

                if (evolution == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 EvolutionSystem이 없습니다. Onikiri/Build Evolution Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "{0}  ·  {1}티어 {2}  공격 ×{3:F2} 체력 ×{4:F2}",
                        evolution.IsUnlocked
                            ? "개방"
                            : "잠김 (Lv." + Onikiri.Progression.EvolutionCurve.UnlockLevel + " 필요)",
                        evolution.Tier, evolution.TierName,
                        evolution.AttackMultiplier, evolution.HealthMultiplier),
                        GUILayout.Width(430f));

                    if (GUILayout.Button("티어 초기화", GUILayout.Width(96f)))
                    {
                        evolution.DebugSetTier(0);
                        Debug.Log("[Onikiri] 전직 티어를 0(로닌)으로 되돌렸다. 보석도 골드도 "
                                  + "돌려주지 않는다 - 환불은 초기화가 아니라 별개의 치트다.");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!evolution.CanEvolve))
                    {
                        string next = string.Format("진화 → {0} (보석 {1} + 골드 {2})",
                            evolution.NextTierName, evolution.GemCostNow,
                            Onikiri.Core.NumberFormatter.Format(evolution.GoldCostNow));

                        if (GUILayout.Button(next, GUILayout.Width(340f)))
                            evolution.TryEvolve();
                    }

                    // 재화를 무시하고 한 칸. 연출까지 함께 확인하는 경로다
                    using (new EditorGUI.DisabledScope(evolution.IsMaxTier))
                        if (GUILayout.Button("한 칸 올리기", GUILayout.Width(96f)))
                            evolution.DebugSetTier(evolution.Tier + 1);
                }
            }
        }

        /**
         * @brief 동료.
         *
         * `해금`·`레벨업`은 실제 구매 경로(보석/골드)를 그대로 탄다. `해금(무료)`는
         * 재화를 무시한다 - **등장 연출도 튄다**(PetSystem.DebugUnlock). 이 치트의
         * 절반은 동료가 화면 왼쪽에서 달려 들어오는 장면을 눈으로 확인하는 용도다.
         * 보유 동료는 전원 출전이라 출전 버튼이 없다.
         *
         * `동료 초기화`는 재화를 돌려주지 않는다. 환불은 초기화가 아니라 별개의
         * 치트라는 규칙 그대로다.
         */
        private void DrawPetTools()
        {
            EditorGUILayout.LabelField("동료", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var pets = Onikiri.Progression.PetSystem.Instance;
                if (pets == null)
                    pets = Object.FindFirstObjectByType<Onikiri.Progression.PetSystem>(
                        FindObjectsInactive.Include);

                if (pets == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 PetSystem이 없습니다. Onikiri/Build Pet Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format(
                        "{0}  ·  출전 {1}마리  합산 DPS +{2:P0}",
                        pets.IsUnlocked
                            ? "개방"
                            : "잠김 (st" + Onikiri.Progression.PetCurve.UnlockStage + " 필요)",
                        CountUnlocked(pets), pets.TotalBonus),
                        GUILayout.Width(300f));

                    if (GUILayout.Button("동료 초기화", GUILayout.Width(96f)))
                    {
                        pets.DebugResetPets();
                        Debug.Log("[Onikiri] 동료를 전부 잠금·Lv.1로 되돌렸다. 보석도 골드도 "
                                  + "돌려주지 않는다 - 환불은 초기화가 아니라 별개의 치트다.");
                    }
                }

                for (int i = 0; i < pets.PetCount; i++)
                {
                    var pet = pets.GetPet(i);
                    if (pet == null) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(string.Format(
                            "{0}  {1}  Lv.{2}  +{3:P0}",
                            pet.petName,
                            pet.unlocked ? "출전 중" : "잠김",
                            pet.level, pets.BonusOf(i)), GUILayout.Width(300f));

                        int index = i;

                        if (!pet.unlocked)
                        {
                            using (new EditorGUI.DisabledScope(!pets.CanUnlock(index)))
                                if (GUILayout.Button("해금 " + pet.unlockGems + "보석",
                                                     GUILayout.Width(110f)))
                                    pets.TryUnlock(index);

                            // 재화 무시. 등장 연출까지 함께 확인하는 경로다
                            if (GUILayout.Button("해금(무료)", GUILayout.Width(90f)))
                                pets.DebugUnlock(index);
                        }
                        else
                        {
                            using (new EditorGUI.DisabledScope(!pets.CanLevelUp(index)))
                                if (GUILayout.Button("레벨업", GUILayout.Width(70f)))
                                    pets.TryLevelUp(index);

                            // 재화를 무시하고 다섯 칸. 곡선을 훑는 경로다
                            using (new EditorGUI.DisabledScope(pets.IsMaxed(index)))
                                if (GUILayout.Button("Lv+5", GUILayout.Width(60f)))
                                    pets.DebugSetLevel(index, pet.level + 5);
                        }
                    }
                }
            }
        }

        private static int CountUnlocked(Onikiri.Progression.PetSystem pets)
        {
            int count = 0;
            for (int i = 0; i < pets.PetCount; i++)
            {
                var pet = pets.GetPet(i);
                if (pet != null && pet.unlocked) count++;
            }
            return count;
        }

        /**
         * @brief 성장 축을 되돌린다. **곡선을 다시 밟기 위한 것이다.**
         *
         * ## 왜 "세이브 삭제" 옆에 또 두는가
         *
         * 그 버튼은 전부를 지운다 - 스테이지·레벨·골드까지. 곡선 하나를 다시
         * 보려면 그 전부를 다시 만들어야 하고, 더 나쁜 것은 **되돌린 순간 그
         * 곡선을 볼 수 없게 된다**는 점이다:
         *
         *   스테이지 1  ->  골드 획득 축이 잠긴다 (6스테이지 해금)
         *   레벨 1     ->  오의 셋이 전부 잠긴다 (Lv.10/15/20)
         *   골드 0     ->  아무것도 살 수 없다
         *
         * 여기 있는 것들은 **축 하나씩만** 되돌린다. 스테이지 33·레벨 74를 그대로
         * 둔 채 "Lv.1부터 다시 사는 과정"을 몇 번이고 볼 수 있다.
         *
         * ## 환불 규칙이 축마다 다르다
         *
         *   강화·오의   골드를 돌려주지 않는다. 파밍으로 다시 벌 수 있고, 골드는
         *              위쪽에 이미 자기 버튼이 있다
         *   스탯 포인트  전부 돌려준다. **레벨에서만 나오는 재화**라, 회수 없이
         *              0으로 만들면 레벨 74가 준 포인트가 영영 사라진다
         */
        private void DrawResetTools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("초기화", GUILayout.Width(64f));

                using (new EditorGUI.DisabledScope(upgrades == null))
                    if (GUILayout.Button("강화 축"))
                    {
                        upgrades.DebugResetLevels();
                        Debug.Log("[Onikiri] 강화 " + upgrades.TrackCount
                                  + "축을 Lv.1로 되돌렸다. 골드는 환불하지 않는다.");
                    }

                using (new EditorGUI.DisabledScope(character == null))
                    if (GUILayout.Button("스탯 포인트"))
                    {
                        character.DebugRefundPoints();
                        Debug.Log("[Onikiri] 스탯 포인트를 전부 회수했다 - 남은 포인트 "
                                  + character.UnspentPoints + "개.");
                    }

                using (new EditorGUI.DisabledScope(skills == null))
                    if (GUILayout.Button("오의"))
                    {
                        skills.DebugResetLevels();
                        Debug.Log("[Onikiri] 오의를 새 게임 상태로 되돌렸다.");
                    }

                if (GUILayout.Button("전부"))
                {
                    if (upgrades != null) upgrades.DebugResetLevels();
                    if (character != null) character.DebugRefundPoints();
                    if (skills != null) skills.DebugResetLevels();

                    // 스테이지·레벨·골드는 건드리지 않는다. 그것까지 지우는 버튼은
                    // 아래 "세이브 삭제"이고, 둘이 같은 일을 하면 하나가 필요 없다
                    Debug.Log("[Onikiri] 성장 축을 전부 되돌렸다 (강화 / 스탯 포인트 / 오의). "
                              + "스테이지·레벨·골드는 그대로다.");
                }
            }
        }

        /**
         * @brief 캐릭터 레벨.
         *
         * 오의 해금(Lv.10/15/20)과 전직 자리표시(Lv.30)가 전부 이 값에 걸려 있는데,
         * 그 값을 올릴 방법이 이 창에 없어서 확인하려면 실제로 몇 스테이지를
         * 돌려야 했다.
         *
         * 레벨을 직접 대입하지 않고 **경험치를 부어 레벨업 경로를 그대로 태운다.**
         * 스탯 포인트 지급과 증폭 재적용이 실제 코드에서 나야, 이 버튼으로 만든
         * 상태가 진짜 플레이 상태와 같다고 말할 수 있다 - 보스 실패를 상태 조작이
         * 아니라 시계 소진으로 재현한 것과 같은 규칙이다.
         */
        private void DrawLevelTools()
        {
            if (character == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("레벨", GUILayout.Width(64f));
                EditorGUILayout.LabelField(
                    string.Format("Lv.{0}  대기 {1}", character.Level, character.PendingLevelUps),
                    GUILayout.Width(110f));

                if (GUILayout.Button("EXP +1렙")) GrantLevels(1);
                if (GUILayout.Button("+5렙")) GrantLevels(5);
                if (GUILayout.Button("레벨업 청구")) character.ClaimLevelUps();
            }

            // 경험치 스트립(2a 후속) 확인용. 위 버튼들은 청구까지 해버려서
            // 스트립의 두 상태 - 절반 채움(옥색이 반쯤 물든 경계선)과 대기
            // (레벨업 버튼 등장 + 스트립 맥동) - 를 화면에 세울 방법이 없었다.
            // 여기는 붓기만 하고 청구하지 않는다.
            // 개선안 v2: 레벨업 버튼은 이제 **캐릭터 패널의 레벨 헤더**
            // (스트립 아래 "Lv · EXP" 줄의 오른쪽 끝)에 뜬다. 미청구 상태에서
            // 함께 확인할 것 셋: 헤더의 버튼 등장, 상단 초상화와 하단 캐릭터
            // 탭의 빨간 점(LevelUpNoticeBadge), 초상화 탭 -> 캐릭터 패널이
            // 열리는 홈 동작. 청구하면 셋 다 꺼져야 한다
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("스트립", GUILayout.Width(64f));
                EditorGUILayout.LabelField(
                    string.Format("채움 {0:P0}", character.ExpFraction),
                    GUILayout.Width(110f));

                if (GUILayout.Button("+0.5렙 (채움만)"))
                    character.AddExp(character.ExpRequired * BigDouble.FromDouble(0.5));
                if (GUILayout.Button("+1렙 미청구 (맥동)"))
                    character.AddExp(character.ExpRequired);

                // 알림 점의 지금 상태. 화면의 점과 이 값이 어긋나면 배지가
                // CharacterLevel.Changed를 놓치고 있다는 뜻이다
                EditorGUILayout.LabelField(
                    character.PendingLevelUps > 0 ? "알림 점 ON" : "알림 점 OFF",
                    EditorStyles.miniLabel, GUILayout.Width(90f));
            }
        }

        /**
         * @brief 경험치를 n레벨분 붓고 청구까지 한다.
         *
         * 필요량을 레벨마다 다시 물어야 한다. ExpCurve가 레벨에 따라 자라므로
         * 현재 레벨의 필요량에 n을 곱하면 뒤로 갈수록 모자란다.
         */
        private void GrantLevels(int count)
        {
            for (int n = 0; n < count; n++)
            {
                character.AddExp(character.ExpRequired);
                character.ClaimLevelUps();
            }
        }

        /**
         * @brief 발도 오의.
         *
         * 이 구역이 생긴 이유는 "자동 시전을 켰는데 나가는지 모르겠다"였다. 화면에
         * 쿨다운 표시가 없고 전용 모션도 없어서, 오의가 도는지 확인할 자리가
         * 어디에도 없었다. 이제 장착 슬롯 칩에 쿨타임 오버레이가 붙었다
         * (SkillCooldownOverlay - 마스크 + 남은 초). 여기의 진행률과 화면의
         * 숫자가 어긋나면 오버레이가 다른 시계를 보고 있다는 뜻이다 - 둘 다
         * 같은 SkillSystem 값을 읽으므로 절대 어긋나면 안 된다.
         *
         * 세 가지를 나눠 보여준다 - 셋이 서로 다른 실패를 가리키기 때문이다.
         *
         *   쿨다운 진행  타이머가 도는가        (0에 붙어 있으면 사거리가 계속 비었다)
         *   시전 횟수    실제로 나갔는가        (타이머는 도는데 0이면 CastSkill이 거절 중)
         *   초당 환산    DPS에 얼마나 들어가는가
         *
         * ## 49단계에 넷째가 붙었다 - **장착**
         *
         * 오의가 여덟이 되고 자리가 넷이 되면서 새로운 실패가 하나 생겼다:
         * **"쿨다운이 아예 안 도는" 오의.** 안 끼운 오의는 타이머가 멈춰 있는
         * 것이 정상인데, 화면에서는 "쿨다운 표시가 없는 오의"와 구분되지 않는다.
         *
         * 그래서 줄마다 자리 번호를 적고, 자리를 여기서 직접 바꿀 수 있게 한다.
         * 최전선 게이트(st51)는 위쪽 스테이지 절에서 넘길 수 있으므로 여기에
         * 다시 두지 않는다 - 같은 치트를 두 곳에 두면 어느 쪽이 실제 경로인지
         * 흐려진다.
         *
         * "지금 시전"은 **실제 시전 경로를 그대로 태운다.** 상태를 직접 바꾸면
         * 이 버튼으로 본 화면이 실제 플레이의 화면과 다르다는 의심이 남는다.
         */
        private void DrawSkillTools()
        {
            EditorGUILayout.LabelField("발도 오의", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (skills == null)
                {
                    EditorGUILayout.HelpBox(
                        "씬에 SkillSystem이 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.Format("자동 시전 {0}   환산 {1:F2}회/초   자리 {2}/{3}",
                            skills.AutoCast ? "ON" : "OFF", skills.CastRate,
                            skills.SlotCapacity, Onikiri.Progression.SkillCurve.MaxSlots),
                        GUILayout.Width(290f));

                    if (GUILayout.Button(skills.AutoCast ? "끄기" : "켜기", GUILayout.Width(60f)))
                        skills.AutoCast = !skills.AutoCast;

                    // 셋을 한꺼번에 터뜨린다. 세 색이 겹치는 화면이 실제로 어떻게
                    // 보이는지는 그 조합을 만들어봐야만 알 수 있다
                    if (GUILayout.Button("쿨다운 채우기")) skills.DebugFillCooldowns();

                    // 오의 축만 새 게임 상태로. "세이브 삭제"와 다른 이유는
                    // SkillSystem.DebugResetLevels 주석 참고 - 그쪽은 캐릭터 레벨까지
                    // 1로 돌려서 오의가 잠겨 살 수조차 없게 된다
                    if (GUILayout.Button("초기화", GUILayout.Width(56f)))
                    {
                        skills.DebugResetLevels();
                        Debug.Log("[Onikiri] 오의를 새 게임 상태로 되돌렸다 - "
                                  + "레벨 1 / 쿨다운 0 / 시전 수 0 / 자동 시전 ON. "
                                  + "골드는 환불하지 않는다.");
                    }
                }

                // 장착 자리. 실제 조작 경로(SkillSlotChip / SkillButton)와 **같은
                // 함수**를 지난다 - 여기서 본 결과가 화면의 결과와 같아야 한다
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("장착", GUILayout.Width(34f));

                    for (int slotIndex = 0; slotIndex < Onikiri.Progression.SkillCurve.MaxSlots; slotIndex++)
                    {
                        int chip = slotIndex;
                        bool locked = skills.IsSlotLocked(chip);
                        int equipped = skills.EquippedAt(chip);

                        string label = locked
                            ? "잠김"
                            : equipped >= 0 ? skills.GetSlot(equipped).displayName : "비어 있음";

                        using (new EditorGUI.DisabledScope(locked || equipped < 0))
                        {
                            // 누르면 뺀다. 칩과 같은 조작이다
                            if (GUILayout.Button(label, GUILayout.Width(84f)))
                                skills.Equip(chip, -1);
                        }
                    }

                    if (GUILayout.Button("기본 구성", GUILayout.Width(72f)))
                    {
                        for (int slotIndex = 0; slotIndex < Onikiri.Progression.SkillCurve.MaxSlots; slotIndex++)
                            skills.Equip(slotIndex, -1);
                        skills.FillEmptySlots();
                        Debug.Log("[Onikiri] 장착을 기준 구성으로 되돌렸다 - "
                                  + "상한 기여 내림차순, 같으면 표 순서.");
                    }
                }

                for (int i = 0; i < skills.SlotCount; i++)
                {
                    var slot = skills.GetSlot(i);
                    if (slot == null) continue;

                    bool unlocked = skills.IsUnlocked(i);
                    int index = i;
                    int seat = skills.SlotOf(i);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 잠긴 줄의 조건은 게이트 종류에 따라 다른 말을 한다.
                        // 신규 셋은 최전선(49b), 가챠 몫 둘은 뽑기(50단계) -
                        // 뒤쪽에 "st41 필요"를 적으면 거짓말이 된다. 그 값은
                        // 게이트가 아니라 골드 비용의 기준점이다
                        string gate = slot.gachaGated ? "뽑기"
                            : slot.unlockLevel > 0 ? "Lv." + slot.unlockLevel
                            : "st" + slot.unlockStage;

                        EditorGUILayout.LabelField(
                            unlocked
                                ? string.Format("{0}{1} Lv.{2}",
                                    seat >= 0 ? "[" + (seat + 1) + "] " : "     ",
                                    slot.displayName, slot.level)
                                : string.Format("     {0} ({1} 필요)", slot.displayName, gate),
                            GUILayout.Width(160f));

                        // 쿨다운을 막대로 그린다. 숫자만 적으면 "도는지"를 두 번
                        // 읽어서 비교해야 하는데, 막대는 한 번 보면 안다
                        var bar = GUILayoutUtility.GetRect(60f, 16f, GUILayout.Width(60f));
                        EditorGUI.ProgressBar(bar, unlocked ? skills.CooldownFraction(i) : 0f,
                            unlocked ? skills.SecondsUntilCast(i).ToString("F1") : "-");

                        EditorGUILayout.LabelField(
                            string.Format("×{0:F2}  시전 {1}회", skills.MultiplierOf(i), skills.CastCountOf(i)),
                            GUILayout.Width(120f));

                        // 빈 자리에만 끼운다 - 화면의 장착 버튼과 같은 규칙이다
                        // (SkillButton.OnEquip). 자리가 다 차 있으면 안 눌린다
                        bool hasRoom = false;
                        for (int open = 0; open < skills.SlotCapacity; open++)
                            if (skills.EquippedAt(open) < 0) { hasRoom = true; break; }

                        using (new EditorGUI.DisabledScope(!unlocked || seat >= 0 || !hasRoom))
                        {
                            if (GUILayout.Button("장착", GUILayout.Width(40f)))
                            {
                                for (int open = 0; open < skills.SlotCapacity; open++)
                                {
                                    if (skills.EquippedAt(open) >= 0) continue;
                                    skills.Equip(open, index);
                                    break;
                                }
                            }
                        }

                        using (new EditorGUI.DisabledScope(!unlocked))
                        {
                            if (GUILayout.Button("지금 시전", GUILayout.Width(70f)) && !skills.DebugCastNow(index))
                                Debug.Log("[Onikiri] 시전이 거절됐다 - 사거리에 벨 것이 없다. "
                                          + "요괴가 들어온 뒤 다시 누르면 나간다.");

                            using (new EditorGUI.DisabledScope(skills.IsMaxed(index)))
                            {
                                if (GUILayout.Button("+1", GUILayout.Width(28f))) skills.TryPurchase(index);
                                if (GUILayout.Button("최대", GUILayout.Width(40f)))
                                    for (int n = 0; n < 1000 && skills.TryPurchase(index); n++) { }
                            }
                        }
                    }
                }

                // 화면이 하단 탭 뒤에 숨어 있어서, 여는 것만으로도 탭을 찾아
                // 눌러야 했다. 잠겨 있으면 탭이 안 눌리므로 이쪽이 유일한 경로이기도 하다
                var panel = GameObject.Find("UI Canvas");
                var screen = panel != null ? panel.transform.Find("SafeArea/SkillPanel") : null;
                using (new EditorGUI.DisabledScope(screen == null))
                {
                    bool open = screen != null && screen.gameObject.activeSelf;
                    if (GUILayout.Button(open ? "스킬 패널 닫기" : "스킬 패널 열기"))
                        screen.gameObject.SetActive(!open);
                }
            }
        }

        /**
         * @brief 보스전.
         *
         * 실제로 10마리를 잡고 30초를 기다리지 않고도 등장 연출·전투·실패 세 화면을
         * 각각 띄울 수 있어야 한다. 특히 실패는 그냥 기다려서는 재현하기 어렵다 -
         * 강화가 앞서 있으면 보스가 먼저 죽는다. 그래서 남은 시간을 직접 태운다.
         */
        private void DrawBossTools()
        {
            if (boss == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("보스", GUILayout.Width(64f));
                EditorGUILayout.LabelField(
                    boss.Current == BossFight.Phase.Fighting
                        ? string.Format("전투 중  {0:F1}초  체력 {1:P0}", boss.SecondsLeft, boss.BossHealthFraction)
                        : boss.Current.ToString(),
                    GUILayout.Width(190f));

                using (new EditorGUI.DisabledScope(!boss.CanChallenge))
                    if (GUILayout.Button("도전")) boss.Challenge();

                // 시계를 끝까지 밀어 실패 경로를 그대로 태운다. 실패 문구 계산도
                // 실제 코드가 돌아야 하므로 상태를 직접 바꾸지 않는다
                using (new EditorGUI.DisabledScope(boss.Current != BossFight.Phase.Fighting))
                    if (GUILayout.Button("시간 소진")) boss.DebugExpireTimer();

                /**
                 * @brief 지금 서 있는 보스를 그 자리에서 벤다.
                 *
                 * 도전 다음에 곧바로 누르면 보스가 **달려오는 중에** 죽는다.
                 * 진행이 통째로 막혔던 경로가 그것이다 - 사무라이의 사거리는
                 * 전선보다 앞까지 닿아서 강한 빌드의 첫 타격이 접근 구간에
                 * 떨어지는데, 그 처치가 버려져 스테이지가 안 올랐다
                 * (BossFight.CountsAsClear).
                 *
                 * 상태를 바꾸지 않고 실제 피해로 죽인다. 그래야 확인하려던
                 * 경로(Enemy.Killed -> 스포너 -> BossFight)가 그대로 돈다 -
                 * "시간 소진"이 상태를 직접 안 바꾸는 것과 같은 이유다.
                 */
                using (new EditorGUI.DisabledScope(boss.Boss == null || !boss.Boss.IsAlive))
                    if (GUILayout.Button("즉시 처치"))
                        boss.Boss.TakeDamage(boss.Boss.MaxHealth * Onikiri.Core.BigDouble.FromDouble(2d));
            }

            // 접근 구간에서는 보스의 위치를 함께 보여준다. 사무라이 사거리 안에
            // 들어왔는데 아직 Fighting이 아닌 그 구간이 진행 정지의 현장이었다
            if (boss.Current == BossFight.Phase.Approaching && boss.Boss != null)
                EditorGUILayout.LabelField(
                    string.Format("    달려오는 중  x = {0:F2}  (전선 도달 시 전투 시작)",
                                  boss.Boss.CurrentX),
                    EditorStyles.miniLabel);

            if (boss.Current == BossFight.Phase.Failed && !string.IsNullOrEmpty(boss.FailureMessage))
                EditorGUILayout.HelpBox(boss.FailureMessage, MessageType.Warning);
        }

        /**
         * @brief 폴리싱 배치 (UX 15항목).
         *
         * ## 왜 한 절이 필요한가
         *
         * 이 배치의 대부분은 **화면에만 있는 것**이라 테스트가 잡을 수 있는
         * 것이 거의 없다. 그렇다고 눈으로만 확인하려면 조건을 만드는 데
         * 시간이 걸린다 - 일괄 수령을 보려면 보상이 쌓여 있어야 하고,
         * 배수 구매를 보려면 골드가 있어야 하고, 팝업 넷은 각각 다른 곳에서
         * 열린다.
         *
         * 그 조건 만들기와 진입을 여기 모은다. 48단계의 요괴 애니 절이
         * "눈으로 볼 일을 30분 기다리지 않고 하는 것"이라 적은 것과 같은
         * 이유이고, 같은 방식이다.
         */
        private void DrawPolishTools()
        {
            EditorGUILayout.LabelField("폴리싱 (UX 15항목)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // ---- #9 배수 구매
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("#9 배수", GUILayout.Width(80f));
                    EditorGUILayout.LabelField(
                        Onikiri.UI.UpgradeBatchSelector.IsMax
                            ? "최대"
                            : "×" + Onikiri.UI.UpgradeBatchSelector.Current,
                        GUILayout.Width(60f));

                    var upgrades = Object.FindFirstObjectByType<UpgradeSystem>(FindObjectsInactive.Include);
                    using (new EditorGUI.DisabledScope(!Application.isPlaying || upgrades == null))
                    {
                        // 공격력 축(0)에 실제로 배수 구매를 걸어본다. 총액이
                        // 한 칸씩 산 것과 같아야 한다는 계약은 테스트가 재고
                        // (UpgradeBatchTests), 여기서는 화면이 따라오는지를 본다
                        if (GUILayout.Button("공격력 ×10"))
                            upgrades.TryPurchaseMany(0, 10);
                        if (GUILayout.Button("공격력 최대"))
                        {
                            var track = upgrades.GetTrack(0);
                            var wallet = PlayerWallet.Instance;
                            if (track != null)
                                upgrades.TryPurchaseMany(0, track.AffordableLevels(wallet, 0));
                        }
                    }
                }

                // ---- #5 일괄 수령
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("#5 수령", GUILayout.Width(80f));

                    var quests = QuestSystem.Instance;
                    EditorGUILayout.LabelField(
                        quests != null ? "받을 것 " + quests.TotalClaimable + "개" : "-",
                        GUILayout.Width(120f));

                    using (new EditorGUI.DisabledScope(!Application.isPlaying || quests == null))
                    {
                        // 쌓인 상태를 만든다. 반복 퀘스트는 카운터에서 티어가
                        // 유도되므로 카운터를 밀면 여러 개가 한꺼번에 열린다 -
                        // 백 개가 쌓인 화면이 이 버튼의 실제 대상이다
                        if (GUILayout.Button("보상 쌓기"))
                            for (int n = 0; n < 200; n++) quests.ReportMobKill();

                        if (GUILayout.Button("일괄 수령"))
                            Debug.Log("[Onikiri] 일괄 수령: " + quests.ClaimAll() + "개");
                    }
                }

                // ---- 팝업 넷 (#1 · #12 · #14)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("팝업", GUILayout.Width(80f));
                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("설정")) TogglePopup("SettingsPanel");
                        if (GUILayout.Button("랭킹")) TogglePopup("LeaderboardPanel");
                        if (GUILayout.Button("스킬 정보")) OpenSkillPopup();
                    }
                }

                // ---- #2 경험치 줄 / 가이드 카드 (완료 토스트는 제거됐다 -
                //      완료 신호는 카드와 퀘스트 아이콘 배지 둘로 충분하다)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("상시 HUD", GUILayout.Width(80f));
                    EditorGUILayout.LabelField(
                        "경험치 줄 " + LayerStateOf("ExpStrip")
                        + " · 가이드 카드 " + LayerStateOf(BattleContentBuilder.GuideCardName),
                        EditorStyles.miniLabel);
                }
            }
        }

        /** SafeArea 아래의 판 하나를 켜고 끈다 */
        private static void TogglePopup(string name)
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var found = safeArea != null ? safeArea.Find(name) : null;
            if (found == null) { Debug.LogWarning("[Onikiri] " + name + " 없음"); return; }
            found.gameObject.SetActive(!found.gameObject.activeSelf);
        }

        /** 첫 오의로 정보 팝업을 연다 (#14) */
        private static void OpenSkillPopup()
        {
            var popup = Object.FindFirstObjectByType<Onikiri.UI.SkillInfoPopup>(
                FindObjectsInactive.Include);
            if (popup == null) { Debug.LogWarning("[Onikiri] SkillInfoPopup 없음"); return; }
            popup.Open(0);
        }

        /**
         * @brief 그 HUD가 지금 어느 층에 서 있는가 (#2).
         *
         * 층위가 틀리면 증상이 "가끔 안 보인다"라 눈으로 재현하기 어렵다 -
         * 어느 화면을 열었느냐에 따라 갈리기 때문이다. 값으로 읽어둔다.
         */
        private static string LayerStateOf(string name)
        {
            var safeArea = MainSceneBuilder.FindBand(MainSceneBuilder.SafeAreaName);
            var found = safeArea != null ? safeArea.Find(name) : null;
            if (found == null) return "없음";

            var canvas = found.GetComponent<Canvas>();
            return canvas != null && canvas.overrideSorting
                ? "층 " + canvas.sortingOrder
                : "층 없음(파묻힘)";
        }

        /**
         * @brief 요괴 애니 · 참격 (48단계).
         *
         * ## 왜 이 절이 필요한가
         *
         * 이 스텝이 한 일은 전부 **화면에만 보이는 것**이다. 밴드도 세이브도
         * 안 움직이므로 테스트가 잡아줄 수 있는 것이 거의 없고, "요괴가 걷는가",
         * "보스가 휘두르는가"는 결국 눈으로 봐야 한다. 그 눈으로 보는 일을
         * 화면 앞에서 30분 기다리지 않고 하는 것이 이 절이다.
         *
         * ## 표가 문서가 아니라 실물을 읽는다
         *
         * 어느 요괴에 어떤 클립이 붙었는지를 주석에서 베껴 적지 않고 생성된
         * `EnemyDefinition`에서 직접 읽는다. 빌더가 태그를 잘못 집으면 표에
         * 0으로 뜬다 - 베껴 적은 표는 그 경우에도 여전히 맞다고 우긴다.
         *
         * 에디트 모드에서도 보인다. 표는 씬이 아니라 애셋을 읽으므로 플레이를
         * 누를 이유가 없고, 참격 버튼만 플레이 중에 열린다.
         */
        private void DrawEnemyAnimationTools()
        {
            EditorGUILayout.LabelField("요괴 애니 · 참격", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawClipTable();
                EditorGUILayout.Space(4f);
                DrawVfxLibraryTools();
            }
        }

        /** 생성된 정의에 실제로 들어간 프레임 수. 빈 칸이 곧 "팩에 그 태그가 없다" */
        private void DrawClipTable()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("요괴", EditorStyles.miniBoldLabel, GUILayout.Width(104f));
                EditorGUILayout.LabelField("대기", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
                EditorGUILayout.LabelField("걷기", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
                EditorGUILayout.LabelField("공격", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
                EditorGUILayout.LabelField("피격", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
                EditorGUILayout.LabelField("사망", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
            }

            int walked = 0, attacked = 0, hurt = 0, total = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null) continue;

                total++;
                if (Length(definition.walkFrames) > 0) walked++;
                if (Length(definition.attackFrames) > 0) attacked++;
                if (Length(definition.hurtFrames) > 0) hurt++;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(definition.name.Replace("Enemy_", ""),
                                               GUILayout.Width(104f));
                    Count(Length(definition.idleFrames));
                    Count(Length(definition.walkFrames));
                    Count(Length(definition.attackFrames));
                    Count(Length(definition.hurtFrames));
                    Count(Length(definition.deathFrames));
                }
            }

            EditorGUILayout.LabelField(string.Format(
                "정의 {0}개 중 걷기 {1} · 공격 {2} · 피격 {3}",
                total, walked, attacked, hurt), EditorStyles.miniLabel);
        }

        private static int Length(Sprite[] frames) { return frames != null ? frames.Length : 0; }

        /** 0은 흐리게. 빈 칸이 눈에 안 띄면 표를 읽는 의미가 없다 */
        private static void Count(int value)
        {
            using (new EditorGUI.DisabledScope(value == 0))
                EditorGUILayout.LabelField(value.ToString(), GUILayout.Width(34f));
        }

        /**
         * @brief 뜯어낸 참격을 그 자리에서 한 번 터뜨린다.
         *
         * 보스가 휘두를 때까지 기다리면 보스전을 열고 2초를 기다려야 하고,
         * 크기나 높이를 한 번 고칠 때마다 그 왕복이 반복된다. 사무라이 자리에
         * 바로 띄우면 배율·각도·높이를 화면에서 바로 비교할 수 있다.
         *
         * **풀 증가 수를 함께 띄운다.** 참격은 풀에서 나오는데, 조용히 늘어나는
         * 풀은 프레임 히칭의 원인을 찾을 수 없게 만든다(ObjectPool 주석).
         */
        private void DrawVfxLibraryTools()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(YokaiVfxBaker.LibraryPath);
            if (library == null)
            {
                EditorGUILayout.HelpBox(
                    "참격 라이브러리가 없습니다. Onikiri/Art/Harvest Yokai VFX 를 실행하세요.",
                    MessageType.Warning);
                return;
            }

            for (int i = 0; i < library.Count; i++)
            {
                var clip = library.At(i);
                if (clip == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(clip.id, GUILayout.Width(104f));
                    EditorGUILayout.LabelField(string.Format(
                        "{0}장 {1:F0}fps  {2:F2}초  x{3:F0}",
                        Length(clip.frames), clip.frameRate, clip.Seconds, clip.scale),
                        GUILayout.Width(150f));

                    // 플레이 중에만. 풀은 Awake에서 만들어지므로 에디트 모드에서는
                    // 터뜨릴 것이 없다
                    using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying
                                                       || attackVfx == null || combat == null))
                    {
                        if (GUILayout.Button("터뜨리기", GUILayout.Width(70f)))
                        {
                            // 사무라이가 바라보는 쪽(오른쪽)에 띄운다. 보스는
                            // 반대편에서 왼쪽으로 뿜으므로 반전만 다르다
                            attackVfx.Play(clip.id, combat.transform.position, false);
                        }
                    }
                }
            }

            if (attackVfx == null)
            {
                EditorGUILayout.HelpBox(
                    "씬에 VfxBurst가 없습니다. Onikiri/Scene/Build Combat Content 를 실행하세요.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(string.Format(
                "재생 중 {0}장   풀 증가 {1}회",
                attackVfx.ActiveCount, attackVfx.PoolGrowthCount), EditorStyles.miniLabel);
        }

        /**
         * @brief 지역별 잡몹 풀 (36단계).
         *
         * 지역 점프는 실제 코드 경로를 지난다 - SetProgress가 Changed를 쏘고
         * RegionMobSwitcher가 풀을 갈아끼우는, 플레이에서 지역을 넘을 때와 같은
         * 길이다. 필드 비우기는 보상 없는 ClearField라 골드가 새지 않는다.
         */
        private void DrawRegionMobTools()
        {
            if (stage == null || spawner == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("지역 잡몹", GUILayout.Width(64f));

                var roster = AssetDatabase.LoadAssetAtPath<BossRoster>(BossConfigBuilder.RosterPath);
                using (new EditorGUI.DisabledScope(roster == null || roster.regions == null))
                {
                    int firstStage = 1;
                    int count = roster != null && roster.regions != null ? roster.regions.Length : 0;
                    for (int i = 0; i < count; i++)
                    {
                        var region = roster.regions[i];
                        if (region == null) continue;

                        if (GUILayout.Button("지역 " + (i + 1)))
                        {
                            stage.SetProgress(firstStage, 0, 0);

                            // 옛 지역 몹이 화면에 남아 있으면 "지역별로 다른 몹"을
                            // 확인할 수 없다. 보스전 중에는 비우지 않는다 -
                            // ClearField가 보스까지 치워버린다
                            if (boss == null || boss.Current == BossFight.Phase.Farming)
                                spawner.ClearField();
                        }

                        firstStage += region.stageCount;
                    }
                }
            }

            // 지금 풀이 무엇인지 화면에 적는다. 스크린샷이 어느 지역인지는 배경으로
            // 알 수 있지만, 어떤 몹이 나와야 하는지는 여기서 읽는 것이 빠르다
            var applied = mobSwitcher != null ? mobSwitcher.Applied : null;
            var pool = spawner.Definitions;
            if (pool != null && pool.Count > 0)
            {
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] == null) continue;
                    if (names.Length > 0) names.Append(", ");
                    names.Append(pool[i].displayName).Append("(w").Append(pool[i].spawnWeight).Append(")");
                }

                EditorGUILayout.LabelField(" ", (applied != null ? applied.displayName + ": " : "") + names,
                    EditorStyles.miniLabel);
            }

            // 재선택 실경로(37단계). 위의 지역 버튼은 치트(SetProgress - 위로도
            // 점프한다)이고, 이쪽은 실제 게임이 쓰는 SelectStage를 지난다 -
            // st11 게이트와 최전선 클램프가 실제로 걸리는지 여기서 본다
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("재선택", GUILayout.Width(64f));
                EditorGUILayout.LabelField(
                    "최전선 " + stage.MaxStageReached
                    + (stage.IsReselectUnlocked ? "" : "  (st" + StageProgress.ReselectUnlockStage + "부터)"),
                    GUILayout.Width(150f));

                using (new EditorGUI.DisabledScope(!stage.IsReselectUnlocked))
                {
                    if (GUILayout.Button("-10")) ReselectTo(stage.Stage - 10);
                    if (GUILayout.Button("-1")) ReselectTo(stage.Stage - 1);
                    if (GUILayout.Button("최전선으로")) ReselectTo(stage.MaxStageReached);
                }
            }
        }

        /**
         * @brief 발도 개방 체인 (43단계).
         *
         * 치명타 확률 100% -> 심화 축 해금이 이 스텝의 축이다. 실제 도달은
         * st80 언저리라, 체인이 도는지 눈으로 보려면 벽(Lv.98~177, 총
         * ~3.6e20 골드)을 밀어줄 지갑이 필요하다. 실제 구매 경로(TryPurchase)를
         * 지나는 것이 요점이다 - 레벨을 직접 꽂으면 화면 갱신·해금 이벤트가
         * 실제와 다른 길로 돈다.
         */
        private void DrawMasteryTools()
        {
            if (upgrades == null) return;

            var crit = upgrades.GetTrack(Onikiri.Progression.UpgradeSystem.CritRateId);
            if (crit == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool open = crit.IsMaxed || crit.IsValueCapped;
                EditorGUILayout.LabelField("심화", GUILayout.Width(64f));
                EditorGUILayout.LabelField(
                    open ? "열림 (치명타 100%)" : "잠김 - 치명타 Lv." + crit.Level + "/177",
                    GUILayout.Width(170f));

                if (!open && GUILayout.Button("치명타 100%까지 (골드 지급)"))
                {
                    var wallet = PlayerWallet.Instance;
                    if (wallet != null) wallet.Add(BigDouble.FromDouble(1e21d));

                    int index = -1;
                    for (int i = 0; i < upgrades.TrackCount; i++)
                        if (upgrades.GetTrack(i) == crit) { index = i; break; }

                    if (index >= 0)
                        for (int n = 0; n < 200 && upgrades.TryPurchase(index); n++) { }
                }
            }
        }

        /**
         * @brief 무한 구간 점프 (42단계).
         *
         * st41+는 세계가 순환하는 무한 구간이다. 깊은 스테이지의 배경 순환·
         * 보스·여유는 여기로 점프해야 눈으로 확인할 수 있다 - 시뮬이 숫자를
         * 보증하고, 이 버튼이 화면을 보증한다. SetProgress 4인자는 최전선까지
         * 세팅하는 치트다(잠금 상태 절과 같은 도구).
         */
        private void DrawDeepZoneTools()
        {
            if (stage == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("무한 구간", GUILayout.Width(64f));

                // 52단계에 st500(거버넌스 끝)과 st550(f2p 벽 실측)이 늘었다 -
                // 계약 경계의 화면을 눈으로 볼 수 있어야 한다
                foreach (int target in new[] { 51, 100, 200, 500, 550 })
                {
                    if (GUILayout.Button("st" + target))
                    {
                        stage.SetProgress(target, 0, 0, target);
                        if (spawner != null && (boss == null || boss.Current == BossFight.Phase.Farming))
                            spawner.ClearField();
                    }
                }

                // 지금 어느 바퀴의 어느 지역인지. 표기 지역(무한 증가)과 세트
                // 지역(4개 순환)이 다른 값이라 둘 다 적는다
                int region = Onikiri.Progression.BossCurve.RegionOf(stage.Stage);
                int contentRegion = (region - 1) % 4 + 1;
                EditorGUILayout.LabelField(
                    "지역 " + region + " (세트 " + contentRegion + ")", EditorStyles.miniLabel);
            }

            // 도달층 기록 (52단계). 이 값이 곧 리더보드 점수가 된다 -
            // StageProgress.MaxStageReached 주석. 새니티 상한도 함께 적는다
            EditorGUILayout.LabelField(string.Format(
                "도달층(리더보드 예정) st{0} · 계약 st{1}~{2} · 새니티 캡 {3:N0}",
                stage.MaxStageReached,
                Onikiri.Progression.StageSimulation.ReachContractFrom,
                Onikiri.Progression.StageSimulation.ReachContractTo,
                Onikiri.Progression.StageProgress.ReachSanityCap),
                EditorStyles.miniLabel);
        }

        /**
         * @brief 잠긴 탭 미리보기 (41단계).
         *
         * 미리보기는 "잠긴 세이브"가 있어야 확인할 수 있는데, 개발 세이브는
         * 대부분 다 풀려 있다. 여기 버튼이 최전선·레벨을 게이트 바로 앞으로
         * 되돌린다 - 세이브를 지우고 거기까지 다시 가는 방법은 확인 비용이
         * 확인보다 크다.
         *
         * 되돌리기는 치트다(SetProgress 4인자 - 최전선을 **낮춘다**,
         * Restore - 레벨을 덮는다). 실제 게임에 최전선이 내려가는 경로는
         * 없고, 있어서도 안 된다. 포인트는 레벨에 맞춰 다시 계산되므로
         * 이미 쓴 포인트가 음수 빚으로 남지 않게 전부 환불부터 한다.
         */
        private void DrawLockPreviewTools()
        {
            if (stage == null || character == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("잠금 상태", GUILayout.Width(64f));

                if (GUILayout.Button("전부 잠금 (Lv.9 · st9)"))
                {
                    character.DebugRefundPoints();
                    character.Restore(9, 0, 0, 0);
                    stage.SetProgress(9, 0, 0, 9);
                    if (spawner != null && (boss == null || boss.Current == BossFight.Phase.Farming))
                        spawner.ClearField();
                }

                if (GUILayout.Button("동료만 잠금 (st30)"))
                {
                    stage.SetProgress(30, 0, 0, 30);
                    if (spawner != null && (boss == null || boss.Current == BossFight.Phase.Farming))
                        spawner.ClearField();
                }
            }

            EditorGUILayout.LabelField(" ",
                "잠긴 탭도 눌려서 화면이 열려야 하고, 액션 버튼만 죽어 있어야 한다",
                EditorStyles.miniLabel);
        }

        /** 실경로 재선택. RegionSelectPanel과 같은 가드(파밍 중) + 필드 정리 */
        private void ReselectTo(int target)
        {
            if (stage == null) return;
            if (boss != null && boss.Current != BossFight.Phase.Farming) return;

            int before = stage.Stage;
            stage.SelectStage(target);
            if (stage.Stage != before && spawner != null) spawner.ClearField();
        }

        /**
         * @brief 세이브와 방치 보상.
         *
         * 방치 보상은 실제로 몇 시간을 기다리지 않으면 확인할 방법이 없다. 저장된
         * 시각을 과거로 돌린 뒤 다시 불러오면, 앱을 껐다 켠 것과 정확히 같은 경로를
         * 탄다 - 보상 계산도 팝업도 실제 코드가 그대로 돈다.
         */
        private void DrawSaveTools()
        {
            EditorGUILayout.LabelField("세이브 / 방치 보상", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.SelectableLabel(
                    SaveSystem.Exists ? SaveSystem.Path : SaveSystem.Path + "  (아직 없음)",
                    GUILayout.Height(16f));

                using (new EditorGUI.DisabledScope(session == null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("지금 저장")) session.Save();
                        if (GUILayout.Button("다시 불러오기")) session.ReloadFromDisk();
                        if (GUILayout.Button("세이브 삭제")) session.DeleteSaveAndReload();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("방치", GUILayout.Width(40f));
                        offlineHours = EditorGUILayout.Slider(offlineHours, 0.1f, 12f);
                        if (GUILayout.Button("적용", GUILayout.Width(60f))) SimulateOffline(offlineHours);
                    }

                    EditorGUILayout.HelpBox(
                        "저장 -> 종료 시각을 " + offlineHours.ToString("F1") +
                        "시간 전으로 되돌림 -> 다시 불러오기. 상한은 8시간이라 그보다 " +
                        "크게 잡으면 '상한 도달' 표시를 확인할 수 있습니다.", MessageType.None);
                }
            }
        }

        private void SimulateOffline(float hours)
        {
            if (session == null) return;

            session.Save();

            var data = SaveSystem.Load();
            // 저장 직후의 시각에서 빼야 한다. 지금 시각에서 빼면 자동 저장이 한 번
            // 끼어들었을 때 그만큼이 사라진다
            data.lastQuitUtcTicks -= (long)(hours * System.TimeSpan.TicksPerHour);
            SaveSystem.Save(data);

            session.ReloadFromDisk();
        }

        /**
         * @brief Firebase 스파이크 - 도달층 write/read 왕복.
         *
         * 여기서 되는 것이 증명의 전부는 아니다. **에디터의 Firestore는 데스크톱
         * 네이티브이고 폰의 Firestore는 Play 서비스 위에서 돈다** - 여기서 초록불이
         * 떠도 기기에서 의존성 확인이 막히는 경우가 실제로 있다. 그래서 이 절은
         * "코드가 맞게 짜였는가"까지만 답하고, 진짜 답은 기기의 오버레이 버튼과
         * logcat에 있다 (CloudSpikeOverlay).
         *
         * 단계별 버튼을 따로 둔 이유는 실패 지점이 네 군데로 뚜렷하게 갈리기
         * 때문이다 - 의존성(Play 서비스), 로그인(콘솔의 익명 인증 설정), write
         * (보안 규칙), read(네트워크). 한 버튼만 있으면 어디서 멈췄는지 로그를
         * 거슬러 올라가야 한다.
         */
        private void DrawCloudTools()
        {
            EditorGUILayout.LabelField("클라우드 (Firebase 스파이크)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("상태", CloudScores.Status);
                Row("uid", string.IsNullOrEmpty(CloudScores.Uid) ? "(로그인 전)" : CloudScores.Uid);
                Row("도달층", stage != null
                    ? string.Format("로컬 {0}   /   서버 {1}", stage.MaxStageReached,
                        CloudScores.LastReadStage >= 0
                            ? CloudScores.LastReadStage.ToString() : "(안 읽음)")
                    : "(StageProgress 없음)");
                Row("문서", CloudScores.Collection + "/"
                    + (string.IsNullOrEmpty(CloudScores.Uid) ? "{uid}" : CloudScores.Uid));

                using (new EditorGUI.DisabledScope(CloudScores.IsBusy))
                {
                    if (GUILayout.Button("Firebase: 초기화→로그인→write→read", GUILayout.Height(26f)))
                        CloudScores.RunSpike();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("초기화")) Forget(CloudScores.InitializeAsync());
                        if (GUILayout.Button("로그인")) Forget(CloudScores.SignInAnonymouslyAsync());
                        if (GUILayout.Button("write"))
                            Forget(CloudScores.SubmitReachAsync(CloudScores.CurrentReach()));
                        if (GUILayout.Button("read")) Forget(CloudScores.FetchReachAsync());
                    }

                    // 오프라인 안전은 이 게임에서 부가 기능이 아니다 - 방치형은
                    // 네트워크가 없는 곳에서도 계속 돌아야 하고, Firebase가 그것을
                    // 막으면 그 순간 곁다리가 본체를 죽인 것이다. 끄고 나서 위
                    // 버튼들을 눌러보는 것이 이 절의 마지막 검사다
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("오프라인", GUILayout.Width(64f));
                        if (GUILayout.Button("네트워크 끄기"))
                            Forget(CloudScores.SetNetworkEnabledAsync(false));
                        if (GUILayout.Button("네트워크 켜기"))
                            Forget(CloudScores.SetNetworkEnabledAsync(true));
                    }
                }

                EditorGUILayout.HelpBox(
                    "여기는 스파이크 경로(무조건 write)입니다. 게임이 실제로 쓰는 경로는 "
                    + "아래 랭킹 절의 '조건부 제출'입니다.\n"
                    + "실기 확인: Onikiri/Build/폰으로 빌드 + 설치 (개발 빌드) -> "
                    + "화면 좌상단 Firebase 버튼 -> adb logcat -s Unity", MessageType.Info);
            }

            DrawLeaderboardTools();
        }

        /**
         * @brief 랭킹 (54단계). 스파이크 절 바로 다음이다 - 같은 문서를 쓴다.
         *
         * 이 절이 없으면 확인 비용이 확인보다 커지는 자리가 셋이다:
         *
         *   디바운스 20초  기다려야만 제출이 나간다. "지금 제출"이 그것을 건너뛴다
         *   후퇴 거부      낮은 값이 거부되는 것을 보려면 서버보다 낮은 도달층이
         *                  필요한데, 도달층은 내려가지 않는다(52단계 단조성).
         *                  그래서 **임의 값 제출**을 둔다 - 이 버튼만이 규칙을
         *                  시험할 수 있다(정상 경로로는 낮은 값이 나올 수 없다)
         *   첫 진입 이름   이름을 한 번 정하면 되돌릴 방법이 게임 안에 없다
         */
        private void DrawLeaderboardTools()
        {
            EditorGUILayout.LabelField("랭킹 (리더보드)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("내 이름", PlayerProfile.Name
                    + (PlayerProfile.HasChosenName ? "" : "  (아직 안 정함)"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    debugPlayerName = EditorGUILayout.TextField("이름 바꾸기", debugPlayerName);
                    if (GUILayout.Button("적용", GUILayout.Width(60f)))
                    {
                        if (!PlayerProfile.SetName(debugPlayerName))
                            Debug.LogWarning("[Onikiri] 빈 이름은 저장되지 않습니다.");
                    }
                    if (GUILayout.Button("지우기", GUILayout.Width(60f))) PlayerProfile.Clear();
                }

                var submitter = Object.FindFirstObjectByType<Onikiri.Cloud.LeaderboardSubmitter>();
                Row("자동 제출기", submitter != null ? "배선됨 (Battle)" : "없음 - 랭킹 패널을 다시 빌드하세요");

                using (new EditorGUI.DisabledScope(submitter == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("지금 제출 (디바운스 건너뜀)"))
                    {
                        submitter.ForgetLastAttempt();
                        submitter.Submit("테스트 패널");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("임의 값", GUILayout.Width(64f));
                    debugSubmitStage = EditorGUILayout.IntField(debugSubmitStage);
                    // 후퇴 거부를 눈으로 보는 유일한 경로다. 정상 플레이에서는
                    // 서버보다 낮은 값이 애초에 만들어지지 않는다
                    if (GUILayout.Button("조건부 제출", GUILayout.Width(100f)))
                        Forget(CloudScores.SubmitIfHigherAsync(debugSubmitStage));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("상위 10 조회")) FetchTopForPanel(10);
                    if (GUILayout.Button("내 순위"))
                        Forget(CloudScores.FetchRankAsync(CloudScores.CurrentReach()));
                }

                if (!string.IsNullOrEmpty(leaderboardPreview))
                    EditorGUILayout.HelpBox(leaderboardPreview, MessageType.None);

                EditorGUILayout.HelpBox(
                    "제출은 도달층이 오른 뒤 " + Onikiri.Cloud.LeaderboardPolicy.DebounceSeconds
                    + "초 조용하면 자동으로, 앱이 뒤로 갈 때 한 번 더 나갑니다.\n"
                    + "서버 규칙(4-B)이 같은 검사를 한 겹 더 합니다 - 남의 문서·범위 밖·"
                    + "내려가는 값은 거부됩니다. 값의 진위 검증(Cloud Functions)은 다음 스텝입니다.",
                    MessageType.Info);
            }

            DrawAccountTools();
        }

        /**
         * @brief 계정 연동 (55단계). **에디터에서 확인할 수 있는 것과 없는 것을 가른다.**
         *
         * 구글 로그인은 안드로이드 네이티브(Credential Manager)라 에디터에서는
         * 아예 돌지 않는다 - 여기서 "연동" 버튼을 눌러 봐야 "이 기기에서는 쓸
         * 수 없습니다"만 나온다. 그래서 이 절이 하는 일은 셋이다:
         *
         *   상태 표시    지금 정체성이 무엇인지 (Unknown / Guest / Linked)
         *   병합 계산기  **이 스텝에서 에디터로 검사 가능한 유일한 알맹이.**
         *                재설치·다기기의 숫자 조합을 손으로 넣어 어느 값이
         *                살아남는지 본다. 실기에서는 그 상황을 만드는 데
         *                재설치 한 번이 통째로 든다
         *   분기표       어떤 실패가 어느 갈래로 가는지. 특히 **취소가
         *                복구로 새지 않는다**는 것을 눈으로 확인하는 자리
         *
         * 진짜 검증은 실기다: 개발 빌드 → 화면 좌상단 Firebase ▼ → 구글 연동.
         */
        private void DrawAccountTools()
        {
            EditorGUILayout.LabelField("계정 연동 (구글)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("상태", Onikiri.Cloud.AccountLink.State.ToString()
                    + (Onikiri.Cloud.AccountLink.State == Onikiri.Cloud.AccountState.Unknown
                        ? "  (Firebase 초기화 전)" : string.Empty));
                Row("마지막 갈래", Onikiri.Cloud.AccountLink.LastPlan.ToString());

                string linked = Onikiri.Cloud.AccountLink.LinkedLabel;
                Row("연동 계정", string.IsNullOrEmpty(linked) ? "(없음)" : linked);

                var google = Onikiri.Cloud.AuthProviders.Google;
                Row("공급자", google.DisplayName + " (" + google.Id + ")   사용가능 = "
                    + google.IsAvailable);
                Row("애플 자리", Onikiri.Cloud.AuthProviders.Apple.Id + "   사용가능 = "
                    + Onikiri.Cloud.AuthProviders.Apple.IsAvailable + "  (스텁 - 다음 스텝)");

                using (new EditorGUI.DisabledScope(Onikiri.Cloud.AccountLink.IsBusy))
                {
                    if (GUILayout.Button("구글 연동 시도 (에디터에서는 실패가 정답)"))
                        Forget(Onikiri.Cloud.AccountLink.LinkAsync(google));
                }

                if (!string.IsNullOrEmpty(Onikiri.Cloud.AccountLink.Status))
                    EditorGUILayout.HelpBox(Onikiri.Cloud.AccountLink.Status, MessageType.None);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("병합 계산기 (복구할 때 어느 값이 남는가)",
                                           EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    mergeLocal = EditorGUILayout.IntField("로컬", mergeLocal);
                    mergeAbandoned = EditorGUILayout.IntField("버릴 문서", mergeAbandoned);
                    mergeRecovered = EditorGUILayout.IntField("복구 문서", mergeRecovered);
                }

                int merged = Onikiri.Cloud.AccountLinkPolicy.MergedStage(
                    mergeLocal, mergeAbandoned, mergeRecovered);
                bool writes = Onikiri.Cloud.AccountLinkPolicy.ShouldMergeAfterRecovery(
                    merged, mergeRecovered);

                Row("결과", merged + "층 유지   /   서버에 쓰는가 = "
                    + (writes ? "예" : "아니오 (복구된 값이 이미 최고)"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("재설치 직후 (1 / 0 / 171)"))
                    { mergeLocal = 1; mergeAbandoned = 0; mergeRecovered = 171; }
                    if (GUILayout.Button("익명으로 더 감 (500 / 500 / 171)"))
                    { mergeLocal = 500; mergeAbandoned = 500; mergeRecovered = 171; }
                    if (GUILayout.Button("백업 복원 (12 / 500 / 171)"))
                    { mergeLocal = 12; mergeAbandoned = 500; mergeRecovered = 171; }
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("분기표 (실패 -> 계획)", EditorStyles.miniBoldLabel);

                foreach (Onikiri.Cloud.AuthFailure failure
                         in System.Enum.GetValues(typeof(Onikiri.Cloud.AuthFailure)))
                {
                    if (failure == Onikiri.Cloud.AuthFailure.None) continue;
                    Row(failure.ToString(),
                        "연동 실패 시 -> "
                        + Onikiri.Cloud.AccountLinkPolicy.PlanAfterLinkFailure(failure)
                        + "     /     자격증명 실패 시 -> "
                        + Onikiri.Cloud.AccountLinkPolicy.PlanAfterAcquireFailure(failure));
                }

                EditorGUILayout.HelpBox(
                    "구글 로그인은 **안드로이드 실기 전용**입니다 (Credential Manager 네이티브).\n"
                    + "실기 확인: Onikiri/Build/폰으로 빌드 + 설치 (개발 빌드) -> "
                    + "좌상단 Firebase ▼ -> 구글 연동 -> adb logcat -s Unity | grep AccountLink\n"
                    + "★ 이 스텝의 진짜 검증은 **앱 삭제 후 재설치 -> 구글 로그인 -> "
                    + "도달층 복구**입니다. AlreadyInUse -> Recover 갈래를 밟는지 보세요.",
                    MessageType.Info);
            }
        }

        /** 조회 결과를 패널 안에 그대로 적는다 - 콘솔과 화면을 오가지 않게 */
        private void FetchTopForPanel(int count)
        {
            leaderboardPreview = "조회 중...";
            CloudScores.FetchTopAsync(count).ContinueWith(task =>
            {
                var entries = task.Result;
                if (entries == null) { leaderboardPreview = "조회 실패 (콘솔 참고)"; return; }
                if (entries.Length == 0) { leaderboardPreview = "아직 아무도 없습니다"; return; }

                var text = new System.Text.StringBuilder();
                for (int i = 0; i < entries.Length; i++)
                    text.AppendFormat("{0,3}위  {1,-14} {2}층{3}\n", i + 1, entries[i].Name,
                        entries[i].MaxStage, entries[i].IsMe ? "  <- 나" : string.Empty);
                leaderboardPreview = text.ToString().TrimEnd();
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /**
         * @brief 반환된 Task를 버리되 예외는 관측한다.
         *
         * 그냥 버리면 실패한 Task가 GC될 때 UnobservedTaskException으로 뒤늦게
         * 떠서, 어느 버튼이 원인인지 알 수 없는 로그가 콘솔에 남는다. CloudScores가
         * 내부에서 이미 다 잡지만, 이 창은 그 약속에 기대지 않는다.
         */
        private static void Forget(System.Threading.Tasks.Task task)
        {
            task.ContinueWith(t => Debug.LogWarning("[Onikiri] 클라우드 호출 실패: " + t.Exception),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        private void DrawScreenTools()
        {
            EditorGUILayout.LabelField("게임 뷰", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // CropFrame.None이라 세로가 긴 기기일수록 월드가 더 보인다.
                // 지면선과 UI 밴드가 세 비율에서 모두 유지되는지 확인하는 용도
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("비율", GUILayout.Width(40f));
                    if (GUILayout.Button("9:16")) GameViewSizes.Select(1080, 1920, "9:16 Portrait");
                    if (GUILayout.Button("9:19.5")) GameViewSizes.Select(1080, 2340, "9:19.5 Portrait");
                    if (GUILayout.Button("9:21")) GameViewSizes.Select(1080, 2520, "9:21 Portrait");
                }

                if (GUILayout.Button("스크린샷 (Assets/Screenshots)"))
                {
                    string path = "Assets/Screenshots/shot_" + System.DateTime.Now.ToString("HHmmss") + ".png";
                    System.IO.Directory.CreateDirectory("Assets/Screenshots");
                    // 카메라를 RenderTexture에 직접 그리지 않고 게임 뷰를 그대로 뜬다.
                    // 직접 그리면 레이아웃이 실제 Screen 크기 기준으로 계산돼 있어
                    // 프레이밍이 어긋난 그림이 나온다
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log("[Onikiri] 스크린샷은 다음 프레임에 기록됩니다 -> " + path);
                }
            }
        }

        private void DrawBuilders()
        {
            EditorGUILayout.LabelField("빌더", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("스테이지 + 전투 다시 빌드"))
                {
                    // 순서가 있다. 스테이지가 지면 앵커와 사무라이를 만들고,
                    // 전투가 그 위에 스포너와 강화를 얹는다
                    BattleStageBuilder.Build();
                    BattleContentBuilder.Build();
                }

                if (GUILayout.Button("폰트 다시 굽기 (문자셋 포함)"))
                    PixelFontAssetBuilder.BuildAll();

                if (GUILayout.Button("폰트 검증 시트"))
                    FontProofSheet.Render();
            }
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(90f));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(16f));
            }
        }

        private static int PixelRatio()
        {
            var camera = Camera.main;
            if (camera == null) return 0;
            var ppc = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            return ppc != null ? ppc.pixelRatio : 0;
        }
    }
}
