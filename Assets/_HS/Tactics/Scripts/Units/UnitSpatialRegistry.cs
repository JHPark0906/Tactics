using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 씬 안의 유닛 위치와 반지름을 평면 원으로 조회하는 계약이다.
    /// 탐지와 이동 충돌처럼 유닛의 공간 점유를 조회하는 소비자가 함께 사용한다.
    /// </summary>
    public interface IUnitSpatialRegistry
    {
        /// <summary>유닛 하나를 등록한다. 이미 등록되어 있으면 위치·반지름을 새 값으로 바꾼다.</summary>
        /// <param name="team">등록할 유닛의 진영 구성요소이며, 이후 질의 결과를 식별하는 열쇠다.</param>
        /// <param name="transform">위치를 읽을 변환이다. 질의마다 이 변환의 현재 위치를 다시 읽으므로 따로 갱신할 필요가 없다.</param>
        /// <param name="radius">유닛이 평면 위에서 차지하는 원의 반지름(미터)이다.</param>
        void Register(TeamMember team, Transform transform, float radius);

        /// <summary>등록을 해제한다. 등록되어 있지 않으면 아무 일도 하지 않는다.</summary>
        /// <param name="team">해제할 유닛의 진영 구성요소이다.</param>
        void Unregister(TeamMember team);

        /// <summary>지정한 원과 겹치는 유닛을 모은다.</summary>
        /// <remarks>
        /// 등록된 각 유닛을 그 반지름의 원으로 보고 <paramref name="area"/>와 겹치는지 확인한다.
        /// 그래서 중심이 <paramref name="area"/> 밖이라도 몸이 걸쳐 있으면 결과에 든다.
        /// 자기 자신도 걸러내지 않으므로 호출부가 필요하면 직접 뺀다.
        /// </remarks>
        /// <param name="area">겹침을 확인할 기준 원이다.</param>
        /// <param name="results">결과를 담을 목록이다. 먼저 비운 뒤 채운다.</param>
        void CollectOverlapping(PlanarCircle area, List<TeamMember> results);

        /// <summary>등록된 유닛의 지금 자리와 반지름을 원 하나로 얻는다.</summary>
        /// <remarks>
        /// <see cref="CollectOverlapping"/>이 누가 겹치는지만 알려 주므로, 정확한 자르기·미끄러짐
        /// 계산에 쓸 그 유닛의 실제 원(중심·반지름)이 다시 필요한 소비자를 위한 것이다.
        /// </remarks>
        /// <param name="team">조회할 유닛의 진영 구성요소이다.</param>
        /// <param name="circle">등록되어 있으면 그 유닛의 지금 원이다. 없으면 뜻이 없다.</param>
        /// <returns>등록되어 있으면 true이다.</returns>
        bool TryGetCircle(TeamMember team, out PlanarCircle circle);
    }

    /// <summary>
    /// <see cref="IUnitSpatialRegistry"/>의 씬 배치용 구현이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>위치를 직접 갱신받지 않는다.</b> 등록할 때 <see cref="Transform"/> 참조를 들고 있다가
    /// 질의가 올 때마다 그 자리에서 현재 위치를 읽는다. 유닛이 움직여도 따로 알려 줄 대상이 없고,
    /// 값이 오래될 일도 없다.
    /// </para>
    /// <para>
    /// <b>씬 스코프다.</b> 이 컴포넌트는 씬마다 하나씩 놓이며, <c>TacticsLifetimeScope</c>가
    /// <c>IBattleUnitRegistry</c>와 같은 방식(<c>SceneComponentLocator</c> + Transient)으로 찾아 준다.
    /// 프로젝트 스코프 Singleton으로 두면 씬을 넘어가도 죽은 컴포넌트를 붙든 채 남아,
    /// 다음 스테이지의 유닛이 이전 씬의 레지스트리에 등록되는 사고가 난다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitSpatialRegistry : MonoBehaviour, IUnitSpatialRegistry
    {
        private readonly List<Entry> _entries = new();

        /// <inheritdoc />
        public void Register(TeamMember team, Transform transform, float radius)
        {
            if (team == null || transform == null)
            {
                return;
            }

            var entry = new Entry(team, transform, Mathf.Max(0f, radius));
            var index = IndexOf(team);
            if (index >= 0)
            {
                _entries[index] = entry;
                return;
            }

            _entries.Add(entry);
        }

        /// <inheritdoc />
        public void Unregister(TeamMember team)
        {
            var index = IndexOf(team);
            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }
        }

        /// <inheritdoc />
        public void CollectOverlapping(PlanarCircle area, List<TeamMember> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                if (entry.Transform == null)
                {
                    // 파괴된 유닛이 해제를 거치지 않고 남아 있을 수 있다. 다음 등록·해제가 이 자리를
                    // 정리하므로 여기서는 조용히 건너뛰기만 한다.
                    continue;
                }

                var candidateCircle = new PlanarCircle(
                    PlanarPosition.FromWorld(entry.Transform.position),
                    entry.Radius);
                if (PlanarGeometry.Overlaps(area, candidateCircle))
                {
                    results.Add(entry.Team);
                }
            }
        }

        /// <inheritdoc />
        public bool TryGetCircle(TeamMember team, out PlanarCircle circle)
        {
            var index = IndexOf(team);
            if (index < 0 || _entries[index].Transform == null)
            {
                circle = default;
                return false;
            }

            var entry = _entries[index];
            circle = new PlanarCircle(PlanarPosition.FromWorld(entry.Transform.position), entry.Radius);
            return true;
        }

        /// <summary>등록 목록에서 진영 구성요소가 있는 자리를 찾는다.</summary>
        /// <param name="team">찾을 진영 구성요소이다.</param>
        /// <returns>목록 안의 위치이며 없으면 -1이다.</returns>
        private int IndexOf(TeamMember team)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Team == team)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>등록된 유닛 하나의 위치·반지름 기록이다.</summary>
        private readonly struct Entry
        {
            public Entry(TeamMember team, Transform transform, float radius)
            {
                Team = team;
                Transform = transform;
                Radius = radius;
            }

            public TeamMember Team { get; }
            public Transform Transform { get; }
            public float Radius { get; }
        }
    }
}
