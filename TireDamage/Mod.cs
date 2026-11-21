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
        public const string Version = "0.8.0";
        internal static MelonLogger.Instance Log;
        private static readonly string[] Categories = { "TireDamage" };
        private int _physicsErrors;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings.Init();
            CrashGuard.Init();
            Net.Init();
            Remote.Init();
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
            UpdateHud();
            UpdateGroundDrag();
            UpdateNet();
            if (Settings.Enabled.Value) Service.Update(HarmonyInstance);
        }

        private static void UpdateGroundDrag()
        {
            if (!CrashGuard.Enter("drag")) return;
            try { GroundDrag.Update(); }
            catch (System.Exception ex)
            {
                if (_dragErrors++ < 5) Log.Warning("Ground drag: " + ex);
            }
            CrashGuard.Exit("drag");
        }

        private static int _dragErrors, _netErrors, _hudErrors;

        private static void UpdateHud()
        {
            try { TireHud.Update(); }
            catch (System.Exception ex)
            {
                if (_hudErrors++ < 5) Log.Warning("Tire HUD: " + ex);
            }
        }

        private static void UpdateNet()
        {
            try
            {
                // Always on (even with the mod off) so the host can tell a client and put its car back.
                Net.EnsureHooked();
                Net.ProcessInbox();
                Remote.Update();
            }
            catch (System.Exception ex)
            {
                if (_netErrors++ < 5) Log.Warning("Multiplayer: " + ex);
            }
        }

        public override void OnFixedUpdate()
        {
            try
            {
                Tires.FixedUpdate();
                Remote.FixedUpdate();
            }
            catch (System.Exception ex)
            {
                // Runs every physics step: log the first few only.
                if (_physicsErrors++ < 5) Log.Warning("Tire physics: " + ex);
            }
        }

        public override void OnApplicationQuit() => Net.Unhook();

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
