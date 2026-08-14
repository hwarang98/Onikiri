using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Onikiri.EditorTools
{
    /**
     * @brief 폰으로 빌드하고 설치하고 실행하는 것을 메뉴 하나로 묶는다.
     *
     * Build Profiles 창으로도 되지만 그 길에는 매번 같은 걸림돌이 있다:
     *
     *   - 프로파일이 하나도 없으면 플랫폼 목록 자체가 안 뜬다
     *   - 무선 디버깅은 폰을 껐다 켜거나 무선 디버깅을 토글할 때마다 **포트가
     *     바뀐다.** 페어링은 한 번뿐이지만 연결은 매번 다시 해야 하고,
     *     안 붙어 있으면 Build And Run이 빌드를 다 하고 나서 실패한다
     *
     * 두 번째가 특히 나쁘다. 실패가 5분짜리 빌드 **뒤에** 오기 때문이다.
     * 그래서 여기서는 순서를 뒤집는다 - **기기부터 확인하고 빌드한다.**
     */
    public static class AndroidDeployBuilder
    {
        private const string ApkPath = "Builds/Android/ONIKIRI.apk";

        [MenuItem("Onikiri/Build/폰으로 빌드 + 설치", false, 100)]
        public static void BuildAndDeploy()
        {
            BuildAndDeploy(false);
        }

        /**
         * @brief 개발 빌드. 디버그 오버레이(CloudSpikeOverlay)가 여기에만 들어간다.
         *
         * 그 오버레이가 `#if DEVELOPMENT_BUILD`로 묶여 있어서, 일반 빌드로는
         * 화면에 아무 버튼도 없다 - Firebase 스파이크를 기기에서 눌러보려면
         * 반드시 이쪽으로 빌드해야 한다. 출시 빌드는 위의 일반 경로가 맞다.
         */
        [MenuItem("Onikiri/Build/폰으로 빌드 + 설치 (개발 빌드)", false, 102)]
        public static void BuildAndDeployDevelopment()
        {
            BuildAndDeploy(true);
        }

        private static void BuildAndDeploy(bool development)
        {
            string adb = FindAdb();
            if (adb == null)
            {
                Debug.LogError("[Onikiri] adb를 찾지 못했습니다. Android Build Support가 설치돼 있는지 확인하세요.");
                return;
            }

            // 기기부터. 빌드가 끝난 뒤에 연결 실패를 알게 되는 것이 가장 아깝다
            string device = EnsureDevice(adb);
            if (device == null)
            {
                EditorUtility.DisplayDialog("폰이 연결되지 않았습니다",
                    "무선 디버깅이 켜져 있는지 확인하세요.\n\n"
                    + "폰: 개발자 옵션 > 무선 디버깅 ON\n"
                    + "그래도 안 되면 무선 디버깅을 껐다 켜세요 - 포트가 바뀝니다.\n\n"
                    + "자세한 내용은 콘솔을 보세요.", "확인");
                return;
            }

            Debug.Log("[Onikiri] 기기 확인: " + device + " - "
                      + (development ? "개발 빌드를" : "빌드를") + " 시작합니다.");

            if (!BuildApk(development)) return;
            if (!Install(adb, device)) return;
            Launch(adb, device);
        }

        [MenuItem("Onikiri/Build/폰 연결만 확인", false, 101)]
        public static void CheckDeviceOnly()
        {
            string adb = FindAdb();
            if (adb == null) { Debug.LogError("[Onikiri] adb를 찾지 못했습니다."); return; }

            string device = EnsureDevice(adb);
            Debug.Log(device != null
                ? "[Onikiri] 폰 연결됨: " + device
                : "[Onikiri] 연결된 폰이 없습니다. 폰에서 무선 디버깅을 껐다 켜고 다시 시도하세요.");
        }

        // ---------------------------------------------------------------- 기기

        /**
         * @brief 붙어 있는 기기를 찾고, 없으면 mDNS로 찾아 연결까지 시도한다.
         *
         * 무선 디버깅의 포트는 고정이 아니다. 그래서 저장해둔 주소로 다시 붙는
         * 방법은 폰을 재부팅한 다음부터 안 통한다 - 매번 현재 포트를 물어봐야 한다.
         */
        private static string EnsureDevice(string adb)
        {
            string found = FirstOnlineDevice(adb);
            if (found != null) return found;

            Debug.Log("[Onikiri] 붙어 있는 기기가 없습니다. mDNS로 무선 디버깅을 찾는 중...");

            // adb-XXXX._adb-tls-connect._tcp   192.168.0.26:42533
            string services = Run(adb, "mdns services");
            var matches = Regex.Matches(services, @"(\d{1,3}(?:\.\d{1,3}){3}:\d+)");

            var tried = new HashSet<string>();
            foreach (Match m in matches)
            {
                string address = m.Groups[1].Value;
                if (!tried.Add(address)) continue;

                Debug.Log("[Onikiri] connect " + address);
                string result = Run(adb, "connect " + address);
                Debug.Log("[Onikiri]   " + result.Trim());

                string online = FirstOnlineDevice(adb);
                if (online != null) return online;
            }

            if (matches.Count == 0)
                Debug.LogWarning("[Onikiri] mDNS에 무선 디버깅이 안 보입니다. "
                    + "폰과 PC가 같은 네트워크에 있어야 하고, 폰에서 무선 디버깅이 켜져 있어야 합니다. "
                    + "처음이면 '무선 디버깅 > 페어링 코드로 기기 페어링'으로 한 번 페어링해야 합니다.");

            return null;
        }

        /** `adb devices`에서 상태가 정확히 device인 첫 항목. offline/unauthorized는 거른다 */
        private static string FirstOnlineDevice(string adb)
        {
            foreach (var line in Run(adb, "devices").Split('\n'))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("List of devices")) continue;

                var parts = text.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (parts[1] == "device") return parts[0];

                Debug.Log("[Onikiri] 기기 " + parts[0] + " 상태가 '" + parts[1] + "'라 건너뜁니다.");
            }
            return null;
        }

        // ---------------------------------------------------------------- 빌드

        private static bool BuildApk(bool development)
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled) scenes.Add(scene.path);

            if (scenes.Count == 0)
            {
                Debug.LogError("[Onikiri] 빌드에 포함된 씬이 없습니다. Build Profiles의 Scene List를 확인하세요.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // 개발 빌드는 Debug.Log가 logcat에 그대로 남고(스파이크의 증거가
                // 거기 있다) 디버그 오버레이가 컴파일에 포함된다
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Onikiri] 빌드 실패: " + summary.result
                               + " (오류 " + summary.totalErrors + "개)");
                return false;
            }

            Debug.Log(string.Format("[Onikiri] 빌드 성공: {0} ({1:F1}MB, {2:F0}초)",
                ApkPath, new FileInfo(ApkPath).Length / (1024f * 1024f),
                summary.totalTime.TotalSeconds));
            return true;
        }

        // ---------------------------------------------------------------- 설치

        private static bool Install(string adb, string device)
        {
            Debug.Log("[Onikiri] 설치 중... (무선이라 1~2분 걸릴 수 있습니다)");

            string result = Run(adb, "-s " + device + " install -r \""
                                    + Path.GetFullPath(ApkPath) + "\"", 600000);

            if (result.IndexOf("Success", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("[Onikiri] 설치 실패:\n" + result.Trim()
                    + "\n\n서명이 다르다는 오류면 폰에서 앱을 지우고 다시 시도하세요.");
                return false;
            }

            Debug.Log("[Onikiri] 설치 완료.");
            return true;
        }

        private static void Launch(string adb, string device)
        {
            string package = PlayerSettings.GetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android);

            Run(adb, "-s " + device + " shell monkey -p " + package
                     + " -c android.intent.category.LAUNCHER 1");

            Debug.Log("[Onikiri] 실행 요청: " + package + " - 폰 화면을 보세요.");
        }

        // ---------------------------------------------------------------- adb

        /**
         * @brief adb 실행 파일을 찾는다.
         *
         * Unity가 쓰는 SDK를 먼저 본다. 사람이 따로 깐 SDK와 Unity가 번들한 SDK가
         * 다를 수 있고, 그때 기준이 되어야 하는 것은 Unity 쪽이다 - 빌드를 하는
         * 주체가 Unity이기 때문이다.
         */
        private static string FindAdb()
        {
            var candidates = new List<string>();

            string sdk = EditorPrefs.GetString("AndroidSdkRoot");
            if (!string.IsNullOrEmpty(sdk)) candidates.Add(Path.Combine(sdk, "platform-tools/adb.exe"));

            string editor = Path.GetDirectoryName(EditorApplication.applicationPath);
            if (!string.IsNullOrEmpty(editor))
                candidates.Add(Path.Combine(editor,
                    "Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"));

            string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(local))
                candidates.Add(Path.Combine(local, "Android/Sdk/platform-tools/adb.exe"));

            foreach (var path in candidates)
                if (File.Exists(path)) return path;

            Debug.LogWarning("[Onikiri] adb 후보 경로에 파일이 없습니다:\n  "
                             + string.Join("\n  ", candidates.ToArray()));
            return null;
        }

        private static string Run(string exe, string arguments, int timeoutMs = 60000)
        {
            var info = new ProcessStartInfo(exe, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(timeoutMs);
                return output + error;
            }
        }
    }
}
