using System;
using UnityEngine;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// PlayerPrefs를 사용하는 기본 저장소 백엔드이다.
    /// 소량의 설정·저장 데이터에 적합하며, Windows에서는 레지스트리에 기록되므로 용량 한계가 있다.
    /// </summary>
    public sealed class PlayerPrefsSaveDataStorage : ISaveDataStorage
    {
        /// <inheritdoc />
        public bool Exists(string key)
        {
            ValidateKey(key);
            return PlayerPrefs.HasKey(key);
        }

        /// <inheritdoc />
        public bool TryRead(string key, out string value)
        {
            ValidateKey(key);
            if (!PlayerPrefs.HasKey(key))
            {
                value = null;
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        /// <inheritdoc />
        public void Write(string key, string value)
        {
            ValidateKey(key);
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            ValidateKey(key);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장소 키가 비어 있지 않은지 검증한다.
        /// </summary>
        /// <param name="key">검증할 저장소 키이다.</param>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("저장소 키는 비어 있을 수 없습니다.", nameof(key));
            }
        }
    }
}
