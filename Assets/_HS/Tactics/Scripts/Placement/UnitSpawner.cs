using System;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Character.Movement;
using HS.Tactics.Units;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HS.Tactics.Placement
{
    /// <summary>
    /// 유닛 정의의 프리팹으로 유닛을 만들고, 조립이 시작되기 전에 진영과 의존성을 갖춰 준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>비활성 상태로 조립하는 이유.</b> <see cref="TacticalUnit"/>은 Awake에서 스스로 조립하며,
    /// 그때 진영이 아직 비어 있으면 유닛 정의의 기본 진영으로 채운다. 배치 시점에 지정한 진영을 살리려면
    /// 조립이 시작되기 전에 진영을 넣어야 하므로, 비활성 임시 부모 아래에 인스턴스를 만들어
    /// Awake를 미룬 채 정의와 진영을 지정하고 의존성까지 주입한 다음 활성화한다.
    /// VContainer의 <see cref="ObjectResolverUnityExtensions.InjectGameObject"/>는 비활성 자식까지 훑으므로
    /// 이 순서에서 빠지는 컴포넌트는 없다.
    /// </para>
    /// <para>
    /// <b>깨어나는 순간 이미 제자리에 있다.</b> 활성화는 실제 부모로 옮기는 순간 일어나므로 좌표는
    /// 그보다 먼저 맞춘다. 좌표는 부르는 쪽이 정한 값을 그대로 쓰며, 배치 격자에서는 항목의 칸 중심이다.
    /// </para>
    /// <para>
    /// <b>의존성 주입.</b> 자동 주입은 씬을 로드할 때만 일어나므로 실행 중에 만든 유닛은 대상이 아니다.
    /// 그래서 넘겨받은 resolver로 계층 전체에 직접 주입한다. resolver가 없으면 주입 없이 세우되,
    /// 그 사실을 한 번은 경고로 남긴다. 주입이 빠진 유닛은 사망 알림도 승패 집계도 없이 조용히 싸우기 때문이다.
    /// </para>
    /// </remarks>
    public sealed class UnitSpawner : IDisposable
    {
        private readonly Transform _unitParent;
        private GameObject _stagingRoot;
        private bool _isDisposed;
        private bool _hasWarnedMissingResolver;

        /// <summary>스폰한 유닛을 담을 부모를 지정해 스포너를 생성한다.</summary>
        /// <param name="unitParent">스폰한 유닛의 부모이며, null이면 씬 최상위에 놓는다.</param>
        public UnitSpawner(Transform unitParent = null)
        {
            _unitParent = unitParent;
        }

        /// <summary>
        /// 유닛 정의의 프리팹으로 유닛을 만들어 지정한 좌표에 세운다.
        /// 정의나 프리팹이 없거나 프리팹에 <see cref="TacticalUnit"/>이 없으면 만들지 않고 null을 반환한다.
        /// </summary>
        /// <param name="definition">스폰할 유닛의 정의이다.</param>
        /// <param name="position">유닛을 세울 월드 좌표이다.</param>
        /// <param name="rotation">유닛이 바라볼 회전이다.</param>
        /// <param name="team">배치 시점에 지정하는 진영이며, 지정되지 않은 값이면 정의의 기본 진영을 따른다.</param>
        /// <param name="resolver">스폰한 계층에 주입할 컨테이너이며 null이면 주입 없이 세우고 한 번 경고한다.</param>
        /// <param name="explicitLevel">
        /// 전투를 구성하는 쪽이 정한 레벨이며, null이면 유닛이 육성 진행에서(없으면 시작 레벨로) 레벨을 정한다.
        /// 조립 전에만 반영되므로 활성화 직전인 이 자리에서 <see cref="TacticalUnit.ConfigureExplicitLevel"/>을 부른다.
        /// </param>
        /// <returns>스폰한 유닛이며 만들지 못했으면 null이다.</returns>
        public TacticalUnit Spawn(
            UnitDefinition definition,
            Vector3 position,
            Quaternion rotation,
            TeamId team,
            IObjectResolver resolver = null,
            int? explicitLevel = null)
        {
            if (_isDisposed)
            {
                Debug.LogWarning("[UnitSpawner] 이미 정리된 스포너로는 유닛을 만들 수 없다.");
                return null;
            }

            if (definition == null)
            {
                Debug.LogWarning("[UnitSpawner] 유닛 정의가 없어 스폰하지 않는다.");
                return null;
            }

            if (!definition.HasUnitPrefab)
            {
                Debug.LogWarning($"[UnitSpawner] {definition.DisplayName} 정의에 프리팹이 없어 스폰하지 않는다.", definition);
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(definition.UnitPrefab, GetStagingRoot());
            if (!instance.TryGetComponent<TacticalUnit>(out var unit))
            {
                Debug.LogWarning(
                    $"[UnitSpawner] {definition.DisplayName} 프리팹에 TacticalUnit이 없어 스폰을 취소한다.", definition);
                DestroyObject(instance);
                return null;
            }

            unit.SetDefinition(definition);
            ApplyTeam(instance, team);
            InjectDependencies(instance, definition, resolver);

            // 활성화(조립) 전이 명시 레벨을 넣을 수 있는 유일한 창이다. 조립이 시작되면 레벨은 더 반영되지 않는다.
            // 레벨은 곡선을 들고 오지 않으므로 주입과의 앞뒤 순서는 상관없다.
            if (explicitLevel.HasValue)
            {
                unit.ConfigureExplicitLevel(explicitLevel.Value);
            }

            Activate(instance, position, rotation);
            return unit;
        }

        /// <summary>임시 부모를 정리한다. 이미 스폰한 유닛은 부모가 바뀌었으므로 함께 사라지지 않는다.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_stagingRoot != null)
            {
                DestroyObject(_stagingRoot);
            }

            _stagingRoot = null;
        }

        /// <summary>
        /// 아직 비활성인 인스턴스의 계층 전체에 컨테이너 의존성을 주입한다.
        /// resolver가 없으면 주입하지 않고, 스포너 하나당 한 번만 경고를 남긴다.
        /// </summary>
        /// <remarks>
        /// 매번 경고하지 않는 이유는 배치가 유닛 수만큼 반복되기 때문이다. 같은 원인을 스무 줄로 찍으면
        /// 그 아래 다른 경고가 묻힌다. 한 번이면 원인을 찾는 데 충분하다.
        /// </remarks>
        private void InjectDependencies(GameObject instance, UnitDefinition definition, IObjectResolver resolver)
        {
            if (resolver != null)
            {
                resolver.InjectGameObject(instance);
                return;
            }

            if (_hasWarnedMissingResolver)
            {
                return;
            }

            _hasWarnedMissingResolver = true;
            Debug.LogWarning(
                $"[UnitSpawner] {definition.DisplayName}을 컨테이너 없이 세웠다. 이 스포너로 만드는 유닛은 " +
                "의존성 주입을 받지 못해 사망 알림과 승패 집계에 연결되지 않는다. " +
                "이 경고는 스포너마다 한 번만 남긴다.",
                instance);
        }

        /// <summary>
        /// 배치 시점에 지정한 진영을 유닛에 적용한다.
        /// 지정되지 않은 진영이면 그대로 두어 조립 시 정의의 기본 진영이 쓰이게 한다.
        /// </summary>
        private static void ApplyTeam(GameObject instance, TeamId team)
        {
            if (!team.IsAssigned)
            {
                return;
            }

            if (instance.TryGetComponent<TeamMember>(out var teamMember))
            {
                teamMember.SetTeam(team);
            }
        }

        /// <summary>조립이 끝난 유닛의 좌표를 먼저 맞춘 뒤 실제 부모로 옮기고 활성화한다.</summary>
        /// <remarks>
        /// <para>
        /// <b>자리를 정한 다음에 붙인다.</b> 프리팹 루트는 활성 상태로 저장되어 있어, 비활성 임시 부모에서
        /// 활성 부모로 옮기는 순간 계층에서 활성이 되고 그 자리에서 Awake가 돈다. 붙인 뒤에 좌표를
        /// 맞추면 Awake는 임시 부모의 자리(원점이나 부모 위치)에서 돌고, 이동 구성요소가 그때 읽은
        /// 로직 위치가 엉뚱한 곳이 된다.
        /// </para>
        /// <para>
        /// 월드 자리를 지킨 채 옮긴다. 임시 부모와 실제 부모의 자세가 같다는 전제에 기대지 않기 위해서다.
        /// 마지막의 활성화는 프리팹 루트가 비활성으로 저장된 경우를 위한 것이며, 그 경우에도 좌표는
        /// 이미 맞춰져 있다.
        /// </para>
        /// </remarks>
        private void Activate(GameObject instance, Vector3 position, Quaternion rotation)
        {
            if (instance.TryGetComponent<PlanarCharacterMover>(out var mover))
            {
                mover.Relocate(position, rotation);
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
            }

            instance.transform.SetParent(_unitParent, true);
            instance.SetActive(true);
        }

        /// <summary>
        /// 조립하는 동안 인스턴스를 담아 둘 비활성 부모를 준비한다.
        /// 부모가 비활성이면 자식의 Awake가 미뤄지므로, 진영과 의존성을 먼저 갖출 수 있다.
        /// </summary>
        private Transform GetStagingRoot()
        {
            if (_stagingRoot != null)
            {
                return _stagingRoot.transform;
            }

            _stagingRoot = new GameObject("UnitSpawnStaging");
            _stagingRoot.SetActive(false);
            if (_unitParent != null)
            {
                _stagingRoot.transform.SetParent(_unitParent, false);
            }

            return _stagingRoot.transform;
        }

        /// <summary>실행 중인지에 따라 알맞은 방식으로 오브젝트를 파괴한다.</summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
