// Validation-only Game View driver. Uses Editor internal APIs (Unity 6000.6).
// Set BattleCity.M0.Width/Height in SessionState before invoking; defaults to 768x720.
var assembly = typeof(UnityEditor.Editor).Assembly;
var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
var sizes = singleton.GetProperty("instance").GetValue(null);
var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
var gameType = assembly.GetType("UnityEditor.GameView");
var game = UnityEditor.EditorWindow.GetWindow(gameType);
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var selected = gameType.GetProperty("selectedSizeIndex", flags);
if (UnityEditor.SessionState.GetInt("BattleCity.M0.PreviousSizeIndex", -1) < 0)
    UnityEditor.SessionState.SetInt("BattleCity.M0.PreviousSizeIndex", (int)selected.GetValue(game));
int width = UnityEditor.SessionState.GetInt("BattleCity.M0.Width", 768);
int height = UnityEditor.SessionState.GetInt("BattleCity.M0.Height", 720);
var label = "BattleCity M0 " + width + "x" + height;
var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
int index = -1;
for(int i=0;i<texts.Length;i++) if(texts[i].StartsWith(label, System.StringComparison.Ordinal)) index=i;
if(index < 0)
{
    var kind = System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
    var size = System.Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[] { kind, width, height, label });
    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
    index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
}
selected.SetValue(game, index); game.Repaint();
return "Game View target = " + width + "x" + height;
