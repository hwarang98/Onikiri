using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 지역별 잡몹 풀(36단계)이 서로 다르게 보이면서 밸런스는 같은지 검사한다.
     *
     * ## 이 검사가 지키는 약속
     *
     * 보스 체력과 방치 보상은 스포너의 **가중 평균**에서 유도된다
     * (BossFight.BossMaxHealth -> EnemySpawner.AverageBaseHealth). 풀이 지역마다
     * 갈라진 뒤에도 시뮬레이션·밴드가 한 치도 안 움직이려면, 어느 지역 풀로
     * 평균을 내도 8단계부터 쓰던 필드 평균(HP 128/9, 골드 49/9)이 나와야 한다.
     *
     * StageSimulationTests의 전체 평균 검사는 8종을 한 번에 섞으므로, 한 풀이
     * 후하고 다른 풀이 박해도 상쇄되면 통과한다. 그래서 여기서 풀별로 다시 잰다.
     */
    public class RegionMobPoolTests
    {
        private const string BossFolder = "Assets/_Project/Data/Bosses";

        private static RegionConfig Region(int number)
        {
            var config = AssetDatabase.LoadAssetAtPath<RegionConfig>(
                BossFolder + "/Region_" + number + ".asset");
            Assert.IsNotNull(config, "Region_" + number + " 애셋이 없다");
            return config;
        }

        private static EnemyDefinition[] Pool(int number)
        {
            var config = Region(number);
            Assert.IsNotNull(config.mobs, "지역 " + number + "에 잡몹 풀이 안 물려 있다"
                + " - 그 지역은 앞 지역의 몹을 그대로 쓰게 된다");
            Assert.IsNotNull(config.mobs.mobs, "지역 " + number + " 풀이 비어 있다");
            return config.mobs.mobs;
        }

        /** 풀의 종을 아트로 구분한다. 이름이 같아도 다른 시트면 다른 종이다 */
        private static HashSet<Texture> Species(EnemyDefinition[] pool)
        {
            var textures = new HashSet<Texture>();
            foreach (var definition in pool)
                if (definition != null && definition.idleFrames != null && definition.idleFrames.Length > 0)
                    textures.Add(definition.idleFrames[0].texture);
            return textures;
        }

        [Test]
        public void EveryRegion_CarriesATwoMobPool()
        {
            for (int region = 1; region <= 4; region++)
            {
                var pool = Pool(region);
                Assert.AreEqual(2, pool.Length, "지역 " + region + " 풀은 주력+부몹 두 종이어야 한다");
                foreach (var definition in pool)
                    Assert.IsNotNull(definition, "지역 " + region + " 풀에 빈 칸이 있다");
            }
        }

        /**
         * @brief 어느 지역 풀로 평균을 내도 필드 평균이 같다.
         *
         * 지역을 넘는 순간 스포너의 평균이 움직이면 보스 체력과 방치 보상이
         * 지역마다 달라진다 - 그것은 밸런스 변경이고, 36단계는 밸런스 무관을
         * 약속했다.
         */
        [Test]
        public void EveryPool_KeepsTheFieldAverages()
        {
            for (int region = 1; region <= 4; region++)
            {
                var pool = Pool(region);

                BigDouble healthSum = BigDouble.Zero;
                BigDouble goldSum = BigDouble.Zero;
                double totalWeight = 0d;

                foreach (var definition in pool)
                {
                    if (definition == null) continue;
                    healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                    goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                    totalWeight += definition.spawnWeight;
                }

                Assert.Greater(totalWeight, 0d, "지역 " + region + " 풀의 가중치 합이 0이다");

                // 8단계부터 쓰던 필드 평균. StageSimulationTests와 같은 값이다
                Assert.AreEqual(14.222d, (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble(), 0.01d,
                    "지역 " + region + " 풀의 평균 체력이 다르다 - 이 지역만 보스가 다른 체력으로 나온다");
                Assert.AreEqual(5.444d, (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble(), 0.01d,
                    "지역 " + region + " 풀의 평균 골드가 다르다 - 이 지역만 방치 보상이 달라진다");
            }
        }

        /**
         * @brief 풀의 몹은 화면에 나올 수 있어야 한다.
         *
         * idle이 비면 투명 요괴, death가 비면 죽는 순간 뚝 사라지는 요괴다.
         * Kasa-obake는 태그가 없어 이름 범위로 프레임을 집으므로(빌더 참고),
         * 임포터가 이름 규칙을 바꾸는 날 여기서 잡혀야 한다.
         */
        [Test]
        public void PoolMobs_CanBeSeenAndDie()
        {
            for (int region = 1; region <= 4; region++)
            {
                foreach (var definition in Pool(region))
                {
                    if (definition == null) continue;
                    Assert.Greater(definition.idleFrames.Length, 0, definition.name + ": idle 프레임이 없다");
                    Assert.Greater(definition.deathFrames.Length, 0, definition.name + ": 사망 프레임이 없다");
                    Assert.Greater(definition.spawnWeight, 0f, definition.name + ": 가중치 0이 풀에 들어 있다");
                }
            }
        }

        /**
         * @brief 지역 1~3은 서로 다른 몹으로 보인다. 지역 4는 지역 3의 재활용이다.
         *
         * 팩에 남는 몹이 없어 지역 4는 지역 3의 두 종을 주력만 바꿔 쓴다(의도된
         * 겹침 - 엔드게임 전용 몹 팩을 사면 지역 4만 갈아끼운다). 그 겹침이
         * 1~3으로 번지면 "지역마다 다른 잡몹"이 조용히 무너지므로 방향을 나눠 건다.
         */
        [Test]
        public void RegionsOneToThree_ShareNoSpecies()
        {
            for (int a = 1; a <= 3; a++)
                for (int b = a + 1; b <= 3; b++)
                {
                    var overlap = Species(Pool(a));
                    overlap.IntersectWith(Species(Pool(b)));
                    Assert.AreEqual(0, overlap.Count,
                        "지역 " + a + "와 지역 " + b + "가 같은 몹을 쓴다");
                }
        }

        [Test]
        public void RegionFour_ReusesRegionThreeSpecies()
        {
            var three = Species(Pool(3));
            var four = Species(Pool(4));
            Assert.IsTrue(three.SetEquals(four),
                "지역 4는 지역 3의 두 종을 재활용하기로 했다(주력 역전). 구성이 달라졌으면 "
                + "엔드게임 몹 팩이 들어온 것이니 이 검사를 새 구성으로 바꿀 것");
        }

        /**
         * @brief 초롱(외눈 등롱의 원본)과 보스 정의는 어느 풀에도 없다.
         *
         * 지역 1 피날레와 같은 그림이 잡몹으로 나오면 피날레의 무게가 사라진다.
         */
        [Test]
        public void LanternBoss_StaysOutOfEveryPool()
        {
            var chochin = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/_Project/Data/Enemy_Chochin.asset");
            Assert.IsNotNull(chochin, "Enemy_Chochin이 없다 - 외눈 등롱 보스가 원본을 잃는다");
            Assert.AreEqual(0f, chochin.spawnWeight,
                "Enemy_Chochin에 가중치가 있다 - 잡몹 평균에 다시 섞여 들어간다");

            for (int region = 1; region <= 4; region++)
                foreach (var definition in Pool(region))
                    Assert.AreNotEqual(chochin, definition,
                        "지역 " + region + " 풀에 초롱이 있다 - 지역 1 피날레와 같은 그림이다");
        }
    }
}
