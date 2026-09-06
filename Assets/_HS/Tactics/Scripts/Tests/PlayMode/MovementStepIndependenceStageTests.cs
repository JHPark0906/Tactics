using System.Collections.Generic;
using System.Reflection;
using HS.Tactics.Character.Movement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// 실제 PlanarCharacterMover를 서로 다른 스텝 크기로 구동해 같은 시각의 변위를 비교한다.
    /// 경로는 두 점을 반환하는 PathPlanner 대역을 사용해 경로 추종과 속도 적분을 격리한다.
    /// 이동기를 비활성으로 두고 Tick만 호출해 Unity FixedUpdate가 추가로 이동시키지 않게 한다.
    /// 시간 간격과 수치는 이진 정확하며 가속·감속 전환점이 두 스텝 계획의 경계에 놓인다. 회전은 비교하지 않는다.
    /// </summary>
    public sealed class MovementStepIndependenceStageTests
    {
        private const float MaxSpeed = 4f;
        private const float Acceleration = 2f;
        private const float CoarseStep = 0.25f;
        private const float FineStep = 0.125f;
        private const int CoarseSteps = 20;
        private static readonly Vector3 Displacement = new(0f, 0f, 12f);

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
        public void HalvingTheStepLeavesEveryCoarseBoundaryUnchanged()
        {
            var coarse = CreateMover("Coarse", new Vector3(-10f, 0f, -6f));
            var fine = CreateMover("Fine", new Vector3(10f, 0f, -6f));

            Assert.That(coarse.MoveTo(coarse.transform.position + Displacement), Is.True, "무대 확인: 굵은 쪽이 목적지를 받지 못했다.");
            Assert.That(fine.MoveTo(fine.transform.position + Displacement), Is.True, "무대 확인: 잔 쪽이 목적지를 받지 못했다.");

            var coarseStart = coarse.CurrentLogicPosition;
            var fineStart = fine.CurrentLogicPosition;

            var coarseArrivalStep = -1;
            var fineArrivalStep = -1;
            for (var step = 1; step <= CoarseSteps; step++)
            {
                coarse.Tick(CoarseStep);
                fine.Tick(FineStep);
                fine.Tick(FineStep);

                var time = step * CoarseStep;
                var coarseOffset = coarse.CurrentLogicPosition - coarseStart;
                var fineOffset = fine.CurrentLogicPosition - fineStart;
                Assert.That(fineOffset.x, Is.EqualTo(coarseOffset.x), $"t={time} 의 x 변위가 스텝 크기에 따라 다르다.");
                Assert.That(fineOffset.z, Is.EqualTo(coarseOffset.z), $"t={time} 의 z 변위가 스텝 크기에 따라 다르다.");

                if (coarseArrivalStep < 0 && coarse.HasReachedDestination)
                {
                    coarseArrivalStep = step;
                }

                if (fineArrivalStep < 0 && fine.HasReachedDestination)
                {
                    fineArrivalStep = step;
                }
            }

            var finalOffset = coarse.CurrentLogicPosition - coarseStart;
            Assert.That(finalOffset.z, Is.EqualTo(Displacement.z), "무대 확인: t=5.0 에 12미터를 다 갔다.");
            Assert.That(coarseArrivalStep, Is.GreaterThan(0), "무대 확인: 굵은 쪽이 도착하지 않았다.");
            Assert.That(
                fineArrivalStep,
                Is.EqualTo(coarseArrivalStep),
                "도착이 판정되는 굵은 스텝 경계가 스텝 크기에 따라 다르다.");
        }

        /// <summary>
        /// 곧은 두 점짜리 경로를 내는 이동기를 세운다. 비활성이라 FixedUpdate 가 걷지 않고 검사가
        /// Tick 을 준다.
        /// </summary>
        private PlanarCharacterMover CreateMover(string objectName, Vector3 start)
        {
            var moverObject = new GameObject(objectName);
            _createdObjects.Add(moverObject);
            moverObject.transform.position = start;

            var mover = moverObject.AddComponent<PlanarCharacterMover>();
            mover.enabled = false;
            mover.Initialize(null);
            mover.SetSpeed(MaxSpeed);
            SetPrivateField(mover, "acceleration", Acceleration);
            SetPrivateField(mover, "autoBraking", true);
            mover.SetPathPlanner(StraightLinePlan);
            return mover;
        }

        /// <summary>곧은 두 점(출발·목적지)짜리 경로를 그대로 돌려준다.</summary>
        private static bool StraightLinePlan(Vector3 from, Vector3 destination, List<Vector3> corners)
        {
            corners.Add(from);
            corners.Add(destination);
            return true;
        }

        /// <summary>인스펙터 전용 조정값을 검사에서 정확한 수치로 맞출 때만 리플렉션으로 건드린다.</summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"무대 확인: '{fieldName}' 필드를 찾지 못했다.");
            field.SetValue(target, value);
        }
    }
}
