using System;
using System.Collections.Generic;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.TitleScreen;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RelicLoadoutJournal
{
    public sealed class JournalScreen : MonoBehaviour
    {
        internal static JournalScreen Current { get; private set; }
        internal bool IsOpen => overlay != null && overlay.gameObject.activeSelf;

        private static readonly Color Gold = new Color32(255, 217, 146, 255);
        private static readonly Color Parchment = new Color32(255, 239, 202, 255);
        private TextMeshProUGUI style, page, summary, count, relicName, description;
        private Image buttonStyle, icon, crest;
        private TextMeshProUGUI day, completed;
        private const int StripSize = 5;
        private readonly Button[] slots = new Button[StripSize];
        private readonly Image[] slotIcons = new Image[StripSize];
        private readonly GameObject[] markers = new GameObject[StripSize];
        private readonly Dictionary<string, RelicDetail> details = new Dictionary<string, RelicDetail>();
        private int stripStart, visibleSlots;
        private RectTransform overlay, sheet, play, viewport;
        private Button launcher, newer, older, previous, next, back;
        private ControlHint submitHint, cancelHint;
        private Action<TitleScreenManager> openOverlay, closeOverlay;
        private TitleScreenManager title;
        private List<CompletedRun> runs;
        private int runIndex, relicIndex;
        private bool readFailed;

        internal void Attach(TitleScreenManager manager, GameObject first, GameObject settings, GameObject continueOverlay)
        {
            title = manager;
            openOverlay = AccessTools.MethodDelegate<Action<TitleScreenManager>>(AccessTools.Method(typeof(TitleScreenManager), "OpenOverlay"));
            closeOverlay = AccessTools.MethodDelegate<Action<TitleScreenManager>>(AccessTools.Method(typeof(TitleScreenManager), "CloseOverlay"));
            style = first.GetComponentInChildren<TextMeshProUGUI>(true);
            buttonStyle = first.GetComponent<Image>();
            var canvas = first.GetComponentInParent<Canvas>(true).rootCanvas;
            var playRect = (RectTransform)first.transform;
            play = playRect;
            viewport = (RectTransform)canvas.transform;
            launcher = MakeButton(playRect.parent, "Relic Journal", Open);
            var launcherRect = (RectTransform)launcher.transform;
            // The title scene serializes a disabled layout group and positions its buttons by hand.
            // Anchor beside Play so changing scale also moves the journal with the native menu.
            launcherRect.anchorMin = playRect.anchorMin;
            launcherRect.anchorMax = playRect.anchorMax;
            launcherRect.pivot = playRect.pivot;
            launcherRect.sizeDelta = playRect.sizeDelta;
            launcherRect.anchoredPosition = playRect.anchoredPosition - Vector2.right * (playRect.rect.width + 12);
            launcherRect.SetSiblingIndex(settings.transform.GetSiblingIndex());
            FitLauncher();
            Current = this;
            overlay = Box(canvas.transform, "Relic Loadout Journal", new Color32(20, 14, 12, 237));
            Stretch(overlay);
            overlay.gameObject.SetActive(false);
            sheet = Box(overlay, "Journal", new Color32(94, 67, 46, 255));
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.anchoredPosition = Vector2.zero;
            var nativeFrame = continueOverlay.transform.Find("Panel")?.GetComponent<Image>();
            if (nativeFrame != null && nativeFrame.sprite != null)
            {
                var frame = sheet.GetComponent<Image>();
                frame.sprite = nativeFrame.sprite;
                frame.type = nativeFrame.type;
                frame.pixelsPerUnitMultiplier = nativeFrame.pixelsPerUnitMultiplier;
                frame.color = nativeFrame.color;
            }
            sheet.sizeDelta = PageSize;
            var inset = Box(sheet, "Page", new Color32(52, 38, 32, 255));
            Stretch(inset, 10);
            Place(Label(sheet, "Relic Loadout Journal", 38, Gold).rectTransform, 24, 12, 732, 54);
            page = Label(sheet, "", 23, Parchment);
            Place(page.rectTransform, 205, 74, 370, 29);
            completed = Label(sheet, "", 20, Parchment);
            Place(completed.rectTransform, 205, 103, 370, 26);
            newer = PageButton("Newer run", 30, 80, () => ChangeRun(-1));
            older = PageButton("Older run", 570, 80, () => ChangeRun(1));
            crest = Box(sheet, "Kingdom crest", Color.white).GetComponent<Image>();
            crest.preserveAspect = true;
            crest.raycastTarget = false;
            Place(crest.rectTransform, 44, 142, 42, 42);
            summary = Label(sheet, "", 31, Gold);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            day = Label(sheet, "", 29, Parchment);
            day.alignment = TextAlignmentOptions.MidlineRight;
            Place(day.rectTransform, 550, 137, 186, 50);
            var entry = Box(sheet, "Held relic", new Color32(37, 28, 25, 255));
            Place(entry, 28, 195, 724, 190);
            icon = Box(entry, "Relic icon", Color.white).GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Place(icon.rectTransform, 24, 48, 90, 90);
            relicName = Label(entry, "", 32, Gold);
            relicName.alignment = TextAlignmentOptions.MidlineLeft;
            relicName.overflowMode = TextOverflowModes.Ellipsis;
            relicName.parseCtrlCharacters = false;
            description = Label(entry, "", 30, Parchment);
            description.alignment = TextAlignmentOptions.MidlineLeft;
            previous = PageButton("Previous relic", 30, 398, () => ChangeRelic(-1));
            next = PageButton("Next relic", 570, 398, () => ChangeRelic(1));
            count = Label(entry, "", 21, Parchment);
            count.alignment = TextAlignmentOptions.MidlineLeft;
            for (int i = 0; i < StripSize; i++)
            {
                int slot = i;
                var button = MakeButton(sheet, "?", () => InspectSlot(slot));
                button.name = "Held relic " + (i + 1);
                slots[i] = button;
                var image = Box(button.transform, "Icon", Color.white).GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                Stretch(image.rectTransform, 7);
                slotIcons[i] = image;
                var mark = Box(button.transform, "Inspected relic", Parchment);
                mark.GetComponent<Image>().raycastTarget = false;
                Place(mark, 7, 50, 34, 3);
                markers[i] = mark.gameObject;
            }
            back = PageButton("Back", 300, 460, Close);
            submitHint = new ControlHint(sheet, "Select", 30, Label);
            cancelHint = new ControlHint(sheet, "Back", 540, Label);
        }

        private void Open()
        {
            if (IsOpen || !title.IsOpen()) return;
            readFailed = false;
            try { runs = Plugin.CurrentJournal().Read(message => { readFailed = true; Plugin.Warn(message); }); }
            catch (Exception e)
            {
                Plugin.Warn("Could not read history: " + e.Message);
                runs = new List<CompletedRun>();
                readFailed = true;
            }
            runIndex = relicIndex = 0;
            details.Clear();
            openOverlay(title);
            overlay.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            Fit();
            Render();
            Focus(visibleSlots > 0 ? slots[0] : older.interactable ? older : back);
        }

        internal void Close()
        {
            if (!IsOpen) return;
            overlay.gameObject.SetActive(false);
            closeOverlay(title);
            Focus(launcher);
        }

        private static void Focus(Button button)
        {
            GamepadManager.Instance.SetGameObjectToSelect(button.gameObject, force: true);
            EventSystem.current?.SetSelectedGameObject(button.gameObject);
        }

        private void Update()
        {
            if (!IsOpen)
            {
                if (launcher != null && launcher.gameObject.activeInHierarchy) FitLauncher();
                return;
            }
            Fit();
            var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
            bool pad = GamepadManager.Instance.IsGamepadInUse();
            submitHint.Refresh(module?.submit?.action, pad);
            cancelHint.Refresh(module?.cancel?.action, pad);
            // Restore focus after a pointer click on the page or a device switch.
            if (pad && EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null || !selected.transform.IsChildOf(overlay) ||
                    !selected.activeInHierarchy || selected.TryGetComponent<Button>(out var b) && !b.IsInteractable())
                    Focus(visibleSlots > 0 ? slots[relicIndex - stripStart] : back);
            }
        }

        private void FitLauncher()
        {
            var edge = viewport.TransformPoint(new Vector3(viewport.rect.xMin, viewport.rect.center.y));
            float room = play.rect.xMin - play.InverseTransformPoint(edge).x - 22;
            float width = Mathf.Clamp(room, 1, play.rect.width);
            var rect = (RectTransform)launcher.transform;
            if (!Mathf.Approximately(rect.rect.width, width))
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var position = play.anchoredPosition;
            position.x += play.rect.xMin - 12 - (1 - rect.pivot.x) * width;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }

        private void Fit()
        {
            sheet.localScale = Vector3.one * FitScale(overlay.rect.size);
        }

        private static readonly Vector2 PageSize = new Vector2(780, 520);

        internal static float FitScale(Vector2 available) => Mathf.Clamp(
            Mathf.Min(available.x / (PageSize.x + 20), available.y / (PageSize.y + 20)), 0, 1);

        private void ChangeRun(int delta)
        {
            runIndex = Mathf.Clamp(runIndex + delta, 0, runs.Count - 1);
            relicIndex = 0;
            details.Clear();
            Render();
            if (delta > 0 && !older.interactable) Focus(newer);
            if (delta < 0 && !newer.interactable) Focus(older);
        }

        private void ChangeRelic(int delta)
        {
            relicIndex = Mathf.Clamp(relicIndex + delta, 0, runs[runIndex].Relics.Length - 1);
            Render();
            if (delta > 0 && !next.interactable) Focus(previous);
            if (delta < 0 && !previous.interactable) Focus(next);
        }

        private void InspectSlot(int slot)
        {
            relicIndex = stripStart + slot;
            Render();
            Focus(slots[slot]);
        }

        private RelicDetail Detail(string id)
        {
            if (!details.TryGetValue(id, out var detail))
                details[id] = detail = RelicDetail.Resolve(id);
            return detail;
        }

        private void Render()
        {
            var run = runs.Count == 0 ? null : runs[runIndex];
            int held = run?.Relics.Length ?? 0;
            newer.interactable = runIndex > 0;
            older.interactable = runIndex + 1 < runs.Count;
            previous.interactable = relicIndex > 0;
            next.interactable = relicIndex + 1 < held;
            page.text = run == null ? "" : $"Run {runs.Count - runIndex} of {runs.Count}";
            count.text = held == 0 ? "" : $"Relic {relicIndex + 1} of {held}";
            ShowRun(run);
            var id = held == 0 ? null : run.Relics[relicIndex];
            var detail = id == null ? null : Detail(id);
            icon.sprite = detail?.Icon;
            icon.enabled = icon.sprite != null;
            relicName.fontSizeMin = 21;
            if (icon.enabled)
            {
                Place(count.rectTransform, 138, 12, 560, 27);
                Place(relicName.rectTransform, 138, 42, 560, 47);
                Place(description.rectTransform, 138, 91, 560, 86);
            }
            else
            {
                Place(count.rectTransform, 22, 10, 680, 27);
                Place(relicName.rectTransform, 22, 39, 680, 80);
                Place(description.rectTransform, 22, 119, 680, 61);
            }
            if (held == 0) Place(description.rectTransform, 22, 22, 680, 145);
            relicName.text = detail?.Name ?? "";
            description.richText = detail != null;
            description.text = detail?.Description?.Replace("<color=blue>", "<color=#9DC4FF>").Replace("<color=purple>", "<color=#D7A4FF>") ?? (run != null ? "No relics held." : readFailed ? "See the BepInEx log for details."
                : "Finish a run to see its relics here.");
            stripStart = (relicIndex / StripSize) * StripSize;
            visibleSlots = Math.Min(held - stripStart, StripSize);
            for (int i = 0; i < StripSize; i++)
            {
                slots[i].gameObject.SetActive(i < visibleSlots);
                if (i >= visibleSlots) continue;
                Place((RectTransform)slots[i].transform, 390 - (visibleSlots * 56 - 8) / 2f + i * 56, 396, 48, 48);
                var thumbnail = Detail(run.Relics[stripStart + i]).Icon;
                slotIcons[i].sprite = thumbnail;
                slotIcons[i].enabled = thumbnail != null;
                slots[i].GetComponentInChildren<TextMeshProUGUI>(true).enabled = thumbnail == null;
                markers[i].SetActive(stripStart + i == relicIndex);
            }
            WireNavigation();
        }

        private void ShowRun(CompletedRun run)
        {
            completed.text = run == null ? "" : run.CompletedUtc.ToLocalTime().ToString("d");
            day.text = run == null ? "" : $"Day {run.FinalDay}";
            crest.sprite = null;
            if (run == null) summary.text = readFailed ? "History unavailable" : "No completed runs yet";
            else
            {
                var race = (Races)run.Kingdom;
                summary.text = race + " / " + run.Hero;
                try
                {
                    var kingdom = PrefabsManager.Instance?.GetKingdomData(race);
                    var hero = AddressablesManager.Instance?.GetHero(run.Hero);
                    crest.sprite = kingdom != null ? kingdom.GetIcon() : null;
                    summary.text = (kingdom != null ? kingdom.GetTitle() : race.ToString()) + " / " +
                        (hero != null ? hero.GetName() : run.Hero);
                }
                catch (Exception e) { Plugin.Warn("Could not display run details: " + e.Message); }
            }
            crest.enabled = crest.sprite != null;
            Place(summary.rectTransform, crest.enabled ? 100 : 44, 136, crest.enabled ? 444 : 500, 54);
        }

        private void WireNavigation()
        {
            Button leftTop = Available(newer, older), rightTop = Available(older, newer);
            Button leftMid = Available(previous, next), rightMid = Available(next, previous);
            Link(newer, older, back, leftMid ?? back);
            Link(older, newer, back, rightMid ?? back);
            Link(previous, next, leftTop ?? back, back);
            Link(next, previous, rightTop ?? back, back);
            Link(back, back, leftMid ?? leftTop ?? back, leftTop ?? leftMid ?? back);
            if (visibleSlots == 0) return;
            var current = slots[relicIndex - stripStart];
            Link(newer, older, back, current);
            Link(older, newer, back, current);
            Link(back, back, current, leftTop ?? current);
            for (int i = 0; i < visibleSlots; i++)
            {
                var button = slots[i];
                var left = i > 0 ? slots[i - 1] : previous.interactable ? previous : button;
                var right = i + 1 < visibleSlots ? slots[i + 1] : next.interactable ? next : button;
                button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left,
                    selectOnRight = right, selectOnUp = leftTop ?? back, selectOnDown = back };
            }
            Link(previous, slots[0], leftTop ?? back, back);
            Link(next, slots[visibleSlots - 1], rightTop ?? back, back);
        }

        private static Button Available(Button first, Button second) => first.interactable ? first : second.interactable ? second : null;

        private static void Link(Button button, Button across, Button up, Button down)
        {
            var horizontal = across.interactable ? across : button;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = horizontal, selectOnRight = horizontal, selectOnUp = up, selectOnDown = down };
        }

        private Button PageButton(string caption, float x, float y, Action click)
        {
            var button = MakeButton(sheet, caption, click);
            Place((RectTransform)button.transform, x, y, 180, 44);
            return button;
        }

        private Button MakeButton(Transform parent, string caption, Action click)
        {
            var rect = Box(parent, caption, Color.white);
            var image = rect.GetComponent<Image>();
            image.sprite = buttonStyle.sprite;
            image.type = buttonStyle.type;
            image.pixelsPerUnitMultiplier = buttonStyle.pixelsPerUnitMultiplier;
            var button = rect.gameObject.AddComponent<JournalButton>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color32(115, 84, 60, 255);
            colors.selectedColor = colors.highlightedColor = Gold;
            colors.disabledColor = new Color32(85, 74, 68, 190);
            button.colors = colors;
            button.onClick.AddListener(() => click());
            var label = Label(rect, caption, 28, Color.white);
            button.SetCaption(label);
            Stretch(label.rectTransform, 3);
            return button;
        }

        private TextMeshProUGUI Label(Transform parent, string caption, float size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.spriteAsset = style.spriteAsset;
            text.alignment = TextAlignmentOptions.Center;
            // Inactive TMP components otherwise replace these size limits on first activation.
            text.fontSize = size;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 13;
            text.fontSizeMax = size;
            text.color = color;
            text.raycastTarget = false;
            text.richText = false;
            text.text = caption;
            return text;
        }

        internal static RectTransform Box(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float margin = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * margin;
            rect.offsetMax = -Vector2.one * margin;
        }

        internal static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (IsOpen && title != null) Close();
            if (launcher != null) Destroy(launcher.gameObject);
            if (overlay != null) Destroy(overlay.gameObject);
        }
    }
}
