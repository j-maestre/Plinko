using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Plinko.EditorTools
{
    /// <summary>
    /// Deja el proyecto en vertical: orientación Portrait en Player Settings y
    /// la vista Game en 1080x1920. Se aplica una vez por sesión del editor.
    /// </summary>
    [InitializeOnLoad]
    static class PortraitSetup
    {
        const int Width = 1080, Height = 1920;
        const string Label = "Plinko vertical";

        static PortraitSetup()
        {
            if (SessionState.GetBool("Plinko.PortraitSetup", false)) return;
            SessionState.SetBool("Plinko.PortraitSetup", true);
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Plinko/Vista vertical 1080x1920")]
        static void Apply()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            try { SelectGameViewSize(); }
            catch (Exception e)
            {
                Debug.Log("No se pudo fijar la resolución de la vista Game automáticamente (" + e.Message +
                          "). Elige 1080x1920 en el desplegable de la vista Game.");
            }
        }

        // La API de tamaños de la vista Game es interna: se usa reflexión.
        static void SelectGameViewSize()
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            var groupType = group.GetType();

            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize");
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                var s = getSize.Invoke(group, new object[] { i });
                var st = s.GetType();
                if ((int)st.GetProperty("width").GetValue(s) == Width &&
                    (int)st.GetProperty("height").GetValue(s) == Height) { index = i; break; }
            }

            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new[] { Enum.Parse(enumType, "FixedResolution"), Width, Height, (object)Label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = count;
            }

            var gameViewType = asm.GetType("UnityEditor.GameView");
            var views = Resources.FindObjectsOfTypeAll(gameViewType);
            var prop = gameViewType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var v in views.Cast<EditorWindow>())
            {
                prop.SetValue(v, index);
                v.Repaint();
            }
        }
    }
}
