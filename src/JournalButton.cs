using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RelicLoadoutJournal
{
    public sealed class JournalButton : Button
    {
        private static bool pointerMode;
        private TextMeshProUGUI caption;
        private JournalButtonArt.Plates plates;
        private SelectionState requested;
        private SelectionState? painted;
        private bool pointerInside, darkFocusText;

        internal void UseArt(JournalButtonArt.Plates value, TextMeshProUGUI label, bool darkFocus)
        {
            caption = label;
            plates = value;
            darkFocusText = darkFocus;
            if (plates != null)
            {
                transition = Transition.None;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = plates.PixelsPerUnitMultiplier;
                image.canvasRenderer.SetColor(Color.white);
            }
            DoStateTransition(currentSelectionState, true);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (!UsingGamepad()) pointerMode = true;
            base.OnPointerEnter(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            base.OnPointerExit(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!UsingGamepad()) pointerMode = true;
            base.OnPointerDown(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            pointerMode = false;
            base.OnMove(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            if (eventData is AxisEventData) pointerMode = false;
            base.OnSelect(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            pointerMode = false;
            base.OnSubmit(eventData);
        }

        protected override void OnDisable()
        {
            pointerInside = false;
            painted = null;
            base.OnDisable();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            requested = state;
            if (plates == null) base.DoStateTransition(state, instant);
            Paint();
        }

        private void LateUpdate() => Paint();

        private static bool UsingGamepad() => GamepadManager.Instance != null && GamepadManager.Instance.IsGamepadInUse();

        private void Paint()
        {
            if (caption == null || !isActiveAndEnabled) return;
            var state = requested;
            if (state == SelectionState.Selected && pointerMode && !UsingGamepad())
                state = pointerInside ? SelectionState.Highlighted : SelectionState.Normal;
            if (state == SelectionState.Pressed && pointerMode && !pointerInside)
                state = SelectionState.Normal;
            if (state == SelectionState.Highlighted && UsingGamepad())
                state = SelectionState.Normal;
            if (painted == state) return;
            painted = state;

            if (plates != null)
            {
                switch (state)
                {
                    case SelectionState.Highlighted:
                    case SelectionState.Selected:
                        image.sprite = plates.Focus;
                        break;
                    case SelectionState.Pressed:
                        image.sprite = plates.Press;
                        break;
                    case SelectionState.Disabled:
                        image.sprite = plates.Disabled;
                        break;
                    default:
                        image.sprite = plates.Rest;
                        break;
                }
            }

            bool focus = state == SelectionState.Highlighted || state == SelectionState.Selected;
            caption.color = state == SelectionState.Disabled ? new Color32(191, 175, 159, 255)
                : darkFocusText && focus ? new Color32(47, 32, 24, 255) : (Color32)Color.white;
        }
    }
}
