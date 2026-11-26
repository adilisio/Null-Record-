using System.IO;
using Game.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Player builds. Output goes to Builds/ (git-ignored).
    /// Batch: Unity -batchmode -buildTarget WebGL -executeMethod Game.EditorTools.Builds.WebGL -quit
    ///        Unity -batchmode -buildTarget Android -executeMethod Game.EditorTools.Builds.Android -quit
    /// </summary>
    public static class Builds
    {
        private static readonly string[] Scenes = { GameFlow.TitleScene, GameFlow.GameScene };

        [MenuItem("Null Record/Build/WebGL", priority = 40)]
        public static void WebGL()
        {
            CommonSettings();
            // Gzip plus decompression fallback works on static hosts (e.g. GitHub Pages) that don't send Content-Encoding.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:Responsive"; // full-window canvas, loading bar, rotate hint
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("Null Record/Build/Android APK", priority = 41)]
        public static void Android()
        {
            CommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nullrecord.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false; // a plain APK to sideload
            Build(BuildTarget.Android, "Builds/Android/NullRecord.apk");
        }

        private static void CommonSettings()
        {
            PlayerSettings.productName = "Null Record";
            PlayerSettings.companyName = "Null Record";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0") PlayerSettings.bundleVersion = "0.1.0";
        }

        private static void Build(BuildTarget target, string path)
        {
            if (Path.HasExtension(path)) Directory.CreateDirectory(Path.GetDirectoryName(path));
            else Directory.CreateDirectory(path);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            BuildSummary s = report.summary;
            Debug.Log($"[Null Record] {target} build {s.result}: {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s -> {path}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
