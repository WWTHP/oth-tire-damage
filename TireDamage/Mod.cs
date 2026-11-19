using MelonLoader;

[assembly: MelonInfo(typeof(TireDamage.Mod), "TireDamage", TireDamage.Mod.Version, "WWTHP")]
[assembly: MelonGame(null, "over the hill")]

namespace TireDamage
{
    /// <summary>
    /// Tires take damage from hard hits, pop, and rupture if you keep driving on them. A popped
    /// tire can be patched with a tool kit cosmetic (pipe wrench / hi-lift jack, reusable); a spare
    /// wheel cosmetic replaces any flat once. Houses, the mechanic and camp replace every tire.
    /// TireKey (F4) opens the repair screen (<see cref="TirePanel"/>). Diagnostics and settings: the
    /// settings hub tab, or MenuKey without the hub.
    /// </summary>
    public sealed class Mod : MelonMod
    {
        public const string Version = "0.5.2";
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
            Log.Msg($"TireDamage {Version} loaded. {Settings.TireKey.Value} = tire repair screen; " +
                    $"settings in the settings hub (or {Settings.MenuKey.Value} without it).");
        }

        public override void OnUpdate()
        {
            bool keyTaken = Menu.Update();
            Car.Update();
            if (Settings.Enabled.Value && !keyTaken) TirePanel.Update();
            else if (TirePanel.IsOpen && !Settings.Enabled.Value) TirePanel.Toggle();
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
            if (!Settings.Enabled.Value) return;
            TirePanel.TrackMouse();
            if (TirePanel.IsOpen && TirePanel.UseFallback) Menu.DrawFallbackPanel();
            Toast.Draw();
        }
    }
}
