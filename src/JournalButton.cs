using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RelicLoadoutJournal
{
    public sealed class JournalButton : Button
    {
        private TextMeshProUGUI caption;

        internal void SetCaption(TextMeshProUGUI value)
        {
            caption = value;
            Paint(currentSelectionState);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            Paint(state);
        }

        private void Paint(SelectionState state)
        {
            if (caption == null) return;
            switch (state)
            {
                case SelectionState.Selected:
                case SelectionState.Highlighted:
                    caption.color = new Color32(47, 32, 24, 255);
                    break;
                case SelectionState.Disabled:
                    caption.color = new Color32(191, 175, 159, 255);
                    break;
                default:
                    caption.color = Color.white;
                    break;
            }
        }
    }
}
