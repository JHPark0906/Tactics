using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 맵의 시작 지대에서 유닛을 놓을 수 있는 영역을 표시하는 컴포넌트이다.
    /// 스테이지 씬에 수작업으로 배치하고 인스펙터에서 크기를 맞춘다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 구역은 이 오브젝트의 위치를 기준으로 계산하므로, 씬에서 오브젝트를 옮기면 구역도 함께 움직인다.
    /// 맵이 일자형이라 축에 정렬된 상자로 충분하며 회전은 구역 판정에 반영하지 않는다.
    /// 다만 <see cref="SpawnRotation"/>은 이 오브젝트의 회전을 그대로 쓰므로,
    /// 오브젝트의 정면을 적 진영 쪽으로 두면 배치한 유닛도 그 방향을 보고 서게 된다.
    /// </para>
    /// <para>
    /// 구역 계산은 값을 요청할 때마다 수행하므로 Awake 같은 수명주기 콜백에 기대지 않는다.
    /// 덕분에 수명주기 콜백이 호출되지 않는 EditMode 테스트에서도 그대로 검증할 수 있다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitPlacementZone : MonoBehaviour
    {
        [Header("Area")]
        [Tooltip("이 오브젝트의 위치를 기준으로 한 구역 중심의 국소 오프셋이다.")]
        [SerializeField]
        private Vector3 areaOffset = Vector3.zero;

        [Tooltip("구역의 각 축 길이이다. 임시값이며 스테이지에 맞춰 조정한다.")]
        [SerializeField]
        private Vector3 areaSize = new(12f, 4f, 8f);

        [Header("Gizmo")]
        [Tooltip("씬 뷰에서 구역을 그릴 때 사용할 색이다.")]
        [SerializeField]
        private Color gizmoColor = new(0.2f, 0.7f, 1f, 0.25f);

        /// <summary>이 구역이 나타내는 배치 영역이며 오브젝트의 현재 위치를 반영한다.</summary>
        public PlacementArea Area => new(transform.position + areaOffset, areaSize);

        /// <summary>이 구역에 배치한 유닛이 바라볼 방향이며 오브젝트의 회전을 그대로 사용한다.</summary>
        public Quaternion SpawnRotation => transform.rotation;

        /// <summary>지정한 월드 좌표가 높이를 무시하고 구역 안에 있는지 확인한다.</summary>
        /// <param name="worldPosition">확인할 월드 좌표이다.</param>
        /// <returns>구역 안이면 true이다.</returns>
        public bool Contains(Vector3 worldPosition)
        {
            return Area.ContainsIgnoringHeight(worldPosition);
        }

        /// <summary>지정한 월드 좌표를 구역 안으로 끌어당긴다.</summary>
        /// <param name="worldPosition">보정할 월드 좌표이다.</param>
        /// <returns>구역 안으로 보정한 좌표이다.</returns>
        public Vector3 ClampToArea(Vector3 worldPosition)
        {
            return Area.ClampToArea(worldPosition);
        }

        /// <summary>씬 뷰에서 구역의 범위를 눈으로 확인할 수 있게 상자를 그린다.</summary>
        private void OnDrawGizmos()
        {
            var area = Area;
            if (area.IsEmpty)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(area.Center, area.Size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(area.Center, area.Size);
        }
    }
}
