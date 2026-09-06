using UnityEngine;

namespace HS.Framework.Foundation.Patterns
{
    /// <summary>
    /// 씬에 배치되는 단일 인스턴스 MonoBehaviour의 기반 클래스이다.
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        private static readonly Singleton<T> Singleton = new();

        /// <summary>
        /// 현재 씬에 배치된 인스턴스를 가져온다.
        /// </summary>
        public static T Instance
        {
            get
            {
                ClearDestroyedInstance();
                if (Singleton.HasInstance)
                {
                    return Singleton.Instance;
                }

                var instance = FindFirstObjectByType<T>();
                if (instance != null)
                {
                    Singleton.TrySetInstance(instance);
                }

                return Singleton.Instance;
            }
        }

        protected virtual void Awake()
        {
            ClearDestroyedInstance();
            if (Singleton.TrySetInstance((T)this))
            {
                DontDestroyOnLoad(gameObject);
                return;
            }

            if (!ReferenceEquals(Singleton.Instance, this))
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 이미 파괴된 인스턴스가 정적 컨테이너에 남아 있으면 비운다.
        /// 도메인 리로드를 끄고 플레이 모드를 다시 시작하면 <see cref="OnDestroy"/>가 호출되지 않은 채
        /// 이전 세션의 파괴된 인스턴스가 남을 수 있고, 그대로 두면 새 세션의 정상 인스턴스가 중복으로
        /// 판정되어 스스로 파괴된다.
        /// 제네릭 형식에는 <c>RuntimeInitializeOnLoadMethod</c>를 적용할 수 없으므로,
        /// 다른 정적 상태 보유 클래스가 플레이 모드 진입 시점에 수행하는 리셋을 여기서는 접근 시점에 수행해
        /// 같은 규약을 만족시킨다.
        /// </summary>
        private static void ClearDestroyedInstance()
        {
            var cached = Singleton.Instance;

            // Singleton<T>는 참조 비교만 하므로, Unity가 파괴한 객체는 여기서 직접 판정해 제거한다.
            if (cached is not null && (UnityEngine.Object)cached == null)
            {
                Singleton.TryClearInstance(cached);
            }
        }

        protected virtual void OnDestroy()
        {
            Singleton.TryClearInstance((T)this);
        }
    }
}
