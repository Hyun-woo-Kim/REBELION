using System.IO;
using UnityEditor;
using UnityEngine;

namespace ForceHarmony.Editor
{
    public static class ForceHarmonyDataTableConverter
    {
        private const string SourceFolder = "Assets/DataTable";
        private const string RuntimeFolder = "Assets/StreamingAssets/ForceHarmony/Data";

        [MenuItem("Force Harmony/Data Tables/Convert CSV To Runtime Cache")]
        public static void ConvertCsvToRuntimeCache()
        {
            if (!Directory.Exists(SourceFolder))
            {
                Debug.LogError($"[ForceHarmony] Missing source folder: {SourceFolder}");
                return;
            }

            Directory.CreateDirectory(RuntimeFolder);

            var copiedCount = 0;
            foreach (var sourcePath in Directory.GetFiles(SourceFolder, "*.csv", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(RuntimeFolder, fileName).Replace('\\', '/');
                File.Copy(sourcePath, targetPath, true);
                copiedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ForceHarmony] Converted {copiedCount} CSV data tables to {RuntimeFolder}");
        }
    }
}
