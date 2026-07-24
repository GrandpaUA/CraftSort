using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace CraftSort
{
    [BepInPlugin("dev.craftsort", "CraftSort", "1.0.0")]
    public class CraftSortPlugin : BaseUnityPlugin
    {
        public static CraftSortPlugin Instance { get; private set; } = null!;

        private ConfigEntry<bool> _enabled = null!;
        private ConfigEntry<string> _defaultSortMode = null!;
        private ConfigEntry<bool> _rememberLastMode = null!;

        public static bool Enabled => Instance._enabled.Value;
        public static string DefaultSortMode => Instance._defaultSortMode.Value;
        public static bool RememberLastMode => Instance._rememberLastMode.Value;

        public static void Log(string msg) => Instance?.Logger.LogInfo(msg);

        private void Awake()
        {
            Instance = this;

            _enabled = Config.Bind("General", "Enabled", true, "Enable or disable the mod");
            _defaultSortMode = Config.Bind("General", "DefaultSortMode", "None", "Sort mode on open: None/Armor/Block/PhysDmg/etc");
            _rememberLastMode = Config.Bind("General", "RememberLastMode", false, "Keep last sort mode between station openings");

            if (!Enabled)
                return;

            new Harmony("dev.craftsort").PatchAll();
            Logger.LogInfo("CraftSort loaded");
        }
    }
}
