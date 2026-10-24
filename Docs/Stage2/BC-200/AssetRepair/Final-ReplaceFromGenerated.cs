// One-off eval_file evidence, outside Assets. Only Unity generates asset serialization.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling) throw new System.InvalidOperationException("Editor busy");
for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
    if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Dirty scene");
var path=BattleCity.Editor.Stage01Authoring.LevelPath;
var scenePath=BattleCity.Editor.Stage01Authoring.ScenePath;
var setup=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
var original=System.IO.File.ReadAllBytes(path);
var meta=System.IO.File.ReadAllBytes(path+".meta");
var scene=System.IO.File.ReadAllBytes(scenePath);
var guid=UnityEditor.AssetDatabase.AssetPathToGUID(path);
var backup="Docs/Stage2/BC-200/AssetRepair/Final-before.rawbytes";
if(System.IO.File.Exists(backup)) throw new System.InvalidOperationException("Backup already exists");
System.IO.File.WriteAllBytes(backup,original);
var tempPath="Assets/BattleCity/Data/Stage01-Repair-"+System.Guid.NewGuid().ToString("N")+".asset";
var fresh=UnityEngine.ScriptableObject.CreateInstance<BattleCity.LevelDefinition>();
bool created=false, replaced=false;
object result=null;
try
{
    BattleCity.Editor.Stage01Authoring.Populate(fresh);
    BattleCity.Editor.Stage01Authoring.ValidatePlacement(fresh);
    fresh.name="Stage01";
    var expected=fresh.terrain.ToArray();
    UnityEditor.AssetDatabase.CreateAsset(fresh,tempPath);
    created=true;
    UnityEditor.AssetDatabase.SaveAssetIfDirty(fresh);
    var generated=System.IO.File.ReadAllLines(tempPath).Single(l=>l.StartsWith("  terrain: ")).Substring(11);
    if(!System.Text.RegularExpressions.Regex.IsMatch(generated,@"\A[0-9a-fA-F]{5408}\z")) throw new System.InvalidOperationException("Generated terrain invalid");
    var old=UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCity.LevelDefinition>(path);
    UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
    if(old!=null) UnityEngine.Resources.UnloadAsset(old);
    UnityEditor.FileUtil.ReplaceFile(tempPath,path);
    replaced=true;
    UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate|UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var loaded=UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCity.LevelDefinition>(path);
    if(loaded==null) throw new System.InvalidOperationException("Reload returned null");
    // Explicitly release the imported instance and load again, proving this is not the old cached object.
    UnityEngine.Resources.UnloadAsset(loaded);
    bool unloaded=loaded==null;
    var reloaded=UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCity.LevelDefinition>(path);
    BattleCity.Editor.Stage01Authoring.ValidatePlacement(reloaded);
    if(!unloaded || reloaded==null || !reloaded.terrain.SequenceEqual(expected)) throw new System.InvalidOperationException("Fresh reload mismatch");
    var field=System.IO.File.ReadAllLines(path).Single(l=>l.StartsWith("  terrain: ")).Substring(11);
    if(!System.Text.RegularExpressions.Regex.IsMatch(field,@"\A[0-9a-fA-F]{5408}\z")) throw new System.InvalidOperationException("Disk terrain invalid");
    for(int i=0;i<676;i++) {
        uint value=0;
        for(int b=0;b<4;b++) value|=(uint)System.Convert.ToByte(field.Substring(i*8+b*2,2),16)<<(8*b);
        if(!System.Enum.IsDefined(typeof(BattleCity.TerrainKind),(int)value) || (int)expected[i]!=(int)value) throw new System.InvalidOperationException("Disk enum mismatch "+i);
    }
    if(!meta.SequenceEqual(System.IO.File.ReadAllBytes(path+".meta")) || guid!=UnityEditor.AssetDatabase.AssetPathToGUID(path)) throw new System.InvalidOperationException("Meta changed");
    result=new {tempPath, guid, terrainCharacters=field.Length,cells=reloaded.terrain.Length,unloaded,matchesVerifiedMatrix=true,allDiskEnumsLegal=true,metaUnchanged=true,sha256=System.BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.IO.File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()};
}
catch
{
    if(replaced) {
        // Restore exact original bytes through FileUtil if post-replacement validation fails.
        UnityEditor.FileUtil.ReplaceFile(backup,path);
        UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceUpdate|UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    }
    throw;
}
finally
{
    if(created && System.IO.File.Exists(tempPath)) UnityEditor.AssetDatabase.DeleteAsset(tempPath);
    else if(!created && fresh!=null) UnityEngine.Object.DestroyImmediate(fresh);
    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
}
if(!scene.SequenceEqual(System.IO.File.ReadAllBytes(scenePath))) throw new System.InvalidOperationException("Scene bytes changed");
var controller=UnityEngine.Object.FindAnyObjectByType<BattleCity.ArenaController>();
if(controller==null || controller.level==null || UnityEditor.AssetDatabase.GetAssetPath(controller.level)!=path) throw new System.InvalidOperationException("Scene binding missing");
controller.ValidateBindings();
return new {repair=result,sceneUnchanged=true,sceneBindingGuid=UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(controller.level)),sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,tempDeleted=!System.IO.File.Exists(tempPath),tempMetaDeleted=!System.IO.File.Exists(tempPath+".meta")};
