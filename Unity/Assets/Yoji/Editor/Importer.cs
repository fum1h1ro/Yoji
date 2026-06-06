using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using Yoji.Runtime;
using Yoji.Runtime.Components;

namespace Yoji.Editor
{
    public class YojiImporter : AssetPostprocessor
    {
        private bool IsReadableBackup;

        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            var modelImporter = (ModelImporter)assetImporter;
            IsReadableBackup = modelImporter.isReadable;
            modelImporter.isReadable = true;
        }

        void OnPostprocessModel(GameObject obj)
        {
            var modelImporter = (ModelImporter)assetImporter;
            var assetDir = Path.GetDirectoryName(assetPath);
            var setting =
                AssetDatabase.FindAssets("t:Yoji.Editor.ConvertSettings", new[]{ assetDir })
                    .Select(x => AssetDatabase.GUIDToAssetPath(x))
                    .Where(x => Path.GetDirectoryName(x) == assetDir)
                    .Select(x => AssetDatabase.LoadAssetAtPath<ConvertSettings>(x))
                    .FirstOrDefault();
            if (setting == null) return;

            //var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                ModifyMeshRenderer(renderer);
                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    ConvertSkinnedMesh(skinnedMeshRenderer, skinnedMeshRenderer.sharedMesh, setting);
                }
                else
                {
                    var meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
                    ModifyMesh((MeshRenderer)renderer, meshFilter.sharedMesh, setting);
                }
            }
            modelImporter.isReadable = IsReadableBackup;
        }

        private void ModifyMeshRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void ConvertSkinnedMesh(SkinnedMeshRenderer renderer, Mesh mesh, ConvertSettings setting)
        {
            var providor = new TriangleProvidor(mesh);
            var constructor = new FrameConstructor(providor);
            constructor.DestroyUselessWire = setting.DestroyUselessWire;
            constructor.Construct();

            var fs = constructor.ToFrameStructure();
            fs.name = $"frame_{mesh.name}";
            using (var editor = fs.BeginEdit())
            {
                foreach (var bindPose in mesh.bindposes)
                {
                    editor.AddBindPose(bindPose);
                }
            }

            context.AddObjectToAsset(fs.name, fs);

            var fr = renderer.gameObject.AddComponent<FrameRenderer>();
            fr.FrameStructure = fs;
            fr.Bones = renderer.bones;
            fr.Materials = renderer.sharedMaterials;

            ModifyMaterials(renderer.sharedMaterials, setting);

            SkinnedMeshRenderer.DestroyImmediate(renderer);
        }

        private void ModifyMesh(MeshRenderer renderer, Mesh mesh, ConvertSettings setting)
        {
            var providor = new TriangleProvidor(mesh);
            var constructor = new FrameConstructor(providor);
            constructor.DestroyUselessWire = setting.DestroyUselessWire;
            constructor.Construct();

            using (var vb = constructor.ToVertexBuffer())
            {
                vb.ApplyToMesh(mesh);
                mesh.RecalculateBounds();
            }

            ModifyMaterials(renderer.sharedMaterials, setting);
        }

        private void ModifyMaterials(Material[] materials, ConvertSettings setting)
        {
            var shader = Shader.Find("Yoji/Simple");

            foreach (var mat in materials)
            {
                mat.shader = shader;
                mat.SetFloat("_LineWidth", setting.DefaultLineWidth);
                mat.SetFloat("_BackLineDensity", setting.DefaultBackLineDensity);
                mat.SetFloat("_EdgeLineDensity", setting.DefaultEdgeLineDensity);
                mat.SetFloat("_FrontLineDensity", setting.DefaultFrontLineDensity);
                mat.SetFloat("_SmoothAngle", YojiCosAngleDrawer.ConvertAngle(setting.DefaultSmoothAngle));
            }
        }
    }




#if false
        public ConvertSettings[] _convertSettings;

        [MenuItem("Yoji/CreatePrefabFromMesh")]
        static void MenuItem_CreatePrefabFromMesh()
        {
            try
            {
                var guids = Selection.assetGUIDs;
                if (guids.Length == 0) throw new InvalidOperationException("Please select some assets");

                var projDir = Application.dataPath;
                projDir = projDir.Substring(0, projDir.LastIndexOf("Assets"));

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Directory.Exists(projDir + path))
                    {
                        throw new InvalidOperationException(path + " is directory.");
                    }
                    if (System.IO.File.Exists(projDir + path))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (asset == null) throw new InvalidOperationException("Please select Model Assets");
                        var assetDir = System.IO.Path.GetDirectoryName(path);
                        var assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                        var framePath = projDir + assetDir + "/" + assetName + ".yoji";
                        //Debug.Log(framePath);
                        System.IO.File.WriteAllText(framePath, guid);
                        //var alreadyExists = System.IO.File.Exists(framePath);
                        AssetDatabase.Refresh();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        public override void OnImportAsset(AssetImportContext context)
        {
            var guid = System.IO.File.ReadAllText(assetPath);
            var srcAssetPath = AssetDatabase.GUIDToAssetPath(guid);
            var srcObj = AssetDatabase.LoadAssetAtPath<GameObject>(srcAssetPath);
            if (srcObj == null) throw new InvalidOperationException("cannot load asset: " + srcAssetPath);
            MakeConvertSettings(srcObj);
            var dstObj = GameObject.Instantiate(srcObj);
            ConvertMeshToFrame(dstObj);

            AssetDatabase.SetLabels(dstObj, new string[]{"YojiFrame"});
            context.AddObjectToAsset(srcObj.name, dstObj);
            context.SetMainObject(dstObj);

            CopyMaterials(context, dstObj);

            foreach (var filter in dstObj.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                context.AddObjectToAsset(mesh.name, mesh);
            }
        }

        static void CopyMaterials(AssetImportContext context, GameObject srcObj)
        {
            var materials = EnumerateMaterials(srcObj)
                .Select((m) => Material.Instantiate(m))
                .Select((m) => { m.shader = Shader.Find("Yoji/Simple"); return m; })
                .Select((m) => { m.name = m.name.Replace("(Clone)", ""); return m; })
                .ToList();
            foreach (var m in materials)
            {
                context.AddObjectToAsset(m.name, m);
            }
            foreach (var r in srcObj.GetComponentsInChildren<MeshRenderer>())
            {
                var sharedMaterials = new Material[r.sharedMaterials.Length];
                for (int si = 0; si < r.sharedMaterials.Length; ++si)
                {
                    sharedMaterials[si] = materials.Where((m) => m.name == r.sharedMaterials[si].name).First();
                }
                r.sharedMaterials = sharedMaterials;
            }
        }

        static List<Material> EnumerateMaterials(GameObject srcObj)
        {
            var materials = new HashSet<Material>();
            foreach (var renderer in srcObj.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    materials.Add(mat);
                }
            }
            return materials.ToList();
        }

        static int IndexOfConvertSetting(IEnumerable<ConvertSettings> settings, string name)
        {
            int idx = 0;
            foreach (var setting in settings) {
                if (string.Compare(setting.RawName, name) == 0) {
                    return idx;
                }
                ++idx;
            }
            return -1;
        }

        static ConvertSettings FindConvertSetting(IEnumerable<ConvertSettings> settings, string name)
        {
            int idx = IndexOfConvertSetting(settings, name);
            return (idx < 0)? null : settings.ElementAt(idx);
        }

        void MakeConvertSettings(GameObject srcObj)
        {
            Assert.IsNotNull(srcObj);
            var newSettings = new List<ConvertSettings>();
            var materials = EnumerateMaterials(srcObj);
            foreach (var material in materials)
            {
                if (FindConvertSetting(newSettings, material.name) != null) continue;
                if (_convertSettings == null || FindConvertSetting(_convertSettings, material.name) == null)
                {
                    newSettings.Add(new ConvertSettings(material));
                    continue;
                }
                newSettings.Add(FindConvertSetting(_convertSettings, material.name));
            }
            _convertSettings = newSettings.ToArray();
        }

        void ConvertMeshToFrame(GameObject prefabRoot)
        {
            var meshRenderers = prefabRoot.GetComponentsInChildren<MeshRenderer>();
            //var skinnedMeshRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var meshRenderer in meshRenderers)
            {
                var meshFilter = meshRenderer.gameObject.GetComponent<MeshFilter>();
                var mesh = meshFilter.sharedMesh;
                meshFilter.sharedMesh = ConvertMesh(mesh);
            }
        }

    }



    class MyAllPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var yojis = AssetDatabase.FindAssets("l:YojiFrame");
            foreach (var y in yojis)
            {
                Debug.Log(y);
            }


            foreach (string path in importedAssets)
            {
                Debug.Log("Reimported Asset: " + path);
                var guid = AssetDatabase.AssetPathToGUID(path);














            }
            foreach (string str in deletedAssets)
            {
                Debug.Log("Deleted Asset: " + str);
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                Debug.Log("Moved Asset: " + movedAssets[i] + " from: " + movedFromAssetPaths[i]);
            }
        }
    }
#endif






#if false

    [CustomEditor(typeof(YojiImporter))]
    public class YojiImporterEditor : ScriptedImporterEditor
    {
        YojiImporter _importer;

        public override void OnEnable()
        {
            _importer = target as YojiImporter;
            Debug.Log("START");
        }

        public override void OnDisable()
        {
            _importer = null;
            Debug.Log("DISABLE");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            //if (GUILayout.Button("Save")) {
            //	EditorUtility.SetDirty(_importer);
            //}
            ApplyRevertGUI();
        }
        //protected override bool OnApplyRevertGUI() {
        //	if (HasModified()) {
        //		ApplyButton();
        //	}
        //	return false;
        //}
    }
#endif
}

