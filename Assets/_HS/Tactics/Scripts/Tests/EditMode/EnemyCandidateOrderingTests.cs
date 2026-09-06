using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 탐지 후보의 정렬 결과가 입력 순서와 무관한지 검증한다.
    /// 거리가 같아도 좌표 순으로 차례를 정해 후보 수집 순서가 표적 선택을 바꾸지 않게 한다.
    /// </summary>
    public sealed class EnemyCandidateOrderingTests
    {
        /// <summary>거리를 재는 기준 좌표이다.</summary>
        private static readonly Vector3 Origin = Vector3.zero;

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
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
        public void TheSameCandidatesInADifferentOrderProduceTheSameOrder()
        {
            var near = CreateCandidate(new Vector3(1f, 0f, 0f));
            var middle = CreateCandidate(new Vector3(4f, 0f, 0f));
            var far = CreateCandidate(new Vector3(9f, 0f, 0f));

            var firstOrdering = Sorted(near, middle, far);
            var secondOrdering = Sorted(far, near, middle);
            var thirdOrdering = Sorted(middle, far, near);

            Assert.That(firstOrdering, Is.EqualTo(secondOrdering));
            Assert.That(firstOrdering, Is.EqualTo(thirdOrdering));
            Assert.That(firstOrdering[0], Is.SameAs(near), "가장 가까운 후보가 앞에 온다.");
        }

        [Test]
        public void TiesAtTheSameDistanceAreBrokenByXThenZ()
        {
            // 원점에서 거리가 모두 같아, 정렬이 없으면 입력 순서가 그대로 결과가 된다.
            var lowXLowZ = CreateCandidate(new Vector3(-3f, 0f, -4f));
            var lowXHighZ = CreateCandidate(new Vector3(-3f, 0f, 4f));
            var highX = CreateCandidate(new Vector3(3f, 0f, 4f));

            var firstOrdering = Sorted(highX, lowXHighZ, lowXLowZ);
            var secondOrdering = Sorted(lowXHighZ, lowXLowZ, highX);

            Assert.That(firstOrdering, Is.EqualTo(secondOrdering));
            Assert.That(
                firstOrdering,
                Is.EqualTo(new[] { lowXLowZ, lowXHighZ, highX }),
                "거리가 같으면 x가, x도 같으면 z가 차례를 정한다.");
        }

        [Test]
        public void TheChosenCandidateDoesNotDependOnTheInputOrder()
        {
            // 가장 가까운 후보가 둘이고 거리가 완전히 같은, 정렬이 없으면 갈리는 상황이다.
            var tieLeft = CreateCandidate(new Vector3(-2f, 0f, 0f));
            var tieRight = CreateCandidate(new Vector3(2f, 0f, 0f));
            var far = CreateCandidate(new Vector3(0f, 0f, 10f));

            var chosenFromOneOrder = Sorted(tieRight, far, tieLeft)[0];
            var chosenFromAnotherOrder = Sorted(far, tieLeft, tieRight)[0];

            Assert.That(chosenFromOneOrder, Is.SameAs(chosenFromAnotherOrder));
            Assert.That(chosenFromOneOrder, Is.SameAs(tieLeft), "동점은 x가 작은 쪽이 이긴다.");
        }

        [Test]
        public void DistanceIsMeasuredFromTheGivenOrigin()
        {
            var nearOrigin = CreateCandidate(new Vector3(1f, 0f, 0f));
            var nearElsewhere = CreateCandidate(new Vector3(9f, 0f, 0f));
            var candidates = new List<TeamMember> { nearElsewhere, nearOrigin };

            EnemyCandidateOrdering.Sort(candidates, new Vector3(10f, 0f, 0f));

            Assert.That(
                candidates[0],
                Is.SameAs(nearElsewhere),
                "기준 좌표가 바뀌면 차례도 그 기준으로 다시 정해진다.");
        }

        [Test]
        public void SortingHandlesEmptyAndMissingLists()
        {
            var empty = new List<TeamMember>();

            Assert.That(() => EnemyCandidateOrdering.Sort(empty, Origin), Throws.Nothing);
            Assert.That(() => EnemyCandidateOrdering.Sort(null, Origin), Throws.Nothing);
            Assert.That(empty, Is.Empty);
        }

        /// <summary>주어진 차례로 후보를 담아 정렬한 결과를 돌려준다.</summary>
        /// <param name="candidates">정렬할 후보들이다.</param>
        /// <returns>정렬된 후보 목록이다.</returns>
        private static List<TeamMember> Sorted(params TeamMember[] candidates)
        {
            var list = new List<TeamMember>(candidates);
            EnemyCandidateOrdering.Sort(list, Origin);
            return list;
        }

        /// <summary>지정한 좌표에 놓인 후보를 만든다.</summary>
        /// <param name="position">후보를 놓을 좌표이다.</param>
        /// <returns>만든 후보의 진영 구성요소이다.</returns>
        private TeamMember CreateCandidate(Vector3 position)
        {
            var candidateObject = new GameObject("Candidate");
            candidateObject.transform.position = position;
            _createdObjects.Add(candidateObject);
            return candidateObject.AddComponent<TeamMember>();
        }
    }
}
