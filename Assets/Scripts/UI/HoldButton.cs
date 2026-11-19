using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>Botón que repite su acción mientras se mantiene pulsado (la acción decide su propio cooldown).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action OnHold;
        bool held;
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData e) { held = true; OnHold?.Invoke(); }
        public void OnPointerUp(PointerEventData e) => held = false;
        public void OnPointerExit(PointerEventData e) => held = false;

        void Update()
        {
            if (held && (selectable == null || selectable.interactable)) OnHold?.Invoke();
        }
    }
}
