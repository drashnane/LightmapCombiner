using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
[CustomEditor(typeof(LightmapHolder))]
public class LightmapHolderEditor : Editor
{
    class EXRData
    {
        public int width;
        public int height;
        public float[] pixels;
    }
    const int MAX_SIZE = 1024;
    Texture2D previewTexture = null;
    public override void OnInspectorGUI()
    {
        var holder = target as LightmapHolder;
        if (holder.renderers != null && holder.scales != null)
            EditorGUILayout.LabelField($"{holder.renderers.Length} renderers in {holder.scales.Length} indices");
        if (GUILayout.Button("Clear Lightmaps"))
        {
            ClearHolder(holder);

            Lightmapping.Clear();
        }
        if (Lightmapping.isRunning)
        {
            EditorGUILayout.LabelField("Baking...");
        }
        else
        {
            if (GUILayout.Button("Bake"))
            {
                ClearHolder(holder);
                LightmapEditorSettings.maxAtlasSize = MAX_SIZE;
                LightmapEditorSettings.filteringMode = LightmapEditorSettings.FilterMode.Auto;
                LightmapEditorSettings.lightmapper = LightmapEditorSettings.Lightmapper.ProgressiveGPU;

                Lightmapping.BakeAsync();
            }
            if (GUILayout.Button("Combine Lightmaps"))
            {
                ClearHolder(holder);
                try
                {
                    Combine(holder);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

    }
    void ClearHolder(LightmapHolder holder)
    {
        holder.dest = null;
        holder.dir = null;
        holder.shadowMask = null;
        holder.renderers = null;
        holder.scales = null;
    }
    string CombineExr(LightmapHolder holder, LightmapData[] data)
    {
        var textures = data.Select(d => ReadTexture(AssetDatabase.GetAssetPath(d.lightmapColor), d.lightmapColor.width, d.lightmapColor.height)).ToArray();
        if (textures.Length == 0)
            return "";
        EXRData combine = new EXRData() { width = MAX_SIZE * 2, height = MAX_SIZE * 2, pixels = new float[MAX_SIZE * MAX_SIZE * 4 * 4] };
        for (int i = 0; i < combine.pixels.Length; ++i)
        {
            combine.pixels[i] = 1;
        }
        holder.scales = new float[textures.Length];
        for (int i = 0; i < textures.Length; ++i)
        {
            var texture = textures[i];
            if (texture == null)
                continue;
            var raw = textures[i].pixels;
            var offsety = combine.height / 2 - texture.height;
            holder.scales[i] = (float)texture.height / (float)combine.height;
            for (int x = 0; x < texture.width; ++x)
            {
                for (int y = 0; y < texture.height; ++y)
                {

                    var index = (x + y * texture.width) * 4;
                    var destX = (i % 2) * (combine.width / 2) + x;
                    var destY = (1 - i / 2) * (combine.height / 2) + y + offsety;
                    var dest = (destX + destY * combine.width) * 4;
                    combine.pixels[dest] = raw[index];
                    combine.pixels[dest + 1] = raw[index + 1];
                    combine.pixels[dest + 2] = raw[index + 2];
                    combine.pixels[dest + 3] = raw[index + 3];
                }
            }
        }

        var fileName = AssetDatabase.GetAssetPath(data[0].lightmapColor);
        fileName = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)) + "_combined" + Path.GetExtension(fileName);
        WriteTexture(fileName, combine);
        return fileName;
    }
    string CombineTexture(LightmapHolder holder, string[] paths)
    {
        var textures = paths.Select(d => { var img = new Texture2D(2, 2); img.LoadImage(File.ReadAllBytes(d)); return img; }).ToArray();
        var combine = new Texture2D(MAX_SIZE * 2, MAX_SIZE * 2, TextureFormat.RGBA32, false);
        for (int i = 0; i < textures.Length; ++i)
        {
            combine.SetPixels((i % 2) * MAX_SIZE, (i / 2) * MAX_SIZE, textures[i].width, textures[i].height, textures[i].GetPixels());
        }
        var fileName = paths[0];
        fileName = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName)) + "_combined" + Path.GetExtension(fileName);
        File.WriteAllBytes(fileName, combine.EncodeToPNG());
        return fileName;
    }
    void Combine(LightmapHolder holder)
    {

        var data = LightmapSettings.lightmaps;
        if (data.Length <= 1)
            return;
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r => r.gameObject.isStatic).ToArray();
        holder.renderers = new LightmapHolder.LightmapRenderers[renderers.Length];
        for (int i = 0; i < renderers.Length; ++i)
        {
            holder.renderers[i] = new LightmapHolder.LightmapRenderers() { index = renderers[i].lightmapIndex, renderer = renderers[i], scaleOffset = renderers[i].lightmapScaleOffset };
        }
        EditorUtility.DisplayProgressBar("", "Processing Lightmaps...", 0.2f);
        string lightmapFileName = CombineExr(holder, data);
        EditorUtility.DisplayProgressBar("", "Processing Dirs...", 0.4f);
        string dirFileName = data[0].lightmapDir == null ? "" : CombineTexture(holder, data.Select(d => AssetDatabase.GetAssetPath(d.lightmapDir)).ToArray());
        EditorUtility.DisplayProgressBar("", "Processing Shadowmasks...", 0.6f);
        string shadowMaskFileName = data[0].shadowMask == null ? "" : CombineTexture(holder, data.Select(d=>AssetDatabase.GetAssetPath(d.shadowMask)).ToArray());
        EditorUtility.DisplayProgressBar("", "Importing Assets...", 0.8f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(lightmapFileName) as TextureImporter;
        importer.textureType = TextureImporterType.Lightmap;
        importer.textureCompression = LightmapEditorSettings.textureCompression ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        
        holder.dest = AssetDatabase.LoadAssetAtPath<Texture2D>(lightmapFileName);
        if (!string.IsNullOrEmpty(shadowMaskFileName))
        {
            holder.shadowMask = AssetDatabase.LoadAssetAtPath<Texture2D>(shadowMaskFileName);
        }
        if (!string.IsNullOrEmpty(dirFileName))
        {
            importer = AssetImporter.GetAtPath(dirFileName) as TextureImporter;
            importer.textureCompression = LightmapEditorSettings.textureCompression ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            holder.dir = AssetDatabase.LoadAssetAtPath<Texture2D>(dirFileName);

        }
        foreach (var l in LightmapSettings.lightmaps)
        {
            if (l.lightmapColor != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(l.lightmapColor));
            if (l.lightmapDir != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(l.lightmapDir));
            if (l.shadowMask != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(l.shadowMask));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        holder.UpdateLightmap();

    }
    static EXRData ReadTexture(string fileName, int width, int height)
    {
        EXRData ret = new EXRData();
        ret.pixels = new float[width * height * 4];
        string err = "";
        IntPtr ptr = IntPtr.Zero;
        if (TinyExr.TinyExr.LoadEXR(ref ptr, ref ret.width, ref ret.height, fileName, ref err) != 0)
        {
            throw new Exception(err);
        }
        Marshal.Copy(ptr, ret.pixels, 0, ret.pixels.Length);
        Marshal.FreeHGlobal(ptr);
        return ret;
    }
    static void WriteTexture(string fileName, EXRData data)
    {
        string err = "";
        IntPtr ptr = Marshal.AllocHGlobal(sizeof(float) * data.pixels.Length);
        Marshal.Copy(data.pixels, 0, ptr, data.pixels.Length);
        if (TinyExr.TinyExr.SaveEXR(ptr, data.width, data.height, 4, 0, fileName, ref err) != 0)
        {
            throw new Exception(err);
        }
        Marshal.FreeHGlobal(ptr);
    }
    public override bool HasPreviewGUI()
    {
        var t = target as LightmapHolder;
        return t.dest != null || t.dir != null || t.shadowMask != null;
    }
    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        var t = target as LightmapHolder;
        var rect = r;
        rect.height = 20f;
        int count = 0;
        if (t.dest != null)
            ++count;
        if (t.dir != null)
            ++count;
        if (t.shadowMask != null)
            ++count;
        if (count > 0)
            rect.width /= count;
        if(t.dest != null)
        {
            if (previewTexture == null)
                previewTexture = t.dest;
            if(GUI.Toggle(rect, previewTexture == t.dest,"Color", EditorStyles.toolbarButton))
            {
                previewTexture = t.dest;
            }
            rect.x += rect.width;
        }
        if (t.shadowMask != null)
        {
            if (GUI.Toggle(rect, previewTexture == t.shadowMask, "Mask", EditorStyles.toolbarButton))
            {
                previewTexture = t.shadowMask;
            }
            rect.x += rect.width;
        }
        if (t.dir != null)
        {
            if (GUI.Toggle(rect, previewTexture == t.dir, "Dir", EditorStyles.toolbarButton))
            {
                previewTexture = t.dir;
            }
            rect.x += rect.width;
        }
        if (previewTexture == null)
            return;
        r.y += 20f;
        r.height -= 20f;
        if(t.dest != null)
            EditorGUI.DrawPreviewTexture(r, previewTexture,null, ScaleMode.ScaleToFit);
    }
}

