using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Tactics.Foundation.Simulation;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>대상이 사거리 안에 있는 동안 사격 간격마다 명중과 피해를 판정한다.</summary>
    /// <remarks>
    /// <para>한 번의 활성화는 한 교전이다. 대기 중에도 활성 상태를 유지해 하위 이동 분기로 넘어가지 않는다.
    /// 대상이 사라지거나 죽거나 사거리 밖으로 나가면 종료한다.</para>
    /// <para>명중 규칙은 HitChanceCalculator가 담당한다. 사격 간격은 지속 효과로 표현하고,
    /// 어빌리티가 활성 상태일 때만 흐르는 타이머나 재활성화를 막는 쿨다운 슬롯을 사용하지 않는다.
    /// 어빌리티와 효과에는 각각 시간을 공급해야 하며, Tactics는 두 시간을 고정 스텝에서 진행한다.</para>
    /// <para>피해 효과와 대상의 효과 실행기가 있으면 피해를 효과로 전달한다. 그렇지 않으면 IDamageable을 사용한다.
    /// 공격력 어트리뷰트가 연결되어 있으면 그 값을 우선하고, 없으면 유닛 정의의 공격력을 사용한다.</para>
    /// </remarks>
    public sealed class AttackAbility : GameplayAbility
    {
        /// <summary>사격 간격이 흐르는 동안 소유자에게 부여되는 태그 이름이다.</summary>
        public const string IntervalTagName = "Cooldown.Attack";

        /// <summary>사격 간격 태그이며 이름에서 한 번만 해석한다.</summary>
        private static readonly GameplayTag IntervalTag = GameplayTag.Parse(IntervalTagName);

        /// <summary>사격에 사용할 수치이며 활성화할 때 유닛 정의에서 읽는다.</summary>
        private AttackProfile _profile;

        /// <summary>대상을 알려 주는 공급자이다.</summary>
        private ICombatTargetSource _targetSource;

        /// <summary>거리를 재는 기준이 되는 공격자의 Transform이다.</summary>
        private Transform _selfTransform;

        /// <summary>피해를 발생시킨 주체로 기록할 GameObject이다.</summary>
        private GameObject _instigator;

        /// <summary>구성요소를 다시 찾지 않도록 기억해 둔 대상이다.</summary>
        private Transform _cachedTarget;

        /// <summary>기억해 둔 대상의 피해 적용 창구이다.</summary>
        private IDamageable _cachedDamageable;

        /// <summary>기억해 둔 대상의 엄폐 상태이다.</summary>
        private IUnitCoverState _cachedTargetCover;

        /// <summary>기억해 둔 대상의 효과 실행기 컴포넌트이며, 없으면 null이다.</summary>
        private GameplayEffectComponent _cachedTargetEffects;

        /// <summary>사격 간격을 나타내는 지속 효과 정의이며 간격을 알게 될 때 만들어진다.</summary>
        private GameplayEffectDefinition _intervalEffect;

        /// <summary>지금 들고 있는 간격 효과가 몇 초로 만들어졌는지이며, 간격이 바뀌면 다시 만든다.</summary>
        private float _intervalEffectSeconds;

        /// <summary>마지막 사격에서 명중 판정에 사용한 명중률이며, 아직 쏘지 않았으면 0이다.</summary>
        public float LastHitChance { get; private set; }

        /// <summary>마지막 사격이 명중했는지 여부이다.</summary>
        public bool LastShotHit { get; private set; }

        /// <summary>이번 활성화에서 쏜 횟수이다.</summary>
        public int ShotCount { get; private set; }

        /// <inheritdoc />
        /// <remarks>사격할 대상이 지금 있는지 확인한다. 없으면 트리가 다른 분기로 넘어가야 하므로 거부한다.</remarks>
        public override bool CanActivate()
        {
            return TryResolveOwner() && TryGetValidTarget(out _);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 사격 간격은 일부러 건드리지 않는다. 활성화할 때마다 간격을 지우면
        /// 상위 분기가 가로챘다 돌아올 때마다 즉시 다시 쏠 수 있어 사격 속도 제한이 무의미해진다.
        /// 간격 태그가 아직 없는 첫 활성화에서는 그 자리에서 한 발 쏜다.
        /// </remarks>
        protected override void OnActivate()
        {
            ShotCount = 0;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 대상이 아직 사격할 만한지 매 틱 다시 확인하고, 아니면 교전을 끝낸다.
        /// 이어지는 동안에는 간격 태그가 걷힐 때까지 기다렸다가 한 발 쏘고 계속 진행한다.
        /// 흐른 시간을 쓰지 않는 것은 간격을 여기서 세지 않기 때문이며, 그 시간은 효과 실행기가 따로 흘린다.
        /// </remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            if (!TryGetValidTarget(out var target))
            {
                return GameplayAbilityTickResult.Finished;
            }

            if (System.Tags.HasTag(IntervalTag))
            {
                return GameplayAbilityTickResult.Running;
            }

            Fire(target);
            StartInterval();
            return GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        /// <remarks>대상 캐시만 비운다. 이 어빌리티는 바깥 자원을 잡지 않으므로 놓을 것이 없다.</remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            _cachedTarget = null;
            _cachedDamageable = null;
            _cachedTargetCover = null;
            _cachedTargetEffects = null;
        }

        /// <summary>
        /// 소유 액터에서 사격에 필요한 구성요소와 수치를 찾는다.
        /// </summary>
        /// <returns>사격할 수 있는 구성이 갖춰져 있으면 true이다.</returns>
        private bool TryResolveOwner()
        {
            var owner = System != null ? System.Owner : null;
            if (owner == null)
            {
                return false;
            }

            if (_targetSource == null || _selfTransform == null)
            {
                _targetSource = owner.GetComponent<ICombatTargetSource>();
                _selfTransform = owner.transform;
                _instigator = owner;
            }

            if (_targetSource == null)
            {
                return false;
            }

            var unit = owner.GetComponent<TacticalUnit>();
            var definition = unit != null ? unit.Definition : null;
            if (definition == null)
            {
                return false;
            }

            _profile = new AttackProfile(
                definition.AttackDamage,
                definition.AttackRange,
                definition.AttackInterval,
                definition.BaseHitChance);
            return true;
        }

        /// <summary>
        /// 지금 사격할 수 있는 대상을 찾는다. 살아 있고 사거리 안에 있어야 한다.
        /// </summary>
        /// <param name="target">찾은 대상이며 없으면 null이다.</param>
        /// <returns>사격할 대상이 있으면 true이다.</returns>
        private bool TryGetValidTarget(out Transform target)
        {
            target = null;
            if (!TryResolveOwner())
            {
                return false;
            }

            var candidate = _targetSource.CurrentTarget;
            if (candidate == null)
            {
                return false;
            }

            ResolveTargetComponents(candidate);
            if (_cachedDamageable == null || _cachedDamageable.IsDead)
            {
                return false;
            }

            if (Vector3.Distance(_selfTransform.position, candidate.position) > _profile.Range)
            {
                return false;
            }

            target = candidate;
            return true;
        }

        /// <summary>
        /// 명중을 판정하고 명중했으면 피해를 준다.
        /// </summary>
        /// <param name="target">사격할 대상이다.</param>
        private void Fire(Transform target)
        {
            LastHitChance = _profile.BaseHitChance;
            // 굴림은 Unity 난수다. 전투를 다시 재현하는 것은 이 프로젝트가 보장하지 않으므로 난수원을 따로 두지 않는다.
            LastShotHit = HitChanceCalculator.Roll(LastHitChance);
            ShotCount++;
            if (!LastShotHit)
            {
                return;
            }

            var damage = ResolveDamage();
            var damageEffect = Definition is AttackAbilityDefinition attackDefinition ? attackDefinition.DamageEffect : null;
            if (damageEffect != null && _cachedTargetEffects != null)
            {
                var context = new GameplayEffectContext(_instigator, this, System.Attributes)
                    .SetByCaller(DamageEffectDefinition.DamageTagName, damage);
                _cachedTargetEffects.ApplyEffect(damageEffect, this, context);
                return;
            }

            _cachedDamageable.ApplyDamage(damage, _instigator);
        }

        /// <summary>
        /// 이번 사격의 피해량을 정한다. 정의가 가리키는 공격력 어트리뷰트를 소유자가 가지고 있으면 거기서 읽고,
        /// 아니면 유닛 정의의 공격력을 쓴다.
        /// </summary>
        /// <returns>피해량이며 항상 1 이상이다.</returns>
        private int ResolveDamage()
        {
            var attackPower = Definition is AttackAbilityDefinition attackDefinition ? attackDefinition.AttackPowerAttribute : null;
            if (attackPower != null && System.Attributes.TryGetCurrentValue(attackPower, out var attackPowerValue))
            {
                return Mathf.Max(1, AttributeValue.ToInt(attackPowerValue));
            }

            return _profile.Damage;
        }

        /// <summary>
        /// 다음 사격까지 기다리도록 간격 효과를 건다.
        /// </summary>
        /// <remarks>
        /// 효과를 거는 출처를 이 어빌리티로 두지만, 어빌리티가 끝나도 효과는 남는다.
        /// <see cref="GameplayAbilitySystem.EndAbility"/>는 출처가 어빌리티인 효과를 거두지 않으므로
        /// 가로채여 교전이 끝나도 간격은 계속 흐른다. 그것이 이 방식을 고른 이유이다.
        /// </remarks>
        private void StartInterval()
        {
            EnsureIntervalEffect();
            System.Effects.Apply(_intervalEffect, this);
        }

        /// <summary>
        /// 지금 간격에 맞는 효과 정의를 갖춘다. 간격이 바뀌었으면 다시 만든다.
        /// </summary>
        private void EnsureIntervalEffect()
        {
            if (_intervalEffect != null && Mathf.Approximately(_intervalEffectSeconds, _profile.Interval))
            {
                return;
            }

            // 앞서 만든 정의를 파괴하지는 않는다. 유닛 정의의 간격은 실행 중에 바뀌지 않아 이 자리는 거의 지나지 않고,
            // 에디터에서 ScriptableObject를 Destroy하면 오류가 남아 테스트가 그것으로 실패한다.
            _intervalEffectSeconds = _profile.Interval;
            _intervalEffect = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                duration: _intervalEffectSeconds,
                grantedTags: new[] { IntervalTagName });
            _intervalEffect.name = "AttackInterval";
        }

        /// <summary>대상이 바뀌었을 때만 대상의 체력과 엄폐 구성요소를 다시 찾는다.</summary>
        /// <param name="target">현재 대상의 Transform이다.</param>
        private void ResolveTargetComponents(Transform target)
        {
            if (_cachedTarget == target)
            {
                return;
            }

            _cachedTarget = target;
            _cachedDamageable = target.GetComponentInParent<IDamageable>();
            _cachedTargetCover = target.GetComponentInParent<IUnitCoverState>();
            _cachedTargetEffects = target.GetComponentInParent<GameplayEffectComponent>();
        }
    }
}
