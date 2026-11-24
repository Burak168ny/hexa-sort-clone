using HexaSort.Core;
using HexaSort.Model;
using HexaSort.UI;
using HexaSort.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HexaSort.Editor
{
    /// <summary>Builds the uGUI hierarchy (portrait, 1080x1920 reference) and wires it to the UIManager.</summary>
    internal static class UIBuilder
    {
        private static readonly Color Ink = new Color32(0x22, 0x27, 0x44, 0xFF);
        private static readonly Color Dim = new Color(0.07f, 0.08f, 0.16f, 0.72f);
        private static readonly Color Green = new Color32(0x4C, 0xC9, 0x64, 0xFF);
        private static readonly Color Orange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        private static readonly Color Blue = new Color32(0x3D, 0x8B, 0xFF, 0xFF);
        private static readonly Color Chip = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color BarBack = new Color(0.08f, 0.1f, 0.2f, 0.55f);

        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _gear;
        private static Sprite _hexagon;

        /// <summary>Sprites must be baked (and imported) before the scene is created.</summary>
        public static void BakeSprites()
        {
            foreach (SpriteBaker.Shape shape in System.Enum.GetValues(typeof(SpriteBaker.Shape))) SpriteBaker.GetOrBake(shape);
        }

        /// <summary>Scene objects the UI needs references to.</summary>
        public struct Context
        {
            public GameManager Game;
            public BoosterController Boosters;
            public MergeSequencePlayer Player;
            public BoardView Board;
            public Camera Camera;
            public VisualConfig Visuals;
        }

        public static void Build(Context context)
        {
            var game = context.Game;
            _rounded = SpriteBaker.GetOrBake(SpriteBaker.Shape.RoundedRect);
            _circle = SpriteBaker.GetOrBake(SpriteBaker.Shape.Circle);
            _gear = SpriteBaker.GetOrBake(SpriteBaker.Shape.Gear);
            _hexagon = SpriteBaker.GetOrBake(SpriteBaker.Shape.Hexagon);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetAsLastSibling();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = canvasGo.AddComponent<UIManager>();
            GameSceneBuilder.SetRef(ui, "_game", game);

            var hud = BuildHud(canvasGo.transform, ui);
            BuildBoosterBar(hud, context);
            BuildJuice(canvasGo.transform, hud.GetComponent<HudView>(), context);
            BuildHome(canvasGo.transform, ui);
            BuildWin(canvasGo.transform, ui);
            BuildFail(canvasGo.transform, ui);
            BuildSettings(canvasGo.transform, ui);
        }

        private static UIPanel BuildHud(Transform canvas, UIManager ui)
        {
            // No background: the board must stay visible and touchable.
            var hud = NewPanel<UIPanel>("HUD", canvas, null);
            var safe = Stretch(NewRect("SafeArea", hud.transform));
            safe.gameObject.AddComponent<SafeArea>();
            var view = hud.gameObject.AddComponent<HudView>();

            var level = NewText("LevelText", safe, "Level 1", 56, FontStyles.Bold, Color.white);
            Place(level.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 80f));

            var bar = Place(NewRect("GoalBar", safe), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(620f, 64f));
            AddImage(bar, _rounded, BarBack);
            var fillArea = Stretch(NewRect("FillArea", bar));
            fillArea.offsetMin = new Vector2(6f, 6f);
            fillArea.offsetMax = new Vector2(-6f, -6f);
            var fill = Stretch(NewRect("Fill", fillArea));
            fill.anchorMax = new Vector2(0f, 1f);
            AddImage(fill, _rounded, Green);
            var goal = NewText("GoalText", bar, "0 / 0", 38, FontStyles.Bold, Color.white);
            Stretch(goal.rectTransform);

            var badge = Place(NewRect("GoalBadge", bar), new Vector2(0f, 0.5f), new Vector2(-46f, 0f), new Vector2(92f, 92f));
            badge.pivot = new Vector2(0.5f, 0.5f);
            AddImage(badge, _hexagon, Orange);

            var tutorial = NewText("TutorialText", safe, "", 44, FontStyles.Bold, Color.white);
            Place(tutorial.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(940f, 120f));
            tutorial.textWrappingMode = TextWrappingModes.Normal;
            tutorial.gameObject.SetActive(false);

            var settings = NewIconButton("SettingsButton", safe, _gear, 120f);
            Place(settings, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(120f, 120f));

            GameSceneBuilder.SetRef(view, "_levelText", level);
            GameSceneBuilder.SetRef(view, "_goalText", goal);
            GameSceneBuilder.SetRef(view, "_fill", fill);
            GameSceneBuilder.SetRef(view, "_goalBadge", badge);
            GameSceneBuilder.SetRef(view, "_tutorialText", tutorial);
            GameSceneBuilder.SetRef(ui, "_hudPanel", hud);
            GameSceneBuilder.SetRef(ui, "_hud", view);
            GameSceneBuilder.SetRef(ui, "_settingsButton", settings.GetComponent<Button>());
            return hud;
        }

        private static readonly (BoosterType type, string label, SpriteBaker.Shape icon)[] BoosterButtons =
        {
            (BoosterType.Hammer, "Hammer", SpriteBaker.Shape.Hammer),
            (BoosterType.Move, "Move", SpriteBaker.Shape.Move),
            (BoosterType.Shuffle, "Shuffle", SpriteBaker.Shape.Shuffle),
        };

        private static void BuildBoosterBar(UIPanel hud, Context context)
        {
            var safe = hud.transform.Find("SafeArea");
            var bar = Place(NewRect("BoosterBar", safe), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(720f, 220f));
            var component = bar.gameObject.AddComponent<BoosterBar>();
            GameSceneBuilder.SetRef(component, "_game", context.Game);
            GameSceneBuilder.SetRef(component, "_boosters", context.Boosters);
            GameSceneBuilder.SetRef(component, "_hud", hud.GetComponent<HudView>());

            var so = new SerializedObject(component);
            var slots = so.FindProperty("_slots");
            slots.arraySize = BoosterButtons.Length;
            for (int i = 0; i < BoosterButtons.Length; i++)
            {
                var (type, label, icon) = BoosterButtons[i];
                float x = (i - (BoosterButtons.Length - 1) * 0.5f) * 230f;

                var button = NewIconButton(label + "Button", bar, SpriteBaker.GetOrBake(icon), 150f);
                Place(button, new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(150f, 150f));

                // Pill-shaped badge, wide enough for three digits.
                var badge = Place(NewRect("CountBadge", button), new Vector2(1f, 1f), new Vector2(26f, 14f), new Vector2(100f, 56f));
                AddImage(badge, _rounded, Orange).pixelsPerUnitMultiplier = 1.5f;
                var count = NewText("Count", badge, "300", 32, FontStyles.Bold, Color.white);
                Stretch(count.rectTransform);

                var caption = NewText("Label", bar, label, 34, FontStyles.Bold, Color.white);
                Place(caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(220f, 56f));

                var slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("Type").enumValueIndex = (int)type;
                slot.FindPropertyRelative("Button").objectReferenceValue = button.GetComponent<Button>();
                slot.FindPropertyRelative("Count").objectReferenceValue = count;
                slot.FindPropertyRelative("Background").objectReferenceValue = button.GetComponent<Image>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Overlay layers for flying goal icons and combo text (never block input).</summary>
        private static void BuildJuice(Transform canvas, HudView hud, Context context)
        {
            var icons = Stretch(NewRect("GoalIcons", canvas));
            var flight = icons.gameObject.AddComponent<GoalIconFlight>();
            GameSceneBuilder.SetRef(flight, "_player", context.Player);
            GameSceneBuilder.SetRef(flight, "_camera", context.Camera);
            GameSceneBuilder.SetRef(flight, "_hud", hud);
            GameSceneBuilder.SetRef(flight, "_visuals", context.Visuals);
            GameSceneBuilder.SetRef(flight, "_iconSprite", _hexagon);

            var combos = Stretch(NewRect("ComboPopups", canvas));
            var popup = combos.gameObject.AddComponent<ComboPopup>();
            GameSceneBuilder.SetRef(popup, "_player", context.Player);
            GameSceneBuilder.SetRef(popup, "_board", context.Board);
            GameSceneBuilder.SetRef(popup, "_camera", context.Camera);

            var so = new SerializedObject(popup);
            var texts = so.FindProperty("_texts");
            texts.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var text = NewText("Combo" + i, combos, "COMBO x2", 96, FontStyles.Bold, Color.white);
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(800f, 140f);
                texts.GetArrayElementAtIndex(i).objectReferenceValue = text;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHome(Transform canvas, UIManager ui)
        {
            var home = NewPanel<UIPanel>("HomePanel", canvas, new Color(Dim.r, Dim.g, Dim.b, 0.55f));
            var content = Stretch(NewRect("Content", home.transform));
            content.gameObject.AddComponent<SafeArea>();

            var logo = Place(NewRect("Logo", content), new Vector2(0.5f, 0.5f), new Vector2(0f, 560f), new Vector2(220f, 220f));
            AddImage(logo, _hexagon, Orange);
            var logoMotion = new UnityEditor.SerializedObject(logo.gameObject.AddComponent<SpinIdle>());
            logoMotion.FindProperty("_degreesPerSecond").floatValue = 0f;
            logoMotion.FindProperty("_floatAmplitude").floatValue = 14f;
            logoMotion.FindProperty("_pulse").floatValue = 0.04f;
            logoMotion.ApplyModifiedPropertiesWithoutUndo();
            var title = NewText("Title", content, "HEXA SORT", 150, FontStyles.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 340f), new Vector2(1000f, 200f));

            var play = NewButton("PlayButton", content, "PLAY", Green, 96, out _);
            Place(play, new Vector2(0.5f, 0.5f), new Vector2(0f, -520f), new Vector2(620f, 200f));
            var level = NewText("LevelText", content, "Level 1", 60, FontStyles.Bold, Color.white);
            Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -690f), new Vector2(600f, 90f));

            GameSceneBuilder.SetRef(ui, "_homePanel", home);
            GameSceneBuilder.SetRef(ui, "_homeLevelText", level);
            GameSceneBuilder.SetRef(ui, "_playButton", play.GetComponent<Button>());
        }

        private static void BuildWin(Transform canvas, UIManager ui)
        {
            var win = NewPanel<UIPanel>("WinPanel", canvas, Dim);
            var card = NewCard(win, new Vector2(860f, 700f));
            // Light rays spinning behind the badge (a sibling placed before the card, so it draws behind it).
            var rays = NewRect("Rays", card.parent);
            rays.SetSiblingIndex(card.GetSiblingIndex());
            rays.anchorMin = rays.anchorMax = card.anchorMin;
            rays.pivot = new Vector2(0.5f, 0.5f);
            rays.anchoredPosition = card.anchoredPosition + new Vector2(0f, card.sizeDelta.y * (1f - card.pivot.y));
            rays.sizeDelta = new Vector2(900f, 900f);
            AddImage(rays, SpriteBaker.GetOrBake(SpriteBaker.Shape.Rays), new Color(1f, 0.93f, 0.6f, 0.55f));
            rays.gameObject.AddComponent<SpinIdle>();
            var badge = Place(NewRect("Badge", card), new Vector2(0.5f, 1f), new Vector2(0f, 110f), new Vector2(220f, 220f));
            AddImage(badge, _hexagon, Green);
            var title = NewText("Title", card, "Level 1\nComplete!", 92, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(780f, 260f));
            var next = NewButton("NextButton", card, "NEXT", Green, 76, out _);
            Place(next, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(540f, 170f));

            GameSceneBuilder.SetRef(ui, "_winPanel", win);
            GameSceneBuilder.SetRef(ui, "_winTitle", title);
            GameSceneBuilder.SetRef(ui, "_nextButton", next.GetComponent<Button>());
        }

        private static void BuildFail(Transform canvas, UIManager ui)
        {
            var fail = NewPanel<UIPanel>("FailPanel", canvas, Dim);
            var card = NewCard(fail, new Vector2(860f, 860f));
            var title = NewText("Title", card, "Out of Space!", 92, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(780f, 130f));
            var subtitle = NewText("Subtitle", card, "No empty cell left for your stacks.", 46, FontStyles.Normal,
                new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(780f, 120f));

            var revive = NewButton("ReviveButton", card, "REVIVE\n<size=60%>clear 3 stacks</size>", Blue, 70, out _);
            Place(revive, new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(560f, 190f));
            var retry = NewButton("RetryButton", card, "RETRY", Orange, 70, out _);
            Place(retry, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(560f, 160f));

            GameSceneBuilder.SetRef(ui, "_failPanel", fail);
            GameSceneBuilder.SetRef(ui, "_reviveButton", revive.GetComponent<Button>());
            GameSceneBuilder.SetRef(ui, "_retryButton", retry.GetComponent<Button>());
        }

        private static void BuildSettings(Transform canvas, UIManager ui)
        {
            var settings = NewPanel<SettingsPanel>("SettingsPanel", canvas, Dim);
            var card = NewCard(settings, new Vector2(820f, 1080f));
            var title = NewText("Title", card, "Settings", 84, FontStyles.Bold, Ink);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 120f));

            var sound = NewButton("SoundButton", card, "Sound: ON", Green, 54, out var soundLabel);
            Place(sound, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(600f, 140f));
            var haptics = NewButton("VibrationButton", card, "Vibration: ON", Green, 54, out var hapticsLabel);
            Place(haptics, new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(600f, 140f));
            var restart = NewButton("RestartButton", card, "Restart Level", Orange, 54, out _);
            Place(restart, new Vector2(0.5f, 1f), new Vector2(0f, -560f), new Vector2(600f, 140f));
            var home = NewButton("HomeButton", card, "Home", Blue, 54, out _);
            Place(home, new Vector2(0.5f, 1f), new Vector2(0f, -730f), new Vector2(600f, 140f));
            var close = NewButton("CloseButton", card, "Close", new Color(0.88f, 0.89f, 0.93f), 54, out var closeLabel);
            closeLabel.color = Ink;
            Place(close, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(400f, 130f));

            GameSceneBuilder.SetRef(settings, "_soundButton", sound.GetComponent<Button>());
            GameSceneBuilder.SetRef(settings, "_soundLabel", soundLabel);
            GameSceneBuilder.SetRef(settings, "_hapticsButton", haptics.GetComponent<Button>());
            GameSceneBuilder.SetRef(settings, "_hapticsLabel", hapticsLabel);
            GameSceneBuilder.SetRef(settings, "_restartButton", restart.GetComponent<Button>());
            GameSceneBuilder.SetRef(settings, "_homeButton", home.GetComponent<Button>());
            GameSceneBuilder.SetRef(settings, "_closeButton", close.GetComponent<Button>());
            GameSceneBuilder.SetRef(ui, "_settingsPanel", settings);
        }

        // ---- Helpers ----

        private static T NewPanel<T>(string name, Transform parent, Color? background) where T : UIPanel
        {
            var rt = Stretch(NewRect(name, parent));
            rt.gameObject.AddComponent<CanvasGroup>();
            if (background.HasValue) AddImage(rt, null, background.Value); // also blocks input to the board
            return rt.gameObject.AddComponent<T>();
        }

        private static RectTransform NewCard(UIPanel panel, Vector2 size)
        {
            var card = Place(NewRect("Card", panel.transform), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            AddImage(card, _rounded, Color.white);
            GameSceneBuilder.SetRef(panel, "_content", card);
            return card;
        }

        private static RectTransform NewButton(string name, Transform parent, string label, Color color, float fontSize,
            out TextMeshProUGUI text)
        {
            var rt = NewRect(name, parent);
            var image = AddImage(rt, _rounded, color);
            image.raycastTarget = true;
            AddButton(rt, image);
            text = NewText("Label", rt, label, fontSize, FontStyles.Bold, Color.white);
            Stretch(text.rectTransform);
            return rt;
        }

        private static RectTransform NewIconButton(string name, Transform parent, Sprite icon, float size)
        {
            var rt = NewRect(name, parent);
            var image = AddImage(rt, _circle, Chip);
            image.raycastTarget = true;
            AddButton(rt, image);
            var glyph = Place(NewRect("Icon", rt), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            AddImage(glyph, icon, Color.white);
            return rt;
        }

        private static void AddButton(RectTransform rt, Image target)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            rt.gameObject.AddComponent<ButtonPressScale>();
        }

        private static Image AddImage(RectTransform rt, Sprite sprite, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = sprite == null; // full-screen backgrounds block input, decorations do not
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors and pivots at the same normalized point, then offsets and sizes.</summary>
        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Imports TMP Essential Resources if missing. Returns false while the import is pending.</summary>
        public static bool EnsureTextMeshProResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            EditorUtility.DisplayDialog("HexaSort setup",
                "TextMeshPro Essential Resources are being imported.\n\n" +
                "When the import finishes, run Tools > HexaSort > Setup Game Scene again.", "OK");
            return false;
        }
    }
}
