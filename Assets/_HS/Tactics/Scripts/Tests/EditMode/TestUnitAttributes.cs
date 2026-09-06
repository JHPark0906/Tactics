using System;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛과 엄폐물이 조립될 때 갖춰야 하는 어트리뷰트 묶음을 검사에서 만들어 주는 도우미이다.
    /// </summary>
    /// <remarks>
    /// 체력은 어트리뷰트 집합이 갖고 조립 코드가 <see cref="UnitAttributeIds"/>로 그것을 찾는다. 그래서 체력을 읽는 검사는
    /// 정의 에셋과 같은 식별자를 가진 정의로 묶음을 갖춰야 한다. 정의 에셋을 직접 읽지 않는 것은 검사가 에셋의 임시 수치에
    /// 매이지 않게 하기 위해서이다.
    /// </remarks>
    internal sealed class TestUnitAttributes : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        /// <summary>유닛 묶음을 만든다.</summary>
        /// <param name="withExperience">Hero 묶음처럼 경험치를 넣을지 여부이다.</param>
        internal TestUnitAttributes(bool withExperience = true)
        {
            MaxHealth = Track(AttributeDefinition.CreateRuntime(UnitAttributeIds.MaxHealth, 100f, 1f));
            Health = Track(AttributeDefinition.CreateRuntime(UnitAttributeIds.Health, 100f, 0f, MaxHealth));
            AttackPower = Track(AttributeDefinition.CreateRuntime(UnitAttributeIds.AttackPower, 10f));
            Level = Track(AttributeDefinition.CreateRuntime(UnitAttributeIds.Level, 1f, 1f));
            Xp = withExperience ? Track(AttributeDefinition.CreateRuntime(UnitAttributeIds.Xp, 0f)) : null;

            var entries = new List<AttributeSetDefinition.Entry>
            {
                new(MaxHealth), new(Health), new(AttackPower), new(Level)
            };
            if (Xp != null)
            {
                entries.Add(new AttributeSetDefinition.Entry(Xp));
            }

            Set = Track(AttributeSetDefinition.CreateRuntime(entries));

            CoverAbsorbChance = Track(AttributeDefinition.CreateRuntimeWithMaxValue(UnitAttributeIds.CoverAbsorbChance, 0.35f, 0f, 1f));
            CoverSet = Track(AttributeSetDefinition.CreateRuntime(new List<AttributeSetDefinition.Entry>
            {
                new(MaxHealth), new(Health), new(CoverAbsorbChance)
            }));
        }

        /// <summary>엄폐물이 대신 맞아 줄 확률 정의이며 엄폐물 묶음에만 들어간다.</summary>
        internal AttributeDefinition CoverAbsorbChance { get; }

        /// <summary>엄폐물 묶음이다. 체력, 최대 체력, 흡수 확률을 담는다.</summary>
        internal AttributeSetDefinition CoverSet { get; }

        /// <summary>최대 체력 정의이다.</summary>
        internal AttributeDefinition MaxHealth { get; }

        /// <summary>현재 체력 정의이며 최대 체력을 상한으로 삼는다.</summary>
        internal AttributeDefinition Health { get; }

        /// <summary>공격력 정의이다.</summary>
        internal AttributeDefinition AttackPower { get; }

        /// <summary>레벨 정의이다.</summary>
        internal AttributeDefinition Level { get; }

        /// <summary>경험치 정의이며 경험치 없이 만들었으면 null이다.</summary>
        internal AttributeDefinition Xp { get; }

        /// <summary>위 정의를 담은 묶음이다.</summary>
        internal AttributeSetDefinition Set { get; }

        /// <summary>이 묶음과 지정한 어빌리티를 담은 어빌리티 집합을 만든다.</summary>
        /// <param name="abilities">집합에 넣을 어빌리티 정의이다.</param>
        internal GameplayAbilitySet CreateAbilitySet(params GameplayAbilityDefinition[] abilities)
        {
            return Track(GameplayAbilitySet.CreateRuntime(abilities, attributeSetDefinitions: new[] { Set }));
        }

        /// <summary>이 묶음을 갖춘 어빌리티 집합을 든 유닛 정의를 만든다.</summary>
        /// <param name="displayName">표시 이름이다.</param>
        /// <param name="maxHealth">최대 체력이다.</param>
        /// <param name="team">기본 진영이다.</param>
        /// <param name="attackDamage">공격력이다.</param>
        /// <param name="abilities">집합에 넣을 어빌리티 정의이다.</param>
        internal UnitDefinition CreateUnitDefinition(
            string displayName,
            int maxHealth,
            TeamId team = default,
            int attackDamage = 10,
            params GameplayAbilityDefinition[] abilities)
        {
            // 식별자를 표시 이름으로 둔다. 코드에서 만든 정의는 에셋 이름이 비어 있어 진행 데이터가 종류를 가리킬 수 없기 때문이다.
            return Track(UnitDefinition.CreateRuntime(
                displayName,
                maxHealth,
                team,
                attackDamage: attackDamage,
                abilitySet: CreateAbilitySet(abilities),
                id: displayName));
        }

        /// <summary>
        /// 어빌리티 시스템이 없는 오브젝트(엄폐물, 소품)에 이 묶음의 체력을 갖추고 체력 문에 연결한다.
        /// </summary>
        /// <param name="target">체력을 갖출 오브젝트이며 체력 문이 없으면 붙인다.</param>
        /// <returns>연결된 체력 문이다.</returns>
        internal HealthAttributeComponent AttachHealth(GameObject target)
        {
            if (!target.TryGetComponent<HealthAttributeComponent>(out var health))
            {
                health = target.AddComponent<HealthAttributeComponent>();
            }

            Set.ApplyTo(target.GetComponent<AttributeSetComponent>().Attributes);
            health.ConfigureAttributes(Health, MaxHealth);
            return health;
        }

        /// <summary>
        /// 오브젝트를 엄폐물로 조립한다. 엄폐 지점을 붙이고, 엄폐물 묶음과 부서짐 어빌리티를 어빌리티 시스템에 부여하며,
        /// 체력 문을 연결한다. 어빌리티 시스템은 수동 제어로 두므로 틱은 검사가 돌린다.
        /// </summary>
        /// <param name="target">엄폐물로 만들 오브젝트이다.</param>
        /// <param name="absorbChance">흡수 확률 어트리뷰트에 넣을 값이다.</param>
        /// <returns>붙인 엄폐 지점이다.</returns>
        internal CoverPoint AttachCover(GameObject target, float absorbChance = 0.35f)
        {
            if (!target.TryGetComponent<CoverPoint>(out var cover))
            {
                cover = target.AddComponent<CoverPoint>();
            }

            // 에디터는 플레이 모드가 아닐 때 OnEnable을 호출하지 않는다. 죽은 상태 태그 구독은 거기서 걸리므로
            // 여기서 직접 구동해, 체력이 다했을 때 MarkDestroyed로 이어지는 검사가 조용히 걸리지 않게 한다.
            MonoBehaviourLifecycle.InvokeOnEnable(cover);

            var abilitySystem = target.GetComponent<GameplayAbilitySystemComponent>();
            abilitySystem.ConfigureManualControl();
            var abilities = new GameplayAbilityDefinition[]
            {
                Track(DeathAbilityDefinition.CreateRuntime(UnitAbilityTags.CoverDeath))
            };
            Track(GameplayAbilitySet.CreateRuntime(abilities, attributeSetDefinitions: new[] { CoverSet }))
                .GrantTo(abilitySystem.System);
            abilitySystem.System.Attributes.SetBaseValue(CoverAbsorbChance, absorbChance);
            // 체력 문은 엄폐 지점이 처음 쓸 때 묶음의 이름으로 정의를 찾아 묶는다. 여기서 미리 건드려 효과가 오기 전에 묶이게 한다.
            _ = cover.Health;
            return cover;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : UnityEngine.Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
