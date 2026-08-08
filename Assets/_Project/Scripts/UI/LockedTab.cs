using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 아직 없는 기능을 자리만 잡아 보여주는 탭.
     *
     * 12단계에서 스킬과 전직은 구현되지 않았다. 그런데도 탭을 세워두는 이유는,
     * 방치형에서 "앞으로 무엇이 열리는가"가 계속할 이유의 절반이기 때문이다.
     * 빈 화면에 강화 하나만 있으면 이 게임이 어디까지 가는지 알 수 없다.
     *
     * 대신 **거짓말은 하지 않는다.** 눌러도 아무 일이 없는 탭이 아니라 잠긴
     * 모양으로 서 있고, 필요한 레벨을 함께 적는다. 눌리는데 빈 화면이 나오는
     * 것보다 안 눌리는 편이 정직하다.
     *
     * 해금 레벨에 도달하면 잠금 표시만 풀린다 - 기능 자체는 다음 단계에서 붙는다.
     * 그때까지 열린 탭이 빈 화면을 보여주면 안 되므로, 실제 화면이 생기기 전까지
     * requiredLevel은 도달 불가능한 값이 아니라 **의도한 값**으로 두고 해금
     * 시점의 표시만 바뀐다.
     */
    public sealed class LockedTab : MonoBehaviour
    {
        [SerializeField] private string displayName = "스킬";
        [SerializeField] private int requiredLevel = 10;

        /**
         * @brief 스테이지 조건. 0이면 안 쓴다.
         *
         * 32단계에 생겼다. 다른 탭은 전부 캐릭터 레벨로 잠그는데 **장비만
         * 스테이지**다 - 대장간이 지역 1의 랜드마크이고, 화면에 서 있는 건물이
         * 열리는 조건은 "그 지역을 지나왔는가"여야 말이 된다
         * (EquipmentCurve.UnlockStage).
         *
         * 두 조건을 **또는**이 아니라 **그리고**로 묶는다. 어느 하나로만 열면
         * 탭에 적을 문구가 둘이 되고("Lv.10 또는 11스테이지"), 그것은 조건이
         * 아니라 수수께끼다. 실제로는 한 탭이 둘 중 하나만 쓴다.
         */
        [Tooltip("이 스테이지부터 열린다. 0이면 레벨 조건만 쓴다")]
        [SerializeField] private int requiredStage;

        /**
         * @brief 레벨은 넘겼는데 기능이 아직 없을 때 적을 문구. 비우면 안 쓴다.
         *
         * 위 주석은 "해금 레벨에 도달하면 잠금 표시만 풀린다"고 적어두고 그때
         * 화면이 이미 있을 것을 전제했다. 18단계에서 전직이 성장 패널의 탭이
         * 되면서 그 전제가 깨졌다 - 지금 세이브가 Lv.41이라 조건은 이미 넘겼는데
         * 전직 화면은 없다. 그 상태에서 예전 규칙대로 그리면 **밝게 열린 빈 판**이
         * 나오고, 그것은 "해금됐다"와 "고장났다"가 구분되지 않는 화면이다.
         *
         * 조건과 구현은 다른 사실이므로 따로 적는다. 조건을 넘겼어도 구현이
         * 없으면 열리지 않은 것으로 그린다 - 이 컴포넌트가 처음부터 지켜온
         * "거짓말은 하지 않는다"가 그쪽이다.
         */
        [Tooltip("레벨은 넘겼지만 기능이 아직 없을 때의 문구. 비우면 해금 시 이름만 남는다")]
        [SerializeField] private string pendingLabel = "";

        /**
         * @brief 열렸을 때 이 탭이 켜는 화면. 없으면 예전처럼 눌리지 않는다.
         *
         * 26단계에 생겼다. 그전까지 이 컴포넌트는 **눌리지 않는 것이 전부**였고,
         * 아래 Refresh의 마지막 줄이 "이 줄이 다음 단계에서 사라지는 것이 실제로
         * 구현됐다는 표시가 된다"고 적어뒀다. 스킬 화면이 생기면서 그 줄을 지우는
         * 대신 조건으로 바꿨다 - 전직은 아직 화면이 없어서 예전 규칙이 그대로
         * 필요하기 때문이다.
         *
         * 참조가 있으면 "구현됐다"의 증거가 된다. pendingLabel처럼 사람이 적는
         * 문구가 아니라 **실제로 켤 대상이 있는가**로 판정하므로, 화면을 만들지
         * 않고 잠금만 푸는 실수가 성립하지 않는다.
         */
        [Tooltip("열렸을 때 여는 화면. 비어 있으면 잠금 표시만 하고 눌리지 않는다")]
        [SerializeField] private GameObject screen;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Tooltip("탭 배경. 해금 여부에 따라 밝기만 바뀐다")]
        [SerializeField] private Image background;

        [Header("색")]
        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);
        [SerializeField] private Color unlockedColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        /**
         * @brief 배경 **틴트**. 최종 색이 아니다.
         *
         * 탭 배경은 9-슬라이스 판이고 Image.color는 그 위에 곱해진다. 여기에
         * 예전처럼 최종 색(#2A253C)을 적으면 이미 어두운 판이 한 번 더 눌려
         * 새까맣게 죽는다 - 실제로 그렇게 나왔다.
         *
         * 밝은 쪽 값을 넣어야 판의 테두리와 파인 모양이 남는다.
         */
        [Tooltip("배경 틴트. 최종 색이 아니라 판에 곱해지는 값이다")]
        [SerializeField] private Color lockedBackground = new Color(0.55f, 0.56f, 0.78f, 1f);
        [SerializeField] private Color unlockedBackground = new Color(0.78f, 0.80f, 1.00f, 1f);

        private CharacterLevel character;
        private StageProgress stage;

        public bool IsUnlocked
        {
            get
            {
                if (character == null || character.Level < requiredLevel) return false;
                if (requiredStage <= 0) return true;
                return stage != null && stage.Stage >= requiredStage;
            }
        }

        /** 잠긴 탭에 적을 조건. 둘 중 실제로 쓰는 쪽만 적는다 */
        private string Requirement
        {
            get
            {
                return requiredStage > 0
                    ? requiredStage + "스테이지"
                    : "Lv." + requiredLevel;
            }
        }

        /**
         * @brief 켜질 때마다 다시 그린다.
         *
         * 전직 자리표시가 꺼진 채로 씬에 저장되기 때문이다(18단계). 꺼진
         * 오브젝트의 Start는 처음 켜진 다음 프레임에 돌아서, 그 한 프레임 동안
         * 빌더가 적어둔 글자가 그대로 남는다. StatPointButton과 같은 처리다.
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

            // 스테이지 조건을 쓰는 탭(장비)은 보스를 잡는 순간 열려야 한다.
            // 레벨 이벤트만 듣고 있으면 다음 레벨업까지 잠긴 채로 남는다
            if (stage == null) stage = Object.FindFirstObjectByType<StageProgress>();
            if (stage != null) stage.Changed += Refresh;

            if (screen != null && button != null) button.onClick.AddListener(Toggle);

            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
            if (stage != null) stage.Changed -= Refresh;
            if (screen != null && button != null) button.onClick.RemoveListener(Toggle);
        }

        /**
         * @brief 화면을 열고 닫는다. 같은 버튼이 둘 다 한다.
         *
         * 닫기 버튼을 따로 두지 않는 이유는, 이 탭이 **화면의 이름표**이기
         * 때문이다. 탭을 다시 누르면 닫히는 것이 하단 탭바에서 가장 배울 것이
         * 없는 규칙이고, 별도의 X 버튼은 화면 안에 눌러야 할 것을 하나 더
         * 만든다.
         */
        private void Toggle()
        {
            if (screen == null || !IsUnlocked) return;
            screen.SetActive(!screen.activeSelf);
        }

        private void Refresh()
        {
            bool unlocked = IsUnlocked;

            // 조건을 넘겨도 기능이 없으면 열린 것이 아니다. 밝게 그리면 눌러볼
            // 이유를 만들어놓고 아무 일도 일어나지 않는다.
            //
            // 켤 화면이 있으면 문구가 있어도 pending이 아니다 - 화면의 존재가
            // 문구보다 강한 증거다. 그래야 화면을 붙이면서 pendingLabel을 지우는
            // 것을 잊어도 "밝은데 안 눌리는" 상태가 나오지 않는다
            bool pending = unlocked && screen == null && !string.IsNullOrEmpty(pendingLabel);
            bool available = unlocked && !pending;

            if (label != null)
            {
                // 잠겨 있을 때만 조건을 적는다. 열린 뒤에도 "Lv.10"이 남아 있으면
                // 그것이 조건인지 현재 상태인지 구분되지 않는다
                if (!unlocked) label.text = displayName + " " + Requirement;
                else label.text = pending ? pendingLabel : displayName;

                label.color = available ? unlockedColor : lockedColor;
            }

            if (background != null)
                background.color = available ? unlockedBackground : lockedBackground;

            // 켤 화면이 있을 때만 눌린다. 없으면 열려도 못 누르는 것이 예전
            // 규칙 그대로다 - 전직이 아직 그 상태다
            if (button != null) button.interactable = available && screen != null;

            // 잠긴 사이에 화면이 켜져 있을 수는 없다. 세이브를 지우거나 레벨이
            // 내려가는 경로(테스트 패널)에서 열린 화면이 그대로 남는다
            if (!available && screen != null && screen.activeSelf) screen.SetActive(false);
        }
    }
}
