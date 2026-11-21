using UnityEngine;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>Helpers y paleta para construir uGUI por código.</summary>
    public static class UIFactory
    {
        // Paleta (estética "laboratorio nocturno", nada de casino)
        public static readonly Color Panel = new Color(0.085f, 0.095f, 0.18f, 0.98f);
        public static readonly Color PanelBorder = new Color(0.22f, 0.25f, 0.45f);
        public static readonly Color CardColor = new Color(0.13f, 0.145f, 0.26f);
        public static readonly Color CardDarkColor = new Color(0.10f, 0.11f, 0.21f);
        public static readonly Color ShadowColor = new Color(0f, 0f, 0.02f, 0.45f);
        public static readonly Color Accent = new Color(0.30f, 0.80f, 0.95f);
        public static readonly Color MoneyGreen = new Color(0.49f, 0.89f, 0.55f);
        public static readonly Color Gold = new Color(1f, 0.80f, 0.32f);
        public static readonly Color Disabled = new Color(0.20f, 0.22f, 0.36f);
        public static readonly Color TextMain = new Color(0.96f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.58f, 0.63f, 0.80f);

        public static Color CategoryColor(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Bolas => new Color(0.31f, 0.76f, 0.97f),
            UpgradeCategory.Pinchos => new Color(0.70f, 0.53f, 1f),
            UpgradeCategory.Rebotes => new Color(0.30f, 0.82f, 0.63f),
            UpgradeCategory.Multiplicadores => new Color(1f, 0.68f, 0.30f),
            UpgradeCategory.Sobrecarga => new Color(1f, 0.42f, 0.75f),
            _ => Accent
        };

        static Font font;
        static bool CustomFont;
        /// <summary>Pon un .ttf en Assets/Resources/Fonts/UI.ttf para cambiar la fuente de todo el juego.</summary>
        public static Font Font
        {
            get
            {
                if (font == null) { font = Resources.Load<Font>("Fonts/UI"); CustomFont = font != null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one);

        public static RectTransform Inset(RectTransform rt, float l, float b, float r, float t)
        {
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, bool rounded = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = SpriteFactory.UIPanel;
                img.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return img;
        }

        /// <summary>Panel redondeado con sombra desplazada debajo. Devuelve el panel (la sombra va por detrás).</summary>
        public static RectTransform Card(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, float shadow = 8f)
        {
            var holder = Rect(name, parent, anchorMin, anchorMax);
            var sh = Stretch("Shadow", holder);
            Inset(sh, 0, -shadow, 0, shadow);
            Image(sh, ShadowColor).raycastTarget = false;
            var body = Stretch("Body", holder);
            Image(body, color);
            return body;
        }

        public static Text Text(RectTransform rt, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            // la fuente del juego (Lilita One) ya es gruesa: la negrita sintética solo se usa con la fuente por defecto
            t.fontStyle = bold && !CustomFont ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static void AddShadow(Text t, float dist = 3f, float alpha = 0.5f)
        {
            var s = t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(0, -dist);
        }

        /// <summary>Moneda dibujada con dos círculos.</summary>
        public static RectTransform Coin(Transform parent, float size)
        {
            var rt = Rect("Coin", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(size, size);
            var outer = rt.gameObject.AddComponent<Image>();
            outer.sprite = SpriteFactory.Circle;
            outer.color = Gold;
            outer.raycastTarget = false;
            var inner = Rect("Inner", rt, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f));
            var img = inner.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Circle;
            img.color = new Color(0.95f, 0.62f, 0.15f);
            img.raycastTarget = false;
            return rt;
        }

        public static Button Button(RectTransform rt, Color color, out Text label, string text, int size)
        {
            var img = Image(rt, Color.white);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            SetButtonColor(btn, color);

            // brillo en la mitad superior para dar volumen
            var gloss = Rect("Gloss", rt, new Vector2(0, 0.5f), Vector2.one);
            Inset(gloss, 4, 0, 4, 4);
            Image(gloss, new Color(1, 1, 1, 0.07f)).raycastTarget = false;

            label = Text(Stretch("Label", rt), text, size, TextMain, TextAnchor.MiddleCenter, true);
            AddShadow(label, 2f, 0.35f);
            return btn;
        }

        public static void SetButtonColor(Button btn, Color color)
        {
            var cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            cb.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            cb.selectedColor = color;
            cb.disabledColor = Disabled;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
        }
    }
}
