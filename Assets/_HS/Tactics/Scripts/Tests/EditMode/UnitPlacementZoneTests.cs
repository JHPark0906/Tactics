using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>씬에 배치한 구역 컴포넌트가 오브젝트 위치를 반영해 판정하는지 검증한다.</summary>
    public sealed class UnitPlacementZoneTests
    {
        private GameObject _zoneObject;

        [TearDown]
        public void TearDown()
        {
            if (_zoneObject != null)
            {
                Object.DestroyImmediate(_zoneObject);
                _zoneObject = null;
            }
        }

        [Test]
        public void ZoneFollowsTheObjectPositionWithoutLifecycleCallbacks()
        {
            var zone = CreateZone();
            var area = zone.Area;
            Assert.That(area.IsEmpty, Is.False, "기본 크기만으로도 판정 가능한 구역이어야 한다.");
            Assert.That(zone.Contains(Vector3.zero), Is.True);

            _zoneObject.transform.position = new Vector3(100f, 0f, 0f);

            Assert.That(
                zone.Contains(Vector3.zero),
                Is.False,
                "구역을 옮기면 이전 좌표는 더 이상 구역 안이 아니어야 한다.");
            Assert.That(zone.Contains(new Vector3(100f, 0f, 0f)), Is.True);
        }

        [Test]
        public void ZoneClampsPositionsBackIntoItsArea()
        {
            var zone = CreateZone();

            var clamped = zone.ClampToArea(new Vector3(500f, 0f, 500f));

            Assert.That(zone.Contains(clamped), Is.True);
        }

        [Test]
        public void SpawnRotationFollowsTheObjectRotation()
        {
            var zone = CreateZone();
            var facing = Quaternion.Euler(0f, 90f, 0f);

            _zoneObject.transform.rotation = facing;

            Assert.That(Quaternion.Angle(zone.SpawnRotation, facing), Is.LessThan(0.01f));
        }

        private UnitPlacementZone CreateZone()
        {
            _zoneObject = new GameObject("PlacementZone");
            return _zoneObject.AddComponent<UnitPlacementZone>();
        }
    }
}
