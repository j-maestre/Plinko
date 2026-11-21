using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Plinko.EditorTools
{
    /// <summary>
    /// Activa el símbolo PLINKO_ADS cuando el paquete de Unity Ads está instalado, para que el código
    /// de anuncios reales solo se compile si el paquete existe (sin él, se usan anuncios simulados).
    /// </summary>
    [InitializeOnLoad]
    static class AdsSetup
    {
        const string Symbol = "PLINKO_ADS";

        static AdsSetup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            bool hasPackage = Type.GetType("UnityEngine.Advertisements.Advertisement, UnityEngine.Advertisements") != null;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                bool has = (";" + defines + ";").Contains(";" + Symbol + ";");
                if (hasPackage && !has)
                    PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(defines) ? Symbol : defines + ";" + Symbol);
                else if (!hasPackage && has)
                    PlayerSettings.SetScriptingDefineSymbols(target, (";" + defines + ";").Replace(";" + Symbol + ";", ";").Trim(';'));
            }
        }
    }
}
