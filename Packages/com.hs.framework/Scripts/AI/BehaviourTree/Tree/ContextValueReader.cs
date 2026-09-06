using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥에 담긴 값을 자리들이 바라는 모양으로 읽는다.</summary>
    /// <remarks>
    /// 문맥은 값을 object로 담으므로 정수를 담아 두고 실수로 읽으면 없다고 답한다. 수와 위치는
    /// 여러 자리가 같은 방법으로 읽어야 하므로 여기 모아 둔다.
    /// </remarks>
    public static class ContextValueReader
    {
        /// <summary>키에 담긴 값을 수로 읽는다.</summary>
        /// <param name="context">값을 주고받는 곳이다.</param>
        /// <param name="key">읽을 키이다.</param>
        /// <param name="number">읽은 수이며, 없거나 수가 아니면 0이다.</param>
        /// <returns>수로 읽었으면 참이다.</returns>
        public static bool TryGetNumber(IBehaviourContext context, string key, out double number)
        {
            if (context != null && context.TryGetValue<object>(key, out var value))
            {
                switch (value)
                {
                    case int integer:
                        number = integer;
                        return true;
                    case float single:
                        number = single;
                        return true;
                    case double precise:
                        number = precise;
                        return true;
                    case long wide:
                        number = wide;
                        return true;
                }
            }

            number = 0d;
            return false;
        }

        /// <summary>키에 담긴 값을 세계 좌표로 읽는다.</summary>
        /// <remarks>좌표 그 자체이거나, 그 자리에 있는 Transform·GameObject·구성요소이면 읽는다.</remarks>
        /// <param name="context">값을 주고받는 곳이다.</param>
        /// <param name="key">읽을 키이다.</param>
        /// <param name="position">읽은 좌표이며, 없으면 영이다.</param>
        /// <returns>좌표로 읽었으면 참이다.</returns>
        public static bool TryGetPosition(IBehaviourContext context, string key, out Vector3 position)
        {
            if (context != null && context.TryGetValue<object>(key, out var value))
            {
                switch (value)
                {
                    case Vector3 point:
                        position = point;
                        return true;
                    case Transform transform when transform != null:
                        position = transform.position;
                        return true;
                    case GameObject gameObject when gameObject != null:
                        position = gameObject.transform.position;
                        return true;
                    case Component component when component != null:
                        position = component.transform.position;
                        return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        /// <summary>키에 담긴 값을 방향으로 읽는다.</summary>
        /// <remarks>방향 그 자체이거나, Transform·GameObject·구성요소이면 그것이 바라보는 쪽이다.</remarks>
        /// <param name="context">값을 주고받는 곳이다.</param>
        /// <param name="key">읽을 키이다.</param>
        /// <param name="direction">읽은 방향이며, 없으면 영이다.</param>
        /// <returns>방향으로 읽었으면 참이다.</returns>
        public static bool TryGetDirection(IBehaviourContext context, string key, out Vector3 direction)
        {
            if (context != null && context.TryGetValue<object>(key, out var value))
            {
                switch (value)
                {
                    case Vector3 vector:
                        direction = vector;
                        return true;
                    case Transform transform when transform != null:
                        direction = transform.forward;
                        return true;
                    case GameObject gameObject when gameObject != null:
                        direction = gameObject.transform.forward;
                        return true;
                    case Component component when component != null:
                        direction = component.transform.forward;
                        return true;
                }
            }

            direction = Vector3.zero;
            return false;
        }
    }
}
