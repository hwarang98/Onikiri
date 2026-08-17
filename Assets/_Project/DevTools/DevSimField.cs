#if UNITY_EDITOR
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEditor;

namespace Onikiri.DevTools
{
    /**
     * @brief 시뮬레이션 입력 `Field`를 **에셋에서** 만든다. 단일 출처다.
     *
     * ## 왜 여기로 올렸나 - 두 벌이 되면 프리셋이 밴드와 다른 세계에서 나온다
     *
     * 이 함수는 5.0단계까지 `PromotionTrialFixture` 안에만 있었다. 그런데 프리셋
     * 세이브를 찍어내는 쪽은 Editor 메뉴(`Assembly-CSharp-Editor`)이고 그쪽은
     * 테스트 어셈블리를 참조할 수 없다 - 그대로 두면 에셋 읽기를 한 벌 더
     * 적어야 하고, 그 둘이 갈리는 날 **프리셋은 밴드가 잰 것과 다른 잡몹 평균에서
     * 유도된다.** 그 어긋남은 세이브 어디에도 안 적히므로 아무도 못 본다.
     *
     * 그래서 함수를 양쪽이 다 볼 수 있는 자리로 옮기고, 테스트 쪽은 이것을
     * 부르게 했다(`PromotionTrialFixture.FieldFromAssets`). 값은 한 비트도
     * 바뀌지 않는다 - 옮긴 것이지 고친 것이 아니다.
     */
    public static class DevSimField
    {
        const string DataFolder = "Assets/_Project/Data";

        /**
         * @brief 스폰 가중치로 평균 낸 1스테이지 기준 잡몹 체력·골드.
         *
         * 보충 간격 1.1초는 스포너의 기본값이다. 세 값이 곧 곡선의 입구이고,
         * 여기가 움직이면 M 표도 기준 화력도 함께 움직인다.
         */
        public static StageSimulation.Field FieldFromAssets()
        {
            BigDouble healthSum = BigDouble.Zero;
            BigDouble goldSum = BigDouble.Zero;
            double totalWeight = 0d;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { DataFolder }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.spawnWeight <= 0f) continue;

                healthSum += definition.maxHealth * BigDouble.FromDouble(definition.spawnWeight);
                goldSum += definition.goldReward * BigDouble.FromDouble(definition.spawnWeight);
                totalWeight += definition.spawnWeight;
            }

            return new StageSimulation.Field
            {
                AverageMobHealth = totalWeight > 0d
                    ? (healthSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d,
                AverageMobGold = totalWeight > 0d
                    ? (goldSum / BigDouble.FromDouble(totalWeight)).ToDouble() : 0d,
                SpawnInterval = 1.1d
            };
        }
    }
}
#endif
