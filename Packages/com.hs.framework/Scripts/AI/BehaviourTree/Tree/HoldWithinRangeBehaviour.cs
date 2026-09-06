using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>대상이 정해진 거리 안에 들어오면 이동을 멈추고 성공하는 잎이다.</summary>
    /// <remarks>
    /// 추격 자리보다 앞에 놓아 쓴다. 거리 안이면 이 자리가 성공해 추격을 막고, 거리 밖이면 실패해
    /// 뒤의 추격 자리로 넘어간다. 그래서 유닛이 대상에게 달라붙지 않고 거리 언저리에서 멈춘다.
    /// 멈춰 서 있기만 하고 그 밖의 일은 하지 않는다.
    /// </remarks>
    public sealed class HoldWithinRangeBehaviour : IBehaviour
    {
        private readonly Transform _self;
        private readonly ICharacterMover _mover;
        private readonly string _targetKey;
        private readonly float _holdDistance;
        private bool _isHolding;

        /// <summary>거리를 잴 기준과 멈추게 할 이동 수단, 대상 키와 멈출 거리를 지정한다.</summary>
        /// <param name="self">거리를 잴 기준이 되는 유닛의 Transform이다.</param>
        /// <param name="mover">멈추게 할 이동 수단이다.</param>
        /// <param name="targetKey">대상 Transform이 담긴 문맥 키이다.</param>
        /// <param name="holdDistance">이 거리 안에 들어오면 멈추는 기준 거리(미터)이다.</param>
        /// <exception cref="ArgumentNullException">기준 Transform이나 이동 수단이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">대상 키가 비어 있으면 발생한다.</exception>
        public HoldWithinRangeBehaviour(Transform self, ICharacterMover mover, string targetKey, float holdDistance)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            _targetKey = LeafTaskKeys.Require(targetKey, nameof(targetKey));
            _holdDistance = Mathf.Max(0f, holdDistance);
        }

        /// <inheritdoc />
        /// <remarks>대상이 거리 안이면 멈추고 성공하며, 대상이 없거나 거리 밖이면 실패한다.</remarks>
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<Transform>(_targetKey, out var target)
                || target == null
                || _self == null
                || Vector3.Distance(_self.position, target.position) > _holdDistance)
            {
                _isHolding = false;
                return BehaviourStatus.Failure;
            }

            // 거리에 막 들어온 순간에만 멈춘다. 매 실행마다 멈추라고 지시할 필요는 없다.
            if (!_isHolding)
            {
                _isHolding = true;
                _mover.Stop();
            }

            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 멈춤 지시 말고는 바깥에 잡은 것이 없으므로 되돌릴 것은 멈춰 있다는 기억뿐이다.
        /// 가로채였다가 다시 고르면 거리 안에서 멈춤을 한 번 더 지시한다.
        /// </remarks>
        public void Reset() => _isHolding = false;
    }

    /// <summary>대상이 거리 안에 들어오면 멈추고 성공하는 잎의 설명이다.</summary>
    /// <remarks>
    /// 멈출 거리를 에셋에 적는다. 이동 수단은 유닛에서 찾으며, 유닛에 <see cref="ICharacterMover"/>를
    /// 구현한 구성요소가 없으면 null을 돌린다.
    /// </remarks>
    [Serializable]
    public sealed class HoldWithinRangeDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("이 거리 안에 들어오면 멈춘다(미터).")]
        private float holdDistance = 8f;

        [SerializeField]
        [Tooltip("대상 Transform이 담긴 문맥 키이다.")]
        private string targetKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(targetKey) ? "Hold Within Range" : $"Hold Within {holdDistance:0.#}m: {targetKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || context.Owner == null)
            {
                return null;
            }

            return context.Owner.TryGetComponent<ICharacterMover>(out var mover)
                ? new HoldWithinRangeBehaviour(context.Owner.transform, mover, targetKey, holdDistance)
                : null;
        }
    }
}
