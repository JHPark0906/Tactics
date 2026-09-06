using HS.Framework.Ability.Abilities;
using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 전투에 배치할 수 있는 유닛 종류 하나를 데이터로 정의한다.
    /// 배치, 조립, 공격 등 유닛을 다루는 모든 코드가 이 정의를 단일 출처로 삼는다.
    /// </summary>
    /// <remarks>
    /// 여기 들어 있는 수치는 모두 게임이 돌아가는 것을 확인하기 위한 <b>임시값</b>이다.
    /// 밸런스는 아직 정해지지 않았으므로 실제 조정은 기획 수치가 확정된 뒤에 한다.
    /// </remarks>
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Tactics/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Tooltip("저장 데이터가 이 유닛 종류를 가리킬 때 쓰는 식별자이다. 비워 두면 에디터가 채운다. 한 번 정해지면 바꾸지 마라 — 바꾸면 그 종류로 저장된 육성 레벨을 찾지 못한다.")]
        [SerializeField]
        private string id;

        [Tooltip("UI와 디버그에 표시할 유닛 이름이다.")]
        [SerializeField]
        private string displayName = "Unit";

        [Tooltip("최대 체력이다. 임시값이다.")]
        [SerializeField]
        [Min(1)]
        private int maxHealth = 100;

        [Tooltip("이 유닛이 기본으로 속하는 진영이다. 배치 시점에 진영이 이미 정해져 있으면 그쪽이 우선한다.")]
        [SerializeField]
        private TeamId defaultTeam;

        [Tooltip("이동 속도(초당 미터)이다. 임시값이다.")]
        [SerializeField]
        [Min(0.1f)]
        private float moveSpeed = 3.5f;

        [Tooltip("바라보는 방향이 도는 속도(초당 도)이다. 임시값이다.\n" +
                 "이동과는 별개로 돌며 경로에 영향을 주지 않는다. 사거리 안의 적을 향해 조준을 마치는 데 드는 시간이 " +
                 "이 값으로 정해지므로, 등 뒤에서 온 적에 빨리 대처하는 유닛은 이 값이 크다. 레벨은 이 값을 올리지 않는다.")]
        [SerializeField]
        [Min(1f)]
        private float turnSpeed = DefaultTurnSpeed;

        [Tooltip("유닛이 평면 위에서 차지하는 원의 반지름(미터)이다. 탐지·배치 판정이 이 값으로 유닛의 크기를 안다. 임시값이다.")]
        [SerializeField]
        [Min(0.1f)]
        private float radius = 0.5f;

        [Tooltip("공격이 닿는 최대 거리(미터)이다. 임시값이다.")]
        [SerializeField]
        [Min(0.1f)]
        private float attackRange = 8f;

        [Tooltip("한 번의 공격이 주는 피해량이다. 임시값이다.")]
        [SerializeField]
        [Min(1)]
        private int attackDamage = 10;

        [Tooltip("공격과 다음 공격 사이의 간격(초)이다. 임시값이다.")]
        [SerializeField]
        [Min(0.05f)]
        private float attackInterval = 1f;

        [Tooltip("엄폐하지 않은 대상을 맞출 확률이다. 1이면 항상 명중한다. 임시값이다.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float baseHitChance = 0.75f;

        [Tooltip("이 유닛을 스폰할 때 사용할 프리팹이다. TacticalUnit 컴포넌트를 포함해야 한다.")]
        [SerializeField]
        private GameObject unitPrefab;
        
        [SerializeField] private GameplayAbilitySet abilitySet;
        public GameplayAbilitySet AbilitySet => abilitySet;

        /// <summary>
        /// 저장 데이터가 이 유닛 종류를 가리킬 때 쓰는 식별자이며, 비어 있으면 에셋 이름으로 물러난다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>이름이나 파일 위치가 바뀌어도 같아야 한다.</b> 저장된 육성 레벨은 이 값으로 종류를 찾으므로,
        /// 값이 달라지면 그 종류의 레벨을 잃는다. 그래서 표시 이름과 따로 둔다 —
        /// 표시 이름은 언제든 바뀔 수 있는 것이고 이것은 바뀌면 안 되는 것이다.
        /// </para>
        /// <para>
        /// 에셋 이름으로 물러나는 것은 아직 채워지지 않은 정의도 저장에 참여시키기 위해서이다.
        /// 다만 그렇게 물러난 값은 파일 이름을 바꾸면 달라지므로, 에디터가 채워 둔 값이 있는 편이 낫다.
        /// </para>
        /// </remarks>
        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        /// <summary>UI와 디버그에 표시할 유닛 이름이며, 비어 있으면 에셋 이름을 사용한다.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>최대 체력이며 항상 1 이상이다.</summary>
        public int MaxHealth => Mathf.Max(1, maxHealth);

        /// <summary>이 유닛이 기본으로 속하는 진영이다.</summary>
        public TeamId DefaultTeam => defaultTeam;

        /// <summary>이동 속도(초당 미터)이며 항상 0보다 크다.</summary>
        public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);

        /// <summary>별도 값이 지정되지 않았을 때 사용하는 회전 속도(초당 도)이다.</summary>
        public const float DefaultTurnSpeed = 120f;

        /// <summary>바라보는 방향이 도는 속도(초당 도)이며 항상 1 이상이다. 레벨에 물리지 않는다.</summary>
        public float TurnSpeed => Mathf.Max(1f, turnSpeed);

        /// <summary>유닛이 평면 위에서 차지하는 원의 반지름(미터)이며 항상 0보다 크다.</summary>
        public float Radius => Mathf.Max(0.1f, radius);

        /// <summary>공격이 닿는 최대 거리(미터)이며 항상 0보다 크다.</summary>
        public float AttackRange => Mathf.Max(0.1f, attackRange);

        /// <summary>한 번의 공격이 주는 피해량이며 항상 1 이상이다.</summary>
        public int AttackDamage => Mathf.Max(1, attackDamage);

        /// <summary>공격 간격(초)이며 항상 0보다 크다.</summary>
        public float AttackInterval => Mathf.Max(0.05f, attackInterval);

        /// <summary>엄폐하지 않은 대상을 맞출 확률(0~1)이다.</summary>
        public float BaseHitChance => Mathf.Clamp01(baseHitChance);

        /// <summary>이 유닛을 스폰할 때 사용할 프리팹이며, 지정되지 않았으면 null이다.</summary>
        public GameObject UnitPrefab => unitPrefab;

        /// <summary>프리팹이 연결되어 스폰할 수 있는 정의인지 여부이다.</summary>
        public bool HasUnitPrefab => unitPrefab != null;

#if UNITY_EDITOR
        /// <summary>
        /// 식별자가 비어 있으면 에셋의 GUID로 채운다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>사용자가 손으로 적지 않아도 되게 한다.</b> 비워 둔 채 저장에 참여하면 에셋 이름으로 물러나는데,
        /// 그 값은 파일 이름을 바꾸는 순간 달라져 저장된 레벨을 잃는다.
        /// </para>
        /// <para>
        /// <b>GUID를 여기서 읽는 것은 에디터에서만 가능하다.</b> 실행 중에는 에셋 경로를 물을 수 없으므로,
        /// 미리 문자열로 적어 두고 실행 중에는 그 문자열만 읽는다.
        /// </para>
        /// <para>
        /// <b>이미 값이 있으면 건드리지 않는다.</b> 사용자가 읽기 좋은 이름을 손수 적어 두었을 수 있고,
        /// 그것을 GUID로 덮으면 저장 데이터가 가리키던 종류를 잃는다.
        /// </para>
        /// </remarks>
        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            id = assetGuid;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>
        /// 에셋을 만들지 않고 코드에서 유닛 정의를 생성한다.
        /// 테스트와 에디터 도구가 사용하며, 게임 실행 중에는 에셋 정의를 사용한다.
        /// </summary>
        /// <param name="displayName">표시 이름이다.</param>
        /// <param name="maxHealth">최대 체력이다.</param>
        /// <param name="defaultTeam">기본 진영이다.</param>
        /// <param name="moveSpeed">이동 속도이다.</param>
        /// <param name="attackRange">공격 사거리이다.</param>
        /// <param name="attackDamage">공격력이다.</param>
        /// <param name="attackInterval">공격 간격이다.</param>
        /// <param name="unitPrefab">스폰에 사용할 프리팹이다.</param>
        /// <param name="baseHitChance">엄폐하지 않은 대상을 맞출 확률이다.</param>
        /// <param name="abilitySet">이 유닛이 받을 어빌리티 집합이다.</param>
        /// <param name="id">저장 데이터가 이 종류를 가리킬 식별자이며, 비우면 에셋 이름을 쓴다.</param>
        /// <param name="turnSpeed">바라보는 방향이 도는 속도(초당 도)이다.</param>
        /// <param name="radius">유닛이 평면 위에서 차지하는 원의 반지름이다.</param>
        /// <returns>생성한 유닛 정의이다.</returns>
        public static UnitDefinition CreateRuntime(
            string displayName,
            int maxHealth,
            TeamId defaultTeam = default,
            float moveSpeed = 3.5f,
            float attackRange = 8f,
            int attackDamage = 10,
            float attackInterval = 1f,
            GameObject unitPrefab = null,
            float baseHitChance = 0.75f,
            GameplayAbilitySet abilitySet = null,
            string id = null,
            float turnSpeed = DefaultTurnSpeed,
            float radius = 0.5f)
        {
            var definition = CreateInstance<UnitDefinition>();
            definition.displayName = displayName;
            definition.maxHealth = maxHealth;
            definition.defaultTeam = defaultTeam;
            definition.moveSpeed = moveSpeed;
            definition.turnSpeed = turnSpeed;
            definition.radius = radius;
            definition.attackRange = attackRange;
            definition.attackDamage = attackDamage;
            definition.attackInterval = attackInterval;
            definition.unitPrefab = unitPrefab;
            definition.baseHitChance = baseHitChance;
            definition.abilitySet = abilitySet;
            definition.id = id;
            return definition;
        }
    }
}
