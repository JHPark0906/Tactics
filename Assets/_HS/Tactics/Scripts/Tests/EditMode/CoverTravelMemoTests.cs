using System.Collections.Generic;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 거리 결과를 캐시하면 같은 출발점과 후보를 다시 재지 않으면서 같은 후보를 선택하는지 검증한다.
    /// 특히 모든 후보에 도달할 수 없는 경우의 반복 조회와 출발점·측정 수단·후보 변경 시 재계산을 확인한다.
    /// </summary>
    public sealed class CoverTravelMemoTests
    {
        /// <summary>위협은 북쪽 멀리 두고, 엄폐 후보는 그 사이에 흩는다.</summary>
        private static readonly Vector3 ThreatPosition = new(0f, 0f, 20f);

        /// <summary>후보에서 위협까지 허용하는 거리이며 모든 후보가 통과할 만큼 넉넉하다.</summary>
        private const float AttackRange = 30f;

        private readonly List<GameObject> _createdObjects = new();

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
        public void StandingStillDoesNotMeasureTheSameCandidateTwice()
        {
            var counter = new CountingTravel(isReachable: true);
            var sensor = CreateSensor(counter, out _, new Vector3(-3f, 0f, 5f), new Vector3(3f, 0f, 6f));

            sensor.TryFindCover(ThreatPosition, out _, AttackRange);
            var afterFirst = counter.CallCount;
            sensor.TryFindCover(ThreatPosition, out _, AttackRange);

            Assert.That(afterFirst, Is.GreaterThan(0), "첫 번째에는 실제로 재야 비교할 것이 생긴다.");
            Assert.That(
                counter.CallCount,
                Is.EqualTo(afterFirst),
                "같은 자리에서 같은 후보를 다시 재면 매 고정 스텝 같은 계산이 되풀이된다.");
        }

        [Test]
        public void NoReachableCandidateStillMeasuresOnlyOnceWhileStandingStill()
        {
            var counter = new CountingTravel(isReachable: false);
            var sensor = CreateSensor(
                counter,
                out _,
                new Vector3(-3f, 0f, 5f),
                new Vector3(3f, 0f, 6f),
                new Vector3(0f, 0f, 7f),
                new Vector3(5f, 0f, 8f));

            sensor.TryFindCover(ThreatPosition, out _, AttackRange);
            var afterFirst = counter.CallCount;
            for (var tick = 0; tick < 9; tick++)
            {
                sensor.TryFindCover(ThreatPosition, out _, AttackRange);
            }

            Assert.That(
                afterFirst,
                Is.EqualTo(4),
                "아무도 닿지 않으면 멈출 근거가 없으므로 첫 번에는 후보를 전부 잰다.");
            Assert.That(
                counter.CallCount,
                Is.EqualTo(afterFirst),
                "제자리에 선 유닛이 열 스텝 동안 같은 계산을 열 번 하면 그것이 이 변경이 없애려는 비용이다.");
        }

        [Test]
        public void MovingMeasuresAgain()
        {
            var counter = new CountingTravel(isReachable: true);
            var sensor = CreateSensor(counter, out var unitObject, new Vector3(-3f, 0f, 5f));

            sensor.TryFindCover(ThreatPosition, out _, AttackRange);
            var afterFirst = counter.CallCount;
            unitObject.transform.position = new Vector3(0f, 0f, 1f);
            sensor.TryFindCover(ThreatPosition, out _, AttackRange);

            Assert.That(
                counter.CallCount,
                Is.GreaterThan(afterFirst),
                "자리가 달라지면 거리도 달라지므로 기억한 값을 쓰면 틀린 거리로 고르게 된다.");
        }

        [Test]
        public void ChangingTheCandidatesMeasuresAgain()
        {
            var counter = new CountingTravel(isReachable: true);
            var sensor = CreateSensor(counter, out _, new Vector3(-3f, 0f, 5f));
            var addedPoint = CreateCoverPoint(new Vector3(2f, 0f, 4f));

            sensor.TryFindCover(ThreatPosition, out _, AttackRange);
            var afterFirst = counter.CallCount;
            sensor.SetCoverPoints(new[] { addedPoint });
            sensor.TryFindCover(ThreatPosition, out _, AttackRange);

            Assert.That(
                counter.CallCount,
                Is.GreaterThan(afterFirst),
                "후보가 바뀌었는데 기억을 그대로 쓰면 새 후보를 한 번도 재지 않는다.");
        }

        [Test]
        public void TheChosenSpotIsWhatTheSelectionRuleWouldPick()
        {
            var counter = new CountingTravel(isReachable: true);
            var sensor = CreateSensor(
                counter,
                out var unitObject,
                new Vector3(-3f, 0f, 5f),
                new Vector3(1f, 0f, 3f),
                new Vector3(6f, 0f, 9f));

            Assert.That(sensor.TryFindCover(ThreatPosition, out var chosen, AttackRange), Is.True);

            var candidates = new List<CoverCandidate>();
            foreach (var coverPoint in sensor.CoverPoints)
            {
                candidates.Add(coverPoint.ToCandidate());
            }

            var expectedIndex = CoverSelection.SelectBestIndex(
                candidates,
                unitObject.transform.position,
                ThreatPosition,
                sensor.SearchRadius,
                AttackRange,
                StraightLineCoverTravel.TryMeasure);

            Assert.That(expectedIndex, Is.Not.EqualTo(CoverSelection.NoCoverIndex));
            Assert.That(
                chosen,
                Is.SameAs(sensor.CoverPoints[expectedIndex]),
                "덜 재는 대신 다른 자리를 고르면 화면에서는 '가끔 이상한 데로 간다'로만 보인다.");
        }

        [Test]
        public void RememberedMeasurementsDoNotChangeTheChoiceAcrossTicks()
        {
            var counter = new CountingTravel(isReachable: true);
            var sensor = CreateSensor(
                counter,
                out _,
                new Vector3(-3f, 0f, 5f),
                new Vector3(1f, 0f, 3f),
                new Vector3(6f, 0f, 9f));

            sensor.TryFindCover(ThreatPosition, out var firstChoice, AttackRange);
            sensor.TryFindCover(ThreatPosition, out var secondChoice, AttackRange);

            Assert.That(secondChoice, Is.SameAs(firstChoice), "같은 상황에서 고르는 자리는 스텝마다 같아야 한다.");
        }

        /// <summary>지정한 좌표에 엄폐 지점을 둔 센서를 만든다.</summary>
        /// <param name="travel">거리를 재는 수단이며 호출 횟수를 센다.</param>
        /// <param name="unitObject">센서를 붙인 오브젝트이며 원점에 선다.</param>
        /// <param name="coverPositions">엄폐 지점을 둘 좌표들이다.</param>
        /// <returns>만든 센서이다.</returns>
        private CoverSensor CreateSensor(
            CountingTravel travel,
            out GameObject unitObject,
            params Vector3[] coverPositions)
        {
            unitObject = CreateObject("Unit");
            unitObject.transform.position = Vector3.zero;

            var sensor = unitObject.AddComponent<CoverSensor>();
            sensor.SetTravelDistance(travel.TryMeasure);

            var coverPoints = new List<CoverPoint>();
            foreach (var coverPosition in coverPositions)
            {
                coverPoints.Add(CreateCoverPoint(coverPosition));
            }

            sensor.SetCoverPoints(coverPoints);
            return sensor;
        }

        /// <summary>지정한 좌표에 엄폐 지점을 만든다.</summary>
        /// <param name="position">엄폐 지점을 둘 좌표이다.</param>
        /// <returns>만든 엄폐 지점이다.</returns>
        private CoverPoint CreateCoverPoint(Vector3 position)
        {
            var coverObject = CreateObject("Cover");
            coverObject.transform.position = position;
            return coverObject.AddComponent<CoverPoint>();
        }

        /// <summary>정리 목록에 등록된 빈 오브젝트를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 오브젝트이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>
        /// 직선거리를 반환하면서 호출 횟수를 기록하는 측정 대역이다.
        /// SetTravelDistance로 센서에 연결해 거리 재계산 횟수를 검사한다.
        /// </summary>
        private sealed class CountingTravel
        {
            private readonly bool _isReachable;

            /// <summary>측정 수단을 만든다.</summary>
            /// <param name="isReachable">끝까지 갈 수 있다고 답할지 여부이다.</param>
            internal CountingTravel(bool isReachable)
            {
                _isReachable = isReachable;
            }

            /// <summary>지금까지 불린 횟수이다.</summary>
            internal int CallCount { get; private set; }

            /// <summary>직선거리를 이동 거리로 돌려주며 호출 횟수를 센다.</summary>
            /// <param name="from">출발 월드 좌표이다.</param>
            /// <param name="to">도착 월드 좌표이다.</param>
            /// <param name="distance">잰 거리이다.</param>
            /// <returns>만들 때 정한 도달 가능 여부이다.</returns>
            internal bool TryMeasure(Vector3 from, Vector3 to, out float distance)
            {
                CallCount++;
                distance = Vector3.Distance(from, to);
                return _isReachable;
            }
        }
    }
}
