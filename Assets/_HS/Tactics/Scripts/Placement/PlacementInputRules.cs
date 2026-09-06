using System.Collections.Generic;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 맵 조작 한 번이 배치 계획에 요구하는 동작이다.
    /// </summary>
    public enum PlacementInputActionKind
    {
        /// <summary>아무것도 하지 않는다.</summary>
        None = 0,

        /// <summary>선택한 유닛 정의를 클릭한 좌표에 새로 배치한다.</summary>
        Place = 1,

        /// <summary>클릭한 자리에 있는 배치 유닛을 집어 들어 옮길 준비를 한다.</summary>
        BeginMove = 2,

        /// <summary>집어 든 유닛을 클릭한 좌표로 옮긴다.</summary>
        Move = 3,

        /// <summary>클릭한 자리에 있는 배치 유닛을 회수한다.</summary>
        Recall = 4,

        /// <summary>집어 든 유닛을 제자리에 두고 옮기기를 그만둔다.</summary>
        CancelMove = 5
    }

    /// <summary>
    /// 맵 조작을 해석한 결과이며, 무엇을 어느 배치 항목에 대해 할지 담는다.
    /// </summary>
    public readonly struct PlacementInputCommand
    {
        /// <summary>수행할 동작이다.</summary>
        public PlacementInputActionKind Kind { get; }

        /// <summary>동작 대상 배치 항목의 식별자이며, 대상이 없으면 0이다.</summary>
        public int EntryId { get; }

        /// <summary>맵 조작 해석 결과를 생성한다.</summary>
        /// <param name="kind">수행할 동작이다.</param>
        /// <param name="entryId">동작 대상 배치 항목의 식별자이다.</param>
        public PlacementInputCommand(PlacementInputActionKind kind, int entryId = 0)
        {
            Kind = kind;
            EntryId = entryId;
        }

        /// <summary>아무것도 하지 않는 결과이다.</summary>
        public static PlacementInputCommand None => new(PlacementInputActionKind.None);
    }

    /// <summary>
    /// 포인터 좌표를 배치 요청으로 바꾸는 규칙을 담은 순수 계층이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity 수명주기와 물리, 입력 장치에 의존하지 않으므로 EditMode에서 그대로 검증할 수 있다.
    /// 실제 포인터 읽기와 광선 발사는 <see cref="PlacementInputController"/>가 맡는다.
    /// </para>
    /// <para>
    /// 배치 상한과 구역 포함 판정 같은 규칙은 <see cref="UnitPlacementPlan"/>이 이미 담당하므로
    /// 여기서 다시 구현하지 않는다. 이 계층이 정하는 것은 "클릭 한 번이 배치인가, 이동인가, 회수인가"뿐이다.
    /// </para>
    /// </remarks>
    public static class PlacementInputRules
    {
        /// <summary>광선이 평면과 평행한지 판단할 때 쓰는 허용 오차이다.</summary>
        private const float RayPlaneEpsilon = 1e-5f;

        /// <summary>
        /// 광선을 수평 평면과 만나는 지점으로 투영한다.
        /// 바닥 콜라이더에 맞지 않았을 때 쓰는 대체 경로이며, 광선이 평면과 평행하거나
        /// 교점이 광선 뒤쪽이면 실패로 답한다.
        /// </summary>
        /// <param name="ray">투영할 광선이다.</param>
        /// <param name="planeHeight">수평 평면의 높이(Y)이다.</param>
        /// <param name="point">평면과 만나는 지점이며 실패하면 기본값이다.</param>
        /// <returns>교점을 찾았으면 true이다.</returns>
        public static bool TryProjectRayOntoPlane(Ray ray, float planeHeight, out Vector3 point)
        {
            point = default;
            var directionY = ray.direction.y;
            if (Mathf.Abs(directionY) < RayPlaneEpsilon)
            {
                return false;
            }

            var distance = (planeHeight - ray.origin.y) / directionY;
            if (distance < 0f)
            {
                return false;
            }

            point = ray.origin + ray.direction * distance;
            return true;
        }

        /// <summary>
        /// 클릭한 좌표에서 가장 가까운 배치 항목을 찾는다.
        /// 높이 차이는 무시하고 수평 거리만 보므로, 바닥 높이가 조금 달라도 같은 자리로 인식한다.
        /// </summary>
        /// <param name="entries">현재 배치된 항목 목록이다.</param>
        /// <param name="worldPosition">클릭한 월드 좌표이다.</param>
        /// <param name="pickRadius">같은 자리로 인정할 최대 수평 거리이다.</param>
        /// <param name="entryId">찾은 배치 항목의 식별자이며 없으면 0이다.</param>
        /// <returns>선택 반경 안에서 항목을 찾았으면 true이다.</returns>
        public static bool TryPickEntry(
            IReadOnlyList<PlacementEntry> entries,
            Vector3 worldPosition,
            float pickRadius,
            out int entryId)
        {
            entryId = 0;
            if (entries == null || pickRadius <= 0f)
            {
                return false;
            }

            var squaredPickRadius = pickRadius * pickRadius;
            var nearestSquaredDistance = float.MaxValue;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!entry.IsValid)
                {
                    continue;
                }

                var offsetX = entry.Position.x - worldPosition.x;
                var offsetZ = entry.Position.z - worldPosition.z;
                var squaredDistance = (offsetX * offsetX) + (offsetZ * offsetZ);
                if (squaredDistance > squaredPickRadius || squaredDistance >= nearestSquaredDistance)
                {
                    continue;
                }

                nearestSquaredDistance = squaredDistance;
                entryId = entry.Id;
            }

            return entryId > 0;
        }

        /// <summary>
        /// 주 조작(기본은 좌클릭)을 해석한다.
        /// 유닛을 집어 든 상태면 그 유닛을 옮기고, 아니면 클릭한 자리의 유닛을 집어 들며,
        /// 빈 자리를 클릭했고 배치할 유닛을 골라 두었다면 새로 배치한다.
        /// </summary>
        /// <param name="heldEntryId">집어 든 배치 항목의 식별자이며 없으면 0이다.</param>
        /// <param name="pickedEntryId">클릭한 자리에 있는 배치 항목의 식별자이며 없으면 0이다.</param>
        /// <param name="hasSelectedDefinition">배치할 유닛 정의를 골라 두었는지 여부이다.</param>
        /// <returns>수행할 동작이다.</returns>
        public static PlacementInputCommand ResolvePrimaryCommand(
            int heldEntryId,
            int pickedEntryId,
            bool hasSelectedDefinition)
        {
            if (heldEntryId > 0)
            {
                return new PlacementInputCommand(PlacementInputActionKind.Move, heldEntryId);
            }

            if (pickedEntryId > 0)
            {
                return new PlacementInputCommand(PlacementInputActionKind.BeginMove, pickedEntryId);
            }

            return hasSelectedDefinition
                ? new PlacementInputCommand(PlacementInputActionKind.Place)
                : PlacementInputCommand.None;
        }

        /// <summary>
        /// 보조 조작(기본은 우클릭)을 해석한다.
        /// 유닛을 집어 든 상태면 옮기기를 그만두고, 아니면 클릭한 자리의 유닛을 회수한다.
        /// </summary>
        /// <param name="heldEntryId">집어 든 배치 항목의 식별자이며 없으면 0이다.</param>
        /// <param name="pickedEntryId">클릭한 자리에 있는 배치 항목의 식별자이며 없으면 0이다.</param>
        /// <returns>수행할 동작이다.</returns>
        public static PlacementInputCommand ResolveSecondaryCommand(int heldEntryId, int pickedEntryId)
        {
            if (heldEntryId > 0)
            {
                return new PlacementInputCommand(PlacementInputActionKind.CancelMove, heldEntryId);
            }

            return pickedEntryId > 0
                ? new PlacementInputCommand(PlacementInputActionKind.Recall, pickedEntryId)
                : PlacementInputCommand.None;
        }
    }
}
