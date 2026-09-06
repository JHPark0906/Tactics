using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Scene;
using UnityEngine;

namespace HS.Framework.Interaction.Abilities
{
    /// <summary>
    /// 상호작용으로 씬을 전환하는 요청 하나가 실어 나르는 것이다.
    /// </summary>
    /// <remarks>
    /// 씬 전환 서비스를 함께 싣는 것은, 이 값을 받는 <see cref="InteractionSceneTransitionAbilityDefinition"/>이
    /// 어빌리티(평범한 C# 클래스)라서 <c>[Inject]</c>로 서비스를 직접 받을 수 없기 때문이다. 요청을 보내는
    /// <c>InteractionSceneTransitionAction</c>은 MonoBehaviour라 주입을 받을 수 있으므로, 받아 둔 것을
    /// 그대로 실어 넘긴다.
    /// </remarks>
    public readonly struct InteractionSceneTransitionPayload
    {
        /// <summary>이 요청을 처리할 씬 전환 서비스이다.</summary>
        public ISceneTransitionService SceneTransitionService { get; }

        /// <summary>도착할 씬이다.</summary>
        public SceneReference DestinationScene { get; }

        /// <summary>경유할 로딩 씬이며, 곧바로 전환할 것이면 지정하지 않은 참조이다.</summary>
        public SceneReference LoadingScene { get; }

        /// <summary>씬 전환 요청을 생성한다.</summary>
        /// <param name="sceneTransitionService">이 요청을 처리할 씬 전환 서비스이다.</param>
        /// <param name="destinationScene">도착할 씬이다.</param>
        /// <param name="loadingScene">경유할 로딩 씬이며, 곧바로 전환할 것이면 null이다.</param>
        public InteractionSceneTransitionPayload(
            ISceneTransitionService sceneTransitionService,
            SceneReference destinationScene,
            SceneReference loadingScene)
        {
            SceneTransitionService = sceneTransitionService;
            DestinationScene = destinationScene;
            LoadingScene = loadingScene;
        }
    }

    /// <summary>
    /// 지정한 씬으로 이동하는, 재사용 가능한 상호작용 어빌리티의 정의이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 정의 하나를 여러 상호작용 대상이 공유한다.</b> 씬마다 도착지가 다른데도 정의를 하나만 두는 것은,
    /// 상호작용 대상마다 별도 정의를 만들어 인터랙터에게 부여하면 <see cref="GameplayAbilitySystem"/>이
    /// 태그로 추적하는 부여 목록이 마주친 상호작용 대상 수만큼 계속 자라기 때문이다. 그 대신 도착지는
    /// <see cref="InteractionSceneTransitionPayload"/>에 실어 매번 다르게 보낸다 — 사망 이벤트가 가해자를
    /// 싣는 것과 같은 자리이다.
    /// </para>
    /// <para>
    /// <b>이 정의를 트리거로 삼은 어빌리티만 반응한다.</b> <c>InteractionSceneTransitionAction</c>은
    /// <see cref="TriggerEventTag"/>로 게임플레이 이벤트를 보낼 뿐 이 어빌리티를 직접 알지 못한다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS/Interaction/Scene Transition Ability", fileName = "InteractionSceneTransitionAbility")]
    public sealed class InteractionSceneTransitionAbilityDefinition : GameplayAbilityDefinition
    {
        /// <summary>이 어빌리티 자신을 식별하는 태그 이름이다. 부여 여부를 확인할 때 쓴다.</summary>
        public const string AbilityTagName = "Ability.Interaction.SceneTransition";

        /// <summary>이 어빌리티를 트리거로 여는 이벤트 태그 이름이다.</summary>
        public const string TriggerEventTagName = "Event.Interaction.SceneTransition";

        private static readonly GameplayTag SelfTag = GameplayTag.Parse(AbilityTagName);
        private static readonly GameplayTag TriggerTag = GameplayTag.Parse(TriggerEventTagName);

        /// <summary>이 어빌리티 자신을 식별하는 태그이다. 아직 부여되지 않았으면 부여할 때 이 태그로 확인한다.</summary>
        public static GameplayTag SceneTransitionAbilityTag => SelfTag;

        /// <summary>이 어빌리티를 트리거로 여는 이벤트 태그이다.</summary>
        public static GameplayTag TriggerEventTag => TriggerTag;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility() => new InteractionSceneTransitionAbility();

        /// <summary>테스트와 런타임 조립에 사용할 비저장 정의를 만든다.</summary>
        /// <returns>만든 정의이다.</returns>
        public static InteractionSceneTransitionAbilityDefinition CreateRuntime()
        {
            var definition = CreateInstance<InteractionSceneTransitionAbilityDefinition>();
            definition.ConfigureRuntime(
                AbilityTagName,
                triggerEventTags: new[] { TriggerEventTagName });
            return definition;
        }

        /// <summary>
        /// 상호작용 주체에 이 어빌리티가 아직 없으면 부여한다.
        /// </summary>
        /// <remarks>
        /// 이벤트로 트리거되는 어빌리티는 미리 부여되어 있어야 반응한다 — 이벤트를 보내는 것만으로는
        /// 아무도 반응하지 않는다. 이 정의는 인터랙터마다 하나만 있으면 되므로(도착지는 매번 페이로드로
        /// 실어 보낸다) <see cref="HS.Framework.Interaction.Actions.InteractionSceneTransitionAction"/>이
        /// 실행될 때마다 이 메서드를 불러 확인하고, 처음이면 만들어 부여해 둔다.
        /// </remarks>
        /// <param name="abilitySystem">부여할 어빌리티 시스템 컴포넌트이다.</param>
        public static void EnsureGranted(GameplayAbilitySystemComponent abilitySystem)
        {
            if (abilitySystem.System.IsGranted(SceneTransitionAbilityTag))
            {
                return;
            }

            abilitySystem.System.GrantAbility(CreateRuntime());
        }

        /// <summary>
        /// 씬을 불러오는 동안 활성 상태로 남아 있다가, 불러오기가 끝나면(또는 실패하면) 스스로 끝나는 어빌리티이다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="GameplayAbility.IsActive"/>인 동안 활성화 검사가 후속 요청을 거부하므로,
        /// 씬을 불러오는 중에는 같은 어빌리티가 중복 실행되지 않는다.
        /// </para>
        /// <para>
        /// <b>취소는 곧 정리다.</b> 소유 액터가 파괴되거나 어빌리티 시스템이 통째로 정리되면
        /// <see cref="OnEnd"/>가 <see cref="GameplayAbilityEndReason.Cancelled"/>로 불리는데,
        /// 그 자리에서 아직 진행 중인 불러오기를 취소한다. 씬 전환 서비스 쪽이 취소를 받아들이므로
        /// 어중간하게 두 씬이 겹쳐 뜨는 일이 없다.
        /// </para>
        /// </remarks>
        private sealed class InteractionSceneTransitionAbility : GameplayAbility
        {
            private CancellationTokenSource _cancellationSource;
            private bool _isLoading;

            /// <inheritdoc />
            protected override void OnActivate()
            {
                if (TriggeringEvent.Payload is not InteractionSceneTransitionPayload payload ||
                    payload.SceneTransitionService == null ||
                    payload.DestinationScene == null ||
                    !payload.DestinationScene.IsAssigned)
                {
                    return;
                }

                _isLoading = true;
                _cancellationSource = new CancellationTokenSource();
                LoadAsync(payload, _cancellationSource.Token).Forget();
            }

            /// <inheritdoc />
            protected override GameplayAbilityTickResult OnTick(float deltaTime)
            {
                return _isLoading ? GameplayAbilityTickResult.Running : GameplayAbilityTickResult.Finished;
            }

            /// <inheritdoc />
            protected override void OnEnd(GameplayAbilityEndReason endReason)
            {
                _cancellationSource?.Cancel();
                _cancellationSource?.Dispose();
                _cancellationSource = null;
            }

            private async UniTask LoadAsync(InteractionSceneTransitionPayload payload, CancellationToken cancellationToken)
            {
                try
                {
                    if (payload.LoadingScene != null && payload.LoadingScene.IsAssigned)
                    {
                        await payload.SceneTransitionService
                            .LoadSceneAfterLoadingSceneAsync(payload.LoadingScene, payload.DestinationScene)
                            .AttachExternalCancellation(cancellationToken);
                    }
                    else
                    {
                        await payload.SceneTransitionService
                            .LoadSceneAsync(payload.DestinationScene)
                            .AttachExternalCancellation(cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    _isLoading = false;
                }
            }
        }
    }
}
