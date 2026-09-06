using HS.Framework.Gameplay.Health;
using HS.Tactics.Units;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 유닛 프리팹에 붙어 자신을 승패 집계에 등록하고, 비활성화·파괴 시 스스로 해제하는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>스폰 시 등록 방법.</b> 이 컴포넌트를 유닛 프리팹에 붙여 두면 배치 코드는 스폰 직후
    /// VContainer의 <c>IObjectResolver.InjectGameObject</c>로 계층에 주입하기만 하면 되고, 등록 호출을 따로 하지 않는다.
    /// 유닛 조립과 마찬가지로 스폰한 쪽의 주입이 전제 조건이며, 주입이 없으면 등록되지 않는다.
    /// </para>
    /// <para>
    /// <b>사망과의 관계.</b> 사망은 프레임워크의 사망 이벤트로 집계에 반영되고, 이 컴포넌트의 해제는
    /// 회수·파괴 경로만 담당한다. 사망 직후 오브젝트가 비활성화되어 해제가 뒤따라도 이미 집계에서 빠진 뒤라
    /// 중복으로 처리되지 않으며, 해제만으로는 승패가 판정되지 않는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TacticalUnit))]
    public sealed class BattleUnitRegistrant : MonoBehaviour
    {
        private IBattleUnitRegistry _registry;
        private TacticalUnit _unit;
        private bool _isRegistered;

        /// <summary>현재 승패 집계에 등록되어 있는지 여부이다.</summary>
        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// 등록 대상 집계를 주입받는다. 이미 다른 집계에 등록되어 있었으면 거기서 빠진 뒤 새 집계에 등록한다.
        /// 같은 집계가 다시 오면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="registry">전투 유닛 집계 계약이며, 씬에 집계 서비스가 없으면 null이 온다.</param>
        [Inject]
        public void InjectRegistry(IBattleUnitRegistry registry)
        {
            if (ReferenceEquals(_registry, registry))
            {
                return;
            }

            Unregister();
            _registry = registry;
            Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            // 사망은 집계가 알림으로 받으므로, 죽어서 꺼지는 유닛을 회수 경로가 한 번 더 빼지 않는다.
            // 다만 사망이 확정될 때만 건너뛴다 — 확정할 수 없으면(예: 유닛 정의가 없어 체력 어트리뷰트에
            // 아직 묶이지 못한 채 꺼지는 유닛) 회수 경로를 막을 이유가 없다.
            if (TryGetComponent<IDamageable>(out var health) && IsConfirmedDead(health))
            {
                _isRegistered = false;
                return;
            }

            Unregister();
        }

        /// <summary>
        /// 넘겨받은 대상이 죽었다고 확정할 수 있는지 답한다.
        /// </summary>
        /// <remarks>
        /// <see cref="HealthAttributeComponent"/>는 체력 어트리뷰트에 묶이기 전에는 <see cref="IDamageable.IsDead"/>가
        /// 언제나 "살아 있음"으로 답하고, 묻는 것 자체가 <c>EnsureBound</c>의 오류 로그를 낸다 — 그래서 이
        /// 구현에 한해 묶였을 때만 답을 믿는다. 다른 <see cref="IDamageable"/> 구현은 그런 부작용이 없으므로
        /// 그대로 믿는다.
        /// </remarks>
        /// <param name="damageable">사망 여부를 물을 대상이다.</param>
        /// <returns>죽었다고 확정할 수 있으면 true이다.</returns>
        private static bool IsConfirmedDead(IDamageable damageable)
        {
            return damageable is HealthAttributeComponent health ? health.IsBound && health.IsDead : damageable.IsDead;
        }

        private void OnDestroy()
        {
            Unregister();
            _registry = null;
        }

        /// <summary>
        /// 집계와 유닛이 모두 준비되고 활성 상태일 때 자신을 등록한다.
        /// 유닛 참조는 여기서 늦게 찾는다. 스폰 경로는 비활성 상태에서 주입을 먼저 받으므로,
        /// Awake에서만 참조를 잡으면 주입 시점과 조립 시점 중 어느 쪽이 앞서느냐에 결과가 달라지기 때문이다.
        /// </summary>
        private void Register()
        {
            if (_isRegistered || !isActiveAndEnabled || _registry == null)
            {
                return;
            }

            if (_unit == null)
            {
                _unit = GetComponent<TacticalUnit>();
            }

            if (_unit == null)
            {
                return;
            }

            _isRegistered = _registry.RegisterUnit(_unit);
        }

        /// <summary>등록되어 있으면 집계에서 자신을 해제한다.</summary>
        private void Unregister()
        {
            if (!_isRegistered || _registry == null)
            {
                _isRegistered = false;
                return;
            }

            _registry.UnregisterUnit(gameObject);
            _isRegistered = false;
        }
    }
}
