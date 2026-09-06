using System;
using System.Collections.Generic;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>노드들이 값을 주고받는 곳이다.</summary>
    /// <remarks>
    /// 지켜보는 것들은 키마다 넣은 순서로 담아 두고 그 순서로 부른다. 해시 기반으로 순회하면
    /// 부르는 순서가 실행마다 달라지고, 그러면 어느 자리가 먼저 끊기는지가 달라진다.
    /// </remarks>
    public sealed class BehaviourContext : IBehaviourContext
    {
        private readonly Dictionary<string, object> _values = new();
        private readonly Dictionary<string, List<Subscription>> _observers = new();

        /// <inheritdoc />
        public bool TryGetValue<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key)
                && _values.TryGetValue(key, out var storedValue)
                && storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <inheritdoc />
        public void SetValue<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", nameof(key));
            }

            if (_values.TryGetValue(key, out var previous) && Equals(previous, value))
            {
                return;
            }

            _values[key] = value;
            Notify(key);
        }

        /// <inheritdoc />
        public bool RemoveValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_values.Remove(key))
            {
                return false;
            }

            Notify(key);
            return true;
        }

        /// <inheritdoc />
        public IDisposable Observe(string key, Action<string> onChanged)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", nameof(key));
            }

            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            if (!_observers.TryGetValue(key, out var list))
            {
                list = new List<Subscription>();
                _observers[key] = list;
            }

            Compact(list);
            var subscription = new Subscription(onChanged);
            list.Add(subscription);
            return subscription;
        }

        /// <summary>그 키를 지켜보는 것들을 걸어 둔 순서로 부른다.</summary>
        /// <remarks>
        /// 부르는 동안 다른 것이 끊길 수 있으므로 자리를 세면서 돈다. 끊긴 것은 건너뛰고,
        /// 목록에서 실제로 빼는 일은 다음에 걸 때 한다.
        /// </remarks>
        /// <param name="key">바뀐 키이다.</param>
        private void Notify(string key)
        {
            if (!_observers.TryGetValue(key, out var list))
            {
                return;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var subscription = list[index];
                if (!subscription.IsDisposed)
                {
                    subscription.Invoke(key);
                }
            }
        }

        /// <summary>끊긴 것을 목록에서 실제로 뺀다.</summary>
        /// <param name="list">정리할 목록이다.</param>
        private static void Compact(List<Subscription> list)
        {
            for (var index = list.Count - 1; index >= 0; index--)
            {
                if (list[index].IsDisposed)
                {
                    list.RemoveAt(index);
                }
            }
        }

        /// <summary>걸어 둔 것 하나이며, 버리면 알림이 끊긴다.</summary>
        private sealed class Subscription : IDisposable
        {
            private Action<string> _onChanged;

            public bool IsDisposed => _onChanged == null;

            public Subscription(Action<string> onChanged) => _onChanged = onChanged;

            public void Invoke(string key) => _onChanged?.Invoke(key);

            public void Dispose() => _onChanged = null;
        }
    }
}
