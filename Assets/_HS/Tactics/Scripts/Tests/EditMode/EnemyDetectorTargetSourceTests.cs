using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 적 탐지기가 어빌리티의 대상 공급자 계약을 실제로 구현하는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 사격과 엄폐 어빌리티는 소유 유닛에서 <see cref="ICombatTargetSource"/>를 찾아 대상을 묻는다.
    /// 탐지기가 그 계약을 구현하지 않으면 찾는 것이 null이라 세 어빌리티가 한 번도 활성화되지 않는데,
    /// 오류도 경고도 없어 사용자에게는 유닛이 적 앞에 가만히 서 있는 것으로만 보인다.
    /// 다른 검사들은 가짜 공급자를 붙여 돌기 때문에 이 자리를 잡지 못한다.
    /// </para>
    /// <para>
    /// 탐지는 <see cref="IUnitSpatialRegistry"/>에 묻는 평면 원-겹침이므로, 여기서는 콜라이더 대신
    /// 레지스트리를 직접 만들어 적을 등록한 뒤 감지를 돌린다.
    /// </para>
    /// </remarks>
    public sealed class EnemyDetectorTargetSourceTests
    {
        /// <summary>사격 어빌리티의 식별 태그이다.</summary>
        private static readonly GameplayTag AttackTag = GameplayTag.Parse(UnitAbilityTags.Attack);

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private TestUnitAttributes _unitAttributes;
        private GameplayAbilitySystem _abilitySystem;
        private GameObject _unitObject;
        private EnemyDetector _detector;
        private UnitSpatialRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _unitAttributes = new TestUnitAttributes();
            _unitObject = CreateObject("Attacker");
            var unit = _unitObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime(
                "소총병", 100, new TeamId(1), attackRange: 10f)));
            unit.InitializeUnit();
            _detector = _unitObject.AddComponent<EnemyDetector>();
            _registry = CreateObject("Registry").AddComponent<UnitSpatialRegistry>();
            _detector.InjectSpatialRegistry(_registry);

            _attributes = new AttributeSet();
            _abilitySystem = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes), _unitObject);
            _abilitySystem.GrantAbility(Track(AttackAbilityDefinition.CreateRuntime(UnitAbilityTags.Attack)));
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            _unitAttributes?.Dispose();
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void TheDetectorIsTheTargetSourceTheAbilitiesLookFor()
        {
            Assert.That(
                _unitObject.GetComponent<ICombatTargetSource>(),
                Is.SameAs(_detector),
                "어빌리티가 찾는 계약을 탐지기가 구현하지 않으면 사격과 엄폐가 영영 활성화되지 않는다.");
        }

        [Test]
        public void AttackCanActivateOnceTheDetectorHasATarget()
        {
            var enemy = CreateEnemy(new Vector3(0f, 0f, 5f));

            var detected = _detector.RefreshTarget();

            Assert.That(detected, Is.SameAs(enemy.transform), "사거리 안의 적을 탐지기가 대상으로 잡아야 한다.");
            Assert.That(_detector.CurrentTarget, Is.SameAs(enemy.transform));
            Assert.That(
                _abilitySystem.CanActivate(AttackTag),
                Is.EqualTo(GameplayAbilityActivationResult.Success),
                "탐지기가 잡은 대상이 사격 어빌리티까지 닿아야 한다.");
        }

        [Test]
        public void AttackIsRejectedWhileTheDetectorHasNoTarget()
        {
            Assert.That(_detector.CurrentTarget, Is.Null);
            Assert.That(_abilitySystem.CanActivate(AttackTag), Is.EqualTo(GameplayAbilityActivationResult.Rejected));
        }

        [Test]
        public void ClearingTheDetectorTakesTheTargetAwayFromTheAbility()
        {
            CreateEnemy(new Vector3(0f, 0f, 5f));
            _detector.RefreshTarget();
            Assert.That(_abilitySystem.CanActivate(AttackTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            _detector.ClearTarget();

            Assert.That(_abilitySystem.CanActivate(AttackTag), Is.EqualTo(GameplayAbilityActivationResult.Rejected));
        }

        /// <summary>탐지기가 찾을 수 있고 사격 어빌리티가 피해를 줄 수 있는 적을 세운다.</summary>
        /// <param name="position">적을 세울 좌표이다.</param>
        /// <returns>적의 GameObject이다.</returns>
        private GameObject CreateEnemy(Vector3 position)
        {
            var enemyObject = CreateObject("Enemy");
            enemyObject.transform.position = position;
            var team = enemyObject.AddComponent<TeamMember>();
            team.SetTeam(new TeamId(2));
            _unitAttributes.AttachHealth(enemyObject);
            _registry.Register(team, enemyObject.transform, 0.5f);
            return enemyObject;
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
