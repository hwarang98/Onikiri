using System;
using System.IO;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 마지막 Load가 어떻게 끝났는가. 인트로 타이틀이 읽는다.
     *
     * 그 전까지는 손상된 세이브가 **조용히** 새 게임이 됐다(로그 한 줄뿐).
     * 출시 앱에서 그것은 "진행이 사라졌는데 아무도 말해주지 않았다"가 된다 -
     * 타이틀 화면이 생기면서 처음으로 이것을 사람에게 적을 자리가 생겼다.
     */
    public enum SaveLoadOutcome
    {
        /** 파일이 없다. 첫 실행의 정상 경로 */
        NoFile = 0,

        /** 그대로 읽혔다 (마이그레이션 포함) */
        Loaded,

        /** 못 읽었다 (빈 파일·깨진 JSON). 원본은 백업했다 */
        Corrupt,

        /** 이 빌드보다 새 버전. 다운그레이드 - 원본은 백업했다 */
        FutureVersion,
    }

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

        /**
         * @brief 마지막 Load의 결과. Load를 아직 안 불렀으면 NoFile.
         *
         * 세이브 데이터가 아니라 **이번 실행의 관측**이라 static 필드다.
         * 세이브에 적으면 "손상됐었다"가 영구 기록이 되는데, 그 사실은
         * 다음 정상 저장이 오면 더는 참이 아니다.
         */
        public static SaveLoadOutcome LastOutcome { get; private set; } = SaveLoadOutcome.NoFile;

        /** 읽지 못한 원본을 치워 두는 곳. 문의가 오면 이 파일이 증거다 */
        public static string BackupPath
        {
            get { return Path + ".broken"; }
        }

        /**
         * @brief 못 읽는 원본을 옆에 치워 둔다. **다음 Save가 덮어쓰기 전에.**
         *
         * 새 게임으로 시작한 첫 자동 저장(30초)이 원본을 지운다 - 백업이
         * 없으면 "읽지 못했다"가 30초 뒤 "복구할 수도 없다"로 굳는다.
         * 복사 실패는 삼킨다. 백업은 최선의 노력이지 게이트가 아니다.
         */
        private static void BackUpBrokenFile()
        {
            try
            {
                if (File.Exists(Path)) File.Copy(Path, BackupPath, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Onikiri] Could not back up the broken save: " + exception.Message);
            }
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
            if (!File.Exists(Path))
            {
                LastOutcome = SaveLoadOutcome.NoFile;
                return SaveData.NewGame();
            }

            try
            {
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[Onikiri] Save file was empty; starting a new game.");
                    BackUpBrokenFile();
                    LastOutcome = SaveLoadOutcome.Corrupt;
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
                    BackUpBrokenFile();
                    LastOutcome = SaveLoadOutcome.FutureVersion;
                    return SaveData.NewGame();
                }

                if (loadedVersion != SaveData.CurrentVersion)
                    Debug.Log("[Onikiri] Migrated save v" + loadedVersion + " -> v" + SaveData.CurrentVersion + ".");

                LastOutcome = SaveLoadOutcome.Loaded;
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Onikiri] Save file unreadable, starting a new game: " + exception.Message);
                BackUpBrokenFile();
                LastOutcome = SaveLoadOutcome.Corrupt;
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
