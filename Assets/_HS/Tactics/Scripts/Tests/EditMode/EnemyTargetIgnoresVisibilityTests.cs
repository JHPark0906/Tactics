using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 표적 선정이 시야를 보지 않는다는 규칙을 고정한다. 반경 안의 적은 사이에 벽이 있어도, 등 뒤에 있어도,
    /// 엄폐 중이어도 표적이 된다. 엄폐는 보이지 않게 하는 것이 아니라 대신 맞아 주는 것이기 때문이다.
    /// </summary>
    /// <remarks>
    /// 벽은 실제 콜라이더로 세우고 관찰자는 실제로 등을 돌린다. 선정에 광선 검사나 각도 검사가 들어오면
    /// 그 벽과 그 방향에 걸려 여기서 드러난다.
    /// </remarks>
    public sealed class EnemyTargetIgnoresVisibilityTests
    {
        private const float Radius = 20f;
        private static readonly Vector3 EnemyPosition = new(0f, 0f, 6f);

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
        public void AHostileBehindAWallIsTargeted()
        {
            var wall = CreateObject("Wall", new Vector3(0f, 0f, 3f));
            wall.AddComponent<BoxCollider>().size = new Vector3(10f, 10f, 1f);
            var enemy = CreateTeamMember("Enemy", 2, EnemyPosition);
            Physics.SyncTransforms();

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { enemy }, Radius);

            Assert.That(selected, Is.SameAs(enemy), "사이에 벽이 있어도 반경 안이면 표적이다.");
        }

        [Test]
        public void AHostileBehindTheBackIsTargeted()
        {
            _self.transform.rotation = Quaternion.LookRotation(Vector3.back);
            var enemy = CreateTeamMember("Enemy", 2, EnemyPosition);

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { enemy }, Radius);

            Assert.That(selected, Is.SameAs(enemy), "등 뒤에 있어도 반경 안이면 표적이다.");
        }

        [Test]
        public void AHostileInCoverIsTargeted()
        {
            var enemy = CreateTeamMember("Enemy", 2, EnemyPosition);
            var coverState = enemy.gameObject.AddComponent<UnitCoverState>();
            var cover = CreateObject("Cover", EnemyPosition).AddComponent<CoverPoint>();
            Assert.That(coverState.ClaimCover(cover), Is.True);
            Assert.That(coverState.IsInCover, Is.True, "무대 확인: 적은 엄폐 지점에 서 있어야 한다.");

            var selected = EnemyTargetSelector.SelectNearestHostileInRange(_self, new[] { enemy }, Radius);

            Assert.That(selected, Is.SameAs(enemy), "엄폐 중이어도 반경 안이면 표적이다.");
        }

        private GameObject CreateObject(string objectName, Vector3 position)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            createdObject.transform.position = position;
            return createdObject;
        }

        private TeamMember CreateTeamMember(string objectName, int teamValue, Vector3 position)
        {
            var member = CreateObject(objectName, position).AddComponent<TeamMember>();
            member.SetTeam(new TeamId(teamValue));
            return member;
        }
    }
}
