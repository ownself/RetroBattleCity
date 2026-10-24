using UnityEngine;

namespace BattleCity.Editor
{
    /// <summary>Fixed reference-frame HUD, rendered into the same pixel-perfect camera target as the arena.</summary>
    public static class Stage01HudAuthoring
    {
        public static ArenaHud Create(ArenaController controller)
        {
            var go = new GameObject("Pixel HUD - read only", typeof(RectTransform), typeof(Canvas), typeof(ArenaHud));
            var hud = go.GetComponent<ArenaHud>();
            var rect = go.GetComponent<RectTransform>();
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(PixelGrid.ReferenceWidth, PixelGrid.ReferenceHeight);
            rect.position = PixelGrid.ToWorld(-PixelGrid.MapScreenOffset);
            rect.localScale = Vector3.one / PixelGrid.PixelsPerUnit;
            hud.hudCanvas = go.GetComponent<Canvas>();
            hud.hudCanvas.renderMode = RenderMode.WorldSpace;
            hud.hudCanvas.sortingLayerName = "HUD";
            hud.hudCanvas.sortingOrder = 0;
            hud.sprites = controller.sprites;
            var gray = new Color32(99, 99, 99, 255);
            Panel(rect, "Left border", 0, 0, 16, 240, gray);
            Panel(rect, "Right HUD slot", 224, 0, 32, 240, gray);
            Panel(rect, "Top border", 16, 224, 208, 16, gray);
            Panel(rect, "Bottom border", 16, 0, 208, 16, gray);
            Caption(rect, "Stage label", "STAGE", 228, 213);
            Icon(rect, "Stage flag", hud.sprites.Get("hud.flag"), 228, 192, 16, 16);
            hud.stageDigits = Number(rect, "Stage", 180);
            Caption(rect, "Enemy label", "ENEMY", 228, 157);
            Icon(rect, "Enemy icon", hud.sprites.Get("tank.white.enemy.basic.up.0"), 228, 136, 16, 16);
            hud.enemyDigits = Number(rect, "Enemies remaining", 124);
            Caption(rect, "Lives label", "LIVES", 228, 101);
            Icon(rect, "Player lives icon", hud.sprites.Get("hud.lives1"), 228, 88, 16, 8);
            hud.lifeDigits = Number(rect, "Total lives", 76);
            hud.phaseCaption = Caption(rect, "Phase and start or resume prompt", "READY  ENTER START", 16, 230);
            hud.restartCaption = Caption(rect, "Restart prompt", "R RESTART", 16, 5);
            Caption(rect, "Life meaning", "LIVES CURRENT + RESERVE", 104, 5);
            controller.hud = hud;
            hud.ValidateBindings();
            return hud;
        }

        private static RectTransform Box(Transform parent, string name, int x, int y, int width, int height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Panel(Transform parent, string name, int x, int y, int w, int h, Color color)
        {
            var image = Box(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
        }

        private static UnityEngine.UI.Image Icon(Transform parent, string name, Sprite sprite, int x, int y, int w, int h)
        {
            var image = Box(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Image[] Number(Transform parent, string name, int y)
        {
            var result = new UnityEngine.UI.Image[4];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Icon(parent, name + " digit " + i, null, 224 + i * 8, y, 8, 8);
                result[i].enabled = i == result.Length - 1;
                if (i == result.Length - 1) result[i].sprite = parent.GetComponent<ArenaHud>().sprites.Get("hud.digit.0");
            }
            return result;
        }

        private static PixelHudText Caption(Transform parent, string name, string text, int x, int y)
        {
            var rect = Box(parent, name, x, y, text.Length * 4, 5);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var caption = rect.gameObject.AddComponent<PixelHudText>();
            caption.color = Color.black; caption.raycastTarget = false; caption.Text = text;
            return caption;
        }
    }
}
