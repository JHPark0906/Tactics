using HS.Framework.Gameplay.Teams;
using HS.Framework.Scene;
using HS.Tactics.Lane;
using HS.Tactics.Pathfinding;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Battlefield
{
    /// <summary>
    /// 스테이지 데이터로 전장을 런타임에 만든다. 바닥 타일을 깔고, 레인의 시종점을 채우고, 전장 경계의 크기를 정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>지오메트리는 <c>Awake</c>에서, 보고는 <c>[Inject]</c>에서.</b> 씬의 <c>Awake</c>는 어떤 <c>[Inject]</c>보다
    /// 먼저 돌므로, 여기서 바닥·레인·경계를 세워 두면 씬에 놓인 유닛이 주입받을 때 경로 계획 서비스가 찾는 경계가
    /// 이미 최종 크기다. 경계는 첫 경로 계획 때 그래프에 굳어져 뒤늦게 바꿀 수 없으므로 이 순서를 바꾸지 않는다.
    /// 컨테이너가 없어도(에디터에서 씬을 바로 재생) 바닥은 생긴다.
    /// </para>
    /// <para>
    /// <b>초기화 진행률은 <c>[Inject]</c> 본문 안에서만 보고한다.</b> 주입은 씬 오브젝트가 다 만들어진 뒤,
    /// 씬 전환의 완료 게이트가 도는 것보다 앞서 동기로 온다. <c>Awake</c>에는 서비스 참조가 없고 <c>Start</c>는
    /// 게이트 뒤라 그때의 보고는 무시된다. 그래서 첫 줄에서 0을, 마지막 줄에서 1을 보고하고 그 뒤에는 아무것도
    /// 두지 않는다 — 1을 보고하는 순간 전환이 그 자리에서 완료될 수 있다.
    /// </para>
    /// <para>
    /// <b>생성은 동기다.</b> 배치 재생이 로드 다음 프레임에 유닛을 세우고 전투를 개시하므로, 생성이 여러 프레임에
    /// 걸치면 바닥과 적이 없는 상태에서 전투가 시작된다.
    /// </para>
    /// <para>
    /// 타일 자리와 경계 크기의 산술은 <see cref="BattlefieldTiling"/>이 낸다. 이 컴포넌트는 그 값대로 세울 뿐이며,
    /// 자신의 transform이 원점·무회전이라는 전제로 자식을 그 좌표에 놓는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattlefieldGenerator : MonoBehaviour
    {
        private const string BoundsObjectName = "BattleBounds";

        [Tooltip("이 씬이 만들 스테이지의 데이터이다. 길이가 바닥의 크기를 정한다.")]
        [SerializeField]
        private StageData stageData;

        [Tooltip("몸통 바닥 타일 프리팹이다. 12×12 m이고 원점이 XZ 중심이어야 한다.")]
        [SerializeField]
        private GameObject bodyTilePrefab;

        [Tooltip("끝단 바닥 타일 프리팹이다. 폭 12 m·길이 8 m이고 원점이 z=0 가장자리에 있어 +Z로 뻗어야 한다.")]
        [SerializeField]
        private GameObject capPrefab;

        [Tooltip("시종점을 채울 레인이다. 비워 두면 씬의 대표 레인을 쓴다.")]
        [SerializeField]
        private BattleLane lane;

        [Tooltip("배치 리스트의 유닛을 세울 진영이다. 전장이 세우는 유닛은 모두 이 진영이다.")]
        [SerializeField]
        private TeamId enemyTeam = new(2);

        private const string EnemyUnitsObjectName = "EnemyUnits";

        private bool _isGenerated;
        private StageUnitPlacer _placer;

        /// <summary>바닥·레인·경계를 이미 세웠는지 여부이다.</summary>
        public bool IsGenerated => _isGenerated;

        /// <summary>배치 리스트의 유닛을 세울 진영이다.</summary>
        public TeamId EnemyTeam => enemyTeam;

        private void Awake()
        {
            GenerateBattlefield();
        }

        private void OnDestroy()
        {
            // 배치기는 스포너의 스테이징 루트만 정리한다. 이미 세운 유닛은 씬 루트 아래 있어 함께 사라지지 않는다.
            _placer?.Dispose();
            _placer = null;
        }

        /// <summary>
        /// 씬 전환 서비스와 해석기를 주입받아, 초기화 진행률을 보고하면서 배치 리스트의 유닛을 세운다.
        /// </summary>
        /// <remarks>
        /// 첫 줄이 0을, 마지막 줄이 1을 보고한다. 전환 중이 아니면(에디터에서 씬을 바로 재생) 보고는 조용히 무시된다.
        /// 1은 스폰이 던져도 반드시 보고한다 — 빠뜨리면 전환이 영원히 끝나지 않아 로딩 화면이 닫히지 않는다.
        /// 1을 보고한 뒤에는 아무것도 두지 않는다 — 그 자리에서 전환이 완료될 수 있다.
        /// </remarks>
        /// <param name="sceneTransition">초기화 진행률을 받을 씬 전환 서비스이다.</param>
        /// <param name="resolver">세운 유닛에 의존성을 주입할 해석기이다.</param>
        [Inject]
        public void InjectRuntimeDependencies(ISceneTransitionService sceneTransition, IObjectResolver resolver)
        {
            sceneTransition?.ReportInitializationProgress(0f);
            try
            {
                SpawnPlacements(resolver);
            }
            finally
            {
                sceneTransition?.ReportInitializationProgress(1f);
            }
        }

        /// <summary>
        /// 바닥 타일을 깔고 레인의 시종점을 채우고 전장 경계의 크기를 정한다. 두 번째 호출부터는 아무것도 하지 않는다.
        /// </summary>
        /// <remarks>
        /// <c>Awake</c>가 부르며, 수명주기가 돌지 않는 에디트 모드에서는 직접 부른다. 스테이지 데이터나 프리팹이
        /// 비어 있으면 경고만 남기고 세우지 않는다.
        /// </remarks>
        public void GenerateBattlefield()
        {
            if (_isGenerated)
            {
                return;
            }

            if (stageData == null)
            {
                Debug.LogWarning($"[BattlefieldGenerator] {name}에 스테이지 데이터가 없어 전장을 만들지 않는다.", this);
                return;
            }

            if (bodyTilePrefab == null || capPrefab == null)
            {
                Debug.LogWarning($"[BattlefieldGenerator] {name}에 바닥 타일 프리팹이 비어 있어 전장을 만들지 않는다.", this);
                return;
            }

            var tileCount = BattlefieldTiling.GetBodyTileCount(stageData.BattlefieldLength);
            for (var index = 0; index < tileCount; index++)
            {
                PlaceTile(bodyTilePrefab, $"BodyTile_{index}",
                    BattlefieldTiling.GetBodyTilePosition(index, tileCount), Quaternion.identity);
            }

            var capPlus = PlaceTile(capPrefab, "CapPlus",
                BattlefieldTiling.GetPlusCapPosition(tileCount), BattlefieldTiling.PlusCapRotation);
            var capMinus = PlaceTile(capPrefab, "CapMinus",
                BattlefieldTiling.GetMinusCapPosition(tileCount), BattlefieldTiling.MinusCapRotation);

            var boundsObject = new GameObject(BoundsObjectName);
            boundsObject.transform.SetParent(transform, false);
            boundsObject.AddComponent<BattleBounds>().SetHalfExtents(BattlefieldTiling.GetBoundsHalfExtents(tileCount));

            // 시작점은 +Z 끝단, 종점은 -Z 끝단이다. 어느 진영이 시작점에서 종점으로 나아가는지는 레인이 이미
            // 갖고 있는 값을 그대로 둔다 — 전진 방향은 레인의 몫이지 바닥을 까는 쪽의 몫이 아니다.
            var targetLane = lane != null ? lane : BattleLane.Active;
            if (targetLane != null)
            {
                targetLane.SetLane(capPlus.transform, capMinus.transform, targetLane.ForwardTeam);
            }
            else
            {
                Debug.LogWarning($"[BattlefieldGenerator] {name}이 레인을 찾지 못해 시종점을 채우지 못한다.", this);
            }

            _isGenerated = true;
        }

        /// <summary>배치 리스트의 유닛을 <see cref="enemyTeam"/> 진영으로 세운다. 두 번째 호출부터는 아무것도 하지 않는다.</summary>
        /// <remarks>
        /// <para>
        /// 유닛은 이 순간 새로 만드는 씬 루트 아래에 세운다 — 이 컴포넌트의 자식으로 세우면 컨테이너의 계층 주입이
        /// 컴포넌트를 주입한 뒤 자식을 재귀로 훑어 그 유닛을 한 번 더 주입한다. 씬 루트 목록은 주입이 시작될 때
        /// 스냅숏이라 그 뒤에 만든 루트는 다시 방문되지 않는다.
        /// </para>
        /// <para>
        /// 세울 것이 없으면 루트도 만들지 않는다 — 빈 스테이지나 검사 무대에 빈 오브젝트를 남기지 않는다.
        /// 회전은 identity(+Z, 아군 쪽을 봄)이다.
        /// </para>
        /// </remarks>
        /// <param name="resolver">세운 유닛에 의존성을 주입할 해석기이며 없으면 null이다.</param>
        public void SpawnPlacements(IObjectResolver resolver)
        {
            if (_placer != null || stageData == null || stageData.UnitPlacements.Count == 0)
            {
                return;
            }

            var enemyUnitsRoot = new GameObject(EnemyUnitsObjectName);
            _placer = new StageUnitPlacer(enemyUnitsRoot.transform);
            _placer.Place(stageData, enemyTeam, Quaternion.identity, resolver);
        }

        /// <summary>프리팹을 자식으로 세워 이름과 월드 자리를 준다.</summary>
        private GameObject PlaceTile(GameObject prefab, string tileName, Vector3 position, Quaternion rotation)
        {
            var tile = Instantiate(prefab, position, rotation, transform);
            tile.name = tileName;
            return tile;
        }
    }
}
