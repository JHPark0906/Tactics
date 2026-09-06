using System;
using UnityEngine;

namespace HS.Framework.UI.Settings
{
    /// <summary>
    /// 프로젝트별 오디오 채널 설정 항목을 정의한다.
    /// </summary>
    [Serializable]
    public sealed class SettingsAudioChannelDefinition
    {
        /// <summary>
        /// 채널을 식별하는 ID이다.
        /// </summary>
        [SerializeField] private string id;

        /// <summary>
        /// 사용자에게 표시할 채널 이름이다.
        /// </summary>
        [SerializeField] private string displayName;

        /// <summary>
        /// 기본 볼륨 값이다.
        /// </summary>
        [SerializeField] [Range(0f, 1f)] private float defaultVolume = 1f;

        /// <summary>
        /// 기본 음소거 여부이다.
        /// </summary>
        [SerializeField] private bool defaultMuted;

        /// <summary>
        /// 오디오 채널 정의를 생성한다.
        /// </summary>
        public SettingsAudioChannelDefinition()
        {
        }

        /// <summary>
        /// 오디오 채널 정의를 생성한다.
        /// </summary>
        /// <param name="id">채널을 식별하는 ID이다.</param>
        /// <param name="displayName">사용자에게 표시할 채널 이름이다.</param>
        /// <param name="defaultVolume">기본 볼륨 값이다.</param>
        /// <param name="defaultMuted">기본 음소거 여부이다.</param>
        public SettingsAudioChannelDefinition(string id, string displayName, float defaultVolume = 1f, bool defaultMuted = false)
        {
            this.id = id;
            this.displayName = displayName;
            this.defaultVolume = Mathf.Clamp01(defaultVolume);
            this.defaultMuted = defaultMuted;
        }

        /// <summary>
        /// 채널을 식별하는 ID를 가져온다.
        /// </summary>
        public string Id => string.IsNullOrWhiteSpace(id) ? DisplayName : id;

        /// <summary>
        /// 사용자에게 표시할 채널 이름을 가져온다.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

        /// <summary>
        /// 기본 볼륨 값을 가져온다.
        /// </summary>
        public float DefaultVolume => Mathf.Clamp01(defaultVolume);

        /// <summary>
        /// 기본 음소거 여부를 가져온다.
        /// </summary>
        public bool DefaultMuted => defaultMuted;
    }
}
