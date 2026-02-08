using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Devian.TestsMcp.Editor
{
    /// <summary>
    /// One-click setup for MaterialEffectV2 test environment.
    /// Menu: Devian/Tests/Setup MaterialEffectV2
    /// </summary>
    public static class MaterialEffectV2Setup
    {
        private const string BasePath = "Assets/Tests_MCP/MaterialEffectV2";
        private const string MaterialsPath = BasePath + "/Materials";
        private const string EffectsPath = BasePath + "/Effects";
        private const string ScenesPath = BasePath + "/Scenes";

        [MenuItem("Devian/Tests/Setup MaterialEffectV2")]
        public static void SetupAll()
        {
            // Ensure folders exist
            EnsureFolder(BasePath);
            EnsureFolder(MaterialsPath);
            EnsureFolder(EffectsPath);
            EnsureFolder(ScenesPath);

            // Detect shader and color property
            string shaderName;
            string colorPropName;
            if (Shader.Find("Unlit/Color") != null)
            {
                shaderName = "Unlit/Color";
                colorPropName = "_Color";
            }
            else if (Shader.Find("Universal Render Pipeline/Unlit") != null)
            {
                shaderName = "Universal Render Pipeline/Unlit";
                colorPropName = "_BaseColor";
            }
            else
            {
                Debug.LogError("[MaterialEffectV2Setup] No suitable shader found (Unlit/Color or URP/Unlit)");
                return;
            }

            Debug.Log($"[MaterialEffectV2Setup] Using shader: {shaderName}, colorProp: {colorPropName}");

            // 1. Create Materials
            var matBlue = CreateMaterial($"{MaterialsPath}/M_Base_Blue.mat", shaderName, colorPropName, Color.blue);
            var matRed = CreateMaterial($"{MaterialsPath}/M_Effect_Red.mat", shaderName, colorPropName, Color.red);
            var matGreen = CreateMaterial($"{MaterialsPath}/M_Effect_Green.mat", shaderName, colorPropName, Color.green);

            // 2. Create Effect Assets (MaterialSetMaterialEffectAsset)
            var effectRed = CreateMaterialSetEffectAsset($"{EffectsPath}/ME_Set_Red.asset", new Material[] { matRed }, 10);
            var effectGreen = CreateMaterialSetEffectAsset($"{EffectsPath}/ME_Set_Green.asset", new Material[] { matGreen }, 20);

            // 3. Create Scene with configured GameObjects
            CreateTestScene(matBlue, effectRed, effectGreen);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MaterialEffectV2Setup] Setup complete! Open the scene and press Play.");
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                var folderName = System.IO.Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Material CreateMaterial(string path, string shaderName, string colorPropName, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Debug.Log($"[MaterialEffectV2Setup] Material already exists: {path}");
                existing.shader = Shader.Find(shaderName);
                existing.SetColor(colorPropName, color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find(shaderName);
            var mat = new Material(shader);
            mat.SetColor(colorPropName, color);

            AssetDatabase.CreateAsset(mat, path);
            Debug.Log($"[MaterialEffectV2Setup] Created material: {path}");
            return mat;
        }

        private static MaterialSetMaterialEffectAsset CreateMaterialSetEffectAsset(string path, Material[] materials, int priority)
        {
            var existing = AssetDatabase.LoadAssetAtPath<MaterialSetMaterialEffectAsset>(path);
            if (existing != null)
            {
                Debug.Log($"[MaterialEffectV2Setup] Effect asset already exists: {path}");
                SetEffectAssetFields(existing, materials, priority);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<MaterialSetMaterialEffectAsset>();
            SetEffectAssetFields(asset, materials, priority);

            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[MaterialEffectV2Setup] Created effect asset: {path}");
            return asset;
        }

        private static void SetEffectAssetFields(MaterialSetMaterialEffectAsset asset, Material[] materials, int priority)
        {
            var so = new SerializedObject(asset);

            // Base class field: _priority
            var priorityProp = so.FindProperty("_priority");
            if (priorityProp != null)
            {
                priorityProp.intValue = priority;
            }
            else
            {
                Debug.LogWarning("[MaterialEffectV2Setup] _priority field not found via SerializedObject");
            }

            // This class field: _materials
            var materialsProp = so.FindProperty("_materials");
            if (materialsProp != null)
            {
                materialsProp.arraySize = materials.Length;
                for (int i = 0; i < materials.Length; i++)
                {
                    materialsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
                }
            }
            else
            {
                Debug.LogWarning("[MaterialEffectV2Setup] _materials field not found via SerializedObject");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTestScene(Material baseMaterial, MaterialSetMaterialEffectAsset effectA, MaterialSetMaterialEffectAsset effectB)
        {
            string scenePath = $"{ScenesPath}/MaterialEffectV2_Minimal.unity";

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create TestActor (Cube)
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "TestActor";
            cube.transform.position = Vector3.zero;

            // Set base material
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = baseMaterial;

            // Add components
            var driver = cube.AddComponent<MaterialEffectDriverCommon>();
            var controller = cube.AddComponent<MaterialEffectController>();
            var harness = cube.AddComponent<Devian.TestsMcp.MaterialEffectV2Harness>();

            // Configure Driver: set _renderer via SerializedObject
            var driverSO = new SerializedObject(driver);
            var rendererProp = driverSO.FindProperty("_renderer");
            if (rendererProp != null)
            {
                rendererProp.objectReferenceValue = renderer;
                driverSO.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[MaterialEffectV2Setup] _renderer field not found on MaterialEffectDriverCommon");
            }

            // Configure Controller: set _driverComponent if available
            var controllerSO = new SerializedObject(controller);
            var driverCompProp = controllerSO.FindProperty("_driverComponent");
            if (driverCompProp != null)
            {
                driverCompProp.objectReferenceValue = driver;
                controllerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // Configure Harness
            var harnessSO = new SerializedObject(harness);

            var controllerProp = harnessSO.FindProperty("_controller");
            if (controllerProp != null)
            {
                controllerProp.objectReferenceValue = controller;
            }

            var effectAProp = harnessSO.FindProperty("_effectA");
            if (effectAProp != null)
            {
                effectAProp.objectReferenceValue = effectA;
            }

            var effectBProp = harnessSO.FindProperty("_effectB");
            if (effectBProp != null)
            {
                effectBProp.objectReferenceValue = effectB;
            }

            var stepSecondsProp = harnessSO.FindProperty("_stepSeconds");
            if (stepSecondsProp != null)
            {
                stepSecondsProp.floatValue = 1.0f;
            }

            harnessSO.ApplyModifiedPropertiesWithoutUndo();

            // Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[MaterialEffectV2Setup] Created scene: {scenePath}");
        }
    }
}
