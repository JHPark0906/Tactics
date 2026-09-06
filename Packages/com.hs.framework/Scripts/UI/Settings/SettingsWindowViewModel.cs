using System.Collections.Generic;
using System.ComponentModel;
using HS.Framework.Foundation.MVVM;
using HS.Framework.Settings;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 설정창 전체의 그래픽, 입력, 오디오, 로케일 섹션 ViewModel을 조합한다.
    /// </summary>
    public sealed class SettingsWindowViewModel : ChangeTrackingViewModelBase, IEditableViewModel
    {
        private readonly AudioSettingsService _audioSettingsService;

        /// <summary>
        /// 설정창 ViewModel을 생성한다.
        /// </summary>
        /// <param name="graphics">그래픽 설정 섹션 ViewModel이다.</param>
        /// <param name="input">입력 설정 섹션 ViewModel이다.</param>
        /// <param name="audioChannels">오디오 채널 섹션 ViewModel 목록이다.</param>
        /// <param name="audioSettingsService">오디오 변경을 저장할 설정 서비스다.</param>
        /// <param name="locale">로케일 설정 섹션 ViewModel이다.</param>
        public SettingsWindowViewModel(
            GraphicsSettingsViewModel graphics,
            InputSettingsViewModel input,
            IEnumerable<SettingsAudioChannelViewModel> audioChannels = null,
            AudioSettingsService audioSettingsService = null,
            LocaleSettingsViewModel locale = null)
        {
            Graphics = graphics;
            Input = input;
            Locale = locale;
            AudioChannels = new List<SettingsAudioChannelViewModel>(audioChannels ?? new SettingsAudioChannelViewModel[0]);
            _audioSettingsService = audioSettingsService;

            SubscribeChild(Graphics);
            SubscribeChild(Input);
            SubscribeChild(Locale);
            foreach (var audioChannel in AudioChannels)
            {
                SubscribeChild(audioChannel);
            }
        }

        /// <summary>
        /// 그래픽 설정 섹션 ViewModel을 가져온다.
        /// </summary>
        public GraphicsSettingsViewModel Graphics { get; }

        /// <summary>
        /// 입력 설정 섹션 ViewModel을 가져온다.
        /// </summary>
        public InputSettingsViewModel Input { get; }

        /// <summary>
        /// 오디오 채널 섹션 ViewModel 목록을 가져온다.
        /// </summary>
        public IReadOnlyList<SettingsAudioChannelViewModel> AudioChannels { get; }

        /// <summary>
        /// 로케일 설정 섹션 ViewModel을 가져온다.
        /// </summary>
        public LocaleSettingsViewModel Locale { get; }

        /// <summary>
        /// 설정창 전체에 적용 대기 중인 변경 값이 있는지 여부를 가져온다.
        /// </summary>
        public override bool HasChanges
        {
            get
            {
                if ((Graphics != null && Graphics.HasChanges) ||
                    (Input != null && Input.HasChanges) ||
                    (Locale != null && Locale.HasChanges))
                {
                    return true;
                }

                foreach (var audioChannel in AudioChannels)
                {
                    if (audioChannel.HasChanges)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 지정한 설정 서비스와 오디오 채널 정의를 사용해 기본 설정창 ViewModel을 만든다.
        /// </summary>
        /// <param name="displaySettingsService">그래픽 설정 서비스이다.</param>
        /// <param name="inputSettingsService">입력 설정 서비스이다.</param>
        /// <param name="audioSettingsService">프로젝트 오디오 설정 서비스이다.</param>
        /// <param name="localeSettingsService">프로젝트 로케일 설정 서비스이며, null이면 로케일 섹션을 만들지 않는다.</param>
        /// <returns>생성된 설정창 ViewModel이다.</returns>
        public static SettingsWindowViewModel CreateDefault(
            DisplaySettingsService displaySettingsService,
            InputSettingsService inputSettingsService,
            AudioSettingsService audioSettingsService,
            LocaleSettingsService localeSettingsService = null)
        {
            var audioChannels = new List<SettingsAudioChannelViewModel>();

            if (audioSettingsService != null)
            {
                audioChannels.Add(new SettingsAudioChannelViewModel(
                    audioSettingsService,
                    AudioSettingsService.MasterChannelId,
                    "Master",
                    audioSettingsService.DefaultMasterVolume,
                    audioSettingsService.IsDefaultMasterMuted,
                    true,
                    audioSettingsService.MasterVolume,
                    audioSettingsService.IsMasterMuted));

                foreach (var channel in audioSettingsService.Channels)
                {
                    audioChannels.Add(new SettingsAudioChannelViewModel(
                        audioSettingsService,
                        channel.Id,
                        channel.DisplayName,
                        channel.DefaultVolume,
                        channel.IsDefaultMuted,
                        false,
                        channel.Volume,
                        channel.IsMuted));
                }
            }

            var viewModel = new SettingsWindowViewModel(
                new GraphicsSettingsViewModel(displaySettingsService),
                new InputSettingsViewModel(inputSettingsService),
                audioChannels,
                audioSettingsService,
                localeSettingsService != null ? new LocaleSettingsViewModel(localeSettingsService) : null);
            return viewModel;
        }

        /// <summary>
        /// 이전 정의 기반 UI와의 호환을 위해 독립 오디오 편집 항목을 포함한 ViewModel을 만든다.
        /// </summary>
        public static SettingsWindowViewModel CreateDefault(
            DisplaySettingsService displaySettingsService,
            InputSettingsService inputSettingsService,
            IEnumerable<SettingsAudioChannelDefinition> audioChannelDefinitions)
        {
            var audioChannels = new List<SettingsAudioChannelViewModel>();
            if (audioChannelDefinitions != null)
            {
                foreach (var definition in audioChannelDefinitions)
                {
                    audioChannels.Add(new SettingsAudioChannelViewModel(definition));
                }
            }

            return new SettingsWindowViewModel(
                new GraphicsSettingsViewModel(displaySettingsService),
                new InputSettingsViewModel(inputSettingsService),
                audioChannels);
        }

        /// <summary>
        /// 설정창 전체의 편집 중인 값을 런타임에 적용하고 저장한다.
        /// </summary>
        public void Apply()
        {
            Apply(true, true);
        }

        /// <summary>
        /// 설정창 전체의 편집 중인 값을 적용한다.
        /// 변경이 없는 섹션은 건너뛰어 불필요한 재적용과 PlayerPrefs 재저장을 막는다.
        /// </summary>
        /// <param name="applyGraphicsToRuntime">그래픽 설정을 Unity 화면에 즉시 적용할지 여부이다.</param>
        /// <param name="save">PlayerPrefs에 저장할지 여부이다.</param>
        public void Apply(bool applyGraphicsToRuntime = true, bool save = true)
        {
            if (Graphics != null && Graphics.HasChanges)
            {
                Graphics.Apply(applyGraphicsToRuntime, save);
            }

            if (Input != null && Input.HasChanges)
            {
                Input.Apply(save);
            }

            if (Locale != null && Locale.HasChanges)
            {
                Locale.Apply(save);
            }

            var hasAudioChanges = false;
            foreach (var audioChannel in AudioChannels)
            {
                if (audioChannel.HasChanges)
                {
                    audioChannel.Apply(false);
                    hasAudioChanges = true;
                }
            }

            if (save && hasAudioChanges)
            {
                _audioSettingsService?.Save();
            }

            OnHasChangesChanged();
        }

        /// <summary>
        /// 설정창 전체의 편집 중인 값을 마지막 적용 값으로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            Graphics?.Cancel();
            Input?.Cancel();
            Locale?.Cancel();

            foreach (var audioChannel in AudioChannels)
            {
                audioChannel.Cancel();
            }

            OnHasChangesChanged();
        }

        /// <summary>
        /// 설정창 전체의 편집 중인 값을 기본값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            Graphics?.ResetToDefaults();
            Input?.ResetToDefaults();
            Locale?.ResetToDefaults();

            foreach (var audioChannel in AudioChannels)
            {
                audioChannel.ResetToDefaults();
            }

            OnHasChangesChanged();
        }

        /// <summary>
        /// 하위 ViewModel 변경 시 전체 변경 여부를 갱신한다.
        /// </summary>
        /// <param name="child">구독할 하위 ViewModel이다.</param>
        private void SubscribeChild(INotifyPropertyChanged child)
        {
            if (child != null)
            {
                child.PropertyChanged += OnChildPropertyChanged;
            }
        }

        /// <summary>
        /// 하위 ViewModel 변경 시 설정창 전체 변경 여부를 알린다.
        /// </summary>
        /// <param name="sender">변경된 하위 ViewModel이다.</param>
        /// <param name="e">속성 변경 이벤트 인자이다.</param>
        private void OnChildPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HasChanges))
            {
                OnHasChangesChanged();
            }
        }

        /// <summary>
        /// 하위 ViewModel 구독과 보유한 ViewModel을 해제한다.
        /// </summary>
        protected override void OnDispose()
        {
            UnsubscribeChild(Graphics);
            UnsubscribeChild(Input);
            UnsubscribeChild(Locale);

            foreach (var audioChannel in AudioChannels)
            {
                UnsubscribeChild(audioChannel);
                audioChannel.Dispose();
            }

            Graphics?.Dispose();
            Input?.Dispose();
            Locale?.Dispose();
            base.OnDispose();
        }

        /// <summary>
        /// 하위 ViewModel의 속성 변경 구독을 해제한다.
        /// </summary>
        /// <param name="child">구독을 해제할 하위 ViewModel이다.</param>
        private void UnsubscribeChild(INotifyPropertyChanged child)
        {
            if (child != null)
            {
                child.PropertyChanged -= OnChildPropertyChanged;
            }
        }
    }
}
