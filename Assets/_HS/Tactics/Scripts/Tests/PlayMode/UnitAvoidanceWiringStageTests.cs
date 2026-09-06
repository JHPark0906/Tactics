using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Character.Movement;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// 유닛 조립이 이동기에 다른 유닛 조회 수단을 연결하고 실제 공간 레지스트리의 원으로 이동을 막는지 검증한다.
    /// 차단 유닛은 위치와 반지름만 직접 등록하고, 이동기에는 직선 PathPlanner 대역을 주입한다.
    /// 원 충돌의 정확한 좌표 계산, 경로 계획과 유닛 자체 등록은 이 파일의 범위 밖이다.
    /// </summary>
    public sealed class UnitAvoidanceWiringStageTests
    {
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
        public void TheUnitAssemblyWiresANearbyUnitsQuery()
        {
            var unitObject = new GameObject("Unit");
            _createdObjects.Add(unitObject);
            unitObject.transform.position = new Vector3(0f, 0f, -6f);
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;

            LogAssert.Expect(LogType.Warning, new Regex("유닛 정의가 없어"));
            LogAssert.Expect(LogType.Warning, new Regex("행동 트리 에셋이 없어"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.InitializeUnit();

            Assert.That(mover.HasNearbyUnitsQuery, Is.True, "유닛 조립이 이동기에 다른 유닛을 찾는 방법을 끼워야 한다.");
        }

        [Test]
        public void AMovingUnitIsStoppedByANeighborRegisteredInTheRealRegistry()
        {
            var registry = new GameObject("Registry").AddComponent<UnitSpatialRegistry>();
            _createdObjects.Add(registry.gameObject);

            // 막는 쪽은 자리와 반지름만 있으면 된다 — TacticalUnit이 아니라 레지스트리에 직접 등록한다.
            var blockerObject = new GameObject("Blocker");
            _createdObjects.Add(blockerObject);
            blockerObject.transform.position = new Vector3(0f, 0f, 0f);
            var blockerTeam = blockerObject.AddComponent<TeamMember>();
            registry.Register(blockerTeam, blockerObject.transform, 0.5f);

            var moverObject = new GameObject("Mover");
            _createdObjects.Add(moverObject);
            moverObject.transform.position = new Vector3(0f, 0f, -10f);
            var moverTeam = moverObject.AddComponent<TeamMember>();
            var mover = moverObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;
            mover.SetSpeed(4f);

            mover.Initialize(null);
            mover.SetPathPlanner(StraightLinePlan);

            var nearbyBuffer = new List<TeamMember>();
            mover.SetNearbyUnitsQuery((sweep, results) =>
            {
                results.Clear();
                nearbyBuffer.Clear();
                registry.CollectOverlapping(sweep, nearbyBuffer);
                foreach (var candidate in nearbyBuffer)
                {
                    if (candidate == moverTeam)
                    {
                        continue;
                    }

                    if (registry.TryGetCircle(candidate, out var circle))
                    {
                        results.Add(circle);
                    }
                }
            });

            Assert.That(mover.MoveTo(new Vector3(0f, 0f, 10f)), Is.True, "무대 확인: 목적지를 받지 못했다.");
            for (var step = 0; step < 40; step++)
            {
                mover.Tick(0.1f);
            }

            // 이 이동기는 SetObstacles를 부르지 않아 자신의 반지름은 0이다. 그래서 막는 원의 반지름
            // 0.5가 그대로 합산 반지름이 되고, 자를 때 더하는 여유(0.01)만큼 밖에서 멈춘다 — 자리는
            // 스텝 크기와 무관하게 이 값으로 고정된다(장애물 자르기가 걸음마다 자르는 것이지
            // 겹친 뒤에 밀어내는 것이 아니라서, 큰 스텝이라도 그 안에서 정확한 진입 지점을 찾는다).
            var finalPlanar = PlanarPosition.FromWorld(mover.CurrentLogicPosition);
            Assert.That(finalPlanar.Z, Is.EqualTo(-0.51f).Within(0.02f), "가운데를 지키는 이웃의 원 경계(반지름 0.5) 바로 밖에서 멈춰야 한다.");
            Assert.That(
                PlanarPosition.Distance(finalPlanar, new PlanarPosition(0f, 0f)),
                Is.GreaterThanOrEqualTo(0.5f),
                "합산 반지름 0.5 안으로 파고들면(관통) 안 된다.");
        }

        /// <summary>곧은 두 점(출발·목적지)짜리 경로를 그대로 돌려준다.</summary>
        private static bool StraightLinePlan(Vector3 from, Vector3 destination, List<Vector3> corners)
        {
            corners.Add(from);
            corners.Add(destination);
            return true;
        }
    }
}
