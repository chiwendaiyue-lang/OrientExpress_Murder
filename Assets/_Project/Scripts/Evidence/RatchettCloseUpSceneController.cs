using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class RatchettCloseUpHotspot
{
    public string buttonObjectName;
    public string evidenceId;
    public string availableHint = "点击查看";
}

/// <summary>
/// 案发包厢二级近景（桌子 / 窗户 / 尸体等）：在场景里摆 Button 热点，Inspector 配置证据 id 与悬停文案。
/// </summary>
public class RatchettCloseUpSceneController : MonoBehaviour
{
    [SerializeField] private string cabinSceneName = SceneLoader.SCENE_CRIME_SCENE;
    [SerializeField] private string exhaustedHint = "没有什么值得注意的了";
    [SerializeField] private string backButtonObjectName = "Hotspot_Back_Button";
    [SerializeField] private string backButtonHint = "返回包厢";
    [SerializeField] private List<RatchettCloseUpHotspot> collectionHotspots = new List<RatchettCloseUpHotspot>();

    private readonly Dictionary<string, HoverHintTrigger> hintTriggers = new Dictionary<string, HoverHintTrigger>();

    private void Start()
    {
        foreach (RatchettCloseUpHotspot hotspot in collectionHotspots)
        {
            if (hotspot == null || string.IsNullOrEmpty(hotspot.buttonObjectName))
            {
                continue;
            }

            RatchettCloseUpHotspot captured = hotspot;
            HoverHintTrigger trigger = AutoBindButton(
                captured.buttonObjectName,
                () => OnCollectionHotspotClicked(captured),
                captured.availableHint);
            if (trigger != null)
            {
                hintTriggers[captured.buttonObjectName] = trigger;
            }
        }

        AutoBindButton(backButtonObjectName, OnBackToCabinClicked, backButtonHint, false);
        RefreshHintStates();
    }

    private void OnCollectionHotspotClicked(RatchettCloseUpHotspot hotspot)
    {
        if (hotspot == null || string.IsNullOrEmpty(hotspot.evidenceId))
        {
            return;
        }

        AddEvidence(hotspot.evidenceId);
        RatchettCrimeSceneProgress.TryMarkFinished();
        RefreshHintStates();
    }

    private void OnBackToCabinClicked()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(cabinSceneName);
        }
        else
        {
            SceneManager.LoadScene(cabinSceneName);
        }
    }

    private void AddEvidence(string evidenceId)
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        if (notebookManager != null)
        {
            notebookManager.AddEvidence(evidenceId);
        }
        else
        {
            Debug.LogWarning("RatchettCloseUpSceneController: DetectiveNotebookManager 不存在，无法记录侦探笔记。");
        }

        if (evidenceManager == null)
        {
            Debug.LogWarning("RatchettCloseUpSceneController: EvidenceManager 不存在，无法记录证据。");
        }
        else
        {
            evidenceManager.AddClue(evidenceId);
        }
    }

    private HoverHintTrigger AutoBindButton(string buttonName, UnityEngine.Events.UnityAction callback, string hint, bool required = true)
    {
        GameObject buttonObject = GameObject.Find(buttonName);
        if (buttonObject == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCloseUpSceneController: 找不到按钮 {buttonName}");
            }

            return null;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCloseUpSceneController: 对象 {buttonName} 没有 Button 组件");
            }

            return null;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);

        if (string.Equals(buttonName, backButtonObjectName, StringComparison.Ordinal))
        {
            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }
        }

        HoverHintTrigger trigger = buttonObject.GetComponent<HoverHintTrigger>();
        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<HoverHintTrigger>();
        }

        trigger.hint = hint;
        return trigger;
    }

    private void RefreshHintStates()
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        foreach (RatchettCloseUpHotspot hotspot in collectionHotspots)
        {
            if (hotspot == null || string.IsNullOrEmpty(hotspot.buttonObjectName))
            {
                continue;
            }

            if (!hintTriggers.TryGetValue(hotspot.buttonObjectName, out HoverHintTrigger trigger) || trigger == null)
            {
                continue;
            }

            bool done = HasEvidence(evidenceManager, notebookManager, hotspot.evidenceId);
            trigger.SetHint(done ? exhaustedHint : hotspot.availableHint);
        }
    }

    private static bool HasEvidence(EvidenceManager evidenceManager, DetectiveNotebookManager notebookManager, string evidenceId)
    {
        return (evidenceManager != null && evidenceManager.HasClue(evidenceId))
            || (notebookManager != null && notebookManager.HasEvidence(evidenceId));
    }
}
