using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 存档槽弹窗视图：绑定在 <c>Resources/UI/SaveSlotModal</c> 预制体根上，
/// 可在 Prefab 模式下调 RectTransform、字体、配色与 Layout。
/// </summary>
public class SaveSlotModalView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button[] slotButtons = new Button[4];
    [SerializeField] private TMP_Text[] slotLineTexts = new TMP_Text[4];
    [SerializeField] private Button backButton;
    [SerializeField] private Button dimCloseButton;

    private Action<int> onPickedSlot;
    private Action onCancel;

    public void Setup(Canvas parentCanvas, SaveSlotModalUI.PickMode mode, Action<int> onPicked, Action onCancelled)
    {
        onPickedSlot = onPicked;
        onCancel = onCancelled;

        MatchCanvasScalerFromParent(parentCanvas);

        if (titleText != null)
        {
            titleText.text = mode == SaveSlotModalUI.PickMode.NewGame
                ? "选择存档槽（新游戏）"
                : "选择存档槽（继续游戏）";
        }

        for (int i = 0; i < GameSaveService.SlotCount; i++)
        {
            BindSlotRow(i, mode);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Close);
        }

        if (dimCloseButton != null)
        {
            dimCloseButton.onClick.RemoveAllListeners();
            dimCloseButton.onClick.AddListener(Close);
        }
    }

    private void MatchCanvasScalerFromParent(Canvas parentCanvas)
    {
        CanvasScaler mine = GetComponent<CanvasScaler>();
        if (mine == null || parentCanvas == null)
        {
            return;
        }

        CanvasScaler parentScaler = parentCanvas.GetComponent<CanvasScaler>();
        if (parentScaler == null)
        {
            return;
        }

        mine.uiScaleMode = parentScaler.uiScaleMode;
        mine.referencePixelsPerUnit = parentScaler.referencePixelsPerUnit;
        mine.referenceResolution = parentScaler.referenceResolution;
        mine.screenMatchMode = parentScaler.screenMatchMode;
        mine.matchWidthOrHeight = parentScaler.matchWidthOrHeight;
    }

    private void BindSlotRow(int slot, SaveSlotModalUI.PickMode mode)
    {
        if (slot < 0 || slot >= GameSaveService.SlotCount)
        {
            return;
        }

        SaveGameData preview = GameSaveService.TryLoadSlot(slot);
        bool exists = preview != null;
        string line2 = exists
            ? $"存档时间 {new DateTime(preview.utcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm} · {preview.activeSceneName}"
            : "空槽";

        string line1 = mode == SaveSlotModalUI.PickMode.NewGame && exists
            ? $"槽位 {slot + 1}（将覆盖已有存档）"
            : $"槽位 {slot + 1}";

        TMP_Text label = slot < slotLineTexts.Length ? slotLineTexts[slot] : null;
        Button btn = slot < slotButtons.Length ? slotButtons[slot] : null;

        if (label != null)
        {
            if (mode == SaveSlotModalUI.PickMode.Continue && !exists)
            {
                label.text = $"{line1}\n<size=78%><color=#6A625C>{line2}</color></size>";
                label.color = new Color(0.5f, 0.48f, 0.45f);
            }
            else
            {
                label.text = string.IsNullOrEmpty(line2)
                    ? line1
                    : $"{line1}\n<size=78%><color=#C8B8A8>{line2}</color></size>";
                label.color = Color.white;
            }
        }

        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            bool canPick = mode != SaveSlotModalUI.PickMode.Continue || exists;
            btn.interactable = canPick;
            int captured = slot;
            if (canPick)
            {
                btn.onClick.AddListener(() => Pick(captured));
            }
        }
    }

    private void Pick(int slot)
    {
        onPickedSlot?.Invoke(slot);
        Destroy(transform.root.gameObject);
    }

    private void Close()
    {
        onCancel?.Invoke();
        Destroy(transform.root.gameObject);
    }

    private void OnValidate()
    {
        if (slotButtons != null && slotButtons.Length != 4)
        {
            Array.Resize(ref slotButtons, 4);
        }

        if (slotLineTexts != null && slotLineTexts.Length != 4)
        {
            Array.Resize(ref slotLineTexts, 4);
        }
    }
}
