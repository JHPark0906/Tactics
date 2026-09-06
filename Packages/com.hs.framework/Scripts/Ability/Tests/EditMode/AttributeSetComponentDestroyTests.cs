using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Attributes;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 파괴된 뒤의 어트리뷰트 집합 접근이 새 집합을 만들지 않고, 그 사실을 알리는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 집합은 첫 사용 때 만드는 지연 조립이라, 파괴 뒤의 접근이 그 조립을 다시 타면 죽은 오브젝트가
    /// 시작값 그대로의 새 집합을 갖게 된다. 여기서는 파괴 전의 집합이 닫힌 채 그대로 돌아오는지,
    /// 한 번도 만들지 않았으면 시작 어트리뷰트 없는 빈 집합이 돌아오는지, 경고가 한 번만 나는지 본다.
    /// </para>
    /// <para>
    /// 에디터는 플레이 모드가 아닐 때 OnDestroy를 부르지 않으므로 리플렉션으로 직접 부른다.
    /// </para>
    /// </remarks>
    public sealed class AttributeSetComponentDestroyTests
    {
        private readonly List<Object> _createdObjects = new();
        private AttributeDefinition _maxHealth;
        private AttributeDefinition _health;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
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
        }

        [Test]
        public void AccessAfterDestroyReturnsTheSameClosedSetInsteadOfANewOne()
        {
            var component = CreateComponentWithStartingSet();
            var before = component.Attributes;
            Assert.That(before.GetCurrentValue(_health), Is.EqualTo(100f).Within(0.001f), "파괴 전에는 시작값으로 시작한다.");
            before.SetBaseValue(_health, 0f);
            var notifications = 0;
            using var subscription = before.Changed.Subscribe(_ => notifications++);

            InvokeOnDestroy(component);
            LogAssert.Expect(LogType.Warning, new Regex("AttributeSetComponent"));
            var after = component.Attributes;

            Assert.That(after, Is.SameAs(before), "파괴 뒤에는 새 집합을 만들지 않고 쓰던 집합을 돌려준다.");
            Assert.That(after.GetCurrentValue(_health), Is.Zero, "죽어서 0이 된 체력이 시작값으로 되살아나면 안 된다.");

            after.SetBaseValue(_health, 50f);
            Assert.That(notifications, Is.Zero, "닫힌 집합은 값이 바뀌어도 알리지 않는다.");
        }

        [Test]
        public void AccessAfterDestroyWithoutPriorUseGivesAClosedEmptySet()
        {
            var component = CreateComponentWithStartingSet();

            InvokeOnDestroy(component);
            LogAssert.Expect(LogType.Warning, new Regex("AttributeSetComponent"));
            var attributes = component.Attributes;

            Assert.That(attributes, Is.Not.Null, "null을 돌려주면 부르는 쪽이 그 자리에서 죽는다.");
            Assert.That(attributes.Count, Is.Zero, "파괴 뒤에 시작 어트리뷰트를 갖추면 죽은 오브젝트가 새 값을 갖는다.");
            Assert.That(attributes.Contains(_health), Is.False);
            Assert.That(component.Attributes, Is.SameAs(attributes), "닫힌 빈 집합도 한 번만 만든다.");
        }

        [Test]
        public void TheWarningIsRaisedOnlyOncePerComponent()
        {
            var component = CreateComponentWithStartingSet();
            _ = component.Attributes;
            InvokeOnDestroy(component);
            LogAssert.Expect(LogType.Warning, new Regex("AttributeSetComponent"));

            _ = component.Attributes;
            _ = component.Attributes;
            _ = component.Attributes;

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>시작 어트리뷰트 묶음(최대 체력·체력)을 연결한 컴포넌트를 만든다.</summary>
        private AttributeSetComponent CreateComponentWithStartingSet()
        {
            var actor = Track(new GameObject("Actor"));
            var component = actor.AddComponent<AttributeSetComponent>();
            var definition = Track(AttributeSetDefinition.CreateRuntime(new[]
            {
                new AttributeSetDefinition.Entry(_maxHealth),
                new AttributeSetDefinition.Entry(_health)
            }));
            var field = typeof(AttributeSetComponent).GetField(
                "attributeSets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "AttributeSetComponent.attributeSets 필드를 찾지 못했다.");
            field.SetValue(component, new List<AttributeSetDefinition> { definition });
            return component;
        }

        /// <summary>에디터가 부르지 않는 OnDestroy를 직접 부른다.</summary>
        private static void InvokeOnDestroy(AttributeSetComponent component)
        {
            var method = typeof(AttributeSetComponent).GetMethod(
                "OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "AttributeSetComponent.OnDestroy 메서드를 찾지 못했다.");
            method.Invoke(component, null);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
