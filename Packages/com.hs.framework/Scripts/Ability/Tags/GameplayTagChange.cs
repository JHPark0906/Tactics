namespace HS.Framework.Ability.Tags
{
    /// <summary>
    /// 태그 컨테이너에서 일어난 변화의 종류이다.
    /// </summary>
    /// <remarks>
    /// 참조 계수가 오르내리는 것 자체는 알리지 않고, 태그를 실제로 얻거나 잃는 순간만 알린다.
    /// 관찰하는 쪽이 관심 있는 것은 "지금 이 태그를 가지고 있는가"이지 몇 번 부여됐는지가 아니기 때문이다.
    /// </remarks>
    public enum GameplayTagChangeKind
    {
        /// <summary>없던 태그를 새로 얻었다. 참조 계수가 0에서 1 이상으로 올랐을 때이다.</summary>
        Gained = 0,

        /// <summary>가지고 있던 태그를 모두 잃었다. 참조 계수가 0으로 떨어졌을 때이다.</summary>
        Lost = 1
    }

    /// <summary>
    /// 태그 컨테이너의 변화를 알리는 값이다.
    /// </summary>
    public readonly struct GameplayTagChange
    {
        /// <summary>변화가 일어난 태그이다.</summary>
        public GameplayTag Tag { get; }

        /// <summary>일어난 변화의 종류이다.</summary>
        public GameplayTagChangeKind ChangeKind { get; }

        /// <summary>변화 직후의 참조 계수이며, 태그를 잃었으면 0이다.</summary>
        public int Count { get; }

        /// <summary>태그 변화 알림을 생성한다.</summary>
        /// <param name="tag">변화가 일어난 태그이다.</param>
        /// <param name="changeKind">일어난 변화의 종류이다.</param>
        /// <param name="count">변화 직후의 참조 계수이다.</param>
        public GameplayTagChange(GameplayTag tag, GameplayTagChangeKind changeKind, int count)
        {
            Tag = tag;
            ChangeKind = changeKind;
            Count = count;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Tag} {ChangeKind} (count: {Count})";
        }
    }
}
