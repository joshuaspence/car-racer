using CarRacer.Editor;
using CarRacer.Game;
using NUnit.Framework;
using UnityEngine;

namespace CarRacer.Tests
{
    /// <summary>Things a phone build needs that the editor would hide: the editor has every shader, a build only what it references.</summary>
    public class BuildSettingsTests
    {
        [Test]
        public void TheMaterialShaderIsAlwaysIncludedInBuilds()
        {
            Assert.IsNotNull(Shader.Find(SimpleMaterials.ShaderName));
            ProjectConfigurator.Apply();
            Assert.IsTrue(ProjectConfigurator.IsAlwaysIncluded(SimpleMaterials.ShaderName));
        }

        [Test]
        public void MaterialsUseThatShader()
        {
            var material = SimpleMaterials.Get(new Color(0.1f, 0.2f, 0.3f));
            Assert.AreEqual(SimpleMaterials.ShaderName, material.shader.name);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f), material.color);
        }
    }
}
