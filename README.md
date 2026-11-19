# Plinko

Juego incremental/idle para móvil (vertical) con mecánica de tablero de pinchos. Unity 6.3 LTS (6000.3.29f1), plantilla Universal 2D (URP).

## Cómo funciona

- Sin `Rigidbody`: el camino de cada bola se decide al lanzarla y se anima con arcos parabólicos entre pinchos.
- Ningún cubo multiplica por debajo de x1 y ninguna tirada cuesta dinero: no hay apuesta.
- Todo se crea por código al pulsar Play en `SampleScene` (`GameManager.Bootstrap`), sin prefabs ni assets de arte.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Assets/Scripts/Core` | Balance, mejoras, estadísticas, guardado, prestigio |
| `Assets/Scripts/Board` | Tablero, bolas, generación de caminos, fondo, sprites procedurales |
| `Assets/Scripts/Feel` | Partículas, sonido procedural, efectos de "punch" |
| `Assets/Scripts/UI` | Interfaz uGUI construida por código y panel de prestigio |
| `Assets/Scripts/Editor` | Ajuste automático a vista vertical 1080x1920 |

Los números de balance están en `Core/Balance.cs`, `Core/Upgrades.cs` y `Core/Prestige.cs`.

## Atajos de depuración (editor / development build)

- **Espacio**: lanzar bola
- **M**: duplicar dinero
- **E**: forzar una Avalancha
- Panel de prestigio → **BORRAR TODO (debug)**: empezar de cero
