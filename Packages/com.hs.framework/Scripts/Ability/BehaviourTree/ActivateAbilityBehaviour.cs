using HS.Framework.AI.Behaviour;
using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;

namespace HS.Framework.Ability.BehaviourTree
{
    /// <summary>
    /// 태그로 지정한 어빌리티를 활성화하고 그 진행 상태를 행동 트리 결과로 옮기는 노드이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>행동 트리는 무엇을 할지 정하고 어빌리티가 수행한다.</b> 이 노드 하나가 그 둘을 잇는다.
    /// 행위마다 전용 노드를 만드는 대신 어빌리티를 만들고 이 노드에 태그만 지정하면 되므로,
    /// 새 행위를 더할 때 트리 쪽 코드를 손대지 않아도 된다.
    /// </para>
    /// <para>
    /// <b>활성화할 수 없으면 실패를 답한다.</b> 부여되지 않은 어빌리티도 여기 포함된다.
    /// 덕분에 그 어빌리티를 갖지 않은 액터는 이 분기를 그냥 건너뛰고 다음 우선순위로 넘어간다.
    /// 선택자가 기다리게 만드는 진행 중이나 예외를 돌려주지 않는 것이 핵심이다.
    /// </para>
    /// <para>
    /// <b>매 틱 상태를 다시 확인한다.</b> 진행 중을 돌려주는 노드는 그만둘 조건을 매 틱 평가하는 자리에 두어야 한다.
    /// 그래서 이 노드는 어빌리티가 스스로 끝나기만 기다리지 않고, 틱마다 그 어빌리티가 아직 부여되어 있는지,
    /// 자기가 시작한 그 활성화가 아직 이어지고 있는지를 다시 본다. 바깥에서 취소되거나 부여가 해제되면
    /// 곧바로 실패로 빠져나오므로 액터가 굳지 않는다.
    /// </para>
    /// <para>
    /// <b>가로채였으면 시작한 것을 되돌린다.</b> <see cref="Reset"/>이 불리면 자기가 시작한 활성 어빌리티를 취소한다.
    /// 상위 분기가 우선순위를 가져갔는데 어빌리티가 계속 돌면 두 행동이 겹치기 때문이다.
    /// 취소 경로에서도 어빌리티의 종료 처리가 한 번 불리므로 잡고 있던 자원은 그때 풀린다.
    /// </para>
    /// </remarks>
    public sealed class ActivateAbilityBehaviour : IBehaviour
    {
        /// <summary>어빌리티를 보유한 시스템이다.</summary>
        private readonly GameplayAbilitySystem _abilitySystem;

        /// <summary>활성화할 어빌리티의 식별 태그이다.</summary>
        private readonly GameplayTag _abilityTag;

        /// <summary>이 노드가 시작해 지켜보고 있는 어빌리티이며, 지켜보는 중이 아니면 null이다.</summary>
        private GameplayAbility _startedAbility;

        /// <summary>시작한 시점의 활성화 횟수이며, 남의 활성화를 자기 것으로 착각하지 않기 위한 표식이다.</summary>
        private int _startedActivationCount;

        /// <summary>
        /// 어빌리티 활성화 노드를 생성한다.
        /// </summary>
        /// <param name="abilitySystem">어빌리티를 보유한 시스템이다.</param>
        /// <param name="abilityTag">활성화할 어빌리티의 식별 태그이다.</param>
        /// <exception cref="ArgumentNullException">시스템이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">어빌리티 태그가 유효하지 않으면 발생한다.</exception>
        public ActivateAbilityBehaviour(GameplayAbilitySystem abilitySystem, GameplayTag abilityTag)
        {
            _abilitySystem = abilitySystem ?? throw new ArgumentNullException(nameof(abilitySystem));
            if (!abilityTag.IsValid)
            {
                throw new ArgumentException("어빌리티 태그는 유효한 값이어야 합니다.", nameof(abilityTag));
            }

            _abilityTag = abilityTag;
        }

        /// <summary>
        /// 어빌리티 활성화 노드를 태그 이름으로 생성한다.
        /// </summary>
        /// <param name="abilitySystem">어빌리티를 보유한 시스템이다.</param>
        /// <param name="abilityTagName">활성화할 어빌리티의 식별 태그 이름이다.</param>
        /// <exception cref="ArgumentException">태그 이름의 형식이 잘못되었으면 발생한다.</exception>
        public ActivateAbilityBehaviour(GameplayAbilitySystem abilitySystem, string abilityTagName)
            : this(abilitySystem, GameplayTag.Parse(abilityTagName))
        {
        }

        /// <summary>이 노드가 활성화하는 어빌리티의 식별 태그이다.</summary>
        public GameplayTag AbilityTag => _abilityTag;

        /// <summary>이 노드가 시작한 활성화를 지금 지켜보고 있는지 여부이다.</summary>
        public bool IsWatchingActivation => _startedAbility != null;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (_startedAbility != null)
            {
                return ResolveWatchedActivation();
            }

            if (_abilitySystem.TryActivate(_abilityTag) != GameplayAbilityActivationResult.Success)
            {
                // 부여되지 않음, 쿨다운, 자원 부족 어느 쪽이든 지금 이 분기는 갈 수 없다는 뜻이므로 다음 우선순위로 넘긴다.
                return BehaviourStatus.Failure;
            }

            if (!_abilitySystem.TryGetAbility(_abilityTag, out var ability))
            {
                return BehaviourStatus.Failure;
            }

            _startedAbility = ability;
            _startedActivationCount = ability.ActivationCount;

            // 활성화한 자리에서 이미 끝나는 어빌리티가 흔하므로 곧바로 판정한다.
            return ResolveWatchedActivation();
        }

        /// <inheritdoc />
        public void Reset()
        {
            if (IsWatchedActivationRunning())
            {
                _abilitySystem.CancelAbility(_abilityTag);
            }

            _startedAbility = null;
        }

        /// <summary>
        /// 지켜보던 활성화가 어떻게 되었는지 판정하고 그 결과를 행동 트리 상태로 옮긴다.
        /// </summary>
        /// <returns>이번 틱의 노드 상태이다.</returns>
        private BehaviourStatus ResolveWatchedActivation()
        {
            var watchedAbility = _startedAbility;
            if (!_abilitySystem.TryGetAbility(_abilityTag, out var currentAbility) ||
                !ReferenceEquals(currentAbility, watchedAbility))
            {
                // 지켜보던 사이에 부여가 해제되었다. 기다려도 끝나지 않으므로 실패로 빠져나온다.
                _startedAbility = null;
                return BehaviourStatus.Failure;
            }

            if (watchedAbility.ActivationCount != _startedActivationCount)
            {
                // 내가 시작한 활성화가 이미 끝나고 다른 활성화가 시작되었다. 남의 활성화를 기다리지 않는다.
                _startedAbility = null;
                return BehaviourStatus.Failure;
            }

            if (watchedAbility.IsActive)
            {
                return BehaviourStatus.Running;
            }

            _startedAbility = null;
            return watchedAbility.LastEndReason == GameplayAbilityEndReason.Completed
                ? BehaviourStatus.Success
                : BehaviourStatus.Failure;
        }

        /// <summary>이 노드가 시작한 활성화가 아직 이어지고 있는지 확인한다.</summary>
        /// <returns>이어지고 있으면 true이다.</returns>
        private bool IsWatchedActivationRunning()
        {
            return _startedAbility != null &&
                   _startedAbility.IsActive &&
                   _startedAbility.ActivationCount == _startedActivationCount &&
                   _abilitySystem.TryGetAbility(_abilityTag, out var currentAbility) &&
                   ReferenceEquals(currentAbility, _startedAbility);
        }
    }
}
