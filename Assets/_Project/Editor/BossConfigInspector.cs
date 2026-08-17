using Onikiri.Battle;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief BossConfig 인스펙터. 측정 버튼과 검사가 붙는다.
     *
     * 기본 인스펙터로도 편집은 되지만, 셀 크기와 발밑 여백은 **틀렸을 때
     * 조용히 틀린다.** 셀 크기가 어긋나면 프레임이 반씩 잘려 나오고 여백이
     * 어긋나면 보스가 공중에 뜨거나 땅에 박히는데, 둘 다 플레이해 봐야 안다.
     * 여기서 재고 여기서 경고한다.
     */
    [CustomEditor(typeof(BossConfig))]
    public sealed class BossConfigInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (BossConfig)target;

            EditorGUILayout.Space();

            if (config.kind == BossConfig.ArtKind.Sheets)
                DrawSheetTools(config);
            else
                DrawScaledMobChecks(config);
        }

        private void DrawSheetTools(BossConfig config)
        {
            EditorGUILayout.LabelField("시트 도구", EditorStyles.boldLabel);

            // 셀 폭 추정: 모든 시트 폭의 최대공약수. 한 줄 배치라 시트 높이가
            // 곧 셀 높이다. 이 방법이 다크 사무라이 128과 FULL_Samurai 96을
            // 정확히 맞춘다
            if (GUILayout.Button("셀 크기 추정 (시트 폭의 최대공약수)"))
            {
                int width, height;
                if (GuessCell(config, out width, out height))
                {
                    Undo.RecordObject(config, "Guess cell size");
                    config.cellWidth = width;
                    config.cellHeight = height;
                    EditorUtility.SetDirty(config);
                    Debug.Log("[Onikiri] Guessed cell " + width + "x" + height + " for " + config.name);
                }
                else
                {
                    Debug.LogError("[Onikiri] Could not guess a cell size - sheet heights differ.");
                }
            }

            if (GUILayout.Button("발밑 여백 자동 측정"))
            {
                string report;
                int measured = BossConfigBuilder.MeasureFeetPadding(config, out report);

                if (measured < 0)
                {
                    Debug.LogError("[Onikiri] Could not measure feet padding for " + config.name
                                   + " (" + report + ")");
                }
                else
                {
                    Undo.RecordObject(config, "Measure feet padding");
                    config.feetPadding = measured;
                    EditorUtility.SetDirty(config);
                    Debug.Log("[Onikiri] " + config.name + " feet padding = " + measured
                              + "px  [" + report + "]");
                }
            }

            if (GUILayout.Button("이 보스만 다시 빌드"))
            {
                BossConfigBuilder.Build(config);
                AssetDatabase.SaveAssets();
            }

            if (config.generatedDefinition == null)
            {
                EditorGUILayout.HelpBox(
                    "아직 정의가 생성되지 않았습니다. [이 보스만 다시 빌드] 또는 "
                    + "Onikiri/Scene/Build Combat Content 를 실행하세요.",
                    MessageType.Warning);
            }
        }

        private void DrawScaledMobChecks(BossConfig config)
        {
            if (config.scale < 1)
            {
                EditorGUILayout.HelpBox("확대 배율은 1 이상이어야 합니다.", MessageType.Error);
                return;
            }

            // 어두운 틴트는 곱연산이라 스프라이트를 더 어둡게만 만든다. 요괴 아트가
            // 원래도 어두워서 검은 덩어리가 된다 - 11단계에서 실제로 겪었다
            float brightness = Mathf.Max(config.tint.r, Mathf.Max(config.tint.g, config.tint.b));
            if (brightness < 0.75f)
            {
                EditorGUILayout.HelpBox(
                    "틴트가 어둡습니다. SpriteRenderer.color는 곱연산이라 어두운 틴트는 "
                    + "요괴를 검은 덩어리로 만듭니다. 한 채널을 255에 두고 나머지를 낮추세요.",
                    MessageType.Warning);
            }

            if (config.baseMob == null)
            {
                EditorGUILayout.HelpBox(
                    "잡몹이 비어 있습니다. 이대로 두면 그 스테이지의 잡몹이 자동으로 쓰입니다 "
                    + "(일반 스테이지 보스의 기본 동작).",
                    MessageType.Info);
            }
        }

        private static bool GuessCell(BossConfig config, out int width, out int height)
        {
            width = 0;
            height = -1;

            var sheets = new[] { config.idleSheet, config.hurtSheet, config.deathSheet, config.attackSheet };
            foreach (var sheet in sheets)
            {
                if (sheet == null) continue;

                width = Gcd(width, sheet.width);
                if (height < 0) height = sheet.height;
                else if (height != sheet.height) return false;
            }

            return width > 0 && height > 0;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = b; b = a % b; a = t; }
            return a;
        }
    }
}
