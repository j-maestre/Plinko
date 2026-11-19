using UnityEditor;
using UnityEngine;

namespace Plinko.EditorTools
{
    /// <summary>
    /// Sube la versión antes de cada release: bundleVersion (lo que compara el auto-updater con el tag)
    /// y el versionCode de Android (tiene que crecer para que el APK se instale encima).
    /// </summary>
    static class VersionTools
    {
        [MenuItem("Plinko/Versión/Subir parche (x.y.Z)")]
        static void BumpPatch() => Bump(2);

        [MenuItem("Plinko/Versión/Subir menor (x.Y.0)")]
        static void BumpMinor() => Bump(1);

        [MenuItem("Plinko/Versión/Ver versión actual")]
        static void Show() =>
            EditorUtility.DisplayDialog("Versión", "Versión: " + PlayerSettings.bundleVersion +
                "\nversionCode Android: " + PlayerSettings.Android.bundleVersionCode +
                "\n\nTag de la release: v" + PlayerSettings.bundleVersion, "OK");

        static void Bump(int part)
        {
            var nums = new int[3];
            var parts = PlayerSettings.bundleVersion.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out nums[i]);
            nums[part]++;
            for (int i = part + 1; i < 3; i++) nums[i] = 0;

            PlayerSettings.bundleVersion = nums[0] + "." + nums[1] + "." + nums[2];
            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();
            Debug.Log("Versión " + PlayerSettings.bundleVersion + " (versionCode " + PlayerSettings.Android.bundleVersionCode +
                      "). Crea la release con el tag v" + PlayerSettings.bundleVersion);
            Show();
        }
    }
}
