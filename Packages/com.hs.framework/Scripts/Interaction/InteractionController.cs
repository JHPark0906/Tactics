using HS.Framework.Ability.Abilities;
using HS.Framework.Interaction.Abilities;
using HS.Framework.Interaction.Targeting;
using R3;
using UnityEngine;

namespace HS.Framework.Interaction
{
    /// <summary>
    /// 탐색된 상호작용 대상의 상태를 관리하고 실행 요청을 처리한다.
    /// 입력 처리와 UI 표시는 상위 게임 계층에서 담당한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>상호작용 시도는 같은 오브젝트에 어빌리티 시스템이 있으면 그것을 거친다.</b> <see cref="TryInteract"/>는
    /// <see cref="GameplayAbilitySystemComponent"/>를 찾으면 <see cref="InteractAbilityDefinition"/>을
    /// (처음이면 만들어) 부여하고 발동을 요청한다. 실제로 무엇을 하는지는 여전히
    /// <see cref="PerformInteraction"/> 하나에 있으며 어빌리티는 그 문을 열 뿐이다.
    /// </para>
    /// <para>
    /// <b>어빌리티 시스템이 없어도 그대로 동작한다.</b> 이 모듈은 GAS 없이도 쓸 수 있는 범용
    /// 상호작용 시스템이어야 한다 — 없으면 <see cref="PerformInteraction"/>을 곧바로 부른다.
    /// 어빌리티 시스템이 있는 쪽은 다른 어빌리티의 차단 태그로 상호작용을 막거나 쿨다운을 거는 등
    /// 표준 GAS 통제를 덤으로 얻는다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class InteractionController : MonoBehaviour, IInteractor
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] [Min(0f)] private float interactionDistance = 2f;
        [SerializeField] private LayerMask interactionMask = Physics.DefaultRaycastLayers;

        private readonly ReactiveProperty<InteractionFocusState> _focusState = new(default);
        private InteractionTargetSensor _targetSensor;
        private GameplayAbilitySystemComponent _abilitySystem;
        private bool _hasCheckedForAbilitySystem;
        private bool _lastPerformInteractionResult;

        /// <summary>상호작용을 수행하는 GameObject이다.</summary>
        public GameObject GameObject => gameObject;

        /// <summary>현재 주시 중인 상호작용 대상이다.</summary>
        public IInteractable FocusedInteractable => _focusState.Value.Interactable;

        /// <summary>현재 대상의 상호작용 가능 상태이다.</summary>
        public InteractionAvailability Availability => _focusState.Value.Availability;

        /// <summary>구독 시 현재 값을 즉시 전달하는 상호작용 포커스 상태 스트림이다.</summary>
        public Observable<InteractionFocusState> Focus => _focusState;

        private void Awake()
        {
            ConfigureTargetSensor();
        }

        /// <summary>
        /// 카메라와 탐색 설정을 지정한다.
        /// </summary>
        public void Initialize(Camera viewCamera)
        {
            this.viewCamera = viewCamera;
            ConfigureTargetSensor();
        }

        /// <summary>
        /// 현재 카메라 시선에서 상호작용 대상을 다시 탐색한다.
        /// </summary>
        public void RefreshFocus()
        {
            if (_targetSensor == null)
            {
                return;
            }

            var focusedInteractable = _targetSensor.FindTarget();
            var availability = focusedInteractable?.Evaluate(this) ?? new InteractionAvailability(false);
            var state = new InteractionFocusState(focusedInteractable, availability);
            if (!_focusState.Value.Equals(state))
            {
                _focusState.Value = state;
            }
        }

        /// <summary>현재 상호작용 포커스를 비운다.</summary>
        public void ClearFocus()
        {
            if (!_focusState.Value.Equals(default))
            {
                _focusState.Value = default;
            }
        }

        /// <summary>
        /// 현재 대상에 상호작용을 요청한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 어빌리티 시스템이 있으면 <see cref="InteractAbilityDefinition"/>의 발동으로 요청하고,
        /// 없으면 <see cref="PerformInteraction"/>을 곧바로 부른다.
        /// </para>
        /// <para>
        /// <b>반환값은 어빌리티 발동 성공이 아니라 실제 상호작용 성공이다.</b> 어빌리티 발동 자체는
        /// 대상이 지금 상호작용 가능한지와 무관하게 성공할 수 있다(코스트·쿨다운·차단 태그만 본다).
        /// 그것을 그대로 돌려주면 「대상이 상호작용 불가라 아무 일도 안 일어났다」와 「실제로 실행됐다」가
        /// 호출하는 쪽에서 구별되지 않는다. <see cref="InteractAbilityDefinition"/>의 어빌리티는 활성화되는
        /// 순간 곧바로 <see cref="PerformInteraction"/>을 불러 그 결과를 남겨 두고, 틱을 재정의하지 않은
        /// 어빌리티는 활성화 호출 안에서 곧바로 끝나므로 이 메서드가 그 결과를 곧바로 읽을 수 있다.
        /// </para>
        /// </remarks>
        /// <returns>실제로 상호작용을 실행했으면 true이다.</returns>
        public bool TryInteract()
        {
            var abilitySystem = ResolveAbilitySystem();
            if (abilitySystem == null)
            {
                return PerformInteraction();
            }

            EnsureInteractAbilityGranted(abilitySystem);
            _lastPerformInteractionResult = false;
            var activationResult = abilitySystem.TryActivate(InteractAbilityDefinition.InteractTag);
            return activationResult == GameplayAbilityActivationResult.Success && _lastPerformInteractionResult;
        }

        /// <summary>
        /// 시선의 대상을 다시 찾고, 가능하면 그 대상에 상호작용을 실행한다.
        /// </summary>
        /// <remarks>
        /// <see cref="TryInteract"/>가 직접 부르거나, <see cref="InteractAbilityDefinition"/>이 발동될 때
        /// 그 어빌리티가 대신 불러 준다. 실제 판단과 실행은 이 메서드 하나에만 있다.
        /// </remarks>
        /// <returns>실제로 상호작용을 실행했으면 true이다.</returns>
        public bool PerformInteraction()
        {
            RefreshFocus();
            var state = _focusState.Value;
            if (state.Interactable == null || !state.Availability.IsAvailable)
            {
                _lastPerformInteractionResult = false;
                return false;
            }

            state.Interactable.Interact(this);
            _lastPerformInteractionResult = true;
            return true;
        }

        private void OnDestroy()
        {
            _focusState.Dispose();
        }

        /// <summary>같은 오브젝트의 어빌리티 시스템 컴포넌트를 찾는다. 없으면 다시 찾지 않는다.</summary>
        /// <returns>찾은 컴포넌트이며 없으면 null이다.</returns>
        private GameplayAbilitySystemComponent ResolveAbilitySystem()
        {
            if (_hasCheckedForAbilitySystem)
            {
                return _abilitySystem;
            }

            _hasCheckedForAbilitySystem = true;
            TryGetComponent(out _abilitySystem);
            return _abilitySystem;
        }

        /// <summary>
        /// 상호작용 시도 어빌리티가 아직 부여되지 않았으면 부여한다.
        /// </summary>
        /// <remarks>
        /// 인스펙터 설정 없이도 상호작용이 되어야 하므로, 런타임에 하나 만들어 그 어빌리티 시스템에만 둔다.
        /// 태그로 부여 여부를 추적하는 <see cref="GameplayAbilitySystem.GrantAbility"/>가 이미 있으면
        /// 아무것도 하지 않으므로 여러 번 불러도 안전하다.
        /// </remarks>
        /// <param name="abilitySystem">어빌리티를 부여할 컴포넌트이다.</param>
        private static void EnsureInteractAbilityGranted(GameplayAbilitySystemComponent abilitySystem)
        {
            if (abilitySystem.System.IsGranted(InteractAbilityDefinition.InteractTag))
            {
                return;
            }

            abilitySystem.System.GrantAbility(InteractAbilityDefinition.CreateRuntime());
        }

        private void ConfigureTargetSensor()
        {
            _targetSensor ??= GetComponent<InteractionTargetSensor>();
            if (_targetSensor == null)
            {
                _targetSensor = gameObject.AddComponent<InteractionTargetSensor>();
            }

            _targetSensor.Configure(viewCamera, interactionDistance, interactionMask);
        }
    }
}
