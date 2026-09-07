using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using Yoji;
using Yoji.Components;

namespace Yoji.Editor
{
    public class Importer : AssetPostprocessor
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
            var provider = new TriangleProvider(mesh);
            var constructor = new FrameConstructor(provider);
            // SkinnedMeshはスキニング変形後まで法線が確定しないため、バインドポーズ時点の法線比較による
            // 不要ワイヤー削除(DestroyUselessWire)は適用できない。常に全ワイヤーを保持する。
            constructor.DestroyUselessWire = false;
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
            var provider = new TriangleProvider(mesh);
            var constructor = new FrameConstructor(provider);
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
                mat.SetFloat("_SmoothAngle", CosAngleDrawer.ConvertAngle(setting.DefaultSmoothAngle));
            }
        }
    }
}

