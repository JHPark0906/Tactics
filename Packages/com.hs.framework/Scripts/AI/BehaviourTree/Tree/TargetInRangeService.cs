using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>가지가 도는 동안 대상이 정해진 거리 안인지 재서 문맥에 표식을 두는 서비스이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>왜 필요한가.</b> 선택 자리는 진행 중인 자식을 기억하며, 그 자식이 끝날 때까지 앞의 자식을
    /// 다시 보지 않는다. 그래서 "거리 안이면 멈춘다"를 위에 두고 "쫓아간다"를 아래에 두어도,
    /// 추격이 시작된 뒤에는 대상이 거리 안으로 들어와도 멈추지 못하고 그대로 달라붙는다.
    /// 중간에 끊으려면 지켜볼 값이 문맥에 있어야 하고, 그 값을 두는 것이 이 서비스이다.
    /// </para>
    /// <para>
    /// <b>값이 아니라 있고 없음으로 말한다.</b> 조건 자리는 키에 값이 있는지만 본다.
    /// 그래서 거리 밖이면 <c>false</c>를 담는 것이 아니라 <b>키를 지운다.</b>
    /// <c>false</c>를 담으면 "값이 있다"가 되어 조건이 참으로 읽는다.
    /// </para>
    /// <para>
    /// <b>같은 상태면 다시 쓰지 않아도 된다.</b> 문맥은 담긴 값과 같으면 알리지 않으므로,
    /// 거리 안에 계속 있는 동안 매 간격마다 <c>true</c>를 다시 담아도 지켜보던 자리가 깨어나지 않는다.
    /// 지우는 것도 이미 없으면 알리지 않는다. 그래서 알림은 경계를 넘는 순간에만 나간다.
    /// </para>
    /// </remarks>
    public sealed class TargetInRangeService : ServiceBehaviour
    {
        private readonly Transform _self;
        private readonly float _range;
        private readonly string _targetKey;
        private readonly string _inRangeKey;

        /// <summary>거리를 잴 기준과 거리, 읽고 쓸 자리를 지정한다.</summary>
        /// <param name="self">거리를 잴 기준이 되는 유닛의 Transform이다.</param>
        /// <param name="range">이 거리 안이면 안으로 본다(미터).</param>
        /// <param name="interval">다시 재는 간격(초)이다.</param>
        /// <param name="targetKey">대상 Transform이 담긴 문맥 키이다.</param>
        /// <param name="inRangeKey">거리 안일 때 표식을 둘 문맥 키이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이다.</param>
        /// <exception cref="ArgumentNullException">기준 Transform이 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public TargetInRangeService(
            Transform self,
            float range,
            float interval,
            string targetKey,
            string inRangeKey,
            Func<float> timeProvider = null)
            : base(interval, timeProvider)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _targetKey = LeafTaskKeys.Require(targetKey, nameof(targetKey));
            _inRangeKey = LeafTaskKeys.Require(inRangeKey, nameof(inRangeKey));
            _range = Mathf.Max(0f, range);
        }

        /// <inheritdoc />
        /// <remarks>대상이 없어도 표식을 지운다. 남겨 두면 없는 대상에 대해 거리 안이라고 믿는다.</remarks>
        protected override void Execute(in BehaviourTickContext context)
        {
            var values = context.Context;
            if (!values.TryGetValue<Transform>(_targetKey, out var target)
                || target == null
                || Vector3.Distance(_self.position, target.position) > _range)
            {
                values.RemoveValue(_inRangeKey);
                return;
            }

            values.SetValue(_inRangeKey, true);
        }
    }

    /// <summary>가지가 도는 동안 대상이 거리 안인지 재서 표식을 두는 서비스의 설명이다.</summary>
    /// <remarks>
    /// 거리를 에셋에 적는다. 거리가 유닛마다 다른 게임은 이 설명 대신 유닛에서 거리를 읽어
    /// <see cref="TargetInRangeService"/>를 만드는 자기 설명을 둔다. 유닛 자신의 자리가 필요하므로
    /// 유닛이 없으면 null을 돌린다.
    /// </remarks>
    [Serializable]
    public sealed class TargetInRangeServiceDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("이 거리 안이면 안으로 본다(미터).")]
        private float range = 10f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("다시 재는 간격(초)이다. 0이면 실행할 때마다 잰다.")]
        private float interval = 0.1f;

        [SerializeField]
        [Tooltip("대상 Transform이 담긴 문맥 키이다.")]
        private string targetKey;

        [SerializeField]
        [Tooltip("거리 안일 때 표식을 둘 문맥 키이다.")]
        private string inRangeKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(inRangeKey) ? "Service: Target In Range" : $"Service: Target In Range → {inRangeKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(inRangeKey) || context.Owner == null
                ? null
                : new TargetInRangeService(context.Owner.transform, range, interval, targetKey, inRangeKey);
    }
}
