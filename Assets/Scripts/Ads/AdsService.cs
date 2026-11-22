using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;
#if PLINKO_LEVELPLAY && UNITY_ANDROID && !UNITY_EDITOR
using Unity.Services.LevelPlay;
#endif

namespace Plinko
{
    /// <summary>
    /// Anuncios recompensados (solo los que el jugador elige ver a cambio de un premio).
    /// En el móvil con el paquete LevelPlay (Ads Mediation) usa anuncios reales; en el editor o sin
    /// paquete, muestra un anuncio simulado de 2 segundos para poder probarlo todo.
    /// </summary>
    public class AdsService : MonoBehaviour
    {
        public const string LevelPlayAppKey = "29c3ebebd";
        const string RewardedAdUnitId = "muy6u8l1wu0c3mqa";   // "Rewarded_Android" en LevelPlay

        Action pendingReward, pendingFail;
        public bool Showing { get; private set; }

        static Sprite playIcon;
        /// <summary>Icono de "ver anuncio" (triángulo de play blanco).</summary>
        public static Sprite PlayIcon => playIcon != null ? playIcon : (playIcon = MakePlayIcon());

#if PLINKO_LEVELPLAY && UNITY_ANDROID && !UNITY_EDITOR
        LevelPlayRewardedAd ad;
        bool initStarted, initDone, rewarded, closedPending;
        float closedTimer;
        string lastError = "iniciando…";
        // los avisos de LevelPlay llegan desde el hilo de Java: se encolan y se procesan en Update
        readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();

        public void Init()
        {
            if (initStarted) return;
            initStarted = true;
            LevelPlay.OnInitSuccess += _ => mainThread.Enqueue(OnInitSuccess);
            LevelPlay.OnInitFailed += e => mainThread.Enqueue(() => { initStarted = false; lastError = "init " + e.ErrorCode + ": " + e.ErrorMessage; });
            LevelPlay.Init(LevelPlayAppKey);
        }

        void OnInitSuccess()
        {
            initDone = true;
            lastError = "cargando…";
            ad = new LevelPlayRewardedAd(RewardedAdUnitId);
            ad.OnAdLoaded += _ => mainThread.Enqueue(() => lastError = "");
            ad.OnAdLoadFailed += e => mainThread.Enqueue(() =>
            {
                lastError = "carga " + e.ErrorCode + ": " + e.ErrorMessage;
                Debug.LogWarning("[Ads] Load: " + e);
                CancelInvoke(nameof(Load));
                Invoke(nameof(Load), 10f);
            });
            ad.OnAdDisplayFailed += (_, e) => mainThread.Enqueue(() =>
            {
                Showing = false;
                lastError = "mostrar " + e.ErrorCode + ": " + e.ErrorMessage;
                GameManager.Instance?.UI.ShowBanner("Anuncio falló (" + lastError + ")", Color.gray);
                var f = pendingFail; pendingReward = pendingFail = null;
                f?.Invoke();
                Load();
            });
            ad.OnAdRewarded += (_, __) => mainThread.Enqueue(() =>
            {
                rewarded = true;
                if (closedPending) Finish();   // el premio llegó después de cerrar
            });
            ad.OnAdClosed += _ => mainThread.Enqueue(() =>
            {
                if (rewarded) { Finish(); return; }
                // algunas redes avisan del premio justo después de cerrar: se espera un momento
                closedPending = true;
                closedTimer = 1.5f;
            });
            Load();
        }

        public bool Ready => ad != null && ad.IsAdReady();

        public void ShowRewarded(Action onReward, Action onFail = null)
        {
            if (Showing) return;
            if (!Ready)
            {
                onFail?.Invoke();
                if (initDone) Load(); else Init();
                // diagnóstico visible: por qué no hay anuncio
                GameManager.Instance?.UI.ShowBanner("Anuncio no listo (" + lastError + ")", Color.gray);
                return;
            }
            pendingReward = onReward; pendingFail = onFail;
            Showing = true;
            rewarded = false; closedPending = false;
            ad.ShowAd();
        }

        void Load() { if (ad != null && !ad.IsAdReady()) ad.LoadAd(); }

        void Finish()
        {
            Showing = false;
            closedPending = false;
            var r = pendingReward; var f = pendingFail;
            pendingReward = pendingFail = null;
            if (rewarded) r?.Invoke(); else f?.Invoke();
            rewarded = false;
            Load();
        }

        void Update()
        {
            while (mainThread.TryDequeue(out var a)) a();
            if (closedPending)
            {
                closedTimer -= Time.unscaledDeltaTime;
                if (closedTimer <= 0) Finish();
            }
        }
#else
        // ---------------- Anuncio simulado (editor / sin paquete)
        RectTransform fake;
        Text fakeText;
        float fakeTime;

        public void Init() { }
        public bool Ready => true;

        public void ShowRewarded(Action onReward, Action onFail = null)
        {
            if (Showing) return;
            pendingReward = onReward; pendingFail = onFail;
            Showing = true;
            fakeTime = 2f;
            if (fake == null) BuildFake();
            fake.gameObject.SetActive(true);
        }

        void Update()
        {
            if (!Showing) return;
            fakeTime -= Time.unscaledDeltaTime;
            fakeText.text = "ANUNCIO DE PRUEBA\n<size=40>" + Mathf.CeilToInt(Mathf.Max(0, fakeTime)) + "</size>";
            if (fakeTime > 0) return;
            Showing = false;
            fake.gameObject.SetActive(false);
            var r = pendingReward; pendingReward = pendingFail = null;
            r?.Invoke();
        }

        void BuildFake()
        {
            var go = new GameObject("FakeAdCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            fake = UIFactory.Stretch("Fake", (RectTransform)go.transform);
            UIFactory.Image(fake, new Color(0, 0, 0, 0.95f), false);
            fakeText = UIFactory.Text(UIFactory.Stretch("Text", fake), "", 60, Color.white, TextAnchor.MiddleCenter, true);
            fake.gameObject.SetActive(false);
        }
#endif

        static Sprite MakePlayIcon()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // triángulo apuntando a la derecha con bordes suaves
                float fx = (x + 0.5f) / N, fy = (y + 0.5f) / N;
                float left = fx - 0.22f;
                float half = 0.5f * (1f - (fx - 0.22f) / 0.62f) * 0.72f;   // altura que se estrecha hacia la punta
                float d = Mathf.Min(left, half - Mathf.Abs(fy - 0.5f));
                float a = Mathf.Clamp01(d * N * 0.7f + 0.5f) * (fx < 0.84f ? 1f : 0f);
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }
    }
}
