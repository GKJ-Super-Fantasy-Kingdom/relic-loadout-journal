using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.TitleScreen;
using UnityEngine;

namespace RelicLoadoutJournal
{
    [BepInPlugin(Id, "Relic Loadout Journal", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Id = "sfk.relicloadoutjournal";
        private static Plugin instance;
        private Harmony patches;

        private void Awake()
        {
            instance = this;
            patches = new Harmony(Id);
            patches.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            patches?.UnpatchSelf();
            instance = null;
            if (JournalScreen.Current != null) Destroy(JournalScreen.Current);
        }

        internal static void Warn(string message) => instance?.Logger.LogWarning(message);

        internal static Journal CurrentJournal() => new Journal(
            Path.Combine(Application.persistentDataPath, "RelicLoadoutJournal"), SettingsManager.Instance.GetProfile() + 1);

        [HarmonyPatch(typeof(MainManager), nameof(MainManager.LoadTitleScene))]
        private static class Complete
        {
            // SaveAndUnload has finalized the run. A retryable loss or save-and-exit never reaches this with true.
            private static void Prefix(bool runComplete)
            {
                if (!runComplete) return;
                try
                {
                    var run = new CompletedRun {
                        RunId = Guid.Parse(GameDataManager.Instance.GetRunID()),
                        CompletedUtc = DateTime.UtcNow,
                        Kingdom = (int)RaceManager.Instance.GetRace(),
                        Hero = UnitManager.Instance.GetHero().GetEntityIdentifier(),
                        FinalDay = DaytimeManager.Instance.CurrentDay(),
                        Relics = RelicManager.Instance.GetRelics().Where(r => r != null).Select(r => r.name).ToArray()
                    };
                    CurrentJournal().Append(run);
                }
                catch (Exception e) { Warn("Could not record completed run: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(TitleScreenManager), "Start")]
        private static class TitleMenu
        {
            private static void Postfix(TitleScreenManager __instance, GameObject ___firstButton,
                GameObject ___settingsButton, GameObject ___continueOverlay)
            {
                try
                {
                    __instance.gameObject.AddComponent<JournalScreen>().Attach(__instance, ___firstButton, ___settingsButton, ___continueOverlay);
                }
                catch (Exception e) { Warn("Could not attach journal: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(TitleScreenManager), nameof(TitleScreenManager.Close), new Type[0])]
        private static class Back
        {
            private static bool Prefix()
            {
                if (JournalScreen.Current == null || !JournalScreen.Current.IsOpen) return true;
                JournalScreen.Current.Close();
                return false;
            }
        }
    }
}
