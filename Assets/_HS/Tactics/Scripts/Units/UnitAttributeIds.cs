namespace HS.Tactics.Units
{
    /// <summary>
    /// 유닛과 엄폐물이 쓰는 어트리뷰트 정의의 식별자이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 어떤 어트리뷰트가 존재하는지는 정의 에셋이 정하고, 코드는 그 에셋을 직접 참조하지 않는다. 조립 코드가
    /// 어빌리티 집합이 갖춰 준 집합에서 이름으로 체력·공격력·레벨을 찾을 때 이 식별자를 쓴다.
    /// 에셋의 식별자와 이 상수가 같은지는 정의 에셋 검사가 대조한다.
    /// </para>
    /// <para>
    /// 태그 이름을 <see cref="UnitAbilityTags"/>에 모아 둔 것과 같은 이유이다. 문자열을 각자 적으면 한쪽만 고쳐도
    /// 아무 데서도 안 터지고 체력만 조용히 조립되지 않는다.
    /// </para>
    /// </remarks>
    public static class UnitAttributeIds
    {
        /// <summary>현재 체력이다.</summary>
        public const string Health = "Health";

        /// <summary>최대 체력이며 현재 체력의 상한이다.</summary>
        public const string MaxHealth = "MaxHealth";

        /// <summary>사격 피해량의 바탕이 되는 공격력이다.</summary>
        public const string AttackPower = "AttackPower";

        /// <summary>육성 레벨이다. 진행 데이터의 투영이며 전투 중에는 바뀌지 않는다.</summary>
        public const string Level = "Level";

        /// <summary>현재 경험치이다. 진행 데이터의 투영이며 Hero 묶음에만 있다.</summary>
        public const string Xp = "Xp";

        /// <summary>엄폐 중인 유닛이 맞을 때 엄폐물이 대신 받을 확률이다. 엄폐물 묶음에만 있다.</summary>
        public const string CoverAbsorbChance = "Cover.AbsorbChance";
    }
}
