using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfoPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text factText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button returnButton;

    [Header("Text")]
    [SerializeField] private string returnHint = "Press ESC to return to the main view";
    [SerializeField] private string returnButtonLabel = "Back to Space View";

    private void Awake()
    {
        AutoWireMissingReferences();

        if (returnButton != null)
        {
            TMP_Text buttonText = returnButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = returnButtonLabel;
            }
        }

        HideInfo();
    }

    public void ShowInfo(string bodyName, string fact)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        SetInfoElementsActiveForOrphans(true);

        if (titleText != null)
        {
            titleText.text = bodyName;
        }

        if (factText != null)
        {
            factText.text = fact;
        }

        if (hintText != null)
        {
            hintText.text = returnHint;
        }
    }

    public void HideInfo()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        SetInfoElementsActiveForOrphans(false);
    }

    private bool IsTransformUnderPanel(Transform t)
    {
        if (t == null || panelRoot == null)
        {
            return false;
        }

        return t == panelRoot.transform || t.IsChildOf(panelRoot.transform);
    }

    private void SetInfoElementsActiveForOrphans(bool isActive)
    {
        void SetIfOrphan(Component c)
        {
            if (c == null)
            {
                return;
            }

            if (IsTransformUnderPanel(c.transform))
            {
                return;
            }

            c.gameObject.SetActive(isActive);
        }

        SetIfOrphan(titleText);
        SetIfOrphan(factText);
        SetIfOrphan(hintText);
        SetIfOrphan(returnButton);
    }

    public void BindReturnButton(UnityEngine.Events.UnityAction onReturnRequested)
    {
        if (returnButton == null)
        {
            return;
        }

        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(onReturnRequested);
    }

    private void AutoWireMissingReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (returnButton == null)
        {
            returnButton = panelRoot.GetComponentInChildren<Button>(true);
        }
    }
}
