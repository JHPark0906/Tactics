using System;
using UnityEngine;

namespace HS.Framework.Persistence
{
    /// <summary>
    /// 저장 가능한 게임 데이터의 공통 메타데이터를 제공한다.
    /// </summary>
    [Serializable]
    public abstract class SaveGame
    {
        [SerializeField] private int version;
        [SerializeField] private long createdAtUtcTicks;
        [SerializeField] private long lastSavedAtUtcTicks;

        /// <summary>
        /// 저장 데이터의 스키마 버전이다.
        /// </summary>
        public int Version => version;

        /// <summary>
        /// 저장 데이터를 처음 생성한 UTC 시각이다.
        /// </summary>
        public DateTime CreatedAtUtc => new(createdAtUtcTicks, DateTimeKind.Utc);

        /// <summary>
        /// 저장 데이터를 마지막으로 기록한 UTC 시각이다.
        /// </summary>
        public DateTime LastSavedAtUtc => new(lastSavedAtUtcTicks, DateTimeKind.Utc);

        internal void Initialize(int version, DateTime utcNow)
        {
            this.version = version;
            createdAtUtcTicks = utcNow.Ticks;
            lastSavedAtUtcTicks = utcNow.Ticks;
        }

        internal void PrepareForSave(int version, DateTime utcNow)
        {
            this.version = version;
            if (createdAtUtcTicks == default)
            {
                createdAtUtcTicks = utcNow.Ticks;
            }

            lastSavedAtUtcTicks = utcNow.Ticks;
        }
    }
}
