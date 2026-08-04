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
                damageNumbers = null; hitAudio = null;
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
                    Row("공격속도", string.Format("설정 {0:F2} -> 실측 {1:F2}회/초  ({2:P0})",
                        combat.AttacksPerSecond, measuredRate,
                        combat.AttacksPerSecond > 0f ? measuredRate / combat.AttacksPerSecond : 0f));

                    Row("데미지", NumberFormatter.Format(combat.Damage));
                    Row("누적 공격", combat.AttackCount.ToString());
                }

                var wallet = PlayerWallet.Instance;
                if (wallet != null)
                    Row("골드", NumberFormatter.Format(wallet.Gold) +
                                "   (누적 " + NumberFormatter.Format(wallet.LifetimeGold) + ")");

                if (spawner != null)
                {
                    int alive = 0;
                    foreach (var enemy in spawner.Active) if (enemy != null && enemy.IsAlive) alive++;
                    Row("요괴", alive + "마리 생존 / 활성 " + spawner.Active.Count);
                }

                // 풀 증식은 조용히 일어나고 폰에서 프레임 히칭으로만 나타난다.
                // 0이 아니면 prewarm이 부족하다는 뜻이다
                Row("풀 증식", string.Format("적 {0} / 참격 {1} / 데미지 {2}",
                    spawner != null ? spawner.PoolGrowthCount : 0,
                    combat != null ? combat.SlashPoolGrowthCount : 0,
                    damageNumbers != null ? damageNumbers.PoolGrowthCount : 0));

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

                            int index = i;
                            if (GUILayout.Button("+1")) upgrades.TryPurchase(index);
                            if (GUILayout.Button("+10"))
                                for (int n = 0; n < 10 && upgrades.TryPurchase(index); n++) { }
                            if (GUILayout.Button("최대"))
                                for (int n = 0; n < 10000 && upgrades.TryPurchase(index); n++) { }
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(combat == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("공격속도", GUILayout.Width(64f));
                    // 강화 상한을 넘겨 스윙 압축이 버티는지 보기 위한 직접 설정.
                    // 강화 레벨과는 무관하며 다음 구매에서 곡선값으로 되돌아간다
                    float value = EditorGUILayout.Slider(combat != null ? combat.AttacksPerSecond : 0f, 0.5f, 40f);
                    if (combat != null && !Mathf.Approximately(value, combat.AttacksPerSecond))
                        combat.AttacksPerSecond = value;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("시간 배속", GUILayout.Width(64f));
                    if (GUILayout.Button("0.25x")) Time.timeScale = 0.25f;
                    if (GUILayout.Button("1x")) Time.timeScale = 1f;
                    if (GUILayout.Button("4x")) Time.timeScale = 4f;
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
            }
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
