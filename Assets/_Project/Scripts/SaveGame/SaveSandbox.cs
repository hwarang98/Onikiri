#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 테스트용 세이브 격리 (61단계 S5-0). **릴리스 빌드에는 존재하지 않는다.**
     *
     * ## 왜 백업/복원이 아니라 격리인가
     *
     * 60단계까지 PlayMode 검사는 실사용 세이브를 읽어 두고 덮어쓴 뒤 TearDown에서
     * 되돌렸다. 60단계 §9의 행 국면에서 그 TearDown이 못 돌아 실사용 세이브가
     * 테스트 세이브로 덮였다(백업으로 복원했다). 복원은 반창고다 - 검사가 죽거나
     * 에디터가 멈추면 안 붙는다. 이 클래스는 접촉 자체를 없앤다:
     *
     *     만들 때   실사용 파일들의 지문을 떠 두고, SaveSystem.Root를 임시
     *               폴더로 바꿔 끼운다. 세이브·sidecar·백업 전부 따라온다
     *     버릴 때   루트를 되돌리고, **실사용 파일이 바이트 그대로인지 검사한다.**
     *               달라졌으면 예외 - 격리에 구멍이 났다는 뜻이고, 그 회귀를
     *               조용히 넘기지 않는 것이 이 클래스의 두 번째 존재 이유다
     *
     * 계정 교체(61단계)는 세이브를 갈아끼우는 가장 위험한 경로라, 그 검사들이
     * 실사용 파일 근처에도 못 가게 하는 것이 이 스텝의 첫 일이었다.
     */
    public sealed class SaveSandbox : IDisposable
    {
        private readonly string realRoot;
        private readonly string sandboxRoot;

        private readonly string[] watchedPaths;
        private readonly byte[][] watchedBytes;

        private bool disposed;

        /** 격리된 임시 폴더. 검사가 경로를 직접 확인하고 싶을 때 읽는다 */
        public string Root
        {
            get { return sandboxRoot; }
        }

        public SaveSandbox()
        {
            realRoot = Application.persistentDataPath;

            // 실사용 파일 전부의 지문을 **루트를 바꾸기 전에** 떠 둔다
            watchedPaths = WatchList(realRoot);
            watchedBytes = new byte[watchedPaths.Length][];
            for (int i = 0; i < watchedPaths.Length; i++)
                watchedBytes[i] = File.Exists(watchedPaths[i])
                    ? File.ReadAllBytes(watchedPaths[i]) : null;

            sandboxRoot = Path.Combine(Application.temporaryCachePath,
                "onikiri_test_saves", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandboxRoot);

            SaveSystem.UseRootForTests(sandboxRoot);
        }

        /**
         * @brief 루트를 되돌리고 **실사용 파일이 그대로인지 검사한다.**
         *
         * 달라진 파일이 있으면 예외를 던진다 - TearDown에서 터지므로 그 검사는
         * 실패로 적힌다. "경로 격리가 뚫렸는데 검사는 전부 초록"이 이 예외가
         * 막는 상태다.
         */
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            SaveSystem.ResetRootForTests();

            string breached = null;
            for (int i = 0; i < watchedPaths.Length; i++)
            {
                byte[] now = File.Exists(watchedPaths[i])
                    ? File.ReadAllBytes(watchedPaths[i]) : null;

                if (!SameBytes(watchedBytes[i], now))
                {
                    breached = watchedPaths[i];
                    break;
                }
            }

            // 임시 폴더는 최선의 노력으로 지운다. 지우기 실패가 격리 실패는 아니다
            try
            {
                if (Directory.Exists(sandboxRoot)) Directory.Delete(sandboxRoot, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Onikiri] Could not delete the save sandbox: " + exception.Message);
            }

            if (breached != null)
                throw new InvalidOperationException(
                    "테스트가 실사용 세이브 파일을 건드렸다 (경로 격리 회귀): " + breached);
        }

        /** 실사용 루트에서 지켜보는 파일들. 세이브가 만드는 모든 곁 파일이 여기 있어야 한다 */
        private static string[] WatchList(string root)
        {
            string save = Path.Combine(root, SaveSystem.FileName);
            return new[]
            {
                save,
                save + ".tmp",
                save + ".broken",
                save + ".precloud.1",
                save + ".precloud.2",
                save + ".precloud.3",
                save + ".prerecover",
                Path.Combine(root, Cloud.CloudSaveSidecar.FileName),
                Path.Combine(root, Cloud.CloudSaveSidecar.FileName) + ".tmp"
            };
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;

            return true;
        }
    }
}
#endif
