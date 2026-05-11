using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class CharacterStageSlot
{
    public string slotId;
    public Image image;
    public CanvasGroup canvasGroup;
    public RectTransform rectTransform;
    public float hiddenOffsetX = -60f;
    public float shownOffsetX = 0f;
}

public class CharacterStageController : MonoBehaviour
{
    [SerializeField] private List<CharacterStageSlot> slots = new List<CharacterStageSlot>();
    [SerializeField] private float defaultDuration = 0.25f;
    [SerializeField] private float dimmedAlpha = 0.45f;
    [SerializeField] private float focusAlpha = 1f;

    private readonly Dictionary<string, Coroutine> runningCoroutines = new Dictionary<string, Coroutine>();
    private readonly Dictionary<string, CharacterStageSlot> slotLookup = new Dictionary<string, CharacterStageSlot>();
    private readonly Dictionary<string, string> currentCharacterBySlot = new Dictionary<string, string>();

    void Awake()
    {
        slotLookup.Clear();
        foreach (CharacterStageSlot slot in slots)
        {
            if (slot == null || string.IsNullOrEmpty(slot.slotId))
            {
                continue;
            }

            if (slot.image == null)
            {
                continue;
            }

            if (slot.canvasGroup == null)
            {
                slot.canvasGroup = slot.image.GetComponent<CanvasGroup>();
                if (slot.canvasGroup == null)
                {
                    slot.canvasGroup = slot.image.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (slot.rectTransform == null)
            {
                slot.rectTransform = slot.image.rectTransform;
            }

            slot.canvasGroup.alpha = 0f;
            slot.image.enabled = false;
            slotLookup[slot.slotId] = slot;
            currentCharacterBySlot[slot.slotId] = string.Empty;
        }
    }

    public void ApplyStage(DialogueStage stage, string speakerCharacterId)
    {
        if (stage == null)
        {
            return;
        }

        string focusCharacterId = !string.IsNullOrEmpty(stage.focusCharacterId)
            ? stage.focusCharacterId
            : speakerCharacterId;
        string focusSlotId = GetFocusSlotId(stage, focusCharacterId);

        foreach (CharacterStageSlot slot in slots)
        {
            if (slot == null || string.IsNullOrEmpty(slot.slotId) || slot.image == null)
            {
                continue;
            }

            DialogueStageSlot stageSlot = GetStageSlot(stage, slot.slotId);
            if (stageSlot == null)
            {
                Hide(slot, defaultDuration);
                currentCharacterBySlot[slot.slotId] = string.Empty;
                continue;
            }

            if (string.IsNullOrEmpty(stageSlot.portrait))
            {
                Debug.LogWarning($"CharacterStageController: stage 槽位 {slot.slotId} 缺少 portrait");
                Hide(slot, defaultDuration);
                currentCharacterBySlot[slot.slotId] = string.Empty;
                continue;
            }

            float targetAlpha = string.IsNullOrEmpty(focusSlotId) || slot.slotId == focusSlotId
                ? focusAlpha
                : dimmedAlpha;

            Show(slot, stageSlot.portrait, defaultDuration, targetAlpha);
            currentCharacterBySlot[slot.slotId] = stageSlot.characterId;
        }
    }

    public void ExecuteCommands(List<StageCommand> commands)
    {
        if (commands == null)
        {
            return;
        }

        foreach (StageCommand command in commands)
        {
            if (command == null || string.IsNullOrEmpty(command.action))
            {
                continue;
            }

            string action = command.action.ToLowerInvariant();
            float duration = command.duration > 0f ? command.duration : defaultDuration;

            if (action == "hideall")
            {
                HideAll(duration);
                continue;
            }

            if (action == "clearfocus")
            {
                ClearFocus(duration);
                continue;
            }

            if (string.IsNullOrEmpty(command.slotId))
            {
                continue;
            }

            if (!slotLookup.TryGetValue(command.slotId, out CharacterStageSlot slot))
            {
                Debug.LogWarning($"CharacterStageController: 未找到槽位 {command.slotId}");
                continue;
            }

            if (action == "show")
            {
                Show(slot, command.portrait, duration);
            }
            else if (action == "hide")
            {
                Hide(slot, duration);
            }
            else if (action == "focus")
            {
                FocusSlot(command.slotId, duration);
            }
        }
    }

    public void HideAll(float duration = -1f)
    {
        float d = duration > 0f ? duration : defaultDuration;
        foreach (CharacterStageSlot slot in slots)
        {
            if (slot == null || slot.image == null)
            {
                continue;
            }

            Hide(slot, d);
        }
    }

    public void FocusSlot(string slotId, float duration = -1f)
    {
        if (string.IsNullOrEmpty(slotId))
        {
            return;
        }

        float d = duration > 0f ? duration : defaultDuration;
        foreach (CharacterStageSlot slot in slots)
        {
            if (slot == null || slot.image == null || slot.canvasGroup == null || !slot.image.enabled)
            {
                continue;
            }

            float target = slot.slotId == slotId ? focusAlpha : dimmedAlpha;
            StartAlphaOnlyAnimation(slot, target, d);
        }
    }

    public void ClearFocus(float duration = -1f)
    {
        float d = duration > 0f ? duration : defaultDuration;
        foreach (CharacterStageSlot slot in slots)
        {
            if (slot == null || slot.image == null || slot.canvasGroup == null || !slot.image.enabled)
            {
                continue;
            }

            StartAlphaOnlyAnimation(slot, focusAlpha, d);
        }
    }

    private void Show(CharacterStageSlot slot, string portraitName, float duration)
    {
        Show(slot, portraitName, duration, focusAlpha);
    }

    private void Show(CharacterStageSlot slot, string portraitName, float duration, float targetAlpha)
    {
        if (string.IsNullOrEmpty(portraitName))
        {
            Debug.LogWarning($"CharacterStageController: show 缺少 portrait，槽位 {slot.slotId}");
            return;
        }

        Sprite portrait = Resources.Load<Sprite>($"Characters/{portraitName}");
        if (portrait == null)
        {
            Debug.LogWarning($"CharacterStageController: 找不到立绘 Characters/{portraitName}");
            return;
        }

        bool wasVisible = slot.image.enabled && slot.canvasGroup != null && slot.canvasGroup.alpha > 0.01f;
        bool isSamePortrait = slot.image.sprite == portrait;

        if (wasVisible)
        {
            if (isSamePortrait)
            {
                // 同一张立绘重复 show 时，不重复播放入场动画
                StartAlphaOnlyAnimation(slot, targetAlpha, duration * 0.5f);
                return;
            }

            // 同槽位切情绪：用淡变替代重新滑入，观感更自然
            StartSwapAnimation(slot, portrait, duration, targetAlpha);
            return;
        }

        slot.image.sprite = portrait;
        slot.image.enabled = true;
        StartSlotAnimation(slot, 0f, targetAlpha, slot.hiddenOffsetX, slot.shownOffsetX, duration, false);
    }

    private void Hide(CharacterStageSlot slot, float duration)
    {
        StartSlotAnimation(slot, slot.canvasGroup.alpha, 0f, slot.shownOffsetX, slot.hiddenOffsetX, duration, true);
        if (slot != null && !string.IsNullOrEmpty(slot.slotId))
        {
            currentCharacterBySlot[slot.slotId] = string.Empty;
        }
    }

    private void StartAlphaOnlyAnimation(CharacterStageSlot slot, float targetAlpha, float duration)
    {
        if (runningCoroutines.TryGetValue(slot.slotId, out Coroutine oldRoutine) && oldRoutine != null)
        {
            StopCoroutine(oldRoutine);
        }

        Coroutine routine = StartCoroutine(AnimateAlpha(slot, slot.canvasGroup.alpha, targetAlpha, duration));
        runningCoroutines[slot.slotId] = routine;
    }

    private void StartSwapAnimation(CharacterStageSlot slot, Sprite targetSprite, float duration)
    {
        StartSwapAnimation(slot, targetSprite, duration, focusAlpha);
    }

    private void StartSwapAnimation(CharacterStageSlot slot, Sprite targetSprite, float duration, float targetAlpha)
    {
        if (runningCoroutines.TryGetValue(slot.slotId, out Coroutine oldRoutine) && oldRoutine != null)
        {
            StopCoroutine(oldRoutine);
        }

        Coroutine routine = StartCoroutine(AnimateSwap(slot, targetSprite, duration, targetAlpha));
        runningCoroutines[slot.slotId] = routine;
    }

    private void StartSlotAnimation(CharacterStageSlot slot, float fromAlpha, float toAlpha, float fromX, float toX, float duration, bool disableImageWhenDone)
    {
        if (runningCoroutines.TryGetValue(slot.slotId, out Coroutine oldRoutine) && oldRoutine != null)
        {
            StopCoroutine(oldRoutine);
        }

        Coroutine routine = StartCoroutine(AnimateSlot(slot, fromAlpha, toAlpha, fromX, toX, duration, disableImageWhenDone));
        runningCoroutines[slot.slotId] = routine;
    }

    private IEnumerator AnimateSlot(CharacterStageSlot slot, float fromAlpha, float toAlpha, float fromX, float toX, float duration, bool disableImageWhenDone)
    {
        if (slot.canvasGroup == null || slot.rectTransform == null)
        {
            yield break;
        }

        Vector2 startPos = slot.rectTransform.anchoredPosition;
        startPos.x = fromX;
        Vector2 endPos = slot.rectTransform.anchoredPosition;
        endPos.x = toX;

        slot.rectTransform.anchoredPosition = startPos;
        slot.canvasGroup.alpha = fromAlpha;

        if (duration <= 0f)
        {
            slot.canvasGroup.alpha = toAlpha;
            slot.rectTransform.anchoredPosition = endPos;
        }
        else
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = Mathf.SmoothStep(0f, 1f, p);
                slot.canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                slot.rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                yield return null;
            }
        }

        if (disableImageWhenDone && slot.image != null)
        {
            slot.image.enabled = false;
        }
    }

    private IEnumerator AnimateAlpha(CharacterStageSlot slot, float fromAlpha, float toAlpha, float duration)
    {
        if (slot.canvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            slot.canvasGroup.alpha = toAlpha;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = Mathf.SmoothStep(0f, 1f, p);
            slot.canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            yield return null;
        }
    }

    private IEnumerator AnimateSwap(CharacterStageSlot slot, Sprite targetSprite, float duration, float targetAlpha)
    {
        if (slot.canvasGroup == null || slot.image == null)
        {
            yield break;
        }

        float d = Mathf.Max(0.05f, duration);
        float half = d * 0.5f;
        float fromAlpha = slot.canvasGroup.alpha;
        float midAlpha = Mathf.Min(fromAlpha, 0.2f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = Mathf.SmoothStep(0f, 1f, p);
            slot.canvasGroup.alpha = Mathf.Lerp(fromAlpha, midAlpha, eased);
            yield return null;
        }

        slot.image.sprite = targetSprite;

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = Mathf.SmoothStep(0f, 1f, p);
            slot.canvasGroup.alpha = Mathf.Lerp(midAlpha, targetAlpha, eased);
            yield return null;
        }
    }

    private DialogueStageSlot GetStageSlot(DialogueStage stage, string slotId)
    {
        if (stage == null || stage.slots == null || string.IsNullOrEmpty(slotId))
        {
            return null;
        }

        foreach (DialogueStageSlot slot in stage.slots)
        {
            if (slot != null && slot.slotId == slotId)
            {
                return slot;
            }
        }

        return null;
    }

    private string GetFocusSlotId(DialogueStage stage, string focusCharacterId)
    {
        if (stage == null || stage.slots == null || string.IsNullOrEmpty(focusCharacterId))
        {
            return string.Empty;
        }

        foreach (DialogueStageSlot slot in stage.slots)
        {
            if (slot != null && slot.characterId == focusCharacterId)
            {
                return slot.slotId;
            }
        }

        return string.Empty;
    }
}
