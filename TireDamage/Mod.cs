using MelonLoader;

[assembly: MelonInfo(typeof(TireDamage.Mod), "TireDamage", TireDamage.Mod.Version, "WWTHP")]
[assembly: MelonGame(null, "over the hill")]

namespace TireDamage
{
    /// <summary>
    /// Tires take damage from hard hits, pop, and rupture if you keep driving on them. A popped
    /// tire can be patched with a tool kit cosmetic (pipe wrench / hi-lift jack, reusable); a spare
    /// wheel cosmetic replaces any flat once. Houses, the mechanic and camp replace every tire.
    /// Hold RepairKey (F4) while stopped to repair. Settings: the settings hub tab, or MenuKey.
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public const string Version = "0.2.0";
        internal static MelonLogger.Instance Log;
        private static readonly string[] Categories = { "TireDamage" };
        private int _physicsErrors;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings.Init();
            CrashGuard.Init();
            HubTab.Register("Tires", Menu.DrawBody, categories: Categories,
                closed: Menu.SaveIfDirty, reset: Menu.ResetDefaults, order: 10, owner: "TireDamage " + Version);
            Log.Msg($"TireDamage {Version} loaded. Hold {Settings.RepairKey.Value} to repair; " +
                    $"settings in the settings hub (or {Settings.MenuKey.Value} without it).");
        }

        public override void OnUpdate()
        {
            Menu.Update();
            Car.Update();
            Tires.Update();
            if (Settings.Enabled.Value) Service.Update(HarmonyInstance);
        }

        public override void OnFixedUpdate()
        {
            try { Tires.FixedUpdate(); }
            catch (System.Exception ex)
            {
                // Runs every physics step: log the first few only.
                if (_physicsErrors++ < 5) Log.Warning("Tire physics: " + ex);
            }
        }

        public override void OnGUI()
        {
            Menu.Draw();
            if (Settings.Enabled.Value) Toast.Draw();
        }
    }
}
