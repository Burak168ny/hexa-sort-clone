using System.IO;
using HexaSort.Core;
using HexaSort.Utils;
using HexaSort.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HexaSort.Editor
{
    /// <summary>
    /// One-click project setup: renderer, meshes, materials, prefabs, config and the game scene.
    /// Idempotent - assets are updated in place (GUIDs kept) and the scene is regenerated.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Game.unity";
        private const string ConfigPath = Root + "/Settings/VisualConfig.asset";
        private const string RendererPath = "Assets/Settings/HexaSort_UniversalRenderer.asset";
        private const string ShaderName = "HexaSort/Stylized";
        private const string GhostMaterialPath = Root + "/Materials/Ghost.mat";
        private const string ParticleMaterialPath = Root + "/Materials/Particle.mat";
        private const string ShadowMeshPath = Root + "/Art/Meshes/HexShadow.asset";
        private const string TileMeshPath = Root + "/Art/Meshes/HexTile.asset";
        private const string AudioLibraryPath = Root + "/Settings/AudioLibrary.asset";
        private const string FlatQuadMeshPath = Root + "/Art/Meshes/FlatQuad.asset";
        private const string GlowMaterialPath = Root + "/Materials/Glow.mat";
        private const string StackShadowMaterialPath = Root + "/Materials/StackShadow.mat";
        private const string BackGlowMaterialPath = Root + "/Materials/BackGlow.mat";
        private const string PostFxPath = Root + "/Settings/PostFX.asset";

        private static readonly string[] ColorNames = { "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Cyan", "Pink" };

        // Layout of the generated meshes, relative to VisualConfig.HexSize = 0.5.
        private const float TileRadius = 0.44f;
        private const float CellRadius = 0.47f;
        private const float CellHeight = 0.1f;

        [MenuItem("Tools/HexaSort/Setup Game Scene", priority = 0)]
        public static void SetupGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!UIBuilder.EnsureTextMeshProResources()) return;

            EnsureFolders();
            EnsureUniversalRenderer();
            var config = EnsureConfig();
            var meshes = EnsureMeshes(config);
            EnsureMaterials(config);
            UIBuilder.BakeSprites();
            EnsureFeedbackAssets();
            EnsurePrefabs(meshes, config);
            CampaignAssetBuilder.EnsureDatabase();
            BuildScene();
            ConfigurePlayer();

            AssetDatabase.SaveAssets();
            Debug.Log($"[HexaSort] Scene ready at {ScenePath}. Press Play.");
        }

        [MenuItem("Tools/HexaSort/Reset Progress", priority = 20)]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(Model.ProgressService.LevelKey);
            PlayerPrefs.DeleteKey(Model.ProgressService.SoundKey);
            PlayerPrefs.DeleteKey(Model.ProgressService.HapticsKey);
            foreach (var booster in Model.BoosterInventory.All) PlayerPrefs.DeleteKey(Model.BoosterInventory.KeyFor(booster));
            PlayerPrefs.DeleteKey(Model.BoosterInventory.GrantKey);
            PlayerPrefs.Save();
            Debug.Log("[HexaSort] Progress reset: level 1, settings on, boosters back to the starting count.");
        }

        private struct MeshSet
        {
            public Mesh Tile;
            public Mesh Cell;
            public Mesh Shadow;
            public Mesh Quad;
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         "Scripts", "Prefabs", "Levels", "Art", "Art/Meshes", "Art/Shaders", "Materials",
                         "Materials/Tiles", "Scenes", "Settings",
                     })
            {
                Directory.CreateDirectory(Path.Combine(Root, folder));
            }
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The template ships with the 2D renderer. This is a 3D game, so add a forward Universal
        /// renderer and make it the default (the 2D renderer stays in the list, unused).
        /// </summary>
        private static void EnsureUniversalRenderer()
        {
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset pipeline))
            {
                Debug.LogWarning("[HexaSort] No URP asset assigned in Graphics settings; skipping renderer setup.");
                return;
            }

            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            var defaultIndex = so.FindProperty("m_DefaultRendererIndex");

            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData) index = i;

            if (index < 0)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<UniversalRendererData>();
                    data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                    AssetDatabase.CreateAsset(data, RendererPath);
                }
                index = list.arraySize;
                list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = data;
            }

            defaultIndex.intValue = index;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Hex edges alias badly; 4x MSAA is cheap on tile-based mobile GPUs.
            pipeline.msaaSampleCount = 4;
            // The stylized shaders fake their lighting: no HDR buffer and no shadow maps needed on mobile.
            pipeline.supportsHDR = false;
            var assetSo = new SerializedObject(pipeline);
            SetBool(assetSo, "m_MainLightShadowsSupported", false);
            SetBool(assetSo, "m_AdditionalLightShadowsSupported", false);
            assetSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var property = so.FindProperty(field);
            if (property != null) property.boolValue = value;
        }

        private static VisualConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<VisualConfig>(ConfigPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<VisualConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static MeshSet EnsureMeshes(VisualConfig config)
        {
            return new MeshSet
            {
                Tile = SaveMesh(HexMeshBuilder.BuildPrism(TileRadius, config.TileHeight, "HexTile"), TileMeshPath),
                Shadow = SaveMesh(HexMeshBuilder.BuildPrism(TileRadius * 1.05f, 0.004f, "HexShadow"), ShadowMeshPath),
                Quad = SaveMesh(HexMeshBuilder.BuildFlatQuad("FlatQuad"), FlatQuadMeshPath),
                // Shifted down so the cell's top face sits at CellTopY, where stacks rest.
                Cell = SaveMesh(HexMeshBuilder.BuildPrism(CellRadius, CellHeight, "HexCell", config.CellTopY - CellHeight * 0.5f),
                    Root + "/Art/Meshes/HexCell.asset"),
            };
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureMaterials(VisualConfig config)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new FileNotFoundException($"Shader '{ShaderName}' not found. Is HexStylized.shader imported?");

            var palette = config.Palette;
            var tiles = new Material[palette.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                string name = i < ColorNames.Length ? ColorNames[i] : $"Color{i}";
                tiles[i] = SaveMaterial(shader, $"{Root}/Materials/Tiles/Tile_{i:00}_{name}.mat", palette[i], shade: 0.4f, rim: 0.3f,
                    bevel: 0.42f, fresnel: 0.1f);
            }

            var cell = SaveMaterial(shader, Root + "/Materials/Cell.mat", new Color32(0x3A, 0x43, 0x66, 0xFF), 0.45f, 0.1f, 0.14f, 0.06f);
            var highlight = SaveMaterial(shader, Root + "/Materials/CellHighlight.mat", new Color32(0x8F, 0xA6, 0xF0, 0xFF), 0.45f, 0.1f, 0.3f, 0.12f);
            var locked = SaveMaterial(shader, Root + "/Materials/CellLocked.mat", new Color32(0x26, 0x2B, 0x40, 0xFF), 0.45f, 0.1f, 0.08f, 0.06f);

            config.EditorSetMaterials(tiles, cell, highlight, locked);
            EditorUtility.SetDirty(config);
        }

        /// <summary>Transparent materials for drag preview and particles, and the audio library.</summary>
        private static void EnsureFeedbackAssets()
        {
            var ghost = LoadOrCreateMaterial(GhostMaterialPath, "HexaSort/Ghost");
            ghost.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.4f));
            EditorUtility.SetDirty(ghost);

            var particle = LoadOrCreateMaterial(ParticleMaterialPath, "HexaSort/Particle");
            particle.SetTexture("_BaseMap", SpriteBaker.GetOrBake(SpriteBaker.Shape.Hexagon).texture);
            particle.SetColor("_Tint", Color.white);
            EditorUtility.SetDirty(particle);

            // Soft glow texture shared by flashes, contact shadows and the light pool under the board.
            var glowTexture = SpriteBaker.GetOrBake(SpriteBaker.Shape.Glow).texture;
            var glow = LoadOrCreateMaterial(GlowMaterialPath, "HexaSort/Particle");
            glow.SetTexture("_BaseMap", glowTexture);
            glow.SetColor("_Tint", Color.white);
            EditorUtility.SetDirty(glow);
            var stackShadow = LoadOrCreateMaterial(StackShadowMaterialPath, "HexaSort/Particle");
            stackShadow.SetTexture("_BaseMap", glowTexture);
            stackShadow.SetColor("_Tint", new Color(0.05f, 0.06f, 0.16f, 0.42f));
            EditorUtility.SetDirty(stackShadow);
            var backGlow = LoadOrCreateMaterial(BackGlowMaterialPath, "HexaSort/Particle");
            backGlow.SetTexture("_BaseMap", glowTexture);
            backGlow.SetColor("_Tint", new Color(0.78f, 0.84f, 1f, 0.24f));
            EditorUtility.SetDirty(backGlow);

            var library = AssetDatabase.LoadAssetAtPath<Core.Audio.AudioLibrary>(AudioLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<Core.Audio.AudioLibrary>();
                AssetDatabase.CreateAsset(library, AudioLibraryPath);
            }
            library.EnsureAllIds();
            EditorUtility.SetDirty(library);
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new FileNotFoundException($"Shader '{shaderName}' not found.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        private static Material SaveMaterial(Shader shader, string path, Color color, float shade, float rim,
            float bevel = 0.35f, float fresnel = 0.2f)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_ShadeStrength", shade);
            mat.SetFloat("_RimDarken", rim);
            mat.SetFloat("_BevelLight", bevel);
            mat.SetFloat("_BevelWidth", 0.16f);
            mat.SetFloat("_TopGradient", 0.12f);
            mat.SetFloat("_Fresnel", fresnel);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private const string TilePrefabPath = Root + "/Prefabs/Tile.prefab";
        private const string CellPrefabPath = Root + "/Prefabs/Cell.prefab";
        private const string StackPrefabPath = Root + "/Prefabs/Stack.prefab";

        private static void EnsurePrefabs(MeshSet meshes, VisualConfig config)
        {
            var tileGo = NewRenderable("Tile", meshes.Tile, config.GetTileMaterial(0));
            var tile = tileGo.AddComponent<TileView>();
            SetRef(tile, "_renderer", tileGo.GetComponent<MeshRenderer>());

            var cellGo = NewRenderable("Cell", meshes.Cell, config.CellMaterial);
            var cell = cellGo.AddComponent<HexCellView>();
            SetRef(cell, "_renderer", cellGo.GetComponent<MeshRenderer>());
            SetRef(cell, "_lockLabel", NewLockLabel(cellGo.transform, config));

            var stackGo = new GameObject("Stack");
            stackGo.AddComponent<StackView>();
            // Soft contact shadow that stays on the ground and fades as the stack lifts.
            var stackShadow = NewRenderable("Shadow", meshes.Quad, Load<Material>(StackShadowMaterialPath));
            stackShadow.transform.SetParent(stackGo.transform, false);
            stackShadow.AddComponent<BlobShadow>();

            SavePrefab(tileGo, TilePrefabPath);
            SavePrefab(cellGo, CellPrefabPath);
            SavePrefab(stackGo, StackPrefabPath);
        }

        /// <summary>Unlock-score text lying flat on the cell, readable from the tilted camera.</summary>
        private static TMPro.TextMeshPro NewLockLabel(Transform cell, VisualConfig config)
        {
            var go = new GameObject("LockLabel", typeof(RectTransform));
            go.transform.SetParent(cell, false);
            go.transform.localPosition = new Vector3(0f, config.CellTopY + 0.005f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = go.AddComponent<TMPro.TextMeshPro>();
            text.font = TMPro.TMP_Settings.defaultFontAsset;
            text.text = "00";
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.rectTransform.sizeDelta = new Vector2(0.62f, 0.4f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 4f;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            go.SetActive(false);
            return text;
        }

        private static GameObject NewRenderable(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            ConfigureRenderer(go.AddComponent<MeshRenderer>(), material);
            return go;
        }

        private static void ConfigureRenderer(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Opening a scene unloads unused assets, which invalidates references held from before.
            // Load everything the scene wires up only after the scene exists.
            var config = Load<VisualConfig>(ConfigPath);
            var tilePrefab = Load<TileView>(TilePrefabPath);
            var cellPrefab = Load<HexCellView>(CellPrefabPath);
            var stackPrefab = Load<StackView>(StackPrefabPath);

            // Camera: tilted top-down view; CameraFitter frames board + tray per level and screen aspect.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x55, 0x63, 0x9A, 0xFF);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50f;
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 8.7f, -5.7f), Quaternion.Euler(60f, 0f, 0f));
            var cameraData = camGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            var fitter = camGo.AddComponent<CameraFitter>();

            // Post-processing: soft bloom on the glossy highlights, a vignette and a little more punch.
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = EnsurePostFx();

            // A pool of light on the floor under the board and tray.
            var pool = NewRenderable("LightPool", Load<Mesh>(FlatQuadMeshPath), Load<Material>(BackGlowMaterialPath));
            pool.transform.position = new Vector3(0f, -0.12f, -1.5f);
            pool.transform.localScale = new Vector3(15f, 1f, 15f);

            var pools = new GameObject("Pools");
            var tilePool = pools.AddComponent<TilePool>();
            SetRef(tilePool, "_config", config);
            SetRef(tilePool, "_prefab", tilePrefab);
            var stackPool = pools.AddComponent<StackViewPool>();
            SetRef(stackPool, "_config", config);
            SetRef(stackPool, "_tilePool", tilePool);
            SetRef(stackPool, "_prefab", stackPrefab);

            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<BoardView>();
            SetRef(board, "_config", config);
            SetRef(board, "_cellPrefab", cellPrefab);
            SetRef(board, "_stackPool", stackPool);

            var trayGo = new GameObject("Tray");
            trayGo.transform.position = new Vector3(0f, 0f, -3.5f);
            var tray = trayGo.AddComponent<TrayView>();
            SetRef(tray, "_stackPool", stackPool);

            var game = new GameObject("Game");
            var levels = game.AddComponent<LevelManager>();
            SetRef(levels, "_database", Load<Core.Levels.LevelDatabase>(CampaignAssetBuilder.DatabasePath));
            var player = game.AddComponent<MergeSequencePlayer>();
            SetRef(player, "_board", board);
            var drag = game.AddComponent<DragController>();
            SetRef(drag, "_camera", cam);
            SetRef(drag, "_board", board);
            SetRef(drag, "_tray", tray);
            var manager = game.AddComponent<GameManager>();
            SetRef(manager, "_boardView", board);
            SetRef(manager, "_trayView", tray);
            SetRef(manager, "_player", player);
            SetRef(manager, "_drag", drag);
            SetRef(manager, "_levels", levels);
            SetRef(manager, "_cameraFitter", fitter);

            // Drag preview: ghost silhouette on the hovered cell + shadow under the lifted stack.
            var ghostMaterial = Load<Material>(GhostMaterialPath);
            var previewGo = new GameObject("DropPreview");
            var ghost = NewRenderable("Ghost", Load<Mesh>(TileMeshPath), ghostMaterial);
            ghost.transform.SetParent(previewGo.transform, false);
            var shadow = NewRenderable("Shadow", Load<Mesh>(ShadowMeshPath), ghostMaterial);
            shadow.transform.SetParent(previewGo.transform, false);
            var preview = previewGo.AddComponent<DropPreview>();
            SetRef(preview, "_config", config);
            SetRef(preview, "_ghost", ghost.GetComponent<MeshRenderer>());
            SetRef(preview, "_shadow", shadow.GetComponent<MeshRenderer>());
            SetRef(drag, "_preview", preview);

            var particleMaterial = Load<Material>(ParticleMaterialPath);
            var burst = new GameObject("TileBurstFx").AddComponent<TileBurstFx>();
            SetRef(burst, "_material", particleMaterial);
            SetRef(burst, "_glowMaterial", Load<Material>(GlowMaterialPath));

            var audioGo = new GameObject("Audio");
            var audio = audioGo.AddComponent<Core.Audio.AudioManager>();
            SetRef(audio, "_library", Load<Core.Audio.AudioLibrary>(AudioLibraryPath));

            var boosters = game.AddComponent<BoosterController>();
            SetRef(boosters, "_game", manager);
            SetRef(boosters, "_camera", cam);
            SetRef(boosters, "_board", board);
            SetRef(boosters, "_drag", drag);

            var feedback = game.AddComponent<Core.Feedback.FeedbackController>();
            SetRef(feedback, "_game", manager);
            SetRef(feedback, "_player", player);
            SetRef(feedback, "_drag", drag);
            SetRef(feedback, "_camera", fitter);
            SetRef(feedback, "_audio", audio);
            SetRef(feedback, "_burst", burst);
            SetRef(feedback, "_visuals", config);
            SetRef(feedback, "_confettiMaterial", particleMaterial);

            UIBuilder.Build(new UIBuilder.Context
            {
                Game = manager, Boosters = boosters, Player = player, Board = board, Camera = cam, Visuals = config,
            });

            EditorSceneManager.SaveScene(scene, ScenePath);

            // The game scene must be first in the build so it is the one that boots.
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static VolumeProfile EnsurePostFx()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostFxPath);
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0.04f, 0.05f, 0.16f));
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            var grade = GetOrAdd<ColorAdjustments>(profile);
            grade.contrast.Override(8f);
            grade.saturation.Override(12f);
            grade.postExposure.Override(0.05f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Hexa Sort Clone";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException($"[HexaSort] Missing {typeof(T).Name} at {path}.");
            return asset;
        }

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
