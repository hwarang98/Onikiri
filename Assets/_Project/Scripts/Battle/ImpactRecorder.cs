using System.Collections;
using System.IO;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 타격감을 검토하기 위한 개발 전용 프레임 그래버.
     *
     * 타격감은 약 70ms짜리 창 안에서 결정되는데, 수동 스크린샷으로는 잡을 수 없다.
     * 연속된 렌더 프레임을 디스크로 떨궈서 스윙·임팩트·히트스톱을 나중에 프레임
     * 단위로 들여다볼 수 있게 한다.
     */
    public sealed class ImpactRecorder : MonoBehaviour
    {
        [SerializeField] private int frameCount = 40;
        [SerializeField] private string outputFolder = "Captures";

        public bool IsRecording { get; private set; }
        public int FramesWritten { get; private set; }
        public int FreezeFrames { get; private set; }

        public void Record(int frames, string folder)
        {
            if (IsRecording) return;
            frameCount = frames;
            outputFolder = folder;
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            IsRecording = true;
            FramesWritten = 0;
            FreezeFrames = 0;

            // 에디터가 백그라운드에 있어도 플레이어 루프가 계속 돌게 한다
            Application.runInBackground = true;
            Directory.CreateDirectory(outputFolder);

            var camera = Camera.main;
            int width = Mathf.Max(1, camera.pixelWidth);
            int height = Mathf.Max(1, camera.pixelHeight);

            var renderTexture = new RenderTexture(width, height, 24);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);

            for (int i = 0; i < frameCount; i++)
            {
                yield return null;

                // end-of-frame을 기다리지 않고 직접 렌더한다. 포커스를 잃은 에디터는
                // Game 뷰 리페인트를 멈추고, 그러면 WaitForEndOfFrame이 영원히 반환되지
                // 않아 녹화가 아무것도 만들지 못한다
                var previousTarget = camera.targetTexture;
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.targetTexture = previousTarget;

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);
                RenderTexture.active = previousActive;

                // 정지된 프레임이 정작 보고 싶은 것이므로, 각 프레임을 찍을 때
                // 시간이 멈춰 있었는지를 파일명에 기록한다
                bool frozen = Time.timeScale == 0f;
                if (frozen) FreezeFrames++;

                string name = string.Format("f{0:D3}{1}.png", i, frozen ? "_freeze" : string.Empty);
                File.WriteAllBytes(Path.Combine(outputFolder, name), readback.EncodeToPNG());
                FramesWritten++;
            }

            RenderTexture.active = null;
            Destroy(readback);
            renderTexture.Release();
            Destroy(renderTexture);

            IsRecording = false;
            Debug.Log("[Onikiri] ImpactRecorder wrote " + FramesWritten + " frames ("
                      + FreezeFrames + " during hitstop) to " + outputFolder);
        }
    }
}
