using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Tags;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 효과를 적용하는 순간의 사정을 담는다. 누가 걸었는지, 어느 어트리뷰트에서 크기를 읽을지, 호출자가 정한 크기가 무엇인지이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>효과 정의는 데이터이고 크기는 그 순간의 사정에 달린 경우가 있다.</b> 같은 피해 효과라도 쏜 유닛의 공격력에 따라,
    /// 또는 명중 판정 뒤 호출자가 정한 값에 따라 크기가 다르다. 정의에 숫자를 박으면 그런 효과를 표현할 수 없으므로,
    /// 적용하는 쪽이 이 값을 함께 넘기고 수정자가 <see cref="GameplayEffectMagnitudeSource"/>에 따라 여기서 크기를 읽는다.
    /// </para>
    /// <para>
    /// <b>가해자는 어트리뷰트 변화까지 따라간다.</b> 이 효과가 기본값을 바꿀 때 <see cref="Instigator"/>가
    /// <see cref="AttributeChangeContext"/>에 실려 필터와 변화 알림에 닿는다. 사망 알림이 누가 쓰러뜨렸는지 아는 길이 이것이다.
    /// </para>
    /// <para>
    /// 넘기지 않아도 된다. 숫자로 고정된 효과는 사정을 필요로 하지 않으며, 그때는 null로 두면 된다.
    /// </para>
    /// </remarks>
    public sealed class GameplayEffectContext
    {
        /// <summary>호출자가 정한 크기이며 태그로 구분한다.</summary>
        private Dictionary<GameplayTag, float> _setByCaller;

        /// <summary>효과 적용의 사정을 생성한다.</summary>
        /// <param name="instigator">이 효과를 일으킨 액터이며 알 수 없으면 null이다.</param>
        /// <param name="causer">이 효과를 건 것이며 보통 어빌리티이다. 없으면 null이다.</param>
        /// <param name="sourceAttributes">크기를 읽을 원천 어트리뷰트 집합이며, 보통 가해자의 것이다. 없으면 null이다.</param>
        public GameplayEffectContext(
            GameObject instigator = null,
            object causer = null,
            AttributeSet sourceAttributes = null)
        {
            Instigator = instigator;
            Causer = causer;
            SourceAttributes = sourceAttributes;
        }

        /// <summary>이 효과를 일으킨 액터이며 알 수 없으면 null이다.</summary>
        public GameObject Instigator { get; }

        /// <summary>이 효과를 건 것이며 없으면 null이다.</summary>
        public object Causer { get; }

        /// <summary>원천 어트리뷰트 집합이며 없으면 null이다. 원천 어트리뷰트 크기가 여기서 읽는다.</summary>
        public AttributeSet SourceAttributes { get; }

        /// <summary>호출자가 정한 크기의 수이다.</summary>
        public int SetByCallerCount => _setByCaller?.Count ?? 0;

        /// <summary>
        /// 호출자가 정한 크기를 태그로 등록한다. 같은 태그를 다시 등록하면 덮어쓴다.
        /// </summary>
        /// <param name="tag">크기를 구분하는 태그이다.</param>
        /// <param name="value">크기이다.</param>
        /// <returns>이어서 등록할 수 있도록 자기 자신을 돌려준다.</returns>
        public GameplayEffectContext SetByCaller(GameplayTag tag, float value)
        {
            if (!tag.IsValid)
            {
                return this;
            }

            _setByCaller ??= new Dictionary<GameplayTag, float>();
            _setByCaller[tag] = value;
            return this;
        }

        /// <summary>
        /// 호출자가 정한 크기를 태그 이름으로 등록한다. 이름의 형식이 잘못되었으면 등록하지 않는다.
        /// </summary>
        /// <param name="tagName">크기를 구분하는 태그 이름이다.</param>
        /// <param name="value">크기이다.</param>
        /// <returns>이어서 등록할 수 있도록 자기 자신을 돌려준다.</returns>
        public GameplayEffectContext SetByCaller(string tagName, float value)
        {
            return GameplayTag.TryParse(tagName, out var tag) ? SetByCaller(tag, value) : this;
        }

        /// <summary>호출자가 정한 크기를 찾는다.</summary>
        /// <param name="tag">크기를 구분하는 태그이다.</param>
        /// <param name="value">찾은 크기이며 없으면 0이다.</param>
        /// <returns>등록되어 있으면 true이다.</returns>
        public bool TryGetSetByCaller(GameplayTag tag, out float value)
        {
            if (_setByCaller != null && tag.IsValid && _setByCaller.TryGetValue(tag, out value))
            {
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>이 사정을 어트리뷰트 변화의 원인으로 옮긴다.</summary>
        /// <param name="fallbackCause">건 것을 밝히지 않았을 때 대신 쓸 원인이다.</param>
        /// <returns>어트리뷰트 변화에 실을 원인이다.</returns>
        public AttributeChangeContext ToAttributeChangeContext(object fallbackCause)
        {
            return new AttributeChangeContext(Causer ?? fallbackCause, Instigator);
        }
    }
}
