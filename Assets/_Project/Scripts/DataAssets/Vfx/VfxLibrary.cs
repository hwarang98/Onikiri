using System;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 이름으로 꺼내 쓰는 이펙트 클립 모음.
     *
     * ## 왜 애셋인가
     *
     * 지금까지 이펙트 클립은 그것을 쓰는 컴포넌트 안에 살았다 - 팩 참격은
     * `SkillPerformer.Choreography.slashFrames`에, 타격 불꽃은 `PlayerCombat`에.
     * 쓰는 곳이 하나뿐일 때는 그 편이 짧다.
     *
     * 요괴에게서 뜯어낸 조각(YokaiVfxBaker)은 사정이 다르다. **뜯을 때부터
     * 쓸 곳이 하나가 아니었다** - 보스가 휘두를 때도 쓰고, 다음 단계의 오의도
     * 여기서 가져간다. 그때마다 다른 컴포넌트에 같은 배열을 하나씩 더 심으면
     * 스케일과 각도가 조금씩 달라지고, 같은 참격이 부르는 곳마다 다른 크기로
     * 뜬다.
     *
     * 그래서 클립과 **그 클립을 어떻게 놓는가**(배율·각도·거리)를 한 애셋에
     * 함께 둔다. 부르는 쪽은 이름만 안다.
     *
     * ## 배율은 정수만
     *
     * 11단계 픽셀 격자 규칙이다. Pixel Perfect 카메라가 아트 픽셀 하나를 화면
     * 픽셀 N개로 늘리는데, 여기에 소수 배율이 곱해지면 어떤 픽셀은 7개, 어떤
     * 픽셀은 8개로 그려져 격자가 눈에 띄게 일그러진다. PackSlash에 적어둔
     * 것과 같은 이유다.
     */
    [CreateAssetMenu(menuName = "Onikiri/VFX Library", fileName = "VfxLibrary")]
    public sealed class VfxLibrary : ScriptableObject
    {
        /**
         * @brief 이펙트 하나. 그림과 "어떻게 놓는가"를 함께 들고 있다.
         */
        [Serializable]
        public sealed class Clip
        {
            [Tooltip("부르는 쪽이 쓰는 이름. VfxLibrary 안에서 유일해야 한다")]
            public string id;

            [Tooltip("구운 시트에서 잘라낸 프레임들")]
            public Sprite[] frames;

            [Tooltip("재생 속도. 프레임이 넷뿐인 조각은 느리게 돌려야 눈에 남는다")]
            public float frameRate = 18f;

            [Tooltip("원본 픽셀 대비 배수. **정수만** - 위 주석의 픽셀 격자 규칙")]
            public float scale = 2f;

            [Tooltip("회전각 (도). 뜯어낸 원본 방향을 화면 방향으로 돌린다")]
            public float angle;

            [Tooltip("시전자 기준 앞쪽 거리 (월드 단위). 반전되면 반대쪽이 앞이다")]
            public float forwardOffset = 1f;

            [Tooltip("시전자 발밑 기준 높이 (월드 단위)")]
            public float heightOffset = 0.6f;

            /** 한 번 재생하는 데 걸리는 시간 (초). 0이면 재생할 것이 없다 */
            public float Seconds
            {
                get
                {
                    if (frames == null || frames.Length == 0 || frameRate <= 0f) return 0f;
                    return frames.Length / frameRate;
                }
            }
        }

        [SerializeField] private Clip[] clips;

        public int Count { get { return clips != null ? clips.Length : 0; } }

        public Clip At(int index)
        {
            if (clips == null || index < 0 || index >= clips.Length) return null;
            return clips[index];
        }

        /** 이름으로 찾는다. 없으면 null - 부르는 쪽이 조용히 넘어가도 되는 값이다 */
        public Clip Find(string id)
        {
            if (clips == null || string.IsNullOrEmpty(id)) return null;
            foreach (var clip in clips)
                if (clip != null && clip.id == id) return clip;
            return null;
        }
    }
}
