using System;
using SuperFantasyKingdom;
using UnityEngine;

namespace RelicLoadoutJournal
{
    internal sealed class RelicDetail
    {
        internal string Name;
        internal string Description;
        internal Sprite Icon;

        internal static RelicDetail Resolve(string id)
        {
            var detail = new RelicDetail { Name = id, Description = "Relic unavailable in this installation." };
            try
            {
                var relic = AddressablesManager.Instance?.GetRelic(id);
                if (relic == null) return detail;
                detail.Icon = relic.GetIcon();
                var title = relic.GetTitle();
                if (!string.IsNullOrWhiteSpace(title)) detail.Name = title;
                detail.Description = ReadableDescription(relic.GetDescription());
            }
            catch (Exception e)
            {
                detail.Description = "Relic details unavailable.";
                Plugin.Warn("Could not display relic " + id + ": " + e.Message);
            }
            return detail;
        }

        private static string ReadableDescription(string text) => text?
            .Replace("<color=blue>", "<color=#9DC4FF>")
            .Replace("<color=purple>", "<color=#D7A4FF>")
            // Cursed Relics uses dark red for the curse on its light native card.
            .Replace("<color=#991F29>", "<color=#FFAAA1>");
    }
}
