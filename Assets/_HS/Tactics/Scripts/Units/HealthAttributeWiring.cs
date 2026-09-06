using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 어트리뷰트 집합에서 이름으로 체력 정의를 찾아 체력 문에 연결한다.
    /// </summary>
    /// <remarks>
    /// 체력 문은 어떤 정의가 체력인지 스스로 알지 못한다. 인스펙터에서 정의를 연결해 두었으면 그것을 존중하고,
    /// 비어 있으면 어트리뷰트 묶음이 집합에 갖춰 둔 정의를 <see cref="UnitAttributeIds"/>로 찾는다.
    /// 유닛과 엄폐물이 같은 규칙을 쓰므로 한 자리에 둔다.
    /// </remarks>
    public static class HealthAttributeWiring
    {
        /// <summary>
        /// 체력 문이 아직 정의를 모르면 집합에서 이름으로 찾아 연결한다.
        /// </summary>
        /// <param name="health">연결할 체력 문이다.</param>
        /// <param name="attributes">체력 정의를 찾을 집합이다.</param>
        /// <returns>연결되어 있거나 연결했으면 true이며, 집합에 체력 정의가 없으면 false이다.</returns>
        public static bool TryConfigure(HealthAttributeComponent health, AttributeSet attributes)
        {
            if (health == null || attributes == null)
            {
                return false;
            }

            if (health.HealthAttribute != null && health.MaxHealthAttribute != null)
            {
                return true;
            }

            if (!attributes.TryFindDefinition(UnitAttributeIds.Health, out var healthDefinition) ||
                !attributes.TryFindDefinition(UnitAttributeIds.MaxHealth, out var maxHealthDefinition))
            {
                return false;
            }

            return health.ConfigureAttributes(healthDefinition, maxHealthDefinition);
        }
    }
}
