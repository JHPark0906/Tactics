using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace HS.Framework.Audio
{
    /// <summary>
    /// 사운드 큐 기반 효과음·배경음 재생을 담당하는 서비스 구현이다.
    /// 효과음 소스는 풀에서 대여해 재생이 끝나면 반환하고, 배경음은 2개 슬롯 크로스페이드로 전환한다.
    /// 재생 호출 경로에서는 힙 할당이 발생하지 않는다.
    /// </summary>
    public sealed class SoundService : ISoundService
    {
        private readonly ObjectPool<AudioSource> _sfxSourcePool;
        private readonly List<AudioSource> _activeSfxSources = new();
        private readonly Dictionary<string, AudioMixerGroup> _channelOutputGroups = new(StringComparer.Ordinal);
        private readonly AudioSource[] _bgmSources = new AudioSource[BgmCrossfadeState.SlotCount];
        private readonly float[] _bgmBaseVolumes = new float[BgmCrossfadeState.SlotCount];
        private readonly BgmCrossfadeState _bgmCrossfade = new();

        /// <summary>소스 생성 함수와 효과음 풀 사전 워밍업 수를 받아 서비스를 생성한다.</summary>
        public SoundService(Func<AudioSource> createSource, int sfxPrewarmCount)
        {
            if (createSource == null)
            {
                throw new ArgumentNullException(nameof(createSource));
            }

            _sfxSourcePool = new ObjectPool<AudioSource>(createSource);
            _sfxSourcePool.Prewarm(Mathf.Max(0, sfxPrewarmCount));

            for (var slot = 0; slot < _bgmSources.Length; slot++)
            {
                var source = createSource();
                source.spatialBlend = 0f;
                _bgmSources[slot] = source;
            }
        }

        /// <summary>
        /// 채널 id와 출력 믹서 그룹의 연결을 구성한다. 기존 구성은 대체된다.
        /// </summary>
        public void ConfigureChannelRoutes(IReadOnlyList<SoundChannelRoute> routes)
        {
            _channelOutputGroups.Clear();
            if (routes == null)
            {
                return;
            }

            for (var index = 0; index < routes.Count; index++)
            {
                var route = routes[index];
                if (string.IsNullOrWhiteSpace(route.ChannelId) || route.OutputGroup == null)
                {
                    continue;
                }

                _channelOutputGroups[route.ChannelId] = route.OutputGroup;
            }
        }

        /// <inheritdoc />
        public void PlaySfx(AudioCue cue)
        {
            PlaySfxInternal(cue, default, false);
        }

        /// <inheritdoc />
        public void PlaySfx(AudioCue cue, Vector3 position)
        {
            PlaySfxInternal(cue, position, true);
        }

        /// <inheritdoc />
        public void StopAllSfx()
        {
            for (var index = 0; index < _activeSfxSources.Count; index++)
            {
                var source = _activeSfxSources[index];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                _sfxSourcePool.Release(source);
            }

            _activeSfxSources.Clear();
        }

        /// <inheritdoc />
        public void PlayBgm(AudioCue cue, float fadeSeconds)
        {
            if (cue == null || !cue.TryCreatePlayback(out var playback))
            {
                return;
            }

            var slot = _bgmCrossfade.BeginPlay(fadeSeconds);
            var source = _bgmSources[slot];
            _bgmBaseVolumes[slot] = playback.Volume;
            source.clip = playback.Clip;
            source.pitch = playback.Pitch;
            source.loop = playback.IsLoop;
            source.outputAudioMixerGroup = ResolveOutputGroup(in playback);
            source.volume = _bgmCrossfade.GetWeight(slot) * playback.Volume;
            source.Play();
        }

        /// <inheritdoc />
        public void StopBgm(float fadeSeconds)
        {
            _bgmCrossfade.BeginStop(fadeSeconds);
        }

        /// <summary>
        /// 경과 시간만큼 서비스 상태를 갱신한다. 재생이 끝난 효과음 소스를 풀로 반환하고 배경음 페이드를 진행한다.
        /// </summary>
        /// <param name="deltaTime">경과 시간(초)이다. 일시정지 중에도 페이드가 진행되도록 비스케일 시간을 권장한다.</param>
        public void Tick(float deltaTime)
        {
            ReturnFinishedSfxSources();
            TickBgm(deltaTime);
        }

        private void PlaySfxInternal(AudioCue cue, Vector3 position, bool hasPosition)
        {
            if (cue == null || !cue.TryCreatePlayback(out var playback))
            {
                return;
            }

            var source = _sfxSourcePool.Acquire();
            if (hasPosition)
            {
                source.transform.position = position;
                source.spatialBlend = 1f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.clip = playback.Clip;
            source.volume = playback.Volume;
            source.pitch = playback.Pitch;
            source.loop = playback.IsLoop;
            source.outputAudioMixerGroup = ResolveOutputGroup(in playback);
            source.Play();
            _activeSfxSources.Add(source);
        }

        private void ReturnFinishedSfxSources()
        {
            if (AudioListener.pause)
            {
                return;
            }

            for (var index = _activeSfxSources.Count - 1; index >= 0; index--)
            {
                var source = _activeSfxSources[index];
                if (source != null && source.isPlaying)
                {
                    continue;
                }

                _activeSfxSources.RemoveAt(index);
                if (source == null)
                {
                    continue;
                }

                source.clip = null;
                _sfxSourcePool.Release(source);
            }
        }

        private void TickBgm(float deltaTime)
        {
            _bgmCrossfade.Tick(deltaTime);

            for (var slot = 0; slot < _bgmSources.Length; slot++)
            {
                var source = _bgmSources[slot];
                if (source == null)
                {
                    continue;
                }

                var weight = _bgmCrossfade.GetWeight(slot);
                source.volume = weight * _bgmBaseVolumes[slot];

                var isFadedOut = weight <= 0f && (slot != _bgmCrossfade.ActiveSlot || _bgmCrossfade.IsStopping);
                if (isFadedOut && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        private AudioMixerGroup ResolveOutputGroup(in AudioCuePlayback playback)
        {
            if (playback.OutputGroupOverride != null)
            {
                return playback.OutputGroupOverride;
            }

            if (!string.IsNullOrEmpty(playback.OutputChannelId) &&
                _channelOutputGroups.TryGetValue(playback.OutputChannelId, out var group))
            {
                return group;
            }

            return null;
        }
    }
}
