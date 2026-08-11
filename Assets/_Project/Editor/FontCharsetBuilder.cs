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
     * 출처: 손으로 관리하는 UIStrings.txt, 프리팹과 메인 씬에 이미 작성된
     * TMP 텍스트, 그리고 **화면에 이름이 뜨는 데이터 애셋**.
     *
     * ## 데이터 애셋을 훑는 이유
     *
     * 보스 이름은 씬에도 프리팹에도 없다. `BossConfig.displayName`에 적혀 있고
     * 런타임에 등장 연출이 읽어 간다. 그래서 21단계에 처형인을 추가했을 때
     * UIStrings.txt에 손으로 옮겨 적는 것을 잊었고, **화면에 □□□이 떴다.**
     *
     * 같은 실수를 9단계(보스 이름)와 11단계(사망 문구)에도 했다. 세 번 반복된
     * 것은 "잊지 말자"로 풀 문제가 아니라는 뜻이다. 이름의 출처가 애셋이면
     * 문자셋도 그 애셋에서 나와야 한다 - 손으로 옮겨 적는 단계가 있는 한
     * 그 단계는 언젠가 빠진다.
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
            int fromData = AddFromDataAssets(characters);

            var text = BuildCharsetString(characters);
            File.WriteAllText(CharsetPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CharsetPath);

            Debug.Log(string.Format(
                "[Onikiri] Charset rebuilt: {0} unique characters " +
                "(UIStrings {1}, prefabs {2}, open scene {3}, data {4}) -> {5}",
                characters.Count, fromFile, fromPrefabs, fromScene, fromData, CharsetPath));
        }

        /**
         * @brief 화면에 이름이 뜨는 데이터 애셋에서 글자를 모은다.
         *
         * 여기 적힌 이름은 전부 실제로 화면에 선다.
         *
         *   BossConfig       보스 등장 연출의 이름
         *   EnemyDefinition  일반 스테이지 보스는 "거대 " + 잡몹 이름이다
         *   RegionConfig     상단 바의 "지역 N"
         *
         * 등장하지 않는 애셋(배경 세트의 표시명 등)은 일부러 넣지 않는다.
         * 아틀라스를 작게 유지하는 것이 이 클래스의 존재 이유다.
         */
        public static int AddFromDataAssets(SortedSet<char> into)
        {
            int before = into.Count;

            foreach (var name in DisplayNames()) AddAll(into, name);

            return into.Count - before;
        }

        /** 화면에 서는 데이터 애셋 이름 전부. 글리프 검사도 같은 목록을 본다 */
        public static IEnumerable<string> DisplayNames()
        {
            // 오의 이름은 애셋이 아니라 코드(SkillCatalog)에 있다. 그래도 여기
            // 넣는 이유는 위 주석이 적은 것과 같다 - **이름의 출처가 어디든
            // 문자셋은 그 출처에서 나와야 한다.** UIStrings.txt에 손으로 옮겨
            // 적으면 스킬을 추가하는 날 그 단계가 빠지고, 화면에 □□이 뜬다.
            // 보스 이름으로 세 번 겪은 실수다
            foreach (var skill in Onikiri.Progression.SkillCatalog.Skills)
                yield return skill.DisplayName;

            // 퀘스트 제목도 코드(QuestCatalog)에 있다. 오의와 같은 이유로 여기서
            // 끌어온다 - 열여덟 줄을 손으로 옮겨 적으면 하나를 고치는 날 그 글자가
            // 빠지고, 화면에 □이 뜬다
            foreach (var kind in new[] { Onikiri.Progression.QuestKind.Daily,
                                         Onikiri.Progression.QuestKind.Repeat,
                                         Onikiri.Progression.QuestKind.Achievement })
                foreach (var quest in Onikiri.Progression.QuestCatalog.Of(kind))
                    yield return quest.Title;

            // 장비 이름도 코드(EquipmentCatalog)에 있다. 슬롯 이름 둘과 등급
            // 이름 열이고, **등급 이름은 화면의 제목 자리에 뜬다** - 빠지면
            // "오니키리"가 ㅁㅁㅁㅁ이 된다. 오의·퀘스트와 같은 규칙이다
            foreach (var slot in Onikiri.Progression.EquipmentCatalog.Slots)
            {
                yield return slot.SlotName;
                foreach (var grade in slot.GradeNames) yield return grade;
            }

            // 펫 이름·역할도 코드(PetCatalog)에 있다. 카드와 전투 화면에 선다
            foreach (var pet in Onikiri.Progression.PetCatalog.Pets)
            {
                yield return pet.Name;
                yield return pet.Role;
            }

            // 요도 이름도 코드(YodoCatalog)에 있다 (44단계). 혼·요도·대요괴
            // 이름 셋이 전부 화면에 선다 - 도감의 잠긴 줄이 "○○ 처치 시
            // 해금"으로 보스 이름을 적으므로, 보스 애셋에서 오는 사본이 아니라
            // 이쪽도 걷어야 한다(둘이 같은 글자라 아틀라스는 안 커진다)
            foreach (var blade in Onikiri.Progression.YodoCatalog.Blades)
            {
                yield return blade.SoulName;
                yield return blade.BladeName;
                yield return blade.BossName;

                // 영체 이름(45단계). 소환 순간에 이름 플래시로 화면 한가운데
                // 뜨므로, 빠지면 정확히 그 연출이 ㅁㅁㅁ이 된다
                yield return blade.SpiritName;
            }
            yield return Onikiri.Progression.YodoCatalog.OnikiriName;

            // 전직 티어 이름도 코드(EvolutionCatalog)에 있다 (33단계). 티어
            // 이름은 진화 카드의 제목 자리에 뜬다 - "진 데몬사무라이"가 ㅁ으로
            // 깨지는 화면은 정확히 축하해야 할 순간에 나온다
            yield return Onikiri.Progression.EvolutionCatalog.BaseName;
            foreach (var tier in Onikiri.Progression.EvolutionCatalog.Tiers)
                yield return tier.Name;

            foreach (var guid in AssetDatabase.FindAssets("t:BossConfig", DataFolders))
            {
                var config = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.BossConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (config != null) yield return config.displayName;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", DataFolders))
            {
                var definition = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition != null) yield return definition.displayName;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:RegionConfig", DataFolders))
            {
                var region = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.RegionConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (region != null) yield return region.displayName;
            }
        }

        private static readonly string[] DataFolders = { "Assets/_Project" };

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
