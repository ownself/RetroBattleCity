using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    [CreateAssetMenu(menuName = "Battle City/Sprite Catalog")]
    public sealed class SpriteCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private Dictionary<string, Sprite> lookup;
        public IReadOnlyList<Entry> Entries => entries;

        public Sprite Get(string id)
        {
            if (lookup == null) BuildLookup();
            if (!lookup.TryGetValue(id, out var sprite)) throw new KeyNotFoundException("Missing sprite: " + id);
            return sprite;
        }

        public void Validate() => BuildLookup();

        private void BuildLookup()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.id) || entry.sprite == null || result.ContainsKey(entry.id))
                    throw new InvalidOperationException("Empty, duplicate or unbound Sprite ID: " + entry.id);
                if (entry.sprite.pixelsPerUnit != PixelGrid.PixelsPerUnit)
                    throw new InvalidOperationException("Sprite PPU mismatch: " + entry.id);
                result.Add(entry.id, entry.sprite);
            }
            lookup = result;
        }

        private void OnEnable() => lookup = null;
        private void OnValidate() => lookup = null;
    }
}
