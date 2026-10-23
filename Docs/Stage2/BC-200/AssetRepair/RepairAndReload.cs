// One-off Unity script eval_file; evidence only, outside Assets. No scene/asset YAML edits.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling)
    throw new System.InvalidOperationException("Editor must be stopped and compiled before repair.");
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new System.InvalidOperationException("Unknown dirty scene: repair refused.");
var path = BattleCity.Editor.Stage01Authoring.LevelPath;
var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
var guidBefore = UnityEditor.AssetDatabase.AssetPathToGUID(path);
var sceneHashBefore = System.Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.IO.File.ReadAllBytes(BattleCity.Editor.Stage01Authoring.ScenePath)));
var level = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCity.LevelDefinition>(path);
if (level == null) throw new System.InvalidOperationException("Existing asset must be loaded, not recreated.");
string oldEntityId = level.GetEntityId().ToString();
var oldJson = UnityEngine.JsonUtility.ToJson(level);
BattleCity.Editor.Stage01Authoring.Populate(level);
BattleCity.Editor.Stage01Authoring.ValidatePlacement(level);

// Close only already-verified clean scenes to release their serialized asset references. Never save/rebuild them.
UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
object result;
try
{
    int intermediateTerrainCharacters = -1;
    try
    {
        // Supervisor-approved one-time transition: keep 676 legal enums but change one verified empty cell.
        if (level.terrain[0] != BattleCity.TerrainKind.Empty) throw new System.InvalidOperationException("Expected empty cell 0.");
        var serialized = new UnityEditor.SerializedObject(level);
        serialized.FindProperty("terrain").GetArrayElementAtIndex(0).intValue = (int)BattleCity.TerrainKind.Brick;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.EditorUtility.SetDirty(level);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(level);
        intermediateTerrainCharacters = System.IO.File.ReadAllLines(path).Single(line => line.StartsWith("  terrain: ", System.StringComparison.Ordinal)).Substring("  terrain: ".Length).Length;
    }
    finally
    {
        // No frame, gameplay, or test runs between the two saves. Restore even if the transition failed.
        BattleCity.Editor.Stage01Authoring.Populate(level);
        BattleCity.Editor.Stage01Authoring.ValidatePlacement(level);
        UnityEditor.EditorUtility.SetDirty(level);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(level);
    }
    UnityEngine.Resources.UnloadAsset(level);
    bool oldReferenceUnloaded = level == null;
    UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var reloaded = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleCity.LevelDefinition>(path);
    if (reloaded == null || !oldReferenceUnloaded || object.ReferenceEquals(level, reloaded))
        throw new System.InvalidOperationException("Cannot prove fresh asset load after unloading the cached object.");
    reloaded.Validate();
    BattleCity.Editor.Stage01Authoring.ValidatePlacement(reloaded);
    var expected = UnityEngine.ScriptableObject.CreateInstance<BattleCity.LevelDefinition>();
    try
    {
        BattleCity.Editor.Stage01Authoring.Populate(expected);
        if (!reloaded.terrain.SequenceEqual(expected.terrain)) throw new System.InvalidOperationException("Reloaded terrain differs from the verified matrix.");
        if (UnityEngine.JsonUtility.ToJson(reloaded) != UnityEngine.JsonUtility.ToJson(expected)) throw new System.InvalidOperationException("Reloaded level fields differ from expected authoring data.");
    }
    finally { UnityEngine.Object.DestroyImmediate(expected); }
    var bytes = System.IO.File.ReadAllBytes(path);
    var field = System.IO.File.ReadAllLines(path).Single(line => line.StartsWith("  terrain: ", System.StringComparison.Ordinal)).Substring("  terrain: ".Length);
    if (!System.Text.RegularExpressions.Regex.IsMatch(field, @"\A[0-9a-fA-F]{5408}\z")) throw new System.InvalidOperationException("Disk terrain contains an invalid length/character.");
    for (int i = 0; i < 676; i++)
    {
        uint value = 0;
        for (int b = 0; b < 4; b++) value |= (uint)System.Convert.ToByte(field.Substring(i * 8 + b * 2, 2), 16) << (8 * b);
        if (!System.Enum.IsDefined(typeof(BattleCity.TerrainKind), (int)value) || (int)reloaded.terrain[i] != (int)value)
            throw new System.InvalidOperationException("Invalid or mismatched disk enum at " + i);
    }
    var guidAfter = UnityEditor.AssetDatabase.AssetPathToGUID(path);
    if (guidBefore != guidAfter) throw new System.InvalidOperationException("Asset GUID changed.");
    result = new {
        guidBefore, guidAfter, oldEntityId, newEntityId = reloaded.GetEntityId().ToString(),
        oldReferenceUnloaded, managedReferenceReused = object.ReferenceEquals(level, reloaded),
        sameDataAsPriorCachedAsset = oldJson == UnityEngine.JsonUtility.ToJson(reloaded),
        cells = reloaded.terrain.Length, terrainCharacters = field.Length, intermediateTerrainCharacters,
        counts = reloaded.terrain.GroupBy(t => t).ToDictionary(g => g.Key.ToString(), g => g.Count()),
        allEnumsValidAndMatchDisk = true, matchesVerifiedMatrix = true,
        diskSha256 = System.BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(),
        diskBytes = bytes.Length, diskMtimeUtc = System.IO.File.GetLastWriteTimeUtc(path).ToString("O"),
        reloadedObjectJson = UnityEngine.JsonUtility.ToJson(reloaded)
    };
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
}
var sceneHashAfter = System.Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(System.IO.File.ReadAllBytes(BattleCity.Editor.Stage01Authoring.ScenePath)));
if (sceneHashBefore != sceneHashAfter) throw new System.InvalidOperationException("Scene file unexpectedly changed.");
var sceneController = UnityEngine.Object.FindAnyObjectByType<BattleCity.ArenaController>();
if (sceneController == null || UnityEditor.AssetDatabase.GetAssetPath(sceneController.level) != path)
    throw new System.InvalidOperationException("Restored scene reference is missing.");
sceneController.ValidateBindings();
return new { repair = result, sceneReferenceGuid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(sceneController.level)), sceneFileUnchanged = true, sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };
