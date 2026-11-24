using System;
using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RelicLoadoutJournal
{
    internal sealed class ControlHint
    {
        private readonly Image icon;
        private readonly TextMeshProUGUI label;
        private readonly string verb;
        private string lastPath;
        private InputDevice lastDevice;

        internal ControlHint(RectTransform parent, string verb, float x,
            Func<Transform, string, float, Color, TextMeshProUGUI> makeLabel)
        {
            this.verb = verb;
            icon = JournalScreen.Box(parent, verb + " prompt", Color.white).GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            JournalScreen.Place(icon.rectTransform, x, 465, 35, 34);
            label = makeLabel(parent, verb, 20, Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            JournalScreen.Place(label.rectTransform, x + 40, 462, 150, 40);
        }

        internal void Refresh(InputAction action, bool pad)
        {
            if (action != null)
            {
                foreach (var control in action.controls)
                {
                    if (pad ? !(control.device is Gamepad) : !(control.device is Keyboard)) continue;
                    if (pad && Gamepad.current != null && control.device != Gamepad.current) continue;
                    int i = action.GetBindingIndexForControl(control);
                    if (i < 0) continue;
                    var entry = action.bindings[i];
                    string path = entry.effectivePath;
                    if (!string.IsNullOrEmpty(entry.effectiveProcessors)) path += "Processor";
                    if (lastPath == path && lastDevice == control.device) return;
                    lastPath = path;
                    lastDevice = control.device;
                    var sprite = GamepadManager.Instance.GetButtonPrompt(path);
                    if (sprite == null)
                    {
                        string device = pad ? "Gamepad" : "Keyboard";
                        sprite = GamepadManager.Instance.GetButtonPrompt("<" + device + ">/" + control.name);
                    }
                    icon.enabled = sprite != null;
                    icon.sprite = sprite;
                    label.text = sprite != null ? verb : (control.shortDisplayName ?? control.displayName) + "  " + verb;
                    return;
                }
            }
            lastPath = null;
            lastDevice = null;
            icon.enabled = false;
            label.text = "";
        }
    }
}
