using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>적 탐지의 대상 선정 규칙을 검증한다. 표적은 반경 안에서 가장 가까운 적이다.</summary>
    public sealed class EnemyDetectionTests
    {
        private const float Radius = 20f;

        private readonly List<GameObject> _createdObjects = new();
        private TeamMember _self;

        [SetUp]
        public void SetUp()
        {
            _self = CreateTeamMember("Self", 1, Vector3.zero);
        }

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
        public void SelectsNearestHostileInRange()
        {
            var far = CreateTeamMember("FarEnemy", 2, new Vector3(0f, 0f, 15f));
            var near = CreateTeamMember("NearEnemy", 2, new Vector3(0f, 0f, 5f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { far, near }, Radius);

            Assert.That(selected, Is.SameAs(near));
        }

        [Test]
        public void IgnoresFriendlyCandidates()
        {
            var ally = CreateTeamMember("Ally", 1, new Vector3(0f, 0f, 3f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { ally }, Radius);

            Assert.That(selected, Is.Null, "같은 진영은 대상이 되어서는 안 된다.");
        }

        [Test]
        public void IgnoresCandidatesWithoutAssignedTeam()
        {
            var unassigned = CreateTeamMember("Unassigned", 0, new Vector3(0f, 0f, 3f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { unassigned }, Radius);

            Assert.That(selected, Is.Null, "진영이 지정되지 않은 대상은 중립이라 적대가 아니다.");
        }

        [Test]
        public void IgnoresSelf()
        {
            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { _self }, Radius);

            Assert.That(selected, Is.Null);
        }

        [Test]
        public void SkipsHostileBeyondTheRadius()
        {
            var beyond = CreateTeamMember("BeyondEnemy", 2, new Vector3(0f, 0f, 25f));
            var inRange = CreateTeamMember("InRangeEnemy", 2, new Vector3(0f, 0f, 10f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { beyond, inRange }, Radius);

            Assert.That(selected, Is.SameAs(inRange));
        }

        [Test]
        public void ReturnsNullWhenEveryHostileIsBeyondTheRadius()
        {
            var beyond = CreateTeamMember("BeyondEnemy", 2, new Vector3(0f, 0f, 25f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { beyond }, Radius);

            Assert.That(selected, Is.Null, "반경 밖의 적은 대상이 아니다.");
        }

        [Test]
        public void ReturnsNullForEmptyOrMissingInput()
        {
            Assert.That(
                EnemyTargetSelector.SelectNearestHostileInRange(_self, new TeamMember[0], Radius),
                Is.Null);
            Assert.That(
                EnemyTargetSelector.SelectNearestHostileInRange(_self, null, Radius),
                Is.Null);
            Assert.That(
                EnemyTargetSelector.SelectNearestHostileInRange(null, new[] { _self }, Radius),
                Is.Null);
        }

        [Test]
        public void SkipsNullEntriesInCandidateList()
        {
            var enemy = CreateTeamMember("Enemy", 2, new Vector3(0f, 0f, 6f));

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { null, enemy, null }, Radius);

            Assert.That(selected, Is.SameAs(enemy));
        }

        [Test]
        public void HostileCandidateCheckMatchesTeamRelation()
        {
            var enemy = CreateTeamMember("Enemy", 2, Vector3.zero);
            var ally = CreateTeamMember("Ally", 1, Vector3.zero);

            Assert.That(EnemyTargetSelector.IsHostileCandidate(_self, enemy), Is.True);
            Assert.That(EnemyTargetSelector.IsHostileCandidate(_self, ally), Is.False);
            Assert.That(EnemyTargetSelector.IsHostileCandidate(_self, null), Is.False);
            Assert.That(EnemyTargetSelector.IsHostileCandidate(null, enemy), Is.False);
        }

        private TeamMember CreateTeamMember(string objectName, int teamValue, Vector3 position)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            createdObject.transform.position = position;
            var member = createdObject.AddComponent<TeamMember>();
            member.SetTeam(new TeamId(teamValue));
            return member;
        }
    }
}
