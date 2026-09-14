using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZCJ.Shiploader.Editor
{
    // Visual-only authoring pass. Motion transforms and controller references are never replaced.
    public static class SL15VisualRefinement
    {
        const string Folder = "Assets/Shiploader/VisualRefinement";
        static Material red, dark, steel, cream, glass, yellow;

        [MenuItem("Tools/SL15/Apply Red Industrial Visuals")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring visuals.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Shiploader", "VisualRefinement");
            red = Material("IndustrialRed", new Color32(178, 32, 38, 255), .36f, .4f);
            dark = Material("RedShadow", new Color32(86, 23, 30, 255), .4f, .32f);
            steel = Material("BrushedSteel", new Color32(104, 124, 137, 255), .65f, .36f);
            cream = Material("MarineIvory", new Color32(225, 232, 230, 255), .12f, .3f);
            glass = Material("BridgeGlazing", new Color32(24, 59, 73, 255), .5f, .65f);
            yellow = Material("WarningAmber", new Color32(239, 181, 45, 255), .16f, .33f);

            foreach (string name in new[] { "SL15_Shiploader", "Vessel-L", "Vessel-R" })
            {
                string path = SL15SceneBuilder.PrefabFolder + "/" + name + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RefineRoot(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // Explicitly update scene instances too: existing material overrides may differ from prefabs.
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "SL_ShipLoaderRoot" || root.name == "Vessel-L" || root.name == "Vessel-R") RefineRoot(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("SL15 visual refinement saved: industrial red, curved hulls, marine fittings and batched detail meshes.");
        }

        static void RefineRoot(GameObject root)
        {
            // Idempotent: remove only the detail roots owned by this pass.
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "VIS_RefinementV1").ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            if (root.name.StartsWith("Vessel-")) RefineVessel(root);
            else RefineLoader(root);
        }

        static void RefineLoader(GameObject root)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material old = renderer.sharedMaterial;
                if (old == null) continue;
                if (old.name == "Structure_Yellow" || old.name == "Structure_Light") renderer.sharedMaterial = red;
                if (renderer.name.Contains("PortalDeck") || renderer.name.Contains("SlewPlatform")) renderer.sharedMaterial = dark;
            }
            Transform upper = Find(root.transform, "SL_UpperSlewAssembly");
            Transform detail = Detail(upper);
            // Machinery house panel seams, ventilation louvers, door and service handles.
            for (int i = 0; i < 15; i++)
                Box(detail, "Vent", new Vector3(-1.5f + i * .22f, 2.25f, -4.36f), new Vector3(.12f, 1.12f, .04f), steel);
            Box(detail, "ServiceDoor", new Vector3(1.25f, 1.88f, -4.37f), new Vector3(.72f, 1.95f, .07f), dark);
            Box(detail, "DoorHandle", new Vector3(1.46f, 1.9f, -4.44f), new Vector3(.04f, .24f, .05f), cream);
            for (int i = 0; i < 5; i++)
                Box(detail, "PanelSeam", new Vector3(-3.7f + i * 1.1f, 2.04f, -.54f), new Vector3(.025f, 2.5f, .025f), dark);
            for (int i = 0; i < 6; i++)
                Box(detail, "CounterweightStripe", new Vector3(-2.2f + i * .88f, 1.55f, -12.315f), new Vector3(.3f, 2.4f, .025f), yellow).localRotation = Quaternion.Euler(0, 0, -22);
            // Window mullions and cab visor give the operator station a readable silhouette.
            Box(detail, "CabVisor", new Vector3(3.55f, 3.66f, .7f), new Vector3(2.45f, .12f, 2.8f), red);
            for (int i = -1; i <= 1; i++)
                Box(detail, "CabMullion", new Vector3(3.58f + i * .56f, 2.55f, 1.88f), new Vector3(.045f, 1.18f, .055f), dark);
            foreach (float x in new[] { -3.8f, 2.9f })
            {
                Box(detail, "RoofBeaconBase", new Vector3(x, 3.68f, -2.45f), new Vector3(.22f, .12f, .22f), steel);
                Box(detail, "RoofBeacon", new Vector3(x, 3.86f, -2.45f), new Vector3(.14f, .25f, .14f), yellow);
            }
            Batch(detail, "LoaderCab");

            Transform travel = Find(root.transform, "SL_TravelAssembly");
            Transform structural = Detail(travel);
            foreach (Transform leg in root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("VIS_PortalLeg") && !t.name.Contains("Inner")).ToArray())
            {
                Vector3 end = travel.InverseTransformPoint(leg.TransformPoint(new Vector3(0, -.43f, 0)));
                Box(structural, "LegBaseFlange", end, new Vector3(1.05f, .13f, 1.05f), dark);
                foreach (float x in new[] { -.37f, .37f }) foreach (float z in new[] { -.37f, .37f })
                    Box(structural, "AnchorBolt", end + new Vector3(x, .12f, z), new Vector3(.1f, .12f, .1f), steel);
            }
            Batch(structural, "LoaderPortal");
        }

        static void RefineVessel(GameObject root)
        {
            bool left = root.name == "Vessel-L";
            float length = left ? 58 : 64, width = left ? 8 : 8.5f;
            Transform hull = root.transform.Find("Hull");
            Mesh shape = Hull(length, width);
            hull.GetComponent<MeshFilter>().sharedMesh = SaveMesh(shape, root.name + "_CurvedHull");
            Material navy = Material("MarineHull", new Color32(29, 49, 65, 255), .4f, .35f);
            Material lower = Material("Antifouling", new Color32(108, 40, 35, 255), .12f, .25f);
            hull.GetComponent<MeshRenderer>().sharedMaterials = new[] { lower, navy };
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial == null) continue;
                if (r.name.Contains("Window")) r.sharedMaterial = glass;
                if (r.name.Contains("AccommodationDeck") || r.name == "Wheelhouse") r.sharedMaterial = cream;
            }
            Transform detail = Detail(root.transform);
            // Continuous rub rail follows the actual sheer instead of a rectangular outline.
            for (int i = 0; i < 64; i++) foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 a = HullPoint(i / 64f, length, width, 1);
                Vector3 b = HullPoint((i + 1) / 64f, length, width, 1);
                a.z *= sign; b.z *= sign;
                a.y -= .12f; b.y -= .12f;
                Beam(detail, "SheerStrake", a, b, .075f, cream);
            }
            // Foredeck replaces the oversized rectangular platform with a tapered footprint.
            Transform fore = Find(root.transform, "ForecastleDeck");
            if (fore != null)
            {
                GameObject sourceCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh mesh = UnityEngine.Object.Instantiate(sourceCube.GetComponent<MeshFilter>().sharedMesh);
                UnityEngine.Object.DestroyImmediate(sourceCube);
                Vector3[] v = mesh.vertices;
                for (int i = 0; i < v.Length; i++) if (v[i].x > 0) v[i].z *= .23f;
                mesh.vertices = v; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                fore.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, root.name + "_Foredeck");
                foreach (Transform rail in fore.parent.Cast<Transform>().Where(t => t.name.StartsWith("Safety")).ToArray())
                    UnityEngine.Object.DestroyImmediate(rail.gameObject);
                float rear = length * (.435f - .0525f), front = length * (.435f + .0525f);
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(rear, 2.08f, sign * width * .36f);
                    Vector3 b = new Vector3(front, 2.08f, sign * width * .36f * .23f);
                    Beam(detail, "ForeRail", a + Vector3.up*.42f, b + Vector3.up*.42f, .045f, cream);
                    Beam(detail, "ForeMidRail", a + Vector3.up*.22f, b + Vector3.up*.22f, .03f, cream);
                    for (int i = 0; i <= 6; i++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, i/6f);
                        Beam(detail, "ForeStanchion", p, p+Vector3.up*.42f, .035f, cream);
                    }
                }
            }
            // Deck access stair, pipe runs and mooring fittings remain clear of cargo apertures.
            foreach (float sign in new[] { -1f, 1f })
            {
                for (int i = 0; i < 11; i++)
                    Box(detail, "AccommodationStair", new Vector3(-length * .37f + i * .16f, 1.83f + i * .12f, sign * width * .37f), new Vector3(.2f, .08f, .5f), steel);
                Beam(detail, "DeckServicePipe", new Vector3(-length * .33f, 1.9f, sign * width * .41f), new Vector3(length * .34f, 1.9f, sign * width * .41f), .07f, cream);
                for (int i = 0; i < 6; i++)
                {
                    float x = Mathf.Lerp(-length * .34f, length * .34f, i / 5f);
                    Box(detail, "FairleadBase", new Vector3(x, 1.84f, sign * width * .45f), new Vector3(.55f, .08f, .28f), steel);
                    foreach (float dx in new[] { -.16f, .16f })
                        Box(detail, "MooringBitt", new Vector3(x + dx, 2.02f, sign * width * .45f), new Vector3(.11f, .3f, .14f), navy);
                }
                // Waterline draft ticks: short marks readable in vessel closeups.
                foreach (float x in new[] { -length * .3f, length * .3f })
                    for (int i = 0; i < 7; i++)
                        Box(detail, "DraftMark", new Vector3(x, .08f + i * .18f, sign * (width * (.9f + (.08f + i * .18f + .256f) / 1.856f * .1f) * .5f + .02f)), new Vector3(i % 2 == 0 ? .28f : .16f, .035f, .025f), cream);
            }
            // Hull portholes and bridge roof equipment.
            for (int i = 0; i < 4; i++) foreach (float sign in new[] { -1f, 1f })
                Box(detail, "AftPorthole", new Vector3(-length * .39f + i * .48f, 1.05f, sign * width * .479f), new Vector3(.2f, .22f, .035f), glass);
            Batch(detail, root.name);
        }

        static Vector3 HullPoint(float t, float length, float width, float ring)
        {
            // Hermite stations provide a continuous bow/stern while preserving the original beam envelope.
            float[] xs = { 0, .025f, .09f, .18f, .82f, .9f, .965f, 1 };
            float[] bs = { .31f, .43f, .49f, .5f, .5f, .46f, .27f, .025f };
            float breadth = Smooth(t, xs, bs) * width;
            float sheer = Smooth(t, xs, new[] { .08f, .035f, .012f, 0, 0, .02f, .07f, .13f }) * 3.2f;
            return new Vector3((t - .5f) * length, 1.6f + sheer, breadth);
        }

        static float Smooth(float t, float[] xs, float[] ys)
        {
            int i = 0; while (i < xs.Length - 2 && t > xs[i + 1]) i++;
            float u = Mathf.InverseLerp(xs[i], xs[i + 1], t);
            int prev = Mathf.Max(0, i - 1), next = Mathf.Min(xs.Length - 1, i + 2);
            float m0 = (ys[i + 1] - ys[prev]) / (xs[i + 1] - xs[prev]);
            float m1 = (ys[next] - ys[i]) / (xs[next] - xs[i]);
            float span = xs[i + 1] - xs[i];
            float value = (2*u*u*u-3*u*u+1)*ys[i] + (u*u*u-2*u*u+u)*span*m0 + (-2*u*u*u+3*u*u)*ys[i+1] + (u*u*u-u*u)*span*m1;
            return Mathf.Clamp(value, Mathf.Min(ys[i], ys[i+1]), Mathf.Max(ys[i], ys[i+1]));
        }

        static Mesh Hull(float length, float width)
        {
            var v = new List<Vector3>(); var lower = new List<int>(); var upper = new List<int>();
            float[] zs = { -.52f, -.73f, -.86f, -.9f, -1, 1, .9f, .86f, .73f, .52f };
            float[] ys = { -1.6f, -1.35f, -.85f, -.256f, 1.6f, 1.6f, -.256f, -.85f, -1.35f, -1.6f };
            for (int i = 0; i <= 64; i++)
            {
                Vector3 p = HullPoint(i / 64f, length, width, 1);
                for (int j = 0; j < 10; j++) v.Add(new Vector3(p.x, ys[j] + (p.y - 1.6f) * (j == 4 || j == 5 ? 1 : .4f), p.z * zs[j]));
            }
            for (int i = 0; i < 64; i++) for (int j = 0; j < 10; j++)
            {
                int a = i*10+j, b = i*10+(j+1)%10, c = b+10, d = a+10;
                // Outward-facing sides; the upper rim remains shared for smooth marine shading.
                List<int> dst = j >= 3 && j <= 5 ? upper : lower;
                dst.AddRange(new[] { a, b, c, a, c, d });
            }
            for (int j = 1; j < 9; j++) { upper.AddRange(new[] {0, j+1, j}); upper.AddRange(new[] {640, 640+j, 640+j+1}); }
            var mesh = new Mesh { name = "CurvedMarineHull" };
            mesh.SetVertices(v); mesh.subMeshCount = 2;
            mesh.SetTriangles(lower, 0); mesh.SetTriangles(upper, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        static Transform Detail(Transform parent) { var go = new GameObject("VIS_RefinementV1"); go.transform.SetParent(parent, false); return go.transform; }
        static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat; return go.transform;
        }
        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Transform t = Box(parent, name, (a+b)*.5f, new Vector3(thickness, (b-a).magnitude, thickness), mat);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        }
        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Folder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
            mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static void Batch(Transform detail, string prefix)
        {
            MeshFilter[] sources = detail.GetComponentsInChildren<MeshFilter>();
            foreach (var group in sources.GroupBy(f => f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var combines = group.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = detail.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray();
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; mesh.CombineMeshes(combines, true, true);
                var go = new GameObject("Batched_" + group.Key.name); go.transform.SetParent(detail, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, prefix + "_" + group.Key.name);
                go.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            }
            foreach (MeshFilter f in sources) UnityEngine.Object.DestroyImmediate(f.gameObject);
        }
    }
}
