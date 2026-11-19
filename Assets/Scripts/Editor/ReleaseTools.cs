using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace Plinko.EditorTools
{
    /// <summary>
    /// Publicar una versión en un paso: sube el parche (versión + versionCode), compila el APK
    /// en Builds/ y crea la release en GitHub con el tag correcto usando GitHub CLI (gh).
    /// El auto-actualizador del móvil la detecta al abrir la app.
    /// Requiere haber iniciado sesión una vez con "gh auth login".
    /// </summary>
    static class ReleaseTools
    {
        const string Repo = "j-maestre/Plinko";
        const string GhPath = @"C:\Program Files\GitHub CLI\gh.exe";

        [MenuItem("Plinko/Publicar nueva versión", priority = 0)]
        static void PublishNewVersion()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Publicar", "Cambia antes la plataforma a Android (File → Build Profiles).", "OK");
                return;
            }

            string next = NextPatch(PlayerSettings.bundleVersion);
            if (!EditorUtility.DisplayDialog("Publicar nueva versión",
                    "Se va a:\n\n1. Subir la versión de " + PlayerSettings.bundleVersion + " a " + next +
                    "\n2. Compilar el APK\n3. Crear la release v" + next + " en GitHub\n\n¿Continuar?",
                    "Publicar", "Cancelar"))
                return;

            // 1) Versión
            PlayerSettings.bundleVersion = next;
            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();

            // 2) Build
            Directory.CreateDirectory("Builds");
            string apk = Path.Combine("Builds", "Plinko_v" + next + ".apk");
            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Publicar", "La compilación ha fallado (" + report.summary.result +
                    "). Mira la consola. La versión quedó en " + next + ".", "OK");
                return;
            }

            // 3) Release en GitHub
            string tag = "v" + next;
            string args = "release create " + tag + " \"" + Path.GetFullPath(apk) + "\" --repo " + Repo +
                          " --target main --title \"Plinko " + tag + "\" --generate-notes";
            EditorUtility.DisplayProgressBar("Publicar", "Subiendo " + tag + " a GitHub…", 0.5f);
            try
            {
                var (code, output) = Run(File.Exists(GhPath) ? GhPath : "gh", args);
                EditorUtility.ClearProgressBar();
                if (code == 0)
                {
                    Debug.Log("[Release] Publicada " + tag + ": " + output);
                    EditorUtility.DisplayDialog("Publicar", "Release " + tag + " publicada.\n\n" + output.Trim() +
                        "\n\nAbre la app en el móvil para que se actualice.", "OK");
                }
                else
                {
                    Debug.LogError("[Release] gh falló: " + output);
                    EditorUtility.DisplayDialog("Publicar", "El APK está en " + apk + " pero no se pudo crear la release:\n\n" + output, "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Publicar", "No se pudo ejecutar GitHub CLI: " + e.Message, "OK");
            }
        }

        static string NextPatch(string version)
        {
            var nums = new int[3];
            var parts = version.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out nums[i]);
            nums[2]++;
            return nums[0] + "." + nums[1] + "." + nums[2];
        }

        static (int code, string output) Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return (p.ExitCode, stdout + stderr);
            }
        }
    }
}
