// One-off script eval_file evidence recipe; not imported as a Unity script/asset.
var source = new UnityEngine.Texture2D(2,2);
var frame = new UnityEngine.Texture2D(2,2);
var comparison = new UnityEngine.Texture2D(832,416,UnityEngine.TextureFormat.RGBA32,false);
try
{
    source.LoadImage(System.IO.File.ReadAllBytes(BattleCity.Editor.Stage01Authoring.ReferencePath));
    var bytes = System.IO.File.ReadAllBytes("Assets/Docs/Stage2/BC-200/Stage01-Live.png");
    frame.LoadImage(bytes);
    System.IO.File.WriteAllBytes("Docs/Stage2/BC-200/Stage01-Live.png",bytes);
    for(int y=0;y<208;y++) for(int x=0;x<208;x++)
    {
        var reference=source.GetPixel(x,y);
        // Capture 768x720 = 3x256x240; frozen map offset16 means 48px in this frame.
        var runtime=frame.GetPixel(48+x*3+1,48+y*3+1);
        for(int dy=0;dy<2;dy++) for(int dx=0;dx<2;dx++)
        {
            comparison.SetPixel(x*2+dx,y*2+dy,reference);
            comparison.SetPixel(416+x*2+dx,y*2+dy,runtime);
        }
    }
    comparison.Apply();
    System.IO.File.WriteAllBytes("Docs/Stage2/BC-200/Reference-vs-Stage01.png",comparison.EncodeToPNG());
}
finally
{
    UnityEngine.Object.DestroyImmediate(source);
    UnityEngine.Object.DestroyImmediate(frame);
    UnityEngine.Object.DestroyImmediate(comparison);
}
// Only capture-created paths were inspected in Capture-TemporaryPaths.json before this deletion.
if(!UnityEditor.AssetDatabase.DeleteAsset("Assets/Docs/Stage2/BC-200/Stage01-Live.png")) throw new System.Exception("Cannot remove temporary capture");
foreach(var directory in new[]{"Assets/Docs/Stage2/BC-200","Assets/Docs/Stage2","Assets/Docs"})
{
    if(System.IO.Directory.GetFileSystemEntries(directory).Length != 0) throw new System.Exception("Not empty: " + directory);
    if(!UnityEditor.AssetDatabase.DeleteAsset(directory)) throw new System.Exception("Cannot remove temporary directory: " + directory);
}
return "Reference comparison saved; temporary capture assets removed via AssetDatabase";
