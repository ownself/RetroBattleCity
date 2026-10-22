using System;
using UnityEngine;

namespace BattleCity
{
    public sealed class ArenaController : MonoBehaviour
    {
        public LevelDefinition level;
        public TankDefinition playerDefinition;
        public SpriteCatalog sprites;
        public ArenaMapView mapView;
        public SpriteRenderer playerRenderer;
        private readonly Sprite[,] playerFrames = new Sprite[4, 2];
        public ArenaSession Session { get; private set; }
        public BulletView BulletView { get; private set; }

        private void Awake()
        {
            ValidateBindings();
            for (int direction = 0; direction < 4; direction++) for (int frame = 0; frame < 2; frame++)
                playerFrames[direction, frame] = sprites.Get(playerDefinition.spritePrefix + "." + ((CardinalDirection)direction).SpriteKey() + "." + frame);
            Session = new ArenaSession(level, playerDefinition);
            BulletView = new GameObject("Bullets - display only").AddComponent<BulletView>();
            BulletView.transform.SetParent(transform, false);
            BulletView.Initialize(Session.Bullets, sprites, playerRenderer.sharedMaterial);
            mapView.Render(Session.Map);
            Session.SetFocus(Application.isFocused);
            RenderPlayer();
        }

        private void Update()
        {
            Session.AdvanceFrame(Time.unscaledDeltaTime, KeyboardInputSource.Read());
            mapView.Render(Session.Map); // No tile writes unless logical cell revisions changed.
            RenderPlayer();
            BulletView.Render();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (Session != null) Session.SetFocus(hasFocus);
        }

        private void OnDisable()
        {
            if (Session != null) Session.SetPaused(true);
        }

        private void RenderPlayer()
        {
            var position = Session.Player.Position;
            // Display rounding is never written back to the motor.
            position = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
            playerRenderer.transform.position = PixelGrid.ToWorld(position);
            playerRenderer.sprite = playerFrames[(int)Session.Player.Facing, Session.Player.TrackFrame];
        }

        public void ValidateBindings()
        {
            if (level == null || playerDefinition == null || sprites == null || mapView == null || playerRenderer == null)
                throw new InvalidOperationException("Missing arena reference.");
            level.Validate(); playerDefinition.Validate(); sprites.Validate(); mapView.ValidateBindings();
        }
    }
}
