using System.Collections.Generic;
using R3;

namespace HS.Framework.Ability.Tags
{
    /// <summary>
    /// 부여된 횟수를 세어 가며 게임플레이 태그를 보관하고, 계층 질의와 변화 알림을 제공한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>참조 계수가 이 컨테이너의 핵심이다.</b> 같은 태그를 여러 곳에서 부여할 수 있으므로 부여한 횟수를 세고,
    /// 그만큼 회수되어야 태그가 사라진다. 예를 들어 둔화 효과 둘이 각각 같은 태그를 부여했다면
    /// 하나가 만료되어도 다른 하나가 남아 있는 동안에는 태그가 유지된다.
    /// 이 규칙이 없으면 먼저 끝난 효과가 아직 살아 있는 효과의 상태까지 지워 버린다.
    /// </para>
    /// <para>
    /// <b>계층 질의.</b> <see cref="HasTag"/>는 계층을 따라 판정하므로 상위 이름 하나로 계열 전체를 물을 수 있다.
    /// 정확히 그 태그만 확인하려면 <see cref="HasTagExact"/>를 쓴다.
    /// </para>
    /// <para>
    /// <b>변화 알림.</b> 프레임워크의 기존 관례대로 R3 스트림으로 알린다. 내부 Subject를 그대로 노출하지 않으므로
    /// 바깥에서 알림을 위조할 수 없다. 태그를 얻거나 잃는 순간만 알리며 참조 계수의 오르내림은 알리지 않는다.
    /// </para>
    /// <para>
    /// <b>단독으로 쓸 수 있다.</b> 이 컨테이너는 어빌리티나 효과를 전제하지 않으므로,
    /// 어빌리티 시스템 없이 상태 표식만 필요한 게임에서도 그대로 쓸 수 있다.
    /// </para>
    /// <para>
    /// 내부 상태를 잠금 없이 관리하므로 메인 스레드에서만 사용해야 한다.
    /// </para>
    /// </remarks>
    public sealed class GameplayTagContainer
    {
        /// <summary>보관 중인 태그와 그 태그가 부여된 횟수이다.</summary>
        private readonly Dictionary<GameplayTag, int> _tagCounts = new();

        /// <summary>변화를 알리는 내부 스트림이다.</summary>
        private readonly Subject<GameplayTagChange> _changed = new();

        /// <summary>내부 Subject를 감춘 읽기 전용 스트림이며 구독마다 다시 만들지 않도록 보관한다.</summary>
        private readonly Observable<GameplayTagChange> _changedObservable;

        /// <summary>빈 태그 컨테이너를 생성한다.</summary>
        public GameplayTagContainer()
        {
            _changedObservable = _changed.AsObservable();
        }

        /// <summary>태그를 얻거나 잃을 때마다 알리는 스트림이다.</summary>
        public Observable<GameplayTagChange> Changed => _changedObservable;

        /// <summary>지금 보관 중인 서로 다른 태그의 수이다.</summary>
        public int DistinctTagCount => _tagCounts.Count;

        /// <summary>지금 보관 중인 태그를 열거한다. 순서는 보장하지 않는다.</summary>
        public IEnumerable<GameplayTag> Tags => _tagCounts.Keys;

        /// <summary>
        /// 태그를 부여하고 참조 계수를 올린다.
        /// </summary>
        /// <param name="tag">부여할 태그이며 유효하지 않으면 아무 일도 하지 않는다.</param>
        /// <param name="count">부여할 횟수이며 1 미만이면 아무 일도 하지 않는다.</param>
        /// <returns>부여한 뒤의 참조 계수이며, 아무 일도 하지 않았으면 기존 계수이다.</returns>
        public int AddTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count < 1)
            {
                return GetCount(tag);
            }

            var hadTag = _tagCounts.TryGetValue(tag, out var previousCount);
            var newCount = previousCount + count;
            _tagCounts[tag] = newCount;

            if (!hadTag)
            {
                _changed.OnNext(new GameplayTagChange(tag, GameplayTagChangeKind.Gained, newCount));
            }

            return newCount;
        }

        /// <summary>
        /// 태그를 회수하고 참조 계수를 내린다. 계수가 0이 되면 태그를 잃는다.
        /// </summary>
        /// <param name="tag">회수할 태그이며 보관 중이 아니면 아무 일도 하지 않는다.</param>
        /// <param name="count">회수할 횟수이며 1 미만이면 아무 일도 하지 않는다.</param>
        /// <returns>회수한 뒤의 참조 계수이며, 태그를 잃었으면 0이다.</returns>
        public int RemoveTag(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count < 1 || !_tagCounts.TryGetValue(tag, out var previousCount))
            {
                return GetCount(tag);
            }

            var newCount = previousCount - count;
            if (newCount > 0)
            {
                _tagCounts[tag] = newCount;
                return newCount;
            }

            _tagCounts.Remove(tag);
            _changed.OnNext(new GameplayTagChange(tag, GameplayTagChangeKind.Lost, 0));
            return 0;
        }

        /// <summary>
        /// 남은 참조 계수와 상관없이 태그를 한 번에 없앤다.
        /// 부여한 쪽이 사라져 회수를 기대할 수 없을 때 쓰는 마지막 수단이며,
        /// 평소에는 부여한 만큼 <see cref="RemoveTag"/>로 되돌리는 편이 안전하다.
        /// </summary>
        /// <param name="tag">없앨 태그이다.</param>
        /// <returns>실제로 없앴으면 true이며, 보관 중이 아니었으면 false이다.</returns>
        public bool RemoveTagCompletely(GameplayTag tag)
        {
            if (!tag.IsValid || !_tagCounts.Remove(tag))
            {
                return false;
            }

            _changed.OnNext(new GameplayTagChange(tag, GameplayTagChangeKind.Lost, 0));
            return true;
        }

        /// <summary>
        /// 지정한 태그가 부여된 횟수를 가져온다.
        /// </summary>
        /// <param name="tag">확인할 태그이다.</param>
        /// <returns>부여된 횟수이며 보관 중이 아니면 0이다.</returns>
        public int GetCount(GameplayTag tag)
        {
            return tag.IsValid && _tagCounts.TryGetValue(tag, out var count) ? count : 0;
        }

        /// <summary>
        /// 지정한 태그이거나 그 하위 태그를 하나라도 보관 중인지 확인한다.
        /// </summary>
        /// <param name="tag">확인할 태그이며 보통 계열을 대표하는 상위 이름이다.</param>
        /// <returns>일치하는 태그를 보관 중이면 true이다.</returns>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            // 정확히 같은 이름이 흔한 경우이므로 사전 조회를 먼저 시도하고, 없을 때만 계층을 훑는다.
            if (_tagCounts.ContainsKey(tag))
            {
                return true;
            }

            foreach (var heldTag in _tagCounts.Keys)
            {
                if (heldTag.Matches(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 지정한 태그를 정확히 그 이름으로 보관 중인지 확인한다. 하위 태그는 세지 않는다.
        /// </summary>
        /// <param name="tag">확인할 태그이다.</param>
        /// <returns>같은 이름의 태그를 보관 중이면 true이다.</returns>
        public bool HasTagExact(GameplayTag tag)
        {
            return tag.IsValid && _tagCounts.ContainsKey(tag);
        }

        /// <summary>
        /// 주어진 태그 가운데 하나라도 보관 중인지 확인한다. 계층 일치를 따른다.
        /// </summary>
        /// <param name="tags">확인할 태그 목록이며 비어 있으면 false이다.</param>
        /// <returns>하나라도 일치하면 true이다.</returns>
        public bool HasAny(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                if (HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 주어진 태그를 모두 보관 중인지 확인한다. 계층 일치를 따르며 빈 목록은 참으로 본다.
        /// </summary>
        /// <param name="tags">확인할 태그 목록이다.</param>
        /// <returns>모두 일치하면 true이다.</returns>
        public bool HasAll(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                if (!HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 보관 중인 태그를 모두 없애고 각각에 대해 잃음을 알린다.
        /// </summary>
        public void Clear()
        {
            if (_tagCounts.Count == 0)
            {
                return;
            }

            // 알림을 받은 쪽이 컨테이너를 다시 건드려도 안전하도록 목록을 먼저 확정한 뒤 비운다.
            var removedTags = new List<GameplayTag>(_tagCounts.Keys);
            _tagCounts.Clear();
            foreach (var removedTag in removedTags)
            {
                _changed.OnNext(new GameplayTagChange(removedTag, GameplayTagChangeKind.Lost, 0));
            }
        }
    }
}
