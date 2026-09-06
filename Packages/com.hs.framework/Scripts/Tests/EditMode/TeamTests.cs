using HS.Framework.Gameplay.Teams;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>진영 식별자의 상등 비교와 관계 판정 규칙을 검증한다.</summary>
    public sealed class TeamTests
    {
        [Test]
        public void TeamIdsWithSameValueAreEqual()
        {
            var first = new TeamId(3);
            var second = new TeamId(3);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void TeamIdsWithDifferentValuesAreNotEqual()
        {
            Assert.That(new TeamId(1) == new TeamId(2), Is.False);
            Assert.That(new TeamId(1) != new TeamId(2), Is.True);
        }

        [Test]
        public void DefaultTeamIdIsNotAssigned()
        {
            Assert.That(TeamId.None.IsAssigned, Is.False);
            Assert.That(new TeamId(0).IsAssigned, Is.False);
            Assert.That(new TeamId(1).IsAssigned, Is.True);
            Assert.That(TeamId.None, Is.EqualTo(default(TeamId)));
        }

        [Test]
        public void DefaultPolicyTreatsSameTeamAsFriendlyAndOtherTeamAsHostile()
        {
            var policy = DefaultTeamRelationPolicy.Instance;

            Assert.That(policy.GetRelation(new TeamId(1), new TeamId(1)), Is.EqualTo(TeamRelation.Friendly));
            Assert.That(policy.GetRelation(new TeamId(1), new TeamId(2)), Is.EqualTo(TeamRelation.Hostile));
        }

        [Test]
        public void DefaultPolicyTreatsUnassignedTeamAsNeutral()
        {
            var policy = DefaultTeamRelationPolicy.Instance;

            Assert.That(policy.GetRelation(TeamId.None, new TeamId(1)), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(policy.GetRelation(new TeamId(1), TeamId.None), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(policy.GetRelation(TeamId.None, TeamId.None), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void TeamMemberUsesDefaultPolicyWithoutInjection()
        {
            var gameObject = new GameObject("TeamMemberTests");
            try
            {
                var member = gameObject.AddComponent<TeamMember>();
                member.SetTeam(new TeamId(1));

                Assert.That(member.RelationPolicy, Is.SameAs(DefaultTeamRelationPolicy.Instance));
                Assert.That(member.IsHostileTo(new TeamId(2)), Is.True);
                Assert.That(member.IsHostileTo(new TeamId(1)), Is.False);
                Assert.That(member.GetRelationTo(new TeamId(1)), Is.EqualTo(TeamRelation.Friendly));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InjectedPolicyReplacesDefaultRelationRule()
        {
            var gameObject = new GameObject("TeamMemberTests");
            try
            {
                var member = gameObject.AddComponent<TeamMember>();
                member.SetTeam(new TeamId(1));
                member.InjectRelationPolicy(new AlwaysFriendlyRelationPolicy());

                Assert.That(member.IsHostileTo(new TeamId(2)), Is.False);
                Assert.That(member.GetRelationTo(new TeamId(2)), Is.EqualTo(TeamRelation.Friendly));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HostilityBetweenMembersFollowsTeamAssignment()
        {
            var firstObject = new GameObject("FirstMember");
            var secondObject = new GameObject("SecondMember");
            try
            {
                var first = firstObject.AddComponent<TeamMember>();
                var second = secondObject.AddComponent<TeamMember>();
                first.SetTeam(new TeamId(1));
                second.SetTeam(new TeamId(2));

                Assert.That(first.IsHostileTo(second), Is.True);

                second.SetTeam(new TeamId(1));

                Assert.That(first.IsHostileTo(second), Is.False);
                Assert.That(first.IsHostileTo((TeamMember)null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        /// <summary>모든 진영을 아군으로 판정하는 테스트용 관계 규칙이다.</summary>
        private sealed class AlwaysFriendlyRelationPolicy : ITeamRelationPolicy
        {
            public TeamRelation GetRelation(TeamId source, TeamId other) => TeamRelation.Friendly;
        }
    }
}
