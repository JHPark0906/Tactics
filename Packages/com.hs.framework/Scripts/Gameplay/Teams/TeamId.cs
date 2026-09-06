using System;
using UnityEngine;

namespace HS.Framework.Gameplay.Teams
{
    /// <summary>
    /// 진영을 구분하는 식별자이다.
    /// 진영 구성은 프로젝트마다 다르므로 고정된 열거형 대신 정수 식별자로 표현하며,
    /// 인스펙터에서 지정할 수 있도록 직렬화 가능한 값 형식으로 정의한다.
    /// </summary>
    [Serializable]
    public struct TeamId : IEquatable<TeamId>
    {
        [Tooltip("진영을 구분하는 값이다. 0은 진영이 지정되지 않았음을 뜻한다.")]
        [SerializeField]
        private int value;

        /// <summary>
        /// 지정한 값으로 진영 식별자를 생성한다.
        /// </summary>
        public TeamId(int value)
        {
            this.value = value;
        }

        /// <summary>
        /// 진영이 지정되지 않은 상태를 나타내는 식별자이다.
        /// </summary>
        public static TeamId None => default;

        /// <summary>
        /// 진영을 구분하는 값이다.
        /// </summary>
        public int Value => value;

        /// <summary>
        /// 진영이 실제로 지정되었는지 여부이다.
        /// </summary>
        public bool IsAssigned => value != 0;

        /// <summary>
        /// 두 식별자가 같은 진영을 가리키는지 비교한다.
        /// </summary>
        public static bool operator ==(TeamId left, TeamId right) => left.Equals(right);

        /// <summary>
        /// 두 식별자가 다른 진영을 가리키는지 비교한다.
        /// </summary>
        public static bool operator !=(TeamId left, TeamId right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(TeamId other) => value == other.value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TeamId other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => value;

        /// <inheritdoc />
        public override string ToString() => IsAssigned ? $"Team({value})" : "Team(None)";
    }
}
