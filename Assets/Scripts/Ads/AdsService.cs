using System;
using UnityEngine;
using UnityEngine.UI;
#if PLINKO_ADS && UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Advertisements;
#endif

namespace Plinko
{
    /// <summary>
    /// Anuncios recompensados (solo los que el jugador elige ver a cambio de un premio).
    /// En el móvil con el paquete de Unity Ads usa anuncios reales; en el editor o sin paquete,
    /// muestra un anuncio simulado de 2 segundos para poder probarlo todo.
    /// </summary>
    public class AdsService : MonoBehaviour
#if PLINKO_ADS && UNITY_ANDROID && !UNITY_EDITOR
        , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
    {
        public const string AndroidGameId = "800447745";
        const string RewardedUnit = "Rewarded_Android";   // placement sin pujas ("BP_" exige adMarkup de LevelPlay)
        /// <summary>Anuncios de prueba (no generan dinero). Ponlo a false al publicar en serio.</summary>
        public const bool TestMode = true;

        Action pendingReward, pendingFail;
        public bool Showing { get; private set; }

        static Sprite playIcon;
        /// <summary>Icono de "ver anuncio" (triángulo de play blanco).</summary>
        public static Sprite PlayIcon => playIcon != null ? playIcon : (playIcon = MakePlayIcon());

#if PLINKO_ADS && UNITY_ANDROID && !UNITY_EDITOR
        bool loaded;
        string lastError = "iniciando…";

        public void Init()
        {
            if (!Advertisement.isSupported) { lastError = "no soportado"; return; }
            if (!Advertisement.isInitialized) Advertisement.Initialize(AndroidGameId, TestMode, this);
        }

        public bool Ready => loaded;

        public void ShowRewarded(Action onReward, Action onFail = null)
        {
            if (Showing) return;
            if (!loaded)
            {
                onFail?.Invoke();
                if (Advertisement.isInitialized) Load(); else Init();
                // diagnóstico visible: por qué no hay anuncio
                GameManager.Instance?.UI.ShowBanner("Anuncio no listo (" + lastError + ")", Color.gray);
                return;
            }
            pendingReward = onReward; pendingFail = onFail;
            Showing = true;
            loaded = false;
            Advertisement.Show(RewardedUnit, this);
        }

        void Load() => Advertisement.Load(RewardedUnit, this);

        public void OnInitializationComplete() { lastError = "cargando…"; Load(); }
        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            lastError = "init " + error + ": " + message;
            Debug.LogWarning("[Ads] Init: " + message);
        }
        public void OnUnityAdsAdLoaded(string placementId) { loaded = true; lastError = ""; }
        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            lastError = "carga " + error + ": " + message;
            Debug.LogWarning("[Ads] Load: " + message);
            Invoke(nameof(Load), 10f);
        }
        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            Showing = false;
            lastError = "mostrar " + error + ": " + message;
            GameManager.Instance?.UI.ShowBanner("Anuncio falló (" + lastError + ")", Color.gray);
            var f = pendingFail; pendingReward = pendingFail = null;
            f?.Invoke();
            Load();
        }
        public void OnUnityAdsShowStart(string placementId) { }
        public void OnUnityAdsShowClick(string placementId) { }
        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
        {
            Showing = false;
            var r = pendingReward; var f = pendingFail;
            pendingReward = pendingFail = null;
            if (state == UnityAdsShowCompletionState.COMPLETED) r?.Invoke(); else f?.Invoke();
            Load();
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
