using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stasis.Rendering.EditorTools
{
    /// <summary>
    /// Prints how much runtime memory the project's textures cost, and what the usual
    /// import-setting changes would recover.
    ///
    /// Written because the managed heap reported in the editor (~830 MB while idle) is
    /// mostly the editor itself and does not ship, while textures are native memory that
    /// does. Only assets under Assets/ are counted: render targets and editor icons show
    /// up in a naive scan and would hide the real budget.
    /// </summary>
    public static class StasisTextureMemoryReport
    {
        [MenuItem("Tools/Stasis/Reporte de memoria de texturas")]
        private static void Report()
        {
            long total = 0, uncompressedBytes = 0, saveCompress = 0, saveCap1024 = 0, saveMasks512 = 0;
            int count = 0, uncompressedCount = 0, over1024 = 0, maskCount = 0;

            var byFolder = new Dictionary<string, long>();
            var heaviest = new List<KeyValuePair<long, string>>();

            foreach (var tex in UnityEngine.Resources.FindObjectsOfTypeAll<Texture>())
            {
                if (tex == null) continue;

                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;

                var tex2d = tex as Texture2D;
                if (tex2d == null) continue;

                long bytes = Profiler.GetRuntimeMemorySizeLong(tex);
                total += bytes;
                count++;

                string[] parts = path.Split('/');
                string folder = parts.Length > 2 ? parts[1] + "/" + parts[2] : path;
                byFolder.TryGetValue(folder, out long acc);
                byFolder[folder] = acc + bytes;

                if (bytes > 3 * 1024 * 1024)
                    heaviest.Add(new KeyValuePair<long, string>(
                        bytes, $"{tex2d.width}x{tex2d.height} {tex2d.format,-12} {path}"));

                string format = tex2d.format.ToString();
                bool compressed = format.Contains("DXT") || format.Contains("BC") ||
                                  format.Contains("ETC") || format.Contains("ASTC");
                if (!compressed)
                {
                    uncompressedBytes += bytes;
                    uncompressedCount++;
                    // RGBA32 to DXT5 is a 4x cut; RGB24 to DXT1 is 6x. Use the conservative one.
                    saveCompress += bytes - bytes / 4;
                }

                // Memory scales with pixel count, so halving the longest side quarters it.
                int size = Mathf.Max(tex2d.width, tex2d.height);
                if (size > 1024)
                {
                    over1024++;
                    saveCap1024 += bytes - (long)(bytes * (1024.0 / size) * (1024.0 / size));
                }

                string lower = path.ToLower();
                bool isMask = lower.Contains("metallicsmoothness") || lower.Contains("_mask") ||
                              lower.Contains("occlusion") || lower.Contains("_ao");
                if (isMask && size > 512)
                {
                    maskCount++;
                    saveMasks512 += bytes - (long)(bytes * (512.0 / size) * (512.0 / size));
                }
            }

            heaviest.Sort((a, b) => b.Key.CompareTo(a.Key));
            var folders = new List<string>(byFolder.Keys);
            folders.Sort((a, b) => byFolder[b].CompareTo(byFolder[a]));

            var sb = new StringBuilder();
            sb.AppendLine($"=== MEMORIA DE TEXTURAS: {Mb(total)} MB en {count} assets ===");
            sb.AppendLine($"Sin comprimir: {Mb(uncompressedBytes)} MB en {uncompressedCount} texturas");
            sb.AppendLine();
            sb.AppendLine("AHORRO ESTIMADO:");
            sb.AppendLine($"  Comprimir las {uncompressedCount} sin comprimir       -> ~{Mb(saveCompress)} MB");
            sb.AppendLine($"  Bajar a 512 los {maskCount} mapas metallic/AO/mask  -> ~{Mb(saveMasks512)} MB");
            sb.AppendLine($"  Cap general a 1024 ({over1024} texturas)          -> ~{Mb(saveCap1024)} MB");
            sb.AppendLine();
            sb.AppendLine("POR CARPETA:");
            for (int i = 0; i < Mathf.Min(12, folders.Count); i++)
                sb.AppendLine($"  {Mb(byFolder[folders[i]]),7} MB  {folders[i]}");
            sb.AppendLine();
            sb.AppendLine($"MAS PESADAS (>3 MB), {heaviest.Count} en total:");
            for (int i = 0; i < Mathf.Min(25, heaviest.Count); i++)
                sb.AppendLine($"  {Mb(heaviest[i].Key),7} MB  {heaviest[i].Value}");

            Debug.Log(sb.ToString());
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString("F1");
    }
}
