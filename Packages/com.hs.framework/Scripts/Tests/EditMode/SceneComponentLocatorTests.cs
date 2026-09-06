using System.Collections.Generic;
using HS.Framework.Foundation.Patterns;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 씬 구성요소 확인자가 찾은 것을 다시 쓰고, 파괴되면 다시 찾으며, 없으면 진짜 null을 주는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 확인자가 있는 까닭은 씬에 놓이는 것이 컨테이너보다 늦게 생기기 때문이다. 그래서 <b>파괴된 것을 붙들지 않는
    /// 것</b>과 <b>없을 때 진짜 null을 주는 것</b> 둘이 이 물건의 전부다. 앞의 것이 깨지면 다음 씬의 사용자가 죽은
    /// 대상에 붙고, 뒤의 것이 깨지면 받는 쪽이 그것을 인터페이스로 담는 순간 <c>== null</c> 이 거짓이 되어
    /// 이미 없는 것을 살아 있는 것으로 다룬다.
    /// </para>
    /// <para>
    /// 비활성을 찾을지는 쓰는 쪽이 정하는 값이라 두 방향을 다 본다. 검사 전용 구성요소를 쓰므로 다른 검사가 씬에
    /// 남긴 것에 흔들리지 않는다.
    /// </para>
    /// </remarks>
    public sealed class SceneComponentLocatorTests
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
        public void ResolveFindsTheComponentPlacedInTheScene()
        {
            var target = CreateTarget("Only");

            Assert.That(CreateLocator().Resolve(), Is.SameAs(target));
        }

        [Test]
        public void TheFoundComponentIsHandedOutAgainWithoutSearchingTheSceneTwice()
        {
            var locator = CreateLocator();
            var first = CreateTarget("First");
            Assert.That(locator.Resolve(), Is.SameAs(first));

            CreateTarget("Second");

            Assert.That(
                locator.Resolve(),
                Is.SameAs(first),
                "살아 있는 것을 캐시해 두므로 나중에 생긴 것을 찾으러 씬을 다시 뒤지지 않는다.");
        }

        [Test]
        public void ResolveFindsTheReplacementAfterTheCachedComponentIsDestroyed()
        {
            var locator = CreateLocator();
            var first = CreateTarget("First");
            Assert.That(locator.Resolve(), Is.SameAs(first));

            Object.DestroyImmediate(first.gameObject);
            var replacement = CreateTarget("Replacement");

            Assert.That(
                locator.Resolve(),
                Is.SameAs(replacement),
                "파괴된 것을 붙들고 있으면 다음 씬의 사용자가 죽은 대상에 붙는다.");
        }

        [Test]
        public void ADestroyedComponentWithNoReplacementBecomesARealNull()
        {
            var locator = CreateLocator();
            var only = CreateTarget("Only");
            Assert.That(locator.Resolve(), Is.SameAs(only));

            Object.DestroyImmediate(only.gameObject);

            Assert.That(
                ReferenceEquals(locator.Resolve(), null),
                Is.True,
                "파괴된 Unity 객체를 그대로 넘기면 받는 쪽이 인터페이스로 담는 순간 없는 것을 있는 것으로 다룬다.");
        }

        [Test]
        public void AnEmptySceneGivesNull()
        {
            Assert.That(ReferenceEquals(CreateLocator().Resolve(), null), Is.True);
        }

        [Test]
        public void AnInactiveComponentIsFoundOnlyWhenTheSearchIncludesIt()
        {
            var target = CreateTarget("Inactive");
            target.gameObject.SetActive(false);

            Assert.That(
                CreateLocator().Resolve(),
                Is.Null,
                "꺼져 있는 동안 자기 일을 하지 않는 대상은 찾지 않기로 할 수 있다.");
            Assert.That(
                new SceneComponentLocator<LocatorTarget>(FindObjectsInactive.Include).Resolve(),
                Is.SameAs(target),
                "꺼져 있어도 그 씬에서 그것 하나뿐인 출처는 찾아야 한다.");
        }

        /// <summary>비활성인 것은 찾지 않는 확인자를 만든다. 두 방향 가운데 이쪽이 검사 대부분의 바탕이다.</summary>
        private static SceneComponentLocator<LocatorTarget> CreateLocator()
        {
            return new SceneComponentLocator<LocatorTarget>(FindObjectsInactive.Exclude);
        }

        /// <summary>검사 전용 구성요소를 붙인 오브젝트를 세운다.</summary>
        private LocatorTarget CreateTarget(string objectName)
        {
            var created = new GameObject(objectName);
            _createdObjects.Add(created);
            return created.AddComponent<LocatorTarget>();
        }

        /// <summary>이 검사만 씬에 놓는 구성요소이다. 다른 검사가 남긴 것과 섞이지 않게 하려고 따로 둔다.</summary>
        private sealed class LocatorTarget : MonoBehaviour
        {
        }
    }
}
