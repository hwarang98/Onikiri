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
     *
     * 41단계에서 "안 눌리는 탭"이 "미리보기 탭"이 됐다. 화면이 실제로 있으면
     * 잠겨 있어도 들어가서 볼 수 있고, 잠기는 것은 화면 안의 액션(구매·장착·
     * 레벨업)뿐이다 - 각 시스템이 모델 층에서 다시 막으므로 UI가 뚫려도 돈이
     * 새지 않는다. 자물쇠 아이콘과 조건 라벨은 그대로다 - "볼 수는 있지만
     * 아직 못 쓴다"가 탭의 형태에서 읽혀야 한다.
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

        /**
         * @brief 탭 심볼 (38단계 아이콘화). 잠기면 자물쇠로 바뀐다.
         *
         * 잠금 문구("동료 31스테이지")를 탭 폭에 욱여넣던 방식을 대체한다 -
         * 5탭 폭(내부 192px)에서 그 문구는 구조적으로 넘쳤다. 상태는 심볼이
         * 말하고 글자는 조건 하나만 작게 남는다.
         */
        [Header("아이콘")]
        [SerializeField] private Image icon;
        [SerializeField] private Sprite normalIcon;
        [SerializeField] private Sprite lockedIcon;

        /**
         * @brief 홈 탭인가 (38단계 층위 분리 - "캐릭터" 탭).
         *
         * 홈 탭의 화면(GrowthPanel)은 밴드의 바탕층이라 **절대 꺼지지 않는다.**
         * 이 탭을 누르는 것은 자기 화면을 켜는 것이 아니라 **덮고 있는 다른
         * 화면들을 닫는 것**이다 - 바탕이 드러나는 것으로 "캐릭터 화면에 왔다"가
         * 성립한다.
         */
        [Tooltip("홈 탭. 자기 화면을 토글하지 않고 다른 화면들만 닫는다")]
        [SerializeField] private bool homeTab;

        /**
         * @brief 이 탭이 열릴 때 닫아야 할 다른 화면들 (38단계 상호 배타).
         *
         * 그전에는 탭들이 자기 화면만 토글해서 스킬+퀘스트가 동시에 열릴 수
         * 있었고, 형제 순서로 위의 것만 보였다 - 보이는 상태와 켜진 상태가
         * 달라지는 구조다. 층위를 나누면서 규칙으로 바꾼다: 같은 띠를 쓰는
         * 화면은 한 번에 하나다.
         */
        [SerializeField] private GameObject[] otherScreens;

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
                // 현재 스테이지가 아니라 최전선이다(37단계). 재선택으로 클리어한
                // 지역에 파밍하러 돌아간 순간 대장간이 다시 잠기면, 잠금 해제가
                // "지나왔는가"가 아니라 "지금 서 있는가"가 돼버린다
                return stage != null && stage.MaxStageReached >= requiredStage;
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

            if ((screen != null || homeTab) && button != null) button.onClick.AddListener(Toggle);

            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
            if (stage != null) stage.Changed -= Refresh;
            if ((screen != null || homeTab) && button != null) button.onClick.RemoveListener(Toggle);
        }

        /**
         * @brief 화면을 열고 닫는다. 같은 버튼이 둘 다 한다.
         *
         * 닫기 버튼을 따로 두지 않는 이유는, 이 탭이 **화면의 이름표**이기
         * 때문이다. 탭을 다시 누르면 닫히는 것이 하단 탭바에서 가장 배울 것이
         * 없는 규칙이고, 별도의 X 버튼은 화면 안에 눌러야 할 것을 하나 더
         * 만든다.
         *
         * **잠겨 있어도 화면이 있으면 연다** (41단계 미리보기). 잠긴 콘텐츠를
         * 들여다볼 수 있어야 "빨리 저기까지 가고 싶다"가 생긴다 - 벽이 아니라
         * 목표다. 안전한 이유는 상태 변경이 전부 모델 층에서 다시 막히기
         * 때문이다(SkillSystem.TryPurchase / EquipmentSystem.CanTemper /
         * PetSystem.CanUnlock 모두 해금 조건을 첫 줄에서 본다). 화면 안에는
         * 해금 조건 배너가 선다(PanelLockBanner).
         *
         * 열 때 같은 띠의 다른 화면들을 닫는다(상호 배타). 닫을 때는 아무것도
         * 열지 않는다 - 바탕(GrowthPanel)이 드러나는 것이 곧 홈이다.
         */
        private void Toggle()
        {
            if (homeTab)
            {
                CloseOthers();
                return;
            }

            if (screen == null) return;

            bool opening = !screen.activeSelf;
            CloseOthers();
            screen.SetActive(opening);
        }

        private void CloseOthers()
        {
            if (otherScreens == null) return;
            foreach (var other in otherScreens)
                if (other != null && other.activeSelf) other.SetActive(false);
        }

        /**
         * @brief 잠금 상태에 따라 심볼을 가운데로 옮겼다 되돌린다 (#10).
         *
         * 열린 탭은 아이콘 위 · 글자 아래의 2층 구성이고(빌더가 잡아둔 자리),
         * 잠긴 탭은 글자가 없으니 심볼 하나가 가운데 서는 것이 맞다.
         *
         * 원래 자리는 **처음 한 번만** 기억한다. 매번 현재 값을 기준으로
         * 옮기면 잠금/해금이 오갈 때마다 조금씩 밀린다.
         */
        private void CenterIconWhileLocked(bool locked)
        {
            var rect = icon != null ? icon.rectTransform : null;
            if (rect == null) return;

            if (normalIconAnchor == null)
            {
                normalIconAnchor = rect.anchorMin;
                normalIconPivot = rect.pivot;
                normalIconPosition = rect.anchoredPosition;
            }

            if (locked)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = normalIconAnchor.Value;
                rect.pivot = normalIconPivot;
                rect.anchoredPosition = normalIconPosition;
            }
        }

        private Vector2? normalIconAnchor;
        private Vector2 normalIconPivot;
        private Vector2 normalIconPosition;

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
                /**
                 * @brief 잠긴 탭은 **자물쇠뿐이다** (#10).
                 *
                 * 38단계에 "이름 + 조건"에서 "조건만"으로 줄였고, 이제 그 조건도
                 * 지운다. 조건 문구가 바깥에 필요 없는 이유는 **안에 들어가면
                 * 어차피 나오기 때문이다** - 잠긴 화면은 미리보기로 열리고
                 * (41단계), 그 안에 해금 조건 배너가 선다(PanelLockBanner).
                 *
                 * 같은 사실을 두 곳에 적으면 둘이 갈라진다. 실제로 갈라져 있었다:
                 * 탭은 "11스테이지", 배너는 그 지역 이름까지 적은 문장이다.
                 * 바깥은 **잠겼다**만 말하고, 무엇이 필요한지는 안에서 한 번만
                 * 말한다.
                 *
                 * 조건 자체는 Requirement에 그대로 남는다 - 지운 것은 표시이지
                 * 사실이 아니고, 잠긴 탭 다섯이 나란히 선 하단 바는 글자가
                 * 빠질수록 읽힌다.
                 */
                if (!unlocked) label.text = string.Empty;
                else label.text = pending ? pendingLabel : displayName;

                label.color = available ? unlockedColor : lockedColor;
            }

            if (icon != null)
            {
                var sprite = unlocked ? normalIcon : (lockedIcon != null ? lockedIcon : normalIcon);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = available ? unlockedColor : lockedColor;

                // 글자가 빠졌으므로 자물쇠는 탭 한가운데 선다 (#10). 위쪽에
                // 그대로 두면 아래 절반이 빈 채로 남아 탭이 잘린 것처럼 읽힌다
                CenterIconWhileLocked(!unlocked);
            }

            if (background != null)
                background.color = available ? unlockedBackground : lockedBackground;

            // 켤 화면이 있으면 잠겨 있어도 눌린다(41단계 미리보기). 잠금 표시는
            // 자물쇠와 색이 계속 말하고, 못 하는 것은 화면 안의 액션뿐이다.
            // 화면이 없으면(pending 포함) 예전 규칙 그대로 안 눌린다 - 눌리는데
            // 빈 화면이 나오는 것보다 안 눌리는 편이 정직하다.
            // 홈 탭은 화면 대신 "다른 화면 닫기"가 동작이다
            if (button != null) button.interactable = screen != null || homeTab;

            // 잠긴 채 열려 있는 화면을 여기서 닫지 않는다 - 미리보기가 그
            // 상태다. 예전에는 닫았는데, 레벨 이벤트마다 Refresh가 돌아서
            // 열어둔 미리보기를 다음 프레임에 도로 닫아버린다
        }
    }
}
