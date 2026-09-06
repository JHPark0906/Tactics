using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>엄폐물이 평면 위에서 막는 영역과, 파괴됐을 때 발행하는 이벤트를 검증한다.</summary>
    /// <remarks>
    /// 체력이 실제로 0이 되어 죽은 상태 태그 구독이 <see cref="CoverPoint.MarkDestroyed"/>를 부르는
    /// 경로는 여기서 보지 않는다 — 그 경로는 어빌리티 활성화를 검증하는 자리(<c>CoverAbilityTests</c> 등)의
    /// 일이다. 여기서는 <see cref="CoverPoint.MarkDestroyed"/> 자체가 내는 값만 본다.
    /// </remarks>
    public sealed class CoverPointBoundsAndDestroyedEventTests
    {
        private TestUnitAttributes _attributes;
        private GameObject _coverObject;
        private CoverPoint _cover;

        [SetUp]
        public void SetUp()
        {
            _attributes = new TestUnitAttributes();
            _coverObject = new GameObject("Cover");
            _cover = _attributes.AttachCover(_coverObject);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes.Dispose();
            if (_coverObject != null)
            {
                Object.DestroyImmediate(_coverObject);
            }
        }

        [Test]
        public void BoundsIsCenteredOnTheTransformWithTheConfiguredHalfExtents()
        {
            _coverObject.transform.position = new Vector3(3f, 1f, 4f);

            var bounds = _cover.Bounds;

            Assert.That(bounds.Center.X, Is.EqualTo(3f));
            Assert.That(bounds.Center.Z, Is.EqualTo(4f));
            // 영역 반크기의 기본값은 (0.5, 0.5)이다.
            Assert.That(bounds.HalfExtents.X, Is.EqualTo(0.5f));
            Assert.That(bounds.HalfExtents.Z, Is.EqualTo(0.5f));
        }

        [Test]
        public void BoundsFollowsTheTransformsForwardDirection()
        {
            _coverObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var bounds = _cover.Bounds;

            Assert.That(bounds.Forward.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(bounds.Forward.Z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void DestroyedDoesNotFireBeforeMarkDestroyed()
        {
            var fired = false;
            _cover.Destroyed += () => fired = true;

            Assert.That(fired, Is.False);
        }

        [Test]
        public void MarkDestroyedFiresDestroyedExactlyOnce()
        {
            var fireCount = 0;
            _cover.Destroyed += () => fireCount++;

            _cover.MarkDestroyed();

            Assert.That(fireCount, Is.EqualTo(1));
            Assert.That(_cover.IsDestroyed, Is.True, "무대 확인: 이벤트를 보낼 때는 이미 파괴 표시가 되어 있어야 한다.");
        }

        [Test]
        public void IsDestroyedIsAlreadyTrueWhenTheEventHandlerRuns()
        {
            // 구독자가 "지금 이 엄폐물을 장애물 목록에 넣어도 되는가"를 이벤트 핸들러 안에서 그대로 물을 수
            // 있어야 한다 — MarkDestroyed가 상태를 먼저 바꾸고 나중에 알리는 순서를 지키는지 본다.
            var wasDestroyedDuringEvent = false;
            _cover.Destroyed += () => wasDestroyedDuringEvent = _cover.IsDestroyed;

            _cover.MarkDestroyed();

            Assert.That(wasDestroyedDuringEvent, Is.True);
        }
    }
}
