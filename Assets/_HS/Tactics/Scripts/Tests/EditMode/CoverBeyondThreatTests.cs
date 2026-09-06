using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 양면 엄폐를 켰을 때 유닛이 엄폐하러 적을 지나쳐 달려가지 않는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>양면 엄폐는 판정을 바꾸지 않고 후보를 넓힌다.</b> 한 방향만 막는 엄폐물은 적 뒤에 있으면
    /// 우리 유닛에게 방향이 반대라 무효이고 그래서 후보에 들어오지 않는다. 축만 맞으면 유효한 양면 엄폐물은
    /// <b>적 뒤에 있어도 후보가 된다.</b>
    /// </para>
    /// <para>
    /// <b>거리 조건 둘은 이것을 막지 못한다.</b> 탐색 반경은 유닛에서 얼마나 먼지만 보고,
    /// 사거리 조건은 그 자리에서 쏠 수 있는지만 본다. <b>적 뒤 몇 미터는 두 조건을 모두 통과한다.</b>
    /// 그래서 "위협을 지나친 자리인가"를 따로 본다.
    /// </para>
    /// <para>
    /// 검사는 엄폐 확보 어빌리티를 통해 한다. 규칙만 따로 두드리면 그 규칙이 실제 선택 경로에
    /// 걸려 있지 않게 되어도 통과한다.
    /// </para>
    /// </remarks>
    public sealed class CoverBeyondThreatTests
    {
        /// <summary>엄폐 지점에서 위협까지의 거리보다 넉넉한 사거리이다.</summary>
        private const float AttackRange = 30f;

        private readonly List<GameObject> _createdObjects = new();
        private readonly List<CoverAbilityTestUnit> _units = new();

        private Transform _threat;

        [SetUp]
        public void SetUp()
        {
            _threat = CreateObject("Threat", Vector3.zero).transform;
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
        public void ATwoSidedCoverBehindTheEnemyIsPassedOverForOneOnTheOwnSide()
        {
            var behindEnemy = CreateCover(new Vector3(0f, 0f, 5f), Vector3.forward);
            var ownSide = CreateCover(new Vector3(0f, 0f, -8f), Vector3.forward);
            var unit = CreateUnit(new Vector3(0f, 0f, -5f), behindEnemy, ownSide);

            Assert.That(unit.TakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(
                unit.CoverState.ClaimedCover,
                Is.SameAs(ownSide),
                "적 뒤 엄폐물이 더 가까워도 고르면 안 된다. 엄폐하러 적진으로 뛰어드는 그림이 된다.");
        }

        [Test]
        public void ATwoSidedCoverBehindTheEnemyIsNotChosenEvenWhenItIsTheOnlyOne()
        {
            var behindEnemy = CreateCover(new Vector3(0f, 0f, 5f), Vector3.forward);
            var unit = CreateUnit(new Vector3(0f, 0f, -5f), behindEnemy);

            Assert.That(
                unit.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "달리 갈 곳이 없어도 적을 지나치지는 않는다. 엄폐를 포기하고 다른 분기로 넘어가야 한다.");
            Assert.That(unit.CoverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void ATwoSidedCoverBesideTheEnemyIsStillUsable()
        {
            // 적과 나란한 자리이며 지나친 것이 아니다. 옆으로 벌어진 자리까지 막으면 쓸 자리가 너무 줄어든다.
            var beside = CreateCover(new Vector3(4f, 0f, -3f), Vector3.forward);
            var unit = CreateUnit(new Vector3(0f, 0f, -5f), beside);

            Assert.That(unit.TakeCover(), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(unit.CoverState.ClaimedCover, Is.SameAs(beside));
        }

        /// <summary>
        /// 위협을 지나친 후보는 양면 엄폐 설정과 무관하게 제외되는지 검증한다.
        /// 위협 뒤의 후보는 탐색 반경과 사거리 조건만으로는 걸러지지 않으므로 별도의 지나침 조건이 필요하다.
        /// </summary>
        [Test]
        public void TheRuleAppliesEvenWhileTheSwitchIsOff()
        {
            var behindEnemyFacingBack = CreateCover(new Vector3(0f, 0f, 5f), Vector3.back);
            var unit = CreateUnit(new Vector3(0f, 0f, -5f), behindEnemyFacingBack);

            Assert.That(
                unit.TakeCover(),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "맞은편 진영용 엄폐물은 양면을 켜지 않아도 이쪽 유닛에게 유효하다. 그것을 고르면 적진으로 뛰어든다.");
            Assert.That(unit.CoverState.HasCoverClaim, Is.False);
        }

        /// <summary>지정한 자리와 방향으로 엄폐 지점을 만든다.</summary>
        /// <param name="position">엄폐 지점을 놓을 좌표이다.</param>
        /// <param name="forward">마커의 정면 방향이다.</param>
        /// <returns>만든 엄폐 지점이다.</returns>
        private CoverPoint CreateCover(Vector3 position, Vector3 forward)
        {
            var coverObject = CreateObject("Cover", position);
            coverObject.transform.rotation = Quaternion.LookRotation(forward);
            var coverPoint = coverObject.AddComponent<CoverPoint>();
            return coverPoint;
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
