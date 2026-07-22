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

    private static readonly Color GroundColor = new Color(0.35f, 0.35f, 0.35f);
    private static readonly Color PlatformColor = new Color(0.55f, 0.55f, 0.55f);

    [MenuItem("Tools/Input Lag/Build Test Scene")]
    public static void Build()
    {
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

        BuildPlayer(square, new Vector2(-11f, 1f), groundLayer);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Test scene built and saved to {ScenePath}");
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
        cam.orthographicSize = 7f;
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

    private static void BuildPlayer(Sprite sprite, Vector2 position, int groundLayer)
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
    }
}
