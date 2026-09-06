using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Pathfinding;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>전장 경계가 오브젝트의 위치·회전·크기를 그대로 반영하는지 검증한다.</summary>
    /// <remarks>
    /// <c>UnitPlacementZone</c>과 같은 이유로 값을 <c>Awake</c>에 기대지 않고 매번 계산하므로, 수명주기
    /// 콜백이 오지 않는 EditMode에서도 그대로 값을 읽을 수 있다 — 그 성질 자체를 이 검사가 확인한다
    /// (컴포넌트를 붙인 직후 <c>Awake</c> 없이 바로 값을 읽는다).
    /// </remarks>
    public sealed class BattleBoundsTests
    {
        private GameObject _boundsObject;
        private BattleBounds _bounds;

        [SetUp]
        public void SetUp()
        {
            _boundsObject = new GameObject("Bounds");
            _bounds = _boundsObject.AddComponent<BattleBounds>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_boundsObject != null)
            {
                Object.DestroyImmediate(_boundsObject);
            }
        }

        [Test]
        public void BoundsIsCenteredOnTheObjectWithTheDefaultHalfExtents()
        {
            _boundsObject.transform.position = new Vector3(-5f, 0f, 8f);

            var bounds = _bounds.Bounds;

            Assert.That(bounds.Center.X, Is.EqualTo(-5f));
            Assert.That(bounds.Center.Z, Is.EqualTo(8f));
            Assert.That(bounds.HalfExtents.X, Is.EqualTo(12f));
            Assert.That(bounds.HalfExtents.Z, Is.EqualTo(12f));
        }

        [Test]
        public void MovingTheObjectMovesTheBoundsWithoutAnyLifecycleCallback()
        {
            var before = _bounds.Bounds.Center;
            _boundsObject.transform.position += new Vector3(10f, 0f, 0f);
            var after = _bounds.Bounds.Center;

            Assert.That(after.X, Is.EqualTo(before.X + 10f));
        }

        [Test]
        public void RotatingTheObjectRotatesTheBoundsForward()
        {
            _boundsObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var forward = _bounds.Bounds.Forward;

            Assert.That(forward.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(forward.Z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void APointWellInsideIsContained()
        {
            var bounds = _bounds.Bounds;

            Assert.That(PlanarGeometry.Contains(bounds, new PlanarPosition(0f, 0f)), Is.True);
        }

        [Test]
        public void APointFarOutsideIsNotContained()
        {
            var bounds = _bounds.Bounds;

            Assert.That(PlanarGeometry.Contains(bounds, new PlanarPosition(100f, 100f)), Is.False);
        }
    }
}
