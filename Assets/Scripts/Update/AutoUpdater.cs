using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// Auto-actualizador para builds de prueba en Android, sin preguntar:
    /// al arrancar (y cada 15 min) consulta la última release de GitHub; si su tag es más nuevo,
    /// descarga el APK en segundo plano y lo instala con PackageInstaller.
    ///
    /// - Android 12+: a partir de la primera actualización (cuando la app ya es su propia instaladora)
    ///   se instala sin confirmar. La primera vez, o en Android antiguos, el sistema pide pulsar "Actualizar".
    /// - Hace falta permitir una vez "Instalar apps desconocidas" para Plinko (Android 8+).
    /// - Se desactiva en instalaciones desde Google Play o con el define PLINKO_NO_UPDATER.
    /// - Para instalar encima: versionCode mayor y misma firma.
    /// </summary>
    public class AutoUpdater : MonoBehaviour
    {
        const string LatestReleaseUrl = "https://api.github.com/repos/j-maestre/Plinko/releases/latest";
        const string ApkFileName = "update.apk";
        const float FirstCheckDelay = 3f;
        const float CheckInterval = 15 * 60f;

#pragma warning disable 0649 // los rellena JsonUtility
        [Serializable] class Release { public string tag_name; public Asset[] assets; }
        [Serializable] class Asset { public string name; public string browser_download_url; }
#pragma warning restore 0649

        RectTransform pill;
        Text pillText;
        Button pillButton;
        string apkPath;
        bool busy, waitingForPermission;

#if UNITY_ANDROID && !UNITY_EDITOR && !PLINKO_NO_UPDATER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("AutoUpdater");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoUpdater>();
        }
#endif

        bool firstCheck = true;

        IEnumerator Start()
        {
            Debug.Log("[Updater] Iniciado. Versión instalada: " + Application.version);
            bool fromPlay = false;
            try { fromPlay = IsFromPlayStore(); } catch (Exception e) { Debug.LogWarning("[Updater] " + e); }
            if (fromPlay) { Destroy(gameObject); yield break; }
            BuildPill();
            yield return new WaitForSeconds(FirstCheckDelay);
            while (true)
            {
                if (!busy) yield return CheckAndUpdate();
                yield return new WaitForSeconds(CheckInterval);
            }
        }

        IEnumerator CheckAndUpdate()
        {
            string url = null, version = null;
            bool verbose = firstCheck;   // en la primera comprobación se informa de todo (fase de pruebas)
            firstCheck = false;
            if (verbose) ShowPill("Buscando actualizaciones… (tienes la " + Application.version + ")");
            using (var req = UnityWebRequest.Get(LatestReleaseUrl))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Updater] Error consultando GitHub: " + req.error);
                    if (verbose) ShowPill("No se pudo consultar GitHub: " + req.error, 6f);
                    yield break;
                }

                Release release = null;
                try { release = JsonUtility.FromJson<Release>(req.downloadHandler.text); }
                catch (Exception e) { Debug.LogWarning("[Updater] JSON: " + e.Message); }
                if (release == null || string.IsNullOrEmpty(release.tag_name))
                {
                    if (verbose) ShowPill("Respuesta de GitHub no válida", 5f);
                    yield break;
                }

                version = release.tag_name.TrimStart('v', 'V');
                Debug.Log("[Updater] Última release: " + version + " / instalada: " + Application.version);
                if (CompareVersions(version, Application.version) <= 0)
                {
                    if (verbose) ShowPill("Tienes la última versión (" + Application.version + ")", 3f);
                    yield break; // ya al día
                }
                if (release.assets != null)
                    foreach (var a in release.assets)
                        if (a.name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) { url = a.browser_download_url; break; }
            }
            if (string.IsNullOrEmpty(url))
            {
                ShowPill("La release v" + version + " no tiene APK adjunto", 5f);
                yield break;
            }

            busy = true;
            yield return Download(url, version);
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

        IEnumerator Download(string url, string version)
        {
            try
            {
                apkPath = Path.Combine(CallBridge<string>("cacheDir"), ApkFileName);
                if (File.Exists(apkPath)) File.Delete(apkPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Updater] " + e);
                ShowPill("Error preparando la descarga: " + e.Message, 8f);
                busy = false;
                yield break;
            }

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(apkPath) { removeFileOnAbort = true };
                req.SendWebRequest();
                while (!req.isDone)
                {
                    ShowPill("Descargando v" + version + "…  " + Mathf.FloorToInt(req.downloadProgress * 100) + "%");
                    yield return null;
                }
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Updater] Descarga fallida: " + req.error);
                    ShowPill("No se pudo descargar: " + req.error, 6f);
                    busy = false;
                    yield break;
                }
            }
            ShowPill("Instalando v" + version + "…");
            yield return new WaitForSeconds(0.5f);
            TryInstall();
        }

        void TryInstall()
        {
            bool canInstall;
            try { canInstall = CallBridge<bool>("canInstall"); }
            catch (Exception e) { ShowPill("Error comprobando permisos: " + e.Message, 8f); busy = false; return; }
            if (!canInstall)
            {
                // Android 8+: el usuario tiene que permitir una vez que Plinko instale actualizaciones
                waitingForPermission = true;
                ShowPill("Toca aquí y permite \"Instalar apps desconocidas\" para actualizar", 0, true);
                return;
            }
            waitingForPermission = false;
            if (GameManager.Instance != null) GameManager.Instance.Save(); // la instalación cierra la app

            if (IsXiaomi())
            {
                // HyperOS/MIUI bloquea las sesiones de instalación de apps normales: instalador clásico directamente
                try
                {
                    CallBridgeVoid("install", ApkFileName);
                    ShowPill("Confirma la instalación en la ventana del sistema", 5f);
                }
                catch (Exception e) { ShowPill("No se pudo abrir el instalador: " + e.Message, 8f); }
                busy = false;
                return;
            }

            try
            {
                CallBridgeVoid("installSession", apkPath);
                StartCoroutine(WatchForError());
            }
            catch (Exception e)
            {
                // si la sesión falla, se usa el instalador clásico (siempre pide confirmar)
                Debug.Log("[Updater] PackageInstaller falló, se usa el instalador clásico: " + e.Message);
                CallBridgeVoid("install", ApkFileName);
                HidePill();
                busy = false;
            }
        }

        static bool IsXiaomi()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string m = (build.GetStatic<string>("MANUFACTURER") + " " + build.GetStatic<string>("BRAND")).ToLowerInvariant();
                    return m.Contains("xiaomi") || m.Contains("redmi") || m.Contains("poco");
                }
            }
            catch { return false; }
#else
            return false;
#endif
        }

        IEnumerator WatchForError()
        {
            for (float t = 0; t < 30f; t += 1f)
            {
                yield return new WaitForSeconds(1f);
                string err = null;
                try { err = CallBridgeStatic<string>("takeLastError"); } catch { }
                if (!string.IsNullOrEmpty(err))
                {
                    // Algunas capas (Xiaomi/HyperOS) rechazan la instalación en segundo plano con
                    // "INSTALL_FAILED_ABORTED: Permission denied": se abre el instalador clásico del sistema,
                    // que se lanza en primer plano y pide confirmar.
                    Debug.Log("[Updater] La sesión falló (" + err + "), se usa el instalador clásico");
                    try
                    {
                        CallBridgeVoid("install", ApkFileName);
                        ShowPill("Confirma la instalación en la ventana del sistema", 5f);
                    }
                    catch (Exception e) { ShowPill("No se pudo instalar: " + err + " / " + e.Message, 8f); }
                    busy = false;
                    yield break;
                }
            }
            HidePill();
            busy = false;
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

        static T CallBridgeStatic<T>(string method)
        {
            using (var bridge = new AndroidJavaClass("com.plinko.updater.UpdaterBridge"))
                return bridge.CallStatic<T>(method);
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

        // ------------------------------------------------------------------ Aviso discreto

        void BuildPill()
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

            float y = BoardView.BottomUI + 0.07f;
            pill = UIFactory.Rect("UpdatePill", canvasGo.transform, new Vector2(0.1f, y), new Vector2(0.9f, y + 0.035f));
            var img = UIFactory.Image(pill, new Color(0.05f, 0.08f, 0.18f, 0.92f));
            pillButton = pill.gameObject.AddComponent<Button>();
            pillButton.targetGraphic = img;
            pillButton.onClick.AddListener(() => { if (waitingForPermission) CallBridgeVoid("openInstallPermission"); });
            pillText = UIFactory.Text(UIFactory.Inset(UIFactory.Stretch("Text", pill), 20, 0, 20, 0), "", 24, UIFactory.TextMain, TextAnchor.MiddleCenter);
            pillText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pill.gameObject.SetActive(false);
        }

        void ShowPill(string text, float hideAfter = 0, bool clickable = false)
        {
            pill.gameObject.SetActive(true);
            pillText.text = text;
            pillButton.interactable = clickable;
            pill.GetComponent<Image>().raycastTarget = clickable; // si no, no bloquea toques al juego
            CancelInvoke(nameof(HidePill));
            if (hideAfter > 0) Invoke(nameof(HidePill), hideAfter);
        }

        void HidePill() => pill.gameObject.SetActive(false);
    }
}
