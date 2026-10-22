// Standalone statement block for `unity command script eval_file` (not a compiled project script).
// Idempotent: existing name -> GUID pairs survive re-slicing. Source PNGs are never rewritten.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
var source = "Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - General Sprites.png";
var stage = "Assets/Sprites/NES - Battle City (JPN) - Backgrounds - Stage 01.png";
var output = "Assets/BattleCity/Art/GeneralSprites.png";
foreach (var folder in new[] { "Art", "Data", "Prefabs", "Scenes", "Tests/EditMode", "Tests/PlayMode", "Scripts/Input", "Scripts/AI", "Scripts/Combat", "Scripts/Pickups", "Scripts/UI" })
{
    var current = "Assets/BattleCity";
    foreach (var part in folder.Split('/')) { if (!UnityEditor.AssetDatabase.IsValidFolder(current + "/" + part)) UnityEditor.AssetDatabase.CreateFolder(current, part); current += "/" + part; }
}
var texture = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
try
{
    texture.LoadImage(System.IO.File.ReadAllBytes(source));
    if (texture.width != 400 || texture.height != 256) throw new System.InvalidOperationException("Unexpected source dimensions.");
    var pixels = texture.GetPixels32();
    int removed = 0, preservedBlack = 0;
    for (int i = 0; i < pixels.Length; i++)
    {
        // The gray-backed STAGE/digit panel reuses the near-black key as VALID INK.
        // Protect it explicitly; never erase RGB(0,0,0) anywhere.
        int x = i % 400, yTop = 255 - i / 400;
        bool hudInkPanel = x >= 328 && x < 368 && yTop >= 176 && yTop < 200;
        if (!hudInkPanel && pixels[i].r == 0 && pixels[i].g == 0 && pixels[i].b == 1) { pixels[i].a = 0; removed++; }
        else if (pixels[i].r == 0 && pixels[i].g == 0 && pixels[i].b == 0 && pixels[i].a > 0) preservedBlack++;
    }
    if (removed == 0 || preservedBlack == 0) throw new System.InvalidOperationException("Source palette changed: re-audit transparency.");
    texture.SetPixels32(pixels); texture.Apply();
    System.IO.File.WriteAllBytes(output, texture.EncodeToPNG());
}
finally { UnityEngine.Object.DestroyImmediate(texture); }
UnityEditor.AssetDatabase.ImportAsset(output, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
foreach (var path in new[] { source, stage, output })
{
    var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    imp.textureType = UnityEditor.TextureImporterType.Sprite;
    imp.filterMode = UnityEngine.FilterMode.Point;
    imp.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    imp.crunchedCompression = false; imp.mipmapEnabled = false;
    imp.spritePixelsPerUnit = 16; imp.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    imp.wrapMode = UnityEngine.TextureWrapMode.Clamp; imp.maxTextureSize = 2048;
    imp.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
    imp.alphaIsTransparency = false; // no RGB dilation: preserve the original palette exactly
    if (path == output) {
        imp.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
        var settings = new UnityEditor.TextureImporterSettings(); imp.ReadTextureSettings(settings);
        settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect; imp.SetTextureSettings(settings);
    }
    var platform = imp.GetPlatformTextureSettings("Standalone"); platform.overridden = false; imp.SetPlatformTextureSettings(platform);
    imp.SaveAndReimport();
}
var importer = UnityEditor.AssetImporter.GetAtPath(output);
var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories(); factory.Init();
var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
if (provider == null) throw new System.InvalidOperationException("No Sprite data provider. Aborted.");
provider.InitSpriteEditorDataProvider();
var capabilityProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
if (capabilityProvider == null) throw new System.InvalidOperationException("Sprite edit capability unavailable. Aborted.");
var capability = capabilityProvider.GetEditCapability();
foreach (var required in new[] { UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite, UnityEditor.U2D.Sprites.EEditCapability.EditSpriteName, UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect, UnityEditor.U2D.Sprites.EEditCapability.EditPivot, UnityEditor.U2D.Sprites.EEditCapability.EditBorder })
    if (!capability.HasCapability(required)) throw new System.InvalidOperationException("Sprite capability denied: " + required);
var nameProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
if (nameProvider == null) throw new System.InvalidOperationException("Name/fileID provider unavailable. Aborted.");
var existing = new System.Collections.Generic.Dictionary<string, UnityEngine.GUID>();
foreach (var rect in provider.GetSpriteRects()) existing[rect.name] = rect.spriteID;
var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();
var csv = new System.Text.StringBuilder("id,x_top_left,y_top_left,width,height,pivot_x_pixels,pivot_y_pixels\n");
System.Action<string,int,int,int,int> add = (id,x,y,w,h) =>
{
    if (x < 0 || y < 0 || x+w > 400 || y+h > 256 || w%2 != 0 || h%2 != 0) throw new System.Exception("Invalid slice: " + id);
    var guid = existing.ContainsKey(id) ? existing[id] : UnityEngine.GUID.Generate();
    rects.Add(new UnityEditor.SpriteRect { name = id, rect = new UnityEngine.Rect(x, 256-y-h, w, h), alignment = UnityEngine.SpriteAlignment.Custom, pivot = new UnityEngine.Vector2(.5f, .5f), border = UnityEngine.Vector4.zero, spriteID = guid });
    csv.AppendLine(id+","+x+","+y+","+w+","+h+","+(w/2)+","+(h/2));
};
var palettes = new[] { "yellow", "white", "green", "red" };
var variants = new[] { "player.level0", "player.level1", "player.level2", "player.level3", "enemy.basic", "enemy.fast", "enemy.power", "enemy.armor" };
var directions = new[] { "up", "left", "down", "right" };
for (int p=0; p<4; p++) for (int row=0; row<8; row++) for (int d=0; d<4; d++) for (int f=0; f<2; f++)
    add("tank."+palettes[p]+"."+variants[row]+"."+directions[d]+"."+f, (p%2)*128+d*32+f*16, (p/2)*128+row*16, 16, 16);
add("terrain.brick16",256,0,16,16); add("terrain.steel16",256,16,16,16);
add("terrain.water.0",256,32,16,16); add("terrain.water.1",256,48,16,16);
add("terrain.grass16",272,32,16,16); add("terrain.ice16",288,32,16,16);
add("base.alive",304,32,16,16); add("base.dead",320,32,16,16);
for (int y=0;y<2;y++) for(int x=0;x<2;x++) {
    add("terrain.brick8."+x+"."+y,256+x*8,y*8,8,8);
    add("terrain.steel8."+x+"."+y,256+x*8,16+y*8,8,8);
}
for (int f=0;f<4;f++) add("effect.spawn."+f,256+f*16,96,16,16);
for (int d=0;d<4;d++) add("bullet."+directions[d],320+d*8,100,8,8);
add("effect.flame",352,96,16,16);
var pickups = new[] { "helmet", "clock", "shovel", "star", "grenade", "tank", "gun" };
for(int i=0;i<pickups.Length;i++) add("pickup."+pickups[i],256+i*16,112,16,16);
for(int i=0;i<3;i++) add("effect.explosion.small."+i,256+i*16,128,16,16);
for(int i=0;i<2;i++) add("effect.explosion.large."+i,304+i*32,128,32,32);
for(int i=0;i<2;i++) add("effect.shield."+i,256+i*16,144,16,16);
for(int i=0;i<5;i++) add("hud.score."+((i+1)*100),288+i*16,160,16,8);
add("hud.pause",288,176,40,8); add("hud.game",288,184,32,8); add("hud.over",288,192,32,8);
add("hud.stage",328,176,40,8);
for(int i=0;i<10;i++) add("hud.digit."+i,328+(i%5)*8,184+(i/5)*8,8,8);
add("hud.player1",376,136,16,8); add("hud.player2",376,160,16,8);
add("hud.lives1",376,144,16,8); add("hud.lives2",376,168,16,8);
add("hud.flag",376,184,16,16);
provider.SetSpriteRects(rects.ToArray());
var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
foreach(var rect in rects) pairs.Add(new UnityEditor.SpriteNameFileIdPair(rect.name, rect.spriteID));
nameProvider.SetNameFileIdPairs(pairs); provider.Apply(); importer.SaveAndReimport();
System.IO.Directory.CreateDirectory("Docs/Stage0");
System.IO.File.WriteAllText("Docs/Stage0/SpriteInventory.csv",csv.ToString());
return "Prepared "+rects.Count+" semantic sprites; preserved original PNGs, opaque RGB black and STAGE/digit ink panel. Inventory: Docs/Stage0/SpriteInventory.csv";
