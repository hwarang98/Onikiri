using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 65단계 명중·회피의 런타임 판정.
     *
     * EditMode가 "어디에 넣었는가"(소스)와 기대값(시뮬레이션)을 본다면, 여기는
     * 실제 컴포넌트가 굴린 결과를 본다 - 빗나가면 피해가 들어가지 않고, 피하면
     * 플레이어가 받은 피해가 쌓이지 않으며, 화면에 글자가 뜬다.
     *
     * 확률을 0과 1 근처로 몰아서 잰다(회피 1e12 / 0). 그 사이의 확률은
     * 판정식(RatingContest)이 EditMode에서 이미 검사됐다.
     */
    public class HitRatingPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        PlayerCombat combat;
        PlayerHealth health;
        BossFight fight;
        EnemySpawner spawner;
        Onikiri.UI.DamageNumberSpawner numbers;
        Enemy dummy;

        SaveSandbox sandbox;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            // 저장 경로를 통째로 끊는다(PromotionTrialPlayTests와 같은 이유)
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);

            combat = Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            health = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            fight = Object.FindFirstObjectByType<BossFight>(FindObjectsInactive.Include);
            spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            numbers = Object.FindFirstObjectByType<Onikiri.UI.DamageNumberSpawner>(FindObjectsInactive.Include);

            Assert.IsNotNull(combat, "씬에 PlayerCombat이 없다");
            Assert.IsNotNull(health, "씬에 PlayerHealth가 없다");
            Assert.IsNotNull(fight, "씬에 BossFight가 없다");
            Assert.IsNotNull(spawner, "씬에 EnemySpawner가 없다");
            Assert.IsNotNull(numbers, "씬에 DamageNumberSpawner가 없다");

            // 잡몹 하나가 나올 때까지 기다려 그 정의로 허수아비를 세운다 - 체력이
            // 커서 한 대에 죽지 않고(IsTargetable이 유지된다), 공격력이 있어서
            // 회피 검사가 "피해 0"과 "피해 없음"을 가를 수 있다.
            //
            // 체력은 1e9다 - **1e30이면 안 된다.** DamageTaken은 최대 체력 - 현재
            // 체력이라 1e30에서 수백의 피해는 정밀도 밖으로 사라진다(처음 그렇게
            // 넣어 영체 검사가 0을 봤고, 빗나감 검사는 아무것도 못 재고 통과했다)
            Time.timeScale = 10f;
            Enemy mob = null;
            for (int frame = 0; frame < 600 && mob == null; frame++)
            {
                foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                    if (e.IsAlive && e.Definition != null) { mob = e; break; }
                if (mob == null) yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsNotNull(mob, "잡몹이 나오지 않았다");

            dummy = spawner.SpawnBoss(mob.Definition, BigDouble.FromDouble(1e9d), BigDouble.Zero,
                BigDouble.Zero, 1f, Color.white, 50d, false);
            Assert.IsNotNull(dummy);
            yield return null;

            Random.InitState(65);
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            SetPhase(BossFight.Phase.Farming);
            if (health != null) health.Evasion = 0d;
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ------------------------------------------------------------ 명중

        /** 빗나가면 TakeDamage가 불리지 않는다 - 피해 0이 아니라 "피해 없음" */
        [UnityTest]
        public IEnumerator Miss_DoesNotCallTakeDamage_AndShowsMiss()
        {
            dummy.SetEvasion(1e12d);
            var before = dummy.DamageTaken;
            int misses = combat.MissCount;

            bool landed = combat.DeliverSkillHit(dummy, BigDouble.One, Color.white, 1);
            var after = dummy.DamageTaken;
            yield return null;

            Assert.IsFalse(landed, "회피 1e12인데 맞았다");
            Assert.AreEqual(before.ToDouble(), after.ToDouble(), 0d, "빗나갔는데 피해가 들어갔다");
            Assert.AreEqual(misses + 1, combat.MissCount);
            Assert.AreEqual(Onikiri.UI.DamageNumberSpawner.MissText, numbers.LastLabel, "MISS가 뜨지 않았다");
        }

        /**
         * 70단계 실기: MISS가 같은 피격점의 데미지 숫자 위에 겹쳤다("367,8MISS,736").
         * 글자(MISS · 회피)는 숫자보다 LabelRise 위에서 출발한다 - 둘은 같은 속도로 떠오른다
         */
        [UnityTest]
        public IEnumerator MissLabel_StartsAboveTheDamageNumber()
        {
            Assert.GreaterOrEqual(numbers.LabelOffset.y, Onikiri.UI.DamageNumberSpawner.LabelRise - 0.5f,
                                  "글자 오프셋이 씬에서 줄어 있다");

            var at = dummy.transform.position;
            numbers.ShowSkill(BigDouble.FromDouble(367836736d), at, Color.white, 1);
            numbers.ShowMiss(at);

            RectTransform miss = null, number = null;
            foreach (var popup in Object.FindObjectsByType<Onikiri.UI.DamageNumber>(FindObjectsSortMode.None))
            {
                var text = popup.GetComponent<TMPro.TMP_Text>();
                if (text == null) text = popup.GetComponentInChildren<TMPro.TMP_Text>();
                if (text == null) continue;
                if (text.text == Onikiri.UI.DamageNumberSpawner.MissText) miss = (RectTransform)popup.transform;
                else if (text.text == NumberFormatter.FormatFull(BigDouble.FromDouble(367836736d)))
                    number = (RectTransform)popup.transform;
            }
            Assert.IsNotNull(miss, "MISS가 뜨지 않았다");
            Assert.IsNotNull(number, "데미지 숫자가 뜨지 않았다");

            // 같은 프레임 - 아직 떠오르지 않았다
            Assert.AreEqual(numbers.LabelOffset.y, miss.anchoredPosition.y - number.anchoredPosition.y, 0.5f,
                            "MISS가 숫자와 같은 자리에서 출발했다");
            yield return null;
        }

        /** 확정 5: 영체(출처 Special)는 빗나가지 않는다 */
        [UnityTest]
        public IEnumerator Spirit_NeverMisses()
        {
            dummy.SetEvasion(1e12d);
            var before = dummy.DamageTaken;
            int misses = combat.MissCount;

            bool landed = combat.DeliverSkillHit(dummy, BigDouble.One, Color.white, 1,
                TrialDamageScale.Source.Special);
            var after = dummy.DamageTaken;
            yield return null;

            Assert.IsTrue(landed, "영체가 빗나갔다");
            Assert.Greater(after.ToDouble(), before.ToDouble(), "영체가 맞았는데 피해가 안 들어갔다");
            Assert.AreEqual(misses, combat.MissCount);
        }

        /** 회피 0(귀문의 적)은 늘 맞는다 */
        [UnityTest]
        public IEnumerator ZeroEvasion_AlwaysHits()
        {
            dummy.SetEvasion(0d);
            int misses = combat.MissCount;

            for (int i = 0; i < 50; i++)
                Assert.IsTrue(combat.DeliverSkillHit(dummy, BigDouble.One, Color.white, 1), "회피 0인데 빗나갔다");
            yield return null;

            Assert.AreEqual(misses, combat.MissCount);
        }

        // ------------------------------------------------------------ 회피

        /** 피하면 플레이어가 받은 피해(DamageTaken)가 쌓이지 않고 "회피"가 뜬다 */
        [UnityTest]
        public IEnumerator Dodge_DoesNotAddDamageTaken()
        {
            health.Evasion = 1e12d;
            health.BeginFight();
            SetPhase(BossFight.Phase.Fighting);

            InvokeBossAttack(dummy);
            yield return null;

            Assert.AreEqual(0d, health.DamageTaken, 0d, "피했는데 피해가 쌓였다");
            Assert.AreEqual(1, health.DodgeCount);
            Assert.AreEqual(Onikiri.UI.DamageNumberSpawner.DodgeText, numbers.LastLabel, "회피 글자가 뜨지 않았다");
        }

        /** 대조군: 회피 0이면 난수를 굴리지 않고 그대로 맞는다 */
        [UnityTest]
        public IEnumerator NoEvasion_TakesTheFullHit()
        {
            health.Evasion = 0d;
            health.BeginFight();
            SetPhase(BossFight.Phase.Fighting);

            // 기대값을 **때리기 전에** 잡는다. 한 프레임 뒤 BossFight가 필드를
            // 정리하며 허수아비를 풀로 돌리고, 스포너가 그것을 잡몹으로 다시
            // 쓰면 공격력이 0이 된다
            double expected = dummy.AttackDamage;
            Assert.Greater(expected, 0d);

            InvokeBossAttack(dummy);
            yield return null;

            Assert.AreEqual(expected, health.DamageTaken, 1e-9d);
            Assert.AreEqual(0, health.DodgeCount);
        }

        // ------------------------------------------------------------ 헬퍼

        void SetPhase(BossFight.Phase phase)
        {
            if (fight == null) return;
            var field = typeof(BossFight).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "BossFight.phase를 못 찾았다");
            field.SetValue(fight, phase);
        }

        void InvokeBossAttack(Enemy attacker)
        {
            var method = typeof(BossFight).GetMethod("OnBossAttacked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "BossFight.OnBossAttacked를 못 찾았다");
            method.Invoke(fight, new object[] { attacker });
        }
    }
}
