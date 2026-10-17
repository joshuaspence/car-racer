#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace CarRacer.Editor
{
    /// <summary>
    /// Adds the permissions LAN play needs to the generated Android manifest, so the project
    /// doesn't need a hand-maintained custom AndroidManifest.xml.
    /// </summary>
    public sealed class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        private static readonly string[] Permissions =
        {
            "android.permission.INTERNET",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.ACCESS_WIFI_STATE",
            // Required to acquire a WifiManager.MulticastLock for discovery.
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            "android.permission.VIBRATE",
        };

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string manifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            var document = new XmlDocument();
            document.Load(manifestPath);
            XmlElement manifest = document.DocumentElement;

            foreach (string permission in Permissions)
            {
                if (HasPermission(manifest, permission))
                {
                    continue;
                }

                XmlElement element = document.CreateElement("uses-permission");
                XmlAttribute name = document.CreateAttribute("android", "name", AndroidNamespace);
                name.Value = permission;
                element.Attributes.Append(name);
                manifest.PrependChild(element);
            }

            document.Save(manifestPath);
        }

        private static bool HasPermission(XmlElement manifest, string permission)
        {
            foreach (XmlNode node in manifest.ChildNodes)
            {
                if (node is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNamespace) == permission)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
