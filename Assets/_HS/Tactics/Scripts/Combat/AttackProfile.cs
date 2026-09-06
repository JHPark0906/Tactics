using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 한 번의 사격에 필요한 수치를 모아 전달하는 값이다.
    /// </summary>
    /// <remarks>
    /// 수치는 모두 <see cref="UnitDefinition"/>에서 온 임시값이다.
    /// </remarks>
    public readonly struct AttackProfile
    {
        /// <summary>사격 수치를 지정해 프로필을 만든다.</summary>
        /// <param name="damage">명중했을 때 적용할 피해량이다.</param>
        /// <param name="range">사격이 닿는 최대 거리(미터)이다.</param>
        /// <param name="interval">사격과 다음 사격 사이의 간격(초)이다.</param>
        /// <param name="baseHitChance">엄폐가 없을 때의 기본 명중률(0~1)이다.</param>
        public AttackProfile(int damage, float range, float interval, float baseHitChance)
        {
            Damage = Mathf.Max(1, damage);
            Range = Mathf.Max(0f, range);
            Interval = Mathf.Max(0f, interval);
            BaseHitChance = Mathf.Clamp01(baseHitChance);
        }

        /// <summary>명중했을 때 적용할 피해량이다.</summary>
        public int Damage { get; }

        /// <summary>사격이 닿는 최대 거리(미터)이다.</summary>
        public float Range { get; }

        /// <summary>사격과 다음 사격 사이의 간격(초)이다.</summary>
        public float Interval { get; }

        /// <summary>기본 명중률(0~1)이다.</summary>
        public float BaseHitChance { get; }
    }
}
