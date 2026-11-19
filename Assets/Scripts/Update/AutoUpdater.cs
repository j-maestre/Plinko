using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// Auto-actualizador para builds de prueba en Android: consulta la última release de GitHub,
    /// y si hay una versión más nueva la descarga y abre el instalador del sistema.
    ///
    /// - Android no permite instalar en silencio: el usuario confirma en la pantalla del sistema.
    /// - Se desactiva si la app viene de Google Play (sus normas prohíben auto-actualizarse por fuera)
    ///   o si se define PLINKO_NO_UPDATER en los Scripting Define Symbols.
    /// - El repo debe ser público y la release tener un .apk adjunto; el tag es la versión (v1.2.0).
    /// - Para que se instale encima: versionCode mayor y misma firma (mismo keystore).
    /// </summary>
    public class AutoUpdater : MonoBehaviour
    {
        const string LatestReleaseUrl = "https://api.github.com/repos/j-maestre/Plinko/releases/latest";
        const string ApkFileName = "update.apk";

#pragma warning disable 0649 // los rellena JsonUtility
        [Serializable] class Release { public string tag_name; public string body; public Asset[] assets; }
        [Serializable] class Asset { public string name; public string browser_download_url; public long size; }
#pragma warning restore 0649

        RectTransform dialog;
        Text titleText, bodyText, progressText;
        Button acceptButton, laterButton;
        Text acceptLabel;
        string apkUrl, apkPath, newVersion;
        bool waitingForPermission;

#if UNITY_ANDROID && !UNITY_EDITOR && !PLINKO_NO_UPDATER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("AutoUpdater");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoUpdater>();
        }
#endif

        IEnumerator Start()
        {
            if (IsFromPlayStore()) { Destroy(gameObject); yield break; }
            yield return new WaitForSeconds(3f); // que el juego arranque primero

            using (var req = UnityWebRequest.Get(LatestReleaseUrl))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", "Plinko-Updater");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Debug.Log("[Updater] Sin respuesta: " + req.error); yield break; }

                Release release;
                try { release = JsonUtility.FromJson<Release>(req.downloadHandler.text); }
                catch (Exception e) { Debug.Log("[Updater] JSON no válido: " + e.Message); yield break; }
                if (release == null || string.IsNullOrEmpty(release.tag_name)) yield break;

                newVersion = release.tag_name.TrimStart('v', 'V');
                if (CompareVersions(newVersion, Application.version) <= 0) yield break; // ya actualizado

                if (release.assets != null)
                    foreach (var a in release.assets)
                        if (a.name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) { apkUrl = a.browser_download_url; break; }
                if (string.IsNullOrEmpty(apkUrl)) yield break;

                ShowDialog(release);
            }
        }

        /// <summary>Compara "1.2.10" con "1.2.9" numéricamente. &gt;0 si a es más nueva.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = a.Split('.', '-', '+');
            var pb = b.Split('.', '-', '+');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out var vx) ? vx : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out var vy) ? vy : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        // ------------------------------------------------------------------ Descarga e instalación

        IEnumerator Download()
        {
            acceptButton.interactable = false;
            laterButton.gameObject.SetActive(false);
            apkPath = Path.Combine(CallBridge<string>("cacheDir"), ApkFileName);
            if (File.Exists(apkPath)) File.Delete(apkPath);

            using (var req = new UnityWebRequest(apkUrl, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(apkPath) { removeFileOnAbort = true };
                req.SendWebRequest();
                while (!req.isDone)
                {
                    progressText.text = "Descargando… " + Mathf.FloorToInt(req.downloadProgress * 100) + "%";
                    yield return null;
                }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    progressText.text = "Error al descargar: " + req.error;
                    acceptButton.interactable = true;
                    acceptLabel.text = "Reintentar";
                    laterButton.gameObject.SetActive(true);
                    yield break;
                }
            }
            progressText.text = "Descarga completa";
            TryInstall();
        }

        void TryInstall()
        {
            if (!CallBridge<bool>("canInstall"))
            {
                // Android 8+: hay que permitir "instalar apps desconocidas" a este juego una vez
                waitingForPermission = true;
                progressText.text = "Permite \"Instalar apps desconocidas\" para Plinko y vuelve al juego.";
                CallBridgeVoid("openInstallPermission");
                return;
            }
            waitingForPermission = false;
            progressText.text = "Abriendo el instalador…";
            CallBridgeVoid("install", ApkFileName);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && waitingForPermission) TryInstall(); // vuelve de los ajustes
        }

        // ------------------------------------------------------------------ Puente con Java

        static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        static T CallBridge<T>(string method)
        {
            using (var bridge = new AndroidJavaClass("com.plinko.updater.UpdaterBridge"))
            using (var act = Activity())
                return bridge.CallStatic<T>(method, act);
        }

        static void CallBridgeVoid(string method, params object[] extra)
        {
            using (var bridge = new AndroidJavaClass("com.plinko.updater.UpdaterBridge"))
            using (var act = Activity())
            {
                var args = new object[extra.Length + 1];
                args[0] = act;
                Array.Copy(extra, 0, args, 1, extra.Length);
                bridge.CallStatic(method, args);
            }
        }

        static bool IsFromPlayStore()
        {
            try { return CallBridge<string>("installerPackage") == "com.android.vending"; }
            catch { return false; }
        }

        // ------------------------------------------------------------------ Diálogo

        void ShowDialog(Release release)
        {
            var canvasGo = new GameObject("UpdaterCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            var backdrop = UIFactory.Stretch("Backdrop", root);
            UIFactory.Image(backdrop, new Color(0, 0, 0.04f, 0.8f), false);

            dialog = UIFactory.Card("Dialog", root, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.68f), UIFactory.Panel, 10);
            titleText = UIFactory.Text(UIFactory.Rect("Title", dialog, new Vector2(0, 0.78f), new Vector2(1, 0.95f)),
                "Nueva versión " + release.tag_name, 44, UIFactory.Accent, TextAnchor.MiddleCenter, true);
            string notes = string.IsNullOrEmpty(release.body) ? "" : "\n\n" + Truncate(release.body, 220);
            bodyText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Body", dialog, new Vector2(0, 0.38f), new Vector2(1, 0.78f)), 40, 0, 40, 0),
                "Tienes la " + Application.version + ". ¿Descargar e instalar ahora?" + notes, 26, UIFactory.TextMain, TextAnchor.UpperCenter);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            progressText = UIFactory.Text(UIFactory.Inset(UIFactory.Rect("Progress", dialog, new Vector2(0, 0.27f), new Vector2(1, 0.38f)), 30, 0, 30, 0),
                "", 26, UIFactory.TextDim, TextAnchor.MiddleCenter);
            progressText.horizontalOverflow = HorizontalWrapMode.Wrap;

            laterButton = UIFactory.Button(UIFactory.Rect("Later", dialog, new Vector2(0.06f, 0.06f), new Vector2(0.44f, 0.24f)),
                UIFactory.CardDarkColor, out _, "Luego", 32);
            laterButton.onClick.AddListener(() => Destroy(gameObject));
            acceptButton = UIFactory.Button(UIFactory.Rect("Accept", dialog, new Vector2(0.52f, 0.06f), new Vector2(0.94f, 0.24f)),
                UIFactory.Accent, out acceptLabel, "Actualizar", 32);
            acceptButton.onClick.AddListener(() => StartCoroutine(Download()));
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
