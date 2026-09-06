using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS.Framework.Settings
{
    /// <summary>
    /// 프로젝트별 오디오 채널의 값을 저장하고 Unity 오디오 출력에 적용한다.
    /// </summary>
    public sealed class AudioSettingsService
    {
        /// <summary>
        /// 저장 데이터 형식을 식별하는 버전 번호이다.
        /// </summary>
        private const int SaveVersion = 1;

        /// <summary>
        /// 오디오 설정을 저장할 때 사용하는 저장소 키이다.
        /// 기본 백엔드에서는 같은 이름의 PlayerPrefs 키로 기록된다.
        /// </summary>
        private const string StorageKey = "AudioSettings";

        /// <summary>
        /// 전체 출력을 제어하는 마스터 채널의 식별자이다.
        /// </summary>
        public const string MasterChannelId = "master";

        /// <summary>
        /// 현재 애플리케이션에서 사용 중인 오디오 설정 인스턴스이다.
        /// </summary>
        private static AudioSettingsService _current;

        private readonly List<ChannelState> _channels = new();
        private AudioSettingsPreset _preset;
        private float _masterVolume;
        private bool _isMasterMuted;

        /// <summary>
        /// 오디오 설정이 저장되거나 적용된 뒤 발생한다.
        /// </summary>
        public event Action<AudioSettingsService> OnAudioSettingsChanged;

        /// <summary>
        /// 현재 오디오 설정을 가져온다. FrameworkInitializer보다 먼저 접근하면 값 형식의 기본값으로 초기화한다.
        /// </summary>
        /// <remarks>
        /// 이 정적 접근자는 주입 경로가 아직 없는 지점(FrameworkInitializer의 설정 초기화, 에디터 도구, EditMode 테스트)에서만 쓴다.
        /// 씬에 배치되는 런타임 컴포넌트는 VContainer로 주입받거나 직접 초기화할 수 있다.
        /// </remarks>
        public static AudioSettingsService Current => _current ?? Initialize(null, false, false);

        /// <summary>
        /// 마스터 채널의 현재 볼륨을 가져온다.
        /// </summary>
        public float MasterVolume => _masterVolume;

        /// <summary>
        /// 마스터 채널이 음소거 상태인지 여부를 가져온다.
        /// </summary>
        public bool IsMasterMuted => _isMasterMuted;

        /// <summary>
        /// 프리셋이 정의한 마스터 채널 기본 볼륨을 가져오며, 프리셋이 없으면 0이다.
        /// </summary>
        public float DefaultMasterVolume => _preset != null ? _preset.MasterVolume : default;

        /// <summary>
        /// 프리셋이 정의한 마스터 채널 기본 음소거 여부를 가져오며, 프리셋이 없으면 false이다.
        /// </summary>
        public bool IsDefaultMasterMuted => _preset != null && _preset.IsMasterMuted;

        /// <summary>
        /// 프리셋에 정의된 개별 오디오 채널 상태 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<ChannelState> Channels => _channels;

        /// <summary>
        /// 오디오 설정을 초기화하고 필요에 따라 저장된 설정을 불러오거나 오디오 출력에 적용한다.
        /// 캐시된 인스턴스를 새 인스턴스로 교체하지 않고 제자리에서 갱신하므로,
        /// 초기화 전에 <see cref="Current"/>를 캡처한 소비자와 <see cref="OnAudioSettingsChanged"/> 구독자가
        /// 초기화 결과를 그대로 반영받는다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 사용하지 않을 때 적용할 기본 프리셋이다.</param>
        /// <param name="loadSavedSettings">저장된 설정을 불러올지 여부이다.</param>
        /// <param name="applyOnInitialize">초기화 직후 설정을 오디오 출력에 적용할지 여부이다.</param>
        /// <param name="saveOnInitialize">초기화 직후 설정을 저장할지 여부이다.</param>
        /// <returns>초기화된 오디오 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스이다.</returns>
        public static AudioSettingsService Initialize(
            AudioSettingsPreset defaultSettings = null,
            bool loadSavedSettings = true,
            bool applyOnInitialize = true,
            bool saveOnInitialize = false)
        {
            var settings = EnsureCurrent();
            if (loadSavedSettings)
            {
                settings.LoadInto(defaultSettings);
            }
            else
            {
                settings.Import(defaultSettings);
            }

            if (saveOnInitialize)
            {
                settings.Save();
            }

            if (applyOnInitialize)
            {
                settings.Apply();
            }

            settings.NotifyChanged();
            return settings;
        }

        /// <summary>
        /// 저장된 오디오 설정을 불러오고, 없거나 유효하지 않으면 기본 프리셋 값을 사용한다.
        /// <see cref="Initialize"/>와 마찬가지로 캐시된 인스턴스를 제자리에서 갱신한다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 유효하지 않을 때 적용할 기본 프리셋이다.</param>
        /// <returns>불러온 오디오 설정 인스턴스를 반환하며, <see cref="Current"/>와 항상 같은 인스턴스이다.</returns>
        public static AudioSettingsService Load(AudioSettingsPreset defaultSettings = null)
        {
            var settings = EnsureCurrent();
            settings.LoadInto(defaultSettings);
            return settings;
        }

        /// <summary>
        /// 캐시된 오디오 설정 인스턴스를 가져오며, 없으면 만들어 캐시한다.
        /// 모든 정적 진입점이 이 인스턴스를 재사용해야 초기화가 인스턴스를 교체하지 않는다.
        /// </summary>
        /// <returns>캐시된 오디오 설정 인스턴스를 반환한다.</returns>
        private static AudioSettingsService EnsureCurrent()
        {
            return _current ??= new AudioSettingsService();
        }

        /// <summary>
        /// 도메인 리로드를 끄고 플레이 모드에 진입해도 이전 세션의 정적 상태가 남지 않도록 초기화한다.
        /// 정적 상태를 보유한 프레임워크 클래스는 모두 이 규약(SubsystemRegistration 시점 리셋)을 따르므로,
        /// 새로 정적 필드를 추가하는 작성자는 이 메서드에도 해당 필드를 반드시 추가해야 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _current = null;
        }

        /// <summary>
        /// 테스트에서 정적 캐시 상태가 누출되지 않도록 캐시를 초기 상태로 되돌린다.
        /// 플레이 모드 진입 시 수행하는 리셋과 같은 동작이다.
        /// </summary>
        internal static void ResetCurrentForTests()
        {
            ResetStaticState();
        }

        /// <summary>
        /// 기본 프리셋 값으로 현재 인스턴스 상태를 초기화한 뒤, 저장된 설정이 있으면 덮어쓴다.
        /// 저장 데이터가 손상되었거나 형식 버전이 맞지 않으면 기본 프리셋 값으로 다시 저장한다.
        /// </summary>
        /// <param name="defaultSettings">저장된 설정이 없거나 유효하지 않을 때 적용할 기본 프리셋이다.</param>
        private void LoadInto(AudioSettingsPreset defaultSettings)
        {
            Import(defaultSettings);

            if (!SettingsStorage.Current.TryRead(StorageKey, out var json))
            {
                return;
            }

            try
            {
                if (!Import(JsonUtility.FromJson<SettingsData>(json)))
                {
                    Save();
                }
            }
            catch (ArgumentException)
            {
                Save();
            }
        }

        /// <summary>
        /// 마스터 채널의 볼륨과 음소거 여부를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="volume">설정할 마스터 볼륨이며, 0과 1 사이로 제한된다.</param>
        /// <param name="isMuted">마스터를 음소거할지 여부이다.</param>
        /// <param name="apply">변경한 설정을 즉시 오디오 출력에 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        public void SetMaster(float volume, bool isMuted, bool apply = true, bool save = true)
        {
            _masterVolume = Mathf.Clamp01(volume);
            _isMasterMuted = isMuted;
            Commit(apply, save);
        }

        /// <summary>
        /// 지정한 채널의 볼륨과 음소거 여부를 설정하고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="id">변경할 채널의 식별자이다.</param>
        /// <param name="volume">설정할 채널 볼륨이며, 0과 1 사이로 제한된다.</param>
        /// <param name="isMuted">채널을 음소거할지 여부이다.</param>
        /// <param name="apply">변경한 설정을 즉시 오디오 출력에 적용할지 여부이다.</param>
        /// <param name="save">변경한 설정을 저장할지 여부이다.</param>
        /// <returns>해당 식별자의 채널을 찾아 변경했으면 true를 반환한다.</returns>
        public bool SetChannel(string id, float volume, bool isMuted, bool apply = true, bool save = true)
        {
            if (!TryGetChannel(id, out var channel))
            {
                return false;
            }

            channel.Set(volume, isMuted);
            Commit(apply, save);
            return true;
        }

        /// <summary>
        /// 식별자에 해당하는 채널 상태를 찾는다.
        /// </summary>
        /// <param name="id">찾을 채널의 식별자이다.</param>
        /// <param name="channel">찾은 채널 상태이며, 없으면 null이다.</param>
        /// <returns>채널을 찾았으면 true를 반환한다.</returns>
        public bool TryGetChannel(string id, out ChannelState channel)
        {
            foreach (var item in _channels)
            {
                if (string.Equals(item.Id, id, StringComparison.Ordinal))
                {
                    channel = item;
                    return true;
                }
            }

            channel = null;
            return false;
        }

        /// <summary>
        /// 오디오 설정을 초기화에 사용한 프리셋 기본값으로 되돌리고 필요에 따라 적용하거나 저장한다.
        /// </summary>
        /// <param name="apply">되돌린 설정을 즉시 오디오 출력에 적용할지 여부이다.</param>
        /// <param name="save">되돌린 설정을 저장할지 여부이다.</param>
        public void ResetToDefaults(bool apply = true, bool save = true)
        {
            Import(_preset);
            Commit(apply, save);
        }

        /// <summary>
        /// 현재 오디오 설정을 설정 저장소에 저장한다.
        /// </summary>
        public void Save()
        {
            SettingsStorage.Current.Write(StorageKey, JsonUtility.ToJson(Export()));
        }

        /// <summary>
        /// 현재 오디오 설정을 Unity 오디오 출력에 적용한다.
        /// </summary>
        public void Apply()
        {
            AudioListener.volume = _isMasterMuted ? 0f : _masterVolume;
            foreach (var channel in _channels)
            {
                channel.Apply();
            }
        }

        private void Commit(bool apply, bool save)
        {
            if (apply)
            {
                Apply();
            }

            if (save)
            {
                Save();
            }

            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnAudioSettingsChanged?.Invoke(this);
        }

        private void Import(AudioSettingsPreset preset)
        {
            _preset = preset;
            _masterVolume = preset != null ? preset.MasterVolume : default;
            _isMasterMuted = preset != null && preset.IsMasterMuted;
            _channels.Clear();

            if (preset == null)
            {
                return;
            }

            foreach (var channelPreset in preset.ChannelPresets)
            {
                if (channelPreset == null || string.IsNullOrWhiteSpace(channelPreset.Id) || TryGetChannel(channelPreset.Id, out _))
                {
                    continue;
                }

                _channels.Add(new ChannelState(channelPreset, channelPreset.DefaultVolume, channelPreset.IsDefaultMuted));
            }
        }

        private bool Import(SettingsData data)
        {
            if (data == null || data.version != SaveVersion)
            {
                return false;
            }

            _masterVolume = Mathf.Clamp01(data.masterVolume);
            _isMasterMuted = data.isMasterMuted;
            if (data.channels != null)
            {
                foreach (var savedChannel in data.channels)
                {
                    if (TryGetChannel(savedChannel.id, out var channel))
                    {
                        channel.Set(savedChannel.volume, savedChannel.isMuted);
                    }
                }
            }

            return true;
        }

        private SettingsData Export()
        {
            var channels = new ChannelData[_channels.Count];
            for (var index = 0; index < _channels.Count; index++)
            {
                var channel = _channels[index];
                channels[index] = new ChannelData
                {
                    id = channel.Id,
                    volume = channel.Volume,
                    isMuted = channel.IsMuted
                };
            }

            return new SettingsData
            {
                version = SaveVersion,
                masterVolume = _masterVolume,
                isMasterMuted = _isMasterMuted,
                channels = channels
            };
        }

        [Serializable]
        private sealed class SettingsData
        {
            public int version;
            public float masterVolume;
            public bool isMasterMuted;
            public ChannelData[] channels;
        }

        [Serializable]
        private sealed class ChannelData
        {
            public string id;
            public float volume;
            public bool isMuted;
        }

        /// <summary>
        /// 런타임에서 사용하는 프로젝트 오디오 채널의 상태이다.
        /// </summary>
        public sealed class ChannelState
        {
            internal ChannelState(AudioChannelPreset preset, float volume, bool isMuted)
            {
                Preset = preset;
                Set(volume, isMuted);
            }

            internal AudioChannelPreset Preset { get; }
            public string Id => Preset.Id;
            public string DisplayName => Preset.DisplayName;
            public float DefaultVolume => Preset.DefaultVolume;
            public bool IsDefaultMuted => Preset.IsDefaultMuted;
            public float Volume { get; private set; }
            public bool IsMuted { get; private set; }

            internal void Set(float volume, bool isMuted)
            {
                Volume = Mathf.Clamp01(volume);
                IsMuted = isMuted;
            }

            internal void Apply()
            {
                Preset.Apply(Volume, IsMuted);
            }
        }
    }
}
