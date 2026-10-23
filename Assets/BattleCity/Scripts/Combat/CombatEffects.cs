using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    public enum CombatEffectKind { Hit, Explosion }

    /// <summary>A pooled presentation lease. Never a damage source or collision target.</summary>
    public sealed class CombatEffectState
    {
        public long Id { get; internal set; }
        public CombatEffectKind Kind { get; internal set; }
        public Vector2 Position { get; internal set; }
        public int AgeTicks { get; internal set; }
        public int Frame => AgeTicks / CombatEffects.TicksPerFrame;
    }

    /// <summary>Combat-tick lifetime, independent of render frequency; no delayed callbacks.</summary>
    public sealed class CombatEffects
    {
        // BC-107 prototype timing, not verified original-game timing.
        public const int TicksPerFrame = 4;
        public const int HitFrameCount = 3;
        public const int ExplosionFrameCount = 5;
        private readonly List<CombatEffectState> active = new List<CombatEffectState>();
        private readonly Stack<CombatEffectState> free = new Stack<CombatEffectState>();
        private long nextId;
        public IReadOnlyList<CombatEffectState> Active { get; }

        public CombatEffects() => Active = active.AsReadOnly();

        /// <summary>Pure presentation request; callers must separately settle death/damage.</summary>
        public void PlayExplosion(Vector2 position) => Spawn(CombatEffectKind.Explosion, position);

        // Session alone consumes the current tick's immutable hits, before the next sweep clears them.
        internal void Step(IReadOnlyList<BulletHit> hits)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var effect = active[i];
                effect.AgeTicks++;
                int frames = effect.Kind == CombatEffectKind.Hit ? HitFrameCount : ExplosionFrameCount;
                if (effect.AgeTicks >= frames * TicksPerFrame) ReleaseAt(i);
            }
            for (int i = 0; i < hits.Count; i++) Spawn(CombatEffectKind.Hit, hits[i].Position);
        }

        private void Spawn(CombatEffectKind kind, Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y))
                throw new ArgumentOutOfRangeException(nameof(position));
            var effect = free.Count > 0 ? free.Pop() : new CombatEffectState();
            effect.Id = checked(++nextId);
            effect.Kind = kind; effect.Position = position; effect.AgeTicks = 0;
            active.Add(effect);
        }

        public void Clear()
        {
            while (active.Count > 0) ReleaseAt(active.Count - 1);
        }

        private void ReleaseAt(int index)
        {
            var effect = active[index]; active.RemoveAt(index);
            effect.Id = 0; effect.Kind = default; effect.Position = Vector2.zero; effect.AgeTicks = 0;
            free.Push(effect);
        }
    }
}
