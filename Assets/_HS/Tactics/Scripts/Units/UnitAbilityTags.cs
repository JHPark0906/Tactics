namespace HS.Tactics.Units
{
    /// <summary>
    /// 유닛이 쓰는 어빌리티를 가리키는 태그 이름이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이름 자체는 어느 구현의 것이 아니라 게임이 정한 값이므로, 쓰는 곳마다 상수로 흩어 두지 않고
    /// 한자리에 모아 둔다.
    /// </para>
    /// <para>
    /// 행동 트리 에셋의 자리 설명이 여기서 이름을 읽고, 검사도 같은 이름을 쓴다. 문자열을 각자
    /// 적으면 한쪽만 고쳐도 아무 데서도 안 터지고 어빌리티만 조용히 안 돈다.
    /// </para>
    /// </remarks>
    public static class UnitAbilityTags
    {
        /// <summary>
        /// 모든 어빌리티 태그의 뿌리이다. 계층 일치를 따르므로 이 이름 하나로 어빌리티 전부를 가리킬 수 있으며,
        /// 사망 어빌리티가 다른 어빌리티를 전부 취소하고 차단할 때 이것을 쓴다.
        /// </summary>
        public const string Family = "Ability";

        /// <summary>사격 어빌리티이다.</summary>
        public const string Attack = "Ability.Attack";

        /// <summary>대상을 마주 볼 때까지 도는 조준 어빌리티이다.</summary>
        public const string Aim = "Ability.Aim";

        /// <summary>대상이 사거리 안에 들 때까지 다가가는 접근 어빌리티이다.</summary>
        public const string Approach = "Ability.Approach";

        /// <summary>엄폐를 확보하는 어빌리티이다.</summary>
        public const string TakeCover = "Ability.TakeCover";

        /// <summary>확보한 엄폐를 유지하는 어빌리티이다.</summary>
        public const string MaintainCover = "Ability.MaintainCover";

        /// <summary>죽을 때 발동해 죽은 상태 태그만 붙이는 어빌리티이다.</summary>
        public const string Death = "Ability.Death";

        /// <summary>사망 어빌리티가 끝나면 발동해 연출 시간 뒤 전장에서 물러나게 하는 유닛 전용 어빌리티이다.</summary>
        public const string Disappearance = "Ability.Disappearance";

        /// <summary>엄폐 중인 유닛이 피해를 엄폐물에 넘기는 회피 어빌리티의 태그이다.</summary>
        public const string CoverEvasion = "Ability.CoverEvasion";

        /// <summary>엄폐물이 부서질 때 발동하는 어빌리티의 태그이다.</summary>
        public const string CoverDeath = "Ability.CoverDeath";

        /// <summary>유닛이 엄폐 지점을 확보하고 그 자리에 도착해 엄폐 중임을 나타내는 상태 태그이다.</summary>
        public const string InCoverState = "State.InCover";
    }
}
