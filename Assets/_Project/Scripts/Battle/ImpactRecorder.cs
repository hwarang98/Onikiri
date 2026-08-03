using System.Collections;
using System.IO;
using UnityEngine;

namespace Onikiri.Battle
{
    /// <summary>
    /// Development-only frame grabber used to review combat feel.
    ///
    /// Hit feel lives in a window about 70ms wide, which no manual screenshot can catch.
    /// This dumps consecutive rendered frames to disk so the swing, the impact and the
    /// hitstop can be inspected frame by frame afterwards.
    ///
    /// Waits on end-of-frame rather than a timer so it still records while a hitstop has
    /// timeScale pinned at zero - which is exactly the part worth looking at.
    /// </summary>
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

            // Keeps the player loop ticking while the editor sits in the background.
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

                // Render explicitly instead of waiting for end-of-frame: an unfocused
                // editor stops repainting the Game view, which leaves WaitForEndOfFrame
                // hanging forever and the recorder producing nothing.
                var previousTarget = camera.targetTexture;
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.targetTexture = previousTarget;

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);
                RenderTexture.active = previousActive;

                // Frozen frames are the interesting ones, so record whether time was
                // stopped when each frame was taken.
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
