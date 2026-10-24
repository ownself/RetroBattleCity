using System;
using System.IO;
using System.Linq;
using BattleCity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Ephemeral Editor observation recipe; never imported as a gameplay component.
public static class BC204RuntimeProbe
{
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required");
        var c = UnityEngine.Object.FindFirstObjectByType<ArenaController>();
        var s = c.Session;
        if (c.gameObject.scene.path != "Assets/BattleCity/Scenes/Stage01.unity" || !s.Base.IsAlive)
            throw new Exception("Expected fresh default Stage01 base");
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        s.SetFocus(true); s.SetPaused(false);
        int firstFrame = Time.frameCount, deathFrame = -1, notifications = 0, explosions = 0;
        long firstTick = s.Clock.Tick, deathTick = -1;
        float deadline = Time.realtimeSinceStartup + 10;
        Action onDeath = () => {
            notifications++; deathFrame = Time.frameCount; deathTick = s.Clock.Tick;
            explosions = s.Effects.Active.Count(e => e.Kind == CombatEffectKind.Explosion && e.Position == s.Base.Position);
        };
        s.BaseDestroyed += onDeath;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space)); InputSystem.Update();
        EditorApplication.CallbackFunction observer = null;
        observer = () => {
            if (deathTick < 0 && Time.realtimeSinceStartup < deadline) return;
            if (deathTick >= 0 && s.Clock.Tick < deathTick + 35 && Time.realtimeSinceStartup < deadline) return;
            s.SetPaused(true);
            InputSystem.RemoveDevice(keyboard);
            s.BaseDestroyed -= onDeath;
            EditorApplication.update -= observer;
            SessionState.EraseBool("BC204.ObserverActive");
            var result = new {
                firstFrame, deathFrame, finalFrame = Time.frameCount, firstTick, deathTick, finalTick = s.Clock.Tick,
                notifications, explosionsAtDeath = explosions, baseAlive = s.Base.IsAlive, failureRequested = s.BaseFailureRequested,
                sprite = c.baseRenderer.sprite.name, player = s.Player.Position.ToString(), revision = s.Map.Revision,
                waveTotal = s.EnemyWave.TotalCount, spawned = s.EnemyWave.SpawnedCount, droppedTicks = s.Clock.DroppedTicks,
                paused = s.Clock.Paused, observerRemoved = true, keyboardRemoved = !InputSystem.devices.Contains(keyboard),
                passed = deathTick > firstTick && s.Clock.Tick >= deathTick + 35 && notifications == 1 && explosions == 1 && c.baseRenderer.sprite.name == "base.dead"
            };
            File.WriteAllText("Docs/Stage2/BC-204/Runtime-Observed.json", JsonConvert.SerializeObject(result, Formatting.Indented));
            Debug.Log("BC204 runtime probe " + JsonConvert.SerializeObject(result));
        };
        SessionState.SetBool("BC204.ObserverActive", true);
        EditorApplication.update += observer;
        return "Observer armed; real Controller.Update and keyboard/shared weapon only, no manual Step/AdvanceFrame";
    }
}
