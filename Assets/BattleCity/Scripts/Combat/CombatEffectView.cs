using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Read-only effect projection. Inactive renderers are reused until explicit lifecycle cleanup.</summary>
    public sealed class CombatEffectView : MonoBehaviour
    {
        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private readonly Sprite[] frames = new Sprite[CombatEffects.ExplosionFrameCount];
        private CombatEffects effects;
        private Material material;

        public void Initialize(CombatEffects effects, SpriteCatalog catalog, Material material)
        {
            Clear();
            this.effects = effects; this.material = material;
            for (int i = 0; i < CombatEffects.HitFrameCount; i++)
                frames[i] = catalog.Get("effect.explosion.small." + i);
            frames[3] = catalog.Get("effect.explosion.large.0");
            frames[4] = catalog.Get("effect.explosion.large.1");
        }

        public void Render()
        {
            if (effects == null || !isActiveAndEnabled) return;
            for (int i = 0; i < effects.Active.Count; i++)
            {
                if (i == renderers.Count)
                {
                    var renderer = new GameObject("Combat effect display " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    renderer.transform.SetParent(transform, false);
                    renderer.sharedMaterial = material;
                    renderer.sortingLayerName = "Effects";
                    renderers.Add(renderer);
                }
                var effect = effects.Active[i];
                var view = renderers[i];
                view.sprite = frames[effect.Frame];
                view.transform.position = PixelGrid.ToWorld(new Vector2(Mathf.Round(effect.Position.x), Mathf.Round(effect.Position.y)));
                view.gameObject.SetActive(true);
            }
            for (int i = effects.Active.Count; i < renderers.Count; i++)
            {
                renderers[i].sprite = null;
                renderers[i].gameObject.SetActive(false);
            }
        }

        public void Clear()
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.sprite = null;
                renderer.gameObject.SetActive(false); // Destroy is deferred in Play Mode; hide immediately.
                if (Application.isPlaying) Destroy(renderer.gameObject);
                else DestroyImmediate(renderer.gameObject);
            }
            renderers.Clear();
        }

        private void OnDisable() => Clear();
        private void OnDestroy() { Clear(); effects = null; }
    }
}
