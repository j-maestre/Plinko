using System;
using System.Collections.Generic;

namespace Plinko
{
    /// <summary>Una bola que posee el jugador. Cae en bucle: al llegar a un cubo vuelve a la cola de goteo.</summary>
    public class OwnedBall
    {
        public int Level = 1;
        public bool InFlight;
        public bool Removed;   // fusionada con otra: deja de existir
    }

    /// <summary>
    /// Bolas del jugador: comprar (nivel 1), fusionar las dos de mayor nivel en una del nivel siguiente
    /// y la cola de goteo que decide cuál cae a continuación.
    /// </summary>
    public class BallInventory
    {
        readonly GameManager gm;
        readonly List<OwnedBall> owned = new List<OwnedBall>();
        readonly LinkedList<OwnedBall> waiting = new LinkedList<OwnedBall>();

        public IReadOnlyList<OwnedBall> Owned => owned;
        public int Count => owned.Count;

        public BallInventory(GameManager gm) { this.gm = gm; }

        public void Load(List<int> levels)
        {
            owned.Clear();
            waiting.Clear();
            if (levels == null) return;
            foreach (var l in levels) Add(new OwnedBall { Level = Math.Clamp(l, 1, Balance.MaxBallLevel) });   // partidas viejas por encima del tope
            gm.State.maxLevel = Math.Min(gm.State.maxLevel, Balance.MaxBallLevel);
            gm.State.runMaxLevel = Math.Min(gm.State.runMaxLevel, Balance.MaxBallLevel);
            foreach (var b in owned) { if (b.Level > gm.State.maxLevel) gm.State.maxLevel = b.Level; if (b.Level > gm.State.runMaxLevel) gm.State.runMaxLevel = b.Level; }
            RaiseToBuyLevel();   // partidas guardadas con bolas por debajo del tramo actual
        }

        void Add(OwnedBall b)
        {
            owned.Add(b);
            waiting.AddLast(b);
        }

        public void SaveTo(GameState state)
        {
            state.balls.Clear();
            foreach (var b in owned) state.balls.Add(b.Level);
        }

        // --- Goteo ---

        public OwnedBall TakeNextWaiting()
        {
            while (waiting.Count > 0)
            {
                var b = waiting.First.Value;
                waiting.RemoveFirst();
                if (!b.Removed && !b.InFlight) return b;
            }
            return null;
        }

        /// <summary>Una bola ha terminado de caer: vuelve a la cola.</summary>
        public void ReturnToQueue(OwnedBall b)
        {
            if (!b.Removed && !waiting.Contains(b)) waiting.AddLast(b);
        }

        public int RandomLevel() => owned.Count == 0 ? 1 : owned[UnityEngine.Random.Range(0, owned.Count)].Level;

        public int HighestLevel()
        {
            int max = 1;
            foreach (var b in owned) if (b.Level > max) max = b.Level;
            return max;
        }

        // --- Comprar ---

        /// <summary>Nivel de las bolas que se compran ahora mismo.</summary>
        public int BuyLevel => Balance.BuyLevel(gm.State.runMaxLevel);
        public double BuyCost => Balance.BallCost(gm.State.ballsBought, BuyLevel);
        public bool HasFreeSlot => owned.Count < gm.Stats.MaxBalls;

        public bool TryBuy()
        {
            if (!HasFreeSlot || gm.State.money < BuyCost) return false;
            gm.State.money -= BuyCost;
            gm.State.ballsBought++;
            int level = BuyLevel;
            Add(new OwnedBall { Level = level });
            if (gm.State.maxLevel < level) gm.State.maxLevel = level;
            if (gm.State.runMaxLevel < level) gm.State.runMaxLevel = level;
            SaveTo(gm.State);
            return true;
        }

        // --- Fusionar ---

        /// <summary>Nivel de la siguiente fusión (el mayor que tenga al menos dos bolas), o 0 si no hay.</summary>
        public int MergeableLevel()
        {
            var counts = new Dictionary<int, int>();
            int best = 0;
            foreach (var b in owned)
            {
                if (b.Level >= Balance.MaxBallLevel) continue;   // las Estelares ya no se fusionan
                counts.TryGetValue(b.Level, out int c);
                counts[b.Level] = ++c;
                if (c >= 2 && b.Level > best) best = b.Level;
            }
            return best;
        }

        /// <summary>
        /// Fusiona dos bolas del mayor nivel posible. Devuelve la bola resultante y la que desaparece
        /// (para quitarlas del tablero si estaban cayendo).
        /// </summary>
        /// <summary>Precio de la siguiente fusión (0 si no hay pareja).</summary>
        public double MergeCost
        {
            get
            {
                int level = MergeableLevel();
                return level == 0 ? 0 : Balance.MergeCost(gm.Stats.TierIndex, level + 1);
            }
        }

        public bool TryMerge(out OwnedBall result, out OwnedBall consumed)
        {
            result = consumed = null;
            int level = MergeableLevel();
            if (level == 0) return false;
            double cost = Balance.MergeCost(gm.Stats.TierIndex, level + 1);
            if (gm.State.money < cost) return false;
            gm.State.money -= cost;
            foreach (var b in owned)
            {
                if (b.Level != level) continue;
                if (result == null) result = b;
                else { consumed = b; break; }
            }
            consumed.Removed = true;
            owned.Remove(consumed);
            waiting.Remove(consumed);
            waiting.Remove(result);
            result.Level++;
            if (result.Level > gm.State.maxLevel) gm.State.maxLevel = result.Level;
            if (result.Level > gm.State.runMaxLevel) gm.State.runMaxLevel = result.Level;
            LastRaised += RaiseToBuyLevel();
            SaveTo(gm.State);
            return true;
        }

        /// <summary>Bolas subidas automáticamente en la última subida de tramo (para el aviso; la UI lo pone a 0).</summary>
        public int LastRaised;

        /// <summary>Las bolas por debajo del nivel de compra suben a ese nivel. Devuelve cuántas han subido.</summary>
        int RaiseToBuyLevel()
        {
            int min = BuyLevel, n = 0;
            foreach (var b in owned)
                if (b.Level < min) { b.Level = min; n++; }
            return n;
        }
    }
}
