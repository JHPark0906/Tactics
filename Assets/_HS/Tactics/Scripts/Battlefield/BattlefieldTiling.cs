using UnityEngine;

namespace HS.Tactics.Battlefield
{
    /// <summary>
    /// 전장의 길이에서 바닥 타일의 자리·회전과 전장 경계를 계산하는 순수 산술이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>축과 원점.</b> 전장의 길이 축은 월드 Z이고 폭은 X다. 몸통 타일은 원점을 가운데 두고 Z 축을 따라
    /// 늘어서며, 양 끝에 끝단 타일이 하나씩 붙는다. 배치 구역과 카메라가 이 축·원점에 맞춰 손으로 놓여 있으므로
    /// 여기서 축을 바꾸면 그것들도 함께 옮겨야 한다.
    /// </para>
    /// <para>
    /// <b>타일 치수는 프리팹에서 온다.</b> 몸통 타일은 12×12 m이고 원점이 XZ 중심이다. 끝단 타일은 폭 12 m, 길이 8 m이고
    /// 원점이 z=0 가장자리에 있어 +Z로만 뻗는다. 그래서 −Z 끝단은 180도 돌려 붙여야 몸통 바깥으로 뻗는다 —
    /// 돌리지 않으면 몸통 안쪽과 겹친다.
    /// </para>
    /// <para>
    /// <b>길이는 올림한다.</b> 길이가 타일 크기의 배수가 아니면 타일을 하나 더 깔아 요청한 길이를 모두 덮는다.
    /// 경계는 몸통과 끝단을 전부 담는 크기다.
    /// </para>
    /// <para>
    /// GameObject 없이 좌표만 계산하므로 수명주기 없이 검사할 수 있고, 생성기는 이 값을 그대로 세운다.
    /// </para>
    /// </remarks>
    public static class BattlefieldTiling
    {
        /// <summary>몸통 타일 한 장의 길이(Z 방향, 미터)이다.</summary>
        public const float TileSize = 12f;

        /// <summary>타일의 폭(X 방향, 미터)이다.</summary>
        public const float TileWidth = 12f;

        /// <summary>끝단 타일의 길이(Z 방향, 미터)이다.</summary>
        public const float CapLength = 8f;

        /// <summary>+Z 끝단 타일의 회전이다. 프리팹이 +Z로 뻗으므로 그대로 둔다.</summary>
        public static Quaternion PlusCapRotation => Quaternion.identity;

        /// <summary>−Z 끝단 타일의 회전이다. 프리팹이 +Z로만 뻗으므로 180도 돌려 몸통 바깥으로 향하게 한다.</summary>
        public static Quaternion MinusCapRotation => Quaternion.Euler(0f, 180f, 0f);

        /// <summary>전장 길이를 덮는 데 필요한 몸통 타일 수이다. 올림하며 항상 1 이상이다.</summary>
        /// <param name="battlefieldLength">몸통의 길이(미터)이다.</param>
        /// <returns>몸통 타일 수이다.</returns>
        public static int GetBodyTileCount(float battlefieldLength)
        {
            return Mathf.Max(1, Mathf.CeilToInt(battlefieldLength / TileSize));
        }

        /// <summary>몸통 타일 하나의 자리이다. 타일들이 원점을 가운데 두고 Z 축을 따라 늘어선다.</summary>
        /// <param name="index">0부터 시작하는 타일 번호이며 −Z 쪽이 0이다.</param>
        /// <param name="tileCount">몸통 타일 수이다.</param>
        /// <returns>타일 원점의 월드 좌표이다.</returns>
        public static Vector3 GetBodyTilePosition(int index, int tileCount)
        {
            return new Vector3(0f, 0f, (index - (tileCount - 1) * 0.5f) * TileSize);
        }

        /// <summary>몸통이 +Z 방향으로 끝나는 좌표이며, +Z 끝단 타일의 원점이다.</summary>
        /// <param name="tileCount">몸통 타일 수이다.</param>
        /// <returns>끝단 타일 원점의 월드 좌표이다.</returns>
        public static Vector3 GetPlusCapPosition(int tileCount)
        {
            return new Vector3(0f, 0f, GetBodyHalfLength(tileCount));
        }

        /// <summary>몸통이 −Z 방향으로 끝나는 좌표이며, −Z 끝단 타일의 원점이다.</summary>
        /// <param name="tileCount">몸통 타일 수이다.</param>
        /// <returns>끝단 타일 원점의 월드 좌표이다.</returns>
        public static Vector3 GetMinusCapPosition(int tileCount)
        {
            return new Vector3(0f, 0f, -GetBodyHalfLength(tileCount));
        }

        /// <summary>몸통과 양 끝단을 모두 담는 전장 경계의 반너비이다. x는 폭, y는 길이 방향이다.</summary>
        /// <param name="tileCount">몸통 타일 수이다.</param>
        /// <returns>경계 중심에서 각 축까지의 거리(미터)이다.</returns>
        public static Vector2 GetBoundsHalfExtents(int tileCount)
        {
            return new Vector2(TileWidth * 0.5f, GetBodyHalfLength(tileCount) + CapLength);
        }

        /// <summary>몸통 전체 길이의 절반이다.</summary>
        private static float GetBodyHalfLength(int tileCount)
        {
            return tileCount * TileSize * 0.5f;
        }
    }
}
