using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Plinko
{
    [Serializable]
    public class UpgradeEntry
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class GameState
    {
        public const int CurrentVersion = 2; // subir al cambiar el balance de forma incompatible
        public int version = CurrentVersion;
        public double money = Balance.FirstBallCost;   // justo para comprar la primera bola
        public double totalEarned;
        public double bestDrop;   // mayor premio de una sola bola
        public float overload;    // carga del medidor de Sobrecarga (0..1)

        // --- Prestigio (no se pierde al reiniciar) ---
        public double runEarned;  // ganado en esta partida (decide los lingotes)
        public double cores;      // lingotes de oro sin gastar (moneda de la tienda de prestigio)
        public int prestigeCount;
        public List<UpgradeEntry> prestigeUpgrades = new List<UpgradeEntry>();
        public List<string> unlockedTabs = new List<string>(); // secciones compradas (permanentes)

        // --- Bolas del jugador (niveles) ---
        public List<int> balls = new List<int>();   // se empieza sin bolas: hay que comprar la primera
        public int ballsBought;
        public bool tutorialDone;
        public int tutorialStep;   // 0 comprar, 1 fusionar, 2 primera mejora, 3 terminado
        public int maxLevel;
        public int runMaxLevel;    // mejor bola de esta partida (decide de qué nivel se compran)       // mejor bola conseguida nunca (no se pierde con el prestigio)
        public List<UpgradeEntry> upgrades = new List<UpgradeEntry>();
        public long lastSaveUnix;
    }

    public static class SaveSystem
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static GameState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var state = JsonUtility.FromJson<GameState>(File.ReadAllText(FilePath));
                    if (state != null && state.version == GameState.CurrentVersion) return state;
                    if (state != null) Debug.Log("Partida de una versión anterior del balance: se empieza de cero.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo cargar la partida: " + e.Message);
            }
            return new GameState();
        }

        public static void Save(GameState state)
        {
            try
            {
                state.lastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(state));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("No se pudo guardar la partida: " + e.Message);
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
