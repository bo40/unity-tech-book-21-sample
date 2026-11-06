using UnityEditor;

namespace PolyglotBossRush.Editor
{
    // scripts/build_*.sh が置いたネイティブライブラリを、フォルダ名からプラットフォームに割り当てる。
    // 新しく取り込まれたときは AssetPostprocessor が、ビルド前には SampleBuild が Apply でまとめて適用する
    // （初回のインポートはこのスクリプトのコンパイルより先に走るので、postprocessor だけでは取りこぼす）
    sealed class PluginImportRules : AssetPostprocessor
    {
        const string PluginsFolder = "Assets/PolyglotBossRush/Generated/Plugins";

        void OnPreprocessAsset()
        {
            if (assetImporter is PluginImporter importer)
                Configure(importer, assetPath);
        }

        public static void Apply()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { PluginsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is PluginImporter importer && Configure(importer, path))
                    importer.SaveAndReimport();
            }
        }

        /// <summary>設定を変えたら true</summary>
        static bool Configure(PluginImporter importer, string path)
        {
            if (!path.StartsWith(PluginsFolder) || path.Contains("/Managed/"))
                return false; // F# の DLL は既定（全プラットフォーム）のまま
            if (path.Contains("/macOS/"))
            {
                if (!importer.GetCompatibleWithAnyPlatform() && importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX))
                    return false;
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(true);
                importer.SetEditorData("OS", "OSX");
                importer.SetEditorData("CPU", "ARM64");
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, true);
                return true;
            }
            if (path.Contains("/Android/arm64-v8a/"))
            {
                if (!importer.GetCompatibleWithAnyPlatform() && importer.GetCompatibleWithPlatform(BuildTarget.Android))
                    return false;
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
                importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
                return true;
            }
            return false;
        }
    }
}
