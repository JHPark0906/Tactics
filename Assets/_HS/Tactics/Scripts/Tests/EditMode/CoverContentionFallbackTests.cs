using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐 지점을 빼앗긴 유닛이 그 틱을 버리지 않고 차선을 고르는지, 그리고 차선에도 같은 규칙이 걸리는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>진 유닛은 기다리지 않는다.</b> 후보를 훑을 때 점유된 지점을 먼저 걸러 내므로,
    /// 남이 먼저 고른 자리는 애초에 후보가 아니다. 그래서 경쟁에 진 유닛도 <b>같은 틱에</b> 남은 자리 중
    /// 가장 가까운 곳을 고른다. 진 뒤에 한 틱을 버리는 구조였다면 경쟁이 잦은 유닛이 계속 뒤처졌을 것이다.
    /// </para>
    /// <para>
    /// <b>차선에도 "쏠 수 있는 엄폐만"이 걸려야 한다.</b> 첫 후보에만 사거리를 확인하고 차선은 통과시키면,
    /// 자리를 빼앗긴 유닛이 <b>숨었지만 쏘지는 못하는 자리</b>에 앉는다. 이 상태는 화면에서 "유닛이 엄폐물 뒤에 가만히 있는 것"으로만 보여 원인을 찾기 어렵다.
    /// </para>
    /// </remarks>
    public sealed class CoverContentionFallbackTests
    {
        /// <summary>이 사거리를 넘는 자리에서는 위협을 쏠 수 없다.</summary>
        private const float AttackRange = 30f;

        private readonly List<GameObject> _createdObjects = new();
        private readonly List<CoverAbilityTestUnit> _units = new();

        private Transform _threat;

        [SetUp]
        public void SetUp()
        {
            _threat = CreateObject("Threat", new Vector3(0f, 0f, 20f)).transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var unit in _units)
            {
                unit.Dispose();
            }

            _units.Clear();
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
        public void TheUnitThatLosesTheRaceTakesTheNextCoverInTheSameTick()
        {
            var nearCover = CreateCoverPoint(Vector3.zero);
            var otherCover = CreateCoverPoint(new Vector3(3f, 0f, 0f));
            var winner = CreateUnit(new Vector3(0f, 0f, -5f), nearCover, otherCover);
            var loser = CreateUnit(new Vector3(1f, 0f, -5f), nearCover, otherCover);

            Assert.That(winner.TakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(
                winner.CoverState.ClaimedCover,
                Is.SameAs(nearCover),
                "이긴 유닛이 가장 가까운 자리를 갖는다.");

            Assert.That(
                loser.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Success),
                "자리를 빼앗겨도 그 틱에 남은 자리를 골라야 한다. 한 틱을 버리면 경쟁이 잦은 유닛이 계속 뒤처진다.");
            Assert.That(loser.CoverState.ClaimedCover, Is.SameAs(otherCover));
        }

        [Test]
        public void TheFallbackCoverMustStillBeAbleToShootTheThreat()
        {
            var contestedCover = CreateCoverPoint(Vector3.zero);

            // 진 유닛에게 더 가깝지만, 여기서는 위협까지 32미터라 쏠 수 없다.
            var closeButUnshootable = CreateCoverPoint(new Vector3(0f, 0f, -12f));

            // 조금 더 멀지만 여기서는 쏠 수 있다.
            var fartherButShootable = CreateCoverPoint(new Vector3(4f, 0f, 0f));

            var winner = CreateUnit(
                new Vector3(0f, 0f, -5f), contestedCover, closeButUnshootable, fartherButShootable);
            var loser = CreateUnit(
                new Vector3(0f, 0f, -6f), contestedCover, closeButUnshootable, fartherButShootable);

            winner.TakeCover();

            Assert.That(loser.TakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(
                loser.CoverState.ClaimedCover,
                Is.SameAs(fartherButShootable),
                "차선을 고를 때도 그 자리에서 쏠 수 있어야 한다. 더 가깝다고 쏘지 못하는 자리를 고르면 숨고 안 쏘는 유닛이 된다.");
        }

        [Test]
        public void NoCoverIsChosenWhenEveryFreeOneCannotShoot()
        {
            var contestedCover = CreateCoverPoint(Vector3.zero);
            var unshootableCover = CreateCoverPoint(new Vector3(0f, 0f, -12f));
            var winner = CreateUnit(new Vector3(0f, 0f, -5f), contestedCover, unshootableCover);
            var loser = CreateUnit(new Vector3(0f, 0f, -6f), contestedCover, unshootableCover);

            winner.TakeCover();

            Assert.That(
                loser.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "쏠 수 있는 빈 자리가 없으면 엄폐를 고르지 않고 거부해야 트리가 다른 분기로 넘어간다.");
            Assert.That(loser.CoverState.HasCoverClaim, Is.False);
        }

        /// <summary>위협을 막아 주는 방향으로 놓인 엄폐 지점을 만든다.</summary>
        /// <param name="position">엄폐 지점을 놓을 좌표이다.</param>
        /// <returns>만든 엄폐 지점이다.</returns>
        private CoverPoint CreateCoverPoint(Vector3 position)
        {
            var coverObject = CreateObject("Cover", position);
            coverObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            return coverObject.AddComponent<CoverPoint>();
        }

        /// <summary>주어진 엄폐 지점들을 볼 수 있는 유닛을 만든다.</summary>
        /// <param name="position">유닛을 세울 좌표이다.</param>
        /// <param name="coverPoints">이 유닛이 볼 수 있는 엄폐 지점들이다.</param>
        /// <returns>만든 유닛이다.</returns>
        private CoverAbilityTestUnit CreateUnit(Vector3 position, params CoverPoint[] coverPoints)
        {
            var unit = new CoverAbilityTestUnit(position, AttackRange, _threat, coverPoints);
            _units.Add(unit);
            return unit;
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <param name="position">오브젝트를 놓을 좌표이다.</param>
        /// <returns>만든 GameObject이다.</returns>
        private GameObject CreateObject(string objectName, Vector3 position)
        {
            var created = new GameObject(objectName);
            created.transform.position = position;
            _createdObjects.Add(created);
            return created;
        }
    }
}
