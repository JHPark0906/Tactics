using HS.Framework.Gameplay.Teams;
using UnityEngine;

namespace HS.Tactics.Lane
{
    /// <summary>
    /// 유닛이 자율 전진하는 일자형 전투 레인을 씬에 정의한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 맵이 일자형이라 전진 경로는 시작점과 종점 두 지점으로 충분하다. 분기가 없는 지형에서
    /// 웨이포인트 그래프가 담을 정보는 간선 하나뿐이고, 국소적인 우회는 경로 계획 서비스가 이미 처리한다.
    /// 그래서 레인은 시종점만 정의하고 지형의 세부 형태는 엄폐물 배치로 만든다.
    /// </para>
    /// <para>
    /// 진영별 전진 방향은 <see cref="ForwardTeam"/> 하나로 갈린다. 그 진영은 시작점에서 종점으로,
    /// 나머지 진영은 종점에서 시작점으로 나아가므로 레인을 진영 수만큼 만들 필요가 없다.
    /// </para>
    /// <para>
    /// 배치 방법은 빈 GameObject에 이 컴포넌트를 붙이고, 자식으로 시작점과 종점을 만들어 연결하는
    /// 것이다. 씬에 레인이 하나뿐이라는 전제이며 유닛은 <see cref="Active"/>로 그 레인을 찾는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleLane : MonoBehaviour, IAdvanceTargetSource
    {
        private static BattleLane _active;

        [Tooltip("레인의 시작점이다. 정방향 진영이 여기서 출발한다.")]
        [SerializeField]
        private Transform startPoint;

        [Tooltip("레인의 종점이다. 정방향 진영이 여기를 목표로 나아간다.")]
        [SerializeField]
        private Transform endPoint;

        [Tooltip("시작점에서 종점 방향으로 나아가는 진영이다. 나머지 진영은 반대로 나아간다.")]
        [SerializeField]
        private TeamId forwardTeam = new(1);

        /// <summary>
        /// 씬에 배치된 레인이며, 아직 등록되지 않았으면 씬에서 찾아본다. 레인이 없으면 null이다.
        /// </summary>
        public static BattleLane Active
        {
            get
            {
                if (_active == null)
                {
                    _active = FindFirstObjectByType<BattleLane>();
                }

                return _active;
            }
        }

        /// <summary>레인의 시작 좌표이며, 시작점이 지정되지 않았으면 이 컴포넌트의 좌표이다.</summary>
        public Vector3 StartPosition => startPoint != null ? startPoint.position : transform.position;

        /// <summary>레인의 종점 좌표이며, 종점이 지정되지 않았으면 이 컴포넌트의 좌표이다.</summary>
        public Vector3 EndPosition => endPoint != null ? endPoint.position : transform.position;

        /// <summary>시작점에서 종점 방향으로 나아가는 진영이다.</summary>
        public TeamId ForwardTeam => forwardTeam;

        /// <summary>현재 배치 상태에서 계산한 레인 기하이다.</summary>
        public LaneGeometry Geometry => new(StartPosition, EndPosition);

        /// <summary>전진 목표를 계산할 수 있을 만큼 시종점이 갖춰졌는지 여부이다.</summary>
        public bool IsConfigured => startPoint != null && endPoint != null && Geometry.IsValid;

        /// <summary>
        /// 이 레인을 씬의 대표 레인으로 등록한다.
        /// Awake에서 자동으로 부르며, 수명주기가 돌지 않는 에디트 모드에서는 직접 부른다.
        /// </summary>
        public void InitializeLane()
        {
            if (_active != null && _active != this)
            {
                Debug.LogWarning($"[BattleLane] 씬에 레인이 둘 이상이라 {name}이 기존 레인을 대신한다.", this);
            }

            _active = this;
        }

        /// <summary>시종점과 정방향 진영을 코드에서 지정한다. 맵을 절차적으로 만들 때 사용한다.</summary>
        /// <param name="start">레인의 시작점이다.</param>
        /// <param name="end">레인의 종점이다.</param>
        /// <param name="team">시작점에서 종점 방향으로 나아갈 진영이다.</param>
        public void SetLane(Transform start, Transform end, TeamId team)
        {
            startPoint = start;
            endPoint = end;
            forwardTeam = team;
        }

        /// <inheritdoc />
        /// <remarks>일자형 레인이라 목표는 언제나 레인의 끝점이며 유닛의 현재 좌표는 쓰지 않는다.</remarks>
        public bool TryGetAdvanceTarget(TeamId team, Vector3 position, out Vector3 target)
        {
            target = Vector3.zero;
            if (!IsConfigured
                || !LaneAdvanceCalculator.TryGetOrientation(team, forwardTeam, out var orientation))
            {
                return false;
            }

            target = Geometry.GetGoal(orientation);
            return true;
        }

        private void Awake()
        {
            InitializeLane();
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>디자이너가 레인의 방향과 길이를 씬에서 바로 볼 수 있게 그린다.</summary>
        private void OnDrawGizmos()
        {
            if (startPoint == null || endPoint == null)
            {
                return;
            }

            var start = StartPosition;
            var end = EndPosition;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, 0.5f);
            Gizmos.DrawSphere(end, 0.5f);
        }
#endif
    }
}
