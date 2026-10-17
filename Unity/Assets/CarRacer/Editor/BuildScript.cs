using System;
using System.Collections.Generic;
using System.IO;
using CarRacer.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarRacer.Editor
{
    /// <summary>
    /// Android build entry points: menu items for local builds, and
    /// <see cref="BuildFromCommandLine"/> for GameCI (<c>buildMethod</c> in the workflows).
    /// </summary>
    public static class BuildScript
    {
        public const string BootstrapScenePath = "Assets/Scenes/" + Bootstrap.SceneName + ".unity";

        [MenuItem("Car Racer/Build/Development APK")]
        public static void BuildDevelopmentApk()
        {
            PrepareProject();
            Build("build/Android/CarRacer-dev.apk", development: true);
        }

        /// <summary>Uses the keystore configured in Player Settings (Publishing Settings) on this machine.</summary>
        [MenuItem("Car Racer/Build/Release APK")]
        public static void BuildReleaseApk()
        {
            PrepareProject();
            Build("build/Android/CarRacer.apk", development: false);
        }

        [MenuItem("Car Racer/Create Bootstrap Scene")]
        public static void EnsureBootstrapScene()
        {
            if (!File.Exists(BootstrapScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootstrapScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScenePath, true) };
        }

        /// <summary>
        /// Called by GameCI. Reads the arguments GameCI passes (-customBuildPath, -buildVersion,
        /// -androidVersionCode, -androidKeystoreName/Pass, -androidKeyaliasName/Pass) plus our own
        /// -carRacerDevelopment flag (via customParameters) for PR builds.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var args = ParseArguments(Environment.GetCommandLineArgs());
            PrepareProject();

            if (args.TryGetValue("buildVersion", out string version) && !string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            if (args.TryGetValue("androidVersionCode", out string code) && int.TryParse(code, out int versionCode) && versionCode > 0)
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
            }

            if (args.TryGetValue("androidKeystoreName", out string keystore) && !string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = args.TryGetValue("androidKeystorePass", out string kp) ? kp : string.Empty;
                PlayerSettings.Android.keyaliasName = args.TryGetValue("androidKeyaliasName", out string an) ? an : string.Empty;
                PlayerSettings.Android.keyaliasPass = args.TryGetValue("androidKeyaliasPass", out string ap) ? ap : string.Empty;
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
            }

            string output = args.TryGetValue("customBuildPath", out string path) && !string.IsNullOrEmpty(path)
                ? path
                : "build/Android/CarRacer.apk";
            bool development = args.ContainsKey("carRacerDevelopment");

            bool succeeded = Build(output, development);
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static void PrepareProject()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            ProjectConfigurator.Apply();
            EnsureBootstrapScene();
        }

        private static bool Build(string outputPath, bool development)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime})");
            return summary.result == BuildResult.Succeeded;
        }

        /// <summary>Arguments that always take the next argv entry as their value, even if it starts with "-" (e.g. a password).</summary>
        private static readonly HashSet<string> ValueArguments = new HashSet<string>(StringComparer.Ordinal)
        {
            "customBuildPath",
            "buildVersion",
            "androidVersionCode",
            "androidKeystoreName",
            "androidKeystorePass",
            "androidKeyaliasName",
            "androidKeyaliasPass",
        };

        private static Dictionary<string, string> ParseArguments(string[] argv)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < argv.Length; i++)
            {
                if (!argv[i].StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                string key = argv[i].TrimStart('-');
                bool hasNext = i + 1 < argv.Length;
                string value = hasNext && (ValueArguments.Contains(key) || !argv[i + 1].StartsWith("-", StringComparison.Ordinal))
                    ? argv[++i]
                    : string.Empty;
                result[key] = value;
            }

            return result;
        }
    }
}
