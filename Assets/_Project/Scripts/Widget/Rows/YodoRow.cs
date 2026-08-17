using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 요도 화면의 한 자루. 봉인·합성 버튼 하나를 준다.
     *
     * ## 버튼이 하나인 것이 EquipmentRow와의 차이다
     *
     * 장비 카드는 버튼이 둘이다 - 재화가 갈리기 때문이다(골드 / 보석+골드).
     * 요도는 안 갈린다: 봉인이든 합성이든 **혼 하나 + 파편**이고, 결과도
     * 같다(티어가 하나 오른다). 그래서 한 버튼이 상태에 따라 동사만 바꿔
     * 적는다 - "봉인" / "합성".
     *
     * ## 잠긴 화면에서 값을 감추지 않는다 - 여기만 규칙이 다르다
     *
     * UpgradeButton·SkillButton·EquipmentRow의 잠긴 줄은 값을 감춘다. 값을
     * 보여주면 이미 갖고 있는 것으로 읽히기 때문이다.
     *
     * 요도는 반대로 **보여준다.** 잠긴 이유가 "아직 못 산다"가 아니라
     * "아직 그 요괴를 다시 만나지 못했다"이고, 그 화면이 하는 일이 정확히
     * "저거 갖고 싶다"이기 때문이다(41단계 잠긴 미리보기의 목적). 감추면
     * 요도 탭은 빈 화면 넷이 된다.
     *
     * 대신 **누를 수 없다는 것은 판을 눌러서** 말한다 - PetRow·EquipmentRow가
     * 41b에 확정한 규칙(interactable=false만으로는 SpriteSwap 버튼의 판이
     * 밝게 남아 활성처럼 보인다)을 그대로 쓴다.
     */
    public sealed class YodoRow : MonoBehaviour
    {
        [SerializeField] private YodoSystem system;
        [SerializeField] private int bladeIndex;

        [Header("표시")]
        [SerializeField] private Image icon;
        [SerializeField] private Image rowBackground;

        [Tooltip("요도 이름. 미봉인이면 혼 이름. \"등롱도\" / \"등롱의 혼\"")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("\"봉인 티어 3 / 10\"")]
        [SerializeField] private TMP_Text tierLabel;

        [Tooltip("\"공격력 ×1.113 → ×1.152\"")]
        [SerializeField] private TMP_Text statLabel;

        [Tooltip("\"혼 1 · 파편 12 / 20\"")]
        [SerializeField] private TMP_Text costLabel;

        [Tooltip("\"귀참 ×1.21 · 영체 ×12.4\". 45단계의 상성 표기")]
        [SerializeField] private TMP_Text affinityLabel;

        [Header("봉인·합성")]
        [SerializeField] private Button forgeButton;
        [SerializeField] private Image forgeBackground;
        [SerializeField] private TMP_Text forgeTitle;
        [SerializeField] private TMP_Text forgeCost;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);

        [Tooltip("평상시 행 틴트. 빌더가 스킨에서 적어준다")]
        [SerializeField] private Color normalRowTint = Color.white;
        [SerializeField] private Color masteredRowTint = new Color(0.62f, 0.55f, 0.42f, 1f);
        [SerializeField] private Color lockedRowTint = new Color(0.34f, 0.36f, 0.55f, 1f);

        [Tooltip("아이콘의 평상시 틴트. 카탈로그의 IconTint를 빌더가 적어준다")]
        [SerializeField] private Color iconTint = Color.white;
        [SerializeField] private Color lockedIconTint = new Color(0.42f, 0.40f, 0.48f, 1f);

        [Tooltip("버튼 판의 평상시 틴트. 잠긴 화면에서 되살릴 값이라 들고 있는다")]
        [SerializeField] private Color forgeButtonTint = new Color(0.42f, 0.56f, 1.00f, 1f);

        private StageProgress stage;

        private void Start()
        {
            stage = Object.FindFirstObjectByType<StageProgress>();

            if (forgeButton != null) forgeButton.onClick.AddListener(OnForge);
            if (system == null) system = YodoSystem.Instance;
            if (system != null) system.Changed += Refresh;

            // 해금이 **스테이지**로 걸린다. 보스를 잡아 st41이 된 순간에도
            // 화면이 잠긴 채로 남지 않게 - EquipmentRow와 같은 이유다
            if (stage != null) stage.Changed += Refresh;

            Refresh();
        }

        /** 판이 꺼진 채로 저장된다. 켜질 때 다시 그린다 */
        private void OnEnable()
        {
            if (system == null) system = YodoSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (forgeButton != null) forgeButton.onClick.RemoveListener(OnForge);
            if (system != null) system.Changed -= Refresh;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void OnForge()
        {
            if (system != null) system.TryForge(bladeIndex);
        }

        private void Refresh()
        {
            if (system == null) return;

            var blade = system.GetBlade(bladeIndex);
            if (blade == null) return;

            bool unlocked = system.IsUnlocked;
            bool maxed = system.IsMaxed(bladeIndex);

            if (icon != null) icon.color = unlocked ? iconTint : lockedIconTint;
            if (rowBackground != null)
                rowBackground.color = !unlocked ? lockedRowTint
                                    : maxed ? masteredRowTint : normalRowTint;

            if (nameLabel != null)
            {
                // 봉인 전에는 칼이 아직 없다. 혼의 이름으로 부른다 - 화면이
                // 거짓말하지 않는 가장 싼 방법이고, 봉인의 순간이 이름이
                // 바뀌는 순간이 된다
                nameLabel.text = blade.Sealed ? blade.bladeName : blade.soulName;
                nameLabel.color = !unlocked ? lockedColor
                                : maxed ? masteredColor : affordableColor;
            }

            if (tierLabel != null)
            {
                // 혼격이 있으면 티어 옆에 별로 붙는다(47단계). 자기 줄을 안
                // 내주는 이유는 **혼격이 티어의 형용사**이기 때문이다 -
                // "봉인 5"가 칼의 깊이이고 별은 그 칼에 먹인 혼의 격이라,
                // 줄을 나누면 두 개의 진행 막대로 읽힌다
                tierLabel.text = blade.Sealed
                    ? "봉인 " + blade.tier + " / " + YodoCurve.MaxTier
                      + RaritySuffix(blade.rarity)
                    : "미봉인";
                tierLabel.color = unaffordableColor;
            }

            if (statLabel != null)
            {
                // 전후값은 DPS 줄에 적는다. 버튼에 적으면 44pt 두 줄이 버튼
                // 폭을 넘는다 - 41b가 동료 카드에서 확정한 문법이다
                string text = "공격력 " + Multiplier(system.AttackMultiplier);
                if (unlocked && !maxed)
                    text += " → " + Multiplier(system.NextMultiplierOf(bladeIndex));

                statLabel.text = text;
                statLabel.color = unaffordableColor;
            }

            if (costLabel != null)
            {
                costLabel.text = MaterialsText(blade, maxed);
                costLabel.color = unaffordableColor;
            }

            if (affinityLabel != null)
            {
                affinityLabel.text = AffinityText(blade);

                // **금색이다.** 이 줄만 다른 색인 이유는 다른 줄이 전부
                // "지금 얼마인가"인데 여기는 **이 자루가 무엇을 하는가**이기
                // 때문이다 - 넷을 구분하는 유일한 줄이고, 파편을 어디에
                // 몰지가 이 한 줄에서 결정된다
                affinityLabel.color = blade.Sealed && unlocked ? masteredColor : lockedColor;
            }

            DrawButton(blade, unlocked, maxed);
        }

        /**
         * @brief 이 자루가 강화하는 오의와 영체. **잠긴 미리보기 규칙(41b)을 따른다.**
         *
         * 봉인 전에는 값 대신 조건을 적는다. 요도 행의 다른 줄은 잠긴 채로도
         * 값을 보여주는데(머리 주석 - "저거 갖고 싶다"가 이 화면의 일),
         * 상성만 다른 이유는 **봉인 전에는 값이 존재하지 않기** 때문이다 -
         * 티어 0의 상성은 ×1.000이고 영체 배율은 0이라, 적으면 "이 자루는
         * 아무것도 강화하지 않는다"는 거짓말이 된다. 대신 무엇이 열리는지를
         * 적는다.
         *
         * **혼 이름은 여기 적지 않는다.** "일섬 강화 · 처형인의 혼 봉인 시
         * 해금"으로 적었다가 537px이 되어 504px 상자에서 끝이 잘렸다(실기
         * 캡처 - "…봉인 시 해"에서 끝났다). 그런데 그 혼 이름은 **바로 위
         * 이름 줄이 이미 적고 있다** - 봉인 전 nameLabel이 soulName이다.
         * 잘린 문장을 살리는 값이 아니라 한 행에 두 번 적힌 값이었다.
         */
        private string AffinityText(YodoSystem.Blade blade)
        {
            string skill = AffinitySkillName(bladeIndex);

            if (!blade.Sealed)
                return skill + " 강화 · 봉인 시 해금";

            // **혼격을 넘긴다.** 넘기지 않으면 이 줄이 상성·영체를 혼격 0의
            // 값으로 적고, 그 아래 데미지는 혼격이 걸린 값으로 나온다 -
            // 26단계가 MultiplierOf 하나로 못 박은 "패널과 데미지가 같은
            // 숫자"가 47단계에 다시 깨질 수 있었던 자리다
            double factor = YodoAffinityCurve.ValueAt(bladeIndex, blade.tier, blade.rarity);
            double spirit = YodoSpiritCurve.MultiplierAtTier(blade.tier, blade.rarity);

            return skill + " ×" + factor.ToString("F3")
                 + " · 영체 ×" + spirit.ToString("F1");
        }

        /**
         * @brief "이 보스를 잡아야 한다"를 버튼 두 줄로. **줄바꿈을 직접 넣는다.**
         *
         * 버튼 안에서 접히게 두면 TMP가 한글을 낱자 단위로 끊는다 - 실제로
         * "붉은눈 요괴 처치 필 / 요"가 나왔다. 서양 글자였다면 공백에서
         * 끊겼을 텐데, 한글은 어디서든 끊어도 되는 글자로 취급된다.
         *
         * 그래서 어디서 끊을지를 화면이 아니라 문장이 정한다: 위가 **누구를**,
         * 아래가 **무엇을**. 이름이 짧아 한 줄로도 들어가는 보스까지 두 줄이
         * 되지만, 넷이 같은 모양으로 서는 편이 낫다 - 이 줄을 훑는 눈이 찾는
         * 것은 문장이 아니라 보스 이름이다.
         *
         * 빌드 검사(YodoPanelBuilder.VerifyTextFits)가 이 함수를 그대로 불러
         * 재므로, 문장을 고치면 검사도 같은 문장을 잰다.
         */
        public static string BossBlockedText(string bossName)
        {
            return bossName + "\n처치 필요";
        }

        /**
         * @brief 티어 줄 뒤에 붙는 혼격 (47단계). 혼격 0이면 빈 문자열.
         *
         * 0에서 아무것도 안 붙는 것이 요점이다. ☆☆☆☆를 적으면 무과금의
         * 네 자루가 전부 "미완성"으로 보이는데, 혼격은 자연 출처가 없어
         * 그가 채울 수 없는 칸이다 - 채울 수 없는 빈 칸을 보여주는 것은
         * 안내가 아니라 압박이다(YodoRarityCurve.Stars 주석).
         *
         * 빌드 검사(YodoPanelBuilder.VerifyTextFits)가 이 함수를 그대로
         * 불러 최악 폭을 잰다.
         */
        public static string RaritySuffix(int rarity)
        {
            string stars = YodoRarityCurve.Stars(rarity);
            return stars.Length == 0 ? string.Empty : "  " + stars;
        }

        /** "귀참" 또는 "전 오의". 빈 id가 전 오의라는 규칙은 카탈로그가 정한다 */
        public static string AffinitySkillName(int bladeIndex)
        {
            if (bladeIndex < 0 || bladeIndex >= YodoCatalog.Count) return "오의";

            string id = YodoCatalog.Blades[bladeIndex].AffinitySkillId;
            if (string.IsNullOrEmpty(id)) return "전 오의";

            int index = SkillCatalog.IndexOf(id);
            return index >= 0 ? SkillCatalog.Skills[index].DisplayName : "오의";
        }

        /** "혼 1 / 1 · 파편 12 / 20" - 가진 것을 앞에, 드는 것을 뒤에 */
        private string MaterialsText(YodoSystem.Blade blade, bool maxed)
        {
            if (maxed)
            {
                // 상한 뒤에는 이 혼이 파편으로 바뀐다. 버려지는 드랍이 없다는
                // 것을 화면에도 적는다 - 안 적으면 대요괴를 잡고도 아무 일이
                // 없는 것처럼 보인다
                return "보유 혼 " + blade.souls
                     + " · 이후 혼은 파편 " + YodoCurve.ShardsPerOverflowSoul;
            }

            int shardCost = system.ShardCostOf(bladeIndex);
            string souls = "혼 " + blade.souls + " / " + YodoCurve.SoulsPerTier;
            if (shardCost <= 0) return souls + " · 파편 없음";

            return souls + " · 파편 " + system.Shards + " / " + shardCost;
        }

        private void DrawButton(YodoSystem.Blade blade, bool unlocked, bool maxed)
        {
            if (!unlocked)
            {
                SetButton("봉인", YodoCurve.UnlockStage + "스테이지부터", false, lockedColor);
                if (forgeBackground != null) forgeBackground.color = Dimmed(forgeButtonTint);
                return;
            }

            if (forgeBackground != null) forgeBackground.color = forgeButtonTint;

            if (maxed)
            {
                SetButton("합성", "MASTER", false, masteredColor);
                return;
            }

            string verb = blade.Sealed ? "합성" : "봉인";

            // 무엇이 막고 있는지 적는다. 버튼을 지우지 않는 것이 이 프로젝트의
            // 규칙이고("왜 안 눌리지"가 화면에 남으면 고장으로 읽힌다),
            // **혼과 파편을 구분해 적는 것**이 여기서 특히 중요하다 - 둘은
            // 구하는 방법이 완전히 다르다(대요괴 / 정예·보석)
            if (!system.HasSoulFor(bladeIndex))
            {
                SetButton(verb, BossBlockedText(blade.bossName), false, unaffordableColor);
                return;
            }

            int shardCost = system.ShardCostOf(bladeIndex);
            if (system.Shards < shardCost)
            {
                SetButton(verb, "파편 " + shardCost + " 필요", false, unaffordableColor);
                return;
            }

            SetButton(verb, shardCost > 0 ? "혼 1 · 파편 " + shardCost : "혼 1",
                      true, affordableColor);
        }

        private void SetButton(string titleText, string costText, bool interactable, Color color)
        {
            if (forgeTitle != null) { forgeTitle.text = titleText; forgeTitle.color = color; }
            if (forgeCost != null) { forgeCost.text = costText; forgeCost.color = color; }
            if (forgeButton != null) forgeButton.interactable = interactable;
        }

        /** 판 죽이기. PetRow·EquipmentRow와 같은 값 */
        private static Color Dimmed(Color c)
        {
            return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
        }

        /**
         * @brief 배수를 소수 세 자리로. **장비와 같은 자릿수다.**
         *
         * 티어 한 칸이 +3.5%라 두 자리로도 보이긴 하는데, 같은 화면(대장간)의
         * 장비 카드가 세 자리를 쓴다. 한 화면에서 자릿수가 갈리면 두 축의
         * 크기를 눈으로 비교할 수 없다.
         */
        private static string Multiplier(double value)
        {
            return "×" + value.ToString("F3");
        }
    }
}
