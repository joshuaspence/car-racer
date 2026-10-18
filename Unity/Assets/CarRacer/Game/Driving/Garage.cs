using CarRacer.Core.Art;
using CarRacer.Core.Session;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// The car this phone's player picked in the garage (design doc §11): a body and a colour,
    /// remembered on the phone and sent when hosting or joining. The host may give a different
    /// colour if someone else already has it.
    /// </summary>
    public static class Garage
    {
        public const string BodyKey = "carBody";
        public const string ColourKey = "carColour";

        public static int Body
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(BodyKey, 0), 0, LobbyRoster.BodyCount - 1);
            set => PlayerPrefs.SetInt(BodyKey, ((value % LobbyRoster.BodyCount) + LobbyRoster.BodyCount) % LobbyRoster.BodyCount);
        }

        public static int Colour
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ColourKey, 0), 0, LobbyRoster.ColourCount - 1);
            set => PlayerPrefs.SetInt(ColourKey, ((value % LobbyRoster.ColourCount) + LobbyRoster.ColourCount) % LobbyRoster.ColourCount);
        }

        public static CarChoice Choice => new CarChoice((byte)Body, (byte)Colour);

        public static CarModel Model => CarModels.ForIndex(Body);
    }
}
