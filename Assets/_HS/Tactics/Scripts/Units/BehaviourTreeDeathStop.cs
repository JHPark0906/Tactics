using System;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Gameplay.Health;
using R3;
using UnityEngine;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 자기 오브젝트가 죽은 상태 태그를 얻으면 같은 오브젝트의 행동 트리 실행기를 끄는 컴포넌트이다.
    /// </summary>
    /// <remarks>
    /// <b>왜 <see cref="BehaviourTreeRunner"/> 안이 아니라 여기인가.</b> 행동 트리 실행기는 프레임워크
    /// 컴포넌트이며 다른 게임에도 그대로 재사용된다 — "죽은 상태"라는 이름의 게임플레이 태그를 알아서는 안 된다.
    /// 시체가 계속 싸우지 않도록 트리를 끄는 것은 Tactics만의 규칙이므로, 이 작은 컴포넌트가 대신 구독하고 끈다.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BehaviourTreeRunner))]
    public sealed class BehaviourTreeDeathStop : MonoBehaviour
    {
        /// <summary>죽은 상태를 나타내는 태그이다.</summary>
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private BehaviourTreeRunner _runner;
        private IDisposable _deathSubscription;

        /// <summary>
        /// 자기 오브젝트의 어빌리티 시스템에서 죽은 상태 태그의 변화를 구독한다. 죽으면 다음 고정 스텝을
        /// 기다리지 않고 곧바로 행동 트리 실행기를 끈다.
        /// </summary>
        private void OnEnable()
        {
            if (_runner == null)
            {
                _runner = GetComponent<BehaviourTreeRunner>();
            }

            if (_deathSubscription == null && TryGetComponent<GameplayAbilitySystemComponent>(out var abilitySystem))
            {
                _deathSubscription = abilitySystem.System.Tags.Changed.Subscribe(OnTagChanged);
            }
        }

        private void OnDisable()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
        }

        /// <summary>죽은 상태 태그를 얻는 순간 행동 트리 실행기를 끈다.</summary>
        /// <param name="change">어빌리티 시스템 태그 컨테이너의 변화이다.</param>
        private void OnTagChanged(GameplayTagChange change)
        {
            if (change.ChangeKind == GameplayTagChangeKind.Gained && change.Tag.Matches(DeadStateTag))
            {
                _runner.enabled = false;
            }
        }
    }
}
