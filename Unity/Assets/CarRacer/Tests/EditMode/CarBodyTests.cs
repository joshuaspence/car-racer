using CarRacer.Core.Art;
using CarRacer.Game;
using CarRacer.Game.Driving;
using NUnit.Framework;
using UnityEngine;

namespace CarRacer.Tests
{
    /// <summary>The Kenney car loads from its OBJ and palette, painted in the player's colour.</summary>
    public class CarBodyTests
    {
        [Test]
        public void TheRaceCarBuildsAsOnePaintedMesh()
        {
            var visual = new GameObject("Body").transform;
            try
            {
                Assert.IsTrue(CarBodies.TryBuild(visual, Color.blue, CarModels.Race));
                var body = visual.GetChild(0);
                var mesh = body.GetComponent<MeshFilter>().sharedMesh;
                Assert.Greater(mesh.GetIndexCount(0) / 3, 1000u);
                Assert.AreEqual(0f, mesh.bounds.min.y, 0.01f);
                Assert.Greater(mesh.bounds.center.z, -0.5f);

                var material = body.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreEqual(SimpleMaterials.ShaderName, material.shader.name);
                Assert.IsNotNull(material.mainTexture);
                Assert.AreEqual(512, material.mainTexture.width);
            }
            finally
            {
                Object.DestroyImmediate(visual.gameObject);
            }
        }

        [Test]
        public void EachColourGetsItsOwnPaintAndSharesTheMesh()
        {
            var a = new GameObject("A").transform;
            var b = new GameObject("B").transform;
            try
            {
                CarBodies.TryBuild(a, Color.red, CarModels.Race);
                CarBodies.TryBuild(b, Color.green, CarModels.Race);
                Assert.AreSame(a.GetChild(0).GetComponent<MeshFilter>().sharedMesh, b.GetChild(0).GetComponent<MeshFilter>().sharedMesh);
                Assert.AreNotSame(a.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial, b.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }
    }
}
