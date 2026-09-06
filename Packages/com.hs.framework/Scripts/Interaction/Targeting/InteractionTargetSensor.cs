using UnityEngine;

namespace HS.Framework.Interaction.Targeting
{
    /// <summary>
    /// 카메라 시선의 Raycast로 상호작용 대상을 탐색한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionTargetSensor : MonoBehaviour
    {
        private Camera _viewCamera;
        private float _distance = 2f;
        private LayerMask _layerMask = Physics.DefaultRaycastLayers;

        /// <summary>
        /// 대상 탐색에 사용할 카메라와 물리 설정을 지정한다.
        /// </summary>
        public void Configure(Camera viewCamera, float distance, LayerMask layerMask)
        {
            _viewCamera = viewCamera;
            _distance = Mathf.Max(0f, distance);
            _layerMask = layerMask;
        }

        /// <summary>
        /// 시선에 있는 가장 가까운 상호작용 대상을 반환한다.
        /// </summary>
        public IInteractable FindTarget()
        {
            var viewCamera = ResolveViewCamera();
            if (viewCamera == null)
            {
                return null;
            }

            var viewTransform = viewCamera.transform;
            if (!Physics.Raycast(
                    viewTransform.position,
                    viewTransform.forward,
                    out var hit,
                    _distance,
                    _layerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<IInteractable>();
        }

        private Camera ResolveViewCamera()
        {
            if (_viewCamera == null || !_viewCamera.isActiveAndEnabled)
            {
                _viewCamera = Camera.main;
            }

            return _viewCamera;
        }
    }
}
