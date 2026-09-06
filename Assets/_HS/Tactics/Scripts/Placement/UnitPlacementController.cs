using System;
using System.Collections.Generic;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Character.Movement;
using HS.Tactics.Units;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 배치 단계의 상태와 규칙을 관리하고, 배치한 유닛을 실제로 스폰하며, 전투 개시를 선언한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>핵심 루프에서의 위치.</b> 스테이지에 진입하면 <see cref="BeginPlacement"/>로 배치 단계를 연다.
    /// 배치 단계 동안 플레이어는 유닛을 놓고, 옮기고, 회수할 수 있다. 전투는 저절로 시작되지 않고
    /// 플레이어가 <see cref="TryStartBattle"/>를 명시적으로 부를 때(보통 전투 개시 버튼) 시작한다.
    /// </para>
    /// <para>
    /// <b>규칙과 세계의 분리.</b> 배치 규칙은 순수 클래스인 <see cref="UnitPlacementPlan"/>이 판정하고,
    /// 이 컴포넌트는 판정을 통과한 요청만 세계에 반영한다. 그래서 규칙은 씬 없이 검증할 수 있고,
    /// 계획과 실제 유닛 인스턴스가 어긋날 여지도 이 클래스 안으로 좁혀진다.
    /// </para>
    /// <para>
    /// <b>승패 판정기와의 경계.</b> 유닛을 스폰할 때마다 <see cref="UnitPlaced"/>를,
    /// 회수할 때마다 <see cref="UnitRecalled"/>를 발생시킨다. 승패 판정기가 생존 유닛을 집계하려면
    /// 이 두 이벤트를 구독하면 되며, 등록 API는 판정기 쪽이 정의하므로 여기서는 알림만 내보낸다.
    /// </para>
    /// <para>
    /// <b>배치 책임만 진다.</b> 이 컨트롤러는 배치와 전투 개시 선언까지만 하고, 그 사실을 듣고 자기 상태를
    /// 바꾸는 일은 하지 않는다. <see cref="TryStartBattle"/>이 성공하면 <see cref="PlacementCompletedEvent"/>를
    /// 발행할 뿐이며, 그것을 구독해 무엇을 할지는 듣는 쪽(<see cref="HS.Framework.Gameplay.GameState"/> 구현)이
    /// 정한다. 이 컨트롤러가 받는 쪽을 알거나 직접 참조하는 일은 없다.
    /// </para>
    /// <para>
    /// <b>입력 차단 토큰을 쓰지 않는 이유.</b> 배치 조작은 단계로 걸러내므로 전투 중에 배치 요청이 들어와도
    /// 그대로 거부된다. 게다가 이 게임은 자동 전투라 플레이어가 전투 중 세계에 개입할 수단 자체가 없고,
    /// 유일하게 남는 조작은 전투를 지켜보기 위한 시점 조작이다. 여기서 게임플레이 입력을 전역으로 막으면
    /// 막을 대상은 없이 관전 수단만 사라진다. 그래서 이 컨트롤러는 차단 토큰을 획득하지 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitPlacementController : MonoBehaviour
    {
        /// <summary>
        /// 스테이지당 배치할 수 있는 유닛 수의 기본값이며 <see cref="PartyRules.MaxPartySize"/>를 따른다.
        /// 배치 상한은 유닛 수로 계산한다.
        /// </summary>
        public const int DefaultPlacementCapacity = UnitPlacementPlan.DefaultCapacity;

        [Header("Placement")]
        [Tooltip("유닛을 놓을 수 있는 구역이다. 스테이지 씬에 배치한 구역을 연결한다.")]
        [SerializeField]
        private UnitPlacementZone placementZone;

        [Tooltip("이 스테이지에서 배치할 수 있는 유닛 수이다. 스테이지별로 더 낮게 잡을 수 있으나\n" +
                 "파티 규칙이 정한 상한보다 크게는 잡을 수 없다.")]
        [SerializeField]
        [Range(1, PartyRules.MaxPartySize)]
        private int placementCapacity = DefaultPlacementCapacity;

        [Tooltip("배치한 유닛에 지정할 진영이다. 지정되지 않은 값이면 유닛 정의의 기본 진영을 따른다.")]
        [SerializeField]
        private TeamId playerTeam = new(1);

        [Tooltip("스폰한 유닛을 담을 부모이다. 비워 두면 씬 최상위에 놓는다.")]
        [SerializeField]
        private Transform spawnedUnitParent;

        [Tooltip("컴포넌트가 켜질 때 배치 단계를 자동으로 시작할지 여부이다.")]
        [SerializeField]
        private bool beginPlacementOnEnable = true;

        private readonly Dictionary<int, TacticalUnit> _spawnedUnits = new();
        private UnitPlacementPlan _plan;
        private UnitSpawner _spawner;
        private IObjectResolver _resolver;
        private IPublisher<PlacementCompletedEvent> _placementCompletedPublisher;

        /// <summary>현재 배치 단계이다.</summary>
        public PlacementPhase Phase { get; private set; } = PlacementPhase.Preparing;

        /// <summary>배치 규칙을 판정하는 계획이며 처음 접근할 때 구역과 상한을 반영해 만든다.</summary>
        public UnitPlacementPlan Plan
        {
            get
            {
                _plan ??= new UnitPlacementPlan(GetPlacementArea(), placementCapacity);
                return _plan;
            }
        }

        /// <summary>현재 배치한 유닛 수이다.</summary>
        public int PlacedUnitCount => Plan.Count;

        /// <summary>이 스테이지에서 배치할 수 있는 유닛 수의 상한이다.</summary>
        public int PlacementCapacity => Plan.Capacity;

        /// <summary>더 배치할 수 있는 남은 수이다.</summary>
        public int RemainingCapacity => Plan.RemainingCapacity;

        /// <summary>지금 배치 조작을 받아들이는 단계인지 여부이다.</summary>
        public bool IsPlacementInputAllowed => Phase == PlacementPhase.Placing;

        /// <summary>전투가 시작됐는지 여부이다. 배치가 끝나 전투 단계에 들어갔으면 참이다.</summary>
        /// <remarks>
        /// <para>
        /// <b>「전투가 시작됐는가」를 판정하는 자리는 여기 하나다.</b> 유닛이든 다른 무엇이든 단계를 각자 읽어
        /// 해석하면 같은 판정이 여러 곳에 생기고, 어느 하나가 다르게 읽는 순간 유닛마다 다르게 군다.
        /// 값을 받아 두지 않고 물을 때마다 단계에서 셈한다.
        /// </para>
        /// <para>
        /// 이름을 단계에서 따온 것은 승패 집계에도 <c>HasBattleStarted</c>가 있기 때문이다. 그쪽은 아군과 적이
        /// 각각 하나씩 등록되어 집계가 판정을 내릴 수 있게 됐다는 사실이고, 이쪽은 배치가 끝났다는 사실이다.
        /// </para>
        /// </remarks>
        public bool IsBattlePhase => Phase == PlacementPhase.Battle;

        /// <summary>배치 단계가 바뀐 뒤 발생하며 UI가 표시를 갱신할 때 구독한다.</summary>
        public event Action<PlacementPhase> PhaseChanged;

        /// <summary>배치한 항목이 바뀐 뒤 발생하며, 남은 배치 수 표시를 갱신할 때 구독한다.</summary>
        public event Action<UnitPlacementController> PlacementChanged;

        /// <summary>
        /// 유닛을 스폰한 직후 발생한다.
        /// 승패 판정기가 생존 유닛 집계에 유닛을 등록하는 지점이며, 등록 API는 판정기 쪽이 정의한다.
        /// </summary>
        public event Action<TacticalUnit> UnitPlaced;

        /// <summary>배치한 유닛을 회수해 없애기 직전에 발생하며, 집계에서 제외할 때 구독한다.</summary>
        public event Action<TacticalUnit> UnitRecalled;

        /// <summary>VContainer를 사용하는 런타임 스폰 주입 경로이다.</summary>
        [Inject]
        public void InjectResolver(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// 배치 완료를 알릴 발행자를 주입받는다.
        /// </summary>
        /// <remarks>
        /// 없어도 배치와 전투 개시 자체는 그대로 동작한다 — 이 컨트롤러의 책임은 배치이지 알림의 성패가 아니다.
        /// 받는 쪽(<see cref="HS.Framework.Gameplay.GameState"/> 구현)이 없는 무대에서는 발행자도 등록되지
        /// 않을 수 있으므로, 없으면 그냥 발행하지 않는다.
        /// </remarks>
        /// <param name="placementCompletedPublisher">배치 완료 이벤트의 발행자이며 없을 수 있다.</param>
        [Inject]
        public void InjectPlacementCompletedPublisher(
            IPublisher<PlacementCompletedEvent> placementCompletedPublisher)
        {
            _placementCompletedPublisher = placementCompletedPublisher;
        }

        /// <summary>
        /// 배치 단계를 시작한다. 구역과 상한을 계획에 다시 반영하므로,
        /// 스테이지를 다시 도전할 때 호출하면 새 구역 설정이 적용된다.
        /// </summary>
        public void BeginPlacement()
        {
            Plan.SetArea(GetPlacementArea());
            Plan.SetCapacity(placementCapacity);
            SetPhase(PlacementPhase.Placing);
        }

        /// <summary>
        /// 유닛을 배치하고 스폰한다. 배치 단계가 아니거나 규칙을 위반하면 아무것도 만들지 않는다.
        /// </summary>
        /// <param name="definition">배치할 유닛의 정의이다.</param>
        /// <param name="worldPosition">배치할 월드 좌표이다.</param>
        /// <param name="spawnedUnit">스폰한 유닛이며 실패하면 null이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryPlaceUnit(
            UnitDefinition definition,
            Vector3 worldPosition,
            out TacticalUnit spawnedUnit)
        {
            spawnedUnit = null;
            if (!IsPlacementInputAllowed)
            {
                return PlacementResult.NotInPlacingPhase;
            }

            if (definition != null && !definition.HasUnitPrefab)
            {
                return PlacementResult.MissingUnitPrefab;
            }

            var result = Plan.TryPlace(definition, worldPosition, out var entry);
            return result != PlacementResult.Success ? result : SpawnForEntry(entry, out spawnedUnit);
        }

        /// <summary>
        /// 격자 칸을 골라 유닛을 배치하고 스폰한다. 배치 UI의 칸 버튼이 부르는 자리이다.
        /// </summary>
        /// <param name="definition">배치할 유닛의 정의이다.</param>
        /// <param name="cellIndex">유닛을 세울 칸의 번호이다.</param>
        /// <param name="spawnedUnit">스폰한 유닛이며 실패하면 null이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryPlaceUnitAtCell(
            UnitDefinition definition,
            int cellIndex,
            out TacticalUnit spawnedUnit)
        {
            spawnedUnit = null;
            if (!IsPlacementInputAllowed)
            {
                return PlacementResult.NotInPlacingPhase;
            }

            if (definition != null && !definition.HasUnitPrefab)
            {
                return PlacementResult.MissingUnitPrefab;
            }

            var result = Plan.TryPlaceAtCell(definition, cellIndex, out var entry);
            return result != PlacementResult.Success ? result : SpawnForEntry(entry, out spawnedUnit);
        }

        /// <summary>
        /// 계획이 받아들인 항목을 세계에 만든다.
        /// </summary>
        /// <remarks>
        /// <b>스폰 좌표는 항목의 좌표이지 요청받은 좌표가 아니다.</b> 격자에서는 유닛이 칸의 중심에 서므로
        /// 클릭한 지점과 실제로 서는 자리가 다르다. 요청 좌표로 스폰하면 계획은 칸의 중심을 기억하는데
        /// 세계의 유닛은 다른 곳에 서서, <b>회수·이동이 엉뚱한 자리를 가리키게 된다.</b>
        /// </remarks>
        /// <param name="entry">계획이 받아들인 배치 항목이다.</param>
        /// <param name="spawnedUnit">스폰한 유닛이며 실패하면 null이다.</param>
        /// <returns>처리 결과이다.</returns>
        private PlacementResult SpawnForEntry(PlacementEntry entry, out TacticalUnit spawnedUnit)
        {
            spawnedUnit = GetSpawner().Spawn(
                entry.Definition,
                entry.Position,
                GetSpawnRotation(),
                playerTeam,
                _resolver);
            if (spawnedUnit == null)
            {
                Plan.TryRecall(entry.Id, out _);
                return PlacementResult.SpawnFailed;
            }

            _spawnedUnits[entry.Id] = spawnedUnit;
            PlacementChanged?.Invoke(this);
            UnitPlaced?.Invoke(spawnedUnit);
            return PlacementResult.Success;
        }

        /// <summary>
        /// 이미 배치한 유닛을 구역 안의 다른 좌표로 옮긴다. 배치 단계에서는 이동이 자유롭다.
        /// </summary>
        /// <param name="entryId">옮길 배치 항목의 식별자이다.</param>
        /// <param name="worldPosition">새로 배치할 월드 좌표이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryMoveUnit(int entryId, Vector3 worldPosition)
        {
            if (!IsPlacementInputAllowed)
            {
                return PlacementResult.NotInPlacingPhase;
            }

            var result = Plan.TryMove(entryId, worldPosition, out var movedEntry);
            if (result != PlacementResult.Success)
            {
                return result;
            }

            if (_spawnedUnits.TryGetValue(entryId, out var unit) && unit != null)
            {
                if (unit.TryGetComponent<PlanarCharacterMover>(out var mover))
                {
                    mover.Relocate(movedEntry.Position, GetSpawnRotation());
                }
                else
                {
                    unit.transform.SetPositionAndRotation(movedEntry.Position, GetSpawnRotation());
                }
            }

            PlacementChanged?.Invoke(this);
            return PlacementResult.Success;
        }

        /// <summary>
        /// 배치한 유닛을 회수해 없앤다. 회수한 자리는 즉시 다시 채울 수 있다.
        /// </summary>
        /// <param name="entryId">회수할 배치 항목의 식별자이다.</param>
        /// <returns>처리 결과이다.</returns>
        public PlacementResult TryRecallUnit(int entryId)
        {
            if (!IsPlacementInputAllowed)
            {
                return PlacementResult.NotInPlacingPhase;
            }

            var result = Plan.TryRecall(entryId, out _);
            if (result != PlacementResult.Success)
            {
                return result;
            }

            DespawnUnit(entryId);
            PlacementChanged?.Invoke(this);
            return PlacementResult.Success;
        }

        /// <summary>
        /// 플레이어의 선언으로 전투를 시작한다.
        /// 배치 단계가 아니거나 배치한 유닛이 하나도 없으면 시작하지 않는다.
        /// </summary>
        /// <remarks>
        /// 단계를 옮긴 직후 <see cref="PlacementCompletedEvent"/>를 발행한다. 배치가 끝났다는 사실에
        /// 반응하고 싶은 쪽(<see cref="HS.Framework.Gameplay.GameState"/> 구현)은 이 컨트롤러를 직접
        /// 참조하지 않고 이 이벤트를 구독하면 된다.
        /// </remarks>
        /// <returns>전투를 시작했으면 true이다.</returns>
        public bool TryStartBattle()
        {
            if (Phase != PlacementPhase.Placing || Plan.Count <= 0)
            {
                return false;
            }

            SetPhase(PlacementPhase.Battle);
            _placementCompletedPublisher?.Publish(default);
            return true;
        }

        /// <summary>배치 항목에 대응하는 유닛 인스턴스를 찾는다.</summary>
        /// <param name="entryId">찾을 배치 항목의 식별자이다.</param>
        /// <param name="unit">찾은 유닛이며 없으면 null이다.</param>
        /// <returns>살아 있는 유닛을 찾았으면 true이다.</returns>
        public bool TryGetSpawnedUnit(int entryId, out TacticalUnit unit)
        {
            if (_spawnedUnits.TryGetValue(entryId, out unit) && unit != null)
            {
                return true;
            }

            unit = null;
            return false;
        }

        private void OnEnable()
        {
            if (beginPlacementOnEnable && Phase == PlacementPhase.Preparing)
            {
                BeginPlacement();
            }
        }

        private void OnDestroy()
        {
            _spawner?.Dispose();
            _spawner = null;
            _spawnedUnits.Clear();
        }

        /// <summary>단계를 바꾸고 입력 차단과 구독자에게 반영한다. 같은 단계면 아무것도 하지 않는다.</summary>
        private void SetPhase(PlacementPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>회수한 배치 항목의 유닛 인스턴스를 알리고 없앤다.</summary>
        private void DespawnUnit(int entryId)
        {
            if (!_spawnedUnits.Remove(entryId, out var unit) || unit == null)
            {
                return;
            }

            UnitRecalled?.Invoke(unit);
            if (Application.isPlaying)
            {
                Destroy(unit.gameObject);
            }
            else
            {
                DestroyImmediate(unit.gameObject);
            }
        }

        /// <summary>연결된 구역의 배치 영역을 가져오며, 구역이 없으면 빈 영역을 돌려준다.</summary>
        private PlacementArea GetPlacementArea()
        {
            return placementZone != null ? placementZone.Area : PlacementArea.None;
        }

        /// <summary>배치한 유닛이 바라볼 회전을 가져오며, 구역이 없으면 이 오브젝트의 회전을 쓴다.</summary>
        private Quaternion GetSpawnRotation()
        {
            return placementZone != null ? placementZone.SpawnRotation : transform.rotation;
        }

        /// <summary>스폰에 사용할 스포너를 준비한다.</summary>
        private UnitSpawner GetSpawner()
        {
            return _spawner ??= new UnitSpawner(spawnedUnitParent);
        }

    }
}
