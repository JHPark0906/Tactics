using System;
using System.Collections.Generic;
using HS.Tactics.Units;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// MainMenu에서 고른 영웅 배치를 씬 전환 너머로 들고 가는 값 하나이다.
    /// </summary>
    /// <remarks>
    /// 담는 것은 칸 번호와 유닛 정의뿐이다. 월드 좌표는 담지 않는다 — 그 좌표는 MainMenu의 가짜 배치
    /// 구역 기준으로 계산된 것이라 게임플레이 씬에서는 뜻이 없다. 실제 좌표는 재생하는 쪽이
    /// <see cref="UnitPlacementController.TryPlaceUnitAtCell"/>로 그 씬의 진짜 구역에서 다시 얻는다.
    /// </remarks>
    public readonly struct PartySelectionEntry
    {
        /// <summary>MainMenu의 배치 격자에서 고른 칸 번호이다.</summary>
        public int CellIndex { get; }

        /// <summary>그 칸에 세운 유닛 정의이다.</summary>
        public UnitDefinition Definition { get; }

        /// <summary>칸 번호와 유닛 정의로 항목을 만든다.</summary>
        /// <param name="cellIndex">고른 칸 번호이다.</param>
        /// <param name="definition">그 칸에 세운 유닛 정의이다.</param>
        public PartySelectionEntry(int cellIndex, UnitDefinition definition)
        {
            CellIndex = cellIndex;
            Definition = definition;
        }
    }

    /// <summary>
    /// MainMenu에서 확정한 영웅 배치를 게임플레이 씬까지 전달하는 프로젝트 수명의 서비스이다.
    /// 칸 번호와 유닛 정의만 보관하며 실제 좌표와 스폰은 게임플레이 씬의 배치 컨트롤러가 정한다.
    /// TakeAndClear로 한 번 꺼내면 비워 중복 재생을 막는다. 커밋이 없으면 씬의 수동 배치를 사용할 수 있다.
    /// </summary>
    public sealed class PartySelectionService
    {
        private readonly List<PartySelectionEntry> _entries = new();

        /// <summary>커밋된 선택이 있는지 여부이다.</summary>
        public bool HasSelection => _entries.Count > 0;

        /// <summary>지금 커밋되어 있는 선택을 순서대로 열거한다.</summary>
        public IReadOnlyList<PartySelectionEntry> Entries => _entries;

        /// <summary>
        /// MainMenu의 배치 패널이 확정한 선택을 커밋한다. 이전에 커밋된 것이 있으면 덮어쓴다.
        /// </summary>
        /// <param name="entries">커밋할 선택이다.</param>
        /// <exception cref="ArgumentNullException">선택이 null이면 발생한다.</exception>
        public void Commit(IReadOnlyList<PartySelectionEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            _entries.Clear();
            _entries.AddRange(entries);
        }

        /// <summary>
        /// 커밋된 선택을 꺼내면서 비운다. 게임플레이 씬의 재생 컴포넌트가 시작할 때 한 번 부른다.
        /// </summary>
        /// <returns>꺼낸 선택이며, 커밋된 것이 없었으면 빈 목록이다.</returns>
        public IReadOnlyList<PartySelectionEntry> TakeAndClear()
        {
            if (_entries.Count == 0)
            {
                return Array.Empty<PartySelectionEntry>();
            }

            var taken = new List<PartySelectionEntry>(_entries);
            _entries.Clear();
            return taken;
        }
    }
}
