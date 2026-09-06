using System;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.BehaviourTree;
using HS.Tactics.Behaviour;
using HS.Tactics.Units;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Editor
{
    /// <summary>유닛 행동 트리 에셋을 여기 적힌 모양으로 다시 채우는 메뉴이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>정본은 에셋 파일이다.</b> 이 메뉴는 그 파일을 새로 만들지 않고 안을 다시 채운다. 새로 만들면
    /// GUID가 바뀌어 그 에셋을 가리키던 프리팹이 끊어진다. 파일이 아직 없을 때만 만든다.
    /// </para>
    /// <para>
    /// 모양이 코드에 적혀 있으므로 편집기를 열지 않아도 에셋이 무엇을 담는지 읽을 수 있다. 값은
    /// 편집기가 하는 것과 같은 길(<see cref="SerializedObject"/>)로 채우므로 설명의 필드가 비공개라도
    /// 통로를 따로 내지 않는다.
    /// </para>
    /// <para>
    /// 트리 모양. 적을 찾고 사거리를 재는 두 서비스가 전체를 감싼다. 대상이 있으면 사거리 안일 때
    /// 사격 어빌리티를, 아니면 자리를 나눠 추격한다. 대상이 없으면 전진한다. 두 조건은 뒤엣 형제를
    /// 끊으므로 전진 중에 대상이 나타나면 전진이 끊기고, 추격 중에 사거리 안에 들면 추격이 끊긴다.
    /// </para>
    /// <para>
    /// <b>사격과 추격 사이에 엄폐 유지·확보가 낀다.</b> 사거리 밖이면 자리를 이미 지키고 있는지
    /// (유지), 없으면 새로 잡을 수 있는지(확보)를 차례로 본다. 둘 다 못하면 그제야 추격한다. 두 어빌리티
    /// 모두 각자의 <c>CanActivate</c>가 스스로 거르므로 이 자리에 별도의 조건 노드를 두지 않는다 —
    /// 활성화가 실패하면 같은 틱 안에서 다음 형제로 넘어간다. 유지는 언제나 사격보다 아래에 둔다;
    /// 사격이 도는 동안에는 유지를 다시 재지 않아도 자리를 지키던 조건(사거리 안)이 이미 사격 자체로
    /// 보장되기 때문이다.
    /// </para>
    /// </remarks>
    public static class UnitBehaviourTreeAssetBuilder
    {
        /// <summary>정본 에셋의 경로이다.</summary>
        public const string AssetPath = "Assets/_HS/Tactics/AI/UnitBehaviourTree.asset";

        /// <summary>정본 에셋을 여기 적힌 모양으로 다시 채운다. 없으면 만든다.</summary>
        [MenuItem("Tools/HS Tactics/AI/Rebuild Unit Behaviour Tree")]
        public static void Rebuild()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            Fill(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UnitBehaviourTreeAssetBuilder] {AssetPath}를 다시 채웠다. 자리 {asset.NodeCount}개.", asset);
        }

        /// <summary>에셋의 자리와 부모 번호를 모양대로 채운다.</summary>
        /// <param name="asset">채울 에셋이다.</param>
        private static void Fill(BehaviourTreeAsset asset)
        {
            var serialized = new SerializedObject(asset);
            var slots = new Slots(serialized.FindProperty("nodes"), serialized.FindProperty("parents"));

            var detectEnemy = slots.Add(new DetectEnemyServiceDefinition(), BehaviourTreeAsset.NoParent);
            var targetInRange = slots.Add(new TargetInAttackRangeServiceDefinition(), detectEnemy);
            var choose = slots.Add(new SelectorDefinition(), targetInRange);

            var hasTarget = slots.Add(new ContextValueConditionDefinition(), choose,
                ("key", UnitBehaviourKeys.Target), ("abortScope", BehaviourAbortScope.LowerPriority));
            var engage = slots.Add(new SelectorDefinition(), hasTarget);
            var inRange = slots.Add(new ContextValueConditionDefinition(), engage,
                ("key", UnitBehaviourKeys.TargetInRange), ("abortScope", BehaviourAbortScope.LowerPriority));
            slots.Add(new ActivateAbilityDefinition(), inRange, ("abilityTagName", UnitAbilityTags.Attack));
            slots.Add(new ActivateAbilityDefinition(), engage, ("abilityTagName", UnitAbilityTags.MaintainCover));
            slots.Add(new ActivateAbilityDefinition(), engage, ("abilityTagName", UnitAbilityTags.TakeCover));
            slots.Add(new SpreadChaseTargetDefinition(), engage);

            var advanceDirection = slots.Add(new RefreshAdvanceDirectionDefinition(), choose);
            var advance = slots.Add(new SequenceDefinition(), advanceDirection);
            slots.Add(new StepAlongDirectionDefinition(), advance,
                ("destinationKey", UnitBehaviourKeys.Destination), ("directionKey", UnitBehaviourKeys.AdvanceDirection));
            slots.Add(new MoveToPositionDefinition(), advance, ("destinationKey", UnitBehaviourKeys.Destination));

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>자리 목록과 부모 목록을 함께 늘리는 손이다.</summary>
        private readonly struct Slots
        {
            private readonly SerializedProperty _nodes;
            private readonly SerializedProperty _parents;

            public Slots(SerializedProperty nodes, SerializedProperty parents)
            {
                _nodes = nodes ?? throw new InvalidOperationException("BehaviourTreeAsset에 'nodes' 필드가 없다.");
                _parents = parents ?? throw new InvalidOperationException("BehaviourTreeAsset에 'parents' 필드가 없다.");
                _nodes.ClearArray();
                _parents.ClearArray();
            }

            /// <summary>자리를 하나 더하고 그 번호를 돌려준다.</summary>
            /// <param name="definition">더할 설명이다.</param>
            /// <param name="parent">부모 자리의 번호이며, 뿌리면 <see cref="BehaviourTreeAsset.NoParent"/>이다.</param>
            /// <param name="fields">설명의 직렬화 필드에 넣을 값들이다. 기본값과 다른 것만 적는다.</param>
            /// <returns>더한 자리의 번호이다.</returns>
            public int Add(BehaviourNodeDefinition definition, int parent, params (string Field, object Value)[] fields)
            {
                var index = _nodes.arraySize;
                _nodes.arraySize = index + 1;
                _parents.arraySize = index + 1;

                var element = _nodes.GetArrayElementAtIndex(index);
                element.managedReferenceValue = definition;
                foreach (var (field, value) in fields)
                {
                    Set(element, definition, field, value);
                }

                _parents.GetArrayElementAtIndex(index).intValue = parent;
                return index;
            }

            /// <summary>설명의 직렬화 필드 하나에 값을 넣는다. 필드가 없으면 조용히 넘기지 않고 멈춘다.</summary>
            private static void Set(SerializedProperty element, BehaviourNodeDefinition definition, string field, object value)
            {
                var property = element.FindPropertyRelative(field)
                    ?? throw new InvalidOperationException(
                        $"{definition.GetType().Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 메뉴도 함께 고쳐야 한다.");

                switch (value)
                {
                    case string text:
                        property.stringValue = text;
                        break;
                    case Enum enumValue:
                        property.intValue = Convert.ToInt32(enumValue);
                        break;
                    case float number:
                        property.floatValue = number;
                        break;
                    case int number:
                        property.intValue = number;
                        break;
                    case bool flag:
                        property.boolValue = flag;
                        break;
                    default:
                        throw new InvalidOperationException($"'{field}'에 넣을 값의 형식 {value?.GetType().Name}은 다루지 않는다.");
                }
            }
        }
    }
}
