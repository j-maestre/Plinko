using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// Panel de prestigio a pantalla completa: lingotes, botón de reinicio y tienda de mejoras permanentes.
    /// Se abre desde el botón PRESTIGIO de la barra superior.
    /// </summary>
    public class PrestigeUI : MonoBehaviour
    {
        public static readonly Color CoreColor = new Color(1f, 0.78f, 0.25f); // oro

        class Row
        {
            public PrestigeDef Def;
            public Text Level, Effect, Cost;
            public Image CostIcon;
            public Button Button;
            public Punch Punch;
        }

        GameManager gm;
        RectTransform panel;
        Text coresText, bonusText, gainText, nextText, prestigeLabel, resetLabel;
        RectTransform gainFill;
        Button prestigeButton;
        Punch panelPunch, coresPunch;
        readonly List<Row> rows = new List<Row>();
        float confirmTimer, resetConfirm, refreshTimer;

        public bool IsOpen => panel.gameObject.activeSelf;

        public void Init(GameManager gm, RectTransform canvasRoot)
        {
            this.gm = gm;

            // Fondo oscuro que tapa el juego y cierra al tocar fuera
            panel = UIFactory.Stretch("PrestigePanel", canvasRoot);
            var backdrop = UIFactory.Image(panel, new Color(0.01f, 0.01f, 0.05f, 0.88f), false);
            var closeArea = panel.gameObject.AddComponent<Button>();
            closeArea.transition = Selectable.Transition.None;
            closeArea.targetGraphic = backdrop;
            closeArea.onClick.AddListener(Close);

            var card = UIFactory.Card("Card", panel, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), UIFactory.Panel, 10);
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // tocar la tarjeta no cierra
            panelPunch = Punch.On(card.parent, 0.05f, 0.3f);

            // Cabecera: lingote grande + título
            var header = UIFactory.Rect("Header", card, new Vector2(0.04f, 0.905f), new Vector2(0.82f, 0.985f));
            Ingot(header, new Vector2(62, 0), 1.6f);
            var title = UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Title", header), 132, 0, 0, 0),
                "PRESTIGIO", 52, CoreColor, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(title, 3f);
            var closeRt = UIFactory.Rect("Close", card, new Vector2(0.84f, 0.92f), new Vector2(0.96f, 0.975f));
            UIFactory.Button(closeRt, UIFactory.CardDarkColor, out _, "X", 36).onClick.AddListener(Close);

            // Cartera: lingotes que tienes y el bonus que dan sin gastar
            var coresRt = UIFactory.Rect("Cores", card, new Vector2(0.05f, 0.805f), new Vector2(0.95f, 0.895f));
            UIFactory.Image(coresRt, UIFactory.CardDarkColor);
            Ingot(coresRt, new Vector2(66, 0), 1.25f);
            coresText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Count", coresRt, Vector2.zero, new Vector2(0.62f, 1)), 128, 0, 0, 0),
                "", 54, CoreColor, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(coresText, 2f, 0.5f);
            var bonusRt = UIFactory.Rect("Bonus", coresRt, new Vector2(0.62f, 0.22f), new Vector2(0.97f, 0.78f));
            UIFactory.Image(bonusRt, new Color(UIFactory.MoneyGreen.r, UIFactory.MoneyGreen.g, UIFactory.MoneyGreen.b, 0.16f)).raycastTarget = false;
            bonusText = UIFactory.Text(UIFactory.Stretch("Text", bonusRt), "", 26, UIFactory.MoneyGreen, TextAnchor.MiddleCenter, true);
            coresPunch = Punch.On(coresRt, 0.08f, 0.3f);

            // Recompensa del reinicio
            var gainRt = UIFactory.Rect("Gain", card, new Vector2(0.05f, 0.59f), new Vector2(0.95f, 0.79f));
            UIFactory.Image(gainRt, new Color(CoreColor.r, CoreColor.g, CoreColor.b, 0.55f)).raycastTarget = false;   // borde dorado
            var gainBody = UIFactory.Inset(UIFactory.Stretch("Body", gainRt), 3, 3, 3, 3);
            UIFactory.Image(gainBody, new Color(0.2f, 0.14f, 0.05f)).raycastTarget = false;
            UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Label", gainBody, new Vector2(0, 0.76f), new Vector2(0.6f, 0.96f)), 24, 0, 0, 0),
                "AL REINICIAR GANAS", 22, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft, true);
            var gainLine = UIFactory.Rect("GainLine", gainBody, new Vector2(0, 0.42f), new Vector2(0.6f, 0.78f));
            Ingot(gainLine, new Vector2(56, 0), 1.0f);
            gainText = UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Text", gainLine), 104, 0, 0, 0), "", 54, CoreColor, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(gainText, 2f, 0.5f);
            nextText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Next", gainBody, new Vector2(0, 0.24f), new Vector2(0.6f, 0.42f)), 24, 0, 0, 0),
                "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
            var barBg = UIFactory.Inset(UIFactory.Rect("Bar", gainBody, new Vector2(0, 0.08f), new Vector2(0.6f, 0.22f)), 24, 0, 0, 0);
            UIFactory.Image(barBg, new Color(0, 0, 0, 0.4f)).raycastTarget = false;
            gainFill = UIFactory.Rect("Fill", barBg, Vector2.zero, new Vector2(0, 1));
            UIFactory.Image(gainFill, CoreColor).raycastTarget = false;

            var btnRt = UIFactory.Rect("PrestigeButton", gainBody, new Vector2(0.63f, 0.14f), new Vector2(0.97f, 0.86f));
            prestigeButton = UIFactory.Button(btnRt, CoreColor, out prestigeLabel, "REINICIAR", 28);
            prestigeButton.onClick.AddListener(OnPrestigeClicked);

            UIFactory.Text(UIFactory.Rect("Info", card, new Vector2(0.06f, 0.525f), new Vector2(0.94f, 0.585f)),
                "Reinicia la partida a cambio de lingotes. Cada prestigio suma x0.5 a tus ingresos para siempre. Los lingotes gástalos en lo que quieras: secciones nuevas y mejoras permanentes.",
                21, UIFactory.TextDim, TextAnchor.MiddleLeft).horizontalOverflow = HorizontalWrapMode.Wrap;

            // Tienda
            var shopTitle = UIFactory.Rect("ShopTitle", card, new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.52f));
            UIFactory.Text(shopTitle, "TIENDA PERMANENTE", 28, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleLeft, true);
            BuildShop(UIFactory.Rect("Shop", card, new Vector2(0.03f, 0.07f), new Vector2(0.97f, 0.475f)));

            // Borrar la partida entera (también lingotes y tienda). Doble toque para confirmar.
            var resetRt = UIFactory.Rect("Reset", card, new Vector2(0.06f, 0.012f), new Vector2(0.4f, 0.058f));
            UIFactory.Button(resetRt, new Color(0.55f, 0.2f, 0.3f), out resetLabel, "BORRAR PARTIDA", 22).onClick.AddListener(() =>
            {
                if (resetConfirm > 0) { resetConfirm = 0; gm.ResetProgress(); Close(); }
                else { resetConfirm = 2.5f; resetLabel.text = "¿SEGURO? Toca otra vez"; }
            });

            gm.UpgradesChanged += Refresh;
            panel.gameObject.SetActive(false);
        }

        /// <summary>Icono de lingote de oro (anclado a la izquierda y centrado en vertical del padre).</summary>
        public static Image Ingot(Transform parent, Vector2 pos, float scale)
        {
            var rt = UIFactory.Rect("Ingot", parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            rt.sizeDelta = new Vector2(40f * IngotIcon.Aspect, 40f) * scale;
            rt.anchoredPosition = pos;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = IngotIcon.Sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Barra de progreso redondeada: nunca más estrecha que su alto.</summary>
        static void SetFill(RectTransform fill, float progress)
        {
            var parent = (RectTransform)fill.parent;
            float w = parent.rect.width, h = parent.rect.height;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(progress, 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = new Vector2(Mathf.Max(0f, h - progress * w), 0);
            fill.gameObject.SetActive(progress > 0.001f);
        }

        void BuildShop(RectTransform area)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;
            var viewport = UIFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            UIFactory.Image(viewport, new Color(0, 0, 0, 0), false);
            var content = UIFactory.Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 8, 24);
            vlg.spacing = 14;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            foreach (var def in PrestigeCatalog.All)
            {
                var body = UIFactory.Card(def.Id, content, Vector2.zero, Vector2.one, UIFactory.CardColor, 6);
                var holder = (RectTransform)body.parent;
                holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
                var row = new Row { Def = def, Punch = Punch.On(holder, 0.04f, 0.25f) };

                var icon = UIFactory.Rect("Icon", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                icon.sizeDelta = new Vector2(90, 90);
                icon.anchoredPosition = new Vector2(70, 0);
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = SpriteFactory.Circle;
                iconImg.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, 0.18f);
                UIFactory.Text(UIFactory.Rect("Nv", icon, new Vector2(0, 0.62f), new Vector2(1, 0.92f)), "NV", 18, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleCenter, true);
                iconImg.raycastTarget = false;
                row.Level = UIFactory.Text(UIFactory.Rect("Level", icon, new Vector2(0, 0.08f), new Vector2(1, 0.68f)), "0", 36, CoreColor, TextAnchor.MiddleCenter, true);

                const float left = 130;
                UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Name", body, new Vector2(0, 0.62f), new Vector2(0.68f, 0.94f)), left, 0, 0, 0),
                    def.Name, 32, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
                UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Desc", body, new Vector2(0, 0.36f), new Vector2(0.68f, 0.64f)), left, 0, 0, 0),
                    def.Desc, 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
                row.Effect = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Effect", body, new Vector2(0, 0.06f), new Vector2(0.68f, 0.38f)), left, 0, 0, 0),
                    "", 24, UIFactory.TextMain, TextAnchor.MiddleLeft);

                var btnRt = UIFactory.Rect("Buy", body, new Vector2(0.70f, 0.18f), new Vector2(0.965f, 0.82f));
                row.Button = UIFactory.Button(btnRt, CoreColor, out row.Cost, "", 34);
                // precio: lingote + número
                row.CostIcon = Ingot(btnRt, new Vector2(48, 0), 0.75f);
                UIFactory.Inset(row.Cost.rectTransform, 72, 0, 10, 0);
                row.Button.onClick.AddListener(() =>
                {
                    if (gm.TryBuyPrestige(def)) { row.Punch.Play(); coresPunch.Play(); }
                });
                rows.Add(row);
            }
        }

        public void Open()
        {
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            panelPunch.Play();
            gm.Audio.Click();
            Refresh();
        }

        public void Close()
        {
            panel.gameObject.SetActive(false);
            confirmTimer = 0;
        }

        void OnPrestigeClicked()
        {
            if (!gm.CanPrestige) return;
            if (confirmTimer > 0)
            {
                confirmTimer = 0;
                double gained = gm.CoresOnPrestige;
                gm.DoPrestige();
                gm.Background.Pulse(CoreColor, 1f);
                gm.Board.Shake(0.6f);
                gm.Audio.Record();
                coresPunch.Play(1.5f);
                gm.UI.ShowBanner("¡Prestigio!  +" + NumberFormat.Format(gained) + " lingotes", CoreColor);
                Refresh();
            }
            else
            {
                confirmTimer = 2.5f;
                prestigeLabel.text = "¿SEGURO?";
            }
        }

        void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            if (confirmTimer > 0) confirmTimer -= dt;
            if (resetConfirm > 0) { resetConfirm -= dt; if (resetConfirm <= 0 && resetLabel != null) resetLabel.text = "BORRAR PARTIDA"; }
            refreshTimer -= dt;
            if (refreshTimer <= 0) { refreshTimer = 0.2f; Refresh(); }
        }

        void Refresh()
        {
            if (!IsOpen) return;
            var s = gm.State;
            coresText.text = NumberFormat.Format(s.cores) + " <size=26><color=#E8D2A0>" + (s.cores == 1 ? "lingote" : "lingotes") + "</color></size>";
            bonusText.text = NumberFormat.Multiplier(PrestigeBalance.PrestigeIncome(s.prestigeCount)) + " dinero";

            double gain = gm.CoresOnPrestige;
            double prestigeCost = gm.PrestigeCost;
            gainText.text = "+" + NumberFormat.Format(gain) + "  <size=30><color=#7CE38C>dinero " +
                            NumberFormat.Multiplier(PrestigeBalance.PrestigeIncome(s.prestigeCount + 1)) + "</color></size>";
            nextText.text = gm.CanPrestige
                ? "¡Ya puedes reiniciar!"
                : "Tienes " + NumberFormat.Money(s.money) + " de " + NumberFormat.Money(prestigeCost);
            SetFill(gainFill, Mathf.Clamp01((float)(s.money / prestigeCost)));
            prestigeButton.interactable = gm.CanPrestige;
            if (confirmTimer <= 0)
                prestigeLabel.text = "REINICIAR\n<size=24>" + NumberFormat.Money(gm.PrestigeCost) + "</size>";

            string hex = ColorUtility.ToHtmlStringRGB(CoreColor);
            foreach (var row in rows)
            {
                int lvl = gm.GetPrestigeLevel(row.Def.Id);
                bool max = lvl >= row.Def.MaxLevel;
                double cost = row.Def.CostAt(lvl);
                row.Level.text = lvl.ToString();
                row.Effect.text = max ? row.Def.Effect(lvl) : row.Def.Effect(lvl) + "  <color=#" + hex + ">→  " + row.Def.Effect(lvl + 1) + "</color>";
                row.Cost.text = max ? "MÁX" : NumberFormat.Format(cost);
                row.CostIcon.enabled = !max;
                row.CostIcon.color = new Color(1, 1, 1, s.cores >= cost ? 1f : 0.55f);
                var lrt = row.Cost.rectTransform;
                lrt.offsetMin = new Vector2(max ? 0 : 72, lrt.offsetMin.y);
                row.Button.interactable = !max && s.cores >= cost;
            }
        }
    }
}
