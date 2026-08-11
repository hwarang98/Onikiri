// using System 을 넣지 않는다. System.Object 와 UnityEngine.Object 가 충돌해서
// 이 파일 전체의 FindFirstObjectByType 호출이 모호해진다
using Onikiri.Battle;
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

        /** 방치 보상 확인용. 몇 시간 전에 종료한 것으로 꾸밀지 */
        private float offlineHours = 3f;

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

            DrawSkillTools();
            DrawQuestTools();

            // 장비는 퀘스트 바로 다음이다. 보석이 그쪽에서 나와 이쪽으로
            // 들어가므로, 두 절이 붙어 있으면 루프 전체를 한 화면에서 돌린다
            DrawEquipmentTools();

            // 요도는 장비 바로 다음이다. **같은 화면(대장간)의 옆 탭**이고,
            // 보석 소비처(파편 조달)도 하나 더 여기 있다 - 퀘스트 -> 장비 ->
            // 요도가 한 화면에서 도는 보석 루프다
            DrawYodoTools();

            // 뽑기·상점은 요도 바로 다음이다 (46단계). 파는 것이 전부 요도의
            // 재료(파편·혼 정수)라, 위 절의 자루 목록이 곧 이 절의 결과판이다 -
            // 뽑고 나서 눈을 옮기지 않고 티어가 오르는 것을 본다
            DrawGachaTools();

            // 전직도 보석 소비처라 장비 바로 다음이다 (33단계)
            DrawEvolutionTools();

            // 동료도 보석 소비처(해금)라 전직 다음이다. 레벨은 골드다
            DrawPetTools();

            DrawSaveTools();
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
            // 2b: 레벨업 버튼은 이제 상단 바가 아니라 **성장 패널 헤더의 스트립
            // 라인 오른쪽**에 뜬다. 등장 위치 확인도 이 버튼으로 한다. 상단 바
            // 초상 배지(Lv.n)가 같은 값으로 함께 갱신되는지도 여기서 본다
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
         * 어디에도 없었다.
         *
         * 세 가지를 나눠 보여준다 - 셋이 서로 다른 실패를 가리키기 때문이다.
         *
         *   쿨다운 진행  타이머가 도는가        (0에 붙어 있으면 사거리가 계속 비었다)
         *   시전 횟수    실제로 나갔는가        (타이머는 도는데 0이면 CastSkill이 거절 중)
         *   초당 환산    DPS에 얼마나 들어가는가
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
                        string.Format("자동 시전 {0}   환산 {1:F2}회/초",
                            skills.AutoCast ? "ON" : "OFF", skills.CastRate),
                        GUILayout.Width(190f));

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

                for (int i = 0; i < skills.SlotCount; i++)
                {
                    var slot = skills.GetSlot(i);
                    if (slot == null) continue;

                    bool unlocked = skills.IsUnlocked(i);
                    int index = i;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            unlocked
                                ? string.Format("{0} Lv.{1}", slot.displayName, slot.level)
                                : string.Format("{0} (Lv.{1} 필요)", slot.displayName, slot.unlockLevel),
                            GUILayout.Width(120f));

                        // 쿨다운을 막대로 그린다. 숫자만 적으면 "도는지"를 두 번
                        // 읽어서 비교해야 하는데, 막대는 한 번 보면 안다
                        var bar = GUILayoutUtility.GetRect(60f, 16f, GUILayout.Width(60f));
                        EditorGUI.ProgressBar(bar, unlocked ? skills.CooldownFraction(i) : 0f,
                            unlocked ? skills.SecondsUntilCast(i).ToString("F1") : "-");

                        EditorGUILayout.LabelField(
                            string.Format("×{0:F2}  시전 {1}회", skills.MultiplierOf(i), skills.CastCountOf(i)),
                            GUILayout.Width(120f));

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
            }

            if (boss.Current == BossFight.Phase.Failed && !string.IsNullOrEmpty(boss.FailureMessage))
                EditorGUILayout.HelpBox(boss.FailureMessage, MessageType.Warning);
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

                foreach (int target in new[] { 51, 100, 200 })
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
