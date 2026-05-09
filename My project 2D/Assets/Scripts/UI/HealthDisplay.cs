using TMPro;
using UnityEngine;

/// <summary>
/// Reads a target Health component every frame and shows its current HP in a TextMeshPro text.
/// Drag the Player's Health into "Health To Display" and a TMP text into "Display Text" in the Inspector.
/// </summary>
public class HealthDisplay : MonoBehaviour
{
    [Tooltip("The Health component to read from (drag the Player here).")]
    public Health healthToDisplay;

    [Tooltip("The TextMeshPro text that will show the HP value.")]
    public TextMeshProUGUI displayText;

    [Tooltip("Text shown before the number, e.g. \"HP: \".")]
    public string prefix = "HP: ";

    private void Update()
    {
        if (healthToDisplay == null || displayText == null) return;
        displayText.text = prefix + healthToDisplay.currentHealth;
    }
}
