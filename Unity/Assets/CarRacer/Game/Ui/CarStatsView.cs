using CarRacer.Core.Driving;
using UnityEngine.UIElements;

namespace CarRacer.Game.Ui
{
    /// <summary>A body's handling ratings as three bars (design doc §4, §11), for the garage and the lobby's car picker.</summary>
    public static class CarStatsView
    {
        /// <summary>Top speed, steering and acceleration bars; <paramref name="compact"/> for the lobby's narrow side panel.</summary>
        public static VisualElement Build(CarStats stats, bool compact = false)
        {
            // The lobby's side panel is narrow, so its labels are short.
            float labelWidth = compact ? 62f : 170f;
            float height = compact ? 20f : 26f;
            int size = compact ? 16 : 20;
            var column = new VisualElement { name = "car stats", pickingMode = PickingMode.Ignore };
            column.style.marginTop = column.style.marginBottom = 4f;
            column.Add(UiKit.Rating(compact ? "Speed" : "Top speed", stats.TopSpeed, CarStats.MaxRating, labelWidth, height, size));
            column.Add(UiKit.Rating(compact ? "Steer" : "Steering", stats.Steering, CarStats.MaxRating, labelWidth, height, size));
            column.Add(UiKit.Rating(compact ? "Accel" : "Acceleration", stats.Acceleration, CarStats.MaxRating, labelWidth, height, size));
            return column;
        }
    }
}
