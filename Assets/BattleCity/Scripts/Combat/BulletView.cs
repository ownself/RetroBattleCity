using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Read-only projection, reusing renderers without maintaining a second projectile simulation.</summary>
    public sealed class BulletView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private readonly Sprite[] frames = new Sprite[4];
        private BulletWorld world;
        private Material material;

        public void Initialize(BulletWorld world, SpriteCatalog catalog, Material material)
        {
            this.world = world;
            this.material = material;
            for (int direction = 0; direction < 4; direction++)
                frames[direction] = catalog.Get("bullet." + ((CardinalDirection)direction).SpriteKey());
        }

        public void Render()
        {
            for (int i = 0; i < world.Active.Count; i++)
            {
                if (i == renderers.Count)
                {
                    var renderer = new GameObject("Bullet display " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    renderer.transform.SetParent(transform, false);
                    renderer.sharedMaterial = material;
                    renderer.sortingLayerName = "Projectiles";
                    renderers.Add(renderer);
                }
                var bullet = world.Active[i];
                var view = renderers[i];
                view.sprite = frames[(int)bullet.Direction];
                view.transform.position = PixelGrid.ToWorld(new Vector2(Mathf.Round(bullet.Position.x), Mathf.Round(bullet.Position.y)));
                view.gameObject.SetActive(true);
            }
            for (int i = world.Active.Count; i < renderers.Count; i++) renderers[i].gameObject.SetActive(false);
        }
    }
}
