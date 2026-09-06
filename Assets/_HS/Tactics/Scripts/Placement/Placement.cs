using System;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 스테이지 한 판이 지나가는 배치 흐름의 단계이다.
    /// </summary>
    /// <remarks>
    /// 플레이어의 실력은 배치 단계에서만 발휘되고 전투가 시작되면 관전만 하므로,
    /// 배치 조작을 받아들일지 여부는 전적으로 이 단계 값으로 결정한다.
    /// </remarks>
    public enum PlacementPhase
    {
        /// <summary>아직 배치를 시작하지 않은 상태이며, 스테이지에 막 진입한 직후가 여기에 해당한다.</summary>
        Preparing = 0,

        /// <summary>플레이어가 유닛을 배치하고 자유롭게 이동·회수할 수 있는 단계이다.</summary>
        Placing = 1,

        /// <summary>전투가 진행 중이며 플레이어는 관전만 하는 단계이다.</summary>
        Battle = 2
    }

    /// <summary>
    /// 배치·이동·회수 요청을 처리한 결과이다.
    /// </summary>
    /// <remarks>
    /// <see cref="UnitPlacementPlan"/>은 규칙 위반만 판별하므로
    /// <see cref="Success"/>, <see cref="MissingDefinition"/>, <see cref="NoPlacementZone"/>,
    /// <see cref="CapacityReached"/>, <see cref="OutsidePlacementZone"/>, <see cref="UnknownEntry"/>,
    /// <see cref="CellOccupied"/>, <see cref="CellTooSmall"/>를 반환한다.
    /// 단계 검사와 스폰 실패는 계획이 알 수 없으므로 <see cref="UnitPlacementController"/>가 판별한다.
    /// </remarks>
    public enum PlacementResult
    {
        /// <summary>요청을 처리했다.</summary>
        Success = 0,

        /// <summary>배치 단계가 아니라 요청을 받아들이지 않았다.</summary>
        NotInPlacingPhase = 1,

        /// <summary>배치 가능 구역이 지정되지 않았거나 크기가 없다.</summary>
        NoPlacementZone = 2,

        /// <summary>배치할 유닛 정의가 지정되지 않았다.</summary>
        MissingDefinition = 3,

        /// <summary>유닛 정의에 스폰할 프리팹이 연결되어 있지 않다.</summary>
        MissingUnitPrefab = 4,

        /// <summary>요청한 좌표가 배치 가능 구역 밖이다.</summary>
        OutsidePlacementZone = 5,

        /// <summary>이미 배치 수 상한에 도달했다.</summary>
        CapacityReached = 6,

        /// <summary>이동하거나 회수하려는 배치 항목을 찾지 못했다.</summary>
        UnknownEntry = 7,

        /// <summary>규칙은 통과했으나 유닛 인스턴스를 만들지 못했다.</summary>
        SpawnFailed = 8,

        /// <summary>고른 칸에 이미 다른 유닛이 서 있다. 한 칸에는 한 기만 선다.</summary>
        CellOccupied = 9,

        /// <summary>칸이 유닛의 지름보다 좁아 그 자리에 세울 수 없다.</summary>
        CellTooSmall = 10
    }

    /// <summary>
    /// 배치 계획에 등록된 유닛 한 기의 기록이다.
    /// 어떤 정의를 어디에 놓았는지만 담으며, 실제 GameObject는
    /// <see cref="UnitPlacementController"/>가 항목 식별자에 맞춰 따로 보관한다.
    /// </summary>
    public readonly struct PlacementEntry : IEquatable<PlacementEntry>
    {
        /// <summary>지정한 식별자와 정의, 칸과 좌표로 배치 항목을 생성한다.</summary>
        /// <param name="id">계획 안에서 항목을 구분하는 식별자이며 1 이상이다.</param>
        /// <param name="definition">배치한 유닛의 정의이다.</param>
        /// <param name="cellIndex">배치한 격자 칸의 번호이다.</param>
        /// <param name="position">배치한 월드 좌표이며 보통 그 칸의 중심이다.</param>
        public PlacementEntry(int id, UnitDefinition definition, int cellIndex, Vector3 position)
        {
            Id = id;
            Definition = definition;
            CellIndex = cellIndex;
            Position = position;
        }

        /// <summary>계획 안에서 항목을 구분하는 식별자이며, 0은 유효하지 않은 항목을 뜻한다.</summary>
        public int Id { get; }

        /// <summary>배치한 유닛의 정의이다.</summary>
        public UnitDefinition Definition { get; }

        /// <summary>
        /// 이 유닛이 선 격자 칸의 번호이다.
        /// </summary>
        /// <remarks>
        /// <b>어느 칸이 찼는지를 답하는 것은 이 값 하나다.</b> 점유표를 따로 두지 않는 것은
        /// 같은 물음에 답하는 것이 둘이 되면 <b>언젠가 어긋나고, 어긋난 쪽이 화면이면
        /// 플레이어는 빈 칸을 계속 눌러 보게 되기 때문이다.</b> 전투가 시작된 뒤에는 아무도 읽지 않는다.
        /// </remarks>
        public int CellIndex { get; }

        /// <summary>배치한 월드 좌표이다.</summary>
        public Vector3 Position { get; }

        /// <summary>실제 배치를 가리키는 유효한 항목인지 여부이다.</summary>
        public bool IsValid => Id > 0;

        /// <summary>칸과 좌표만 바꾼 새 항목을 만든다. 식별자와 정의는 그대로 유지한다.</summary>
        /// <param name="cellIndex">새로 지정할 격자 칸의 번호이다.</param>
        /// <param name="position">새로 지정할 월드 좌표이다.</param>
        /// <returns>칸을 옮긴 항목이다.</returns>
        public PlacementEntry WithCell(int cellIndex, Vector3 position)
        {
            return new PlacementEntry(Id, Definition, cellIndex, position);
        }

        /// <inheritdoc />
        public bool Equals(PlacementEntry other)
        {
            return Id == other.Id
                   && Definition == other.Definition
                   && CellIndex == other.CellIndex
                   && Position == other.Position;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PlacementEntry other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Id;

        /// <inheritdoc />
        public override string ToString()
        {
            return IsValid
                ? $"Placement({Id}, {(Definition != null ? Definition.DisplayName : "None")}, "
                  + $"cell {CellIndex}, {Position})"
                : "Placement(None)";
        }
    }
}
