using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 귀문 실기 테스트 전용 빌드. **운영 설치와 운영 클라우드를 못 건드린다.**
     *
     * ## 왜 기존 `AndroidDeployBuilder`를 안 쓰는가
     *
     * 그쪽은 현재 `applicationIdentifier`(`com.studio202.onikiri`)로 빌드한다.
     * 그 패키지는 **폰에 이미 설치돼 있고 실사용 세이브가 그 안에 있다** -
     * 덮어쓰면 v21 마이그레이션이 실사용 데이터에 그대로 걸린다.
     *
     * v21은 되돌릴 수 없다(v21 세이브를 v20 클라이언트가 읽으면 새 게임이 된다).
     * 실기 테스트 하나 하려고 그 위험을 질 이유가 없다.
     *
     * ## 두 가지를 갈아 끼운다
     *
     *   패키지명   `.dev` 접미사. Android가 패키지 단위로 샌드박스하므로
     *              운영 앱의 세이브·설정·캐시가 **물리적으로 분리된다**
     *   Firebase   `google-services.json`을 잠시 치운다. 설정이 없으면
     *              `CloudScores.InitializeAsync`가 실패로 떨어지고, 그 경로는
     *              이미 "게임은 계속 돈다"로 설계돼 있다(그 함수 주석)
     *
     * 둘 다 `finally`에서 되돌린다 - 빌드가 실패해도 프로젝트 설정이 바뀐 채로
     * 남지 않는다.
     *
     * ## 개발 빌드로 굽는다
     *
     * `Debug.Log`가 logcat에 그대로 남는다. 실기 보고서가 요구하는 것이
     * 로그이므로 그것 없이는 무엇이 일어났는지 증명할 수 없다.
     */
    public static class TrialFieldTestBuilder
    {
        /** 운영과 갈라 두는 접미사. 이 한 줄이 실사용 세이브를 지킨다 */
        public const string DevSuffix = ".dev";

        public const string ApkPath = "Builds/Android/ONIKIRI-trial-dev.apk";

        const string FirebaseConfig = "Assets/google-services.json";
        const string FirebaseParked = "Assets/google-services.json.parked";

        [MenuItem("Onikiri/Build/귀문 실기 빌드 (개발 패키지)", false, 110)]
        public static void BuildDevPackage()
        {
            // 이전 빌드가 복구에 실패해 `.dev`가 남아 있을 수 있다. 그 상태를
            // "원래 값"으로 받아들이면 운영 패키지명이 영영 돌아오지 않는다 -
            // 접미사를 **떼고** 시작해서 남은 오염이 스스로 낫게 한다
            string originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (originalId.EndsWith(DevSuffix))
                originalId = originalId.Substring(0, originalId.Length - DevSuffix.Length);

            string devId = originalId + DevSuffix;

            bool parkedFirebase = false;

            try
            {
                // ---- Firebase 설정을 잠시 치운다
                if (File.Exists(FirebaseConfig))
                {
                    if (File.Exists(FirebaseParked)) File.Delete(FirebaseParked);
                    File.Move(FirebaseConfig, FirebaseParked);
                    parkedFirebase = true;

                    var meta = FirebaseConfig + ".meta";
                    if (File.Exists(meta)) File.Move(meta, meta + ".parked");

                    AssetDatabase.Refresh();
                    Debug.Log("[Onikiri] google-services.json을 잠시 치웠다 - "
                              + "이 빌드는 운영 Firestore에 접근하지 않는다.");
                }

                // ---- 개발 패키지명으로 갈아 끼운다
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, devId);
                Debug.Log("[Onikiri] 실기 빌드 패키지: " + devId
                          + " (운영 " + originalId + "은 폰에서 건드리지 않는다)");

                var scenes = new System.Collections.Generic.List<string>();
                foreach (var scene in EditorBuildSettings.scenes)
                    if (scene.enabled) scenes.Add(scene.path);

                if (scenes.Count == 0)
                {
                    Debug.LogError("[Onikiri] 빌드에 포함된 씬이 없습니다.");
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    // 로그가 남아야 실기 보고가 가능하다
                    options = BuildOptions.Development
                });

                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Debug.LogError("[Onikiri] 실기 빌드 실패: " + report.summary.result
                                   + " (오류 " + report.summary.totalErrors + "개)");
                    return;
                }

                Debug.Log(string.Format("[Onikiri] 실기 빌드 성공: {0} ({1:F1}MB, {2:F0}초)",
                    ApkPath, new FileInfo(ApkPath).Length / (1024f * 1024f),
                    report.summary.totalTime.TotalSeconds));
            }
            finally
            {
                // ---- 무슨 일이 있어도 되돌린다
                RestoreApplicationIdentifier(originalId);

                if (parkedFirebase && File.Exists(FirebaseParked))
                {
                    if (File.Exists(FirebaseConfig)) File.Delete(FirebaseConfig);
                    File.Move(FirebaseParked, FirebaseConfig);

                    var parkedMeta = FirebaseConfig + ".meta.parked";
                    if (File.Exists(parkedMeta))
                    {
                        var meta = FirebaseConfig + ".meta";
                        if (File.Exists(meta)) File.Delete(meta);
                        File.Move(parkedMeta, meta);
                    }

                    AssetDatabase.Refresh();
                }

                Debug.Log("[Onikiri] Firebase 설정을 원래대로 되돌렸다.");
            }
        }

        const string ProjectSettingsAsset = "ProjectSettings/ProjectSettings.asset";

        /**
         * @brief 패키지명을 되돌리고 **파일이 실제로 그 값인지 확인한다.**
         *
         * ## 3단계 결함 C가 5.0단계에 다시 났다 - 그 대책이 이 경로를 못 막았다
         *
         * 3단계는 `finally`에 `AssetDatabase.SaveAssets()`를 넣어 "디스크까지 밀어
         * 넣는다"고 적었다. 그런데 `SaveAssets`는 **더티 플래그가 있는 것만 쓴다.**
         * 빌드가 시작될 때 에디터가 이미 `.dev`를 디스크에 저장해 두면, 그 뒤
         * 메모리를 원래 값으로 되돌려도 더티가 아니므로 `SaveAssets`가 **아무것도
         * 쓰지 않는다** - 파일에는 `.dev`가 남고 다음 빌드가 그것을 "원래 값"으로
         * 읽는다. 5.0단계의 실패한 빌드에서 정확히 그 상태가 만들어졌다.
         *
         * 그래서 이제 세 단계로 확인한다:
         *
         *   1. 메모리를 되돌린다
         *   2. `ProjectSettings.asset`을 강제로 더티로 만들고 저장한다
         *   3. **파일을 읽어** 개발 접미사가 남아 있지 않은지 본다. 남아 있으면
         *      `LogError`로 크게 적는다 - 조용히 넘어가면 출시 빌드의 패키지명이
         *      바뀌는 사고가 된다
         */
        private static void RestoreApplicationIdentifier(string originalId)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);

            // 더티가 아니면 SaveAssets가 아무것도 안 쓴다. 그 자산을 직접 더럽힌다
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAsset))
                if (asset != null) EditorUtility.SetDirty(asset);

            AssetDatabase.SaveAssets();

            // ---- 파일로 확인한다. 메모리가 맞다는 것만으로는 부족했다
            try
            {
                if (File.Exists(ProjectSettingsAsset)
                    && File.ReadAllText(ProjectSettingsAsset).Contains(originalId + DevSuffix))
                {
                    Debug.LogError(string.Format(
                        "[Onikiri] ProjectSettings.asset에 개발 패키지명({0})이 남았다. "
                        + "출시 빌드의 패키지명이 바뀌는 사고이므로 즉시 되돌려야 한다 - "
                        + "`git checkout -- {1}` 또는 Player Settings에서 직접 고칠 것.",
                        originalId + DevSuffix, ProjectSettingsAsset));
                    return;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Onikiri] ProjectSettings.asset 확인 실패: " + exception.Message);
            }

            Debug.Log("[Onikiri] 패키지명을 파일까지 확인해 되돌렸다: " + originalId);
        }

        // ---------------------------------------------------------------- 밴드 프리셋

        /**
         * @brief **실제 밴드 빌드를 세이브로 찍어낸다** (승급 5.0단계 §2).
         *
         * ## 왜 이것이 필요했나
         *
         * 3단계 실기는 공격력 강화 레벨만 손으로 바꾼 합성 빌드로 k를 쟀고, 그래서
         * **k를 고정하지 못했다**(Step3 §9.6이 스스로 그렇게 적었다). 판정 기준의
         * 세 빌드는 `StageSimulation`이 정의하므로, 실기가 k를 고정할 자격을 갖는
         * 유일한 길은 그 빌드를 그대로 세이브로 만드는 것이다.
         *
         * ## 어디에 쓰는가 - **사용자 세이브 폴더에 절대 안 쓴다**
         *
         * 출력은 프로젝트 안(`Builds/Step5/presets`)이다. `SaveSystem.Path`는
         * 에디터에서 실사용 세이브를 가리키므로, 그 자리에 쓰면 실기 측정 하나
         * 하려고 실사용 진행을 덮어쓰는 셈이다 - 3단계가 패키지명으로 같은 위험을
         * 피한 것과 같은 판단이고, 여기서는 아예 그 경로를 쓰지 않는다.
         *
         * 기기로는 adb로 밀어 넣는다. 명령은 매니페스트에 함께 적힌다.
         *
         * `Middle`은 만들지 않는다 - 실재하는 빌드가 아니다(`TrialPresetForge.Profile`).
         */
        [MenuItem("Onikiri/Build/귀문 밴드 프리셋 생성 (Floor · CurveFollower x 6문)", false, 111)]
        public static void BuildBandPresets()
        {
            var field = Onikiri.DevTools.DevSimField.FieldFromAssets();

            // 200스테이지를 도는 이유는 테스트 픽스처와 같은 런을 쓰기 위해서다.
            // 게이트 줄은 앞부분이라 길이에 안 흔들리지만, 같은 수를 쓰면 "같은
            // 런인가"를 두 번 묻지 않아도 된다 (PromotionEconomyFixture.Stages)
            const int stages = 200;

            var floor = Onikiri.Progression.StageSimulation.Run(stages, field,
                new Onikiri.Progression.StageSimulation.Policy { GemsFromQuestsOnly = true });
            var curve = Onikiri.Progression.StageSimulation.Run(stages, field,
                Onikiri.Progression.StageSimulation.Policy.Default);

            Directory.CreateDirectory(Onikiri.DevTools.TrialPresetForge.OutputFolder);

            // PlayMode 동등성 검사가 읽는 기대값 표. 프리셋과 **같은 실행에서**
            // 나와야 한다 - 따로 만들면 그 둘이 다른 런에서 나올 수 있다
            var expected = new System.Collections.Generic.List<Onikiri.DevTools.TrialPresetForge.Expected>();

            var log = new System.Text.StringBuilder();
            log.AppendLine("# 귀문 밴드 프리셋 (승급 5.0단계 §2)");
            log.AppendLine();
            log.AppendLine("잡몹 평균 체력 = " + field.AverageMobHealth.ToString("E6")
                           + " / 골드 = " + field.AverageMobGold.ToString("E6")
                           + " / 보충 간격 = " + field.SpawnInterval);
            log.AppendLine();

            int mismatches = 0;

            for (int gate = 1; gate <= Onikiri.Progression.PromotionTrialCatalog.GateCount; gate++)
            {
                int stage = Onikiri.Progression.PromotionTrialCatalog.GateStages[gate - 1];
                double reference = Onikiri.Progression.PromotionTrialCatalog.ReferencePowerForGate(
                    Onikiri.Core.BigDouble.FromDouble(field.AverageMobHealth), gate);

                for (int p = 0; p < 2; p++)
                {
                    var profile = p == 0
                        ? Onikiri.DevTools.TrialPresetForge.Profile.Floor
                        : Onikiri.DevTools.TrialPresetForge.Profile.CurveFollower;

                    var row = (p == 0 ? floor : curve)[stage - 1];
                    var save = Onikiri.DevTools.TrialPresetForge.Build(row, gate);
                    var bad = Onikiri.DevTools.TrialPresetForge.Audit(save, row, gate);
                    var power = Onikiri.DevTools.TrialPresetForge.Measure(row, reference);

                    string name = Onikiri.DevTools.TrialPresetForge.NameOf(profile, gate);
                    string path = Path.Combine(Onikiri.DevTools.TrialPresetForge.OutputFolder, name + ".json");

                    // **여기가 유일한 쓰기 지점이고 프로젝트 안이다.** 실사용
                    // 세이브 경로와 같아지는 일이 없도록 한 번 더 확인한다
                    string full = Path.GetFullPath(path);
                    if (full == Path.GetFullPath(Onikiri.Progression.SaveSystem.Path))
                    {
                        Debug.LogError("[Onikiri] 프리셋 출력 경로가 실사용 세이브와 같다. 중단한다.");
                        return;
                    }

                    File.WriteAllText(path, Onikiri.DevTools.TrialPresetForge.ToJson(save));
                    mismatches += bad.Count;

                    expected.Add(Onikiri.DevTools.TrialPresetForge.ExpectedOf(row, gate, name, reference));

                    log.AppendLine("## " + name + "  (문" + gate + " · st" + stage + ")");
                    log.AppendLine();
                    log.AppendLine("밴드 화력(stats) = " + power.BandDps.ToString("E6")
                                   + "   P = " + power.BandP.ToString("F4"));
                    log.AppendLine("프리셋 화력(구매 뒤) = " + power.PresetDps.ToString("E6")
                                   + "   P = " + power.PresetP.ToString("F4")
                                   + "   비 = x" + power.Ratio.ToString("F4"));
                    log.AppendLine("감사 불일치 = " + bad.Count);
                    foreach (var m in bad) log.AppendLine("  ! " + m);
                    log.AppendLine();
                    log.AppendLine(Onikiri.DevTools.TrialPresetForge.Describe(save));
                }
            }

            log.AppendLine("## 모델링하지 않은 것");
            log.AppendLine();
            foreach (var line in Onikiri.DevTools.TrialPresetForge.NotModelled())
                log.AppendLine("- " + line);

            log.AppendLine();
            log.AppendLine("## 기기에 밀어 넣기 (개발 패키지 전용)");
            log.AppendLine();
            log.AppendLine("adb push <preset>.json /sdcard/Android/data/"
                           + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)
                           + DevSuffix + "/files/" + Onikiri.Progression.SaveSystem.FileName);

            string manifest = Path.Combine(Onikiri.DevTools.TrialPresetForge.OutputFolder, "manifest.md");
            File.WriteAllText(manifest, log.ToString());

            var table = new Onikiri.DevTools.TrialPresetForge.ExpectedTable { presets = expected.ToArray() };
            string expectedPath = Path.Combine(Onikiri.DevTools.TrialPresetForge.OutputFolder,
                Onikiri.DevTools.TrialPresetForge.ExpectedFileName);
            File.WriteAllText(expectedPath, UnityEngine.JsonUtility.ToJson(table, true));

            Debug.Log(string.Format(
                "[Onikiri] 밴드 프리셋 12벌을 만들었다: {0} (감사 불일치 {1}건). 매니페스트: {2} · 기대값: {3}",
                Onikiri.DevTools.TrialPresetForge.OutputFolder, mismatches, manifest, expectedPath));
        }
    }
}
