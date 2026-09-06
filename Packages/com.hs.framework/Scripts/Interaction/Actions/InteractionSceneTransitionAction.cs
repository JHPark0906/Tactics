using HS.Framework.Ability.Abilities;
using HS.Framework.Interaction.Abilities;
using HS.Framework.Scene;
using UnityEngine;
using VContainer;

namespace HS.Framework.Interaction.Actions
{
    /// <summary>
    /// 지정한 씬으로 이동하는 범용 상호작용 동작이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>실행 자체는 상호작용 주체의 어빌리티 시스템에 맡긴다.</b> 이 컴포넌트는 세 가지만 한다 —
    /// 인스펙터에서 도착지·경유 여부를 받는 것, 씬 전환 서비스를 주입받는 것(어빌리티는 평범한 C# 클래스라
    /// <c>[Inject]</c>를 직접 받을 수 없다), 그리고 그 둘을 <see cref="InteractionSceneTransitionPayload"/>에
    /// 실어 <see cref="InteractionSceneTransitionAbilityDefinition"/>을 트리거로 여는 게임플레이 이벤트로
    /// 보내는 것이다. 씬 로드와 중복 요청 차단은 해당 어빌리티가 담당한다.
    /// </para>
    /// <para>
    /// 상호작용 주체에게 어빌리티 시스템이 없으면 이벤트를 받을 자리 자체가 없으므로 아무 일도 하지 않고
    /// 한 번만 경고한다.
    /// </para>
    /// </remarks>
    public sealed class InteractionSceneTransitionAction : MonoBehaviour, IInteractionAction
    {
        [SerializeField] private bool useLoadingScene = true;
        [SerializeField] private SceneReference loadingScene;
        [SerializeField] private SceneReference destinationScene;

        private ISceneTransitionService _sceneTransitionService;
        private bool _hasWarnedMissingAbilitySystem;

        /// <summary>씬 전환 서비스를 주입받는다. 주입 전에는 실행 요청을 무시한다.</summary>
        /// <param name="sceneTransitionService">씬 전환을 맡는 프레임워크 서비스이다.</param>
        [Inject]
        public void InjectSceneTransitionService(ISceneTransitionService sceneTransitionService)
        {
            _sceneTransitionService = sceneTransitionService;
        }

        /// <inheritdoc />
        public void Execute(IInteractor interactor)
        {
            if (_sceneTransitionService == null || destinationScene == null || !destinationScene.IsAssigned)
            {
                return;
            }

            var owner = interactor?.GameObject;
            if (owner == null || !owner.TryGetComponent<GameplayAbilitySystemComponent>(out var abilitySystem))
            {
                WarnMissingAbilitySystemOnce();
                return;
            }

            InteractionSceneTransitionAbilityDefinition.EnsureGranted(abilitySystem);

            var payload = new InteractionSceneTransitionPayload(
                _sceneTransitionService,
                destinationScene,
                useLoadingScene ? loadingScene : null);
            abilitySystem.SendGameplayEvent(InteractionSceneTransitionAbilityDefinition.TriggerEventTag, payload);
        }

        /// <summary>상호작용 주체에 어빌리티 시스템이 없다는 것을 한 번만 알린다.</summary>
        private void WarnMissingAbilitySystemOnce()
        {
            if (_hasWarnedMissingAbilitySystem)
            {
                return;
            }

            _hasWarnedMissingAbilitySystem = true;
            Debug.LogWarning(
                "[InteractionSceneTransitionAction] 상호작용 주체에서 GameplayAbilitySystemComponent를 찾을 수 없어 " +
                "씬을 전환하지 못한다.",
                this);
        }
    }
}
