using System;
using System.Collections.Generic;
using System.Globalization;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Art
{
    /// <summary>The diffuse colours (<c>Kd</c>) in an OBJ material library, as Kenney's Racing Kit colours its pieces.</summary>
    public static class ObjMaterials
    {
        /// <summary>Material name to colour (0..1 per channel), added to <paramref name="into"/>; names already there are kept.</summary>
        public static void Parse(string text, Dictionary<string, Vec3> into)
        {
            string current = null;
            foreach (string raw in text.Split('\n'))
            {
                string[] f = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length == 0)
                {
                    continue;
                }

                if (f[0] == "newmtl" && f.Length > 1)
                {
                    current = f[1];
                }
                else if (f[0] == "Kd" && f.Length > 3 && current != null && !into.ContainsKey(current))
                {
                    into[current] = new Vec3(Float(f[1]), Float(f[2]), Float(f[3]));
                }
            }
        }

        private static float Float(string token) => float.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
