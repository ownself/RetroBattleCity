using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Explicitly rendered, read-only projection of one shared enemy. Owns only its display child.</summary>
    public sealed class EnemyTankView : MonoBehaviour
    {
        private TankState tank;
        private Sprite[,] frames;
        private SpriteRenderer display;

        public void Initialize(TankState tank, TankDefinition definition, SpriteCatalog catalog, Material material)
        {
            Clear();
            if (tank == null) throw new ArgumentNullException(nameof(tank));
            if (tank.Team != CombatTeam.Enemy) throw new ArgumentException("Enemy view requires an enemy tank.", nameof(tank));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (material == null) throw new ArgumentNullException(nameof(material));
            definition.Validate();
            var snapshot = new Sprite[4, 2];
            for (int direction = 0; direction < 4; direction++)
                for (int frame = 0; frame < 2; frame++)
                    snapshot[direction, frame] = catalog.Get(definition.spritePrefix + "." +
                        ((CardinalDirection)direction).SpriteKey() + "." + frame);
            if (display == null)
            {
                display = new GameObject("Enemy tank - display only", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                display.transform.SetParent(transform, false);
            }
            display.sharedMaterial = material;
            display.sortingLayerName = "Actors";
            frames = snapshot;
            this.tank = tank;
            Render();
        }

        public void Render()
        {
            if (display == null) return;
            if (!isActiveAndEnabled || tank == null || !tank.IsAlive)
            {
                display.enabled = false;
                display.sprite = null;
                return;
            }
            var position = tank.Motor.Position;
            display.transform.position = PixelGrid.ToWorld(new Vector2(Mathf.Round(position.x), Mathf.Round(position.y)));
            display.sprite = frames[(int)tank.Motor.Facing, tank.Motor.TrackFrame];
            display.enabled = true;
        }

        /// <summary>Unbinds without touching the shared tank. The hidden renderer may be reused by Initialize.</summary>
        public void Clear()
        {
            tank = null;
            frames = null;
            if (display == null) return;
            display.enabled = false;
            display.sprite = null;
            display.sharedMaterial = null;
        }

        // Re-enable requires an explicit binding; old session state cannot silently reappear.
        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            Clear();
            if (display != null) Destroy(display.gameObject);
        }
    }
}
