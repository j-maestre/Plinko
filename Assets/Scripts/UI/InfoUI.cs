using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// Ayudas: mini tutorial (sale solo la primera vez y con el botón "?") y la lista de tipos de bola
    /// con su valor y el coste de fusionar (botón "i"). Los dos botones van en la esquina superior derecha del tablero.
    /// </summary>
    public class InfoUI : MonoBehaviour
    {
        static readonly (string title, string body)[] Pages =
        {
            ("¡Bienvenido a Plinko!", "Tus bolas caen solas por el tablero. Cada rebote en un pincho da dinero, y el cubo en el que caen lo multiplica."),
            ("Compra bolas", "Pulsa COMPRAR para añadir una bola de madera. La primera es gratis. Cada bola ocupa un hueco: amplía huecos en la pestaña Bolas."),
            ("Fusiona", "FUSIONAR junta tus dos bolas iguales de mayor nivel en una de un material mejor, que vale mucho más: madera → bronce → plata → hierro…"),
            ("Mejora", "En las pestañas de abajo mejoras huecos, goteo, pinchos y más. Las mejoras con \"???\" se descubren al avanzar."),
            ("Momentos especiales", "Llena la barra de SOBRECARGA rebotando para multiplicar todo. Y de vez en cuando, una bola desatará la AVALANCHA."),
            ("Prestigio", "Con mucho dinero podrás reiniciar en PRESTIGIO a cambio de lingotes de oro: desbloquean secciones nuevas y mejoras permanentes."),
        };

        GameManager gm;
        RectTransform root;

        // tutorial
        RectTransform tutorial;
        Text tutTitle, tutBody, tutNextLabel;
        Button tutPrev;
        readonly List<Image> dots = new List<Image>();
        int page;

        // lista de bolas
        RectTransform list;
        readonly List<(int level, Text value, Text merge, Text owned)> listRows = new List<(int, Text, Text, Text)>();

        public void Init(GameManager gm, RectTransform canvasRoot)
        {
            this.gm = gm;
            root = canvasRoot;

            float top = BoardView.TopEdge - BoardView.TopUI - BoardView.OverloadBar;
            RoundButton("Help", "?", new Vector2(0.93f, top - 0.035f), () => OpenTutorial());
            RoundButton("Balls", "i", new Vector2(0.93f, top - 0.085f), OpenList);

            BuildTutorial();
            BuildList();

            if (!gm.State.tutorialDone) Invoke(nameof(OpenFirstTime), 1f);
        }

        void OpenFirstTime() => OpenTutorial();

        void RoundButton(string name, string glyph, Vector2 anchor, UnityEngine.Events.UnityAction action)
        {
            var rt = UIFactory.Rect(name, root, anchor, anchor);
            rt.sizeDelta = new Vector2(76, 76);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Circle;
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            UIFactory.SetButtonColor(btn, new Color(0.14f, 0.16f, 0.30f, 0.92f));
            var ring = UIFactory.Rect("Ring", rt, Vector2.zero, Vector2.one);
            var ringImg = ring.gameObject.AddComponent<Image>();
            ringImg.sprite = SpriteFactory.Ring;
            ringImg.color = new Color(0.55f, 0.65f, 1f, 0.6f);
            ringImg.raycastTarget = false;
            UIFactory.Text(UIFactory.Stretch("G", rt), glyph, 40, Color.white, TextAnchor.MiddleCenter, true);
            var punch = Punch.On(rt, 0.15f, 0.25f);
            btn.onClick.AddListener(() => { punch.Play(); gm.Audio.Click(); action(); });
        }

        RectTransform Modal(string name, Vector2 min, Vector2 max, out RectTransform card)
        {
            var modal = UIFactory.Stretch(name, root);
            var backdrop = UIFactory.Image(modal, new Color(0.01f, 0.01f, 0.05f, 0.85f), false);
            var close = modal.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.targetGraphic = backdrop;
            close.onClick.AddListener(() => CloseModal(modal));
            card = UIFactory.Card("Card", modal, min, max, UIFactory.Panel, 10);
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // tocar la tarjeta no cierra
            modal.gameObject.SetActive(false);
            return modal;
        }

        void CloseModal(RectTransform modal)
        {
            modal.gameObject.SetActive(false);
            if (modal == tutorial && !gm.State.tutorialDone) { gm.State.tutorialDone = true; gm.Save(); }
        }

        // ------------------------------------------------------------------ Tutorial

        void BuildTutorial()
        {
            tutorial = Modal("Tutorial", new Vector2(0.07f, 0.3f), new Vector2(0.93f, 0.7f), out var card);
            tutTitle = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Title", card, new Vector2(0, 0.76f), new Vector2(1, 0.95f)), 40, 0, 40, 0),
                "", 46, UIFactory.Accent, TextAnchor.MiddleLeft, true);
            UIFactory.AddShadow(tutTitle, 3f);
            tutBody = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Body", card, new Vector2(0, 0.30f), new Vector2(1, 0.76f)), 40, 0, 40, 0),
                "", 32, UIFactory.TextMain, TextAnchor.UpperLeft);
            tutBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutBody.lineSpacing = 1.15f;

            for (int i = 0; i < Pages.Length; i++)
            {
                float x = 0.5f + (i - (Pages.Length - 1) * 0.5f) * 0.05f;
                var d = UIFactory.Rect("Dot", card, new Vector2(x, 0.25f), new Vector2(x, 0.25f));
                d.sizeDelta = new Vector2(18, 18);
                var img = d.gameObject.AddComponent<Image>();
                img.sprite = SpriteFactory.Circle;
                img.raycastTarget = false;
                dots.Add(img);
            }

            tutPrev = UIFactory.Button(UIFactory.Rect("Prev", card, new Vector2(0.06f, 0.05f), new Vector2(0.36f, 0.19f)),
                UIFactory.CardDarkColor, out _, "Atrás", 30);
            tutPrev.onClick.AddListener(() => { page = Mathf.Max(0, page - 1); ShowPage(); });
            var next = UIFactory.Button(UIFactory.Rect("Next", card, new Vector2(0.52f, 0.05f), new Vector2(0.94f, 0.19f)),
                UIFactory.Accent, out tutNextLabel, "Siguiente", 30);
            next.onClick.AddListener(() =>
            {
                if (page >= Pages.Length - 1) { CloseModal(tutorial); return; }
                page++;
                ShowPage();
            });
        }

        void OpenTutorial()
        {
            page = 0;
            tutorial.gameObject.SetActive(true);
            tutorial.SetAsLastSibling();
            ShowPage();
        }

        void ShowPage()
        {
            tutTitle.text = Pages[page].title;
            tutBody.text = Pages[page].body;
            tutPrev.gameObject.SetActive(page > 0);
            tutNextLabel.text = page >= Pages.Length - 1 ? "¡A jugar!" : "Siguiente";
            for (int i = 0; i < dots.Count; i++)
                dots[i].color = i == page ? UIFactory.Accent : new Color(1, 1, 1, 0.25f);
        }

        // ------------------------------------------------------------------ Lista de bolas

        void BuildList()
        {
            list = Modal("BallList", new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f), out var card);
            UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Title", card, new Vector2(0, 0.92f), new Vector2(0.8f, 0.985f)), 40, 0, 0, 0),
                "TIPOS DE BOLA", 44, UIFactory.Accent, TextAnchor.MiddleLeft, true);
            UIFactory.Button(UIFactory.Rect("Close", card, new Vector2(0.84f, 0.925f), new Vector2(0.96f, 0.98f)),
                UIFactory.CardDarkColor, out _, "X", 36).onClick.AddListener(() => CloseModal(list));
            UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Sub", card, new Vector2(0, 0.875f), new Vector2(1, 0.92f)), 40, 0, 40, 0),
                "Valor que da cada bola al caer en un cubo x1, y lo que cuesta conseguirla fusionando.", 22, UIFactory.TextDim, TextAnchor.MiddleLeft)
                .horizontalOverflow = HorizontalWrapMode.Wrap;

            var area = UIFactory.Rect("List", card, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.87f));
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40;
            var viewport = UIFactory.Stretch("Viewport", area);
            viewport.gameObject.AddComponent<RectMask2D>();
            UIFactory.Image(viewport, new Color(0, 0, 0, 0), false);
            var content = UIFactory.Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(0.5f, 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 20);
            vlg.spacing = 10;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            for (int level = 1; level <= BallMaterials.Count; level++)
            {
                var body = UIFactory.Card("Lv" + level, content, Vector2.zero, Vector2.one, UIFactory.CardColor, 5);
                ((RectTransform)body.parent).gameObject.AddComponent<LayoutElement>().preferredHeight = 120;

                var icon = UIFactory.Rect("Icon", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                icon.sizeDelta = new Vector2(84, 84);
                icon.anchoredPosition = new Vector2(64, 0);
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = BallMaterials.GetSprite(level);
                img.raycastTarget = false;

                const float left = 124;
                UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Name", body, new Vector2(0, 0.52f), new Vector2(0.62f, 0.95f)), left, 0, 0, 0),
                    "<size=24><color=#9AA3C8>Nv " + level + "</color></size>  " + BallMaterials.GetName(level), 32, UIFactory.TextMain, TextAnchor.MiddleLeft, true);
                var merge = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Merge", body, new Vector2(0, 0.06f), new Vector2(0.62f, 0.5f)), left, 0, 0, 0),
                    "", 22, UIFactory.TextDim, TextAnchor.MiddleLeft);
                var value = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Value", body, new Vector2(0.6f, 0.45f), new Vector2(1, 0.95f)), 0, 0, 20, 0),
                    "", 32, new Color(0.49f, 0.89f, 0.55f), TextAnchor.MiddleRight, true);
                var owned = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Owned", body, new Vector2(0.6f, 0.06f), new Vector2(1, 0.45f)), 0, 0, 20, 0),
                    "", 22, UIFactory.Gold, TextAnchor.MiddleRight);
                listRows.Add((level, value, merge, owned));
            }
        }

        void OpenList()
        {
            int tier = gm.Stats.TierIndex;
            var counts = new Dictionary<int, int>();
            foreach (var b in gm.Inventory.Owned) { counts.TryGetValue(b.Level, out int c); counts[b.Level] = c + 1; }
            foreach (var (level, value, merge, owned) in listRows)
            {
                value.text = NumberFormat.Money(Balance.BallValue(tier, level));
                merge.text = level == 1
                    ? "Se compra con COMPRAR"
                    : "Fusionar 2 de " + BallMaterials.GetName(level - 1) + ": " + NumberFormat.Money(Balance.MergeCost(tier, level));
                owned.text = counts.TryGetValue(level, out int n) ? "Tienes " + n : "";
            }
            list.gameObject.SetActive(true);
            list.SetAsLastSibling();
        }
    }
}
