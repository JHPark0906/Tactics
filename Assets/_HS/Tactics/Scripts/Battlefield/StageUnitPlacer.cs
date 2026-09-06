using System;
using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Battlefield
{
    /// <summary>
    /// 스테이지 데이터의 배치 항목 하나로 세운 유닛과 그 항목의 짝이다.
    /// </summary>
    /// <remarks>
    /// 짝을 지어 돌려주는 것은 세운 뒤에 항목별로 무엇을 더 할 일이 생길 때 어느 유닛이 어느 항목에서 왔는지
    /// 알아야 하기 때문이다. 유닛만 돌려주면 건너뛴 항목이 있을 때 번호가 어긋난다.
    /// </remarks>
    public readonly struct PlacedUnit
    {
        /// <summary>세운 유닛이며 null이 아니다.</summary>
        public TacticalUnit Unit { get; }

        /// <summary>그 유닛을 세운 배치 항목이다.</summary>
        public UnitPlacement Placement { get; }

        /// <summary>유닛과 배치 항목으로 짝을 만든다.</summary>
        /// <param name="unit">세운 유닛이다.</param>
        /// <param name="placement">그 유닛을 세운 배치 항목이다.</param>
        public PlacedUnit(TacticalUnit unit, UnitPlacement placement)
        {
            Unit = unit;
            Placement = placement;
        }
    }

    /// <summary>
    /// 스테이지 데이터의 배치 리스트를 읽어 항목마다 유닛을 한 진영으로 세운다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>스폰은 <see cref="UnitSpawner"/>가 한다.</b> 이 도우미는 항목을 읽어 스포너에 넘기고 결과를 짝지을 뿐이며,
    /// 프리팹을 만들거나 진영·정의·레벨을 유닛에 넣는 일은 스포너 한 곳에 남겨 둔다. 배치 격자
    /// (<see cref="UnitPlacementController"/>)가 세우는 유닛과 같은 길로 세워져야 두 길의 유닛이 다르게 조립되지 않는다.
    /// 레벨은 스포너의 마지막 인자로 넘긴다 — 레벨이 수치에 먹히는 것은 조립 전 한 번뿐이고, 그 자리는 스포너 안에만 있다.
    /// </para>
    /// <para>
    /// <b>스폰 부모는 부르는 쪽이 정하고, 이 도우미가 그 부모로 스포너를 만든다.</b> 유닛이 어디에 매달리는지가
    /// 주입에 닿기 때문이다: 컨테이너의 계층 주입은 컴포넌트를 주입한 뒤 자식을 재귀로 훑으므로, 주입 중인 오브젝트의
    /// 자식으로 세우면 그 유닛이 한 번 더 주입된다. 부르는 쪽은 그 순간 새로 만든 씬 루트를 넘겨 이를 피한다.
    /// 스포너는 이 도우미가 들고 있다가 <see cref="Dispose"/>에서 정리하므로, 부르는 쪽이 자기 수명이 끝날 때 이 도우미를 정리한다.
    /// </para>
    /// <para>
    /// <b>진영은 부르는 쪽이 정한다.</b> 저장소에 적 진영 값이 정해진 곳이 없고 정의 에셋도 진영을 들고 있지 않아,
    /// 여기서 기본값을 정하면 그 수가 여기 하나 더 생긴다. 지정되지 않은 진영을 넘기면 스포너가 그러듯 정의의 기본 진영을 따른다.
    /// </para>
    /// <para>
    /// <b>세우지 못한 항목은 경고하고 건너뛴다.</b> 정의나 프리팹이 없는 항목은 스포너를 부르지 않고 여기서 거르며
    /// (<see cref="UnitPlacementController"/>가 <c>MissingUnitPrefab</c>으로 먼저 거르는 것과 같다), 스포너가 세우지 못한
    /// 항목도 경고만 남기고 다음 항목으로 간다. 한 항목이 빠졌다고 스테이지 전체가 비어서는 안 된다. 돌려주는 목록에는
    /// 실제로 세운 것만 세운 순서대로 들어간다.
    /// </para>
    /// <para>
    /// <b>진행률은 보고하지 않는다.</b> 씬 초기화 진행률을 보고하는 자리는 전장을 만드는 쪽 하나여야 한다 —
    /// 둘이 보고하면 먼저 1에 닿는 쪽이 대기를 끝내 버린다. 이 도우미는 스폰만 한다.
    /// </para>
    /// </remarks>
    public sealed class StageUnitPlacer : IDisposable
    {
        private readonly UnitSpawner _spawner;

        /// <summary>세운 유닛을 담을 부모로 스포너를 만들어 도우미를 준비한다.</summary>
        /// <param name="unitParent">세운 유닛의 부모이다. 주입 중인 오브젝트의 자식이 아닌, 그 순간 새로 만든 씬 루트여야 한다.</param>
        public StageUnitPlacer(Transform unitParent)
        {
            _spawner = new UnitSpawner(unitParent);
        }

        /// <summary>
        /// 스테이지 데이터의 배치 리스트대로 유닛을 세운다.
        /// </summary>
        /// <param name="stage">배치 리스트를 담은 스테이지 데이터이다.</param>
        /// <param name="team">세운 유닛에 지정할 진영이며, 지정되지 않은 값이면 정의의 기본 진영을 따른다.</param>
        /// <param name="spawnRotation">세운 유닛이 바라볼 회전이다. 축은 전장을 만드는 쪽이 정한다.</param>
        /// <param name="resolver">스폰한 계층에 주입할 컨테이너이며 null이면 스포너가 주입 없이 세운다.</param>
        /// <returns>실제로 세운 유닛과 그 항목의 짝이며 세운 순서대로이다. 건너뛴 항목은 들어 있지 않다.</returns>
        /// <exception cref="ArgumentNullException">스테이지 데이터가 null이면 발생한다.</exception>
        public IReadOnlyList<PlacedUnit> Place(
            StageData stage,
            TeamId team,
            Quaternion spawnRotation,
            IObjectResolver resolver)
        {
            if (stage == null)
            {
                throw new ArgumentNullException(nameof(stage));
            }

            return Place(stage.UnitPlacements, team, spawnRotation, resolver);
        }

        /// <summary>
        /// 배치 리스트대로 유닛을 세운다.
        /// </summary>
        /// <param name="placements">세울 배치 항목이다.</param>
        /// <param name="team">세운 유닛에 지정할 진영이며, 지정되지 않은 값이면 정의의 기본 진영을 따른다.</param>
        /// <param name="spawnRotation">세운 유닛이 바라볼 회전이다. 축은 전장을 만드는 쪽이 정한다.</param>
        /// <param name="resolver">스폰한 계층에 주입할 컨테이너이며 null이면 스포너가 주입 없이 세운다.</param>
        /// <returns>실제로 세운 유닛과 그 항목의 짝이며 세운 순서대로이다. 건너뛴 항목은 들어 있지 않다.</returns>
        /// <exception cref="ArgumentNullException">배치 리스트가 null이면 발생한다.</exception>
        public IReadOnlyList<PlacedUnit> Place(
            IReadOnlyList<UnitPlacement> placements,
            TeamId team,
            Quaternion spawnRotation,
            IObjectResolver resolver)
        {
            if (placements == null)
            {
                throw new ArgumentNullException(nameof(placements));
            }

            var placed = new List<PlacedUnit>(placements.Count);
            for (var index = 0; index < placements.Count; index++)
            {
                var placement = placements[index];
                if (placement == null)
                {
                    Debug.LogWarning($"[StageUnitPlacer] {index}번 배치 항목이 비어 있어 건너뛴다.");
                    continue;
                }

                var definition = placement.Definition;
                if (definition == null)
                {
                    Debug.LogWarning($"[StageUnitPlacer] {index}번 배치({placement})에 유닛 정의가 없어 건너뛴다.");
                    continue;
                }

                if (!definition.HasUnitPrefab)
                {
                    Debug.LogWarning(
                        $"[StageUnitPlacer] {index}번 배치({placement})의 정의 {definition.DisplayName}에 프리팹이 없어 건너뛴다.",
                        definition);
                    continue;
                }

                var unit = _spawner.Spawn(
                    definition, placement.Position, spawnRotation, team, resolver, placement.Level);
                if (unit == null)
                {
                    Debug.LogWarning(
                        $"[StageUnitPlacer] {index}번 배치({placement})를 세우지 못했다. 스포너가 남긴 경고에 까닭이 있다.",
                        definition);
                    continue;
                }

                placed.Add(new PlacedUnit(unit, placement));
            }

            return placed;
        }

        /// <summary>스포너를 정리한다. 이미 세운 유닛은 부모가 바뀌었으므로 함께 사라지지 않는다.</summary>
        public void Dispose()
        {
            _spawner.Dispose();
        }
    }
}
