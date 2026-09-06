using UnityEngine;
using UnityEngine.Serialization;

namespace HS.Framework.Character.PlayerStart
{
    /// <summary>
    /// 씬에 플레이어가 없을 때 지정한 플레이어 프리팹을 시작 위치에 생성한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStart : MonoBehaviour
    {
        [FormerlySerializedAs("_playerPrefab")] [SerializeField] private PlayerIdentity playerPrefab;

        private void Awake()
        {
            if (FindFirstObjectByType<PlayerIdentity>() != null)
            {
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("플레이어 프리팹이 PlayerStart에 등록되지 않았습니다.", this);
                return;
            }

            Instantiate(playerPrefab, transform.position, transform.rotation);
        }
    }
}
