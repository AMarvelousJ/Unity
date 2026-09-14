using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZCJ.Shiploader.Editor
{
    public static class SL15SceneBuilder
    {
        public const string RootFolder = "Assets/Shiploader";
        public const string ConfigFolder = RootFolder + "/Config";
        public const string GeneratedFolder = RootFolder + "/Generated";
        public const string MaterialFolder = GeneratedFolder + "/Materials";
        public const string MeshFolder = GeneratedFolder + "/Meshes";
        public const string PrefabFolder = GeneratedFolder + "/Prefabs";
        public const string ProfileFolder = GeneratedFolder + "/Profiles";
        public const string SceneFolder = RootFolder + "/Scenes";
        public const string ConfigPath = ConfigFolder + "/SL15ModelConfig.asset";
        public const string BackendConfigPath = ConfigFolder + "/SL15BackendConfig.asset";
        public const string ScenePath = SceneFolder + "/SL15_Demo.unity";

        private sealed class MaterialSet
        {
            public Material structure;
            public Material structureDark;
            public Material structureLight;
            public Material safety;
            public Material machinery;
            public Material belt;
            public Material chute;
            public Material galvanized;
            public Material wheel;
            public Material glass;
            public Material hull;
            public Material hullLower;
            public Material deck;
            public Material hatch;
            public Material rescue;
            public Material coal;
            public Material ground;
            public Material rail;
            public Material railGuard;
            public Material shipTrim;
            public Material coalParticle;
        }

        private sealed class RigBuildResult
        {
            public GameObject root;
            public ShiploaderRigController controller;
            public ShiploaderEffectsController effects;
            public Transform cameraTarget;
        }

        private readonly struct HoldDefinition
        {
            public readonly string id;
            public readonly float x;
            public readonly float length;
            public readonly float width;
            public readonly float fill;

            public HoldDefinition(string id, float x, float length, float width, float fill)
            {
                this.id = id;
                this.x = x;
                this.length = length;
                this.width = width;
                this.fill = fill;
            }
        }

        [MenuItem("Tools/SL15/Rebuild Demo Scene")]
        public static void RebuildDemoScene()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(ConfigFolder);
            EnsureFolder(SceneFolder);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
            }

            EnsureFolder(GeneratedFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(ProfileFolder);

            SL15ModelConfig config = GetOrCreateConfig();
            SL15BackendConfig backendConfig = GetOrCreateBackendConfig();
            if (!config.Validate(out string validationMessage))
            {
                throw new InvalidOperationException($"SL15 configuration is invalid: {validationMessage}");
            }

            MaterialSet materials = CreateMaterials();
            GameObject environment = BuildEnvironment(config, materials);
            SaveAsConnectedPrefab(environment, PrefabFolder + "/PortEnvironment.prefab");

            HoldDefinition[] leftHolds =
            {
                new("L1", -18f, 7.1f, 5.2f, 0.9f),
                new("L2", -7f, 8f, 5.8f, 0.85f),
                new("L3", 4f, 8f, 5.8f, 0.75f),
                new("L4", 15f, 8f, 5.8f, 0.35f),
            };
            HoldDefinition[] rightHolds =
            {
                new("R1", -22f, 7.3f, 5.35f, 0.15f),
                new("R2", -11f, 8.2f, 6f, 0.55f),
                new("R3", 0f, 8.2f, 6f, 0.85f),
                new("R4", 11f, 8.2f, 6f, 0.75f),
                new("R5", 22f, 8.2f, 6f, 0.3f),
            };

            GameObject vesselLeft = BuildVessel(
                "Vessel-L", new Vector3(10f, 1.6f, -18f), 58f, 8f, 3.2f,
                leftHolds, true, materials);
            SaveAsConnectedPrefab(vesselLeft, PrefabFolder + "/Vessel-L.prefab");

            GameObject vesselRight = BuildVessel(
                "Vessel-R", new Vector3(12f, 1.6f, 24f), 64f, 8.5f, 3.2f,
                rightHolds, false, materials);
            SaveAsConnectedPrefab(vesselRight, PrefabFolder + "/Vessel-R.prefab");

            RigBuildResult rig = BuildShiploader(config, materials);
            SaveAsConnectedPrefab(rig.root, PrefabFolder + "/SL15_Shiploader.prefab");

            BuildLightingAndPostProcessing();
            ShiploaderOrbitCamera orbitCamera = BuildCamera(rig.cameraTarget);
            GameObject bridgeObject = new("SL15_BackendBridge");
            ShiploaderRemoteCoordinator coordinator = bridgeObject.AddComponent<ShiploaderRemoteCoordinator>();
            coordinator.Configure(
                backendConfig,
                rig.controller,
                rig.effects,
                Object.FindObjectsByType<HatchCoverController>(FindObjectsSortMode.None),
                Object.FindObjectsByType<HoldCargoVisualController>(FindObjectsSortMode.None));
            GameObject uiObject = new("SL15_RuntimeUI");
            ShiploaderDemoUI ui = uiObject.AddComponent<ShiploaderDemoUI>();
            ui.Configure(rig.controller, rig.effects, orbitCamera, coordinator);

            Scene scene = SceneManager.GetActiveScene();
            scene.name = "SL15_Demo";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = rig.root;
            SL15VisualRefinement.Apply();
            SL15ReferenceModel.Apply();
            Debug.Log($"SL15 demo scene rebuilt successfully: {ScenePath}");
        }

        private static SL15ModelConfig GetOrCreateConfig()
        {
            SL15ModelConfig config = AssetDatabase.LoadAssetAtPath<SL15ModelConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<SL15ModelConfig>();
            config.ResetToWebDefaults();
            AssetDatabase.CreateAsset(config, ConfigPath);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static SL15BackendConfig GetOrCreateBackendConfig()
        {
            SL15BackendConfig config = AssetDatabase.LoadAssetAtPath<SL15BackendConfig>(BackendConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<SL15BackendConfig>();
            config.ResetToDefaults();
            AssetDatabase.CreateAsset(config, BackendConfigPath);
            EditorUtility.SetDirty(config);
            return config;
        }

        private static MaterialSet CreateMaterials()
        {
            Texture2D beltTexture = CreateBeltTexture();
            return new MaterialSet
            {
                structure = CreateLitMaterial("Structure_Yellow", new Color32(231, 167, 42, 255), 0.24f, 0.35f),
                structureDark = CreateLitMaterial("Structure_Dark", new Color32(82, 94, 105, 255), 0.42f, 0.31f),
                structureLight = CreateLitMaterial("Structure_Light", new Color32(222, 230, 235, 255), 0.28f, 0.36f),
                safety = CreateLitMaterial("Safety_Yellow", new Color32(244, 196, 48, 255), 0.12f, 0.32f),
                machinery = CreateLitMaterial("Machinery", new Color32(45, 55, 65, 255), 0.55f, 0.42f),
                belt = CreateLitMaterial("Conveyor_Belt", new Color32(21, 25, 29, 255), 0.02f, 0.18f, false, beltTexture),
                chute = CreateLitMaterial("Chute_BlueGrey", new Color32(63, 95, 115, 255), 0.36f, 0.38f),
                galvanized = CreateLitMaterial("Galvanized_Steel", new Color32(194, 207, 215, 255), 0.72f, 0.48f),
                wheel = CreateLitMaterial("Wheel_Rubber", new Color32(28, 33, 38, 255), 0.25f, 0.22f),
                glass = CreateLitMaterial("Cab_Glass", new Color32(126, 190, 209, 150), 0.05f, 0.82f, true),
                hull = CreateLitMaterial("Ship_Hull", new Color32(22, 29, 43, 255), 0.28f, 0.29f),
                hullLower = CreateLitMaterial("Ship_Antifouling", new Color32(132, 45, 39, 255), 0.12f, 0.24f),
                deck = CreateLitMaterial("Ship_Deck", new Color32(45, 82, 70, 255), 0.22f, 0.3f),
                hatch = CreateLitMaterial("Ship_HatchCover", new Color32(102, 112, 113, 255), 0.32f, 0.31f),
                rescue = CreateLitMaterial("Ship_RescueOrange", new Color32(232, 91, 38, 255), 0.12f, 0.34f),
                coal = CreateLitMaterial("Coal", new Color32(7, 8, 9, 255), 0.03f, 0.08f),
                ground = CreateLitMaterial("Concrete_Ground", new Color32(191, 201, 209, 255), 0.08f, 0.24f),
                rail = CreateLitMaterial("Rail_Steel", new Color32(26, 34, 46, 255), 0.78f, 0.42f),
                railGuard = CreateLitMaterial("Rail_Guard_Orange", new Color32(249, 115, 22, 255), 0.18f, 0.36f),
                shipTrim = CreateLitMaterial("Ship_Trim", new Color32(236, 241, 245, 255), 0.18f, 0.4f),
                coalParticle = CreateParticleMaterial(),
            };
        }

        private static Texture2D CreateBeltTexture()
        {
            Texture2D texture = new(64, 64, TextureFormat.RGBA32, false, true)
            {
                name = "Belt_Stripes",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            Color dark = new(0.035f, 0.045f, 0.055f, 1f);
            Color stripe = new(0.12f, 0.14f, 0.16f, 1f);
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                bool isStripe = (y / 8) % 2 == 0;
                for (int x = 0; x < 64; x++)
                {
                    float edge = x < 5 || x > 58 ? 0.55f : 1f;
                    pixels[y * 64 + x] = Color.Lerp(dark, isStripe ? stripe : dark, edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, MaterialFolder + "/Belt_Stripes.asset");
            return texture;
        }

        private static Material CreateLitMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            bool transparent = false,
            Texture texture = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { name = name, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
            }
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, MaterialFolder + $"/{name}.mat");
            return material;
        }

        private static Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            Material material = new(shader) { name = "Coal_Particles" };
            Color color = new Color32(12, 12, 12, 255);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, MaterialFolder + "/Coal_Particles.mat");
            return material;
        }

        private static GameObject BuildEnvironment(SL15ModelConfig config, MaterialSet materials)
        {
            GameObject root = new("PortEnvironment");
            CreateBox(root.transform, "ConcreteApron", new Vector3(10f, -0.13f, 3f), new Vector3(150f, 0.24f, 86f), materials.ground);
            float halfGauge = config.railGauge * 0.5f;
            foreach (float z in new[] { -halfGauge, halfGauge })
            {
                CreateBox(root.transform, "CraneRail", new Vector3(10f, 0.18f, z), new Vector3(118f, 0.36f, 0.32f), materials.rail);
            }
            foreach (float z in new[] { -halfGauge - 1.15f, halfGauge + 1.15f })
            {
                CreateBox(root.transform, "SafetyRail", new Vector3(10f, 0.11f, z), new Vector3(120f, 0.2f, 0.18f), materials.railGuard);
            }
            for (float x = -48f; x <= 68.01f; x += 2.5f)
            {
                CreateBox(root.transform, "RailSleeper", new Vector3(x, 0.06f, 0f), new Vector3(0.28f, 0.14f, config.railGauge + 2.8f), materials.rail);
            }
            SetStaticRecursively(root);
            return root;
        }

        private static GameObject BuildVessel(
            string vesselId,
            Vector3 position,
            float length,
            float width,
            float height,
            IReadOnlyList<HoldDefinition> holds,
            bool slidingCovers,
            MaterialSet materials)
        {
            GameObject root = new(vesselId);
            root.transform.position = position;

            Mesh hullMesh = CreateHullMesh(vesselId, length, width, height);
            GameObject hull = new("Hull");
            hull.transform.SetParent(root.transform, false);
            hull.AddComponent<MeshFilter>().sharedMesh = hullMesh;
            hull.AddComponent<MeshRenderer>().sharedMaterials = new[] { materials.hullLower, materials.hull };

            Mesh deckMesh = CreateVesselDeckMesh(vesselId, length, width, 0.14f);
            GameObject deck = new("MainDeck");
            deck.transform.SetParent(root.transform, false);
            deck.transform.localPosition = new Vector3(0f, height * 0.5f + 0.07f, 0f);
            deck.AddComponent<MeshFilter>().sharedMesh = deckMesh;
            deck.AddComponent<MeshRenderer>().sharedMaterial = materials.deck;

            CreateSphere(root.transform, "BulbousBow", new Vector3(length * 0.495f, -height * 0.26f, 0f),
                new Vector3(length * 0.055f, height * 0.42f, width * 0.28f), materials.hullLower);

            foreach (HoldDefinition hold in holds)
            {
                BuildHold(root.transform, vesselId, hold, height, slidingCovers, materials);
            }

            Transform details = NewChild(root.transform, "VesselVisualDetails");
            NewChild(details, "VIS_GeneralArrangementV3");
            BuildForecastle(details, length, width, height, materials);
            BuildAftAccommodation(details, length, width, height, materials);
            BuildDeckCranes(details, length, width, height, holds, materials);
            BuildDeckEquipment(details, length, width, height, holds, materials);
            BuildSideWalkwaysAndMarkings(details, length, width, height, materials);
            return root;
        }

        private static void BuildHold(
            Transform vessel,
            string vesselId,
            HoldDefinition hold,
            float vesselHeight,
            bool slidingCover,
            MaterialSet materials)
        {
            GameObject holdRoot = new($"Hold_{hold.id}");
            holdRoot.transform.SetParent(vessel, false);
            holdRoot.transform.localPosition = new Vector3(hold.x, vesselHeight * 0.5f + 0.19f, 0f);
            float rimHeight = 0.38f;
            float rimWidth = 0.22f;
            Renderer[] coamings =
            {
                CreateBox(holdRoot.transform, "Coaming_Port", new Vector3(0f, rimHeight * 0.5f, -hold.width * 0.5f),
                    new Vector3(hold.length, rimHeight, rimWidth), materials.structureDark).GetComponent<Renderer>(),
                CreateBox(holdRoot.transform, "Coaming_Starboard", new Vector3(0f, rimHeight * 0.5f, hold.width * 0.5f),
                    new Vector3(hold.length, rimHeight, rimWidth), materials.structureDark).GetComponent<Renderer>(),
                CreateBox(holdRoot.transform, "Coaming_Aft", new Vector3(-hold.length * 0.5f, rimHeight * 0.5f, 0f),
                    new Vector3(rimWidth, rimHeight, hold.width), materials.structureDark).GetComponent<Renderer>(),
                CreateBox(holdRoot.transform, "Coaming_Forward", new Vector3(hold.length * 0.5f, rimHeight * 0.5f, 0f),
                    new Vector3(rimWidth, rimHeight, hold.width), materials.structureDark).GetComponent<Renderer>(),
            };
            CreateBox(holdRoot.transform, "CoamingTop_Port", new Vector3(0f, rimHeight + 0.025f, -hold.width * 0.5f),
                new Vector3(hold.length + 0.12f, 0.05f, rimWidth + 0.1f), materials.galvanized);
            CreateBox(holdRoot.transform, "CoamingTop_Starboard", new Vector3(0f, rimHeight + 0.025f, hold.width * 0.5f),
                new Vector3(hold.length + 0.12f, 0.05f, rimWidth + 0.1f), materials.galvanized);
            float coalHeight = Mathf.Lerp(0.06f, 0.3f, hold.fill);
            GameObject coalSurface = CreateBox(holdRoot.transform, "CoalSurface", new Vector3(0f, 0.03f + coalHeight * 0.5f, 0f),
                new Vector3(hold.length * 0.88f, coalHeight, hold.width * 0.82f), materials.coal);
            HoldCargoVisualController cargo = holdRoot.AddComponent<HoldCargoVisualController>();
            cargo.Configure(vesselId, hold.id, coalSurface.transform, coamings, 1.35f, hold.fill);

            GameObject coverRoot = new("HatchCover");
            coverRoot.transform.SetParent(holdRoot.transform, false);
            coverRoot.transform.localPosition = new Vector3(0f, rimHeight + 0.12f, 0f);
            HatchCoverController controller = coverRoot.AddComponent<HatchCoverController>();

            if (slidingCover)
            {
                Transform[] panels = new Transform[2];
                Vector3[] closed = new Vector3[2];
                Vector3[] opened = new Vector3[2];
                Vector3[] rotations = { Vector3.zero, Vector3.zero };
                for (int index = 0; index < 2; index++)
                {
                    float sign = index == 0 ? -1f : 1f;
                    Transform panel = NewChild(coverRoot.transform, $"SlidingPanel_{index + 1}");
                    float panelLength = hold.length * 0.96f;
                    float panelWidth = hold.width * 0.46f;
                    CreateBox(panel, "PanelPlate", Vector3.zero,
                        new Vector3(panelLength, 0.18f, panelWidth), materials.hatch);
                    AddHatchPanelRibs(panel, Vector3.zero, panelLength, panelWidth, 6, materials.galvanized);
                    panels[index] = panel;
                    closed[index] = new Vector3(0f, 0f, sign * hold.width * 0.245f);
                    opened[index] = new Vector3(0f, 0f, sign * hold.width * 0.78f);
                }
                controller.Configure(vesselId, hold.id, panels, closed, rotations, opened, rotations, true);
            }
            else
            {
                Transform[] pivots = new Transform[2];
                Vector3[] positions = new Vector3[2];
                Vector3[] closedRotations = { Vector3.zero, Vector3.zero };
                Vector3[] openRotations = { new(-90f, 0f, 0f), new(90f, 0f, 0f) };
                for (int index = 0; index < 2; index++)
                {
                    float sign = index == 0 ? -1f : 1f;
                    GameObject pivot = new($"FoldingPanelPivot_{index + 1}");
                    pivot.transform.SetParent(coverRoot.transform, false);
                    pivot.transform.localPosition = new Vector3(0f, 0f, sign * hold.width * 0.5f);
                    float panelLength = hold.length * 0.96f;
                    float panelWidth = hold.width * 0.49f;
                    Vector3 panelCenter = new(0f, 0f, -sign * hold.width * 0.245f);
                    CreateBox(pivot.transform, "PanelPlate", panelCenter,
                        new Vector3(panelLength, 0.2f, panelWidth), materials.hatch);
                    AddHatchPanelRibs(pivot.transform, panelCenter, panelLength, panelWidth, 4, materials.galvanized);
                    pivots[index] = pivot.transform;
                    positions[index] = pivot.transform.localPosition;
                }
                controller.Configure(vesselId, hold.id, pivots, positions, closedRotations, positions, openRotations, true);
            }
        }

        private static Mesh CreateHullMesh(string vesselId, float length, float width, float height)
        {
            float[] stations = { -0.5f, -0.475f, -0.41f, -0.32f, 0.32f, 0.4f, 0.465f, 0.5f };
            float[] breadths = { 0.31f, 0.43f, 0.49f, 0.5f, 0.5f, 0.46f, 0.27f, 0.025f };
            float[] bottomRise = { 0.1f, 0.04f, 0f, 0f, 0f, 0f, 0.08f, 0.28f };
            float[] sheerRise = { 0.08f, 0.035f, 0.012f, 0f, 0f, 0.02f, 0.07f, 0.13f };
            List<Vector3> vertices = new();
            for (int i = 0; i < stations.Length; i++)
            {
                float x = stations[i] * length;
                float halfWidth = breadths[i] * width;
                float bottomY = -height * 0.5f + bottomRise[i] * height;
                float chineY = -height * 0.08f + bottomRise[i] * height * 0.2f;
                float sheerY = height * 0.5f + sheerRise[i] * height;
                vertices.Add(new Vector3(x, bottomY, -halfWidth * 0.52f));
                vertices.Add(new Vector3(x, bottomY, halfWidth * 0.52f));
                vertices.Add(new Vector3(x, chineY, -halfWidth * 0.9f));
                vertices.Add(new Vector3(x, chineY, halfWidth * 0.9f));
                vertices.Add(new Vector3(x, sheerY, -halfWidth));
                vertices.Add(new Vector3(x, sheerY, halfWidth));
            }

            List<int> lowerTriangles = new();
            List<int> upperTriangles = new();
            for (int i = 0; i < stations.Length - 1; i++)
            {
                int a = i * 6;
                int b = (i + 1) * 6;
                AddQuad(lowerTriangles, a, a + 2, b + 2, b);
                AddQuad(lowerTriangles, a + 1, b + 1, b + 3, a + 3);
                AddQuad(lowerTriangles, a, b, b + 1, a + 1);
                AddQuad(upperTriangles, a + 2, a + 4, b + 4, b + 2);
                AddQuad(upperTriangles, a + 3, b + 3, b + 5, a + 5);
                AddQuad(upperTriangles, a + 4, a + 5, b + 5, b + 4);
            }
            AddQuad(lowerTriangles, 0, 1, 3, 2);
            AddQuad(upperTriangles, 2, 3, 5, 4);
            int end = (stations.Length - 1) * 6;
            AddQuad(lowerTriangles, end, end + 2, end + 3, end + 1);
            AddQuad(upperTriangles, end + 2, end + 4, end + 5, end + 3);

            Mesh mesh = new() { name = $"{vesselId}_HullMesh" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(lowerTriangles, 0);
            mesh.SetTriangles(upperTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshFolder + $"/{vesselId}_Hull.asset");
            return mesh;
        }

        private static Mesh CreateVesselDeckMesh(string vesselId, float length, float width, float thickness)
        {
            float[] stations = { -0.49f, -0.455f, -0.39f, -0.32f, 0.32f, 0.4f, 0.465f, 0.495f };
            float[] breadths = { 0.3f, 0.42f, 0.475f, 0.48f, 0.48f, 0.435f, 0.255f, 0.02f };
            float[] sheerRise = { 0.18f, 0.08f, 0.025f, 0f, 0f, 0.035f, 0.14f, 0.25f };
            List<Vector3> vertices = new();
            for (int index = 0; index < stations.Length; index++)
            {
                float x = stations[index] * length;
                float halfWidth = breadths[index] * width;
                float y = sheerRise[index];
                vertices.Add(new Vector3(x, y - thickness * 0.5f, -halfWidth));
                vertices.Add(new Vector3(x, y - thickness * 0.5f, halfWidth));
                vertices.Add(new Vector3(x, y + thickness * 0.5f, -halfWidth));
                vertices.Add(new Vector3(x, y + thickness * 0.5f, halfWidth));
            }

            List<int> triangles = new();
            for (int index = 0; index < stations.Length - 1; index++)
            {
                int a = index * 4;
                int b = (index + 1) * 4;
                AddQuad(triangles, a, a + 2, b + 2, b);
                AddQuad(triangles, a + 1, b + 1, b + 3, a + 3);
                AddQuad(triangles, a + 2, a + 3, b + 3, b + 2);
                AddQuad(triangles, a, b, b + 1, a + 1);
            }
            AddQuad(triangles, 0, 1, 3, 2);
            int end = (stations.Length - 1) * 4;
            AddQuad(triangles, end, end + 2, end + 3, end + 1);

            Mesh mesh = new() { name = $"{vesselId}_DeckMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshFolder + $"/{vesselId}_Deck.asset");
            return mesh;
        }

        private static void AddHatchPanelRibs(
            Transform parent,
            Vector3 center,
            float panelLength,
            float panelWidth,
            int ribCount,
            Material material)
        {
            for (int rib = 0; rib < ribCount; rib++)
            {
                float t = ribCount <= 1 ? 0.5f : rib / (ribCount - 1f);
                float x = center.x + Mathf.Lerp(-panelLength * 0.42f, panelLength * 0.42f, t);
                CreateBox(parent, $"PanelRib_{rib + 1:00}", new Vector3(x, center.y + 0.13f, center.z),
                    new Vector3(0.075f, 0.075f, panelWidth * 0.96f), material);
            }
        }

        private static void BuildForecastle(
            Transform parent,
            float length,
            float width,
            float height,
            MaterialSet materials)
        {
            Transform forecastle = NewChild(parent, "ForecastleAssembly");
            float deckY = height * 0.5f + 0.16f;
            float centerX = length * 0.435f;
            float forecastleLength = length * 0.105f;
            float forecastleWidth = width * 0.72f;

            CreateBox(forecastle, "ForecastleDeck", new Vector3(centerX, deckY + 0.16f, 0f),
                new Vector3(forecastleLength, 0.28f, forecastleWidth), materials.deck);
            AddPerimeterRailing(forecastle, new Vector3(centerX, deckY + 0.32f, 0f),
                forecastleLength, forecastleWidth, 0.42f, materials.galvanized);
            CreateBox(forecastle, "WaveBreakwater", new Vector3(length * 0.386f, deckY + 0.58f, 0f),
                new Vector3(0.18f, 0.72f, width * 0.68f), materials.shipTrim);

            foreach (float zSign in new[] { -1f, 1f })
            {
                float z = zSign * width * 0.22f;
                CreateCylinder(forecastle, "AnchorWindlass", new Vector3(length * 0.444f, deckY + 0.55f, z),
                    0.24f, 0.32f, materials.machinery, 16);
                CreateCylinder(forecastle, "MooringCapstan", new Vector3(length * 0.465f, deckY + 0.48f, zSign * width * 0.1f),
                    0.16f, 0.3f, materials.galvanized, 16);
                CreateSphere(forecastle, "AnchorRecess", new Vector3(length * 0.462f, height * 0.15f, zSign * width * 0.285f),
                    new Vector3(0.58f, 0.58f, 0.18f), materials.machinery);
            }

            Vector3 mastBase = new(length * 0.452f, deckY + 0.32f, 0f);
            Vector3 mastTop = new(length * 0.452f, deckY + 2.55f, 0f);
            CreateBeam(forecastle, "Foremast", mastBase, mastTop, 0.11f, materials.galvanized);
            CreateBeam(forecastle, "ForemastCrossTree", mastTop + new Vector3(0f, -0.35f, -0.62f),
                mastTop + new Vector3(0f, -0.35f, 0.62f), 0.06f, materials.galvanized);
            CreateBox(forecastle, "ForeRadar", mastTop + new Vector3(0f, -0.05f, 0f),
                new Vector3(1.1f, 0.08f, 0.12f), materials.shipTrim);
        }

        private static void BuildAftAccommodation(
            Transform parent,
            float length,
            float width,
            float height,
            MaterialSet materials)
        {
            Transform accommodation = NewChild(parent, "AftAccommodationAssembly");
            float deckY = height * 0.5f + 0.16f;
            float centerX = -length * 0.435f;
            float tierHeight = Mathf.Max(0.46f, height * 0.17f);
            float topY = deckY;

            CreateBox(accommodation, "PoopDeck", new Vector3(centerX, deckY + 0.13f, 0f),
                new Vector3(length * 0.105f, 0.24f, width * 0.82f), materials.deck);

            for (int level = 0; level < 4; level++)
            {
                float levelT = level / 3f;
                float levelLength = length * Mathf.Lerp(0.088f, 0.064f, levelT);
                float levelWidth = width * Mathf.Lerp(0.78f, 0.68f, levelT);
                float y = deckY + 0.25f + tierHeight * (level + 0.5f);
                CreateBox(accommodation, $"AccommodationDeck_{level + 1}", new Vector3(centerX, y, 0f),
                    new Vector3(levelLength, tierHeight * 0.88f, levelWidth), materials.shipTrim);

                for (int window = -2; window <= 2; window++)
                {
                    float z = window * levelWidth * 0.16f;
                    CreateBox(accommodation, $"FrontWindow_L{level + 1}_{window + 3}",
                        new Vector3(centerX + levelLength * 0.505f, y + 0.04f, z),
                        new Vector3(0.055f, tierHeight * 0.28f, levelWidth * 0.1f), materials.glass);
                }
                foreach (float zSign in new[] { -1f, 1f })
                {
                    for (int window = -1; window <= 1; window++)
                    {
                        float x = centerX + window * levelLength * 0.22f;
                        CreateBox(accommodation, $"SideWindow_L{level + 1}",
                            new Vector3(x, y + 0.04f, zSign * levelWidth * 0.505f),
                            new Vector3(levelLength * 0.12f, tierHeight * 0.26f, 0.055f), materials.glass);
                    }
                }
                topY = y + tierHeight * 0.5f;
            }

            CreateBox(accommodation, "BridgeWing", new Vector3(centerX + length * 0.006f, topY + 0.16f, 0f),
                new Vector3(length * 0.075f, 0.24f, width * 0.96f), materials.shipTrim);
            CreateBox(accommodation, "Wheelhouse", new Vector3(centerX + length * 0.004f, topY + 0.45f, 0f),
                new Vector3(length * 0.058f, 0.42f, width * 0.64f), materials.shipTrim);
            for (int window = -3; window <= 3; window++)
            {
                CreateBox(accommodation, "BridgeWindow", new Vector3(centerX + length * 0.0335f, topY + 0.48f, window * width * 0.075f),
                    new Vector3(0.06f, 0.18f, width * 0.055f), materials.glass);
            }
            AddPerimeterRailing(accommodation, new Vector3(centerX + length * 0.006f, topY + 0.3f, 0f),
                length * 0.075f, width * 0.96f, 0.38f, materials.galvanized);

            float funnelX = -length * 0.475f;
            CreateCylinder(accommodation, "Funnel", new Vector3(funnelX, deckY + 1.75f, 0f),
                width * 0.07f, 2.4f, materials.shipTrim, 20);
            CreateCylinder(accommodation, "FunnelBand", new Vector3(funnelX, deckY + 2.22f, 0f),
                width * 0.073f, 0.4f, materials.hull, 20);
            CreateCylinder(accommodation, "FunnelCap", new Vector3(funnelX, deckY + 2.95f, 0f),
                width * 0.066f, 0.24f, materials.machinery, 20);

            Vector3 mastBase = new(centerX + length * 0.008f, topY + 0.66f, 0f);
            Vector3 mastTop = mastBase + new Vector3(0f, 2.4f, 0f);
            CreateBeam(accommodation, "RadarMast", mastBase, mastTop, 0.1f, materials.galvanized);
            CreateBeam(accommodation, "RadarCrossTree", mastTop + new Vector3(0f, -0.65f, -0.85f),
                mastTop + new Vector3(0f, -0.65f, 0.85f), 0.06f, materials.galvanized);
            CreateBox(accommodation, "MainRadar", mastTop + new Vector3(0f, -0.18f, 0f),
                new Vector3(1.65f, 0.08f, 0.12f), materials.shipTrim);
            CreateSphere(accommodation, "NavigationDome", mastTop + new Vector3(0f, -0.75f, 0.55f),
                new Vector3(0.34f, 0.34f, 0.34f), materials.shipTrim);

            foreach (float zSign in new[] { -1f, 1f })
            {
                Vector3 boatPosition = new(centerX - length * 0.004f, deckY + 1.05f, zSign * width * 0.54f);
                CreateSphere(accommodation, "Lifeboat", boatPosition,
                    new Vector3(length * 0.043f, 0.52f, width * 0.14f), materials.rescue);
                CreateBeam(accommodation, "LifeboatDavit", boatPosition + new Vector3(-0.8f, 0.35f, -zSign * 0.3f),
                    boatPosition + new Vector3(-0.8f, 1.25f, -zSign * 0.75f), 0.075f, materials.galvanized);
                CreateBeam(accommodation, "LifeboatDavit", boatPosition + new Vector3(0.8f, 0.35f, -zSign * 0.3f),
                    boatPosition + new Vector3(0.8f, 1.25f, -zSign * 0.75f), 0.075f, materials.galvanized);
            }
        }

        private static void BuildDeckCranes(
            Transform parent,
            float length,
            float width,
            float height,
            IReadOnlyList<HoldDefinition> holds,
            MaterialSet materials)
        {
            Transform cranes = NewChild(parent, "DeckCranes");
            HoldDefinition[] orderedHolds = holds.OrderBy(item => item.x).ToArray();
            float deckY = height * 0.5f + 0.16f;
            for (int index = 0; index < orderedHolds.Length - 1; index++)
            {
                float craneX = (orderedHolds[index].x + orderedHolds[index + 1].x) * 0.5f;
                Transform crane = NewChild(cranes, $"DeckCrane_{index + 1:00}");
                crane.localPosition = new Vector3(craneX, 0f, 0f);

                CreateCylinder(crane, "Pedestal", new Vector3(0f, deckY + 0.58f, 0f),
                    0.34f, 1.12f, materials.galvanized, 18);
                CreateBox(crane, "SlewHouse", new Vector3(0f, deckY + 1.2f, 0f),
                    new Vector3(0.95f, 0.58f, 0.92f), materials.structure);
                CreateBox(crane, "OperatorCab", new Vector3(0.18f, deckY + 1.58f, width * 0.075f),
                    new Vector3(0.7f, 0.48f, 0.48f), materials.shipTrim);
                CreateBox(crane, "OperatorWindow", new Vector3(0.545f, deckY + 1.62f, width * 0.075f),
                    new Vector3(0.035f, 0.24f, 0.34f), materials.glass);

                Vector3 boomPivot = new(0f, deckY + 1.75f, 0f);
                Vector3 mastTop = new(0f, deckY + 2.95f, 0f);
                float xSign = index % 2 == 0 ? 1f : -1f;
                float zSign = index % 2 == 0 ? -1f : 1f;
                Vector3 boomTip = new(xSign * length * 0.055f, deckY + 3.55f, zSign * width * 0.24f);
                CreateBeam(crane, "CraneMast", boomPivot, mastTop, 0.14f, materials.structure);
                CreateBeam(crane, "CraneBoom", boomPivot, boomTip, 0.16f, materials.structure);
                CreateBeam(crane, "BoomSupportCable", mastTop, boomTip, 0.035f, materials.galvanized);
                CreateBeam(crane, "HookCable", boomTip, new Vector3(boomTip.x, deckY + 0.65f, boomTip.z),
                    0.025f, materials.machinery);
                CreateSphere(crane, "CargoHook", new Vector3(boomTip.x, deckY + 0.58f, boomTip.z),
                    new Vector3(0.18f, 0.28f, 0.18f), materials.machinery);
            }
        }

        private static void BuildDeckEquipment(
            Transform parent,
            float length,
            float width,
            float height,
            IReadOnlyList<HoldDefinition> holds,
            MaterialSet materials)
        {
            Transform equipment = NewChild(parent, "DeckEquipment");
            float deckY = height * 0.5f + 0.2f;
            foreach (HoldDefinition hold in holds)
            {
                foreach (float zSign in new[] { -1f, 1f })
                {
                    float z = zSign * width * 0.41f;
                    CreateCylinder(equipment, "CargoVent", new Vector3(hold.x, deckY + 0.28f, z),
                        0.12f, 0.5f, materials.galvanized, 14);
                    CreateSphere(equipment, "CargoVentCap", new Vector3(hold.x, deckY + 0.56f, z),
                        new Vector3(0.32f, 0.16f, 0.32f), materials.shipTrim);
                }
            }

            foreach (float xSign in new[] { -1f, 1f })
            {
                foreach (float zSign in new[] { -1f, 1f })
                {
                    float x = xSign * length * 0.405f;
                    float z = zSign * width * 0.24f;
                    CreateCylinder(equipment, "MooringWinch", new Vector3(x, deckY + 0.22f, z),
                        0.2f, 0.36f, materials.machinery, 16);
                    CreateCylinder(equipment, "MooringBollard", new Vector3(x + xSign * 0.8f, deckY + 0.18f, z),
                        0.11f, 0.32f, materials.galvanized, 14);
                }
            }
        }

        private static void BuildSideWalkwaysAndMarkings(
            Transform parent,
            float length,
            float width,
            float height,
            MaterialSet materials)
        {
            Transform sideDetails = NewChild(parent, "SideWalkwaysAndMarkings");
            float deckY = height * 0.5f + 0.2f;
            float aftX = -length * 0.36f;
            float foreX = length * 0.4f;
            foreach (float zSign in new[] { -1f, 1f })
            {
                float z = zSign * width * 0.465f;
                CreateBox(sideDetails, "SideWalkway", new Vector3((aftX + foreX) * 0.5f, deckY, z),
                    new Vector3(foreX - aftX, 0.1f, 0.34f), materials.galvanized);
                CreateBeam(sideDetails, "SideRailTop", new Vector3(aftX, deckY + 0.55f, zSign * width * 0.49f),
                    new Vector3(foreX, deckY + 0.55f, zSign * width * 0.49f), 0.045f, materials.galvanized);
                CreateBeam(sideDetails, "SideRailMid", new Vector3(aftX, deckY + 0.3f, zSign * width * 0.49f),
                    new Vector3(foreX, deckY + 0.3f, zSign * width * 0.49f), 0.035f, materials.galvanized);
                for (int post = 0; post <= 18; post++)
                {
                    float x = Mathf.Lerp(aftX, foreX, post / 18f);
                    CreateBeam(sideDetails, "SideRailPost", new Vector3(x, deckY, zSign * width * 0.49f),
                        new Vector3(x, deckY + 0.56f, zSign * width * 0.49f), 0.035f, materials.galvanized);
                }

                CreateBox(sideDetails, "BootStripe", new Vector3(-length * 0.01f, -height * 0.06f, zSign * width * 0.501f),
                    new Vector3(length * 0.59f, 0.065f, 0.035f), materials.shipTrim);
                for (int mark = 0; mark < 3; mark++)
                {
                    CreateBox(sideDetails, "LoadLineMark", new Vector3(length * 0.16f,
                            -height * 0.2f + mark * 0.18f, zSign * width * 0.503f),
                        new Vector3(0.7f - mark * 0.12f, 0.045f, 0.04f), materials.shipTrim);
                }
            }
        }

        private static RigBuildResult BuildShiploader(SL15ModelConfig config, MaterialSet materials)
        {
            List<Renderer> beltRenderers = new();
            GameObject root = new("SL_ShipLoaderRoot");
            Transform travel = NewChild(root.transform, "SL_TravelAssembly");
            BuildPortal(travel, config, materials, beltRenderers);

            Transform upper = NewChild(root.transform, "SL_UpperSlewAssembly");
            upper.localPosition = new Vector3(0f, config.baseHeight, 0f);
            UpperResult upperResult = BuildUpperAssembly(upper, config, materials, beltRenderers);

            Transform chute = NewChild(root.transform, "SL_ChuteAssembly");
            ChuteResult chuteResult = BuildChute(chute, config, materials);

            Transform slewCenter = NewMarker(root.transform, "MK_SLEW_CENTER", new Vector3(0f, config.baseHeight, 0f));
            Transform boomRoot = NewMarker(upperResult.luffPivot, "MK_BOOM_ROOT", Vector3.zero);
            Transform fixedTip = NewMarker(upperResult.luffPivot, "MK_FIXED_BOOM_TIP", new Vector3(0f, 0f, config.fixedBoomLength));
            Transform boomTip = NewMarker(upperResult.boomHead, "MK_BOOM_TIP", Vector3.zero);
            Transform chuteTop = NewMarker(chute, "MK_CHUTE_TOP", Vector3.zero);
            Transform chuteBottom = NewMarker(chute, "MK_CHUTE_BOTTOM", new Vector3(0f, -config.PresentationChuteLength, 0f));
            Transform discharge = NewMarker(chute, "MK_DISCHARGE", new Vector3(0f, -config.PresentationChuteLength - config.dischargeDrop, 0f));

            Transform cameraTarget = NewMarker(root.transform, "CameraTarget", new Vector3(0f, 9f, 6f));
            ShiploaderRigController controller = root.AddComponent<ShiploaderRigController>();
            controller.Configure(
                config, upper, upperResult.luffPivot, upperResult.telescopicScaleRoot, upperResult.boomHead,
                chute, chuteResult.yaw, upperResult.luffRopeFront, upperResult.mastTop,
                slewCenter, boomRoot, fixedTip, boomTip, chuteTop, chuteBottom, discharge);

            ShiploaderEffectsController effects = root.AddComponent<ShiploaderEffectsController>();
            effects.Configure(beltRenderers.ToArray(), chuteResult.coalFlow);
            controller.ResetPose();

            return new RigBuildResult
            {
                root = root,
                controller = controller,
                effects = effects,
                cameraTarget = cameraTarget,
            };
        }

        private sealed class UpperResult
        {
            public Transform luffPivot;
            public Transform telescopicScaleRoot;
            public Transform boomHead;
            public Transform luffRopeFront;
            public Transform mastTop;
        }

        private sealed class ChuteResult
        {
            public Transform yaw;
            public ParticleSystem coalFlow;
        }

        private static void BuildPortal(Transform parent, SL15ModelConfig config, MaterialSet materials, List<Renderer> beltRenderers)
        {
            float halfBase = config.wheelBase * 0.5f;
            float halfGauge = config.railGauge * 0.5f;
            float halfTopLength = config.portalTopLength * 0.5f;
            float halfTopWidth = config.portalTopWidth * 0.5f;
            float deckY = config.baseHeight - 0.55f;

            foreach (float x in new[] { -halfBase, halfBase })
            {
                foreach (float z in new[] { -halfGauge, halfGauge })
                {
                    string label = $"{(x < 0 ? "Front" : "Rear")}{(z < 0 ? "Left" : "Right")}";
                    Transform bogie = NewChild(parent, $"VIS_Bogie{label}");
                    bogie.localPosition = new Vector3(x, 0f, z);
                    CreateBox(bogie, "BogieFrame", new Vector3(0f, 0.72f, 0f), new Vector3(3.4f, 0.5f, 1.25f), materials.machinery);
                    foreach (float wheelX in new[] { -1.05f, 1.05f })
                    {
                        GameObject wheel = CreateCylinder(bogie, "RailWheel", new Vector3(wheelX, 0.42f, 0f), 0.56f, 0.44f, materials.wheel);
                        wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        CreateCylinder(bogie, "WheelHub", new Vector3(wheelX, 0.42f, 0f), 0.2f, 0.55f, materials.galvanized).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }

                    Vector3 legStart = new(x, 1.25f, z);
                    Vector3 legEnd = new(Mathf.Sign(x) * halfTopLength, deckY, Mathf.Sign(z) * halfTopWidth);
                    CreateBeam(parent, $"VIS_PortalLeg{label}", legStart, legEnd, 0.55f, materials.structure);
                    CreateBeam(parent, $"VIS_PortalLegInner{label}",
                        legStart + new Vector3(-Mathf.Sign(x) * 0.85f, 0f, 0f),
                        legEnd + new Vector3(-Mathf.Sign(x) * 0.65f, 0f, -Mathf.Sign(z) * 0.35f),
                        0.28f, materials.structureDark);
                }
            }

            foreach (float z in new[] { -halfGauge, halfGauge })
            {
                CreateBeam(parent, "VIS_RailSideEqualizerBeam", new Vector3(-halfBase, 1.25f, z),
                    new Vector3(halfBase, 1.25f, z), 0.32f, materials.structureDark);
            }
            CreateBox(parent, "VIS_PortalDeck", new Vector3(0f, deckY, 0f),
                new Vector3(config.portalTopLength + 1.2f, 0.7f, config.portalTopWidth + 1.2f), materials.structureDark);

            foreach (float x in new[] { -halfTopLength, halfTopLength })
            {
                CreateBeam(parent, "PortalTopCrossBeam", new Vector3(x, deckY - 0.5f, -halfTopWidth),
                    new Vector3(x, deckY - 0.5f, halfTopWidth), 0.42f, materials.structure);
            }
            foreach (float z in new[] { -halfTopWidth, halfTopWidth })
            {
                CreateBeam(parent, "PortalTopLongBeam", new Vector3(-halfTopLength, deckY - 0.5f, z),
                    new Vector3(halfTopLength, deckY - 0.5f, z), 0.42f, materials.structure);
            }

            Transform tower = NewChild(parent, "VIS_PortalCentralTower");
            float towerBottom = deckY - 3f;
            foreach (float x in new[] { -1.7f, 1.7f })
            {
                foreach (float z in new[] { -1.45f, 1.45f })
                {
                    CreateBeam(tower, "TowerPost", new Vector3(x, towerBottom, z), new Vector3(x, deckY + 0.15f, z), 0.2f, materials.structureDark);
                }
            }
            foreach (float z in new[] { -1.45f, 1.45f })
            {
                CreateBeam(tower, "TowerBrace", new Vector3(-1.7f, towerBottom, z), new Vector3(1.7f, deckY + 0.15f, z), 0.12f, materials.structure);
                CreateBeam(tower, "TowerBrace", new Vector3(1.7f, towerBottom, z), new Vector3(-1.7f, deckY + 0.15f, z), 0.12f, materials.structure);
            }

            AddPerimeterRailing(parent, new Vector3(0f, deckY + 0.35f, 0f), config.portalTopLength + 1f, config.portalTopWidth + 1f, 0.8f, materials.safety);
            float ladderX = -halfTopLength - 0.25f;
            float ladderZ = -halfTopWidth - 0.42f;
            CreateBeam(parent, "PortalLadderRail", new Vector3(ladderX - 0.34f, 1.4f, ladderZ), new Vector3(ladderX - 0.34f, deckY, ladderZ), 0.06f, materials.safety);
            CreateBeam(parent, "PortalLadderRail", new Vector3(ladderX + 0.34f, 1.4f, ladderZ), new Vector3(ladderX + 0.34f, deckY, ladderZ), 0.06f, materials.safety);
            for (float y = 1.8f; y < deckY; y += 0.45f)
            {
                CreateBeam(parent, "PortalLadderRung", new Vector3(ladderX - 0.34f, y, ladderZ), new Vector3(ladderX + 0.34f, y, ladderZ), 0.045f, materials.safety);
            }

            Transform feed = NewChild(parent, "VIS_LandSideTransfer");
            feed.localPosition = new Vector3(-18f, config.baseHeight + 0.35f, 0f);
            feed.localRotation = Quaternion.Euler(0f, 90f, 0f);
            CreateTruss(feed, "LandSideFeedTruss", 14f, 2.2f, 1.45f, 7, materials.structureLight, materials.belt, materials.structureDark, beltRenderers);
        }

        private static UpperResult BuildUpperAssembly(Transform upper, SL15ModelConfig config, MaterialSet materials, List<Renderer> beltRenderers)
        {
            CreateCylinder(upper, "VIS_SlewBearing", new Vector3(0f, 0.22f, 0f), 2.15f, 0.48f, materials.machinery);
            CreateBox(upper, "VIS_SlewPlatform", new Vector3(0f, 0.68f, 0f), new Vector3(9.2f, 0.55f, 7.6f), materials.structureDark);
            CreateBox(upper, "VIS_MachineryHouse", new Vector3(-1.25f, 2.05f, -2.45f), new Vector3(5.8f, 2.75f, 3.8f), materials.structureLight);
            CreateBox(upper, "VIS_MachineryHouseRoof", new Vector3(-1.25f, 3.48f, -2.45f), new Vector3(6.15f, 0.18f, 4.1f), materials.structureDark);
            CreateBox(upper, "VIS_OperatorCab", new Vector3(3.55f, 2.25f, 0.6f), new Vector3(2.15f, 2.65f, 2.4f), materials.structure);
            CreateBox(upper, "VIS_OperatorCabGlass", new Vector3(3.58f, 2.55f, 1.82f), new Vector3(1.7f, 1.15f, 0.08f), materials.glass);
            foreach (float x in new[] { 2.46f, 4.64f })
            {
                CreateBox(upper, "VIS_OperatorCabSideGlass", new Vector3(x, 2.58f, 0.6f), new Vector3(0.07f, 1.15f, 1.72f), materials.glass);
            }
            AddPerimeterRailing(upper, new Vector3(0f, 1f, 0f), 9.5f, 8f, 0.75f, materials.safety);

            Transform counterBoom = NewChild(upper, "VIS_CounterBoom");
            counterBoom.localPosition = new Vector3(0f, 2.1f, -0.5f);
            counterBoom.localRotation = Quaternion.Euler(0f, 180f, 0f);
            CreateTruss(counterBoom, "CounterBoomTruss", 10.5f, 2.7f, 2.3f, 6, materials.structure, materials.structureDark, materials.structureDark, beltRenderers);
            CreateBox(upper, "VIS_CounterWeight", new Vector3(0f, 1.55f, -11.2f), new Vector3(5.4f, 2.7f, 2.2f), materials.machinery);

            Vector3 mastTopPosition = new(0f, 8f, -1.2f);
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                CreateBeam(upper, "VIS_LuffSupport", new Vector3(x, 1f, -1f), new Vector3(x * 0.45f, mastTopPosition.y, mastTopPosition.z), 0.18f, materials.structure);
            }
            Transform mastTop = NewMarker(upper, "MK_MAST_TOP", mastTopPosition);

            Transform luffPivot = NewChild(upper, "SL_BoomLuffPivot");
            luffPivot.localPosition = new Vector3(0f, config.boomBaseHeightOffset, 0f);
            GameObject pin = CreateCylinder(luffPivot, "VIS_BoomPivotPin", Vector3.zero, 0.52f, 3.8f, materials.machinery);
            pin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Transform fixedBoom = NewChild(luffPivot, "SL_FixedBoomAssembly");
            CreateTruss(fixedBoom, "FixedBoomTruss", config.fixedBoomLength, config.boomTrussWidth, config.boomTrussHeight, 9,
                materials.structure, materials.belt, materials.structureDark, beltRenderers);
            float walkwayX = config.boomTrussWidth * 0.5f + 0.48f;
            float walkwayY = config.boomTrussHeight * 0.5f + 0.08f;
            CreateBox(fixedBoom, "VIS_BoomWalkwayDeck", new Vector3(walkwayX, walkwayY, config.fixedBoomLength * 0.5f),
                new Vector3(0.72f, 0.08f, config.fixedBoomLength), materials.structureDark);
            CreateBeam(fixedBoom, "VIS_BoomWalkwayRail", new Vector3(walkwayX + 0.36f, walkwayY + 0.75f, 0f),
                new Vector3(walkwayX + 0.36f, walkwayY + 0.75f, config.fixedBoomLength), 0.05f, materials.safety);
            for (float z = 0f; z <= config.fixedBoomLength + 0.01f; z += 1.8f)
            {
                CreateBeam(fixedBoom, "BoomRailPost", new Vector3(walkwayX + 0.36f, walkwayY + 0.08f, z),
                    new Vector3(walkwayX + 0.36f, walkwayY + 0.75f, z), 0.045f, materials.safety);
            }

            Transform telescopic = NewChild(luffPivot, "SL_TelescopicBoomAssembly");
            telescopic.localPosition = new Vector3(0f, 0f, config.fixedBoomLength - config.telescopicOverlap);
            CreateTruss(telescopic, "TelescopicTruss", 1f, config.boomTrussWidth * 0.72f, config.boomTrussHeight * 0.68f, 10,
                materials.structureLight, materials.belt, materials.structureDark, beltRenderers);
            telescopic.localScale = new Vector3(1f, 1f, config.telescopicOverlap);

            Transform boomHead = NewChild(luffPivot, "SL_BoomHeadAttachment");
            boomHead.localPosition = new Vector3(0f, 0f, config.fixedBoomLength);
            CreateBox(boomHead, "VIS_HeadPulleyFrame", Vector3.zero, new Vector3(3.4f, 0.45f, 1.35f), materials.structureDark);
            foreach (float x in new[] { -1.55f, 1.55f })
            {
                foreach (float z in new[] { -0.66f, 0.66f })
                {
                    CreateBeam(boomHead, "VIS_HeadPulleySupport", new Vector3(x, -0.9f, z), new Vector3(x, 0.9f, z), 0.09f, materials.structureDark);
                }
            }
            GameObject pulley = CreateCylinder(boomHead, "VIS_HeadPulley", Vector3.zero, 0.72f, 3f, materials.machinery);
            pulley.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreateBox(boomHead, "VIS_TransferHopper", new Vector3(0f, -0.72f, 0.55f), new Vector3(2.2f, 0.72f, 1.3f), materials.galvanized);

            GameObject ropeObject = CreateBox(upper, "VIS_LuffRopeFront", Vector3.zero, Vector3.one, materials.machinery);
            CreateBeam(upper, "VIS_LuffRopeRear", mastTopPosition, new Vector3(0f, 2.4f, -9.5f), 0.055f, materials.machinery);
            return new UpperResult
            {
                luffPivot = luffPivot,
                telescopicScaleRoot = telescopic,
                boomHead = boomHead,
                luffRopeFront = ropeObject.transform,
                mastTop = mastTop,
            };
        }

        private static ChuteResult BuildChute(Transform chute, SL15ModelConfig config, MaterialSet materials)
        {
            float totalLength = config.PresentationChuteLength;
            float branchLength = Mathf.Min(3.1f, totalLength * 0.32f);
            float spread = Mathf.Min(1.65f, branchLength * 0.78f);
            float drop = Mathf.Sqrt(Mathf.Max(branchLength * branchLength - spread * spread, 0.25f));
            float branchPivotY = -totalLength + drop;
            float lowerPlatformY = branchPivotY + 0.62f;
            float trunkTopY = -3.05f;
            float trunkBottomY = lowerPlatformY + 0.18f;
            float guideHalfWidth = 1.25f;
            float guideHalfDepth = Mathf.Max(config.chuteTrunkRadius + 0.25f, 0.95f);

            Transform suspension = NewChild(chute, "VIS_ChuteSuspensionYoke");
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                CreateBeam(suspension, "SuspensionBrace", new Vector3(x, 0.08f, -1.9f), new Vector3(x * 0.58f, -1.35f, -0.38f), 0.09f, materials.machinery);
                CreateBeam(suspension, "SuspensionBrace", new Vector3(x, 0.08f, 1.9f), new Vector3(x * 0.58f, -1.35f, 0.38f), 0.09f, materials.machinery);
            }
            CreateBox(suspension, "VIS_ChuteTopTransition", new Vector3(0f, -1.35f, 0f), new Vector3(1.84f, 0.95f, 1.6f), materials.galvanized);
            CreateGuardedPlatform(chute, "VIS_ChuteTopServicePlatform", -1.92f, 3.8f, 3.2f, materials);
            CreateCylinder(chute, "VIS_ChuteTopRotaryHousing", new Vector3(0f, -2.28f, 0f), 1.03f, 0.82f, materials.galvanized);
            foreach (float y in new[] { -1.92f, -2.68f })
            {
                CreateCylinder(chute, "VIS_ChuteRotaryCollar", new Vector3(0f, y, 0f), 1.18f, 0.16f, materials.machinery);
            }
            foreach (float x in new[] { -1.43f, 1.43f })
            {
                CreateBox(chute, "VIS_ChuteTopDrive", new Vector3(x, -2.22f, 0f), new Vector3(0.58f, 0.66f, 0.82f), materials.machinery);
            }

            float trunkHeight = trunkTopY - trunkBottomY;
            CreateCylinder(chute, "VIS_ChuteTrunk", new Vector3(0f, (trunkTopY + trunkBottomY) * 0.5f, 0f),
                config.chuteTrunkRadius, trunkHeight, materials.chute);
            Transform guide = NewChild(chute, "VIS_ChuteGuideFrame");
            foreach (float x in new[] { -guideHalfWidth, guideHalfWidth })
            {
                foreach (float z in new[] { -guideHalfDepth, guideHalfDepth })
                {
                    CreateBeam(guide, "GuidePost", new Vector3(x, trunkTopY + 0.1f, z), new Vector3(x, trunkBottomY - 0.1f, z), 0.075f, materials.structureDark);
                }
            }
            int guidePanels = 7;
            for (int panel = 0; panel < guidePanels; panel++)
            {
                float y0 = Mathf.Lerp(trunkTopY, trunkBottomY, panel / (float)guidePanels);
                float y1 = Mathf.Lerp(trunkTopY, trunkBottomY, (panel + 1f) / guidePanels);
                foreach (float z in new[] { -guideHalfDepth, guideHalfDepth })
                {
                    CreateBeam(guide, "GuideBrace", new Vector3(-guideHalfWidth, y0, z), new Vector3(guideHalfWidth, y1, z), 0.045f, materials.structure);
                    CreateBeam(guide, "GuideBrace", new Vector3(guideHalfWidth, y0, z), new Vector3(-guideHalfWidth, y1, z), 0.045f, materials.structure);
                }
            }

            float ladderX = guideHalfWidth + 0.11f;
            foreach (float z in new[] { -0.56f, 0.56f })
            {
                CreateBeam(chute, "VIS_ChuteInspectionLadder", new Vector3(ladderX, trunkTopY + 0.15f, z),
                    new Vector3(ladderX, trunkBottomY - 0.05f, z), 0.045f, materials.safety);
            }
            for (int index = 0; index < 15; index++)
            {
                float y = Mathf.Lerp(trunkTopY, trunkBottomY, index / 14f);
                CreateBeam(chute, "ChuteLadderRung", new Vector3(ladderX, y, -0.56f), new Vector3(ladderX, y, 0.56f), 0.04f, materials.safety);
            }

            CreateGuardedPlatform(chute, "VIS_ChuteLowerServicePlatform", lowerPlatformY, 4.2f, 3.5f, materials);
            CreateCylinder(chute, "VIS_ChuteLowerRotaryHead", new Vector3(0f, branchPivotY + 0.2f, 0f), 1.2f, 0.95f, materials.galvanized);
            foreach (float x in new[] { -1.62f, 1.62f })
            {
                CreateBox(chute, "VIS_ChuteLowerDrive", new Vector3(x, branchPivotY + 0.32f, 0f), new Vector3(0.62f, 0.62f, 0.9f), materials.machinery);
            }

            Transform yaw = NewChild(chute, "SL_ChuteYawAssembly");
            yaw.localPosition = new Vector3(0f, branchPivotY, 0f);
            CreateBox(yaw, "VIS_ChuteDistributionYoke", new Vector3(0f, -0.12f, 0f), new Vector3(3.25f, 0.46f, 1.65f), materials.machinery);
            Vector3 branchStart = new(0f, -0.22f, -0.34f);
            Vector3 branchEnd = new(0f, -drop, -spread);
            CreateCylinderBetween(yaw, "VIS_ChuteSingleDischarge", branchStart, branchEnd, 0.62f, materials.chute, 4);
            foreach (float x in new[] { -0.58f, 0.58f })
            {
                CreateBeam(yaw, "DischargeBrace", new Vector3(x, branchStart.y + 0.22f, branchStart.z + 0.22f),
                    new Vector3(x, branchEnd.y + 0.18f, branchEnd.z + 0.32f), 0.055f, materials.machinery);
                CreateBeam(yaw, "DischargeBrace", new Vector3(x, branchStart.y - 0.22f, branchStart.z - 0.22f),
                    new Vector3(x, branchEnd.y - 0.18f, branchEnd.z - 0.32f), 0.055f, materials.machinery);
            }
            CreateBox(yaw, "DischargeOutlet", branchEnd, new Vector3(1.05f, 0.28f, 1.25f), materials.structureDark);
            ParticleSystem particles = CreateCoalFlow(yaw, branchEnd + new Vector3(0f, -0.2f, 0f), materials.coalParticle);
            return new ChuteResult { yaw = yaw, coalFlow = particles };
        }

        private static ParticleSystem CreateCoalFlow(Transform parent, Vector3 localPosition, Material material)
        {
            GameObject particleObject = new("CoalFlow");
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = localPosition;
            particleObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(15, 15, 15, 255));
            main.gravityModifier = 0.72f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 650;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 120f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.2f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static ShiploaderOrbitCamera BuildCamera(Transform target)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(214, 222, 229, 255);
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData additionalData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            // The simulation uses bright sunlit water and pale concrete. The
            // project-wide Bloom profile can blow these surfaces out from
            // grazing camera angles, so this presentation camera stays linear.
            additionalData.renderPostProcessing = false;
            additionalData.renderShadows = true;
            ShiploaderOrbitCamera orbit = cameraObject.AddComponent<ShiploaderOrbitCamera>();
            orbit.Configure(target);
            return orbit;
        }

        private static void BuildLightingAndPostProcessing()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(214, 225, 235, 255);
            RenderSettings.ambientEquatorColor = new Color32(153, 168, 181, 255);
            RenderSettings.ambientGroundColor = new Color32(82, 91, 99, 255);
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color32(214, 222, 229, 255);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 105f;
            RenderSettings.fogEndDistance = 210f;

            GameObject sunObject = new("Directional Light");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.45f;
            sun.color = new Color(1f, 0.96f, 0.89f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            RenderSettings.sun = sun;

            GameObject fillObject = new("Directional Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.38f;
            fill.color = new Color(0.72f, 0.84f, 1f);
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SL15_Demo_Profile";
            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.05f);
            color.contrast.Override(7f);
            color.saturation.Override(-3f);
            Bloom bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1.2f);
            AssetDatabase.CreateAsset(profile, ProfileFolder + "/SL15_Demo_Profile.asset");

            GameObject volumeObject = new("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static Transform CreateTruss(
            Transform parent,
            string name,
            float length,
            float width,
            float height,
            int panels,
            Material frameMaterial,
            Material beltMaterial,
            Material rollerMaterial,
            List<Renderer> beltRenderers)
        {
            Transform truss = NewChild(parent, name);
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            foreach (float x in new[] { -halfWidth, halfWidth })
            {
                foreach (float y in new[] { -halfHeight, halfHeight })
                {
                    CreateBeam(truss, "Longitudinal", new Vector3(x, y, 0f), new Vector3(x, y, length), 0.1f, frameMaterial);
                }
            }
            for (int panel = 0; panel <= panels; panel++)
            {
                float z = length * panel / panels;
                CreateBeam(truss, "CrossMember", new Vector3(-halfWidth, -halfHeight, z), new Vector3(halfWidth, -halfHeight, z), 0.085f, frameMaterial);
                CreateBeam(truss, "TopCrossMember", new Vector3(-halfWidth, halfHeight, z), new Vector3(halfWidth, halfHeight, z), 0.075f, frameMaterial);
                if (panel < panels)
                {
                    float nextZ = length * (panel + 1f) / panels;
                    float sign = panel % 2 == 0 ? 1f : -1f;
                    foreach (float x in new[] { -halfWidth, halfWidth })
                    {
                        CreateBeam(truss, "Diagonal", new Vector3(x, -sign * halfHeight, z), new Vector3(x, sign * halfHeight, nextZ), 0.07f, frameMaterial);
                    }
                }
            }
            GameObject belt = CreateBox(truss, "ConveyorBelt", new Vector3(0f, 0.08f, length * 0.5f), new Vector3(width * 0.72f, 0.12f, length), beltMaterial);
            beltRenderers.Add(belt.GetComponent<Renderer>());
            for (int panel = 0; panel <= panels; panel++)
            {
                float z = length * panel / panels;
                GameObject roller = CreateCylinder(truss, "BeltRoller", new Vector3(0f, 0.18f, z), 0.11f, width * 0.74f, rollerMaterial, 12);
                roller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return truss;
        }

        private static void CreateGuardedPlatform(Transform parent, string name, float centerY, float width, float depth, MaterialSet materials)
        {
            Transform platform = NewChild(parent, name);
            platform.localPosition = new Vector3(0f, centerY, 0f);
            CreateBox(platform, "Deck", Vector3.zero, new Vector3(width, 0.18f, depth), materials.structureDark);
            AddPerimeterRailing(platform, new Vector3(0f, 0.12f, 0f), width, depth, 0.78f, materials.safety);
        }

        private static void AddPerimeterRailing(Transform parent, Vector3 baseCenter, float width, float depth, float height, Material material)
        {
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            float railY = baseCenter.y + height;
            foreach (float z in new[] { baseCenter.z - halfDepth, baseCenter.z + halfDepth })
            {
                CreateBeam(parent, "SafetyRail", new Vector3(baseCenter.x - halfWidth, railY, z), new Vector3(baseCenter.x + halfWidth, railY, z), 0.055f, material);
                CreateBeam(parent, "SafetyMidRail", new Vector3(baseCenter.x - halfWidth, baseCenter.y + height * 0.52f, z), new Vector3(baseCenter.x + halfWidth, baseCenter.y + height * 0.52f, z), 0.04f, material);
                for (float x = -halfWidth; x <= halfWidth + 0.01f; x += 1.25f)
                {
                    CreateBeam(parent, "SafetyPost", new Vector3(baseCenter.x + x, baseCenter.y, z), new Vector3(baseCenter.x + x, railY, z), 0.045f, material);
                }
            }
            foreach (float x in new[] { baseCenter.x - halfWidth, baseCenter.x + halfWidth })
            {
                CreateBeam(parent, "SafetyRail", new Vector3(x, railY, baseCenter.z - halfDepth), new Vector3(x, railY, baseCenter.z + halfDepth), 0.055f, material);
                for (float z = -halfDepth; z <= halfDepth + 0.01f; z += 1.25f)
                {
                    CreateBeam(parent, "SafetyPost", new Vector3(x, baseCenter.y, baseCenter.z + z), new Vector3(x, railY, baseCenter.z + z), 0.045f, material);
                }
            }
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            RemoveCollider(gameObject);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = size;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return gameObject;
        }

        private static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            float radius,
            float height,
            Material material,
            int ignoredSegments = 18)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameObject.name = name;
            RemoveCollider(gameObject);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            return gameObject;
        }

        private static GameObject CreateSphere(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameObject.name = name;
            RemoveCollider(gameObject);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = size;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return gameObject;
        }

        private static GameObject CreateBeam(Transform parent, string name, Vector3 start, Vector3 end, float thickness, Material material)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            GameObject beam = CreateBox(parent, name, (start + end) * 0.5f, new Vector3(thickness, length, thickness), material);
            if (length > 0.0001f)
            {
                beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction / length);
            }
            return beam;
        }

        private static GameObject CreateCylinderBetween(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material,
            int segments = 18)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            GameObject cylinder = CreateCylinder(parent, name, (start + end) * 0.5f, radius, length, material, segments);
            if (length > 0.0001f)
            {
                cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction / length);
            }
            return cylinder;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Transform NewMarker(Transform parent, string name, Vector3 localPosition)
        {
            Transform marker = NewChild(parent, name);
            marker.localPosition = localPosition;
            return marker;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private static void SaveAsConnectedPrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
        }

        private static void SetStaticRecursively(GameObject gameObject)
        {
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }
    }
}
