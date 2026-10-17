using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace CarRacer.Editor
{
    /// <summary>
    /// Player settings applied from code rather than hand-edited in ProjectSettings, so they're
    /// reviewable in diffs and the CI build always uses them (BuildScript calls Apply first).
    /// </summary>
    public static class ProjectConfigurator
    {
        public const string ApplicationId = "io.github.joshuaspence.carracer";
        public const string ProductName = "Car Racer";

        [MenuItem("Car Racer/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = ProductName;
            PlayerSettings.productName = ProductName;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);

            // Oldest supported phone is a Pixel 2 XL (Android 8 at launch, up to 11).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Vulkan first, GLES3 fallback. Adreno 5xx (Pixel 2 XL) should be steered to GLES3
            // with a Vulkan device filter list; that's set up with the rendering work in M1.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            EditorUserBuildSettings.buildAppBundle = false;
            AssetDatabase.SaveAssets();
        }
    }
}
