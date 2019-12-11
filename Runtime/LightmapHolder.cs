using System;
using UnityEngine;
using System.Diagnostics;
[ExecuteInEditMode]
public class LightmapHolder : MonoBehaviour
{
    public Texture2D dest;
    public Texture2D shadowMask;
    public Texture2D dir;
    public LightmapRenderers[] renderers;
    public float[] scales;
    [Serializable]
    public class LightmapRenderers
    {
        public int index;
        public Vector4 scaleOffset;
        public Renderer renderer;
    }
    [Conditional("UNITY_EDITOR")]
    private void OnEnable()
    {
        UpdateLightmap();
    }

    [Conditional("UNITY_EDITOR")]
    public void UpdateLightmap()
    {
        if (dest == null && dir == null && shadowMask == null || renderers == null || scales == null)
            return;
        bool isRunning = Application.isPlaying;
        var lightmapData = new LightmapData { lightmapColor = dest, lightmapDir = dir, shadowMask = shadowMask };
        LightmapSettings.lightmaps = new LightmapData[] { lightmapData };
        foreach (var r in renderers)
        {
            if (r == null)
                continue;
            if (r.index < 0 || r.index >= scales.Length)
            {
                r.renderer.lightmapIndex = r.index;
            }
            else
            {
                var scaleOffset = r.scaleOffset * scales[r.index];
                r.renderer.lightmapIndex = 0;
                scaleOffset.z += (r.index % 2) * 0.5f;
                scaleOffset.w += (r.index / 2) * 0.5f;
                r.renderer.lightmapScaleOffset = scaleOffset;
            }

        }
    }
}
