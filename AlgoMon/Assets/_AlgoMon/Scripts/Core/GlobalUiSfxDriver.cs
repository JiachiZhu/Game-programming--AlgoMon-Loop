/*
Script Audit:
- Purpose: Plays UI hover/click sounds for every Selectable in every scene, with no per-button wiring.
- Attached GameObject: Added to the persistent AudioManager object in AudioManager.Awake.
- Main responsibilities: Raycast the EventSystem each frame; play Hover when the pointed-at Selectable changes, Click on pointer-down over an interactable control, and the Invalid glitch on pointer-down over a disabled control.
- Important variables: hoveredSelectable (last control under the pointer), raycastResults (reused buffer).
- Inputs: Legacy Input mouse position/button (project activeInputHandler = legacy), EventSystem raycasts.
- Outputs or effects: AudioManager.PlayUiSfx calls only; never alters UI state.
- AI/tutorial/template assistance: Drafted with AI assistance; event delivery semantics checked against Unity's EventSystem (topmost raycast hit receives pointer events).
- Testing notes: Hover/click serialized + dynamic buttons across MainTerminal/Grid/Arena; click a disabled button expecting the glitch; controls with SuppressUiClickSfx (zoom toggle) must stay silent on the generic click.
*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Global UI sound driver: instead of wiring every button (serialized, dynamic,
/// or third-party), it mirrors the EventSystem's own hit-testing. Only the
/// topmost raycast hit is considered — exactly the element that would receive
/// the pointer event — so a button covered by a modal overlay stays silent.
/// Clicking a disabled control plays UiSfx.Invalid (disabled Buttons raise no
/// onClick, so this is the only place that feedback can come from).
/// </summary>
[DisallowMultipleComponent]
public sealed class GlobalUiSfxDriver : MonoBehaviour
{
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private Selectable hoveredSelectable;

    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            hoveredSelectable = null;
            return;
        }

        Selectable underPointer = SelectableUnderPointer(eventSystem);

        if (!ReferenceEquals(underPointer, hoveredSelectable))
        {
            hoveredSelectable = underPointer;
            if (underPointer != null && underPointer.interactable)
                AudioManager.Instance?.PlayUiSfx(UiSfx.Hover);
        }

        if (Input.GetMouseButtonDown(0) && underPointer != null &&
            underPointer.GetComponent<SuppressUiClickSfx>() == null)
        {
            AudioManager.Instance?.PlayUiSfx(underPointer.interactable ? UiSfx.Click : UiSfx.Invalid);
        }
    }

    private Selectable SelectableUnderPointer(EventSystem eventSystem)
    {
        var pointer = new PointerEventData(eventSystem) { position = Input.mousePosition };
        raycastResults.Clear();
        eventSystem.RaycastAll(pointer, raycastResults);
        if (raycastResults.Count == 0)
            return null;

        // Topmost hit only — deeper elements never receive the pointer event.
        return raycastResults[0].gameObject.GetComponentInParent<Selectable>();
    }
}

/// <summary>
/// Marker for controls that play their own bespoke click sound (e.g. the
/// terminal-zoom toggle's high/low pair) — the generic click is skipped.
/// </summary>
[DisallowMultipleComponent]
public sealed class SuppressUiClickSfx : MonoBehaviour
{
}
