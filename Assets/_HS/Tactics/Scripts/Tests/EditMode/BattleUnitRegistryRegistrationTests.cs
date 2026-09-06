using System.Collections.Generic;
using HS.Framework.Foundation.Patterns;
using HS.Tactics.Flow;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 승패 집계 등록이 씬이 바뀌어 서비스가 교체되면 새 서비스를 넘겨주는지,
    /// 그리고 그것이 컨테이너 등록 수명에 달려 있음을 검증한다.
    /// </summary>
    /// <remarks>
    /// 찾아 주는 일은 프레임워크의 <see cref="SceneComponentLocator{TComponent}"/>가 하고 그 자체의 규칙은
    /// 프레임워크 검사가 지킨다. 여기서 지키는 것은 <b>Tactics가 그것을 어떤 수명으로 등록했는가</b>이다.
    /// </remarks>
    public sealed class BattleUnitRegistryRegistrationTests
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _createdObjects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ResolveFindsTheReplacementAfterTheCachedServiceIsDestroyed()
        {
            var locator = CreateLocator();
            var first = CreateService("First");
            Assert.That(locator.Resolve(), Is.SameAs(first));

            Object.DestroyImmediate(first.gameObject);
            var replacement = CreateService("Replacement");

            Assert.That(locator.Resolve(), Is.SameAs(replacement));
        }

        [Test]
        public void TransientRegistrationHandsOutTheReplacementService()
        {
            var locator = CreateLocator();
            var builder = new ContainerBuilder();
            builder.Register<IBattleUnitRegistry>(_ => locator.Resolve(), Lifetime.Transient);
            using var container = builder.Build();
            var first = CreateService("First");
            Assert.That(container.Resolve<IBattleUnitRegistry>(), Is.SameAs(first));

            Object.DestroyImmediate(first.gameObject);
            var replacement = CreateService("Replacement");

            Assert.That(container.Resolve<IBattleUnitRegistry>(), Is.SameAs(replacement));
        }

        // Singleton은 첫 조회 결과를 보관하므로 씬 교체 뒤에도 이전 서비스를 반환한다.
        [Test]
        public void SingletonRegistrationKeepsHandingOutTheDestroyedService()
        {
            var locator = CreateLocator();
            var builder = new ContainerBuilder();
            builder.Register<IBattleUnitRegistry>(_ => locator.Resolve(), Lifetime.Singleton);
            using var container = builder.Build();
            var first = CreateService("First");
            Assert.That(container.Resolve<IBattleUnitRegistry>(), Is.SameAs(first));

            Object.DestroyImmediate(first.gameObject);
            CreateService("Replacement");
            var resolved = container.Resolve<IBattleUnitRegistry>();

            Assert.That(resolved, Is.SameAs(first), "Singleton은 파괴된 첫 서비스를 그대로 넘긴다.");
            Assert.That(resolved as Object == null, Is.True, "넘겨받은 서비스는 Unity 기준으로 이미 없는 것이다.");
        }

        /// <summary>스코프가 등록하는 것과 같은 확인자를 만든다. 비활성 서비스는 사망 이벤트를 구독하지 않으므로 찾지 않는다.</summary>
        private static SceneComponentLocator<BattleOutcomeService> CreateLocator()
        {
            return new SceneComponentLocator<BattleOutcomeService>(FindObjectsInactive.Exclude);
        }

        private BattleOutcomeService CreateService(string objectName)
        {
            var serviceObject = new GameObject(objectName);
            _createdObjects.Add(serviceObject);
            return serviceObject.AddComponent<BattleOutcomeService>();
        }
    }
}
