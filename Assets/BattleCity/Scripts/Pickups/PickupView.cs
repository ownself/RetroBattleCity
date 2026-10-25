using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>One reusable renderer, no collider, timers, collection, or writes to session state.</summary>
    public sealed class PickupView : MonoBehaviour
    {
        private PickupState state;
        private Sprite[] frames;
        private SpriteRenderer display;
        public void Initialize(PickupState state, SpriteCatalog catalog, Material material)
        {
            Clear();
            if (state == null || catalog == null || material == null) throw new ArgumentException("Missing pickup view binding.");
            frames = new Sprite[6];
            for (int i = 0; i < frames.Length; i++) frames[i] = catalog.Get(PickupDefinition.SpriteId((PickupKind)i));
            if (display == null)
            {
                display = new GameObject("Pickup - display only", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                display.transform.SetParent(transform, false);
            }
            display.sharedMaterial = material;
            display.sortingLayerName = "Actors";
            display.sortingOrder = 2; // Above grass (Actors/1), below projectiles/effects.
            this.state = state;
            Render();
        }
        public void Render()
        {
            if (display == null) return;
            if (!isActiveAndEnabled || state?.Active == null) { display.enabled = false; display.sprite = null; return; }
            var item = state.Active.Value;
            display.transform.position = PixelGrid.ToWorld(item.Position);
            display.sprite = frames[(int)item.Kind];
            display.enabled = true;
        }
        public void Clear()
        {
            state = null; frames = null;
            if (display == null) return;
            display.enabled = false; display.sprite = null; display.sharedMaterial = null;
        }
        private void OnDisable() => Clear();
        private void OnDestroy() { Clear(); if (display != null) Destroy(display.gameObject); }
    }
}
