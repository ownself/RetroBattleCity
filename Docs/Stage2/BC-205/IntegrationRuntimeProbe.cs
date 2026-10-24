using System;
using System.IO;
using System.Linq;
using BattleCity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Ephemeral observation recipe, never imported as a runtime/Editor asset.
public static class BC205IntegrationRuntimeProbe
{
    public static string Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required");
        var c = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
        var s = c.Session;
        if (c.gameObject.scene.path != "Assets/BattleCity/Scenes/Stage01.unity" || s.PlayerLife == null || !s.PlayerTank.IsAlive)
            throw new Exception("Fresh default Stage01 lifecycle required");
        var shield = c.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "Player shield - display only");
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        var definition = UnityEngine.Object.Instantiate(c.playerDefinition);
        definition.moveSpeedPixelsPerSecond = .001f;
        definition.bulletSpeedPixelsPerSecond = 180;
        var enemy = s.AddTank(definition, CombatTeam.Enemy, new Vector2(72, 40));
        UnityEngine.Object.Destroy(definition); // Shared tank/weapon already snapshot all parameters.
        var player = s.PlayerTank; var motor = s.Player; var weapon = s.PlayerWeapon;
        int firstFrame = Time.frameCount, stage = 0;
        long firstTick = s.Clock.Tick;
        float deadline = Time.realtimeSinceStartup + 180;
        Action<string> snapshot = phase => File.WriteAllText("Docs/Stage2/BC-205/Integration-Runtime-" + phase + ".json",
            JsonConvert.SerializeObject(new {
                phase, stage, firstFrame, frame = Time.frameCount, firstTick, tick = s.Clock.Tick,
                playerAlive = player.IsAlive, lives = s.PlayerLife.RemainingLives,
                wait = s.PlayerLife.RespawnRemainingTicks, protection = s.PlayerLife.ProtectionRemainingTicks,
                playerVisible = c.playerRenderer.enabled, shieldVisible = shield.enabled,
                shieldSprite = shield.sprite == null ? null : shield.sprite.name,
                playerPosition = motor.Position.ToString(), shieldPosition = shield.transform.position.ToString(),
                facing = motor.Facing.ToString(), trackFrame = motor.TrackFrame,
                playerReferenceStable = ReferenceEquals(player, s.PlayerTank), motorReferenceStable = ReferenceEquals(motor, s.Player),
                weaponReferenceStable = ReferenceEquals(weapon, s.PlayerWeapon), enemyId = enemy.EntityId,
                enemyLeases = enemy.Weapon.ActiveBullets, playerLeases = weapon.ActiveBullets,
                hits = s.Bullets.Hits.Select(h => new { h.OwnerId, h.TargetId, kind = h.Kind.ToString() }).ToArray(),
                droppedTicks = s.Clock.DroppedTicks, paused = s.Clock.Paused,
                background = Application.runInBackground
            }, Formatting.Indented));
        s.SetFocus(true); s.SetPaused(false);
        s.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Down, false, true));
        snapshot("Armed");
        EditorApplication.CallbackFunction observer = null;
        observer = () => {
            if (Time.realtimeSinceStartup > deadline || c == null || !EditorApplication.isPlaying)
            {
                if (c != null && EditorApplication.isPlaying) { s.SetPaused(true); snapshot("TimedOut"); }
                InputSystem.RemoveDevice(keyboard); EditorApplication.update -= observer;
                SessionState.EraseBool("BC205.ObserverActive"); return;
            }
            if (stage == 0 && !player.IsAlive)
            {
                s.SetPaused(true); stage = 1; snapshot("Death");
            }
            else if (stage == 1 && !s.Clock.Paused) { stage = 2; snapshot("WaitingResumed"); }
            else if (stage == 2 && player.IsAlive)
            {
                s.SetPaused(true); stage = 3; snapshot("Shield");
            }
            else if (stage == 3 && !s.Clock.Paused) { stage = 4; snapshot("ProtectionResumed"); }
            else if (stage == 4 && !s.PlayerLife.IsProtected)
            {
                s.SetPaused(true); stage = 5; snapshot("Expired");
                InputSystem.RemoveDevice(keyboard); EditorApplication.update -= observer;
                SessionState.EraseBool("BC205.ObserverActive");
                File.WriteAllText("Docs/Stage2/BC-205/Integration-Runtime-Cleanup.json", JsonConvert.SerializeObject(new {
                    observerRemoved = true, keyboardRemoved = !InputSystem.devices.Contains(keyboard),
                    passed = player.IsAlive && s.PlayerLife.RemainingLives == 2 && !shield.enabled && Time.frameCount > firstFrame && s.Clock.Tick > firstTick
                }, Formatting.Indented));
                Debug.Log("BC205 runtime observation complete; ticks=" + s.Clock.Tick + " droppedTicks=" + s.Clock.DroppedTicks);
            }
        };
        SessionState.SetBool("BC205.ObserverActive", true);
        EditorApplication.update += observer;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        return "Observer armed. Only real Controller.Update advances combat; shared enemy WeaponState produces the lethal projectile. Resume paused session after each capture.";
    }
}
