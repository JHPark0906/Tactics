using System;
using System.Collections.Generic;
using HS.Tactics.Units;

namespace HS.Tactics.Progress
{
    /// <summary>
    /// 플레이어가 육성한 유닛 종류별 레벨과 경험치를 들고 있는 상태이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>개체가 아니라 종류의 레벨이다.</b> 같은 종류를 여러 기 배치해도 레벨은 하나이며,
    /// 전투에 나간 개체가 죽어도 그 종류의 레벨은 남는다. 육성은 전투 밖에서 쌓이는 것이기 때문이다.
    /// </para>
    /// <para>
    /// <b>보유 목록이 아니다.</b> 해금을 다루지 않으므로 "가진 유닛"과 "안 가진 유닛"을 나누지 않는다.
    /// 여기 없는 종류는 아직 키우지 않은 것일 뿐이고, 물으면 <see cref="StartingLevel"/>과
    /// <see cref="StartingExperience"/>로 답한다.
    /// </para>
    /// <para>
    /// <b>담지 않는 기준은 레벨만이 아니라 경험치까지다.</b> 레벨이 아직 1이어도 경험치가 쌓여 있으면
    /// 자란 것이므로 담는다. 레벨만 보고 버리면 <b>다음 레벨을 코앞에 둔 종류의 경험치가 저장에서 사라진다.</b>
    /// 그래서 새 유닛 종류가 생겨도 저장 데이터를 고칠 것이 없다.
    /// </para>
    /// <para>
    /// <b>적의 레벨은 여기 없다.</b> 저장하는 것은 플레이어가 육성하는 것이며, 적은 육성 대상이 아니다.
    /// 적도 레벨을 갖지만 그 값은 전투를 구성하는 쪽이 정하는 것이지 플레이어가 쌓은 것이 아니다.
    /// </para>
    /// </remarks>
    public sealed class UnitLevelProgress
    {
        /// <summary>아직 키우지 않은 종류가 갖는 레벨이다.</summary>
        public const int StartingLevel = 1;

        /// <summary>아직 아무것도 쌓지 않은 종류가 갖는 경험치이다.</summary>
        public const int StartingExperience = 0;

        /// <summary>종류 식별자별 육성 상태이다. 시작 상태인 종류는 담지 않는다.</summary>
        private readonly Dictionary<string, UnitGrowth> _growths = new();

        /// <summary>지금까지 담긴 것이 몇 번 바뀌었는지이며, 저장이 필요한지 판단하는 데 쓴다.</summary>
        public int Revision { get; private set; }

        /// <summary>시작 상태보다 자란 종류의 수이다.</summary>
        public int RaisedCount => _growths.Count;

        /// <summary>
        /// 그 종류의 레벨을 읽는다. 키운 적이 없으면 <see cref="StartingLevel"/>이다.
        /// </summary>
        /// <param name="definitionId">유닛 종류의 식별자이다.</param>
        /// <returns>그 종류의 레벨이며 항상 <see cref="StartingLevel"/> 이상이다.</returns>
        public int GetLevel(string definitionId) => Read(definitionId).Level;

        /// <summary>
        /// 그 종류에 쌓인 경험치를 읽는다. 쌓은 적이 없으면 <see cref="StartingExperience"/>이다.
        /// </summary>
        /// <param name="definitionId">유닛 종류의 식별자이다.</param>
        /// <returns>다음 레벨을 향해 쌓인 경험치이다.</returns>
        public int GetExperience(string definitionId) => Read(definitionId).Experience;

        /// <summary>
        /// 그 정의가 가리키는 종류에 쌓인 경험치를 읽는다.
        /// </summary>
        /// <param name="definition">경험치를 읽을 유닛 정의이며 null이면 시작 경험치이다.</param>
        /// <returns>쌓인 경험치이다.</returns>
        public int GetExperience(UnitDefinition definition)
            => definition != null ? GetExperience(definition.Id) : StartingExperience;

        /// <summary>담긴 육성 상태를 읽는다. 없으면 시작 상태이다.</summary>
        /// <param name="definitionId">유닛 종류의 식별자이다.</param>
        /// <returns>그 종류의 육성 상태이다.</returns>
        private UnitGrowth Read(string definitionId)
            => !string.IsNullOrWhiteSpace(definitionId) && _growths.TryGetValue(definitionId, out var growth)
                ? growth
                : UnitGrowth.Start;

        /// <summary>
        /// 그 정의가 가리키는 종류의 레벨을 읽는다.
        /// </summary>
        /// <param name="definition">레벨을 읽을 유닛 정의이며 null이면 시작 레벨이다.</param>
        /// <returns>그 종류의 레벨이다.</returns>
        public int GetLevel(UnitDefinition definition)
            => definition != null ? GetLevel(definition.Id) : StartingLevel;

        /// <summary>
        /// 그 종류의 레벨을 정한다.
        /// </summary>
        /// <remarks>
        /// 시작 레벨 아래로는 내려가지 않는다. 시작 레벨과 같아지면 담아 두지 않고 지운다 —
        /// 물었을 때 어차피 시작 레벨로 답하므로, 담아 두면 저장 파일만 커진다.
        /// </remarks>
        /// <param name="definitionId">유닛 종류의 식별자이며 비어 있으면 아무 일도 하지 않는다.</param>
        /// <param name="level">정할 레벨이다.</param>
        /// <returns>실제로 값이 달라졌으면 true이다.</returns>
        public bool SetLevel(string definitionId, int level)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return false;
            }

            var clamped = Math.Max(StartingLevel, level);
            if (GetLevel(definitionId) == clamped)
            {
                return false;
            }

            Write(definitionId, new UnitGrowth(clamped, Read(definitionId).Experience));
            return true;
        }

        /// <summary>
        /// 그 종류에 경험치를 더하고 오를 수 있는 만큼 레벨을 올린다.
        /// </summary>
        /// <remarks>
        /// 규칙 자체는 <see cref="UnitLevelUp"/>가 갖는다. 여기서는 담긴 것을 꺼내 넘기고 결과를 되담기만 한다.
        /// 규칙을 이 안에 두면 다른 입구가 생길 때마다 같은 규칙이 한 벌씩 복사된다.
        /// </remarks>
        /// <param name="definitionId">유닛 종류의 식별자이며 비어 있으면 아무 일도 하지 않는다.</param>
        /// <param name="amount">더할 경험치이다.</param>
        /// <param name="curve">레벨이 오르는 데 드는 경험치를 아는 곡선이다.</param>
        /// <returns>이번에 오른 레벨 수이며 오르지 않았으면 0이다.</returns>
        public int AddExperience(string definitionId, int amount, UnitLevelCurve curve)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                return 0;
            }

            var before = Read(definitionId);
            var after = UnitLevelUp.Apply(before.Level, before.Experience, amount, curve);
            var grown = new UnitGrowth(after.Level, after.Experience);
            if (!grown.Equals(before))
            {
                Write(definitionId, grown);
            }

            return after.GainedLevels;
        }

        /// <summary>
        /// 그 종류의 육성 상태를 통째로 정한다.
        /// </summary>
        /// <remarks>
        /// 저장에서 되돌릴 때 쓴다. 레벨과 경험치를 따로 넣으면 <b>둘 사이에 다른 것이 끼어들 자리가 생기고</b>,
        /// 되돌리는 도중의 상태를 다른 곳에서 읽을 수 있다.
        /// </remarks>
        /// <param name="definitionId">유닛 종류의 식별자이며 비어 있으면 아무 일도 하지 않는다.</param>
        /// <param name="growth">담을 육성 상태이다.</param>
        /// <returns>실제로 값이 달라졌으면 true이다.</returns>
        public bool SetGrowth(string definitionId, UnitGrowth growth)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || Read(definitionId).Equals(growth))
            {
                return false;
            }

            Write(definitionId, growth);
            return true;
        }

        /// <summary>
        /// 그 종류의 육성 상태를 읽는다. 키운 적이 없으면 시작 상태이다.
        /// </summary>
        /// <param name="definitionId">유닛 종류의 식별자이다.</param>
        /// <returns>그 종류의 육성 상태이다.</returns>
        public UnitGrowth GetGrowth(string definitionId) => Read(definitionId);

        /// <summary>
        /// 그 종류의 육성 상태를 담는다. 시작 상태이면 담지 않고 지운다.
        /// </summary>
        /// <param name="definitionId">유닛 종류의 식별자이다.</param>
        /// <param name="growth">담을 육성 상태이다.</param>
        private void Write(string definitionId, UnitGrowth growth)
        {
            if (growth.IsAtStart)
            {
                _growths.Remove(definitionId);
            }
            else
            {
                _growths[definitionId] = growth;
            }

            Revision++;
        }

        /// <summary>
        /// 시작 상태보다 자란 종류들을 식별자 차례로 훑는다.
        /// </summary>
        /// <remarks>
        /// 차례를 정해 두는 것은 저장한 내용이 실행할 때마다 달라지지 않게 하기 위해서이다.
        /// 사전이 담는 차례는 보장되지 않으므로, 그대로 적으면 바뀐 것이 없어도 파일이 달라 보인다.
        /// </remarks>
        /// <returns>식별자와 육성 상태의 짝을 식별자 오름차순으로 돌려준다.</returns>
        public IEnumerable<KeyValuePair<string, UnitGrowth>> EnumerateRaised()
        {
            var identifiers = new List<string>(_growths.Keys);
            identifiers.Sort(StringComparer.Ordinal);
            foreach (var identifier in identifiers)
            {
                yield return new KeyValuePair<string, UnitGrowth>(identifier, _growths[identifier]);
            }
        }

        /// <summary>담긴 것을 모두 지워 아무것도 키우지 않은 상태로 되돌린다.</summary>
        public void Clear()
        {
            if (_growths.Count == 0)
            {
                return;
            }

            _growths.Clear();
            Revision++;
        }
    }
}
