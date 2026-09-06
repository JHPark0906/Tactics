using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// 같은 형식의 게임 저장 데이터를 여러 슬롯으로 관리하고 주입된 저장소 백엔드에 기록한다.
    /// 백엔드를 지정하지 않으면 PlayerPrefs 백엔드를 사용한다.
    /// </summary>
    public abstract class SaveGameSlot<T> where T : SaveGame, new()
    {
        private readonly Dictionary<int, T> _saveGames = new();
        private readonly Action<SaveGameLoadedEvent> _publishLoaded;
        private readonly Action<SaveGameSavedEvent> _publishSaved;
        private readonly ISaveDataStorage _storage;
        private readonly string _playerPrefsKey;
        private readonly int _saveVersion;

        /// <summary>
        /// 새 게임에 사용하는 기본 저장 슬롯 번호이다.
        /// </summary>
        protected virtual int DefaultSlotIndex => 0;

        /// <summary>
        /// 저장할 수 있는 최대 슬롯 수이다.
        /// </summary>
        protected virtual int MaximumSlotCount => int.MaxValue;

        /// <summary>
        /// 메모리에 로드된 저장 데이터이다.
        /// </summary>
        public IReadOnlyDictionary<int, T> SaveGames => _saveGames;

        /// <summary>
        /// 저장 데이터의 현재 스키마 버전이다.
        /// </summary>
        public int SaveVersion => _saveVersion;

        /// <summary>
        /// 저장할 수 있는 최대 슬롯 수이다.
        /// </summary>
        public int MaxSlotCount => MaximumSlotCount;

        /// <summary>
        /// MessagePipe 발행자를 사용하는 저장 슬롯 관리자를 생성한다.
        /// </summary>
        protected SaveGameSlot(
            string playerPrefsKey,
            IPublisher<SaveGameLoadedEvent> loadedPublisher,
            IPublisher<SaveGameSavedEvent> savedPublisher,
            int saveVersion = 1,
            ISaveDataStorage storage = null)
        {
            if (string.IsNullOrWhiteSpace(playerPrefsKey))
            {
                throw new ArgumentException("저장소 키는 비어 있을 수 없습니다.", nameof(playerPrefsKey));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion), "저장 버전은 1 이상이어야 합니다.");
            }

            _playerPrefsKey = playerPrefsKey;
            _publishLoaded = loadedPublisher == null
                ? throw new ArgumentNullException(nameof(loadedPublisher))
                : loadedPublisher.Publish;
            _publishSaved = savedPublisher == null
                ? throw new ArgumentNullException(nameof(savedPublisher))
                : savedPublisher.Publish;
            _saveVersion = saveVersion;
            _storage = storage ?? new PlayerPrefsSaveDataStorage();
        }

        /// <summary>
        /// 현재 프로젝트에 설정된 기본 저장 슬롯 번호를 반환한다.
        /// </summary>
        public int GetDefaultSlotIndex()
        {
            ValidateSlotConfiguration();
            return DefaultSlotIndex;
        }

        /// <summary>
        /// 저장소 백엔드에서 저장 슬롯을 불러온다.
        /// </summary>
        /// <returns>호환 가능한 저장 데이터를 불러왔으면 true이다.</returns>
        public bool Load()
        {
            ValidateSlotConfiguration();
            _saveGames.Clear();
            if (!_storage.TryRead(_playerPrefsKey, out var json))
            {
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<SerializedSaveGameSlotData>(json);
                if (data == null || data.version != _saveVersion || data.entries == null)
                {
                    return false;
                }

                foreach (var entry in data.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.saveGameJson) || !IsSlotIndexValid(entry.slotIndex))
                    {
                        continue;
                    }

                    var saveGame = JsonUtility.FromJson<T>(entry.saveGameJson);
                    if (saveGame == null || saveGame.Version != _saveVersion)
                    {
                        continue;
                    }

                    _saveGames.TryAdd(entry.slotIndex, saveGame);
                }

                _publishLoaded(new SaveGameLoadedEvent(_playerPrefsKey, _saveGames.Count));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// 메모리의 저장 슬롯을 저장소 백엔드에 기록한다.
        /// </summary>
        public void Save()
        {
            ValidateSlotConfiguration();
            var entries = new List<SerializedSaveGameEntry>(_saveGames.Count);
            var utcNow = DateTime.UtcNow;
            foreach (var pair in _saveGames)
            {
                pair.Value.PrepareForSave(_saveVersion, utcNow);
                entries.Add(new SerializedSaveGameEntry
                {
                    slotIndex = pair.Key,
                    saveGameJson = JsonUtility.ToJson(pair.Value)
                });
            }

            entries.Sort((left, right) => left.slotIndex.CompareTo(right.slotIndex));
            var data = new SerializedSaveGameSlotData
            {
                version = _saveVersion,
                entries = entries.ToArray()
            };

            _storage.Write(_playerPrefsKey, JsonUtility.ToJson(data));
            _publishSaved(new SaveGameSavedEvent(_playerPrefsKey, _saveGames.Count, utcNow));
        }

        /// <summary>
        /// 새 저장 데이터를 생성하거나 기존 데이터를 반환한다.
        /// </summary>
        public T GetOrCreate(int slotIndex)
        {
            ValidateSlotConfiguration();
            ValidateSlotIndex(slotIndex);
            if (_saveGames.TryGetValue(slotIndex, out var saveGame))
            {
                return saveGame;
            }

            saveGame = new T();
            saveGame.Initialize(_saveVersion, DateTime.UtcNow);
            _saveGames.Add(slotIndex, saveGame);
            return saveGame;
        }

        /// <summary>
        /// 지정한 슬롯의 저장 데이터를 가져온다.
        /// </summary>
        public bool TryGet(int slotIndex, out T saveGame)
        {
            ValidateSlotConfiguration();
            ValidateSlotIndex(slotIndex);
            return _saveGames.TryGetValue(slotIndex, out saveGame);
        }

        /// <summary>
        /// 지정한 슬롯의 저장 데이터를 설정한다.
        /// </summary>
        public void Set(int slotIndex, T saveGame)
        {
            ValidateSlotConfiguration();
            ValidateSlotIndex(slotIndex);
            if (saveGame == null)
            {
                throw new ArgumentNullException(nameof(saveGame));
            }

            _saveGames[slotIndex] = saveGame;
        }

        /// <summary>
        /// 지정한 저장 슬롯을 메모리에서 제거한다.
        /// </summary>
        public bool Remove(int slotIndex)
        {
            ValidateSlotConfiguration();
            ValidateSlotIndex(slotIndex);
            return _saveGames.Remove(slotIndex);
        }

        /// <summary>
        /// 모든 저장 슬롯을 메모리에서 제거한다.
        /// </summary>
        public void Clear()
        {
            _saveGames.Clear();
        }

        /// <summary>
        /// 저장소 백엔드에 기록된 모든 저장 슬롯을 삭제한다.
        /// </summary>
        public void DeleteSavedData()
        {
            _saveGames.Clear();
            _storage.Delete(_playerPrefsKey);
        }

        private bool IsSlotIndexValid(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < MaximumSlotCount;
        }

        private void ValidateSlotIndex(int slotIndex)
        {
            if (!IsSlotIndexValid(slotIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"슬롯 인덱스는 0 이상 {MaximumSlotCount - 1} 이하여야 합니다.");
            }
        }

        private void ValidateSlotConfiguration()
        {
            if (MaximumSlotCount < 1)
            {
                throw new InvalidOperationException("최대 슬롯 수는 1 이상이어야 합니다.");
            }

            if (!IsSlotIndexValid(DefaultSlotIndex))
            {
                throw new InvalidOperationException("기본 슬롯 번호가 유효한 범위를 벗어났습니다.");
            }
        }

    }

    /// <summary>
    /// Unity JSON 직렬화에 사용하는 비제네릭 저장 슬롯 컨테이너이다.
    /// </summary>
    [Serializable]
    internal sealed class SerializedSaveGameSlotData
    {
        public int version;
        public SerializedSaveGameEntry[] entries;
    }

    /// <summary>
    /// 하나의 저장 슬롯과 직렬화된 게임 데이터를 나타낸다.
    /// </summary>
    [Serializable]
    internal sealed class SerializedSaveGameEntry
    {
        public int slotIndex;
        public string saveGameJson;
    }
}
