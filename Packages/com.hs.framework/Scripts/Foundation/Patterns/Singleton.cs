using System;

namespace HS.Framework.Foundation.Patterns
{
    /// <summary>
    /// 단일 인스턴스의 생성과 수명을 관리하는 공통 컨테이너이다.
    /// </summary>
    public sealed class Singleton<T> where T : class
    {
        private readonly object _syncRoot = new();
        private T _instance;

        /// <summary>
        /// 현재 인스턴스가 등록되어 있는지 여부이다.
        /// </summary>
        public bool HasInstance
        {
            get
            {
                lock (_syncRoot)
                {
                    return _instance != null;
                }
            }
        }

        /// <summary>
        /// 등록된 인스턴스를 반환한다.
        /// </summary>
        public T Instance
        {
            get
            {
                lock (_syncRoot)
                {
                    return _instance;
                }
            }
        }

        /// <summary>
        /// 인스턴스가 없을 때만 생성 함수를 사용해 등록하고 반환한다.
        /// </summary>
        public T GetOrCreate(Func<T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (_syncRoot)
            {
                _instance ??= factory() ?? throw new InvalidOperationException("싱글턴 인스턴스를 생성할 수 없습니다.");
                return _instance;
            }
        }

        /// <summary>
        /// 인스턴스가 비어 있거나 같은 인스턴스인 경우 등록한다.
        /// </summary>
        public bool TrySetInstance(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            lock (_syncRoot)
            {
                if (_instance != null && !ReferenceEquals(_instance, instance))
                {
                    return false;
                }

                _instance = instance;
                return true;
            }
        }

        /// <summary>
        /// 등록된 인스턴스가 지정한 인스턴스와 같을 때 제거한다.
        /// </summary>
        public bool TryClearInstance(T instance)
        {
            lock (_syncRoot)
            {
                if (!ReferenceEquals(_instance, instance))
                {
                    return false;
                }

                _instance = null;
                return true;
            }
        }
    }
}
