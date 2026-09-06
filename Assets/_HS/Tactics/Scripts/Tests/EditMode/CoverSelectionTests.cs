using System.Collections.Generic;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>엄폐 지점 선택 규칙을 검증한다.</summary>
    public sealed class CoverSelectionTests
    {
        [Test]
        public void ACloserExteriorApproachIsNotSkippedBecauseItsCoverCenterIsFarther()
        {
            var candidates = new[]
            {
                new CoverCandidate(new Vector3(0f, 0f, 4f), false),
                new CoverCandidate(new Vector3(0f, 0f, 6f), false)
            };
            bool Measure(Vector3 from, Vector3 to, out float distance)
            {
                distance = to.z == 4f ? 3f : 2f;
                return true;
            }

            Assert.That(CoverSelection.SelectBestIndex(candidates, Vector3.zero, new Vector3(0f, 0f, 20f),
                10f, measureTravelDistance: Measure), Is.EqualTo(1));
        }

        private static readonly Vector3 Seeker = Vector3.zero;
        private static readonly Vector3 Threat = new(0f, 0f, 20f);

        /// <summary>사거리 제한을 걸지 않을 때 넘기는 값이다.</summary>
        private const float MaxThreatDistance = float.PositiveInfinity;

        /// <summary>표준 후보를 만든다.</summary>
        private static CoverCandidate FacingThreat(Vector3 position, bool isOccupied = false)
        {
            return new CoverCandidate(position, isOccupied);
        }

        /// <summary>
        /// 이동 거리 측정 수단을 주입하면 실제 이동 거리가 짧은 후보를 선택하는지 검증한다.
        /// 측정 수단이 없을 때의 직선거리 선택과 벽을 우회하는 이동 거리 선택을 비교한다.
        /// </summary>
        [Test]
        public void TheNearestByStraightLineIsNotChosenWhenWalkingThereTakesLonger()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(0f, 0f, 3f)),   // 직선 3, 벽을 돌아가면 15
                FacingThreat(new Vector3(8f, 0f, 0f))    // 직선 8, 그대로 8
            };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 20f),
                Is.EqualTo(0),
                "이동 거리 측정 수단이 없으면 직선거리가 가까운 후보를 고른다.");

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 20f, MaxThreatDistance, WalkAroundTheWall),
                Is.EqualTo(1),
                "걸어갈 거리로 고르면 돌아가지 않아도 되는 자리를 고른다.");
        }

        [Test]
        public void ACandidateThatCannotBeWalkedToIsNotChosen()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(0f, 0f, 3f)),   // 가장 가깝지만 닿을 수 없다
                FacingThreat(new Vector3(8f, 0f, 0f))
            };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 20f, MaxThreatDistance, UnreachableNearOne),
                Is.EqualTo(1),
                "못 가는 자리를 아주 먼 것으로 다루면 다른 후보가 없을 때 그것이 뽑혀 유닛이 멈춘다.");
        }

        [Test]
        public void NoCoverIsChosenWhenEveryCandidateIsOutOfReach()
        {
            var candidates = new List<CoverCandidate> { FacingThreat(new Vector3(0f, 0f, 3f)) };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 20f, MaxThreatDistance, NothingIsReachable),
                Is.EqualTo(CoverSelection.NoCoverIndex),
                "갈 수 있는 자리가 하나도 없으면 고르지 않아야 트리가 다른 분기로 넘어간다.");
        }

        [Test]
        public void StraightTravelStillChoosesTheNearestCover()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(0f, 0f, 2f)),
                FacingThreat(new Vector3(0f, 0f, 5f)),
                FacingThreat(new Vector3(0f, 0f, 7f))
            };
            bool MeasureStraightLine(Vector3 from, Vector3 to, out float distance)
            {
                distance = Vector3.Distance(from, to);
                return true;
            }

            var chosen = CoverSelection.SelectBestIndex(
                candidates, Seeker, Threat, 20f, MaxThreatDistance, MeasureStraightLine);

            Assert.That(chosen, Is.EqualTo(0));
        }

        /// <summary>가장 가까운 자리가 벽 뒤에 있어 크게 돌아가야 하는 전장을 흉내 낸다.</summary>
        private static bool WalkAroundTheWall(Vector3 from, Vector3 to, out float distance)
        {
            var straight = Vector3.Distance(from, to);
            distance = Mathf.Approximately(to.z, 3f) ? 15f : straight;
            return true;
        }

        /// <summary>가장 가까운 자리에만 닿을 수 없는 전장을 흉내 낸다.</summary>
        private static bool UnreachableNearOne(Vector3 from, Vector3 to, out float distance)
        {
            distance = Vector3.Distance(from, to);
            return !Mathf.Approximately(to.z, 3f);
        }

        /// <summary>어디에도 닿을 수 없는 전장을 흉내 낸다.</summary>
        private static bool NothingIsReachable(Vector3 from, Vector3 to, out float distance)
        {
            distance = 0f;
            return false;
        }

        [Test]
        public void NoCoverIsSelectedFromEmptyOrNullList()
        {
            Assert.That(
                CoverSelection.SelectBestIndex(null, Seeker, Threat, 10f),
                Is.EqualTo(CoverSelection.NoCoverIndex));
            Assert.That(
                CoverSelection.SelectBestIndex(new List<CoverCandidate>(), Seeker, Threat, 10f),
                Is.EqualTo(CoverSelection.NoCoverIndex));
        }

        [Test]
        public void CandidatesBeyondSearchRadiusAreExcluded()
        {
            var candidates = new List<CoverCandidate> { FacingThreat(new Vector3(0f, 0f, 30f)) };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 10f),
                Is.EqualTo(CoverSelection.NoCoverIndex));
        }

        [Test]
        public void OccupiedCandidatesAreExcluded()
        {
            var candidates = new List<CoverCandidate> { FacingThreat(new Vector3(0f, 0f, 5f), isOccupied: true) };

            Assert.That(
                CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 10f),
                Is.EqualTo(CoverSelection.NoCoverIndex));
        }

        [Test]
        public void NearestValidCandidateIsSelected()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(0f, 0f, 8f)),
                FacingThreat(new Vector3(0f, 0f, 3f)),
                FacingThreat(new Vector3(0f, 0f, 6f))
            };

            Assert.That(CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 10f), Is.EqualTo(1));
        }

        [Test]
        public void EarlierCandidateWinsWhenDistancesAreEqual()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(3f, 0f, 0f)),
                FacingThreat(new Vector3(-3f, 0f, 0f))
            };

            Assert.That(CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 10f), Is.EqualTo(0));
        }

        [Test]
        public void SelectionComparesDistanceOnHorizontalPlane()
        {
            var candidates = new List<CoverCandidate>
            {
                FacingThreat(new Vector3(0f, 40f, 4f)),
                FacingThreat(new Vector3(0f, 0f, 6f))
            };

            Assert.That(CoverSelection.SelectBestIndex(candidates, Seeker, Threat, 10f), Is.EqualTo(0));
        }
    }
}
