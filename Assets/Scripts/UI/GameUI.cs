using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// UI construida por código: barra superior (dinero, por segundo, récord), panel inferior
    /// (lanzar, pestañas y tarjetas de mejora), etiquetas sobre los cubos y textos flotantes.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        class UpgradeRow
        {
            public UpgradeDef Def;
            public GameObject Go;
            public Color Cat;
            public Text Level, Name, Desc, Effect, Cost;
            public Image IconBg, Body;
            public Button Button;
            public RectTransform Fill;
            public Punch Punch, ButtonPunch;
            public float Flash;
            public Image Stripe;
            public Text NvLabel;
            public bool Locked;
            public bool Shown;
        }

        class Tab
        {
            public UpgradeCategory Cat;
            public Button Button;
            public Text Label;
            public Image Badge;
            public Punch Punch;
        }

        class PopupItem
        {
            public RectTransform Rt;
            public Text Text;
            public Vector3 World;
            public float Life;
        }

        const float PopupDuration = 0.9f;
        const int MaxPopups = 40;

        GameManager gm;
        Camera cam;
        RectTransform root, bucketLayer, popupLayer, listContent;
        ScrollRect scroll;

        Text moneyText, incomeText, bestText;
        Punch moneyPunch, bestPunch;
        double shownMoney, lastBest;
        float moneyPunchCooldown;

        Button launchButton;
        Punch launchPunch;
        RectTransform capacityFill;
        Text capacityText, tierText, autoText, evText;
        Image tierDot;

        UpgradeCategory currentTab = UpgradeCategory.Bolas;
        readonly List<Tab> tabs = new List<Tab>();
        readonly List<UpgradeRow> rows = new List<UpgradeRow>();
        readonly List<Text> bucketLabels = new List<Text>();
        readonly List<PopupItem> popups = new List<PopupItem>();
        readonly Stack<PopupItem> popupPool = new Stack<PopupItem>();
        readonly HashSet<UpgradeCategory> affordable = new HashSet<UpgradeCategory>();
        readonly HashSet<UpgradeCategory> newUnlocks = new HashSet<UpgradeCategory>();
        bool orderDirty = true;
        static readonly Color LockedColor = new Color(0.42f, 0.45f, 0.58f);
        bool bucketsDirty = true;
        float refreshTimer;

        // Avisos de desbloqueo
        readonly HashSet<string> knownUnlocked = new HashSet<string>();
        readonly Queue<UpgradeDef> unlockQueue = new Queue<UpgradeDef>();
        RectTransform banner;
        Text bannerText;
        Image bannerStripe;
        Punch bannerPunch;
        float bannerTimer;

        public void Init(GameManager gm)
        {
            this.gm = gm;
            cam = Camera.main;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasGo.transform;

            bucketLayer = UIFactory.Stretch("BucketLabels", root);
            popupLayer = UIFactory.Stretch("Popups", root);

            BuildTopBar();
            BuildOverloadBar();
            BuildRushLabel();
            BuildBottomPanel();
            prestigeUI = new GameObject("PrestigeUI").AddComponent<PrestigeUI>();
            prestigeUI.transform.SetParent(transform, false);

            foreach (var def in UpgradeCatalog.All)
                if (gm.IsVisible(def)) knownUnlocked.Add(def.Id);
            BuildBanner();
            prestigeUI.Init(gm, root); // por encima de todo

            shownMoney = gm.Money;
            lastBest = gm.State.bestDrop;
            gm.UpgradesChanged += () => { bucketsDirty = true; RefreshRows(); };
            gm.ProgressReset += OnProgressReset;
            RefreshRows();
        }

        // ---------------------------------------------------------------- Barra superior

        void BuildTopBar()
        {
            var bar = UIFactory.Rect("TopBar", root, new Vector2(0, 1f - BoardView.TopUI), Vector2.one);
            var body = UIFactory.Card("TopCard", bar, Vector2.zero, Vector2.one, UIFactory.Panel, 6);
            UIFactory.Inset((RectTransform)body.parent, 14, 6, 14, -40); // se esconde por arriba

            // Dinero centrado y, debajo en pequeño, el dinero por segundo
            var moneyArea = UIFactory.Rect("MoneyArea", body, new Vector2(0.25f, 0.36f), new Vector2(0.75f, 0.97f));
            moneyText = UIFactory.Text(UIFactory.Stretch("Money", moneyArea), "$0", 76, UIFactory.TextMain, TextAnchor.MiddleCenter, true);
            UIFactory.AddShadow(moneyText, 4f);
            moneyPunch = Punch.On(moneyArea, 0.06f, 0.25f);

            incomeText = UIFactory.Text(UIFactory.Rect("Income", body, new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.38f)),
                "", 30, UIFactory.MoneyGreen, TextAnchor.MiddleCenter, true);
            UIFactory.AddShadow(incomeText, 2f, 0.4f);

            bestText = BuildChip(body, "MÁX. BOLA", new Vector2(0.715f, 0.16f), new Vector2(0.975f, 0.86f), UIFactory.Gold, TextAnchor.MiddleRight);
            bestPunch = Punch.On(bestText.transform.parent, 0.15f, 0.35f);

            BuildPrestigeChip(body);
        }

        // --- Botón de prestigio (barra superior, izquierda) ---
        PrestigeUI prestigeUI;
        Text prestigeValue;
        Punch prestigePunch;
        float prestigePulse;

        void BuildPrestigeChip(Transform parent)
        {
            var chip = UIFactory.Rect("Chip_Prestige", parent, new Vector2(0.025f, 0.16f), new Vector2(0.285f, 0.86f));
            var img = UIFactory.Image(chip, UIFactory.CardDarkColor);
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            UIFactory.SetButtonColor(btn, UIFactory.CardDarkColor);
            btn.onClick.AddListener(() => prestigeUI.Open());
            UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Label", chip, new Vector2(0, 0.52f), Vector2.one), 20, 0, 20, 6),
                "PRESTIGIO", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
            prestigeValue = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Value", chip, Vector2.zero, new Vector2(1, 0.56f)), 20, 6, 20, 0),
                "", 32, PrestigeUI.CoreColor, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(prestigeValue, 2f, 0.4f);
            prestigePunch = Punch.On(chip, 0.1f, 0.3f);
        }

        void RefreshPrestigeChip(float dt)
        {
            double gain = gm.CoresOnPrestige;
            if (gm.CanPrestige)
            {
                prestigeValue.text = "+" + NumberFormat.Format(gain) + " lingotes";
                prestigePulse -= dt;
                if (prestigePulse <= 0) { prestigePulse = 1.2f; prestigePunch.Play(0.6f); } // llama la atención
            }
            else
            {
                // progreso hacia poder pagar el prestigio
                prestigeValue.text = Mathf.Min(99, Mathf.FloorToInt((float)(gm.Money / gm.PrestigeCost * 100))) + "%";
            }
        }

        /// <summary>Muestra un aviso en el cartel superior (lo usa también el prestigio).</summary>
        public void ShowBanner(string text, Color color)
        {
            bannerText.text = text;
            bannerStripe.color = color;
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();
            bannerPunch.Play(1.5f);
            bannerTimer = 2.5f;
        }

        Text resetLabel;
        float resetConfirm;

        // --- Barra de Sobrecarga ---
        static readonly Color OverloadA = new Color(0.35f, 0.75f, 1f);
        static readonly Color OverloadB = new Color(1f, 0.35f, 0.75f);
        RectTransform overloadBar, overloadFill;
        Image overloadFillImg, overloadGlow;
        Text overloadText;
        Punch overloadPunch;
        float overloadPulseTimer, overloadShown;

        void BuildOverloadBar()
        {
            float top = 1f - BoardView.TopUI;
            overloadBar = UIFactory.Rect("Overload", root, new Vector2(0.05f, top - BoardView.OverloadBar + 0.004f), new Vector2(0.95f, top - 0.004f));
            overloadGlow = UIFactory.Image(UIFactory.Inset(UIFactory.Stretch("Glow", overloadBar), -10, -10, -10, -10), new Color(1, 1, 1, 0));
            overloadGlow.raycastTarget = false;
            UIFactory.Image(overloadBar, UIFactory.CardDarkColor).raycastTarget = false;
            overloadFill = UIFactory.Inset(UIFactory.Rect("Fill", overloadBar, Vector2.zero, new Vector2(0, 1)), 4, 4, 4, 4);
            overloadFillImg = UIFactory.Image(overloadFill, OverloadA);
            overloadFillImg.raycastTarget = false;
            overloadText = UIFactory.Text(UIFactory.Stretch("Label", overloadBar), "SOBRECARGA", 26, UIFactory.TextMain, TextAnchor.MiddleCenter, true);
            UIFactory.AddShadow(overloadText, 2f, 0.6f);
            overloadPunch = Punch.On(overloadBar, 0.06f, 0.3f);

            gm.OverloadStarted += () =>
            {
                gm.Board.Shake(0.6f);
                gm.Background.Pulse(OverloadB, 1f);
                gm.Audio.Record();
                gm.Audio.PitchMul = 1.122f; // +2 semitonos
                bannerQueueOverload = true;
            };
            gm.OverloadEnded += () => gm.Audio.PitchMul = 1f;
        }

        bool bannerQueueOverload, bannerQueueRush;
        static readonly Color RushColor = new Color(0.35f, 0.95f, 1f);
        Text rushText;
        Punch rushPunch;
        float rushPulseTimer;

        void BuildRushLabel()
        {
            float top = 1f - BoardView.TopUI - BoardView.OverloadBar;
            var rt = UIFactory.Rect("Rush", root, new Vector2(0.25f, top - 0.04f), new Vector2(0.75f, top - 0.004f));
            UIFactory.Image(rt, new Color(0.05f, 0.25f, 0.35f, 0.85f)).raycastTarget = false;
            rushText = UIFactory.Text(UIFactory.Stretch("Text", rt), "", 28, RushColor, TextAnchor.MiddleCenter, true);
            UIFactory.AddShadow(rushText, 2f, 0.6f);
            rushPunch = Punch.On(rt, 0.12f, 0.3f);
            rt.gameObject.SetActive(false);

            gm.RushStarted += () =>
            {
                rt.gameObject.SetActive(true);
                rushPunch.Play(1.5f);
                bannerQueueRush = true;
                gm.Board.Shake(0.5f);
                gm.Background.Pulse(RushColor, 1f);
                gm.Audio.Crit();
            };
            gm.RushEnded += () => rt.gameObject.SetActive(false);
        }

        void UpdateRush(float dt)
        {
            if (!gm.InRush) return;
            rushText.text = "¡AVALANCHA!  x" + Balance.RushBoost + " bolas  " + Mathf.CeilToInt(gm.RushTimeLeft) + "s";
            rushPulseTimer -= dt;
            if (rushPulseTimer <= 0)
            {
                rushPulseTimer = 0.6f;
                rushPunch.Play(0.5f);
                gm.Background.Pulse(RushColor, 0.2f);
            }
        }

        void UpdateOverloadBar(float dt)
        {
            float target = gm.Overload;
            overloadShown = Mathf.MoveTowards(overloadShown, target, dt * 2f);
            overloadFill.anchorMax = new Vector2(Mathf.Max(0.02f, overloadShown), 1);
            float t = Time.unscaledTime;

            if (gm.InOverload)
            {
                // frenesí: barra rosa que se vacía, latiendo, con el fondo pulsando
                float beat = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                overloadFillImg.color = Color.Lerp(OverloadB, Color.white, 0.25f * beat);
                overloadGlow.color = new Color(OverloadB.r, OverloadB.g, OverloadB.b, 0.25f + 0.25f * beat);
                overloadText.text = "¡SOBRECARGA x" + NumberFormat.Multiplier(gm.Stats.OverloadMultiplier).Substring(1) + "!  " + Mathf.CeilToInt(gm.OverloadTimeLeft) + "s";
                overloadPulseTimer -= dt;
                if (overloadPulseTimer <= 0)
                {
                    overloadPulseTimer = 0.45f;
                    gm.Background.Pulse(OverloadB, 0.3f);
                }
            }
            else
            {
                // llenándose: se vuelve más rosa y nerviosa cuanto más cerca está
                float near = Mathf.InverseLerp(0.75f, 1f, target);
                overloadFillImg.color = Color.Lerp(OverloadA, OverloadB, overloadShown);
                float flicker = near > 0 ? (0.5f + 0.5f * Mathf.Sin(t * (10f + 20f * near))) : 0f;
                overloadGlow.color = new Color(OverloadB.r, OverloadB.g, OverloadB.b, 0.35f * near * flicker);
                overloadText.text = "SOBRECARGA  " + Mathf.FloorToInt(target * 100f) + "%";
            }
        }

        /// <summary>Botón de debug: pulsar dos veces seguidas borra la partida y empieza de cero.</summary>
        void BuildDebugReset(Transform parent)
        {
            var rt = UIFactory.Rect("DebugReset", parent, new Vector2(0.03f, 0.2f), new Vector2(0.2f, 0.8f));
            var btn = UIFactory.Button(rt, new Color(0.55f, 0.2f, 0.3f), out resetLabel, "RESET", 26);
            btn.onClick.AddListener(() =>
            {
                if (resetConfirm > 0)
                {
                    resetConfirm = 0;
                    resetLabel.text = "RESET";
                    gm.ResetProgress();
                }
                else
                {
                    resetConfirm = 2f;
                    resetLabel.text = "¿SEGURO?";
                }
            });
        }

        void OnProgressReset()
        {
            knownUnlocked.Clear();
            foreach (var def in UpgradeCatalog.All)
                if (gm.IsVisible(def)) knownUnlocked.Add(def.Id);
            unlockQueue.Clear();
            newUnlocks.Clear();
            shownMoney = 0;
            lastBest = 0;
            orderDirty = true;
            SelectTab(UpgradeCategory.Bolas);
        }

        Text BuildChip(Transform parent, string label, Vector2 min, Vector2 max, Color valueColor, TextAnchor align)
        {
            var chip = UIFactory.Rect("Chip_" + label, parent, min, max);
            UIFactory.Image(chip, UIFactory.CardDarkColor);
            UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Label", chip, new Vector2(0, 0.52f), Vector2.one), 20, 0, 20, 6),
                label, 22, UIFactory.TextDim, align);
            var value = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Value", chip, Vector2.zero, new Vector2(1, 0.56f)), 20, 6, 20, 0),
                "0", 38, valueColor, align, true);
            UIFactory.AddShadow(value, 2f, 0.4f);
            return value;
        }

        // ---------------------------------------------------------------- Panel inferior

        void BuildBottomPanel()
        {
            var holder = UIFactory.Rect("BottomPanel", root, Vector2.zero, new Vector2(1, BoardView.BottomUI));
            var bg = UIFactory.Inset(UIFactory.Stretch("Bg", holder), 0, -40, 0, 0); // se esconde por abajo
            UIFactory.Image(bg, UIFactory.Panel);
            var border = UIFactory.Rect("TopLine", holder, new Vector2(0.06f, 1f), new Vector2(0.94f, 1f));
            border.sizeDelta = new Vector2(border.sizeDelta.x, 4);
            UIFactory.Image(border, new Color(UIFactory.PanelBorder.r, UIFactory.PanelBorder.g, UIFactory.PanelBorder.b, 0.6f), false);

            BuildLaunchRow(UIFactory.Rect("LaunchRow", holder, new Vector2(0, 0.80f), new Vector2(1, 0.985f)));
            BuildTabs(UIFactory.Rect("Tabs", holder, new Vector2(0.03f, 0.695f), new Vector2(0.97f, 0.785f)));
            BuildList(UIFactory.Rect("Upgrades", holder, Vector2.zero, new Vector2(1, 0.685f)));
        }

        void BuildLaunchRow(RectTransform row)
        {
            var btnRt = UIFactory.Rect("LaunchButton", row, new Vector2(0.03f, 0.06f), new Vector2(0.56f, 0.96f));
            launchButton = UIFactory.Button(btnRt, UIFactory.Accent, out var label, "LANZAR", 48);
            label.rectTransform.anchorMin = new Vector2(0, 0.36f);
            launchPunch = Punch.On(btnRt, 0.08f, 0.22f);
            var hold = btnRt.gameObject.AddComponent<HoldButton>();
            hold.OnHold = () =>
            {
                if (!gm.ManualReady) return;
                gm.ManualLaunch();
                launchPunch.Play();
            };

            // Barra de capacidad dentro del botón
            var barBg = UIFactory.Rect("CapacityBar", btnRt, new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.34f));
            UIFactory.Image(barBg, new Color(0, 0, 0, 0.3f)).raycastTarget = false;
            capacityFill = UIFactory.Rect("Fill", barBg, Vector2.zero, new Vector2(0, 1));
            UIFactory.Image(capacityFill, new Color(1, 1, 1, 0.85f)).raycastTarget = false;
            capacityText = UIFactory.Text(UIFactory.Stretch("Count", barBg), "", 22, new Color(0.05f, 0.1f, 0.2f), TextAnchor.MiddleCenter, true);

            // Información a la derecha
            var info = UIFactory.Rect("Info", row, new Vector2(0.59f, 0.06f), new Vector2(0.97f, 0.96f));
            UIFactory.Image(info, UIFactory.CardDarkColor);
            tierText = InfoLine(info, 0, out tierDot);
            autoText = InfoLine(info, 1, out var autoDot);
            autoDot.color = UIFactory.CategoryColor(UpgradeCategory.Bolas);
            evText = InfoLine(info, 2, out var evDot);
            evDot.color = UIFactory.CategoryColor(UpgradeCategory.Multiplicadores);
        }

        Text InfoLine(RectTransform parent, int index, out Image dot)
        {
            float y1 = 1f - index / 3f, y0 = y1 - 1f / 3f;
            var line = UIFactory.Inset(UIFactory.Rect("Line" + index, parent, new Vector2(0, y0), new Vector2(1, y1)), 22, 0, 16, 0);
            var d = UIFactory.Rect("Dot", line, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            d.sizeDelta = new Vector2(18, 18);
            d.anchoredPosition = new Vector2(9, 0);
            dot = d.gameObject.AddComponent<Image>();
            dot.sprite = SpriteFactory.Circle;
            dot.raycastTarget = false;
            return UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Text", line), 34, 0, 0, 0), "", 27, UIFactory.TextMain, TextAnchor.MiddleLeft);
        }

        void BuildTabs(RectTransform container)
        {
            UIFactory.Image(container, UIFactory.CardDarkColor);
            var cats = (UpgradeCategory[])System.Enum.GetValues(typeof(UpgradeCategory));
            for (int i = 0; i < cats.Length; i++)
            {
                var cat = cats[i];
                var rt = UIFactory.Rect("Tab_" + cat, container, new Vector2((float)i / cats.Length, 0), new Vector2((float)(i + 1) / cats.Length, 1));
                UIFactory.Inset(rt, 6, 6, 6, 6);
                var tab = new Tab { Cat = cat };
                tab.Button = UIFactory.Button(rt, UIFactory.CardDarkColor, out tab.Label, TabName(cat), 26);
                tab.Punch = Punch.On(rt, 0.08f, 0.2f);
                tab.Button.onClick.AddListener(() => { SelectTab(cat); tab.Punch.Play(); gm.Audio.Click(); });

                var badge = UIFactory.Rect("Badge", rt, new Vector2(1, 1), new Vector2(1, 1));
                badge.sizeDelta = new Vector2(22, 22);
                badge.anchoredPosition = new Vector2(-14, -14);
                tab.Badge = badge.gameObject.AddComponent<Image>();
                tab.Badge.sprite = SpriteFactory.Circle;
                tab.Badge.color = UIFactory.Gold;
                tab.Badge.raycastTarget = false;
                tabs.Add(tab);
            }
        }

        void BuildList(RectTransform area)
        {
            scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40;

            var viewport = UIFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            UIFactory.Image(viewport, new Color(0, 0, 0, 0), false);

            listContent = UIFactory.Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1));
            listContent.pivot = new Vector2(0.5f, 1);
            var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 14, 40);
            vlg.spacing = 18;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = listContent;

            foreach (var def in UpgradeCatalog.All) rows.Add(BuildRow(def));
            SelectTab(currentTab);
        }

        UpgradeRow BuildRow(UpgradeDef def)
        {
            var cat = UIFactory.CategoryColor(def.Category);
            var body = UIFactory.Card(def.Id, listContent, Vector2.zero, Vector2.one, UIFactory.CardColor, 7);
            var holder = (RectTransform)body.parent;
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 176;

            var row = new UpgradeRow { Def = def, Go = holder.gameObject, Cat = cat, Body = body.GetComponent<Image>() };
            row.Punch = Punch.On(holder, 0.04f, 0.25f);

            // Franja de color de la sección
            var stripe = UIFactory.Rect("Stripe", body, new Vector2(0, 0.18f), new Vector2(0, 0.82f));
            stripe.sizeDelta = new Vector2(7, 0);
            stripe.anchoredPosition = new Vector2(10, 0);
            row.Stripe = UIFactory.Image(stripe, cat);
            row.Stripe.raycastTarget = false;

            // Icono con el nivel
            var icon = UIFactory.Rect("Icon", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            icon.sizeDelta = new Vector2(108, 108);
            icon.anchoredPosition = new Vector2(90, 0);
            row.IconBg = icon.gameObject.AddComponent<Image>();
            row.IconBg.sprite = SpriteFactory.Circle;
            row.IconBg.color = new Color(cat.r, cat.g, cat.b, 0.18f);
            row.IconBg.raycastTarget = false;
            row.NvLabel = UIFactory.Text(UIFactory.Rect("Nv", icon, new Vector2(0, 0.6f), new Vector2(1, 0.88f)), "NV", 20, cat, TextAnchor.MiddleCenter, true);
            row.Level = UIFactory.Text(UIFactory.Rect("Level", icon, new Vector2(0, 0.1f), new Vector2(1, 0.66f)), "0", 42,
                Color.Lerp(UIFactory.TextMain, cat, 0.25f), TextAnchor.MiddleCenter, true);

            const float left = 164;
            row.Name = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Name", body, new Vector2(0, 0.62f), new Vector2(0.69f, 0.94f)), left, 0, 0, 0),
                def.Name, 34, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
            row.Desc = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Desc", body, new Vector2(0, 0.34f), new Vector2(0.69f, 0.64f)), left, 0, 6, 0),
                def.Desc ?? "", 23, UIFactory.TextDim, TextAnchor.MiddleLeft);
            row.Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.Effect = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Effect", body, new Vector2(0, 0.06f), new Vector2(0.69f, 0.36f)), left, 0, 0, 0),
                "", 26, UIFactory.TextMain, TextAnchor.MiddleLeft);

            // Botón de compra con barra de progreso hacia el coste
            var btnRt = UIFactory.Rect("Buy", body, new Vector2(0.70f, 0.18f), new Vector2(0.965f, 0.82f));
            row.Button = UIFactory.Button(btnRt, cat, out row.Cost, "", 34);
            row.Fill = UIFactory.Rect("Fill", btnRt, Vector2.zero, new Vector2(0, 1));
            row.Fill.SetSiblingIndex(0);
            UIFactory.Image(row.Fill, new Color(cat.r, cat.g, cat.b, 0.35f)).raycastTarget = false;
            row.ButtonPunch = Punch.On(btnRt, 0.12f, 0.25f);
            row.Button.onClick.AddListener(() =>
            {
                if (gm.TryBuy(def))
                {
                    row.Punch.Play();
                    row.ButtonPunch.Play();
                    row.Flash = 1f;
                }
            });
            return row;
        }

        /// <summary>Cambia el aspecto de la tarjeta entre normal y bloqueada.</summary>
        void SetLockedLook(UpgradeRow row, bool locked)
        {
            if (row.Locked == locked && row.Name.text != "") return;
            row.Locked = locked;
            var c = locked ? LockedColor : row.Cat;
            row.Stripe.color = c;
            row.IconBg.color = new Color(c.r, c.g, c.b, locked ? 0.12f : 0.18f);
            row.NvLabel.gameObject.SetActive(!locked);
            row.Level.color = locked ? LockedColor : Color.Lerp(UIFactory.TextMain, row.Cat, 0.25f);
            row.Name.text = locked ? "???" : row.Def.Name;
            row.Name.color = locked ? LockedColor : UIFactory.TextMain;
            row.Body.color = locked ? UIFactory.CardDarkColor : UIFactory.CardColor;
            row.Fill.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.35f);
            if (!locked) row.Desc.text = row.Def.Desc ?? "";
        }

        void RefreshLocked(UpgradeRow row)
        {
            var def = row.Def;
            var req = UpgradeCatalog.Get(def.RequiresId);
            bool reqKnown = req != null && gm.IsVisible(req);
            int have = req != null ? gm.GetLevel(req.Id) : 0;
            float progress = def.RequiresLevel > 0 ? Mathf.Clamp01((float)have / def.RequiresLevel) : 0;

            row.Level.text = "?";
            row.Desc.text = reqKnown
                ? "Sube <color=#" + ColorUtility.ToHtmlStringRGB(UIFactory.CategoryColor(req.Category)) + ">" + req.Name +
                  "</color> a nivel " + def.RequiresLevel + "  (" + have + "/" + def.RequiresLevel + ")"
                : "Bloqueada";
            row.Effect.text = "";
            row.Button.interactable = false;
            row.Cost.text = "?";
            row.Cost.color = LockedColor;
            row.Fill.gameObject.SetActive(reqKnown && progress > 0.02f);
            row.Fill.anchorMax = new Vector2(progress, 1);
        }

        /// <summary>Desbloqueadas arriba (en orden de catálogo) y bloqueadas al final.</summary>
        void ReorderRows()
        {
            orderDirty = false;
            int index = 0;
            foreach (var row in rows) if (!row.Locked) row.Go.transform.SetSiblingIndex(index++);
            foreach (var row in rows) if (row.Locked) row.Go.transform.SetSiblingIndex(index++);
        }

        void BuildBanner()
        {
            banner = UIFactory.Rect("UnlockBanner", root, new Vector2(0.08f, BoardView.BottomUI + 0.012f), new Vector2(0.92f, BoardView.BottomUI + 0.062f));
            var body = UIFactory.Card("Body", banner, Vector2.zero, Vector2.one, UIFactory.CardColor, 6);
            var stripe = UIFactory.Inset(UIFactory.Rect("Stripe", body, Vector2.zero, new Vector2(0, 1)), 0, 10, 0, 10);
            stripe.sizeDelta = new Vector2(10, stripe.sizeDelta.y);
            stripe.anchoredPosition = new Vector2(14, 0);
            bannerStripe = UIFactory.Image(stripe, Color.white);
            bannerText = UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Text", body), 36, 0, 20, 0), "", 32, UIFactory.TextMain, TextAnchor.MiddleCenter, true);
            bannerPunch = Punch.On(banner, 0.12f, 0.35f);
            banner.gameObject.SetActive(false);
        }

        void UpdateBanner(float dt)
        {
            if (bannerTimer > 0)
            {
                bannerTimer -= dt;
                if (bannerTimer <= 0) banner.gameObject.SetActive(false);
                return;
            }
            if (bannerQueueRush)
            {
                bannerQueueRush = false;
                bannerText.text = "¡AVALANCHA!  <color=#7FF2FF>capacidad y lanzador x" + Balance.RushBoost + "</color>";
                bannerStripe.color = RushColor;
                banner.gameObject.SetActive(true);
                bannerPunch.Play(1.5f);
                bannerTimer = 2f;
                return;
            }
            if (bannerQueueOverload)
            {
                bannerQueueOverload = false;
                bannerText.text = "¡SOBRECARGA!  <color=#FF8AC8>todo x" + NumberFormat.Multiplier(gm.Stats.OverloadMultiplier).Substring(1) + "</color>";
                bannerStripe.color = OverloadB;
                banner.gameObject.SetActive(true);
                bannerPunch.Play(1.5f);
                bannerTimer = 2f;
                return;
            }
            if (unlockQueue.Count == 0) return;

            var def = unlockQueue.Dequeue();
            var cat = UIFactory.CategoryColor(def.Category);
            bannerText.text = "¡Nueva mejora!  <color=#" + ColorUtility.ToHtmlStringRGB(cat) + ">" + def.Name + "</color>";
            bannerStripe.color = cat;
            banner.gameObject.SetActive(true);
            banner.localScale = Vector3.one;
            bannerPunch.Play();
            bannerTimer = 2.2f;
            gm.Audio.Record();
        }

        static string TabName(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Bolas => "Bolas",
            UpgradeCategory.Pinchos => "Pinchos",
            UpgradeCategory.Rebotes => "Rebotes",
            UpgradeCategory.Multiplicadores => "Multipl.",
            UpgradeCategory.Sobrecarga => "Sobrecarga",
            _ => c.ToString()
        };

        void SelectTab(UpgradeCategory cat)
        {
            currentTab = cat;
            foreach (var t in tabs)
            {
                bool on = t.Cat == cat;
                UIFactory.SetButtonColor(t.Button, on ? UIFactory.CategoryColor(t.Cat) : UIFactory.CardDarkColor);
                t.Label.color = on ? UIFactory.TextMain : UIFactory.TextDim;
            }
            RefreshRows();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        void RefreshRows()
        {
            affordable.Clear();
            foreach (var row in rows)
            {
                var def = row.Def;
                bool unlocked = gm.IsVisible(def);
                int lvl = gm.GetLevel(def.Id);
                bool max = lvl >= def.MaxLevel;
                double cost = def.CostAt(lvl);
                bool canBuy = unlocked && !max && gm.Money >= cost;
                if (canBuy) affordable.Add(def.Category);

                if (unlocked && !knownUnlocked.Contains(def.Id))
                {
                    knownUnlocked.Add(def.Id);
                    unlockQueue.Enqueue(def);
                    row.Flash = 1f;
                    newUnlocks.Add(def.Category);
                    var tab = tabs.Find(x => x.Cat == def.Category);
                    if (tab != null && def.Category != currentTab) tab.Punch.Play();
                }

                // Las bloqueadas se ven en gris y sin revelar qué son
                bool locked = !unlocked;
                bool visible = def.Category == currentTab && gm.AppliesToBoard(def);
                if (row.Go.activeSelf != visible) row.Go.SetActive(visible);
                if (visible != row.Shown || locked != row.Locked) orderDirty = true;
                row.Shown = visible;
                if (!visible) continue;
                SetLockedLook(row, locked);
                if (locked) { RefreshLocked(row); continue; }

                string catHex = ColorUtility.ToHtmlStringRGB(row.Cat);
                row.Level.text = lvl.ToString();
                row.Effect.text = max
                    ? def.Effect(lvl)
                    : def.Effect(lvl) + "  <color=#" + catHex + ">→  " + def.Effect(lvl + 1) + "</color>";
                row.Cost.text = max ? "MÁX" : NumberFormat.Money(cost);
                row.Button.interactable = canBuy;
                float progress = max ? 0f : (float)System.Math.Min(1.0, gm.Money / cost);
                row.Fill.gameObject.SetActive(!canBuy && !max && progress > 0.02f);
                row.Fill.anchorMax = new Vector2(progress, 1);
                row.Cost.color = canBuy || max ? UIFactory.TextMain : UIFactory.TextDim;
            }

            if (orderDirty) ReorderRows();

            foreach (var t in tabs)
            {
                if (t.Cat == currentTab) newUnlocks.Remove(t.Cat); // visto
                t.Badge.enabled = (affordable.Contains(t.Cat) || newUnlocks.Contains(t.Cat)) && t.Cat != currentTab;
            }
        }

        // ---------------------------------------------------------------- Update

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateMoney(dt);

            if (resetConfirm > 0)
            {
                resetConfirm -= dt;
                if (resetConfirm <= 0 && resetLabel != null) resetLabel.text = "RESET";
            }

            refreshTimer -= dt;
            if (refreshTimer <= 0)
            {
                refreshTimer = 0.15f;
                RefreshRows();
                RefreshInfo();
            }

            foreach (var row in rows)
            {
                if (row.Flash <= 0) continue;
                row.Flash = Mathf.Max(0, row.Flash - dt * 3f);
                row.Body.color = Color.Lerp(UIFactory.CardColor, Color.Lerp(UIFactory.CardColor, row.Cat, 0.5f), row.Flash);
            }

            UpdateOverloadBar(dt);
            RefreshPrestigeChip(dt);
            UpdateRush(dt);
            UpdateBanner(dt);
            UpdatePopups(Time.deltaTime);
        }

        /// <summary>Contador rodante: el número "sube" hacia el valor real en vez de saltar.</summary>
        void UpdateMoney(float dt)
        {
            double target = gm.Money;
            if (target < shownMoney || shownMoney <= 0) shownMoney = target; // al gastar, salto directo
            else if (target > shownMoney)
            {
                shownMoney += (target - shownMoney) * System.Math.Min(1.0, dt * 10.0);
                if (target - shownMoney < System.Math.Max(0.05, target * 0.001)) shownMoney = target;

                moneyPunchCooldown -= dt;
                if (moneyPunchCooldown <= 0) { moneyPunch.Play(); moneyPunchCooldown = 0.15f; }
            }

            string s = "<color=#" + ColorUtility.ToHtmlStringRGB(UIFactory.MoneyGreen) + ">$</color>" + NumberFormat.Format(shownMoney);
            if (moneyText.text != s) moneyText.text = s;
        }

        void RefreshInfo()
        {
            var s = gm.Stats;
            incomeText.text = "+" + NumberFormat.Money(gm.IncomePerSecond) + " /s";
            bestText.text = NumberFormat.Money(gm.State.bestDrop);
            if (gm.State.bestDrop > lastBest) { lastBest = gm.State.bestDrop; bestPunch.Play(); }

            launchButton.interactable = gm.CanLaunch;
            int maxNow = gm.MaxBallsNow;
            float ratio = maxNow == 0 ? 0 : Mathf.Clamp01((float)gm.Balls.MainCount / maxNow);
            capacityFill.anchorMax = new Vector2(ratio, 1);
            capacityText.text = gm.Balls.MainCount + " / " + maxNow;

            tierDot.color = s.Tier.Color;
            tierText.text = "Bola <b>" + s.Tier.Name + "</b>";
            autoText.text = "Auto " + (gm.LaunchInterval > 0
                ? "<b>" + (1f / gm.LaunchInterval).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " bolas/s</b>"
                : "<color=#7F88AA>apagado</color>");
            evText.text = "Media cubos <b>" + NumberFormat.Multiplier(s.ExpectedBucketMultiplier()) + "</b>";
        }

        // ---------------------------------------------------------------- Etiquetas de cubos

        void LateUpdate()
        {
            if (bucketsDirty) RebuildBucketLabels();
            var board = gm.Board;
            for (int i = 0; i < bucketLabels.Count; i++)
            {
                var rt = bucketLabels[i].rectTransform;
                rt.anchoredPosition = WorldToCanvas(board.BucketCenterPos(i));
                rt.localScale = Vector3.one * board.BucketPulseScale(i);
            }
        }

        void RebuildBucketLabels()
        {
            bucketsDirty = false;
            var board = gm.Board;
            while (bucketLabels.Count < board.BucketCount)
            {
                var rt = UIFactory.Rect("Bucket", bucketLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                rt.sizeDelta = new Vector2(120, 50);
                var t = UIFactory.Text(rt, "", 26, Color.white, TextAnchor.MiddleCenter, true);
                UIFactory.AddShadow(t, 2f, 0.6f);
                bucketLabels.Add(t);
            }
            for (int i = 0; i < bucketLabels.Count; i++)
            {
                bool on = i < board.BucketCount;
                bucketLabels[i].gameObject.SetActive(on);
                if (!on) continue;
                bucketLabels[i].text = NumberFormat.Multiplier(gm.Stats.BucketMultiplier(board.BucketDistance(i)));
            }
            float px = Mathf.Abs(WorldToCanvas(board.BucketCenterPos(1)).x - WorldToCanvas(board.BucketCenterPos(0)).x);
            int size = Mathf.Clamp(Mathf.RoundToInt(px * 0.32f), 14, 40);
            foreach (var t in bucketLabels) t.fontSize = size;
        }

        public void MarkBucketsDirty() => bucketsDirty = true;

        Vector2 WorldToCanvas(Vector3 world)
        {
            if (cam == null) cam = Camera.main;
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            return local;
        }

        // ---------------------------------------------------------------- Textos flotantes

        /// <summary>
        /// Texto flotante. Los de baja prioridad (efectos de rebote) se omiten cuando ya hay muchos en pantalla,
        /// para dejar sitio a lo importante (caídas, críticos) y no saturar con muchas bolas.
        /// </summary>
        public void Popup(Vector3 world, string text, Color color, float scale = 1f, bool lowPriority = false)
        {
            if (popups.Count >= MaxPopups) return;
            if (lowPriority && popups.Count >= MaxPopups / 2) return;
            PopupItem p;
            if (popupPool.Count > 0) { p = popupPool.Pop(); p.Rt.gameObject.SetActive(true); }
            else
            {
                var rt = UIFactory.Rect("Popup", popupLayer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                rt.sizeDelta = new Vector2(300, 60);
                var t = UIFactory.Text(rt, "", 36, Color.white, TextAnchor.MiddleCenter, true);
                t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0.05f, 0.7f);
                p = new PopupItem { Rt = rt, Text = t };
            }
            p.World = world;
            p.Life = 0;
            p.Text.text = text;
            p.Text.color = color;
            p.Text.fontSize = Mathf.RoundToInt(36 * scale);
            p.Rt.anchoredPosition = WorldToCanvas(world);
            p.Rt.localScale = Vector3.zero;
            popups.Add(p);
        }

        void UpdatePopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.Life += dt;
                float t = p.Life / PopupDuration;
                if (t >= 1f)
                {
                    p.Rt.gameObject.SetActive(false);
                    popupPool.Push(p);
                    popups.RemoveAt(i);
                    continue;
                }
                // aparece con rebote, sube frenando y se desvanece
                float scale = t < 0.18f ? EaseOutBack(t / 0.18f) : 1f;
                p.Rt.localScale = Vector3.one * scale;
                float rise = 1f - (1f - t) * (1f - t);
                p.Rt.anchoredPosition = WorldToCanvas(p.World) + Vector2.up * (90f * rise);
                var c = p.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                p.Text.color = c;
            }
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
