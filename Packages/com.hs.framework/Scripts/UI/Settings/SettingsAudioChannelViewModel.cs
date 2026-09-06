using HS.Framework.Foundation.MVVM;
using HS.Framework.Settings;
using UnityEngine;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 오디오 설정 서비스의 마스터 또는 채널 값을 편집한다.
    /// </summary>
    public sealed class SettingsAudioChannelViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {
        private readonly AudioSettingsService _settingsService;
        private readonly bool _isMaster;
        private float _appliedVolume;
        private bool _appliedMuted;
        private float _pendingVolume;
        private bool _pendingMuted;

        public SettingsAudioChannelViewModel(
            AudioSettingsService settingsService,
            string id,
            string displayName,
            float defaultVolume,
            bool defaultMuted,
            bool isMaster = false,
            float? initialVolume = null,
            bool? initialMuted = null)
        {
            _settingsService = settingsService;
            _isMaster = isMaster;
            Id = id;
            DisplayName = displayName;
            DefaultVolume = Mathf.Clamp01(defaultVolume);
            DefaultMuted = defaultMuted;
            _appliedVolume = Mathf.Clamp01(initialVolume ?? DefaultVolume);
            _appliedMuted = initialMuted ?? DefaultMuted;
            _pendingVolume = _appliedVolume;
            _pendingMuted = _appliedMuted;
        }

        /// <summary>
        /// 이전 정의 기반 UI와의 호환을 위해 독립 편집 항목을 만든다.
        /// </summary>
        public SettingsAudioChannelViewModel(SettingsAudioChannelDefinition definition)
            : this(null, definition?.Id, definition?.DisplayName, definition?.DefaultVolume ?? 0f, definition?.DefaultMuted ?? false)
        {
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float DefaultVolume { get; }
        public bool DefaultMuted { get; }

        public float PendingVolume
        {
            get => _pendingVolume;
            set => SetTrackedValue(ref _pendingVolume, Mathf.Clamp01(value));
        }

        public bool PendingMuted
        {
            get => _pendingMuted;
            set => SetTrackedValue(ref _pendingMuted, value);
        }

        public float AppliedVolume => _appliedVolume;
        public bool AppliedMuted => _appliedMuted;
        public override bool HasChanges => !Mathf.Approximately(_appliedVolume, PendingVolume) || _appliedMuted != PendingMuted;

        public void Apply()
        {
            Apply(false);
        }

        public void Apply(bool save)
        {
            if (_settingsService != null)
            {
                if (_isMaster)
                {
                    _settingsService.SetMaster(PendingVolume, PendingMuted, true, save);
                }
                else
                {
                    _settingsService.SetChannel(Id, PendingVolume, PendingMuted, true, save);
                }
            }

            _appliedVolume = PendingVolume;
            _appliedMuted = PendingMuted;
            OnPropertiesChanged(nameof(AppliedVolume), nameof(AppliedMuted), nameof(HasChanges));
        }

        public void Cancel()
        {
            PendingVolume = _appliedVolume;
            PendingMuted = _appliedMuted;
            OnHasChangesChanged();
        }

        public void ResetToDefaults()
        {
            PendingVolume = DefaultVolume;
            PendingMuted = DefaultMuted;
            OnHasChangesChanged();
        }
    }
}
