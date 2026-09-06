using UnityEngine;

namespace HS.Framework.Gameplay.Health
{
    /// <summary>
    /// 피해를 받아 체력이 감소하고 사망 상태가 될 수 있는 대상을 정의한다.
    /// 명중률, 방어력, 엄폐 보정 등 피해 수치를 계산하는 규칙은 게임 레이어의 책임이며,
    /// 이 계약은 이미 계산된 피해량을 적용하는 것까지만 담당한다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 현재 남은 체력이다. 항상 0 이상 최대 체력 이하이다.
        /// </summary>
        int CurrentHealth { get; }

        /// <summary>
        /// 최대 체력이다. 항상 1 이상이다.
        /// </summary>
        int MaxHealth { get; }

        /// <summary>
        /// 체력이 0에 도달해 사망한 상태인지 여부이다.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// 이미 계산된 피해량을 적용한다.
        /// 피해량이 0 이하이거나 이미 사망한 상태이면 아무 변화도 일어나지 않는다.
        /// </summary>
        /// <param name="amount">적용할 피해량이다.</param>
        /// <param name="instigator">피해를 발생시킨 GameObject이며, 알 수 없으면 null을 전달한다.</param>
        void ApplyDamage(int amount, GameObject instigator);
    }

    /// <summary>
    /// 피해가 실제로 적용된 사실을 알린다.
    /// </summary>
    public readonly struct DamageAppliedEvent
    {
        /// <summary>
        /// 피해를 받은 GameObject를 가져온다.
        /// </summary>
        public GameObject Target { get; }

        /// <summary>
        /// 피해를 발생시킨 GameObject를 가져오며, 알 수 없으면 null이다.
        /// </summary>
        public GameObject Instigator { get; }

        /// <summary>
        /// 남은 체력을 넘지 않도록 보정된 뒤 실제로 적용된 피해량을 가져온다.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 피해가 적용된 뒤 남은 체력을 가져온다.
        /// </summary>
        public int RemainingHealth { get; }

        /// <summary>
        /// 피해 적용 이벤트를 생성한다.
        /// </summary>
        public DamageAppliedEvent(GameObject target, GameObject instigator, int amount, int remainingHealth)
        {
            Target = target;
            Instigator = instigator;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }

    /// <summary>
    /// 체력이 0에 도달해 사망한 사실을 알린다.
    /// </summary>
    public readonly struct DeathEvent
    {
        /// <summary>
        /// 사망한 GameObject를 가져온다.
        /// </summary>
        public GameObject Target { get; }

        /// <summary>
        /// 사망을 유발한 GameObject를 가져오며, 알 수 없으면 null이다.
        /// </summary>
        public GameObject Instigator { get; }

        /// <summary>
        /// 사망 이벤트를 생성한다.
        /// </summary>
        public DeathEvent(GameObject target, GameObject instigator)
        {
            Target = target;
            Instigator = instigator;
        }
    }
}
