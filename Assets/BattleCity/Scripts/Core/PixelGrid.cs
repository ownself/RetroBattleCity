using UnityEngine;

namespace BattleCity
{
    /// <summary>Frozen M0 coordinate contract. Logic uses map-local pixels, Y up.</summary>
    public static class PixelGrid
    {
        public const int PixelsPerUnit = 16;
        public const int ReferenceWidth = 256;
        public const int ReferenceHeight = 240;
        public const int MapSizePixels = 208;
        public const int LayoutCellPixels = 16;
        public const int TerrainCellPixels = 8;
        public const int DestructionCellPixels = 4;
        public const int TerrainCellsPerAxis = MapSizePixels / TerrainCellPixels;
        public const int LogicHz = 60;
        public const float LogicStep = 1f / LogicHz;
        public static readonly Vector2Int MapScreenOffset = new Vector2Int(16, 16);
        public static readonly Vector3 CameraPosition = new Vector3(7f, 6.5f, -10f);

        public static Vector3 ToWorld(Vector2 mapPixels) =>
            new Vector3(mapPixels.x / PixelsPerUnit, mapPixels.y / PixelsPerUnit, 0f);
        public static Vector2 ToMapPixels(Vector3 world) =>
            new Vector2(world.x * PixelsPerUnit, world.y * PixelsPerUnit);
        public static Vector2Int TerrainCellAt(Vector2 mapPixels) => new Vector2Int(
            Mathf.FloorToInt(mapPixels.x / TerrainCellPixels),
            Mathf.FloorToInt(mapPixels.y / TerrainCellPixels));
        public static bool Contains(Vector2 p) =>
            p.x >= 0 && p.y >= 0 && p.x < MapSizePixels && p.y < MapSizePixels;
        public static Vector2 TerrainCellCenter(Vector2Int cell) =>
            new Vector2((cell.x + .5f) * TerrainCellPixels, (cell.y + .5f) * TerrainCellPixels);
        public static RectInt SourceRectToUnity(RectInt topLeft, int sourceHeight) =>
            new RectInt(topLeft.x, sourceHeight - topLeft.yMax, topLeft.width, topLeft.height);
    }
}
