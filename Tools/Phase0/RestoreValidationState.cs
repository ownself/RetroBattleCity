// Explicit cleanup after evidence has been copied to Docs/Stage0/Screenshots.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
var assembly = typeof(UnityEditor.Editor).Assembly;
var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
var group = sizesType.GetProperty("currentGroup").GetValue(singleton.GetProperty("instance").GetValue(null));
var gameType = assembly.GetType("UnityEditor.GameView");
var game = UnityEditor.EditorWindow.GetWindow(gameType);
var selected = gameType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
int previous = UnityEditor.SessionState.GetInt("BattleCity.M0.PreviousSizeIndex", -1);
if(previous >= 0) selected.SetValue(game, previous);
var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);
for(int i=texts.Length-1;i>=0;i--)
    if(texts[i].StartsWith("BattleCity M0 ",System.StringComparison.Ordinal) && (bool)group.GetType().GetMethod("IsCustomSize").Invoke(group,new object[]{i}))
        group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});
UnityEditor.SessionState.EraseInt("BattleCity.M0.PreviousSizeIndex");
UnityEditor.SessionState.EraseInt("BattleCity.M0.Width"); UnityEditor.SessionState.EraseInt("BattleCity.M0.Height");
game.Repaint();
if(UnityEditor.AssetDatabase.IsValidFolder("Assets/BattleCitySmoke")) UnityEditor.AssetDatabase.DeleteAsset("Assets/BattleCitySmoke");
if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Temp/bc-first-render.png") != null)
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp/bc-first-render.png");
if(System.IO.Directory.Exists("Assets/Temp") && System.IO.Directory.GetFiles("Assets/Temp").Length == 0 && System.IO.Directory.GetDirectories("Assets/Temp").Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp");
UnityEditor.AssetDatabase.SaveAssets();
return "Restored Game View selection, removed validation presets and temporary capture assets.";
