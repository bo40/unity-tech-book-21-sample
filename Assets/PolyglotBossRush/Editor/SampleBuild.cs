using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PolyglotBossRush.Editor
{
    // シーンの生成とプレイヤーのビルド。バッチモードからも呼べる:
    //   Unity -batchmode -quit -projectPath . -executeMethod PolyglotBossRush.Editor.SampleBuild.BuildAndroid
    public static class SampleBuild
    {
        public const string ScenePath = "Assets/PolyglotBossRush/Scenes/BossRush.unity";

        [MenuItem("Polyglot Boss Rush/Create Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            camera.GetComponent<Camera>().orthographic = true;
            new GameObject("Boss Rush", typeof(BossRushGame));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("Polyglot Boss Rush/Build macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, NamedBuildTarget.Standalone, "Builds/macOS/PolyglotBossRush.app");

        [MenuItem("Polyglot Boss Rush/Build Android")]
        public static void BuildAndroid()
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.unitysunday.polyglotbossrush");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, NamedBuildTarget.Android, "Builds/Android/PolyglotBossRush.apk");
        }

        static void Build(BuildTarget target, NamedBuildTarget namedTarget, string outputPath)
        {
            PluginImportRules.Apply();
            PlayerSettings.productName = "Polyglot Boss Rush";
            PlayerSettings.companyName = "Unity.Sunday";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = target,
            });
            Debug.Log($"[SampleBuild] {target}: {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
