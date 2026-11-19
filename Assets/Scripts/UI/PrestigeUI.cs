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
            public Button Button;
            public Punch Punch;
        }

        GameManager gm;
        RectTransform panel;
        Text coresText, gainText, nextText, prestigeLabel, resetLabel;
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

            // Cabecera
            var title = UIFactory.Text(UIFactory.Rect("Title", card, new Vector2(0.06f, 0.92f), new Vector2(0.8f, 0.985f)),
                "PRESTIGIO", 52, CoreColor, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(title, 3f);
            var closeRt = UIFactory.Rect("Close", card, new Vector2(0.84f, 0.925f), new Vector2(0.96f, 0.98f));
            UIFactory.Button(closeRt, UIFactory.CardDarkColor, out _, "X", 36).onClick.AddListener(Close);

            // Lingotes disponibles
            var coresRt = UIFactory.Rect("Cores", card, new Vector2(0.06f, 0.845f), new Vector2(0.94f, 0.915f));
            UIFactory.Image(coresRt, UIFactory.CardDarkColor);
            Ingot(coresRt, new Vector2(56, 0), 1f);
            coresText = UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Text", coresRt), 100, 0, 20, 0), "", 34, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
            coresPunch = Punch.On(coresRt, 0.08f, 0.3f);

            UIFactory.Text(UIFactory.Rect("Info", card, new Vector2(0.06f, 0.775f), new Vector2(0.94f, 0.84f)),
                "Paga para reiniciar tu partida y llévate lingotes de oro. Con ellos desbloqueas secciones nuevas (Rebotes, Sobrecarga, Multiplicadores) y mejoras permanentes. Cada lingote sin gastar da +1% de dinero.",
                24, UIFactory.TextDim, TextAnchor.MiddleLeft).horizontalOverflow = HorizontalWrapMode.Wrap;

            // Bloque de reinicio
            var gainRt = UIFactory.Rect("Gain", card, new Vector2(0.06f, 0.63f), new Vector2(0.94f, 0.765f));
            UIFactory.Image(gainRt, UIFactory.CardColor);
            gainText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Gain", gainRt, new Vector2(0, 0.55f), new Vector2(0.58f, 1f)), 24, 0, 0, 0),
                "", 32, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
            nextText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Next", gainRt, new Vector2(0, 0.3f), new Vector2(0.58f, 0.58f)), 24, 0, 0, 0),
                "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
            var barBg = UIFactory.Inset(UIFactory.Rect("Bar", gainRt, new Vector2(0, 0.1f), new Vector2(0.58f, 0.24f)), 24, 0, 0, 0);
            UIFactory.Image(barBg, new Color(0, 0, 0, 0.35f), false).raycastTarget = false;
            gainFill = UIFactory.Rect("Fill", barBg, Vector2.zero, new Vector2(0, 1));
            UIFactory.Image(gainFill, CoreColor, false).raycastTarget = false;

            var btnRt = UIFactory.Rect("PrestigeButton", gainRt, new Vector2(0.62f, 0.14f), new Vector2(0.97f, 0.86f));
            prestigeButton = UIFactory.Button(btnRt, CoreColor, out prestigeLabel, "REINICIAR", 28);
            prestigeButton.onClick.AddListener(OnPrestigeClicked);

            // Tienda
            UIFactory.Text(UIFactory.Rect("ShopTitle", card, new Vector2(0.06f, 0.575f), new Vector2(0.94f, 0.62f)),
                "TIENDA PERMANENTE", 28, UIFactory.TextDim, TextAnchor.MiddleLeft, true);
            BuildShop(UIFactory.Rect("Shop", card, new Vector2(0.03f, 0.07f), new Vector2(0.97f, 0.57f)));

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

        /// <summary>Icono de lingote: barra dorada con brillo arriba.</summary>
        public static void Ingot(Transform parent, Vector2 pos, float scale)
        {
            var bar = UIFactory.Rect("Ingot", parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            bar.sizeDelta = new Vector2(58, 30) * scale;
            bar.anchoredPosition = pos;
            UIFactory.Image(bar, new Color(0.85f, 0.58f, 0.12f)).raycastTarget = false;
            var top = UIFactory.Inset(UIFactory.Rect("Top", bar, new Vector2(0.12f, 0.45f), new Vector2(0.88f, 1f)), 0, 0, 0, 3 * scale);
            UIFactory.Image(top, CoreColor).raycastTarget = false;
            var shine = UIFactory.Rect("Shine", bar, new Vector2(0.2f, 0.72f), new Vector2(0.5f, 0.86f));
            UIFactory.Image(shine, new Color(1f, 0.97f, 0.8f, 0.9f), false).raycastTarget = false;
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
                iconImg.raycastTarget = false;
                row.Level = UIFactory.Text(UIFactory.Stretch("Level", icon), "0", 36, CoreColor, TextAnchor.MiddleCenter, true);

                const float left = 130;
                UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Name", body, new Vector2(0, 0.62f), new Vector2(0.68f, 0.94f)), left, 0, 0, 0),
                    def.Name, 32, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
                UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Desc", body, new Vector2(0, 0.36f), new Vector2(0.68f, 0.64f)), left, 0, 0, 0),
                    def.Desc, 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
                row.Effect = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Effect", body, new Vector2(0, 0.06f), new Vector2(0.68f, 0.38f)), left, 0, 0, 0),
                    "", 24, UIFactory.TextMain, TextAnchor.MiddleLeft);

                var btnRt = UIFactory.Rect("Buy", body, new Vector2(0.70f, 0.18f), new Vector2(0.965f, 0.82f));
                row.Button = UIFactory.Button(btnRt, CoreColor, out row.Cost, "", 30);
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
            coresText.text = NumberFormat.Format(s.cores) + " lingotes   <size=24><color=#9AA3C8>(+" +
                             NumberFormat.Percent(PrestigeBalance.UnspentCoreBonus * s.cores) + " dinero)</color></size>";

            double gain = gm.CoresOnPrestige;
            double prestigeCost = gm.PrestigeCost;
            gainText.text = "Recompensa: +" + NumberFormat.Format(gain) + " lingotes";
            nextText.text = gm.CanPrestige
                ? "¡Ya puedes reiniciar!"
                : "Tienes " + NumberFormat.Money(s.money) + " de " + NumberFormat.Money(prestigeCost);
            gainFill.anchorMax = new Vector2(Mathf.Clamp01((float)(s.money / prestigeCost)), 1);
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
                row.Cost.text = max ? "MÁX" : NumberFormat.Format(cost) + (cost == 1 ? " lingote" : " lingotes");
                row.Button.interactable = !max && s.cores >= cost;
            }
        }
    }
}
