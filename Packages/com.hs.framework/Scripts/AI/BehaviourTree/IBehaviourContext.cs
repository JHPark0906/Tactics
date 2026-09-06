using System;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>노드들이 값을 주고받는 곳이며, 값이 바뀌면 알려 준다.</summary>
    /// <remarks>
    /// 값을 읽고 쓰는 것만으로는 매 틱 전부를 다시 훑어야 무엇이 바뀌었는지 알 수 있다.
    /// 바뀔 때 알려 주면 그 값을 보고 있는 자리만 깨어나면 된다.
    /// </remarks>
    public interface IBehaviourContext
    {
        /// <summary>키에 담긴 값을 읽는다.</summary>
        /// <typeparam name="T">읽을 값의 형식이다.</typeparam>
        /// <param name="key">읽을 키이다.</param>
        /// <param name="value">읽은 값이며, 없거나 형식이 다르면 기본값이다.</param>
        /// <returns>읽었으면 참이다.</returns>
        bool TryGetValue<T>(string key, out T value);

        /// <summary>키에 값을 담는다.</summary>
        /// <remarks>
        /// <b>담긴 값과 같으면 알리지 않는다.</b> 같은 값을 다시 써도 바뀐 것이 없으므로, 그것으로
        /// 보고 있던 자리를 깨우면 아무 일도 없었는데 실행 중인 가지가 끊긴다.
        /// </remarks>
        /// <typeparam name="T">담을 값의 형식이다.</typeparam>
        /// <param name="key">담을 키이다.</param>
        /// <param name="value">담을 값이다.</param>
        void SetValue<T>(string key, T value);

        /// <summary>키에 담긴 값을 지운다.</summary>
        /// <param name="key">지울 키이다.</param>
        /// <returns>실제로 지웠으면 참이다.</returns>
        bool RemoveValue(string key);

        /// <summary>그 키의 값이 바뀌면 알려 달라고 걸어 둔다.</summary>
        /// <remarks>
        /// <b>돌려받은 것을 버리면 알림이 끊긴다.</b> 가지에서 벗어난 자리가 계속 알림을 받으면
        /// 실행 중이지도 않은 자리가 트리를 끊게 된다.
        /// 알리는 순서는 걸어 둔 순서이며, 같은 조작이면 언제나 같다.
        /// </remarks>
        /// <param name="key">지켜볼 키이다.</param>
        /// <param name="onChanged">바뀌었을 때 부를 것이며 바뀐 키를 받는다.</param>
        /// <returns>버리면 알림이 끊기는 것이다.</returns>
        IDisposable Observe(string key, Action<string> onChanged);
    }
}
