using System;
using System.Collections.Generic;
using MessagePipe;

namespace HS.Framework.Tests.Support
{
    /// <summary>발행된 메시지를 목록에 쌓기만 하는 검사용 발행자이다.</summary>
    /// <typeparam name="TMessage">메시지 형식이다.</typeparam>
    public sealed class TestPublisher<TMessage> : IPublisher<TMessage>
    {
        /// <summary>발행된 순서대로 쌓인 메시지이다.</summary>
        public List<TMessage> Published { get; } = new List<TMessage>();

        /// <summary>마지막으로 발행된 메시지이며, 아직 없으면 기본값이다.</summary>
        public TMessage Last => Published.Count > 0 ? Published[Published.Count - 1] : default;

        /// <inheritdoc />
        public void Publish(TMessage message) => Published.Add(message);
    }

    /// <summary>구독자를 직접 들고 있다가 검사가 부르는 순간 메시지를 돌리는 검사용 구독 창구이다.</summary>
    /// <typeparam name="TMessage">메시지 형식이다.</typeparam>
    public sealed class TestSubscriber<TMessage> : ISubscriber<TMessage>
    {
        private readonly List<IMessageHandler<TMessage>> _handlers = new List<IMessageHandler<TMessage>>();

        /// <inheritdoc />
        public IDisposable Subscribe(IMessageHandler<TMessage> handler, params MessageHandlerFilter<TMessage>[] filters)
        {
            _handlers.Add(handler);
            return new Subscription(() => _handlers.Remove(handler));
        }

        /// <summary>등록된 구독자 전부에게 메시지를 돌린다. 돌리는 도중 구독이 풀려도 남은 구독자에게는 그대로 간다.</summary>
        /// <param name="message">돌릴 메시지이다.</param>
        public void Publish(TMessage message)
        {
            foreach (var handler in _handlers.ToArray())
            {
                handler.Handle(message);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }

    /// <summary>
    /// 발행과 구독을 한 물건에 합친 검사용 창구이다. 발행하면 목록에 쌓이고 구독자에게도 곧바로 돌아간다.
    /// 발행자와 구독자를 따로 주입받는 대상을 한 창구로 잇고 싶을 때 쓴다.
    /// </summary>
    /// <typeparam name="TMessage">메시지 형식이다.</typeparam>
    public sealed class TestMessageChannel<TMessage> : IPublisher<TMessage>, ISubscriber<TMessage>
    {
        private readonly TestPublisher<TMessage> _publisher = new TestPublisher<TMessage>();
        private readonly TestSubscriber<TMessage> _subscriber = new TestSubscriber<TMessage>();

        /// <summary>발행된 순서대로 쌓인 메시지이다.</summary>
        public List<TMessage> Published => _publisher.Published;

        /// <inheritdoc />
        public void Publish(TMessage message)
        {
            _publisher.Publish(message);
            _subscriber.Publish(message);
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IMessageHandler<TMessage> handler, params MessageHandlerFilter<TMessage>[] filters)
        {
            return _subscriber.Subscribe(handler, filters);
        }
    }
}
