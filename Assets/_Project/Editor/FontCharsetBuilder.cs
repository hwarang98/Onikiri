using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /// <summary>
    /// Collects every character the game actually displays, so the font atlas only has to
    /// contain those.
    ///
    /// Korean is the reason this exists. Baking all 11,172 precomposed Hangul syllables at
    /// 11px would produce a huge multi-page atlas for a mobile idle game that shows maybe
    /// two hundred distinct characters. Extracting the real set keeps it to a single small
    /// page, and re-running this after adding UI text keeps it honest.
    ///
    /// Sources: the hand-maintained UIStrings.txt, plus any TMP text already authored in
    /// prefabs and the main scene.
    /// </summary>
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

        /// <summary>The characters to bake, as one string. Used by the font builder.</summary>
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
                // '#' lines document the file rather than contributing glyphs.
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
                // Whitespace and control characters have no glyph to bake.
                if (c == ' ' || char.IsControl(c) || char.IsWhiteSpace(c)) continue;
                into.Add(c);
            }
        }

        /// <summary>Wraps at 64 characters purely so the file stays readable in a diff.</summary>
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
