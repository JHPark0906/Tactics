using HS.Framework.Foundation.MVVM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 단일 오디오 채널 ViewModel을 UGUI 요소에 표시하고 편집 입력을 전달한다.
    /// </summary>
    public sealed class SettingsAudioChannelItemView : ViewBase<SettingsAudioChannelViewModel>
    {
        /// <summary>
        /// 채널 이름을 표시하는 텍스트이다.
        /// </summary>
        [SerializeField] private TMP_Text channelNameText;

        /// <summary>
        /// 볼륨 값을 편집하는 슬라이더이다.
        /// </summary>
        [SerializeField] private Slider volumeSlider;

        /// <summary>
        /// 음소거 여부를 편집하는 토글이다.
        /// </summary>
        [SerializeField] private Toggle muteToggle;

        /// <summary>
        /// 오디오 채널 ViewModel과 UI 요소를 연결한다.
        /// </summary>
        protected override void OnViewModelBound()
        {
            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (muteToggle != null)
            {
                muteToggle.onValueChanged.RemoveListener(OnMutedChanged);
                muteToggle.onValueChanged.AddListener(OnMutedChanged);
            }
        }

        /// <summary>
        /// UI 표시 값을 ViewModel 상태와 맞춘다.
        /// </summary>
        public override void Refresh()
        {
            if (ViewModel == null)
            {
                return;
            }

            if (channelNameText != null)
            {
                channelNameText.text = ViewModel.DisplayName;
            }

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(ViewModel.PendingVolume);
            }

            if (muteToggle != null)
            {
                muteToggle.SetIsOnWithoutNotify(ViewModel.PendingMuted);
            }
        }

        /// <summary>
        /// 오브젝트가 제거될 때 UI 이벤트 구독을 정리한다.
        /// </summary>
        protected override void OnDestroy()
        {
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            }

            if (muteToggle != null)
            {
                muteToggle.onValueChanged.RemoveListener(OnMutedChanged);
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 볼륨 슬라이더 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="volume">새 볼륨 값이다.</param>
        private void OnVolumeChanged(float volume)
        {
            if (ViewModel != null)
            {
                ViewModel.PendingVolume = volume;
            }
        }

        /// <summary>
        /// 음소거 토글 변경 값을 ViewModel에 전달한다.
        /// </summary>
        /// <param name="isMuted">새 음소거 여부이다.</param>
        private void OnMutedChanged(bool isMuted)
        {
            if (ViewModel != null)
            {
                ViewModel.PendingMuted = isMuted;
            }
        }
    }
}
