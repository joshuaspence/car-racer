using System.Collections.Generic;

namespace CarRacer.Core.Driving
{
    /// <summary>
    /// The "how to drive" tips shown the first time on the track (design doc §5), worded for how
    /// this phone is set up: which side steers, tilt, auto-accelerate and whether Look back is
    /// there. Button names are in capitals, as they are on the buttons.
    /// </summary>
    public static class DrivingTips
    {
        public const string SeenKey = "drivingTipsSeen";

        public static List<string> For(bool leftHanded, bool autoAccelerate, bool tilt, bool lookBack)
        {
            string steerSide = leftHanded ? "right" : "left";
            string buttonSide = leftHanded ? "left" : "right";
            var tips = new List<string>
            {
                tilt
                    ? $"Turn the phone like a steering wheel to steer. You can still drag on the {steerSide} half to correct."
                    : $"Steer by dragging left or right anywhere on the {steerSide} half of the screen.",
                autoAccelerate
                    ? $"Your car accelerates by itself. Hold BRAKE (on the {buttonSide}) to slow down or reverse."
                    : $"Hold GAS (on the {buttonSide}) to go and BRAKE to slow down or reverse.",
                "Hold DRIFT through a corner to charge the meter, then let go for a boost. The longer the drift, the bigger the boost.",
                "In a race, drive through item boxes, then tap the item button to use what you picked up.",
            };

            if (lookBack)
            {
                tips.Add("Hold LOOK BACK to see who's behind you.");
            }

            tips.Add("Bump other cars to knock them off your line. Everyone's car handles the same.");
            return tips;
        }
    }
}
