using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Plinko.EditorTools
{
    /// <summary>
    /// Activa el símbolo PLINKO_LEVELPLAY cuando el paquete LevelPlay (Ads Mediation) está instalado, para que
    /// el código de anuncios reales solo se compile si el paquete existe (sin él, se usan anuncios simulados).
    /// También quita el símbolo antiguo PLINKO_ADS (del paquete Unity Ads, ya retirado).
    /// </summary>
    [InitializeOnLoad]
    static class AdsSetup
    {
        const string Symbol = "PLINKO_LEVELPLAY", Legacy = "PLINKO_ADS";

        static AdsSetup() => EditorApplication.delayCall += Apply;

        static void Apply()
        {
            bool hasPackage = Type.GetType("Unity.Services.LevelPlay.LevelPlay, Unity.LevelPlay") != null;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(target);
                if ((";" + defines + ";").Contains(";" + Legacy + ";"))
                {
                    defines = (";" + defines + ";").Replace(";" + Legacy + ";", ";").Trim(';');
                    PlayerSettings.SetScriptingDefineSymbols(target, defines);
                }
                bool has = (";" + defines + ";").Contains(";" + Symbol + ";");
                if (hasPackage && !has)
                    PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(defines) ? Symbol : defines + ";" + Symbol);
                else if (!hasPackage && has)
                    PlayerSettings.SetScriptingDefineSymbols(target, (";" + defines + ";").Replace(";" + Symbol + ";", ";").Trim(';'));
            }
        }
    }
}
