using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 보스 하나의 설계값. 인스펙터에서 편집한다.
     *
     * 12단계까지 보스 비주얼은 두 파일의 `private const`였다. 경로가 양쪽에
     * 중복돼 있어서 한쪽만 고치면 슬라이싱은 새 팩에 정의는 옛 팩에 붙었고,
     * 셀 크기와 발밑 여백은 팩마다 다른데 눈으로 세야 했다.
     *
     * **이 애셋은 빌더가 읽기만 한다.** 인스펙터에서 고친 값이
     * Build Combat Content로 날아가지 않는다는 뜻이고, 그것이 하드코딩을 걷어낸
     * 진짜 이유다. 빌더가 만들어내는 것(EnemyDefinition)은 따로 있고 그쪽은
     * 언제든 덮어써도 된다.
     *
     * 잡몹 정의(EnemyDefinition)와 같은 패턴이라 프로젝트에 새 개념이 아니다.
     */
    [CreateAssetMenu(fileName = "Boss_", menuName = "Onikiri/Boss Config")]
    public sealed class BossConfig : ScriptableObject
    {
        /**
         * @brief 보스가 어떻게 그려지는가.
         *
         * 두 유형을 한 애셋으로 다루는 이유는 스테이지 매핑이 둘을 구분할 필요가
         * 없기 때문이다. "이 스테이지의 보스"라는 슬롯 하나에 어느 쪽이든 들어가고,
         * 어떻게 그릴지는 이 애셋이 안다.
         */
        public enum ArtKind
        {
            /** 전용 스프라이트 시트 4장. 다크 사무라이가 이쪽 */
            Sheets,

            /** 잡몹 정의를 확대하고 물들인다. 일반 스테이지 보스와 엘리트가 이쪽 */
            ScaledMob
        }

        [Header("공통")]
        [Tooltip("화면에 그대로 뜬다. 새 이름을 쓰면 UIStrings.txt에도 넣어야 한다 - " +
                 "빠지면 □가 되고, VerifyWiring이 빌드에서 잡는다")]
        public string displayName = "보스";

        public ArtKind kind = ArtKind.ScaledMob;

        [Tooltip("등장 연출 전체(화면 암전 + 이름 + 걸어 들어오기)를 쓸지. " +
                 "매 스테이지 반복되면 연출은 무게가 아니라 대기 시간이 된다")]
        public bool fullIntro;

        /**
         * @brief 이 보스가 남기는 혼 (44단계). 비우면 아무것도 남기지 않는다.
         *
         * 값은 YodoCatalog의 Id다. **여기 적는 이유는 42단계의 세계 순환
         * 때문이다** - "st90의 보스는 등롱"이라는 사실은 로스터가 접어서 내는
         * 답이고, 드랍 쪽에서 스테이지로 다시 유도하면 같은 산수가 두 곳에
         * 산다. 지역을 하나 더하는 날 둘이 갈리고, 증상은 "등롱을 벴는데
         * 흑야의 혼이 나온다"다.
         *
         * 대요괴(지역 피날레) 넷만 채운다. 정예는 네 지역이 같은 애셋을
         * 공유하므로 고유한 혼을 적을 수 없고, 대신 파편을 남긴다 - 그
         * 판정은 스테이지 등급이 하므로 애셋에 적을 것이 없다
         * (YodoSystem.ReportBossDefeated).
         */
        [Tooltip("YodoCatalog의 Id. 지역 피날레(대요괴)만 채운다. 비우면 혼 없음")]
        public string soulId;

        // ------------------------------------------------------------ 시트형

        [Header("시트형 (kind = Sheets)")]
        public Texture2D idleSheet;

        [Tooltip("걸어 들어오는 동안 쓸 시트. 비우면 idle로 걷는다 - " +
                 "21단계까지 모든 보스가 그랬고, 크고 느린 보스에서 어색함이 드러났다")]
        public Texture2D walkSheet;

        public Texture2D hurtSheet;
        public Texture2D deathSheet;
        public Texture2D attackSheet;

        /**
         * @brief **추가** 공격 시트. 매 주기에 attackSheet와 함께 무작위로 고른다.
         *
         * 비워 두는 것이 기본이고, 비어 있으면 attackSheet 하나만 도는
         * 지금까지의 동작 그대로다. 대부분의 팩에는 공격 시트가 하나뿐이다.
         *
         * 다크 사무라이(Inimig 9)만 채운다. 그 팩의 오의 블록에는 서로 다른
         * 공격이 넷 들어 있어서(혈참·혈조·혈륜·혈파) 한 벌만 쓰면 아깝고,
         * 무엇보다 2초마다 같은 그림이 돌면 금방 벽지가 된다.
         *
         * 셀 크기는 attackSheet와 같아야 한다 - 같은 config의 셀 하나로 전부
         * 자르기 때문이다. 베이커가 한 번에 구우면 저절로 맞는다.
         */
        [Tooltip("추가 공격 시트. 매 주기에 attackSheet와 함께 무작위로 고른다. " +
                 "비우면 attackSheet 하나만 쓴다. 셀 크기는 같아야 한다")]
        public Texture2D[] attackSheetVariants;

        [Tooltip("시트 한 칸의 크기. 팩마다 다르다 - 모든 시트 폭의 최대공약수가 " +
                 "셀 폭이고, 한 줄 배치라 시트 높이가 곧 셀 높이다")]
        public int cellWidth = 128;
        public int cellHeight = 108;

        [Tooltip("프레임 아래쪽의 빈 픽셀 줄 수. 피벗을 여기에 고정해야 프레임마다 " +
                 "발밑이 흔들리지 않는다. [발밑 여백 자동 측정] 버튼이 채운다")]
        public int feetPadding = 12;

        [Tooltip("잡몹보다 느린 12fps. 보스는 사망 연출이 길어서 같은 속도로 돌리면 " +
                 "죽는 데 2초가 넘는다")]
        public float frameRate = 12f;

        [Tooltip("걸어 들어오는 속도. 무게가 속도로 읽힌다")]
        public float moveSpeed = 0.85f;

        /**
         * @brief 원본 시트가 왼쪽을 보고 그려졌는가.
         *
         * 적은 오른쪽에서 와서 왼쪽의 플레이어를 바라본다. 지금까지 쓴 팩이
         * 전부 오른쪽을 보고 그려져 있어 `Enemy`가 무조건 뒤집었는데, 처형인
         * 팩은 왼쪽을 보고 그려져 있어 **플레이어에게 등을 돌렸다.**
         */
        [Tooltip("시트가 왼쪽을 보고 그려졌으면 체크. 참이면 좌우를 뒤집지 않는다")]
        public bool artFacesLeft;

        [Tooltip("빌더가 시트에서 만들어 넣는다. 손으로 채우지 않는다")]
        public EnemyDefinition generatedDefinition;

        /**
         * @brief 이 보스가 휘두를 때 앞에 뜨는 이펙트 이름 (`VfxLibrary`의 id).
         *
         * 비워두면 `BossFight`의 기본값을 쓴다. 대부분의 보스가 비워둔 채로 두면
         * 되도록 그렇게 했다 - 참격이 뜨는 것이 기본이고, 이 칸은 "이 보스만
         * 다른 것을 뿜는다"를 적는 자리다.
         *
         * 라이브러리에 없는 이름을 적으면 아무것도 안 뜬다. 이펙트를 끄고 싶은
         * 보스가 있으면 그 성질을 이용해 `none`처럼 없는 이름을 적으면 된다.
         */
        [Tooltip("휘두를 때 앞에 뜰 이펙트 이름. 비우면 BossFight의 기본값을 쓴다")]
        public string attackVfxId;

        // ------------------------------------------------------------ 확대형

        [Header("확대형 (kind = ScaledMob)")]
        [Tooltip("확대할 잡몹. 비우면 그 스테이지의 잡몹이 자동으로 쓰인다")]
        public EnemyDefinition baseMob;

        /**
         * @brief 확대 배율. **정수만.**
         *
         * Pixel Perfect Camera가 아트 픽셀 하나를 화면 픽셀 N개로 늘린다.
         * 스프라이트를 s배로 키우면 아트 픽셀 하나가 화면에서 s x N 픽셀이 되는데,
         * 이 값이 정수가 아니면 어떤 픽셀은 7개 어떤 픽셀은 8개로 그려져 격자가
         * 눈에 띄게 일그러진다. 11단계에서 정한 규칙이고 VerifyWiring이 지킨다.
         */
        [Tooltip("정수만. 소수 배율은 픽셀 격자를 일그러뜨린다")]
        public int scale = 2;

        /**
         * @brief 확대판의 틴트.
         *
         * **밝은 값이어야 한다.** SpriteRenderer.color는 곱연산이라 어두운 틴트는
         * 스프라이트를 더 어둡게만 만든다. 처음에 #C87890을 썼다가 원래도 어두운
         * 요괴 아트가 검은 덩어리가 됐다.
         */
        public Color tint = new Color32(0xFF, 0xC0, 0xC8, 0xFF);

        // ------------------------------------------------------------ 배수

        /**
         * @brief 이 보스만의 추가 배수. 기본 1.
         *
         * 밸런스의 주인은 여기가 아니라 BossCurve의 등급별 배수다. 스테이지
         * 곡선이 전 구간의 여유 밴드를 책임지는데 개별 보스가 마음대로 곱하면
         * 그 밴드가 애셋마다 깨진다.
         *
         * 그래도 남겨두는 이유는 "이 한 놈만 유독 단단해야 한다" 같은 연출용
         * 예외가 실제로 생기기 때문이다. 지금 출하하는 두 애셋은 모두 1이고,
         * 1이 아닌 값이 들어오면 그때부터 시뮬레이션이 실제와 갈린다.
         */
        [Header("추가 배수 (연출용 예외. 기본 1)")]
        public double healthMultiplier = 1d;
        public double attackMultiplier = 1d;
        public double goldMultiplier = 1d;

        /** 실제로 스폰에 쓸 정의. 확대형이면 잡몹, 시트형이면 빌더가 만든 것 */
        public EnemyDefinition Definition
        {
            get { return kind == ArtKind.Sheets ? generatedDefinition : baseMob; }
        }

        /** 확대형만 확대한다. 시트형은 아트가 이미 보스 크기다 */
        public float SpawnScale
        {
            get { return kind == ArtKind.Sheets ? 1f : Mathf.Max(1, scale); }
        }

        public Color SpawnTint
        {
            get { return kind == ArtKind.Sheets ? Color.white : tint; }
        }

        public BigDouble ApplyHealth(BigDouble health)
        {
            return health * BigDouble.FromDouble(healthMultiplier);
        }

        public BigDouble ApplyGold(BigDouble gold)
        {
            return gold * BigDouble.FromDouble(goldMultiplier);
        }

        public double ApplyAttack(double damage)
        {
            return damage * attackMultiplier;
        }
    }
}
