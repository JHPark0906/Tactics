using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자기 가지가 실행 중인 동안 정해진 간격으로 되풀이해 일하는 자리이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>가지가 도는 동안에만 일한다.</b> 컴포넌트로 붙여 두면 유닛이 무엇을 하든 계속 도는데,
    /// 그러면 엄폐 중에도 전진 중에도 같은 일을 한다. 자리에 두면 그 자리를 지날 때만 돈다.
    /// </para>
    /// <para>
    /// 자식은 그대로 통과시킨다. 결과를 바꾸지 않으므로 트리 모양에 끼워 넣어도 흐름이 달라지지 않는다.
    /// </para>
    /// </remarks>
    public abstract class ServiceBehaviour : DecoratorBehaviour
    {
        private readonly float _interval;
        private readonly Func<float> _timeProvider;
        private float? _nextTime;

        /// <summary>되풀이할 간격을 지정한다.</summary>
        /// <param name="interval">되풀이 간격(초)이며, 0이면 실행할 때마다 한다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이며, 비우면 <see cref="Time.time"/>을 쓴다.</param>
        protected ServiceBehaviour(float interval, Func<float> timeProvider = null)
        {
            _interval = Mathf.Max(0f, interval);
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <inheritdoc />
        protected sealed override void OnBeforeChildTick(in BehaviourTickContext context)
        {
            var now = _timeProvider();
            if (_nextTime.HasValue && now < _nextTime.Value)
            {
                return;
            }

            _nextTime = now + _interval;
            Execute(context);
        }

        /// <inheritdoc />
        protected sealed override BehaviourStatus Decorate(
            BehaviourStatus childStatus, in BehaviourTickContext context) => childStatus;

        /// <summary>간격이 찼을 때 할 일이다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        protected abstract void Execute(in BehaviourTickContext context);

        /// <inheritdoc />
        /// <remarks>다시 가지에 들어오면 기다리지 않고 바로 한 번 한다.</remarks>
        protected override void OnReset() => _nextTime = null;
    }

    /// <summary>주어진 일을 정해진 간격으로 되풀이하는 서비스이다.</summary>
    public sealed class ActionServiceBehaviour : ServiceBehaviour
    {
        private readonly Action<IBehaviourContext> _action;

        /// <summary>되풀이할 일과 간격을 지정한다.</summary>
        /// <param name="interval">되풀이 간격(초)이다.</param>
        /// <param name="action">문맥을 받아 하는 일이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이다.</param>
        public ActionServiceBehaviour(
            float interval, Action<IBehaviourContext> action, Func<float> timeProvider = null)
            : base(interval, timeProvider)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <inheritdoc />
        protected override void Execute(in BehaviourTickContext context) => _action(context.Context);
    }
}
