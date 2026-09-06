using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Pathfinding
{
    /// <summary>
    /// 전투 중 유닛이 걸을 수 있는 전장의 경계이다. 씬에 하나 놓고 인스펙터에서 크기를 맞춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="HS.Tactics.Placement.UnitPlacementZone"/>과 다른 개념이다.</b> 그것은 배치 단계에서
    /// 유닛을 놓을 수 있는 자리를 표시할 뿐 전투 중 이동과는 무관하다. 이 컴포넌트는 <see cref="BattlePathfindingService"/>가
    /// 시야 그래프를 지을 때 쓰는, 전투 내내 걸을 수 있는 바깥 테두리다.
    /// </para>
    /// <para>
    /// <see cref="HS.Tactics.Placement.UnitPlacementZone"/>처럼 구역을 <c>Awake</c> 같은 수명주기 콜백에 기대지 않고 값을 요청할
    /// 때마다 transform에서 계산한다. 그래서 씬에서 오브젝트를 옮기면 경계도 함께 움직이고, 수명주기 콜백이
    /// 호출되지 않는 EditMode 검사에서도 그대로 값을 읽을 수 있다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleBounds : MonoBehaviour
    {
        [Tooltip("이 오브젝트의 위치를 기준으로 한 경계 중심에서 각 축까지의 거리(미터)이다. 스테이지에 맞춰 조정한다.")]
        [SerializeField]
        private Vector2 halfExtents = new(12f, 12f);

        [Tooltip("씬 뷰에서 경계를 그릴 때 사용할 색이다.")]
        [SerializeField]
        private Color gizmoColor = new(1f, 0.6f, 0.1f, 0.6f);

        /// <summary>이 컴포넌트가 나타내는 전장 경계이며 오브젝트의 현재 위치·회전을 반영한다.</summary>
        public PlanarRectangle Bounds => new(
            PlanarPosition.FromWorld(transform.position),
            new PlanarPosition(halfExtents.x, halfExtents.y),
            PlanarPosition.FromWorld(transform.forward));

        /// <summary>경계 중심에서 각 축까지의 거리를 코드에서 지정한다. 맵을 절차적으로 만들 때 사용한다.</summary>
        /// <param name="value">x는 폭 방향, y는 앞뒤 방향의 거리(미터)이다.</param>
        public void SetHalfExtents(Vector2 value)
        {
            halfExtents = value;
        }

        /// <summary>씬 뷰에서 경계의 범위를 눈으로 확인할 수 있게 상자를 그린다.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.y * 2f));
        }
    }
}
