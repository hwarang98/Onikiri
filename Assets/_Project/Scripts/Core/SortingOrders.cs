namespace Onikiri.Core
{
    /**
     * @brief 전투 씬의 모든 정렬 순서를 한곳에 모은 정의.
     *
     * 뒤에서 앞 순서:
     *   하늘 -> 패럴랙스 레이어 -> 적 -> 플레이어 -> 지면 덮개 -> VFX -> UI
     *
     * 의도적으로 정한 두 가지:
     *  - 적은 플레이어 '뒤'에 그린다. 시선이 따라가야 할 대상은 사무라이이고,
     *    적은 오른쪽에 큐로 정렬돼 겹치지 않으므로 실제로 가려지는 일이 없다.
     *  - 지면 덮개(풀)는 둘 '앞'에 그린다. 풀이 발목을 가로질러야 씬 위에
     *    붙여놓은 것이 아니라 씬 안에 서 있는 것으로 읽힌다.
     */
    public static class SortingOrders
    {
        /** 카메라 전체를 덮는 하늘. 모든 패럴랙스 레이어보다 뒤 */
        public const int SkyFill = -300;

        /** 첫 패럴랙스 레이어. 레이어마다 1씩 증가 */
        public const int BackgroundBase = -200;

        /**
         * @brief 지면 위에 서 있는 배경 소품 (도리이 등).
         *
         * 지면 그림보다는 앞이고 파이터보다는 뒤다. 배경의 일부지만 평면이 아니라
         * 씬 안에 서 있는 물건으로 읽혀야 한다.
         */
        public const int BackgroundProp = -100;

        /**
         * @brief 적은 좁은 구간을 나눠 쓴다. 동시에 여러 마리가 나와도 z-fighting이 없다.
         *
         * 각 적은 EnemyBase + (슬롯 % EnemySlots) 를 갖는다.
         */
        public const int EnemyBase = 0;
        public const int EnemySlots = 40;

        /**
         * @brief 보스는 잡몹 구간 바로 위, 플레이어 아래다.
         *
         * 플레이어 위로 올리지 않는 것은 잡몹과 같은 이유다 - 시선이 따라가야 할
         * 대상은 사무라이다. 잡몹 구간(0~39) 위에 두는 이유는 보스전에서 필드가
         * 비워지긴 하지만, 실패 직후 잡몹이 돌아오는 한두 프레임 동안 겹칠 수 있고
         * 그때 보스가 잡몹에 가리면 안 되기 때문이다.
         */
        public const int Boss = EnemyBase + EnemySlots;

        public const int Player = 50;

        /** 파이터들의 발 위로 그려지는 풀 */
        public const int GroundCover = 70;

        /** 참격은 항상 모두의 위에 읽혀야 한다 */
        public const int Vfx = 100;

        /** 데미지 숫자용 */
        public const int DamageNumber = 200;
    }
}
