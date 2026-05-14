using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class InterrogationEntryConfig
{
    public string id;
    public string displayName;
    public Button button;
    public TMP_Text label;
    public string targetSceneName;
    public string requiredClueId;
    public bool hideIfLocked;
    public UnityEvent onSelected;
}

public class InterrogationPanelController : MonoBehaviour
{
    [SerializeField] private List<InterrogationEntryConfig> entries = new List<InterrogationEntryConfig>();
    [SerializeField] private TMP_Text lockedHintText;
    [SerializeField] private string defaultLockedHint = "当前没有可审问对象。";

    void OnEnable()
    {
        RefreshEntries();
    }

    public void RefreshEntries()
    {
        bool hasAnyAvailable = false;

        foreach (InterrogationEntryConfig entry in entries)
        {
            if (entry == null || entry.button == null)
            {
                continue;
            }

            bool unlocked = IsEntryUnlocked(entry);
            hasAnyAvailable |= unlocked;

            if (entry.hideIfLocked)
            {
                entry.button.gameObject.SetActive(unlocked);
            }
            else
            {
                entry.button.gameObject.SetActive(true);
                entry.button.interactable = unlocked;
            }

            if (entry.label != null)
            {
                string baseName = string.IsNullOrEmpty(entry.displayName) ? entry.button.gameObject.name : entry.displayName;
                entry.label.text = unlocked ? baseName : $"{baseName}（未解锁）";
            }

            entry.button.onClick.RemoveAllListeners();
            entry.button.onClick.AddListener(() => OnEntryClicked(entry));
        }

        if (lockedHintText != null)
        {
            lockedHintText.text = hasAnyAvailable ? string.Empty : defaultLockedHint;
            lockedHintText.gameObject.SetActive(!hasAnyAvailable && !string.IsNullOrEmpty(defaultLockedHint));
        }
    }

    private bool IsEntryUnlocked(InterrogationEntryConfig entry)
    {
        if (string.IsNullOrEmpty(entry.requiredClueId))
        {
            return true;
        }

        return EvidenceManager.Instance != null && EvidenceManager.Instance.HasClue(entry.requiredClueId);
    }

    private void OnEntryClicked(InterrogationEntryConfig entry)
    {
        if (!IsEntryUnlocked(entry))
        {
            return;
        }

        entry.onSelected?.Invoke();

        if (!string.IsNullOrEmpty(entry.targetSceneName))
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(entry.targetSceneName);
            }
            else
            {
                EvidencePanelUI.DisableAllEventSystemComponentsBeforeSceneLoad();
                SceneManager.LoadScene(entry.targetSceneName);
                EvidencePanelUI.EnsureSingleEventSystem();
            }
        }
    }
}
