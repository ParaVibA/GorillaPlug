using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace GorillaPlug.Pluh
{
    internal class PluhConfig
    {
        private static ConfigFile ConfigFile { get; set; }

        internal static ConfigEntry<string> ServerUri { get; set; }
        internal static ConfigEntry<float> VibrateAmplifier { get; set; }

        internal static ConfigEntry<bool> VibrateOnVelocityEnabled { get; set; }
        internal static ConfigEntry<float> VibrateVelocityThreshold { get; set; }
        internal static ConfigEntry<float> VibrateVelocityStrength { get; set; }

        internal static ConfigEntry<bool> VibrateOnTagEnabled { get; set; }
        internal static ConfigEntry<float> VibrateOnTagDuration { get; set; }
        internal static ConfigEntry<float> VibrateOnTagStrength { get; set; }

        static PluhConfig()
        {
            string configPath = Path.Combine(Paths.ConfigPath, "GorillaPlug.cfg");
            ConfigFile = new ConfigFile(configPath, true);

            ServerUri = ConfigFile.Bind(
                "Devices",
                "Server Uri",
                "ws://localhost:12345",
                "URI of the Intiface server."
            );

            VibrateAmplifier = ConfigFile.Bind("Vibrations", "Amplifier", 0.0f, "Global amplification of vibration");

            VibrateOnVelocityEnabled = ConfigFile.Bind("Vibrations.Movement", "VelocityEnabled", true, "Vibrate when moving fast (Screen Shake equivalent)");
            VibrateVelocityThreshold = ConfigFile.Bind("Vibrations.Movement", "VelocityThreshold", 8.0f, "Speed required to trigger vibration");
            VibrateVelocityStrength = ConfigFile.Bind("Vibrations.Movement", "VelocityStrength", 0.3f, "Strength of vibration when moving fast");

            VibrateOnTagEnabled = ConfigFile.Bind("Vibrations.Game", "TagEnabled", true, "Vibrate when you get tagged");
            VibrateOnTagDuration = ConfigFile.Bind("Vibrations.Game", "TagDuration", 1.0f, "Duration of vibration");
            VibrateOnTagStrength = ConfigFile.Bind("Vibrations.Game", "TagStrength", 1.0f, "Strength of vibration");
        }
    }
}