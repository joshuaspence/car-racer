using System;
using System.Text.RegularExpressions;

namespace CarRacer.Core.Platform
{
    public enum QualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>What a quality level changes. Everything else (flat shading, no shadows) is the same on every phone.</summary>
    public struct QualitySettingsValues
    {
        /// <summary>Rendering resolution as a share of the screen's native resolution.</summary>
        public float RenderScale;

        /// <summary>MSAA samples: 0 (off), 2 or 4.</summary>
        public int Msaa;

        /// <summary>The share of the trees a track gets (<see cref="Art.TrackScenery"/>), from 0 to 1.</summary>
        public float TreeShare;
    }

    /// <summary>
    /// Picks a quality level on first launch (design doc §14) from the GPU's name and the phone's
    /// memory, whichever is lower. The Pixel 2 XL (Adreno 540, 4 GB) is the baseline and gets Low,
    /// which renders at 0.65 of native resolution to hold 60 fps.
    /// </summary>
    public static class QualityPresets
    {
        private static readonly Regex AdrenoModel = new Regex(@"Adreno[^0-9]*([0-9]{3})", RegexOptions.IgnoreCase);
        private static readonly Regex MaliModel = new Regex(@"Mali-G([0-9]{2,3})", RegexOptions.IgnoreCase);

        public static QualitySettingsValues For(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.High:
                    return new QualitySettingsValues { RenderScale = 1f, Msaa = 4, TreeShare = 1f };
                case QualityLevel.Medium:
                    return new QualitySettingsValues { RenderScale = 0.75f, Msaa = 2, TreeShare = 1f };
                default:
                    return new QualitySettingsValues { RenderScale = 0.65f, Msaa = 0, TreeShare = 0.5f };
            }
        }

        /// <param name="gpuName">The GPU's name as the graphics driver reports it (e.g. "Adreno (TM) 540").</param>
        /// <param name="memoryMb">The phone's memory in MB.</param>
        public static QualityLevel Pick(string gpuName, int memoryMb)
        {
            var byMemory = memoryMb < 4500 ? QualityLevel.Low : memoryMb < 7000 ? QualityLevel.Medium : QualityLevel.High;
            return (QualityLevel)Math.Min((int)byMemory, (int)ByGpu(gpuName ?? string.Empty));
        }

        /// <summary>
        /// Adreno 5xx and older and Mali before G72 are Low; Adreno 6xx below 640 and Mali G72–G76
        /// are Medium; newer ones are High. A GPU we don't recognise doesn't hold the level down.
        /// </summary>
        private static QualityLevel ByGpu(string gpuName)
        {
            var adreno = AdrenoModel.Match(gpuName);
            if (adreno.Success)
            {
                int model = int.Parse(adreno.Groups[1].Value);
                return model < 600 ? QualityLevel.Low : model < 640 ? QualityLevel.Medium : QualityLevel.High;
            }

            var mali = MaliModel.Match(gpuName);
            if (mali.Success)
            {
                int model = int.Parse(mali.Groups[1].Value);

                // Mali's numbering restarted after G78 (G310, G610, G710…), all of which are newer.
                if (model >= 100)
                {
                    return model >= 600 ? QualityLevel.High : QualityLevel.Medium;
                }

                return model < 72 ? QualityLevel.Low : model < 77 ? QualityLevel.Medium : QualityLevel.High;
            }

            return QualityLevel.High;
        }
    }
}
