using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;
using SuperFantasyKingdom;
using UnityEngine;
using UnityEngine.UI;

// Execute the shipped callback and reader in the game's Mono runtime. Only the test process
// supplies scene objects, native names, the temporary persistent path and the profile preference.
internal static class RunChecks
{
    private static T SceneObject<T>() where T : UnityEngine.Object
    {
        var obj = (T)FormatterServices.GetUninitializedObject(typeof(T));
        AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr").SetValue(obj, new IntPtr(1));
        return obj;
    }

    private static void Set(object obj, string field, object value)
    {
        AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
    }

    private static void Check(bool condition, string text)
    {
        if (!condition) throw new InvalidOperationException(text);
        Console.WriteLine("PASS " + text);
    }

    private static void CheckGameContract(string file)
    {
        using (var game = AssemblyDefinition.ReadAssembly(file))
        {
            var methods = game.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).ToList();
            var completions = methods.Where(m => m.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).FullName == "System.Void MainManager::LoadTitleScene(System.Boolean)" &&
                i.Previous.OpCode == OpCodes.Ldc_I4_1)).ToList();
            Check(completions.Count == 1 && completions[0].Name == "SaveAndUnload",
                "installed game returns to title with completion=true only during finalization");
            var data = game.MainModule.GetType("SuperFantasyKingdom.GameDataManager");
            var load = data.Methods.Single(m => m.Name == "Load").Body.Instructions;
            Check(load.Any(i => i.OpCode == OpCodes.Stfld && ((FieldReference)i.Operand).Name == "m_RunID" &&
                i.Previous.OpCode == OpCodes.Ldfld && ((FieldReference)i.Previous.Operand).Name == "runID"),
                "installed continue path restores the saved run ID");
            Check(data.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i =>
                i.OpCode == OpCodes.Stfld && ((FieldReference)i.Operand).Name == "runID" &&
                i.Previous.OpCode == OpCodes.Ldfld && ((FieldReference)i.Previous.Operand).Name == "m_RunID"),
                "installed save path writes the same run ID");
        }
    }

    private static void RestoreFixture(JObject save, List<string> held)
    {
        GameDataManager.Instance = SceneObject<GameDataManager>();
        Set(GameDataManager.Instance, "m_RunID", (string)save["runID"]);
        RaceManager.Instance = SceneObject<RaceManager>();
        Set(RaceManager.Instance, "race", (Races)(int)save["kingdom"]);
        UnitManager.Instance = SceneObject<UnitManager>();
        var hero = SceneObject<Hero>();
        Set(hero, "entityIdentifier", (string)save["units"].Single(u => (int)u["identifier"] == 0)["name"]);
        Set(UnitManager.Instance, "m_Hero", hero);
        DaytimeManager.Instance = SceneObject<DaytimeManager>();
        Set(DaytimeManager.Instance, "m_CurrentDay", (int)save["day"] + 1);
        SettingsManager.Instance = SceneObject<SettingsManager>();
        RelicManager.Instance = SceneObject<RelicManager>();
        Set(RelicManager.Instance, "m_Relics", held.Select(id => {
            var relic = SceneObject<Relic>();
            Set(relic, "identifier", id);
            return relic;
        }).ToList());
    }

    private static int Main(string[] args)
    {
        CheckGameContract(typeof(GameDataManager).Assembly.Location);
        var mod = Assembly.LoadFrom(args[0]);
        var patches = new Harmony("test.relic-journal.bindings");
        try
        {
            patches.PatchAll(mod);
            Check(patches.GetPatchedMethods().Count() == 3, "all three shipped Harmony patches bind to the installed game");
        }
        finally { patches.UnpatchSelf(); }
        CheckScreen(mod);
        var save = JObject.Parse(File.ReadAllText(args[1]));
        var root = Path.Combine(args[2], "RelicLoadoutJournal");
        var plugin = mod.GetType("RelicLoadoutJournal.Plugin", true);
        var prefix = AccessTools.Method(plugin.GetNestedType("Complete", BindingFlags.NonPublic), "Prefix");
        var current = AccessTools.Method(plugin, "CurrentJournal");
        Action<bool> transition = done => prefix.Invoke(null, new object[] { done });
        var held = save["relics"].Values<string>().ToList();
        RestoreFixture(save, held);
        transition(false);
        Check(!Directory.Exists(root), "save and ordinary return to title create no history");
        const string missing = "removed.mod/relic-A";
        held.Add(missing);
        RestoreFixture(JObject.Parse(File.ReadAllText(args[1])), held);
        transition(true);
        string folder = Path.Combine(root, "profile-1");
        string path = Directory.GetFiles(folder, "*.json").Single();
        string original = File.ReadAllText(path);
        var saved = JObject.Parse(original);
        Check((Guid)saved["RunId"] == (Guid)save["runID"] && (int)saved["Kingdom"] == (int)save["kingdom"] &&
            (string)saved["Hero"] == UnitManager.Instance.GetHero().GetEntityIdentifier() &&
            (int)saved["FinalDay"] == (int)save["day"] + 1 && saved["Relics"].Values<string>().SequenceEqual(held),
            "continued fixture records kingdom, hero, final day and the final held loadout");
        Check(saved.Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(
            new[] { "CompletedUtc", "FinalDay", "Hero", "Kingdom", "Relics", "RunId" }),
            "persisted schema contains metadata and IDs only");
        RestoreFixture(save, new List<string>());
        transition(true);
        Check(Directory.GetFiles(folder, "*.json").Length == 1 && File.ReadAllText(path) == original,
            "reloaded duplicate callback preserves the first completed snapshot byte for byte");
        Set(GameDataManager.Instance, "m_RunID", Guid.NewGuid().ToString());
        transition(true);
        File.WriteAllText(Path.Combine(folder, "interrupted.tmp"), "{");
        File.WriteAllText(Path.Combine(folder, "damaged.json"), "{");
        var journal = current.Invoke(null, null);
        var warnings = new List<string>();
        var read = AccessTools.Method(journal.GetType(), "Read");
        var history = (IList)read.Invoke(journal, new object[] { new Action<string>(warnings.Add) });
        Check(history.Count == 2 && warnings.Count == 1, "reader keeps distinct runs and skips interrupted and damaged records");
        var latest = JObject.FromObject(history[0]);
        Check((Guid)latest["RunId"] != (Guid)save["runID"] && !latest["Relics"].Any(), "newest run first and empty loadout retained");
        var other = AccessTools.Constructor(journal.GetType(), new[] { typeof(string), typeof(int) }).Invoke(new object[] { root, 2 });
        Check(((IList)read.Invoke(other, new object[] { new Action<string>(warnings.Add) })).Count == 0, "other game profile stays empty");
        AddressablesManager.Instance = SceneObject<AddressablesManager>();
        Set(AddressablesManager.Instance, "m_Relics", new Dictionary<string, Relic>());
        var detail = AccessTools.Method(mod.GetType("RelicLoadoutJournal.RelicDetail"), "Resolve").Invoke(null, new object[] { missing });
        Check((string)AccessTools.Field(detail.GetType(), "Name").GetValue(detail) == missing &&
            AccessTools.Field(detail.GetType(), "Icon").GetValue(detail) == null &&
            ((string)AccessTools.Field(detail.GetType(), "Description").GetValue(detail)).Contains("unavailable"),
            "missing modded relic shows its exact ID and fallback description without an icon");
        return 0;
    }

    // Exercise Selectable's real navigation setters without asking Unity to repaint a scene.
    private sealed class TestButton : Button
    {
        protected override void DoStateTransition(SelectionState state, bool instant) { }
    }

    private static void CheckScreen(Assembly mod)
    {
        var type = mod.GetType("RelicLoadoutJournal.JournalScreen", true);
        var page = (Vector2)AccessTools.Field(type, "PageSize").GetValue(null);
        foreach (float scale in new[] { 1f, 1.5f })
        foreach (var resolution in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(1024, 576) })
        {
            var available = resolution / scale;
            float fit = (float)AccessTools.Method(type, "FitScale").Invoke(null, new object[] { available });
            if (fit <= 0 || fit > 1 || page.x * fit >= available.x || page.y * fit >= available.y)
                throw new Exception("Page escaped the available canvas");
        }
        Console.WriteLine("PASS page fits modelled 1x and 1.5x canvases at three resolutions");
        var screen = FormatterServices.GetUninitializedObject(type);
        string[] names = { "newer", "older", "previous", "next", "back" };
        var buttons = names.Select(name => {
            var button = SceneObject<TestButton>();
            Set(screen, name, button);
            return button;
        }).ToArray();
        for (int mask = 0; mask < 16; mask++)
        {
            for (int i = 0; i < buttons.Length; i++)
                Set(buttons[i], "m_Interactable", i == 4 || (mask & (1 << i)) != 0);
            AccessTools.Method(type, "WireNavigation").Invoke(screen, null);
            var visited = new HashSet<Selectable>();
            var queue = new Queue<Selectable>();
            queue.Enqueue(buttons[4]);
            while (queue.Count > 0)
            {
                var button = queue.Dequeue();
                if (!visited.Add(button)) continue;
                if (!button.interactable) throw new Exception("Focus reached disabled action");
                var nav = button.navigation;
                foreach (var target in new[] { nav.selectOnLeft, nav.selectOnRight, nav.selectOnUp, nav.selectOnDown })
                    queue.Enqueue(target);
            }
            if (visited.Count != buttons.Count(b => b.interactable)) throw new Exception("Unreachable action");
        }
        Console.WriteLine("PASS directional focus reaches all enabled actions across 16 pagination states");
    }
}
