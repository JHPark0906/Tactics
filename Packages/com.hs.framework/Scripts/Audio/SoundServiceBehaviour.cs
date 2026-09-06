using HS.Framework.Foundation.Patterns;
using UnityEngine;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 서비스를 씬에 상주시키는 호스트이다.
    /// 재생용 오디오 소스를 자식 오브젝트로 생성하고 매 프레임 서비스 상태를 갱신한다.
    /// </summary>
    public sealed class SoundServiceBehaviour : SingletonBehaviour<SoundServiceBehaviour>
    {
        private const string PooledSourceName = "PooledAudioSource";

        [SerializeField] [Min(0)] private int sfxSourcePrewarmCount = 8;
        [SerializeField] private SoundChannelRoute[] channelRoutes;

        private SoundService _service;

        /// <summary>사운드 재생 서비스 계약이다.</summary>
        public ISoundService Service => EnsureService();

        protected override void Awake()
        {
            base.Awake();
            if (ReferenceEquals(Instance, this))
            {
                EnsureService();
            }
        }

        private void Update()
        {
            _service?.Tick(Time.unscaledDeltaTime);
        }

        private SoundService EnsureService()
        {
            if (_service != null)
            {
                return _service;
            }

            _service = new SoundService(CreateAudioSource, sfxSourcePrewarmCount);
            _service.ConfigureChannelRoutes(channelRoutes);
            return _service;
        }

        private AudioSource CreateAudioSource()
        {
            var child = new GameObject(PooledSourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }
    }
}
