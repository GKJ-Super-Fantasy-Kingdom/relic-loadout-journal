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
            var relic = AddressablesManager.Instance?.GetRelic(id);
            if (relic == null) return detail;
            try
            {
                detail.Icon = relic.GetIcon();
                detail.Name = relic.GetTitle();
                detail.Description = relic.GetDescription();
            }
            catch (Exception e)
            {
                detail.Description = "Relic details unavailable.";
                Plugin.Warn("Could not display relic " + id + ": " + e.Message);
            }
            return detail;
        }
    }
}
