using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
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
    /// 코드에서 직접 지정한 명시 레벨(<see cref="TacticalUnit.ConfigureExplicitLevel"/>)이 플레이어의 육성 진행과
    /// 나란히, 그러나 그것을 이기며 어트리뷰트 투영에 반영되는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전투를 구성하는 쪽이 세우는 적 유닛은 <see cref="UnitProgressionService"/>에 등록된 적이 없다 — 그쪽은 플레이어
    /// 유닛 종류만 추적한다. 그래서 적 유닛의 레벨은 명시 레벨 통로로만 들어오며, 이 파일은 그 통로가 기존 통로와
    /// 같은 규약(조립 전에만 받음, 곡선이 없으면 경고, 시작 레벨 아래로는 내려가지 않음)을 지키는지 고정한다.
    /// </para>
    /// <para>
    /// 명시 레벨은 레벨 값만 주고 곡선은 받지 않는다. 검사에서 곡선이 필요하면 기존 통로
    /// <see cref="TacticalUnit.ConfigureProgression"/>에 진행 없이 곡선만 넘긴다. 기존 통로의 회귀 기준은
    /// <c>TacticalUnitAttributeAssemblyTests</c>가 그대로 든다.
    /// </para>
    /// </remarks>
    public sealed class TacticalUnitExplicitLevelTests
    {
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
        public void AnExplicitLevelScalesHealthAndAttackThroughTheInjectedCurve()
        {
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            var curve = CreateCurve(health: new[] { 1f, 1.5f, 2f }, attack: new[] { 1f, 2f, 3f });
            unit.ConfigureProgression(null, curve);
            unit.ConfigureExplicitLevel(3);

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(400), "명시 레벨 3의 체력 배수 2가 곱해져야 한다.");
            Assert.That(unit.Health.CurrentHealth, Is.EqualTo(400));
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(30f).Within(0.001f), "명시 레벨 3의 공격 배수 3이 곱해져야 한다.");
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f), "어트리뷰트에 적히는 레벨도 명시 레벨이어야 한다.");
            Assert.That(attributes.GetBaseValue(_attributes.Xp), Is.EqualTo(UnitLevelProgress.StartingExperience), "명시 레벨 유닛은 육성 대상이 아니므로 경험치는 시작값이다.");
        }

        [Test]
        public void AnExplicitLevelSetBeforeInjectionStillUsesTheInjectedCurve()
        {
            // 명시 레벨은 레벨 값만 들고 곡선은 주입에서만 오므로, 주입(ConfigureProgression)보다 먼저 불러도 결과가
            // 같아야 한다. 곡선까지 함께 받는 모양이었다면 뒤따르는 주입이 곡선을 덮어써 이 순서에서 배수가 사라진다.
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            var curve = CreateCurve(health: new[] { 1f, 1.5f, 2f }, attack: new[] { 1f, 2f, 3f });
            unit.ConfigureExplicitLevel(3);
            unit.ConfigureProgression(null, curve);

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f), "주입이 뒤따라도 명시 레벨은 살아남아야 한다.");
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(400), "주입된 곡선의 레벨 3 배수가 그대로 곱해져야 한다.");
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void TheExplicitLevelWinsOverTheProgressionLevel()
        {
            // 플레이어 유닛은 명시 레벨을 받을 일이 없으므로 둘이 실제로 겹치지는 않지만, 겹치면 스테이지 데이터가 권위다.
            // 곡선을 다섯 칸으로 두어 레벨 3(배수 2)과 레벨 5(배수 3)가 수치에서도 갈리게 한다.
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            var curve = CreateCurve(health: new[] { 1f, 1.5f, 2f, 2.5f, 3f }, attack: new[] { 1f, 2f, 3f, 4f, 5f });
            var progression = new UnitProgressionService();
            progression.Levels.SetLevel(unit.Definition.Id, 5);
            unit.ConfigureProgression(progression, curve);
            unit.ConfigureExplicitLevel(3);

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f), "육성 진행의 레벨 5가 아니라 명시 레벨 3이 적혀야 한다.");
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(400), "배수도 명시 레벨 3의 값(2)이어야 한다 — 레벨 5면 600이다.");
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(30f).Within(0.001f));
            Assert.That(attributes.GetBaseValue(_attributes.Xp), Is.EqualTo(UnitLevelProgress.StartingExperience));
        }

        [Test]
        public void ProgressionExperienceDoesNotLeakIntoAnExplicitLevelUnit()
        {
            // 플레이어가 같은 종류를 키우고 있어도 그 경험치가 전투를 구성하는 쪽이 세운 유닛의 Xp로 새면 안 된다.
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10);
            var progression = new UnitProgressionService();
            var curve = CreateCurve(health: new[] { 1f }, attack: new[] { 1f });
            progression.Levels.AddExperience(unit.Definition.Id, 40, curve);
            unit.ConfigureProgression(progression, curve);
            unit.ConfigureExplicitLevel(1);

            unit.InitializeUnit();

            Assert.That(
                unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Xp),
                Is.EqualTo(UnitLevelProgress.StartingExperience),
                "육성 진행에 쌓인 경험치 40이 명시 레벨 유닛에 적히면 안 된다.");
        }

        [Test]
        public void AnExplicitLevelWithoutACurveLeavesTheNumbersUnscaledAndWarns()
        {
            var unit = CreateUnit(maxHealth: 200, attackDamage: 10);
            unit.ConfigureExplicitLevel(3);
            LogAssert.Expect(LogType.Warning, new Regex("레벨 곡선"));

            unit.InitializeUnit();

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(200), "곡선이 없으면 곱하지 않는다. 코드에 적힌 배수는 없다.");
            Assert.That(attributes.GetBaseValue(_attributes.AttackPower), Is.EqualTo(10f).Within(0.001f));
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(3f), "곡선이 없어도 레벨 자체는 어트리뷰트에 적힌다.");
        }

        [Test]
        public void ConfiguringAfterAssemblyWarnsAndIsIgnored()
        {
            // ConfigureProgression과 같은 규약이다 — 수치의 투영은 조립 때 한 번만 하므로 뒤늦은 지정은 반영되지 않는다.
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10);
            unit.InitializeUnit();
            LogAssert.Expect(LogType.Warning, new Regex("이미 조립되어"));

            unit.ConfigureExplicitLevel(9);

            var attributes = unit.AbilitySystem.System.Attributes;
            Assert.That(attributes.GetBaseValue(_attributes.Level), Is.EqualTo(UnitLevelProgress.StartingLevel), "조립이 끝난 뒤의 지정은 레벨을 바꾸지 않는다.");
            Assert.That(unit.Health.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void ALevelBelowTheStartingLevelIsRaisedToTheStartingLevel()
        {
            // 곡선은 스스로도 시작 레벨 아래를 붙잡고, 레벨 어트리뷰트 정의도 하한이 1이라 배수와 어트리뷰트만 보면
            // 유닛이 올리지 않아도 검사가 통과한다. 유닛이 올린 값을 그대로 드러내는 자리는 곡선 없음 경고의 레벨 숫자
            // 하나뿐이므로, 곡선 없이 세워 그 경고가 레벨 1을 말하는지 본다 — 올리지 않았다면 레벨 0이라고 말한다.
            var unit = CreateUnit(maxHealth: 100, attackDamage: 10);
            unit.ConfigureExplicitLevel(0);
            LogAssert.Expect(LogType.Warning, new Regex("레벨 1이 수치에 반영되지 않는다"));

            unit.InitializeUnit();

            Assert.That(
                unit.AbilitySystem.System.Attributes.GetBaseValue(_attributes.Level),
                Is.EqualTo(UnitLevelProgress.StartingLevel),
                "레벨 0은 시작 레벨로 올려 적어야 한다.");
        }

        /// <summary>어트리뷰트 묶음을 갖춘 정의로 유닛을 만들고 체력 문에 발행자를 넣는다. 조립은 호출자가 한다.</summary>
        private TacticalUnit CreateUnit(int maxHealth, int attackDamage)
        {
            var unitObject = Track(new GameObject("Unit"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.GetComponent<HealthAttributeComponent>().InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
            unit.SetDefinition(_attributes.CreateUnitDefinition("소총병", maxHealth, attackDamage: attackDamage));
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
