using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class DialogueOption
{
    public string text;
    public string nextNodeId;
    public string nextDialogueId;
    public string nextSceneName;

    // New notebook flow fields.
    public NotebookRequirements requirements;
    public NotebookRewards rewards;

    // Legacy clue fields. Keep them until old dialogue JSON is migrated.
    public string clueToAdd;
    public string requiredClueId;
}

[Serializable]
public class DialogueNode
{
    public string nodeType;
    public string speakerCharacterId;
    public string speakerName;
    public string speaker;
    public string text;
    public string portrait;
    // 当 options 为空时，点击对话框后跳转到该节点（可填 END）
    public string nextNodeId;
    public string nextDialogueId;
    public string nextSceneName;
    public DialogueStage stage;
    public PresentedImageData presentedImage;
    public List<DialogueHotspot> hotspots;
    public NotebookRewards rewards;
    // notice 节点使用：portrait / dialogue_text / presented_image
    public string targetType;
    public string prompt;
    // 当前说话者所在槽位（left/center/right），用于高亮
    public string focusSlotId;
    public List<DialogueOption> options;
    public List<StageCommand> stageCommands;

    /// <summary>为 true 时：本节点笔记奖励延迟到玩家在对话中右键收录；左键继续则跳过（弹丸式）。</summary>
    public bool dialogueDeferNotebookRewards;

    /// <summary>为 true 时：即使有奖励也不延迟（覆盖 Inspector 默认延迟）。</summary>
    public bool dialogueSkipDeferNotebookRewards;

    /// <summary>非空时：在正文中首次出现处包一层 &lt;b&gt;…&lt;/b&gt;（需 TMP 开启 richText）。</summary>
    public string dialogueHighlightPhrase;

    /// <summary>延迟收录时，覆盖默认的右键/左键提示行（纯文本，会包在 TMP 颜色标签外由代码拼接）。</summary>
    public string dialogueRewardInteractHint;
}

[Serializable]
public class DialogueData
{
    public string sceneId;
    public string dialogueId;
    public string dialogueName;
    public string characterId;
    public string characterName;
    public string defaultPortrait;
    public string startNodeId;

    /// <summary>
    /// 主线等非「人名对话」资源：有待延迟收录的笔记奖励时，禁止左键跳过，须右键收录后方可继续。
    /// </summary>
    public bool mainStoryBlockDeferredRewardSkip;

    /// <summary>
    /// 可选：本段对话开始时切换场景中 <see cref="DialogueSceneBackdropBinder"/> 绑定的背景（见该脚本说明）。
    /// </summary>
    public string backdropImageId;

    public List<DialogueNodeEntry> nodes;

    [NonSerialized] public Dictionary<string, DialogueNode> nodeLookup;

    public void BuildLookup()
    {
        nodeLookup = new Dictionary<string, DialogueNode>();

        if (nodes == null)
        {
            return;
        }

        foreach (DialogueNodeEntry entry in nodes)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || entry.node == null)
            {
                continue;
            }

            nodeLookup[entry.id] = entry.node;
        }
    }
}

[Serializable]
public class DialogueNodeEntry
{
    public string id;
    public DialogueNode node;
}

[Serializable]
public class DialogueStage
{
    public string focusCharacterId;
    public List<DialogueStageSlot> slots;
}

[Serializable]
public class DialogueStageSlot
{
    public string slotId;
    public string characterId;
    public string portrait;
}

[Serializable]
public class PresentedImageData
{
    public string imageId;
    public string position;

    public bool HasImage()
    {
        return !string.IsNullOrEmpty(imageId);
    }
}

[Serializable]
public class DialogueHotspot
{
    public string id;
    public string targetType;
    public string targetId;
    public string label;
    public bool correct;
    public string successNodeId;
    public string failureNodeId;
    public float x;
    public float y;
    public float width;
    public float height;
}

[Serializable]
public class StageCommand
{
    // show / hide / hideAll / focus / clearFocus
    public string action;
    // 对应 CharacterStageController 里 slotId
    public string slotId;
    // action=show 时使用，Resources/Characters 下的图片名
    public string portrait;
    // 可选，覆盖默认动画时长
    public float duration;
}
