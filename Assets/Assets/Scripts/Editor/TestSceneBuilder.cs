using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the graybox test scene from code so it is reproducible and
/// reviewable. Run via Tools > Input Lag > Build Test Scene. Re-running
/// overwrites the scene. Editor-only (lives in an Editor folder).
/// </summary>
public static class TestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TestScene.unity";
    // Unity's built-in 1x1-unit square (the GameObject > 2D Object > Sprites > Square asset).
    private const string SquareSpritePath =
        "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Square.png";
    private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string GroundLayerName = "Ground";
    // TMP's default font resources ship inside the uGUI package but must be
    // imported into Assets once per project.
    private const string TmpEssentialsPackagePath =
        "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    private const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    private static readonly Color GroundColor = new Color(0.35f, 0.35f, 0.35f);
    private static readonly Color PlatformColor = new Color(0.55f, 0.55f, 0.55f);

    [MenuItem("Tools/Input Lag/Build Test Scene")]
    public static void Build()
    {
        // Don't silently discard someone's unsaved scene edits.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureTmpEssentials();
        EnsureGroundLayer();
        Sprite square = LoadSquareSprite();
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildGlobalLight();

        // Left ground x[-12..2], right ground x[9..14]: a 7-unit gap that only
        // jump->dash clears (jump reach ~5.7, jump+dash ~8.7).
        BuildBox("Ground Left", square, new Vector2(-5f, -0.5f), new Vector2(14f, 1f), groundLayer, GroundColor);
        BuildBox("Ground Right", square, new Vector2(11.5f, -0.5f), new Vector2(5f, 1f), groundLayer, GroundColor);

        // Each platform top is ~1.5-1.75 units above the previous surface,
        // inside the ~2.5-unit jump height.
        BuildBox("Platform 1", square, new Vector2(-9f, 1.5f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);
        BuildBox("Platform 2", square, new Vector2(-5.5f, 3f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);
        BuildBox("Platform 3", square, new Vector2(-2f, 4.5f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);

        GameObject player = BuildPlayer(square, new Vector2(-11f, 1f), groundLayer);
        CameraFollow follow = WireCameraFollow(player);
        BuildLevelSystems(player, follow, square);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        Debug.Log($"Test scene built and saved to {ScenePath}");
    }

    private static void EnsureSceneInBuildSettings()
    {
        // A player build only contains scenes listed here — without this a
        // WebGL/jam build would ship the empty template scene.
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static CameraFollow WireCameraFollow(GameObject player)
    {
        var camera = GameObject.FindWithTag("MainCamera");
        var follow = camera.AddComponent<CameraFollow>();
        var followSo = new SerializedObject(follow);
        followSo.FindProperty("target").objectReferenceValue = player.transform;
        // View bounds = the level's extent (x[-12..14]), a little margin below the
        // ground (-2), open sky above. The camera view never leaves this rect.
        followSo.FindProperty("boundsMin").vector2Value = new Vector2(-12f, -2f);
        followSo.FindProperty("boundsMax").vector2Value = new Vector2(14f, 40f);
        followSo.ApplyModifiedPropertiesWithoutUndo();
        // Start the camera at its follow position so play mode doesn't open with a
        // swoop; read the offset off the component so retuning it can't go stale.
        camera.transform.position =
            player.transform.position + followSo.FindProperty("offset").vector3Value;
        return follow;
    }

    private static void BuildLevelSystems(GameObject player, CameraFollow follow, Sprite square)
    {
        var managerGo = new GameObject("LevelManager");
        var banner = managerGo.AddComponent<BannerUI>();
        var manager = managerGo.AddComponent<LevelManager>();

        Checkpoint checkpointA = BuildCheckpoint("Checkpoint A (spawn)",
            new Vector2(-11f, 1f), manager);
        BuildCheckpoint("Checkpoint B (pre-gap)", new Vector2(0.5f, 1.5f), manager);

        var killGo = BuildTrigger("Kill Zone", new Vector2(0f, -7f), new Vector2(60f, 2f));
        Wire(killGo.AddComponent<KillZone>(), "levelManager", manager);

        // Goal: visible green block past the jump->dash gap. Collider auto-sizes
        // to the 1x1 sprite, scaled by the transform.
        var goalGo = new GameObject("Goal");
        goalGo.transform.position = new Vector2(13f, 0.75f);
        goalGo.transform.localScale = new Vector3(1f, 1.5f, 1f);
        var goalRenderer = goalGo.AddComponent<SpriteRenderer>();
        goalRenderer.sprite = square;
        goalRenderer.color = new Color(0.25f, 0.85f, 0.35f);
        goalGo.AddComponent<BoxCollider2D>().isTrigger = true;
        Wire(goalGo.AddComponent<LevelGoal>(), "levelManager", manager);

        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("motor").objectReferenceValue =
            player.GetComponent<PlayerMotor>();
        managerSo.FindProperty("queue").objectReferenceValue =
            player.GetComponent<CommandQueue>();
        managerSo.FindProperty("inputHandler").objectReferenceValue =
            player.GetComponent<PlayerInputHandler>();
        managerSo.FindProperty("cameraFollow").objectReferenceValue = follow;
        managerSo.FindProperty("banner").objectReferenceValue = banner;
        managerSo.FindProperty("initialCheckpoint").objectReferenceValue = checkpointA;
        managerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Checkpoint BuildCheckpoint(string name, Vector2 position, LevelManager manager)
    {
        var go = BuildTrigger(name, position, new Vector2(1f, 3f));
        var checkpoint = go.AddComponent<Checkpoint>();
        Wire(checkpoint, "levelManager", manager);
        return checkpoint;
    }

    private static GameObject BuildTrigger(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        return go;
    }

    private static void Wire(Component component, string field, Object value)
    {
        var so = new SerializedObject(component);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureTmpEssentials()
    {
        // Runtime-created TextMeshPro components read the default font from TMP
        // Settings; without the import TMP logs "Can't Generate Mesh, No Font
        // Asset has been assigned" and the queue UI renders nothing.
        if (AssetDatabase.LoadMainAssetAtPath(TmpSettingsAssetPath) != null)
            return;
        // Note: ImportPackage is asynchronous — fine today because the scene
        // contains no baked TMP objects (the queue UI builds itself at runtime),
        // but a future scene-baked label would need to wait for the import.
        AssetDatabase.ImportPackage(
            System.IO.Path.GetFullPath(TmpEssentialsPackagePath), false);
        Debug.Log("Imported TMP Essential Resources (required for the queue UI text).");
    }

    private static void EnsureGroundLayer()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == GroundLayerName)
                return;

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = GroundLayerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }
        throw new System.InvalidOperationException("No free layer slot for the Ground layer.");
    }

    private static Sprite LoadSquareSprite()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (sprite == null)
            throw new System.InvalidOperationException(
                $"Built-in square sprite not found at {SquareSpritePath} — did the com.unity.2d.sprite package layout change?");
        return sprite;
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        // 5.5 leaves the clamped camera room to pan inside this level's bounds;
        // bigger values show more lookahead but make the follow nearly static here.
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        go.transform.position = new Vector3(1f, 4.5f, -10f);
        go.AddComponent<AudioListener>();
    }

    private static void BuildGlobalLight()
    {
        // URP 2D's default sprite material is lit; without a global light
        // everything renders black.
        var go = new GameObject("Global Light 2D");
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
    }

    private static void BuildBox(string name, Sprite sprite, Vector2 position,
        Vector2 size, int layer, Color color)
    {
        var go = new GameObject(name) { layer = layer, isStatic = true };
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        go.AddComponent<BoxCollider2D>(); // auto-sizes to the 1x1 sprite, scaled by transform
    }

    private static GameObject BuildPlayer(Sprite sprite, Vector2 position, int groundLayer)
    {
        var go = new GameObject("Player");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 4f; // motor tuning (jumpVelocity 14) assumes this
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<BoxCollider2D>();

        var motor = go.AddComponent<PlayerMotor>();
        var motorSo = new SerializedObject(motor);
        motorSo.FindProperty("groundLayer").intValue = 1 << groundLayer;
        motorSo.ApplyModifiedPropertiesWithoutUndo();

        var queue = go.AddComponent<CommandQueue>();
        var queueSo = new SerializedObject(queue);
        queueSo.FindProperty("motor").objectReferenceValue = motor;
        queueSo.ApplyModifiedPropertiesWithoutUndo();

        var input = go.AddComponent<PlayerInputHandler>();
        var inputSo = new SerializedObject(input);
        inputSo.FindProperty("actions").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        inputSo.ApplyModifiedPropertiesWithoutUndo();

        var ui = go.AddComponent<CommandQueueUI>();
        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("queue").objectReferenceValue = queue;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }
}
