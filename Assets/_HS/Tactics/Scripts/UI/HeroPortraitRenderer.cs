using System.Collections.Generic;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.UI
{
    /// <summary>
    /// 영웅 프리팹의 정지 3D 초상을 한 번만 구워 <see cref="RenderTexture"/>로 내놓는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>실제 게임플레이 프리팹의 시각 모델만 떼어 쓴다.</b> <see cref="UnitDefinition.UnitPrefab"/>은
    /// <see cref="Units.TacticalUnit"/>·행동 트리 실행기·어빌리티 시스템까지 붙은 완전한 프리팹이다.
    /// 그대로 <c>Instantiate</c>해 활성화하면 그 자리에서 Awake가 돌아 조립이 시작되는데, VContainer
    /// 주입도 유닛 정의도 없는 채로 조립이 시작되면 무엇이 터질지 이 컴포넌트가 책임질 수 없다.
    /// 그래서 <b>비활성 부모 아래에 만들어 Awake를 미룬 채</b>(<see cref="Placement.UnitSpawner"/>가
    /// 스폰 전에 쓰는 것과 같은 요령이다) 시각 모델이 든 자식 하나만 골라 활성 자리로 옮기고,
    /// 스크립트가 잔뜩 붙은 뿌리는 그 자리에서 버린다. 옮겨진 자식에는 그런 스크립트가 없으므로
    /// 조립이 시작될 일 자체가 없다.
    /// </para>
    /// <para>
    /// <b>한 번 구우면 다시 굽지 않는다.</b> 회전이나 재생이 없는 정지 그림이므로 매 프레임 렌더링할
    /// 이유가 없다. 처음 물을 때 한 번 <see cref="Camera.Render"/>로 굽고, 그 뒤로는 캐시한 텍스처를
    /// 그대로 돌려준다. 구운 뒤에는 모델을 꺼 두어 화면 밖에 떠 있는 동안에도 아무 비용이 들지 않는다.
    /// </para>
    /// <para>
    /// <b>화면 밖에 두는 것으로 충분하다.</b> 이 컴포넌트가 앉은 자리가 플레이 구역에서 멀리 떨어져 있으면,
    /// 그 자리를 비추는 것은 여기 붙은 전용 카메라뿐이라 컬링 마스크나 전용 레이어가 필요 없다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HeroPortraitRenderer : MonoBehaviour
    {
        [Tooltip("초상을 구울 전용 카메라이다. 이 컴포넌트의 자리를 기준으로 모델이 설 자리를 비추도록 미리 놓아 둔다.\n" +
                 "매 프레임 렌더링하지 않으므로 꺼 두어도 된다.")]
        [SerializeField]
        private Camera previewCamera;

        [Tooltip("구운 텍스처의 한 변 크기(픽셀)이다.")]
        [SerializeField]
        [Min(32)]
        private int textureSize = 256;

        private readonly Dictionary<UnitDefinition, RenderTexture> _bakedPortraits = new();
        private Transform _stagingRoot;
        private Transform _previewSlot;

        /// <summary>
        /// 그 정의의 정지 초상을 돌려준다. 처음 물으면 한 번 굽고, 그 뒤로는 캐시한 것을 그대로 돌려준다.
        /// </summary>
        /// <param name="definition">초상을 구할 유닛 정의이다.</param>
        /// <returns>구운 텍스처이며, 프리팹이 없거나 시각 모델을 찾지 못했으면 null이다.</returns>
        public RenderTexture GetOrBakePortrait(UnitDefinition definition)
        {
            if (definition == null || !definition.HasUnitPrefab)
            {
                return null;
            }

            if (_bakedPortraits.TryGetValue(definition, out var cached))
            {
                return cached;
            }

            var baked = Bake(definition);
            _bakedPortraits[definition] = baked;
            return baked;
        }

        /// <summary>모델을 세우고 한 번 찍어 텍스처에 담는다.</summary>
        /// <param name="definition">구울 유닛 정의이다.</param>
        /// <returns>구운 텍스처이며, 시각 모델을 찾지 못했으면 null이다.</returns>
        private RenderTexture Bake(UnitDefinition definition)
        {
            if (previewCamera == null)
            {
                Debug.LogWarning(
                    $"[HeroPortraitRenderer] 전용 카메라가 연결되지 않아 {definition.DisplayName}의 초상을 구울 수 없다.",
                    this);
                return null;
            }

            var visual = ExtractVisual(definition.UnitPrefab, definition.DisplayName);
            if (visual == null)
            {
                return null;
            }

            var texture = new RenderTexture(textureSize, textureSize, 16) { name = $"HeroPortrait_{definition.Id}" };
            var previousTarget = previewCamera.targetTexture;
            previewCamera.targetTexture = texture;
            previewCamera.Render();
            previewCamera.targetTexture = previousTarget;

            // 정지 그림이라 다시 그릴 일이 없다. 구운 뒤에는 꺼 두어 화면 밖에 떠 있는 동안 비용이 들지 않게 한다.
            visual.SetActive(false);
            return texture;
        }

        /// <summary>
        /// 프리팹을 비활성 자리에 지어 Awake를 미룬 채, 시각 모델이 든 자식 하나만 활성 자리로 꺼내고
        /// 스크립트가 붙은 뿌리는 버린다.
        /// </summary>
        /// <param name="fullPrefab">전체 게임플레이 프리팹이다.</param>
        /// <param name="displayName">경고에 적을 유닛 이름이다.</param>
        /// <returns>활성 자리로 옮긴 시각 모델이며, 모양이 다르면 null이다.</returns>
        private GameObject ExtractVisual(GameObject fullPrefab, string displayName)
        {
            EnsureStagingHierarchy();

            var instance = Instantiate(fullPrefab, _stagingRoot);
            if (instance.transform.childCount != 1)
            {
                Debug.LogWarning(
                    $"[HeroPortraitRenderer] {displayName} 프리팹의 모양이 예상과 달라(자식 {instance.transform.childCount}개) " +
                    "시각 모델만 떼어 낼 수 없다. 뿌리에 스크립트와 자식 모델 하나만 있는 모양을 기대한다.",
                    this);
                Destroy(instance);
                return null;
            }

            var visual = instance.transform.GetChild(0).gameObject;
            visual.transform.SetParent(_previewSlot, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            Destroy(instance);
            return visual;
        }

        /// <summary>스테이징 뿌리(비활성)와 미리보기 자리(활성)를 처음 쓸 때 만든다.</summary>
        private void EnsureStagingHierarchy()
        {
            if (_stagingRoot != null)
            {
                return;
            }

            var stagingRootObject = new GameObject("PortraitStagingRoot");
            stagingRootObject.transform.SetParent(transform, false);
            stagingRootObject.SetActive(false);
            _stagingRoot = stagingRootObject.transform;

            var previewSlotObject = new GameObject("PortraitPreviewSlot");
            previewSlotObject.transform.SetParent(transform, false);
            _previewSlot = previewSlotObject.transform;
        }

        private void OnDestroy()
        {
            foreach (var texture in _bakedPortraits.Values)
            {
                if (texture != null)
                {
                    texture.Release();
                    Destroy(texture);
                }
            }

            _bakedPortraits.Clear();
        }
    }
}
