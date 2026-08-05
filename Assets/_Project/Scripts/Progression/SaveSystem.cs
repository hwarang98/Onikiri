using System;
using System.IO;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 세이브 파일 읽기/쓰기.
     *
     * 파일 하나에 JSON으로 담는다. 이 규모에서 PlayerPrefs를 쓸 이유는 없고(안드로이드는
     * XML 한 덩어리라 필드가 늘수록 통째로 다시 쓴다), 데이터베이스를 쓸 이유도 없다.
     *
     * 쓰기는 임시 파일에 먼저 하고 바꿔치기한다. 모바일은 저장 도중에 프로세스가
     * 그냥 사라질 수 있는데, 원본에 직접 쓰다가 중간에 끊기면 세이브가 반쪽짜리
     * JSON으로 남아 다음 실행에서 진행이 통째로 날아간다.
     */
    public static class SaveSystem
    {
        public const string FileName = "onikiri_save.json";

        public static string Path
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static bool Exists
        {
            get { return File.Exists(Path); }
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;

            try
            {
                data.version = SaveData.CurrentVersion;

                string json = JsonUtility.ToJson(data, true);
                string temp = Path + ".tmp";

                File.WriteAllText(temp, json);

                // File.Replace는 대상이 없으면 실패한다. 첫 저장에서는 그냥 옮긴다
                if (File.Exists(Path)) File.Replace(temp, Path, null);
                else File.Move(temp, Path);
            }
            catch (Exception exception)
            {
                // 저장 실패로 게임이 멈춰서는 안 된다. 다음 자동 저장에서 다시 시도한다
                Debug.LogError("[Onikiri] Save failed: " + exception.Message);
            }
        }

        /**
         * @brief 세이브를 읽는다. 없거나 손상됐으면 새 게임 데이터를 돌려준다.
         *
         * 손상된 파일에서 예외를 밖으로 던지지 않는다. 플레이어 입장에서 "진행이
         * 사라졌다"와 "게임이 켜지지 않는다"는 전혀 다른 문제다.
         */
        public static SaveData Load()
        {
            if (!File.Exists(Path)) return SaveData.NewGame();

            try
            {
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[Onikiri] Save file was empty; starting a new game.");
                    return SaveData.NewGame();
                }

                int loadedVersion = data.version;
                if (!SaveData.Migrate(data))
                {
                    // 여기 오는 경우는 사실상 하나다 - 이 빌드보다 새 버전의 세이브.
                    // 앱을 다운그레이드했거나 파일이 손상된 경우이고, 둘 다 읽으려
                    // 시도하면 엉뚱한 값으로 진행을 덮어쓴다
                    Debug.LogWarning("[Onikiri] Save version " + loadedVersion + " is newer than " +
                                     SaveData.CurrentVersion + "; starting a new game.");
                    return SaveData.NewGame();
                }

                if (loadedVersion != SaveData.CurrentVersion)
                    Debug.Log("[Onikiri] Migrated save v" + loadedVersion + " -> v" + SaveData.CurrentVersion + ".");

                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Onikiri] Save file unreadable, starting a new game: " + exception.Message);
                return SaveData.NewGame();
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Onikiri] Could not delete the save: " + exception.Message);
            }
        }
    }
}
