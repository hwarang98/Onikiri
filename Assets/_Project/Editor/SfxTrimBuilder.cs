using System.IO;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 긴 원본 효과음에서 앞부분만 잘라낸 짧은 사본을 만든다.
     *
     * 팩의 사망음은 2.667초다. 처치가 잦은 방치형에서는 소리가 끝나기 전에 다음 처치가
     * 들어오므로, 보이스 상한 4개가 꼬리 부분으로 계속 채워지고 정작 새로 들어온
     * 처치음이 밀려난다. 실제 정보가 들어 있는 구간은 앞의 0.3초 남짓이고 나머지는
     * 잔향이다.
     *
     * 원본을 덮어쓰지 않고 별도 파일로 굽는다. 임포트 설정만으로는 길이를 바꿀 수 없고,
     * 서드파티 에셋을 손대면 팩을 다시 받는 순간 되돌아가기 때문이다.
     *
     * 생성물은 커밋한다. 원본이 없어도 빌드가 되어야 한다.
     */
    public static class SfxTrimBuilder
    {
        public const string OutputFolder = "Assets/_Project/Audio/Generated";

        /** 잘라낼 원본 하나 */
        private struct TrimJob
        {
            public string SourcePath;
            public string OutputName;
            public float Seconds;
        }

        private static readonly TrimJob[] Jobs =
        {
            new TrimJob {
                SourcePath = "Assets/Leohpaz/RPG_Essentials_Free/10_Battle_SFX/69_Enemy_death_01.wav",
                OutputName = "69_Enemy_death_01_short",
                Seconds = 0.35f
            }
        };

        /**
         * @brief 끝을 페이드아웃하는 길이 (초).
         *
         * 파형을 그냥 자르면 그 지점의 진폭이 0이 아니라서 스피커에 계단 하나가 그대로
         * 전달되고, 이것이 "탁" 하는 클릭으로 들린다. 40ms면 귀에 잘림이 느껴지지
         * 않으면서 파형을 0으로 데려가기에 충분하다.
         */
        private const float FadeOutSeconds = 0.04f;

        [MenuItem("Onikiri/Audio/Rebuild Trimmed SFX")]
        public static void Rebuild()
        {
            EnsureFolder(OutputFolder);

            foreach (var job in Jobs) Build(job);

            AssetDatabase.Refresh();
        }

        /** 이 잡의 결과물 경로. 배선 코드가 참조한다 */
        public static string OutputPath(string outputName)
        {
            return OutputFolder + "/" + outputName + ".wav";
        }

        public static string DeathShortPath
        {
            get { return OutputPath(Jobs[0].OutputName); }
        }

        private static void Build(TrimJob job)
        {
            var source = AssetDatabase.LoadAssetAtPath<AudioClip>(job.SourcePath);
            if (source == null)
            {
                Debug.LogWarning("[Onikiri] Trim source missing: " + job.SourcePath);
                return;
            }

            // GetData는 DecompressOnLoad 클립에서만 신뢰할 수 있다. 임포트 설정이
            // 아직 적용되기 전이라면 여기서 먼저 맞춰둔다
            EnsureReadable(job.SourcePath);
            source = AssetDatabase.LoadAssetAtPath<AudioClip>(job.SourcePath);

            var samples = new float[source.samples * source.channels];
            if (!source.GetData(samples, 0))
            {
                Debug.LogWarning("[Onikiri] Could not read samples from " + job.SourcePath);
                return;
            }

            int channels = Mathf.Max(1, source.channels);
            int frameCount = source.samples;
            int keepFrames = Mathf.Min(frameCount, Mathf.RoundToInt(job.Seconds * source.frequency));
            int fadeFrames = Mathf.Min(keepFrames, Mathf.RoundToInt(FadeOutSeconds * source.frequency));

            // 모노로 합친다. 재생은 spatialBlend 0이라 두 번째 채널은 어차피 버려지고,
            // 여기서 합쳐두면 임포터가 다시 합칠 일이 없다
            var mono = new float[keepFrames];
            for (int frame = 0; frame < keepFrames; frame++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += samples[frame * channels + c];
                mono[frame] = sum / channels;
            }

            for (int i = 0; i < fadeFrames; i++)
            {
                int index = keepFrames - fadeFrames + i;
                mono[index] *= 1f - (float)i / fadeFrames;
            }

            string path = OutputPath(job.OutputName);
            File.WriteAllBytes(path, EncodeWav16(mono, source.frequency));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log(string.Format(
                "[Onikiri] Trimmed '{0}': {1:F3}s -> {2:F3}s ({3}ms fade-out) -> {4}",
                source.name, source.length, keepFrames / (float)source.frequency,
                Mathf.RoundToInt(FadeOutSeconds * 1000f), path));
        }

        /** GetData가 성공하려면 클립이 압축 해제된 상태로 메모리에 있어야 한다 */
        private static void EnsureReadable(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.DecompressOnLoad) return;

            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        /**
         * @brief 모노 float 샘플을 16비트 PCM WAV로 인코딩한다.
         *
         * Unity에는 AudioClip을 파일로 쓰는 API가 없다. 헤더 44바이트 + 샘플이 전부라
         * 라이브러리를 들이는 것보다 직접 쓰는 편이 낫다.
         */
        private static byte[] EncodeWav16(float[] mono, int sampleRate)
        {
            const int channels = 1;
            const int bitsPerSample = 16;

            int dataBytes = mono.Length * sizeof(short);
            var stream = new MemoryStream(44 + dataBytes);
            var writer = new BinaryWriter(stream);

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                                   // PCM 포맷 청크 길이
            writer.Write((short)1);                             // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);   // 초당 바이트
            writer.Write((short)(channels * bitsPerSample / 8));       // 블록 정렬
            writer.Write((short)bitsPerSample);

            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < mono.Length; i++)
            {
                // 클리핑을 막기 위해 먼저 자른다. 32767을 넘긴 값이 감기면 가장 큰
                // 진폭 구간이 잡음으로 바뀐다
                float sample = Mathf.Clamp(mono[i], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(sample * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(folder.LastIndexOf('/') + 1));
        }
    }
}
