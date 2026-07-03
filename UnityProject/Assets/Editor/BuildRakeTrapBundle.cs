using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BuildRakeTrapBundle
{
    private const string PrefabPath = "Assets/RakeTrap/Prefabs/RakeTrapPrefab.prefab";
    private const string UpgradedPrefabPath = "Assets/RakeTrap/Prefabs/UpgradedRakeTrapPrefab.prefab";
    private const string SourceModelPath = "Assets/RakeTrap/SourceAssets/Rake001.fbx";
    private const string UpgradedSourcePrefabPath = "Assets/RakeTrap/SourceAssets/Upgraded Rake/RakeUpdated.prefab";
    private const string UpgradedMaterialPath = "Assets/RakeTrap/SourceAssets/Upgraded Rake/Materials/Rake002.mat";
    private const string MaterialsDir = "Assets/RakeTrap/Materials";
    private const string AnimationsDir = "Assets/RakeTrap/Animations";
    private const string BundleName = "raketrap.unity3d";
    private const string AnimationTrigger = "Spring";
    private const string SourceHandleName = "RakeHandle";
    private const string SourceHeadName = "RakeHead.001";
    private const string UpgradedSourceHandleName = "RakeHandle.289";
    private const string UpgradedSourceHeadName = "RakeHead.001";
    private const float GroundClearance = 0.02f;
    private const float MinColliderHeight = 0.25f;
    private const float ArmedHandleAngle = -90f;
    private const float SprungHandleAngle = 0f;

    private static readonly string[] BuiltPrefabNames =
    {
        "RakeTrapPrefab.prefab",
        "UpgradedRakeTrapPrefab.prefab"
    };

    [MenuItem("Rake Trap/Build AssetBundle")]
    public static void BuildFromMenu()
    {
        BuildAll();
    }

    public static void BuildAll()
    {
        EnsurePrefab();

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string outputDir = Path.Combine(projectRoot, "1A-RakeTrap", "Resources");
        Directory.CreateDirectory(outputDir);

        BuildPipeline.BuildAssetBundles(
            outputDir,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        DeleteIfExists(Path.Combine(outputDir, new DirectoryInfo(outputDir).Name));
        DeleteIfExists(Path.Combine(outputDir, new DirectoryInfo(outputDir).Name + ".manifest"));
        foreach (string manifest in Directory.GetFiles(outputDir, "*.manifest"))
        {
            DeleteIfExists(manifest);
        }

        Debug.Log($"Built {BundleName} to {outputDir}");
    }

    public static void ValidateBuiltBundle()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string bundlePath = Path.Combine(projectRoot, "1A-RakeTrap", "Resources", BundleName);
        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException($"Missing built bundle at {bundlePath}");
        }

        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            throw new InvalidOperationException($"Could not load asset bundle at {bundlePath}");
        }

        try
        {
            Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetName in bundle.GetAllAssetNames())
            {
                Debug.Log($"Bundle asset: {assetName}");
                foreach (string prefabName in BuiltPrefabNames)
                {
                    if (assetName.EndsWith(prefabName, StringComparison.OrdinalIgnoreCase))
                    {
                        prefabs[prefabName] = bundle.LoadAsset<GameObject>(assetName);
                    }
                }
            }

            foreach (string prefabName in BuiltPrefabNames)
            {
                if (!prefabs.TryGetValue(prefabName, out GameObject prefab) || prefab == null)
                {
                    throw new InvalidOperationException($"Built bundle does not contain {prefabName}");
                }

                ValidatePrefab(prefab, prefabName);
            }

            Debug.Log("Rake trap bundle validation passed.");
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    public static void DumpSourceModel()
    {
        EnsureFolders();
        ConfigureModelImport(SourceModelPath);
        AssetDatabase.ImportAsset(SourceModelPath, ImportAssetOptions.ForceUpdate);

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (source == null)
        {
            throw new FileNotFoundException($"Missing source model at {SourceModelPath}");
        }

        Debug.Log($"Rake source model: {source.name}");
        DumpTransformTree(source.transform, source.transform, 0);

        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"Rake source renderer count: {renderers.Length}");
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            Debug.Log($"Renderer {i}: {GetRelativePath(source.transform, renderer.transform)} type={renderer.GetType().Name} bounds center={renderer.bounds.center} size={renderer.bounds.size}");
        }

        MeshFilter[] meshFilters = source.GetComponentsInChildren<MeshFilter>(true);
        Debug.Log($"Rake source mesh filter count: {meshFilters.Length}");
        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh mesh = meshFilters[i].sharedMesh;
            string meshInfo = mesh == null ? "missing mesh" : $"mesh={mesh.name} vertices={mesh.vertexCount} bounds center={mesh.bounds.center} size={mesh.bounds.size}";
            Debug.Log($"MeshFilter {i}: {GetRelativePath(source.transform, meshFilters[i].transform)} {meshInfo}");
        }
    }

    private static void EnsurePrefab()
    {
        EnsureFolders();
        ConfigureSourceTextures();
        RuntimeAnimatorController controller = EnsureAnimatorController();
        Material rakeMaterial = MakeRakeMaterial();
        Material upgradedMaterial = LoadSourceMaterial(UpgradedMaterialPath);

        EnsureRakePrefab(
            PrefabPath,
            "RakeTrapPrefab",
            SourceModelPath,
            SourceHandleName,
            SourceHeadName,
            rakeMaterial,
            controller);

        EnsureRakePrefab(
            UpgradedPrefabPath,
            "UpgradedRakeTrapPrefab",
            UpgradedSourcePrefabPath,
            UpgradedSourceHandleName,
            UpgradedSourceHeadName,
            upgradedMaterial,
            controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureRakePrefab(
        string prefabPath,
        string prefabName,
        string sourceAssetPath,
        string sourceHandleName,
        string sourceHeadName,
        Material material,
        RuntimeAnimatorController controller)
    {
        if (sourceAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureModelImport(sourceAssetPath);
        }

        AssetDatabase.ImportAsset(sourceAssetPath, ImportAssetOptions.ForceUpdate);

        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
        if (sourceModel == null)
        {
            throw new FileNotFoundException($"Missing source model at {sourceAssetPath}");
        }

        GameObject root = new GameObject(prefabName);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject mesh = new GameObject("Mesh");
        mesh.transform.SetParent(root.transform, false);

        GameObject pivot = new GameObject("HandlePivot");
        pivot.transform.SetParent(mesh.transform, false);
        pivot.transform.localRotation = Quaternion.identity;

        GameObject head = new GameObject("RakeHead");
        head.transform.SetParent(pivot.transform, false);

        GameObject sourceRoot = UnityEngine.Object.Instantiate(sourceModel);
        sourceRoot.name = prefabName + "Source";
        sourceRoot.transform.position = Vector3.zero;
        sourceRoot.transform.rotation = Quaternion.identity;
        sourceRoot.transform.localScale = Vector3.one;

        Transform sourceHandle = FindRequiredChild(sourceRoot.transform, sourceHandleName);
        Transform sourceHead = FindRequiredChild(sourceRoot.transform, sourceHeadName);
        Bounds headBounds = GetRendererBounds(sourceHead);

        pivot.transform.localPosition = new Vector3(headBounds.center.x, headBounds.min.y, headBounds.min.z);

        sourceHead.SetParent(head.transform, true);
        sourceHandle.SetParent(pivot.transform, true);
        ApplyMaterial(sourceHead, material);
        ApplyMaterial(sourceHandle, material);

        pivot.transform.localRotation = Quaternion.Euler(ArmedHandleAngle, 0f, 0f);
        Bounds armedBounds = GetRendererBounds(root.transform);
        mesh.transform.localPosition = new Vector3(0f, GroundClearance - armedBounds.min.y, 0f);

        UnityEngine.Object.DestroyImmediate(sourceRoot);

        // The game raycasts entity-model blocks against this root collider, and
        // BlockShapeModelEntity reads it once to derive the block's custom bounds.
        Bounds groundedBounds = GetRendererBounds(root.transform);
        BoxCollider targetingCollider = root.AddComponent<BoxCollider>();
        Vector3 colliderSize = groundedBounds.size;
        colliderSize.y = Mathf.Max(colliderSize.y, MinColliderHeight);
        targetingCollider.size = colliderSize;
        targetingCollider.center = new Vector3(groundedBounds.center.x, colliderSize.y * 0.5f, groundedBounds.center.z);

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
        if (importer == null)
        {
            throw new InvalidOperationException($"Could not load prefab importer for {prefabPath}");
        }

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();
    }

    private static RuntimeAnimatorController EnsureAnimatorController()
    {
        string idleClipPath = AnimationsDir + "/RakeTrapIdle.anim";
        string springClipPath = AnimationsDir + "/RakeTrapSpring.anim";
        string controllerPath = AnimationsDir + "/RakeTrap.controller";

        DeleteAssetIfExists(idleClipPath);
        DeleteAssetIfExists(springClipPath);
        DeleteAssetIfExists(controllerPath);

        AnimationClip idle = new AnimationClip
        {
            name = "RakeTrapIdle",
            wrapMode = WrapMode.Once
        };
        SetRotationCurve(idle, AnimationCurve.Constant(0f, 0.1f, ArmedHandleAngle));
        AssetDatabase.CreateAsset(idle, idleClipPath);

        AnimationClip spring = new AnimationClip
        {
            name = "RakeTrapSpring",
            wrapMode = WrapMode.Once
        };
        AnimationCurve springCurve = new AnimationCurve(
            new Keyframe(0.00f, ArmedHandleAngle),
            new Keyframe(0.12f, SprungHandleAngle),
            new Keyframe(0.40f, SprungHandleAngle),
            new Keyframe(4.00f, ArmedHandleAngle));
        SetRotationCurve(spring, springCurve);
        AssetDatabase.CreateAsset(spring, springClipPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter(AnimationTrigger, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        idleState.motion = idle;
        stateMachine.defaultState = idleState;

        AnimatorState springState = stateMachine.AddState("Spring");
        springState.motion = spring;

        AnimatorStateTransition anyToSpring = stateMachine.AddAnyStateTransition(springState);
        anyToSpring.hasExitTime = false;
        anyToSpring.duration = 0f;
        anyToSpring.AddCondition(AnimatorConditionMode.If, 0f, AnimationTrigger);

        AnimatorStateTransition springToIdle = springState.AddTransition(idleState);
        springToIdle.hasExitTime = true;
        springToIdle.exitTime = 1f;
        springToIdle.duration = 0f;

        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
    }

    private static void SetRotationCurve(AnimationClip clip, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("Mesh/HandlePivot", typeof(Transform), "localEulerAnglesRaw.x"),
            curve);
    }

    private static void CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Transform FindRequiredChild(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindRequiredChild(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        if (root.parent == null)
        {
            throw new InvalidOperationException($"Source model is missing transform '{name}'.");
        }

        return null;
    }

    private static Bounds GetRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException($"Transform '{root.name}' has no renderers.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void ApplyMaterial(Transform root, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = material;
        }
    }

    private static void ValidatePrefab(GameObject prefab, string prefabName)
    {
        RequireTransform(prefab.transform, prefabName, "Mesh");
        Transform pivot = RequireTransform(prefab.transform, prefabName, "Mesh/HandlePivot");
        Renderer[] pivotRenderers = pivot.GetComponentsInChildren<Renderer>(true);
        if (pivotRenderers.Length < 2)
        {
            throw new InvalidOperationException($"{prefabName} should have rake head and handle renderers under Mesh/HandlePivot.");
        }

        if (prefab.GetComponent<Animator>() == null)
        {
            throw new InvalidOperationException($"{prefabName} root is missing its Animator.");
        }

        BoxCollider rootCollider = prefab.GetComponent<BoxCollider>();
        if (rootCollider == null)
        {
            throw new InvalidOperationException($"{prefabName} root is missing its targeting BoxCollider.");
        }

        Debug.Log($"{prefabName} collider center={rootCollider.center} size={rootCollider.size}");
        if (rootCollider.size.y < MinColliderHeight - 0.01f || rootCollider.size.z < 1.0f)
        {
            throw new InvalidOperationException($"{prefabName} collider looks too small: {rootCollider.size}");
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException($"{prefabName} has no renderers.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Debug.Log($"{prefabName} renderers={renderers.Length}, bounds center={bounds.center}, size={bounds.size}");
        if (bounds.size.y < 0.05f || bounds.size.z < 1.0f)
        {
            throw new InvalidOperationException($"{prefabName} bounds look too small: {bounds.size}");
        }

        ValidateSpringClearance(prefab, prefabName);
    }

    private static void ValidateSpringClearance(GameObject prefab, string prefabName)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Transform pivot = instance.transform.Find("Mesh/HandlePivot");
            if (pivot == null)
            {
                throw new InvalidOperationException($"{prefabName} is missing transform path Mesh/HandlePivot");
            }

            for (float angle = ArmedHandleAngle; angle <= SprungHandleAngle; angle += 15f)
            {
                pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
                Bounds bounds = GetRendererBounds(instance.transform);
                Debug.Log($"Spring clearance angle={angle}, minY={bounds.min.y}, size={bounds.size}");
                if (bounds.min.y < -0.005f)
                {
                    throw new InvalidOperationException($"{prefabName} dips below ground during spring at angle {angle}: minY={bounds.min.y}");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Material MakeRakeMaterial()
    {
        string materialPath = MaterialsDir + "/RT_RakeSource.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = Shader.Find("Standard");
        }

        Texture2D albedo = LoadTexture("Assets/RakeTrap/SourceAssets/Regular Rake/Rake001_Rake001Mat_BaseColor.png", isNormal: false);
        Texture2D normal = LoadTexture("Assets/RakeTrap/SourceAssets/Regular Rake/Rake001_Rake001Mat_Normal.png", isNormal: true);
        if (albedo == null)
        {
            material.color = new Color(0.43f, 0.24f, 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        material.mainTexture = albedo;
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0.05f);
        material.SetFloat("_Glossiness", 0.35f);
        if (normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadSourceMaterial(string materialPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            return null;
        }

        material.shader = Shader.Find("Standard");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MakeSolidMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = Shader.Find("Standard");
        }

        material.mainTexture = null;
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D LoadTexture(string path, bool isNormal)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }

        if (importer != null)
        {
            bool dirty = false;
            TextureImporterType wanted = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != wanted)
            {
                importer.textureType = wanted;
                dirty = true;
            }

            if (!isNormal && !importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ConfigureSourceTextures()
    {
        foreach (string path in Directory.GetFiles("Assets/RakeTrap/SourceAssets", "*.png", SearchOption.AllDirectories))
        {
            string normalized = path.Replace("\\", "/");
            LoadTexture(normalized, normalized.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    private static void ConfigureModelImport(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
        }

        if (importer == null)
        {
            return;
        }

        bool dirty = false;
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            dirty = true;
        }

        if (importer.animationType != ModelImporterAnimationType.None)
        {
            importer.animationType = ModelImporterAnimationType.None;
            dirty = true;
        }

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            dirty = true;
        }

        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "RakeTrap");
        EnsureFolder("Assets/RakeTrap", "Prefabs");
        EnsureFolder("Assets/RakeTrap", "Materials");
        EnsureFolder("Assets/RakeTrap", "Animations");
        EnsureFolder("Assets/RakeTrap", "SourceAssets");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Transform RequireTransform(Transform root, string prefabName, string path)
    {
        Transform transform = root.Find(path);
        if (transform == null)
        {
            throw new InvalidOperationException($"{prefabName} is missing transform path {path}");
        }

        return transform;
    }

    private static void DumpTransformTree(Transform root, Transform current, int depth)
    {
        Debug.Log($"{new string(' ', depth * 2)}{GetRelativePath(root, current)} pos={current.localPosition} rot={current.localEulerAngles} scale={current.localScale}");
        for (int i = 0; i < current.childCount; i++)
        {
            DumpTransformTree(root, current.GetChild(i), depth + 1);
        }
    }

    private static string GetRelativePath(Transform root, Transform current)
    {
        if (current == root)
        {
            return current.name;
        }

        string path = current.name;
        Transform walker = current.parent;
        while (walker != null && walker != root)
        {
            path = walker.name + "/" + path;
            walker = walker.parent;
        }

        return path;
    }

    private static void DeleteAssetIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
