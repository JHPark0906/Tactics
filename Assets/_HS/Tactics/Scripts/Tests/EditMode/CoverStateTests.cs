using HS.Framework.AI.BehaviourTree;
using HS.Framework.Tests.Support;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// CoverPoint의 점유, UnitCoverState의 엄폐 상태와 CoverSensor의 후보 조회를 검증한다.
    /// </summary>
    public sealed class CoverStateTests
    {
        private GameObject _unitObject;
        private GameObject _coverObject;
        private GameObject _threatObject;
        private UnitCoverState _coverState;
        private CoverPoint _coverPoint;

        [SetUp]
        public void SetUp()
        {
            _unitObject = new GameObject("Unit");
            _coverObject = new GameObject("Cover");
            _threatObject = new GameObject("Threat");
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _coverPoint = _coverObject.AddComponent<CoverPoint>();

            // 엄폐물은 북쪽에서 오는 위협을 막고, 위협은 북쪽에 둔다.
            _coverObject.transform.position = Vector3.zero;
            _coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _threatObject.transform.position = new Vector3(0f, 0f, 20f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_unitObject);
            Object.DestroyImmediate(_coverObject);
            Object.DestroyImmediate(_threatObject);
        }

        [Test]
        public void CoverPointRejectsSecondOccupant()
        {
            var otherObject = new GameObject("Other");
            try
            {
                Assert.That(_coverPoint.TryOccupy(_unitObject), Is.True);
                Assert.That(_coverPoint.IsOccupied, Is.True);
                Assert.That(_coverPoint.TryOccupy(otherObject), Is.False);
                Assert.That(_coverPoint.TryOccupy(_unitObject), Is.True, "같은 점유자의 재점유는 허용한다.");
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void ReleaseByNonOccupantIsIgnored()
        {
            var otherObject = new GameObject("Other");
            try
            {
                _coverPoint.TryOccupy(_unitObject);

                _coverPoint.Release(otherObject);

                Assert.That(_coverPoint.IsOccupied, Is.True);
                Assert.That(_coverPoint.Occupant, Is.SameAs(_unitObject));
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void ClaimingCoverDoesNotMeanBeingInCover()
        {
            _unitObject.transform.position = new Vector3(0f, 0f, -10f);

            Assert.That(_coverState.ClaimCover(_coverPoint), Is.True);
            Assert.That(_coverState.HasCoverClaim, Is.True);
            Assert.That(_coverState.IsInCover, Is.False, "확보만 하고 도착하지 않았으면 엄폐 중이 아니다.");
        }

        [Test]
        public void UnitIsCoveredAfterArrivingAtClaimedCover()
        {
            _coverState.ClaimCover(_coverPoint);
            _unitObject.transform.position = Vector3.zero;

            Assert.That(_coverState.IsInCover, Is.True);
        }


        [Test]
        public void ClaimingAnotherCoverReleasesThePreviousOne()
        {
            var secondObject = new GameObject("SecondCover");
            try
            {
                var secondCover = secondObject.AddComponent<CoverPoint>();
                _coverState.ClaimCover(_coverPoint);

                Assert.That(_coverState.ClaimCover(secondCover), Is.True);
                Assert.That(_coverPoint.IsOccupied, Is.False, "이전 지점을 놓아 다른 유닛이 쓸 수 있어야 한다.");
                Assert.That(_coverState.ClaimedCover, Is.SameAs(secondCover));
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void ClaimFailsWhenCoverIsTakenByAnotherUnit()
        {
            var otherObject = new GameObject("OtherUnit");
            try
            {
                _coverPoint.TryOccupy(otherObject);

                Assert.That(_coverState.ClaimCover(_coverPoint), Is.False);
                Assert.That(_coverState.HasCoverClaim, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void ReleasingCoverClearsStateAndOccupancy()
        {
            _coverState.ClaimCover(_coverPoint);

            _coverState.ReleaseCover();

            Assert.That(_coverState.HasCoverClaim, Is.False);
            Assert.That(_coverState.IsInCover, Is.False);
            Assert.That(_coverPoint.IsOccupied, Is.False);
        }

        [Test]
        public void SensorSelectsUnoccupiedCoverValidAgainstThreat()
        {
            var sensor = StraightLineCoverTravel.AttachSensor(_unitObject);
            sensor.SetCoverPoints(new[] { _coverPoint });
            _unitObject.transform.position = new Vector3(0f, 0f, -5f);

            Assert.That(sensor.TryFindCover(_threatObject.transform.position, out var found), Is.True);
            Assert.That(found, Is.SameAs(_coverPoint));

            _coverPoint.TryOccupy(_threatObject);

            Assert.That(sensor.TryFindCover(_threatObject.transform.position, out _), Is.False);
        }

        [Test]
        public void MaintainCoverKeepsRunningOnlyWhileCoverIsValid()
        {
            var context = new BehaviourContext();
            context.SetValue(UnitBehaviourKeys.Target, _threatObject.transform);
            var node = new MaintainCoverBehaviour(_coverState);

            Assert.That(node.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            _coverState.ClaimCover(_coverPoint);
            _unitObject.transform.position = Vector3.zero;

            Assert.That(
                node.Tick(context),
                Is.EqualTo(BehaviourStatus.Running),
                "엄폐 유지는 계속되는 상태이므로 실행 중을 반환한다.");
        }
    }
}
