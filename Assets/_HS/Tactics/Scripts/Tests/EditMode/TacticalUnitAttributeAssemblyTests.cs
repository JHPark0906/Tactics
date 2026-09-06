using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛 조립이 정의 수치와 육성 진행을 어트리뷰트에 한 자리에서 한 번 투영하는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 최대 체력 = 정의 × 곡선의 체력 배수(레벨), 공격력 = 정의 × 곡선의 공격 배수(레벨), 레벨과 경험치 = 진행 데이터.
    /// 배수는 레벨 1의 값까지 곡선 데이터에서 오며, 곡선이 없으면 곱하지 않는다. 그 규칙을 여기서 고정한다.
    /// </para>
    /// <para>
    /// 유닛의 체력 문은 <see cref="HealthAttributeComponent"/>이다.
    /// 조립은 그 문을 요구 컴포넌트로 데려오며, 없으면 붙인다.
    /// </para>
    /// </remarks>
    public sealed class TacticalUnitAttributeAssemblyTests
    {
        private static readonly GameplayTag DeathTag = GameplayTag.Parse(UnitAbilityTags.Death);
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private readonly List<Object> _createdObjects = new();
        private TestUnitAttributes _attributes;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestPublisher<DeathEvent> _deathPublisher;

        [SetUp]
        public void SetUp()
        {
            _attributes = new TestUnitAttributes();
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            _attributes.Dispose();
        }

        [Test]
        public void TheDefinitionIsProjectedOntoTheAttributes()
        {
            var unit = CreateUnit(maxHealth: 250, attackDamage: 30);

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(unit.Health, Is.InstanceOf<HealthAttributeComponent>());
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(250));
            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(250));
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(30f).Within(0.001f));
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(UnitLevelProgress.StartingLevel));
            Assert.That(attributes.GetBaseValue(_attributes.Xp), Is.EqualTo(UnitLevelProgress.StartingExperience));
        }

        [Test]
        public void TheLevelCurveMultipliesHealthAndAttackFromTheProgressLevel()
        {
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            var progression = new UnitProgressionService();
            progression.Levels.SetLevel(unit.Definition.Id, 3);
            var curve = CreateCurve(health: new[] { 1f, 1.5f, 2f }, attack: new[] { 1f, 2f, 3f });
            unit.ConfigureProgression(progression, curve);

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(400), "레벨 3의 체력 배수 2가 곱해져야 한다.");
            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(400));
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(30f).Within(0.001f), "레벨 3의 공격 배수 3이 곱해져야 한다.");
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f));
        }

        [Test]
        public void LevelOneAlsoComesFromTheCurveData()
        {
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10);
            var curve = CreateCurve(health: new[] { 1.25f }, attack: new[] { 0.5f });
            unit.ConfigureProgression(new UnitProgressionService(), curve);

            unit.InitializeUnit();

            Assert.That(unit.Health.MaxHealth, Is.EqualTo(125), "레벨 1의 배수도 코드가 아니라 곡선이 준다.");
            Assert.That(unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void ExperienceIsProjectedFromTheProgressData()
        {
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10);
            var progression = new UnitProgressionService();
            var curve = CreateCurve(health: new[] { 1f }, attack: new[] { 1f });
            progression.Levels.AddExperience(unit.Definition.Id, 40, curve);
            unit.ConfigureProgression(progression, curve);

            unit.InitializeUnit();

            Assert.That(unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Xp), Is.EqualTo(40f));
        }

        [Test]
        public void ProgressWithoutACurveLeavesTheNumbersUnscaledAndWarns()
        {
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            var progression = new UnitProgressionService();
            progression.Levels.SetLevel(unit.Definition.Id, 3);
            unit.ConfigureProgression(progression, null);
            LogAssert.Expect(LogType.Warning, new Regex("레벨 곡선"));

            unit.InitializeUnit();

            Assert.That(unit.Health.MaxHealth, Is.EqualTo(200), "곡선이 없으면 곱하지 않는다. 코드에 적힌 배수는 없다.");
            Assert.That(unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f));
        }

        [Test]
        public void TheProjectionHappensOnlyOnce()
        {
            var unit = CreateUnit(maxHealth: 250, attackDamage: 10);
            unit.InitializeUnit();
            unit.Health.ApplyDamage(50, null);

            unit.InitializeUnit();

            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(200));
        }

        [Test]
        public void DeathThroughTheNewDoorActivatesTheDeathAbilityWithoutTheBridge()
        {
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10, Track(DeathAbilityDefinition.CreateRuntime()));
            unit.InitializeUnit();

            unit.Health.ApplyDamage(100, null);

            Assert.That(unit.AbilitySystem.System.Tags.HasTag(DeadStateTag), Is.True, "체력 문이 보낸 사망 이벤트만으로 사망 어빌리티가 활성화되어야 한다.");
            Assert.That(unit.AbilitySystem.System.TryGetAbility(DeathTag, out var deathAbility), Is.True);
            Assert.That(deathAbility.ActivationCount, Is.EqualTo(1));
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnAbilitySetWithoutHealthAttributesWarnsAndSkipsHealth()
        {
            var unitObject = Track(new GameObject("Unit"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100)));
            LogAssert.Expect(LogType.Warning, new Regex("체력 어트리뷰트"));

            Assert.DoesNotThrow(() => unit.InitializeUnit());
        }

        /// <summary>어트리뷰트 묶음을 갖춘 정의로 유닛을 만들고 체력 문에 발행자를 넣는다. 조립은 호출자가 한다.</summary>
        private TacticalUnit CreateUnit(int maxHealth, int attackDamage, params GameplayAbilityDefinition[] abilities)
        {
            var unitObject = Track(new GameObject("Unit"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.GetComponent<HealthAttributeComponent>().InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
            unit.SetDefinition(_attributes.CreateUnitDefinition("소총병", maxHealth, attackDamage: attackDamage, abilities: abilities));
            return unit;
        }

        /// <summary>지정한 배수 표를 가진 레벨 곡선을 만든다. 직렬화 필드에 닿을 길이 없어 리플렉션으로 채운다.</summary>
        private UnitLevelCurve CreateCurve(float[] health, float[] attack)
        {
            var curve = Track(ScriptableObject.CreateInstance<UnitLevelCurve>());
            SetField(curve, "healthMultipliers", health);
            SetField(curve, "attackMultipliers", attack);
            return curve;
        }

        /// <summary>비공개 직렬화 필드를 채운다.</summary>
        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했다.");
            field.SetValue(target, value);
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
