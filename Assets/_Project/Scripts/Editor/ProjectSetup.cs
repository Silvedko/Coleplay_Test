using System.Collections.Generic;
using System.IO;
using Game.Composition;
using Game.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-shot project setup: builds the cube prefab, puts TargetView on both prefabs, fills the
    /// Addressables groups, creates the GameConfig asset and assembles the playable scene.
    ///
    /// Everything it does is ordinary editor work that would otherwise be done by hand; it lives
    /// here so the setup is reproducible and reviewable instead of hidden in a .unity diff.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenePath = ProjectRoot + "/Scenes/Main.unity";
        private const string SpherePrefabPath = ProjectRoot + "/Prefabs/Target_Sphere.prefab";
        private const string CubePrefabPath = ProjectRoot + "/Prefabs/Target_Cube.prefab";
        private const string FallbackTexturePath = ProjectRoot + "/Art/Textures/Fallback/T_Fallback.png";
        private const string RoundTexturesFolder = ProjectRoot + "/Art/Textures/Rounds";
        private const string ConfigPath = ProjectRoot + "/Settings/GameConfig.asset";

        private const string LocalGroupName = "Core_Local";
        private const string RemoteGroupName = "RoundImages_Remote";
        private const string RoundImageLabel = "round-image";

        [MenuItem("Tools/Coleplay/Set Up Playable Scene")]
        public static void Run()
        {
            EnsureCubePrefab();
            EnsureTargetView(SpherePrefabPath);
            EnsureTargetView(CubePrefabPath);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var localGroup = EnsureGroup(settings, LocalGroupName, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            var remoteGroup = EnsureGroup(settings, RemoteGroupName, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);

            settings.AddLabel(RoundImageLabel);

            MarkAddressable(settings, localGroup, SpherePrefabPath, "Target_Sphere");
            MarkAddressable(settings, localGroup, CubePrefabPath, "Target_Cube");
            MarkAddressable(settings, localGroup, FallbackTexturePath, "T_Fallback");

            var roundTextures = Directory.GetFiles(RoundTexturesFolder, "*.png");
            System.Array.Sort(roundTextures);
            foreach (var path in roundTextures)
            {
                var unityPath = path.Replace('\\', '/');
                var address = Path.GetFileNameWithoutExtension(unityPath);
                var entry = MarkAddressable(settings, remoteGroup, unityPath, address);
                entry?.SetLabel(RoundImageLabel, true, true);
            }

            var config = EnsureConfig();
            BuildScene(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Coleplay setup finished: " + roundTextures.Length + " round textures labelled '"
                      + RoundImageLabel + "', scene saved at " + ScenePath + ".");
        }

        private static void EnsureCubePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath) != null)
            {
                return;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Target_Cube";

            PrefabUtility.SaveAsPrefabAsset(cube, CubePrefabPath);
            Object.DestroyImmediate(cube);
        }

        private static void EnsureTargetView(string prefabPath)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);

            var root = scope.prefabContentsRoot;
            if (root.GetComponent<TargetView>() == null)
            {
                root.AddComponent<TargetView>();
            }
        }

        private static AddressableAssetGroup EnsureGroup(
            AddressableAssetSettings settings,
            string groupName,
            BundledAssetGroupSchema.BundlePackingMode packingMode)
        {
            var group = settings.FindGroup(groupName);

            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BundleMode = packingMode;

                // Both groups stay local for now; the remote profile is switched on in the hosting step.
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }

            return group;
        }

        private static AddressableAssetEntry MarkAddressable(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string assetPath,
            string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("Cannot mark " + assetPath + " as addressable: the asset is missing.");
                return null;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            return entry;
        }

        private static GameConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var serialized = new SerializedObject(config);

            var prefabs = serialized.FindProperty("_targetPrefabs");
            prefabs.arraySize = 2;
            SetAssetReference(prefabs.GetArrayElementAtIndex(0), SpherePrefabPath);
            SetAssetReference(prefabs.GetArrayElementAtIndex(1), CubePrefabPath);

            serialized.FindProperty("_imagesLabel")
                .FindPropertyRelative("m_LabelString").stringValue = RoundImageLabel;

            SetAssetReference(serialized.FindProperty("_fallbackTexture"), FallbackTexturePath);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            return config;
        }

        private static void SetAssetReference(SerializedProperty property, string assetPath)
        {
            property.FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(assetPath);
        }

        private static void BuildScene(GameConfig config)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -3.2f), Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.09f, 0.11f, 0.13f);
            }

            var hud = Object.FindFirstObjectByType<HudView>();
            if (hud == null)
            {
                hud = BuildHud();
            }

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            }

            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("_config").objectReferenceValue = config;
            serializedBootstrap.FindProperty("_hud").objectReferenceValue = hud;
            serializedBootstrap.FindProperty("_camera").objectReferenceValue = camera;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static HudView BuildHud()
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var status = CreateLabel(canvasObject.transform, "StatusLabel",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f),
                TextAlignmentOptions.TopLeft, 42f);

            var score = CreateLabel(canvasObject.transform, "ScoreLabel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f),
                TextAlignmentOptions.TopRight, 42f);

            var miss = CreateLabel(canvasObject.transform, "MissLabel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f),
                TextAlignmentOptions.Bottom, 48f);
            miss.color = new Color(0.9f, 0.28f, 0.24f);

            var hud = canvasObject.AddComponent<HudView>();

            var serialized = new SerializedObject(hud);
            serialized.FindProperty("_statusLabel").objectReferenceValue = status;
            serialized.FindProperty("_scoreLabel").objectReferenceValue = score;
            serialized.FindProperty("_missLabel").objectReferenceValue = miss;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            TextAlignmentOptions alignment,
            float fontSize)
        {
            var label = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);

            var rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rect.sizeDelta = new Vector2(900f, 80f);
            rect.anchoredPosition = anchoredPosition;

            label.alignment = alignment;
            label.fontSize = fontSize;
            label.text = string.Empty;

            return label;
        }
    }
}
