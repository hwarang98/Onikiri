#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 기기에서 Firebase 스파이크를 누를 버튼 하나. **개발 빌드 전용.**
     *
     * 스파이크의 존재 이유가 "안드로이드 실기에서 되는가"라서, 에디터 테스트
     * 패널만으로는 증명이 끝나지 않는다. 에디터에서 도는 Firestore는 데스크톱
     * 네이티브(FirebaseCppFirestore.dll)이고, 기기에서 도는 것은 Play 서비스
     * 위의 안드로이드 SDK다 - 실패 지점이 서로 다른 별개의 두 경로다.
     *
     * ## 파일 전체가 #if로 묶여 있다
     *
     * 출시 빌드에는 이 클래스가 **존재하지 않는다**. 화면에 떠 있는 디버그
     * 버튼은 사용자가 누를 수 있는 버튼이고, 눌러서 되는 일이 서버 쓰기라면
     * 그건 기능이다. 런타임 플래그로 숨기는 방식은 플래그가 뒤집히는 순간
     * 출시본에 노출되므로, 컴파일에서 지우는 쪽을 택했다.
     *
     * ⚠️ **출시 전 제거 대상**: 리더보드 UI가 서면 이 오버레이는 할 일이
     * 없어진다. 그때 파일째 지운다 - 스파이크 코드가 살아남아 본편 옆에
     * 나란히 놓이면 어느 쪽이 진짜 경로인지 다음 사람이 알 수 없다.
     *
     * ## IMGUI인 이유
     *
     * 씬을 건드리지 않기 때문이다. 프리팹이나 Canvas에 버튼을 얹으면 씬
     * 파일이 바뀌고, 그러면 이 스파이크가 "게임에 영향 0"이라고 말할 수
     * 없게 된다. OnGUI 한 장은 씬 밖에서 스스로 생겨 스스로 그린다.
     */
    public sealed class CloudSpikeOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("CloudSpikeOverlay");
            host.AddComponent<CloudSpikeOverlay>();
            DontDestroyOnLoad(host);
        }

        /** 접힌 상태로 시작한다. 펼친 채로 두면 게임 화면을 가린 스크린샷이 남는다 */
        private bool expanded;

        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;

        private void OnGUI()
        {
            EnsureStyles();

            float margin = Screen.width * 0.02f;
            float width = Screen.width * 0.46f;
            float lineHeight = Screen.height * 0.045f;

            // 안전 영역 안에 그린다. 노치 기기에서 상단에 그대로 붙이면
            // 버튼의 절반이 카메라 구멍 아래로 들어가 눌리지 않는다
            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax + margin;

            // App Check 절(62단계)이 붙어 두 줄 늘었다. 모자라면 마지막 줄이
            // 잘려서, 정작 읽어야 할 진단 결과가 화면 밖으로 나간다
            var area = new Rect(margin, top, width, lineHeight * (expanded ? 11.4f : 1f));
            GUILayout.BeginArea(area);

            if (GUILayout.Button(expanded ? "Firebase ▲" : "Firebase ▼",
                                 buttonStyle, GUILayout.Height(lineHeight)))
                expanded = !expanded;

            if (expanded)
            {
                using (new GUILayout.HorizontalScope())
                {
                    // 스파이크가 도는 동안 비활성화한다. 연타로 동시에 여러 벌이
                    // 돌면 write 순서가 뒤엉켜 왕복 값 비교가 의미를 잃는다
                    GUI.enabled = !CloudScores.IsBusy;
                    if (GUILayout.Button("초기화→로그인→write→read",
                                         buttonStyle, GUILayout.Height(lineHeight)))
                        CloudScores.RunSpike();
                    GUI.enabled = true;
                }

                using (new GUILayout.HorizontalScope())
                {
                    // 기기에서 wifi를 끄면 무선 디버깅도 함께 끊겨 관측이 불가능해진다.
                    // 이 두 버튼은 기기의 네트워크가 아니라 Firestore의 네트워크만
                    // 끄므로, 폰이 adb에 붙어 있는 채로 오프라인 경로를 밟을 수 있다
                    if (GUILayout.Button("오프라인", buttonStyle, GUILayout.Height(lineHeight)))
                        Forget(CloudScores.SetNetworkEnabledAsync(false));
                    if (GUILayout.Button("온라인", buttonStyle, GUILayout.Height(lineHeight)))
                        Forget(CloudScores.SetNetworkEnabledAsync(true));
                }

                // 계정 연동(55단계). 여기 두는 이유는 **재설치 검증의 손을
                // 줄이기 위해서**다 - 지웠다 깐 직후의 앱에서 랭킹 화면까지
                // 들어가려면 탭을 여러 번 밟아야 하고, 그 사이에 자동 제출이
                // 끼어들어 어느 쓰기가 무엇이었는지 흐려진다
                using (new GUILayout.HorizontalScope())
                {
                    GUI.enabled = !AccountLink.IsBusy
                                  && AccountLink.State == AccountState.Guest;
                    if (GUILayout.Button("구글 연동", buttonStyle, GUILayout.Height(lineHeight)))
                        Forget(AccountLink.LinkAsync(AuthProviders.Google));
                    GUI.enabled = true;
                }

                GUILayout.Label("도달층 " + CloudScores.CurrentReach()
                                + "   uid " + (CloudScores.Uid ?? "-"), labelStyle);

                // 상태 · 마지막 갈래를 함께 적는다. logcat 없이 화면만 봐도
                // Link였는지 Recover였는지 읽혀야 한다 - 그 둘이 이 스텝의 답이다
                GUILayout.Label("계정 " + AccountLink.State
                                + " / " + AccountLink.LastPlan
                                + (string.IsNullOrEmpty(AccountLink.LinkedLabel)
                                    ? string.Empty : " / " + AccountLink.LinkedLabel), labelStyle);

                GUILayout.Label(CloudScores.Status, labelStyle);
                if (!string.IsNullOrEmpty(AccountLink.Status))
                    GUILayout.Label(AccountLink.Status, labelStyle);

                // ---- App Check (62단계)
                //
                // 에디터 테스트 패널에만 진단을 두면 **실기에서 부를 방법이 없다** -
                // 그런데 Play Integrity가 도는 곳은 여기뿐이다. 데스크톱 Debug
                // provider가 통과하는 것은 실기 통과의 증거가 못 된다(스파이크를
                // 이 오버레이로 옮긴 것과 정확히 같은 이유다).
                //
                // 결과는 화면에 한 줄로 적고 **logcat에도 남긴다** - 폰 화면은
                // 좁고, 실측은 adb로 읽는 편이 정확하다.
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("App Check 진단", buttonStyle,
                                         GUILayout.Height(lineHeight)))
                        RunAppCheck();

                    // 응답 유실은 손으로 만들 수 없다 - 서버가 커밋한 **직후**
                    // 앱이 죽어야 생기는 상태라, seam 없이는 idempotency가
                    // 영영 안 밟힌다 (62단계 7절)
                    CloudSaveStore.DropNextCommitResponseForTests = GUILayout.Toggle(
                        CloudSaveStore.DropNextCommitResponseForTests,
                        "응답유실 1회", buttonStyle, GUILayout.Height(lineHeight));
                }

                GUILayout.Label(string.IsNullOrEmpty(appCheckLine)
                    ? "App Check " + FirebaseAppCheckBootstrap.Provider
                      + " / " + FirebaseAppCheckBootstrap.Setup
                    : appCheckLine, labelStyle);
            }

            GUILayout.EndArea();
        }

        /** 마지막 App Check 진단의 한 줄. 토큰 원문은 **여기 절대 안 온다** */
        private string appCheckLine = string.Empty;

        private bool appCheckBusy;

        /**
         * @brief provider → 토큰 강제 갱신 → 정본 읽기를 한 번에.
         *
         * `RunAsync`가 결과를 Debug.Log로도 남기므로 실측은 화면이 아니라
         * `adb logcat -s Unity | grep AppCheck` 로 읽으면 된다.
         */
        private void RunAppCheck()
        {
            if (appCheckBusy) return;
            appCheckBusy = true;
            appCheckLine = "App Check 진단 중...";

            AppCheckDiagnostics.RunAsync().ContinueWith(task =>
            {
                appCheckBusy = false;

                appCheckLine = task.IsFaulted
                    ? "App Check 진단 예외: " + FirebaseRuntime.Flatten(task.Exception)
                    : task.Result;
            });
        }

        /** 반환된 Task를 버리되 실패는 로그로 남긴다 (CloudScores.Observe와 같은 이유) */
        private static void Forget(System.Threading.Tasks.Task task)
        {
            task.ContinueWith(t => Debug.LogWarning("[CloudSpikeOverlay] " + t.Exception),
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        /**
         * @brief 폰 해상도에 맞춰 글자를 키운다.
         *
         * IMGUI 기본 글꼴은 에디터 화면(96dpi) 기준이라 1080x2400 폰에서는
         * 글자가 2mm쯤으로 찍힌다. 읽을 수 없는 상태 표시는 없는 것과 같다.
         */
        private void EnsureStyles()
        {
            if (buttonStyle != null) return;

            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.018f));

            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                wordWrap = true,
                // 배경이 밝은 지역(가을숲)에서 흰 글씨가 사라진다
                normal = { textColor = Color.yellow },
            };
        }
    }
}
#endif
