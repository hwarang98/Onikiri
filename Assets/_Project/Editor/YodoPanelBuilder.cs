using Onikiri.Progression;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.EditorTools
{
    /**
     * @brief 대장간의 요도 페이지와 도감 페이지, 그리고 YodoSystem을 세운다.
     *
     * ## 왜 EquipmentPanelBuilder에 넣지 않았는가
     *
     * 판(패널)과 헤더와 탭은 저쪽이 만든다 - 대장간 화면의 주인이 하나여야
     * 하기 때문이고, 34단계에 씬 빌더 둘이 같은 뿌리를 만들다 서로의 결과를
     * 지운 일이 있다. 여기는 그 판이 만들어 준 **스크롤 콘텐츠 안에 페이지
     * 둘을 얹기만** 한다.
     *
     * 나눈 이유는 크기다. EquipmentPanelBuilder는 이미 500줄이 넘고, 요도
     * 페이지 둘을 넣으면 900줄이 된다. 한 파일이 "대장간 화면 전체"를
     * 뜻하게 되면 카드 한 줄을 고칠 때마다 도감을 읽어야 한다.
     *
     * ## 두 페이지의 성격이 다르다
     *
     * 요도 페이지는 **누르는 화면**이다 - 자루마다 봉인/합성 버튼이 있고,
     * 맨 위에 파편 지갑과 보석 촉매가 선다.
     *
     * 도감은 **읽는 화면**이다. 버튼이 하나도 없다 - 41단계의 잠긴 미리보기가
     * "진입은 허용하고 액션만 잠근다"였다면, 여기는 애초에 액션이 없는 화면이고
     * 잠긴 줄은 실루엣과 조건 문구로만 말한다.
     */
    public static class YodoPanelBuilder
    {
        /** 파편 지갑 + 보석 촉매 한 줄 */
        private const float ShardBarHeight = 130f;

        /**
         * @brief 요도 한 자루의 행. 본문 **넉 줄**(좌) + 버튼(우).
         *
         * 45단계에 상성 줄이 붙으면서 170에서 자랐다. 44단계는 넉 줄을 170에
         * 우겨넣으려다 물렸고(재료 줄이 다음 행 위로 흘렀다) 그때의 처방은
         * "짧은 것을 오른쪽으로 올려 줄을 하나 없앤다"였는데, 이번에는 그
         * 처방을 두 번 쓸 수 없다 - 티어가 이미 이름 줄 오른쪽에 올라가 있고,
         * 상성은 짧지 않다("귀참 ×1.211 · 영체 ×12.4").
         *
         * 그래서 행을 키운다. 페이지가 1086px이 되지만 대장간은 44단계에
         * 이미 뷰포트 + 페이지 겹침 구조라(ForgePanelTabs가 탭마다 콘텐츠
         * 높이를 맞춘다) 늘어난 만큼 그냥 스크롤된다. VerifyRowsFit이 이
         * 값과 줄 수를 계속 대조한다.
         */
        private const float BladeRowHeight = 224f;

        /** 도감 한 줄. 버튼이 없어 두 줄이면 된다 */
        private const float CodexRowHeight = 130f;

        private const float RowGap = 12f;
        private const float LineHeight = 52f;

        private const float IconLeft = 24f;
        private static readonly float TextLeft = IconLeft + UiIcons.Size + 20f;

        /**
         * @brief 오른쪽 버튼의 폭.
         *
         * 300으로 뒀다가 물렸다(실기 캡처) - 막힌 이유를 적는 줄이 판 밖으로
         * 흘렀다. 캡션 실측으로 "다크 사무라이 처치 필요"가 358px인데 안쪽
         * 폭은 284px뿐이었다.
         *
         * 320이 늘릴 수 있는 끝이다. 본문 넉 줄이 왼쪽에서 484px을 쓰고
         * 그쪽 최악("보유 혼 999 · 이후 혼은 파편 40")이 460px이라, 버튼을
         * 더 넓히면 이번엔 본문이 잘린다. 그래서 나머지는 폭이 아니라 **줄
         * 수로** 받는다 - 긴 보스 이름은 두 줄로 접힌다(BuildSideButton).
         */
        private const float ButtonWidth = 320f;
        private const float ButtonRight = 24f;

        /** 버튼 판과 그 안 글자 사이 여백 */
        private const float ButtonTextPad = 8f;

        /** 글자가 실제로 쓸 수 있는 버튼 안쪽 폭. 실측 검사가 이 값과 대조한다 */
        private const float ButtonTextWidth = ButtonWidth - ButtonTextPad * 2f;

        /** 버튼이 행 위아래로 비우는 몫. 내용이 그만큼 안 들어가면 줄어든다 */
        private const float ButtonInset = 18f;

        /** 본문이 버튼을 침범하지 않도록 비워 둘 오른쪽 폭 */
        private static readonly float TextRight = ButtonRight + ButtonWidth + 16f;

        /**
         * @brief 행 하나의 폭. 대장간 뷰포트가 좌우 48px씩 먹고 남는 값이다
         * (EquipmentPanelBuilder.SidePadding). 실측 검사만 쓴다 - 실제 폭은
         * 페이지가 콘텐츠에 늘어붙어 정해진다.
         */
        private const float RowWidth = Onikiri.Core.DisplayConfig.DesignWidth - 48f * 2f;

        /** 본문 넉 줄이 쓸 수 있는 폭 */
        private const float TextWidth = RowWidth - (IconLeft + UiIcons.Size + 20f)
                                      - (ButtonRight + ButtonWidth + 16f);

        /** 요도 행의 봉인·합성 버튼은 두 줄까지 접는다. 파편 줄의 버튼은 한 줄 */
        private const int BladeCostLines = 2;
        private const int BarCostLines = 1;

        private static readonly Color TextColor = UiSkin.Text;
        private static readonly Color DimColor = UiSkin.TextDim;

        /** 봉인·합성 버튼은 보석 버튼과 같은 청이 아니다 - 아래 주석 참고 */
        private static readonly Color ForgeButtonTint = UiSkin.Chrome;

        /** 파편 조달만 보석 색이다. 화면에서 보석이 드는 유일한 자리 */
        private static readonly Color GemButtonTint = UiSkin.GemAction;

        public static float ForgePageHeight
        {
            get
            {
                return ShardBarHeight + RowGap
                     + YodoCatalog.Count * (BladeRowHeight + RowGap);
            }
        }

        /** 요도 넷 + 오니키리 한 줄 + 전설 둘 (47단계) */
        public static float CodexPageHeight
        {
            get
            {
                return (YodoCatalog.Count + 1 + LegendaryYodoCatalog.Count)
                     * (CodexRowHeight + RowGap);
            }
        }

        // ---------------------------------------------------------------- 시스템

        /**
         * @brief YodoSystem을 세우고 자루 값을 **카탈로그에서 옮겨 적는다.**
         *
         * EquipmentPanelBuilder.EnsureSystem과 같은 규칙이다 - 컴포넌트가 이미
         * 씬에 있으면 스크립트 기본값을 고쳐도 반영되지 않으므로, 빌더가 단일
         * 출처로서 씬에 명시적으로 기록한다.
         *
         * **혼·티어·발견은 덮어쓰지 않는다.** 여기서 0으로 되돌리면 빌더를 한
         * 번 돌릴 때마다 플레이 중인 세이브의 요도가 사라지고, 그것은 골드보다
         * 비싸다 - 혼은 한 바퀴(40스테이지)에 하나씩만 들어온다.
         */
        public static YodoSystem EnsureSystem(GameObject battle)
        {
            var system = battle.GetComponent<YodoSystem>();
            if (system == null) system = battle.AddComponent<YodoSystem>();

            var so = new SerializedObject(system);
            so.FindProperty("upgrades").objectReferenceValue =
                Object.FindFirstObjectByType<UpgradeSystem>(FindObjectsInactive.Include);
            so.FindProperty("gems").objectReferenceValue = battle.GetComponent<GemWallet>();
            so.FindProperty("stage").objectReferenceValue = battle.GetComponent<StageProgress>();

            var blades = so.FindProperty("blades");
            blades.arraySize = YodoCatalog.Count;

            for (int i = 0; i < YodoCatalog.Count; i++)
            {
                var spec = YodoCatalog.Blades[i];
                var element = blades.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = spec.Id;
                element.FindPropertyRelative("soulName").stringValue = spec.SoulName;
                element.FindPropertyRelative("bladeName").stringValue = spec.BladeName;
                element.FindPropertyRelative("bossName").stringValue = spec.BossName;

                // 진행(혼·티어·발견)은 건드리지 않는다. 위 주석 참고 - 새
                // 자루면 직렬화 기본값 0이 그대로 들어간다
                var souls = element.FindPropertyRelative("souls");
                if (souls.longValue < 0L) souls.longValue = 0L;
                var tier = element.FindPropertyRelative("tier");
                if (tier.intValue < 0) tier.intValue = 0;

                // 혼격도 진행이다 - 덮어쓰지 않는다(47단계). 200회에 한 번
                // 나오는 것을 빌더 한 번으로 지우면 골드보다 훨씬 비싸다
                var rarity = element.FindPropertyRelative("rarity");
                if (rarity.intValue < 0) rarity.intValue = 0;
            }

            // 전설 妖刀 (47단계). **사본 수는 덮어쓰지 않는다** - 위와 같은
            // 규칙이고, 이쪽은 자연 출처가 아예 없어 잃으면 되찾을 방법이
            // 뽑기뿐이다
            var legendaries = so.FindProperty("legendaries");
            legendaries.arraySize = LegendaryYodoCatalog.Count;

            for (int i = 0; i < LegendaryYodoCatalog.Count; i++)
            {
                var legend = LegendaryYodoCatalog.Blades[i];
                var element = legendaries.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("id").stringValue = legend.Id;
                element.FindPropertyRelative("bladeName").stringValue = legend.BladeName;

                var copies = element.FindPropertyRelative("copies");
                if (copies.intValue < 0) copies.intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // 세션이 세이브를 넘겨줘야 한다. 배선이 빠지면 요도가 매번 0에서
            // 시작하고, 증상은 "봉인했는데 껐다 켜면 사라진다"로 나온다 -
            // 한 바퀴를 다시 돌아야 하므로 장비 등급보다 손해가 크다
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null)
            {
                var sessionSo = new SerializedObject(session);
                sessionSo.FindProperty("yodo").objectReferenceValue = system;
                sessionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return system;
        }

        // ---------------------------------------------------------------- 영체

        /**
         * @brief 영체 소환체 하나를 Battle/Player 아래에 세운다 (45단계).
         *
         * ## 왜 하나인가
         *
         * 동료는 셋이 각자 전투체를 갖는다(다중 출전이라 넷째가 생기면 칸이
         * 하나 더 필요하다). 영체는 **동시 소환이 하나**라 자루가 넷이어도
         * 무대는 하나면 된다 - 로테이션이 그 무대에 다른 요괴를 올릴 뿐이다.
         * 아트를 미리 굽지 않는 것도 그래서다: 소환 순간에 로스터에서 꺼낸다
         * (SpiritSummon.FramesFor).
         *
         * 트랜스폼을 빌더가 강제하지 않는다. 자리는 SpiritSummon이 매 프레임
         * 로닌 기준으로 다시 놓으므로(summonOffset) 씬의 좌표에 뜻이 없다 -
         * 동료 포메이션이 표의 데이터인 것과 반대의 이유다.
         */
        public static Onikiri.Battle.SpiritSummon EnsureSpiritSummon(GameObject battle)
        {
            var samurai = GameObject.Find("Samurai");
            if (samurai == null) return null;

            var player = samurai.transform.parent != null ? samurai.transform.parent : samurai.transform;

            var existing = player.Find("SpiritSummon");
            var go = existing != null ? existing.gameObject : new GameObject("SpiritSummon");
            if (existing == null) go.transform.SetParent(player, false);

            // Mecanim이 붙어 있으면 SpriteAnimator를 조용히 덮어쓴다 - 동료
            // 전투체와 같은 사고이고 같은 처방이다
            Object.DestroyImmediate(go.GetComponent<Animator>(), true);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = Onikiri.Core.SortingOrders.Spirit;
            renderer.enabled = false;

            var animator = go.GetComponent<Onikiri.Core.SpriteAnimator>();
            if (animator == null) animator = go.AddComponent<Onikiri.Core.SpriteAnimator>();

            var summon = go.GetComponent<Onikiri.Battle.SpiritSummon>();
            if (summon == null) summon = go.AddComponent<Onikiri.Battle.SpiritSummon>();

            var bossFight = battle.GetComponent<Onikiri.Battle.BossFight>();

            var so = new SerializedObject(summon);
            so.FindProperty("combat").objectReferenceValue =
                samurai.GetComponent<Onikiri.Battle.PlayerCombat>();
            so.FindProperty("bossFight").objectReferenceValue = bossFight;
            so.FindProperty("spiritRenderer").objectReferenceValue = renderer;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("nameFlash").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.UI.SkillNameFlash>(FindObjectsInactive.Include);

            // 튜닝 값을 빌더가 명시한다. 컴포넌트가 이미 씬에 있으면 스크립트
            // 기본값을 고쳐도 반영되지 않으므로(동료 전투체·요도 자루와 같은
            // 규칙), 자리를 옮긴 것이 화면에 도달하려면 여기서 적어야 한다 -
            // 실제로 한 번 물렸다(영체가 판다 위에 겹쳐 있었다)
            so.FindProperty("summonOffset").vector2Value = new Vector2(-0.55f, 0.95f);
            so.FindProperty("riseSeconds").floatValue = 0.35f;
            so.FindProperty("fadeSeconds").floatValue = 0.5f;
            so.FindProperty("numberSizeMultiple").intValue = 2;
            so.FindProperty("spiritTint").colorValue = new Color(0.62f, 0.58f, 0.86f, 0.72f);
            so.FindProperty("numberTint").colorValue = new Color32(0xC9, 0xA8, 0xFF, 0xFF);

            // 로스터는 보스전이 이미 물고 있다. 같은 애셋을 두 곳에서 찾으면
            // 한쪽만 바뀌는 날 영체가 옛 요괴로 나온다 - 13단계의 단일 출처
            // 규칙이고, 여기서는 BossFight가 그 출처다
            if (bossFight != null)
            {
                var fightSo = new SerializedObject(bossFight);
                so.FindProperty("roster").objectReferenceValue =
                    fightSo.FindProperty("roster").objectReferenceValue;
            }

            // 45b 연출 배선. 참격·잔상 풀은 오의가, 불꽃 풀은 평타가 이미
            // 들고 있다 - 영체는 빌려 쓴다(SpiritSummon 머리 주석)
            so.FindProperty("performer").objectReferenceValue =
                samurai.GetComponent<Onikiri.Battle.SkillPerformer>();
            so.FindProperty("screenFlash").objectReferenceValue =
                Object.FindFirstObjectByType<Onikiri.UI.ScreenFlash>(FindObjectsInactive.Include);

            // 45c 쓸기 범위. 일섬의 관통(4.6)과 같은 값이다 - 한 화면에 두
            // 종류의 "앞"이 있으면 어느 쪽이 맞는지 눈으로 가릴 수 없다
            so.FindProperty("sweepRange").floatValue = 4.6f;
            so.FindProperty("sweepHeight").floatValue = 2.8f;

            WriteSignatures(so.FindProperty("signatures"));

            so.ApplyModifiedPropertiesWithoutUndo();
            return summon;
        }

        // ---------------------------------------------------------------- 영체 연출 (45b)

        /**
         * @brief 흑야 영체의 몸 크기. **최종 보스의 존재감이다.**
         *
         * 1.0이 대요괴 원본 크기인데, 영체는 로닌 어깨 뒤 위쪽에 서므로
         * 원본대로면 로닌과 비슷한 덩치가 된다. 넷 중 하나만 키워 "이건 급이
         * 다르다"를 말한다 - 나머지 셋은 1.0 그대로다.
         *
         * 1.25가 상한이었다. 1.4까지 올려보니 위쪽이 상단 바에 닿고 아래는
         * 동료 판다와 겹쳐, 45단계가 자리를 위로 올려 얻은 "누가 소환됐는지
         * 읽힌다"가 되돌아왔다.
         */
        private const float DarkSamuraiBodyScale = 1.25f;

        /**
         * @brief 흑야의 화면 번쩍 색. 먹빛에 붉은 기.
         *
         * 귀참(흰빛)과 갈라야 한다. 같은 ScreenFlash를 두 사건이 나눠 쓰는데
         * 색까지 같으면 "또 귀참이 나갔나"로 읽히고, 그러면 30초를 기다린
         * 사건이 7초짜리 사건과 구분되지 않는다.
         */
        private static readonly Color DarkSamuraiFlash = new Color(0.66f, 0.12f, 0.16f, 1f);

        /**
         * @brief 네 영체의 연출 프로필. **흑야가 정점이고 나머지는 그 아래로 벌어진다.**
         *
         * 넷을 똑같이 화려하게 만들지 않는 것이 요점이다. 전부 참격에 플래시에
         * 히트스톱을 주면 30초마다 같은 크기의 사건이 반복될 뿐이라, 어느
         * 자루를 봉인했는지가 화면에서 사라진다. 정점이 하나 있어야 나머지가
         * 그 정점을 가리킨다.
         *
         *   등롱    빛무리 하나. 큰 눈으로 비추는 요괴라 빛이 곧 그의 일이다
         *   처형인  단발 참격. 처형은 **한 번에 끝내는 일**이라 마지막 한 대만 벤다
         *   적안    소형 참격 다타. 광분이라 네 번 다 벤다. 대신 작고 가볍다
         *   흑야    ★ 강림(번쩍+불꽃+잔상) · 발도 참격 네 번 · 마지막에 무게
         *
         * 이 순서가 곧 카탈로그 순서이고 획득 순서다(YodoCatalog 머리 주석) -
         * 뒤로 갈수록 커지는 것이 여정의 모양과 같다.
         *
         * **밸런스는 여기 없다.** 배율·타수·쿨·지속은 YodoSpiritCurve가 정하고
         * 이 표는 그중 어느 것도 읽지 않는다.
         */
        private static void WriteSignatures(SerializedProperty array)
        {
            array.arraySize = YodoCatalog.Count;

            /**
             * @brief 흑야는 **참격 오버레이를 안 쓴다.** 몸 그림에 이미 있다.
             *
             * 46단계에는 이 자리가 팩의 제일 묵직한 시트(Slash3)였다. 몸은
             * 요괴인데 참격만 팩 것이라 서 있는 보스와 불러낸 영체가 화면에서
             * 서로 다른 놈으로 읽혔다. 한 번은 뜯어낸 초승달로 바꿔 봤지만,
             * 그것도 결국 **그림 위에 그림을 겹치는** 방식이라 크기와 자리를
             * 계속 손으로 맞춰야 했다.
             *
             * 지금은 겹칠 필요가 없다. 영체의 몸은 보스의 공격 클립을 그대로
             * 쓰는데(SpiritSummon.FramesFor가 로스터에서 꺼낸다), 그 클립이
             * 오의 블록에서 잘려 나오면서 **초승달이 프레임에 함께 그려져
             * 있다**(YokaiSheetBaker.Bakes).
             *
             * 그래서 오버레이를 뗀다. 보스가 공격할 때 나오는 그 그림이 영체의
             * 공격 그림이고, 둘이 어긋날 방법이 없다.
             */

            // 처형인·적안: 날카로운 갈고리 모양(Slash1)의 같은 흑+적. 굳이
            // 색을 나누지 않는 이유는 요도 넷이 한 팔레트 안에 있어야 하기
            // 때문이다 - 갈리는 것은 색이 아니라 **크기와 횟수**다
            var sharp = SkillPanelBuilder.SliceSlashSheet("Slash1", 2);

            /**
             * @brief 참격 높이는 **발밑에서** 잰다 (SpiritSummon.SpawnSlash).
             *
             * 옛 기준(스프라이트 칸 한가운데 + -0.1)이 요괴마다 아무 데나
             * 찍혀서 바꿨다. 아래 둘은 **화면에서 한 픽셀도 안 움직이도록**
             * 옛 자리를 그대로 계산해 옮겨 적은 값이다:
             *
             *   처형인  (92/2 - 16)/32 x 1.00 - 0.10 = 0.84
             *   붉은눈  (108/2 - 12)/32 x 1.00 - 0.10 = 1.21
             *
             * 등롱은 참격이 없어서(빛무리만) 값이 쓰이지 않는다.
             */
            WriteSignature(array, YodoCatalog.LanternId,
                           bodyScale: 1f,
                           slash: null, slashScale: 0f, slashAngle: 0f, slashHeight: 0f,
                           lastHitOnly: false,
                           flash: true, flashTint: new Color(1f, 0.93f, 0.72f, 1f),
                           spark: true, ghosts: 0,
                           hitStop: 0f, shake: 0.5f, perHitShake: 0.25f);

            WriteSignature(array, YodoCatalog.ExecutionerId,
                           bodyScale: 1f,
                           slash: sharp, slashScale: 2f, slashAngle: -70f, slashHeight: 0.84f,
                           lastHitOnly: true,
                           flash: false, flashTint: Color.white,
                           spark: false, ghosts: 0,
                           hitStop: 1.1f, shake: 1.0f, perHitShake: 0.3f);

            WriteSignature(array, YodoCatalog.RedEyeId,
                           bodyScale: 1f,
                           slash: sharp, slashScale: 1f, slashAngle: -25f, slashHeight: 1.21f,
                           lastHitOnly: false,
                           flash: false, flashTint: Color.white,
                           spark: false, ghosts: 0,
                           hitStop: 0.8f, shake: 0.9f, perHitShake: 0.45f);

            /**
             * ★ 쇼피스. 넷 중 유일하게 강림 셋을 다 켜고 몸도 크다.
             *
             * **배율과 각도만 보스 값으로 옮겼다(3 -> 1, -55도 -> 0도).**
             * 나머지(몸 크기·히트스톱·흔들림·잔상·번쩍·타수)는 46단계 그대로다.
             *
             * 배율을 그대로 둘 수 없었던 이유는 그림이 바뀌었기 때문이다.
             * 팩 참격은 64px 셀이라 3배가 6.00u인데, 초승달은 그려진 폭이
             * 114px이라 3배면 **10.69u** - 화면 폭(6.75u)의 1.6배다. 같은 숫자가
             * 같은 크기를 뜻하지 않는다.
             *
             * `slash: null`이라 배율·각도·높이는 읽히지 않는다. 강림 연출
             * (번쩍·불꽃·잔상 둘)과 무게(히트스톱 1.6)는 46단계 그대로다 -
             * 뗀 것은 겹쳐 그리던 참격 한 장뿐이다.
             */
            WriteSignature(array, YodoCatalog.DarkSamuraiId,
                           bodyScale: DarkSamuraiBodyScale,
                           slash: null, slashScale: 0f, slashAngle: 0f, slashHeight: 0f,
                           lastHitOnly: false,
                           flash: true, flashTint: DarkSamuraiFlash,
                           spark: true, ghosts: 2,
                           hitStop: 1.6f, shake: 1.6f, perHitShake: 0.5f);
        }

        /**
         * @brief 요괴 이펙트 라이브러리에서 클립 하나를 꺼낸다.
         *
         * 라이브러리가 아직 안 구워졌으면 여기서 굽는다. 없다고 조용히 null을
         * 돌려주면 흑야 시그니처의 참격 칸이 빈 채로 굳고, 그 상태는 영체를
         * 실제로 소환해 봐야 드러난다 - 요도 최상위 티어라 한참 뒤의 일이다.
         */
        private static Sprite[] LoadYokaiClip(string id)
        {
            var library = AssetDatabase.LoadAssetAtPath<Onikiri.Battle.VfxLibrary>(
                YokaiVfxBaker.LibraryPath);

            if (library == null)
            {
                if (YokaiVfxBaker.BakeAll()) library = YokaiVfxBaker.BuildLibrary();
            }

            var clip = library != null ? library.Find(id) : null;
            if (clip == null || clip.frames == null || clip.frames.Length == 0)
            {
                Debug.LogError("[Onikiri] Yokai VFX clip '" + id + "' missing - "
                               + "run Onikiri/Art/Harvest Yokai VFX.");
                return null;
            }
            return clip.frames;
        }

        /**
         * @brief 프로필 한 줄. 자리는 카탈로그 순서, 짝은 **id로** 맞춘다.
         *
         * 배열의 자리와 id를 둘 다 쓰는 것이 중복처럼 보이지만 하는 일이
         * 다르다 - 자리는 인스펙터에서 읽는 순서이고, id는 런타임이 실제로
         * 짝을 찾는 열쇠다(SpiritSummon.SignatureFor). 카탈로그가 재배열되면
         * 자리는 흔들려도 짝은 따라간다.
         */
        private static void WriteSignature(SerializedProperty array, string id, float bodyScale,
                                           Sprite[] slash, float slashScale, float slashAngle,
                                           float slashHeight,
                                           bool lastHitOnly, bool flash, Color flashTint,
                                           bool spark, int ghosts,
                                           float hitStop, float shake, float perHitShake)
        {
            var element = array.GetArrayElementAtIndex(YodoCatalog.IndexOf(id));

            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("bodyScale").floatValue = bodyScale;

            bool usesSlash = slash != null && slash.Length > 0;
            element.FindPropertyRelative("usesSlash").boolValue = usesSlash;

            var frames = element.FindPropertyRelative("slashFrames");
            frames.arraySize = usesSlash ? slash.Length : 0;
            for (int i = 0; i < frames.arraySize; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = slash[i];

            element.FindPropertyRelative("slashFrameRate").floatValue = 30f;
            element.FindPropertyRelative("slashScale").floatValue = slashScale;
            element.FindPropertyRelative("slashAngle").floatValue = slashAngle;

            // 영체 기준의 앞이다. 로닌 기준이 아니다 - 영체는 어깨 뒤 위쪽에
            // 따로 서 있어서, 로닌 값을 그대로 쓰면 참격이 몸에서 떨어진다
            element.FindPropertyRelative("slashForwardOffset").floatValue = 1.1f;
            element.FindPropertyRelative("slashHeightOffset").floatValue = slashHeight;
            element.FindPropertyRelative("slashOnLastHitOnly").boolValue = lastHitOnly;

            element.FindPropertyRelative("summonFlash").boolValue = flash;
            element.FindPropertyRelative("summonFlashTint").colorValue = flashTint;
            element.FindPropertyRelative("summonSpark").boolValue = spark;
            element.FindPropertyRelative("summonSparkOffset").vector2Value = new Vector2(0f, 0.35f);

            element.FindPropertyRelative("arrivalGhosts").intValue = ghosts;
            element.FindPropertyRelative("arrivalGhostSpan").floatValue = 0.9f;

            // 잔상은 요도 아이콘의 검푸른 날과 같은 색이다(YodoCatalog.IconTint)
            element.FindPropertyRelative("arrivalGhostTint").colorValue =
                new Color(0.42f, 0.36f, 0.62f, 0.5f);

            element.FindPropertyRelative("hitStopMultiplier").floatValue = hitStop;
            element.FindPropertyRelative("shakeMultiplier").floatValue = shake;
            element.FindPropertyRelative("perHitShakeMultiplier").floatValue = perHitShake;
        }

        // ---------------------------------------------------------------- 페이지

        /** 스크롤 콘텐츠 아래에 겹쳐 서는 페이지 하나. 세 탭이 같은 헬퍼를 쓴다 */
        public static RectTransform CreatePage(RectTransform content, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /**
         * @brief 행 안의 줄들이 행 높이 안에 들어가는지 빌드가 검산한다.
         *
         * 17~18단계에서 글자 폭 어림이 세 번 틀린 뒤로 이 프로젝트는 화면
         * 크기 주장을 빌드가 확인한다. 여기서 실제로 한 번 걸렸다 - 본문을
         * 넉 줄로 뒀다가 44px이 넘쳐 재료 줄이 다음 행 위로 흘렀다(실기
         * 캡처에서 발견). 그때 이 검산이 없었다.
         */
        private static void VerifyRowsFit()
        {
            float bladeContent = 12f + LineHeight * 4f;
            if (bladeContent > BladeRowHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Yodo row needs {0:F0}px but the row is {1:F0}px - "
                    + "the last line spills onto the next row.", bladeContent, BladeRowHeight));

            float codexContent = 14f + LineHeight * 2f;
            if (codexContent > CodexRowHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Codex row needs {0:F0}px but the row is {1:F0}px.",
                    codexContent, CodexRowHeight));

            float barContent = 14f + LineHeight * 2f;
            if (barContent > ShardBarHeight)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Shard bar needs {0:F0}px but the bar is {1:F0}px.",
                    barContent, ShardBarHeight));

            VerifyTextFits();
        }

        /**
         * @brief 최악 문자열을 **실제 폰트로 재서** 상자 폭과 대조한다.
         *
         * 줄 수만 세는 위쪽 검사가 못 잡는 것이 있다: 줄은 맞는데 그 한 줄이
         * 가로로 넘치는 경우다. 실기 캡처에서 정확히 그것에 물렸다 - 버튼의
         * 비용 줄("다크 사무라이 처치 필요")이 판 좌우로 삐져나가 있었고,
         * 본문의 상성 줄은 끝이 잘려 "…봉인 시 해"에서 끝나 있었다.
         *
         * 상단 바가 38단계부터 쓰던 방법(BattleContentBuilder.CheckLabelFits)을
         * 그대로 가져온다. 어림으로 폭을 정하지 않는다 - 17~18단계에 글자 폭
         * 어림이 세 번 틀린 뒤로 이 프로젝트의 화면 크기 주장은 빌드가 잰다.
         */
        private static void VerifyTextFits()
        {
            var caption = UiFonts.Caption;
            var primary = UiFonts.Primary;
            if (caption == null || primary == null) return;   // 폰트 검사가 따로 보고한다

            var probe = new GameObject("__YodoTextProbe", typeof(RectTransform));
            probe.hideFlags = HideFlags.HideAndDontSave;
            var text = probe.AddComponent<TMPro.TextMeshProUGUI>();

            try
            {
                // 버튼 제목(44pt). 한 줄이고 접히지 않는다 - 동사 한 낱말이다
                SetFont(text, primary, Onikiri.UI.PixelFontSizes.GalmuriSmall);
                foreach (var title in new[] { "봉인", "합성", "파편 조달" })
                    CheckLine(text, title, ButtonTextWidth, "button title");

                SetFont(text, caption, Onikiri.UI.PixelFontSizes.GalmuriCaption);

                // 파편 줄의 버튼은 한 줄뿐이라 접힐 곳이 없다
                CheckLine(text, "보석 " + YodoCurve.ShardPackGems
                                + " → 파편 " + YodoCurve.ShardPackShards,
                          ButtonTextWidth, "shard bar cost");
                CheckLine(text, YodoCurve.UnlockStage + "스테이지부터",
                          ButtonTextWidth, "shard bar cost");

                // 요도 행의 버튼은 두 줄까지 접는다. 접고도 넘치는지를 잰다
                int maxShards = YodoCurve.ShardCostAtTier(YodoCurve.MaxTier - 1);
                CheckWrapped(text, "혼 " + YodoCurve.SoulsPerTier + " · 파편 " + maxShards,
                             BladeCostLines, "blade cost");
                CheckWrapped(text, "파편 " + maxShards + " 필요", BladeCostLines, "blade cost");

                for (int i = 0; i < YodoCatalog.Count; i++)
                {
                    var spec = YodoCatalog.Blades[i];
                    CheckWrapped(text, Onikiri.UI.YodoRow.BossBlockedText(spec.BossName),
                                 BladeCostLines, "blade cost");

                    // 본문 넉 줄. 여기는 접히지 않는다 - 행 높이가 줄 수에
                    // 못 박혀 있어(BladeRowHeight) 한 줄이 둘이 되면 다음 행
                    // 위로 흐른다. 넘치면 문장을 줄이는 수밖에 없다
                    string skill = Onikiri.UI.YodoRow.AffinitySkillName(i);
                    CheckLine(text, skill + " 강화 · 봉인 시 해금", TextWidth, "affinity");
                    CheckLine(text, skill + " ×1.888 · 영체 ×888.8", TextWidth, "affinity");
                }

                CheckLine(text, "보유 혼 999 · 이후 혼은 파편 "
                                + YodoCurve.ShardsPerOverflowSoul, TextWidth, "materials");
                CheckLine(text, "공격력 ×1.888 → ×1.888", TextWidth, "stat");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        private static void SetFont(TMP_Text text, TMP_FontAsset font, float size)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = size;
        }

        /** 한 줄로 서야 하는 글자. 접을 곳이 없으니 폭 하나만 본다 */
        private static void CheckLine(TMP_Text probe, string worst, float boxWidth, string where)
        {
            probe.textWrappingMode = TextWrappingModes.NoWrap;
            float needed = probe.GetPreferredValues(worst).x;
            if (needed > boxWidth)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Yodo {0} overflows: \"{1}\" needs {2:F0}px but its box is "
                    + "{3:F0}px. Shorten the text or take the width from a neighbour - "
                    + "the atlas size cannot shrink.", where, worst, needed, boxWidth));
        }

        /** 접혀도 되는 글자. 접은 뒤의 **높이**가 허용 줄 수 안인지를 본다 */
        private static void CheckWrapped(TMP_Text probe, string worst, int lines, string where)
        {
            probe.textWrappingMode = TextWrappingModes.Normal;
            float needed = probe.GetPreferredValues(worst, ButtonTextWidth, 0f).y;
            float box = lines * LineHeight;
            if (needed > box)
                Debug.LogWarning(string.Format(
                    "[Onikiri] Yodo {0} spills out of the button: \"{1}\" wraps to {2:F0}px "
                    + "at {3:F0}px wide but only {4:F0}px ({5} lines) is reserved.",
                    where, worst, needed, ButtonTextWidth, box, lines));
        }

        public static GameObject BuildForgePage(RectTransform content, YodoSystem system,
                                                TMP_FontAsset font)
        {
            VerifyRowsFit();

            var page = CreatePage(content, "YodoPage", ForgePageHeight);

            BuildShardBar(page, system, font);

            for (int i = 0; i < YodoCatalog.Count; i++)
                BuildBladeRow(page, system, font, i);

            return page.gameObject;
        }

        public static GameObject BuildCodexPage(RectTransform content, YodoSystem system,
                                                TMP_FontAsset font)
        {
            var page = CreatePage(content, "CodexPage", CodexPageHeight);

            for (int i = 0; i < YodoCatalog.Count; i++)
                BuildCodexRow(page, system, font, i, i);

            // 오니키리 줄. 인덱스 -1이 그 뜻이고, **완성의 세로축이 끝나는
            // 자리**가 목록 한가운데의 선이 된다(YodoCodexRow.DrawOnikiri 주석)
            BuildCodexRow(page, system, font, YodoCatalog.Count, -1);

            // 그 아래가 전설 - **수집의 가로축**이다(47단계). 세트 보너스에
            // 안 들어가는 사실이 배치로도 읽혀야 한다
            for (int i = 0; i < LegendaryYodoCatalog.Count; i++)
                BuildCodexRow(page, system, font, YodoCatalog.Count + 1 + i, -1, i);

            return page.gameObject;
        }

        // ---------------------------------------------------------------- 파편 줄

        private static void BuildShardBar(RectTransform page, YodoSystem system, TMP_FontAsset font)
        {
            var go = new GameObject("ShardBar", typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, ShardBarHeight);
            rect.anchoredPosition = Vector2.zero;

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Chrome);

            CreateIcon(go.transform, UiIcons.LoadItem(YodoSprites.ShardSprite), UiIcons.Tint, 16f);

            var shardLabel = CreateLabel(go.transform, font, "Shards", TextAlignmentOptions.Left);
            Place((RectTransform)shardLabel.transform, TextLeft, TextRight, 14f, LineHeight);
            shardLabel.text = "파편 0";

            var progress = CreateLabel(go.transform, font, "Progress", TextAlignmentOptions.Left);
            UiFonts.Demote(progress);
            Place((RectTransform)progress.transform, TextLeft, TextRight, 14f + LineHeight, LineHeight);
            progress.color = DimColor;
            progress.text = "봉인 0 / " + YodoCatalog.Count;

            TMP_Text buyTitle, buyCost;
            Image buyImage;
            var buyButton = BuildSideButton(go.transform, font, "Buy", GemButtonTint,
                                            "파편 조달", "보석 " + YodoCurve.ShardPackGems,
                                            ShardBarHeight, BarCostLines,
                                            out buyTitle, out buyCost, out buyImage);

            var bar = go.AddComponent<Onikiri.UI.YodoShardBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("shardLabel").objectReferenceValue = shardLabel;
            so.FindProperty("progressLabel").objectReferenceValue = progress;
            so.FindProperty("buyButton").objectReferenceValue = buyButton;
            so.FindProperty("buyBackground").objectReferenceValue = buyImage;
            so.FindProperty("buyTitle").objectReferenceValue = buyTitle;
            so.FindProperty("buyCost").objectReferenceValue = buyCost;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("buyButtonTint").colorValue = GemButtonTint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 요도 행

        private static void BuildBladeRow(RectTransform page, YodoSystem system,
                                          TMP_FontAsset font, int index)
        {
            var spec = YodoCatalog.Blades[index];

            var go = new GameObject("Yodo" + index, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, BladeRowHeight);
            rect.anchoredPosition = new Vector2(0f,
                -(ShardBarHeight + RowGap + index * (BladeRowHeight + RowGap)));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            var icon = CreateIcon(go.transform, UiIcons.LoadItem(spec.IconSprite), spec.IconTint, 20f);

            // 이름만 44pt다. 나머지는 캡션(39단계 위계) - 목록은 훑는 화면이고
            // 44pt로 남는 것은 그 줄이 무엇인가와 버튼의 동사뿐이다
            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            Place((RectTransform)nameLabel.transform, TextLeft, TextRight + 200f, 12f, LineHeight);
            nameLabel.text = spec.SoulName;

            // **티어는 이름과 같은 줄, 오른쪽 끝이다.** 처음에 둘째 줄로
            // 뒀다가 물렸다 - 본문이 넉 줄(12 + 52x3 + 44 = 212)이 되어 170px
            // 행을 44px 넘겼고, 재료 줄이 다음 행 위로 흘렀다. 32단계 장비
            // 카드가 같은 자리에서 같은 실수를 했고 처방도 같다: **짧은 것을
            // 오른쪽으로 올려 줄을 하나 없앤다**
            var tierLabel = CreateLabel(go.transform, font, "Tier", TextAlignmentOptions.Right);
            UiFonts.Demote(tierLabel);
            Place((RectTransform)tierLabel.transform, TextLeft, TextRight, 12f, LineHeight);
            tierLabel.color = DimColor;
            tierLabel.text = "미봉인";

            var statLabel = CreateLabel(go.transform, font, "Stat", TextAlignmentOptions.Left);
            UiFonts.Demote(statLabel);
            Place((RectTransform)statLabel.transform, TextLeft, TextRight,
                  12f + LineHeight, LineHeight);
            statLabel.color = DimColor;
            statLabel.text = "공격력 ×1.000";

            var costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Left);
            UiFonts.Demote(costLabel);
            Place((RectTransform)costLabel.transform, TextLeft, TextRight,
                  12f + LineHeight * 2f, LineHeight);
            costLabel.color = DimColor;
            costLabel.text = "혼 0 / 1";

            // 상성 줄(45단계). 넷째 줄이고, 이 줄 때문에 행이 224px로 자랐다.
            // 맨 아래인 이유는 위 셋이 "지금 얼마인가"의 순서(무엇 -> 값 ->
            // 재료)로 읽히고, 상성은 그 뒤에 오는 **다음 질문**의 답이기
            // 때문이다 - "그래서 이 자루를 왜 고르는가"
            var affinityLabel = CreateLabel(go.transform, font, "Affinity",
                                            TextAlignmentOptions.Left);
            UiFonts.Demote(affinityLabel);
            Place((RectTransform)affinityLabel.transform, TextLeft, TextRight,
                  12f + LineHeight * 3f, LineHeight);
            affinityLabel.color = UiSkin.Gold;
            affinityLabel.text = Onikiri.UI.YodoRow.AffinitySkillName(index) + " 강화";

            TMP_Text forgeTitle, forgeCost;
            Image forgeImage;

            // **보석 색을 쓰지 않는다.** 청은 "보석이 든다"는 뜻이고
            // (UiSkin.GemAction), 봉인·합성에는 보석이 들지 않는다. 화면에서
            // 두 재화가 섞이지 않게 하는 32단계의 규칙 그대로다 - 이 판에서
            // 청은 파편 조달 버튼 하나뿐이다
            var forgeButton = BuildSideButton(go.transform, font, "Forge", ForgeButtonTint,
                                              "봉인", "혼 1", BladeRowHeight, BladeCostLines,
                                              out forgeTitle, out forgeCost, out forgeImage);

            var row = go.AddComponent<Onikiri.UI.YodoRow>();
            var so = new SerializedObject(row);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("bladeIndex").intValue = index;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("rowBackground").objectReferenceValue = background;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("tierLabel").objectReferenceValue = tierLabel;
            so.FindProperty("statLabel").objectReferenceValue = statLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("affinityLabel").objectReferenceValue = affinityLabel;
            so.FindProperty("forgeButton").objectReferenceValue = forgeButton;
            so.FindProperty("forgeBackground").objectReferenceValue = forgeImage;
            so.FindProperty("forgeTitle").objectReferenceValue = forgeTitle;
            so.FindProperty("forgeCost").objectReferenceValue = forgeCost;
            so.FindProperty("affordableColor").colorValue = TextColor;
            so.FindProperty("unaffordableColor").colorValue = DimColor;
            so.FindProperty("masteredColor").colorValue = UiSkin.Gold;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("iconTint").colorValue = spec.IconTint;
            so.FindProperty("forgeButtonTint").colorValue = ForgeButtonTint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 도감 행

        private static void BuildCodexRow(RectTransform page, YodoSystem system,
                                          TMP_FontAsset font, int slot, int bladeIndex,
                                          int legendaryIndex = -1)
        {
            string name = legendaryIndex >= 0 ? "Legendary" + legendaryIndex
                        : bladeIndex < 0 ? "Onikiri" : "Codex" + bladeIndex;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(page, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, CodexRowHeight);
            rect.anchoredPosition = new Vector2(0f, -slot * (CodexRowHeight + RowGap));

            var background = go.AddComponent<Image>();
            UiSkin.ApplyPanel(background, UiSkin.Row);

            // 오니키리 줄은 아이콘이 없다 - 아직 없는 칼이므로 어느 자루의
            // 그림도 쓸 수 없다. 대신 혼 보석을 세워 "네 혼이 모이는 자리"로
            // 읽히게 한다
            var spec = bladeIndex >= 0 ? YodoCatalog.Blades[bladeIndex] : default(YodoSpec);
            string sprite = bladeIndex >= 0 ? spec.IconSprite : YodoSprites.SoulSprite;
            var tint = bladeIndex >= 0 ? spec.IconTint : Color.white;

            if (legendaryIndex >= 0)
            {
                var legend = LegendaryYodoCatalog.Blades[legendaryIndex];
                sprite = legend.IconSprite;
                tint = legend.IconTint;
            }

            var icon = CreateIcon(go.transform, UiIcons.LoadItem(sprite), tint, 17f);

            var nameLabel = CreateLabel(go.transform, font, "Name", TextAlignmentOptions.Left);
            Place((RectTransform)nameLabel.transform, TextLeft, 24f + 340f, 14f, LineHeight);
            nameLabel.text = "???";

            var valueLabel = CreateLabel(go.transform, font, "Value", TextAlignmentOptions.Right);
            UiFonts.Demote(valueLabel);
            Place((RectTransform)valueLabel.transform, TextLeft, 24f, 14f, LineHeight);
            valueLabel.color = DimColor;
            valueLabel.text = string.Empty;

            var stateLabel = CreateLabel(go.transform, font, "State", TextAlignmentOptions.Left);
            UiFonts.Demote(stateLabel);
            Place((RectTransform)stateLabel.transform, TextLeft, 24f, 14f + LineHeight, LineHeight);
            stateLabel.color = DimColor;
            stateLabel.text = legendaryIndex >= 0
                ? LegendaryYodoCatalog.Blades[legendaryIndex].Flavor
                : bladeIndex >= 0 ? spec.BossName + " 처치 시 해금" : string.Empty;

            var row = go.AddComponent<Onikiri.UI.YodoCodexRow>();
            var so = new SerializedObject(row);
            so.FindProperty("system").objectReferenceValue = system;
            so.FindProperty("bladeIndex").intValue = bladeIndex;
            so.FindProperty("legendaryIndex").intValue = legendaryIndex;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("rowBackground").objectReferenceValue = background;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("stateLabel").objectReferenceValue = stateLabel;
            so.FindProperty("valueLabel").objectReferenceValue = valueLabel;
            so.FindProperty("textColor").colorValue = TextColor;
            so.FindProperty("dimColor").colorValue = DimColor;
            so.FindProperty("goldColor").colorValue = UiSkin.Gold;
            so.FindProperty("normalRowTint").colorValue = UiSkin.Row;
            so.FindProperty("iconTint").colorValue = tint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 조각

        /**
         * @brief 행 오른쪽에 서는 두 줄 버튼.
         *
         * 장비 카드의 버튼은 카드 아래를 반씩 나눠 쓰는데(둘이라서), 요도는
         * 하나뿐이라 오른쪽에 세운다. 41b가 동료 카드에서 확정한 배치이고,
         * 행 높이가 272에서 170으로 줄어드는 것이 그 배치의 이유다.
         *
         * ## 글자가 판 밖으로 흐르던 것을 두 군데서 고쳤다
         *
         * **세로** - 두 줄을 판 위에서부터 14px, 66px에 못 박아 두고 있었다.
         * 요도 행의 버튼(188px)에서는 남았지만 파편 줄의 버튼(94px)에서는
         * 둘째 줄이 24px 넘쳐 판 아래로 빠져나갔다. 이제 두 줄을 한 덩어리로
         * 묶어 **판 한가운데**에 세운다 - 행 높이가 달라도 같은 코드가 선다.
         *
         * **가로** - 캡션 한 줄에 다 적으려다 넘쳤다. 이제 비용 줄은
         * costLines 만큼 접힌다(NoWrap이 기본이라 명시로 푼다). 폰트를 줄이는
         * 선택지는 없다 - 33pt가 아틀라스를 구운 크기이고 그 사이 값은 흐려진다.
         *
         * @param rowHeight 이 버튼이 서는 행의 높이. 위아래 여백을 여기서 뺀다
         * @param costLines 비용 줄이 접힐 수 있는 최대 줄 수
         */
        private static Button BuildSideButton(Transform parent, TMP_FontAsset font, string name,
                                              Color tint, string title, string cost,
                                              float rowHeight, int costLines,
                                              out TMP_Text titleLabel, out TMP_Text costLabel,
                                              out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // 내용이 안 들어가면 여백부터 내준다. 파편 줄(130px)이 그 경우다 -
            // 18px씩 비우면 두 줄이 판을 넘는다
            float contentHeight = LineHeight * (1 + costLines);
            float inset = Mathf.Min(ButtonInset, Mathf.Max(0f, (rowHeight - contentHeight) * 0.5f));

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-ButtonWidth - ButtonRight, inset);
            rect.offsetMax = new Vector2(-ButtonRight, -inset);

            image = go.AddComponent<Image>();
            UiSkin.ApplyPanel(image, tint);

            var button = go.AddComponent<Button>();
            UiSkin.ApplyButton(button, image);

            titleLabel = CreateLabel(go.transform, font, "Title", TextAlignmentOptions.Center);
            PlaceCentered((RectTransform)titleLabel.transform, ButtonTextPad,
                          costLines * LineHeight * 0.5f, LineHeight);
            titleLabel.text = title;

            costLabel = CreateLabel(go.transform, font, "Cost", TextAlignmentOptions.Center);
            UiFonts.Demote(costLabel);
            PlaceCentered((RectTransform)costLabel.transform, ButtonTextPad,
                          -LineHeight * 0.5f, costLines * LineHeight);
            if (costLines > 1) costLabel.textWrappingMode = TextWrappingModes.Normal;
            costLabel.color = DimColor;
            costLabel.text = cost;

            return button;
        }

        private static Image CreateIcon(Transform parent, Sprite sprite, Color tint, float top)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(UiIcons.Size, UiIcons.Size);
            rect.anchoredPosition = new Vector2(IconLeft, -top);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            // 스프라이트가 없으면 자홍색 사각형이 남는다. 장비·강화 행과 같은
            // 규칙 - 빠진 아이콘이 눈에도 드러나야 한다
            if (sprite == null) image.color = new Color(1f, 0f, 1f, 0.35f);

            return image;
        }

        /**
         * @brief 판 **한가운데**를 기준으로 놓는다. Place는 윗변 기준이다.
         *
         * 버튼 안의 두 줄이 이것을 쓴다. 윗변 기준으로 못 박으면 판 높이가
         * 바뀔 때마다 아래로 흘러넘치는데, 같은 헬퍼가 94px 버튼과 188px
         * 버튼에 동시에 서야 한다.
         *
         * @param centerY 판 한가운데에서 위로 얼마나 올릴지(양수가 위)
         */
        private static void PlaceCentered(RectTransform rect, float pad,
                                          float centerY, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(pad, centerY - height * 0.5f);
            rect.offsetMax = new Vector2(-pad, centerY + height * 0.5f);
        }

        private static void Place(RectTransform rect, float left, float right,
                                  float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font, string name,
                                            TextAlignmentOptions alignment)
        {
            var label = QuestPanelBuilder.CreateLabel(parent, font, name, alignment);
            label.color = TextColor;
            return label;
        }
    }
}
