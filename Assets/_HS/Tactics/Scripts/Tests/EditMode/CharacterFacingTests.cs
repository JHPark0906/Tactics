using System.Collections.Generic;
using HS.Tactics.Character.Movement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>바라보는 방향이 고정 스텝에서 각속도만큼씩 돌고, 무엇을 볼지가 명시적으로 정해지는 것을 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 이 구성요소가 회전을 쓰는 유일한 자리다. 도는 양이 스텝 크기에만 달려 있어야 기기가 빠르든 느리든
    /// 같은 시간에 같은 각을 돈다. 그것이 「기기의 성능이 결과를 바꾸지 않는다」의 회전 층 실물이다.
    /// </para>
    /// <para>
    /// 바라볼 점은 둔 쪽이 지운다. 시간이 지나면 저절로 지워지는 창을 두지 않으므로, 지우지 않으면 남는다는 것도
    /// 함께 고정한다. 그 규칙이 있어야 「누가 언제 지우는가」가 코드에 드러난다.
    /// </para>
    /// </remarks>
    public sealed class CharacterFacingTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ItTurnsAtItsAngularSpeedAndNoFaster()
        {
            var facing = CreateFacing(90f);
            facing.LookAt(new Vector3(5f, 0f, 0f));

            facing.Tick(0.5f);

            Assert.That(
                facing.AngleTo(new Vector3(5f, 0f, 0f)),
                Is.EqualTo(45f).Within(0.01f),
                "초당 90도면 0.5초에 45도만 돈다. 남은 45도는 다음 스텝의 몫이다.");
        }

        [Test]
        public void HalvingTheStepLeavesTheSameAngleAtTheSameTime()
        {
            var coarse = CreateFacing(90f);
            var fine = CreateFacing(90f);
            var target = new Vector3(5f, 0f, 0f);
            coarse.LookAt(target);
            fine.LookAt(target);

            for (var index = 0; index < 4; index++)
            {
                coarse.Tick(0.25f);
                fine.Tick(0.125f);
                fine.Tick(0.125f);
            }

            Assert.That(
                fine.AngleTo(target),
                Is.EqualTo(coarse.AngleTo(target)).Within(0.001f),
                "스텝을 쪼개도 같은 시각에 같은 각이어야 기기 성능이 대응 속도를 바꾸지 않는다.");
        }

        [Test]
        public void ItStopsExactlyOnTheTargetInsteadOfOvershooting()
        {
            var facing = CreateFacing(90f);
            var target = new Vector3(5f, 0f, 0f);
            facing.LookAt(target);

            facing.Tick(10f);

            Assert.That(facing.AngleTo(target), Is.EqualTo(0f).Within(0.01f), "한 스텝에 다 돌 수 있으면 지나치지 않고 멈춘다.");
        }

        [Test]
        public void ALookTargetWinsOverTheMovementDirection()
        {
            var facing = CreateFacing(360f);
            facing.ReportMovementDirection(new Vector3(0f, 0f, 1f));
            facing.LookAt(new Vector3(5f, 0f, 0f));

            facing.Tick(1f);

            Assert.That(facing.AngleTo(new Vector3(5f, 0f, 0f)), Is.EqualTo(0f).Within(0.01f), "조준이 걸린 동안에는 그 점을 본다.");
        }

        [Test]
        public void WithoutALookTargetItFacesTheWayItMoves()
        {
            var facing = CreateFacing(360f);

            facing.ReportMovementDirection(new Vector3(0f, 0f, -1f));
            facing.Tick(1f);

            Assert.That(facing.AngleTo(new Vector3(0f, 0f, -5f)), Is.EqualTo(0f).Within(0.01f), "걷는 동안의 정면은 이동 방향이다.");
        }

        [Test]
        public void ClearingTheLookTargetGivesTheMovementDirectionBack()
        {
            var facing = CreateFacing(360f);
            facing.ReportMovementDirection(new Vector3(0f, 0f, -1f));
            facing.LookAt(new Vector3(5f, 0f, 0f));
            facing.Tick(1f);
            Assert.That(facing.HasLookTarget, Is.True);

            facing.ClearLookTarget();
            facing.Tick(1f);

            Assert.That(facing.HasLookTarget, Is.False);
            Assert.That(
                facing.AngleTo(new Vector3(0f, 0f, -5f)),
                Is.EqualTo(0f).Within(0.01f),
                "조준을 놓으면 다시 걷는 쪽을 본다.");
        }

        [Test]
        public void ALookTargetStaysUntilItIsCleared()
        {
            var facing = CreateFacing(360f);
            facing.LookAt(new Vector3(5f, 0f, 0f));

            for (var index = 0; index < 20; index++)
            {
                facing.Tick(0.1f);
            }

            Assert.That(
                facing.HasLookTarget,
                Is.True,
                "시간이 지나면 저절로 풀리는 창을 두지 않는다. 지우는 쪽이 지워야 누가 언제 놓는지가 드러난다.");
        }

        [Test]
        public void ATurnSpeedBelowOneIsRaisedSoAimingCanFinish()
        {
            var facing = CreateFacing(0f);

            Assert.That(facing.TurnSpeed, Is.EqualTo(1f), "0이면 영영 돌지 못해 조준이 끝나지 않는다.");
        }

        [Test]
        public void AZeroLengthDirectionIsIgnoredInsteadOfClearingTheFacing()
        {
            var facing = CreateFacing(360f);
            facing.ReportMovementDirection(new Vector3(0f, 0f, -1f));
            facing.Tick(1f);

            facing.ReportMovementDirection(Vector3.zero);
            facing.Tick(1f);

            Assert.That(
                facing.AngleTo(new Vector3(0f, 0f, -5f)),
                Is.EqualTo(0f).Within(0.01f),
                "멈춰 선 스텝의 영 변위가 정면을 흔들면 안 된다.");
        }

        /// <summary>지정한 각속도로 원점에 선 방향 구성요소를 만든다. 처음에는 +Z를 본다.</summary>
        private CharacterFacing CreateFacing(float turnSpeed)
        {
            var created = new GameObject("Facing");
            _created.Add(created);
            var facing = created.AddComponent<CharacterFacing>();
            facing.SetTurnSpeed(turnSpeed);
            return facing;
        }
    }
}
