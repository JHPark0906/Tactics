using System;
using System.Collections.Generic;
using HS.Framework.Ability.Tags;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 큐가 어떤 순간을 알리는지이다.
    /// </summary>
    public enum GameplayCueEventKind
    {
        /// <summary>효과가 작용하기 시작했거나 억제에서 풀렸다. 어빌리티라면 활성화되었다.</summary>
        Applied = 0,

        /// <summary>즉시 효과가 실행되었거나 지속 효과의 주기가 한 번 실행되었다.</summary>
        Executed = 1,

        /// <summary>효과가 걷히거나 억제되었다. 어빌리티라면 끝났다.</summary>
        Removed = 2
    }

    /// <summary>
    /// 규칙 계층이 연출 계층에 보내는 신호이다. 무엇이 어느 대상에게 어떤 순간에 일어났는지를 태그로 말한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>규칙은 연출을 모른다.</b> 효과와 어빌리티는 자기가 어떤 소리와 그림을 내야 하는지 알지 못하며, 정의에 적힌
    /// 큐 태그로 "이런 일이 일어났다"만 알린다. 무엇을 보여 줄지는 그 태그를 받는 핸들러가 정한다.
    /// 그래서 규칙 계층은 연출 자산 없이 검증되고, 연출은 규칙 코드를 고치지 않고 늘어난다.
    /// </para>
    /// <para>
    /// 대상은 큐가 일어난 액터이며 규칙만 검증하는 자리에서는 null일 수 있다. 사정은 가해자 같은 것을 실어 나른다.
    /// </para>
    /// </remarks>
    public readonly struct GameplayCueEvent
    {
        /// <summary>큐를 구분하는 태그이다.</summary>
        public GameplayTag CueTag { get; }

        /// <summary>어떤 순간인지이다.</summary>
        public GameplayCueEventKind Kind { get; }

        /// <summary>큐가 일어난 액터이며 알 수 없으면 null이다.</summary>
        public GameObject Target { get; }

        /// <summary>큐를 일으킨 효과 기록이며 효과가 아니거나 즉시 효과이면 null이다.</summary>
        public ActiveGameplayEffect Effect { get; }

        /// <summary>큐를 일으킨 순간의 사정이며 없으면 null이다.</summary>
        public GameplayEffectContext Context { get; }

        /// <summary>큐 이벤트를 생성한다.</summary>
        /// <param name="cueTag">큐를 구분하는 태그이다.</param>
        /// <param name="kind">어떤 순간인지이다.</param>
        /// <param name="target">큐가 일어난 액터이며 알 수 없으면 null이다.</param>
        /// <param name="effect">큐를 일으킨 효과 기록이며 없으면 null이다.</param>
        /// <param name="context">큐를 일으킨 순간의 사정이며 없으면 null이다.</param>
        public GameplayCueEvent(
            GameplayTag cueTag,
            GameplayCueEventKind kind,
            GameObject target,
            ActiveGameplayEffect effect = null,
            GameplayEffectContext context = null)
        {
            CueTag = cueTag;
            Kind = kind;
            Target = target;
            Effect = effect;
            Context = context;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{CueTag} {Kind} on {(Target != null ? Target.name : "없음")}";
        }
    }

    /// <summary>
    /// 큐를 받아 연출을 수행하는 것이다. 게임이 구현해 <see cref="GameplayCueDispatcher"/>에 태그로 등록한다.
    /// </summary>
    public interface IGameplayCueHandler
    {
        /// <summary>큐를 처리한다.</summary>
        /// <param name="cue">받은 큐이다.</param>
        void Handle(in GameplayCueEvent cue);
    }

    /// <summary>
    /// 큐 태그별로 핸들러를 등록해 두고, 큐가 오면 태그가 맞는 핸들러를 부른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>계층 일치를 따른다.</b> <c>Cue.Damage</c>에 등록한 핸들러는 <c>Cue.Damage.Fire</c>도 받는다.
    /// 계열 하나를 한 핸들러가 맡고 세부 태그로 갈라 보는 것이 이 형태이다.
    /// </para>
    /// <para>
    /// 한 액터의 규칙 계층(효과 실행기, 어빌리티 시스템)이 같은 디스패처를 보게 하는 것은 조립하는 쪽의 몫이며,
    /// 보통 게임이 디스패처 하나를 서비스로 두고 액터마다 연결한다. 핸들러가 아닌 것이 큐를 지켜보려면
    /// <see cref="Dispatched"/>를 구독한다.
    /// </para>
    /// </remarks>
    public sealed class GameplayCueDispatcher
    {
        /// <summary>등록한 태그별 핸들러 목록이다.</summary>
        private readonly Dictionary<GameplayTag, List<IGameplayCueHandler>> _handlers = new();

        /// <summary>보낸 큐를 알리는 내부 스트림이다.</summary>
        private readonly Subject<GameplayCueEvent> _dispatched = new();

        /// <summary>내부 Subject를 감춘 읽기 전용 스트림이다.</summary>
        private readonly Observable<GameplayCueEvent> _dispatchedObservable;

        /// <summary>큐 디스패처를 생성한다.</summary>
        public GameplayCueDispatcher()
        {
            _dispatchedObservable = _dispatched.AsObservable();
        }

        /// <summary>보낸 모든 큐가 흐르는 스트림이다.</summary>
        public Observable<GameplayCueEvent> Dispatched => _dispatchedObservable;

        /// <summary>등록된 핸들러 수이다.</summary>
        public int HandlerCount
        {
            get
            {
                var count = 0;
                foreach (var handlers in _handlers.Values)
                {
                    count += handlers.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// 태그에 핸들러를 등록한다. 그 태그이거나 그 하위 태그의 큐를 받는다.
        /// </summary>
        /// <param name="cueTag">받을 큐의 태그이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <param name="handler">큐를 처리할 것이다.</param>
        /// <returns>해제하면 이 등록만 빼는 손잡이이며, 등록하지 못했으면 아무 일도 하지 않는 손잡이이다.</returns>
        public IDisposable Register(GameplayTag cueTag, IGameplayCueHandler handler)
        {
            if (!cueTag.IsValid || handler == null)
            {
                return Disposable.Empty;
            }

            if (!_handlers.TryGetValue(cueTag, out var handlers))
            {
                handlers = new List<IGameplayCueHandler>();
                _handlers.Add(cueTag, handlers);
            }

            handlers.Add(handler);
            return new Registration(this, cueTag, handler);
        }

        /// <summary>
        /// 큐를 보낸다. 태그가 맞는 핸들러를 등록한 순서로 부르고 스트림으로도 흘린다.
        /// </summary>
        /// <param name="cue">보낼 큐이다.</param>
        /// <returns>큐를 받은 핸들러 수이다.</returns>
        public int Dispatch(in GameplayCueEvent cue)
        {
            if (!cue.CueTag.IsValid)
            {
                return 0;
            }

            _dispatched.OnNext(cue);
            if (_handlers.Count == 0)
            {
                return 0;
            }

            // 핸들러가 다른 핸들러를 등록하거나 뺄 수 있으므로 순회 대상을 먼저 확정한다.
            var matched = new List<IGameplayCueHandler>();
            foreach (var pair in _handlers)
            {
                if (cue.CueTag.Matches(pair.Key))
                {
                    matched.AddRange(pair.Value);
                }
            }

            foreach (var handler in matched)
            {
                handler.Handle(in cue);
            }

            return matched.Count;
        }

        /// <summary>등록을 뺀다.</summary>
        /// <param name="cueTag">등록한 태그이다.</param>
        /// <param name="handler">뺄 핸들러이다.</param>
        /// <returns>실제로 뺐으면 true이다.</returns>
        private bool Unregister(GameplayTag cueTag, IGameplayCueHandler handler)
        {
            if (!_handlers.TryGetValue(cueTag, out var handlers))
            {
                return false;
            }

            var removed = handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                _handlers.Remove(cueTag);
            }

            return removed;
        }

        /// <summary>등록 하나를 정확히 되돌리는 손잡이이다.</summary>
        private sealed class Registration : IDisposable
        {
            private readonly GameplayTag _cueTag;
            private readonly IGameplayCueHandler _handler;
            private GameplayCueDispatcher _owner;

            public Registration(GameplayCueDispatcher owner, GameplayTag cueTag, IGameplayCueHandler handler)
            {
                _owner = owner;
                _cueTag = cueTag;
                _handler = handler;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                var owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.Unregister(_cueTag, _handler);
            }
        }
    }
}
