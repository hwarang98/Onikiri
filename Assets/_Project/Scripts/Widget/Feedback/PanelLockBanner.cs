using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 잠긴 화면의 미리보기 배너 (41단계).
     *
     * LockedTab이 잠긴 화면도 열어주게 되면서(미리보기), 화면 안에서 "왜
     * 버튼이 전부 죽어 있는가"를 말해줄 것이 필요해졌다. 행 단위 문구는
     * 화면마다 사정이 다르다 - 장비는 "대장간 미개방"을 적지만 동료는
     * "잠김"뿐이고, 스킬은 행 조건(Lv.10/15/20)만 적어서 화면 전체가 잠긴
     * 것과 행이 잠긴 것이 구분되지 않는다. 조건은 한 문장이므로 한 곳에
     * 한 번 적는다.
     *
     * 배너는 **헤더를 덮는다.** 자리를 따로 잡으면 해금 뒤에 그 자리가 빈
     * 채로 남거나 목록 전체를 밀어야 하는데, 잠긴 동안 헤더가 말하던 것
     * (제목·보석 잔고·자동 시전)은 어차피 쓸 수 없는 것들이다. 배너의
     * 이미지가 레이캐스트를 받아 아래 헤더의 조작(자동 시전 토글)도 함께
     * 막힌다.
     *
     * 컴포넌트는 배너 뿌리(빈 RectTransform)에 살고 **자식 판만 켜고 끈다.**
     * 자기 자신을 끄면 해금 이벤트를 받을 몸이 없어 다시 켤 수 없다 -
     * LockedTab이 탭에 남아 있는 것과 같은 구조다.
     *
     * 조건 판정은 LockedTab.IsUnlocked와 같은 식(레벨 그리고 최전선
     * 스테이지)이다. 문구는 빌더가 완성형으로 적는다 - LockedTab의
     * Requirement처럼 여기서 조립하면 같은 조건이 두 문장으로 갈릴 수 있다.
     */
    public sealed class PanelLockBanner : MonoBehaviour
    {
        [SerializeField] private int requiredLevel = 1;

        [Tooltip("이 스테이지(최전선)부터 열린다. 0이면 레벨 조건만 쓴다")]
        [SerializeField] private int requiredStage;

        [Tooltip("잠긴 동안 켜둘 판. 컴포넌트가 아니라 이것만 켜고 끈다")]
        [SerializeField] private GameObject visual;

        private CharacterLevel character;
        private StageProgress stage;

        private bool IsUnlocked
        {
            get
            {
                if (character == null || character.Level < requiredLevel) return false;
                if (requiredStage <= 0) return true;
                // 현재 스테이지가 아니라 최전선이다(37단계) - 재선택으로
                // 돌아간 순간 배너가 되살아나면 안 된다
                return stage != null && stage.MaxStageReached >= requiredStage;
            }
        }

        /**
         * @brief 켜질 때마다 다시 판정한다.
         *
         * 화면(패널)이 꺼진 채 씬에 저장되므로 Start는 처음 열린 다음
         * 프레임에 돈다. 그 한 프레임 동안 빌더가 켜둔 판이 그대로 보이는
         * 것은 맞는 방향이고(잠긴 세이브가 다수다), 해금된 세이브에서는
         * OnEnable이 첫 프레임에 끈다 - LockedTab과 같은 처리다.
         */
        private void OnEnable()
        {
            if (character == null) character = CharacterLevel.Instance;
            if (stage == null) stage = Object.FindFirstObjectByType<StageProgress>();
            Refresh();
        }

        private void Start()
        {
            if (character == null) character = CharacterLevel.Instance;
            if (character != null) character.Changed += Refresh;

            if (stage == null) stage = Object.FindFirstObjectByType<StageProgress>();
            if (stage != null) stage.Changed += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (visual == null) return;

            bool show = !IsUnlocked;
            if (visual.activeSelf != show) visual.SetActive(show);
        }
    }
}
