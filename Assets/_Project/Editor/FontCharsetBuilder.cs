using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 게임이 실제로 표시하는 글자만 모아, 폰트 아틀라스가 그것만 담게 한다.
     *
     * 이것이 존재하는 이유는 한글이다. 완성형 한글 11,172자를 전부 구우면, 실제로는
     * 이백 자 남짓만 보여주는 모바일 방치형에 거대한 다중 페이지 아틀라스가 생긴다.
     * 실제 사용 집합만 뽑으면 작은 한 페이지로 끝나고, UI 문구를 추가한 뒤 다시 돌리면
     * 계속 정확하게 유지된다.
     *
     * 출처: 손으로 관리하는 UIStrings.txt, 그리고 프리팹과 메인 씬에 이미 작성된
     * TMP 텍스트.
     */
    public static class FontCharsetBuilder
    {
        public const string StringsPath = "Assets/_Project/Data/UIStrings.txt";
        public const string CharsetPath = "Assets/_Project/Data/FontCharset.txt";

        [MenuItem("Onikiri/Art/Rebuild Font Charset")]
        public static void Rebuild()
        {
            var characters = new SortedSet<char>();

            int fromFile = AddFromStringsFile(characters);
            int fromPrefabs = AddFromPrefabs(characters);
            int fromScene = AddFromOpenScene(characters);

            var text = BuildCharsetString(characters);
            File.WriteAllText(CharsetPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CharsetPath);

            Debug.Log(string.Format(
                "[Onikiri] Charset rebuilt: {0} unique characters " +
                "(UIStrings {1}, prefabs {2}, open scene {3}) -> {4}",
                characters.Count, fromFile, fromPrefabs, fromScene, CharsetPath));
        }

        /** 구워야 할 글자들을 하나의 문자열로. 폰트 빌더가 쓴다 */
        public static string LoadCharset()
        {
            if (!File.Exists(CharsetPath))
            {
                Rebuild();
            }
            return File.ReadAllText(CharsetPath, Encoding.UTF8).Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static int AddFromStringsFile(SortedSet<char> into)
        {
            if (!File.Exists(StringsPath))
            {
                Debug.LogWarning("[Onikiri] Missing " + StringsPath);
                return 0;
            }

            int before = into.Count;
            foreach (var line in File.ReadAllLines(StringsPath, Encoding.UTF8))
            {
                // '#' 로 시작하는 줄은 파일 설명이지 글리프가 아니다
                if (line.StartsWith("#")) continue;
                AddAll(into, line);
            }
            return into.Count - before;
        }

        private static int AddFromPrefabs(SortedSet<char> into)
        {
            int before = into.Count;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true))
                    AddAll(into, label.text);
            }
            return into.Count - before;
        }

        private static int AddFromOpenScene(SortedSet<char> into)
        {
            int before = into.Count;
            foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddAll(into, label.text);
            return into.Count - before;
        }

        private static void AddAll(SortedSet<char> into, string source)
        {
            if (string.IsNullOrEmpty(source)) return;

            foreach (var c in source)
            {
                // 공백과 제어 문자는 구울 글리프가 없다
                if (c == ' ' || char.IsControl(c) || char.IsWhiteSpace(c)) continue;
                into.Add(c);
            }
        }

        /** 64자마다 줄바꿈. 순전히 diff에서 읽기 좋게 하기 위함 */
        private static string BuildCharsetString(SortedSet<char> characters)
        {
            var builder = new StringBuilder();
            int column = 0;
            foreach (var c in characters)
            {
                builder.Append(c);
                if (++column % 64 == 0) builder.Append('\n');
            }
            return builder.ToString();
        }
    }
}
