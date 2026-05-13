# 东方快车谋杀案 2D 文字推理游戏原型

这是一个基于 Unity 的 2D 文字推理游戏原型，题材取自阿加莎·克里斯蒂《东方快车谋杀案》。玩法目标参考《山河旅探》《大逆转裁判》一类文字推理体验：玩家通过调查、对话、观察异常和阶段性推理，逐步接近案件真相。

## 当前工程重点

项目正在从旧的笼统 `clue` 系统，重构为更清晰的“侦探笔记”体系。

新的长期信息分为三类：

- **物证 physical**：玩家看得见、拿得到、可检查的实体证据。
- **证词 testimony**：人物说过的话，不等于事实，可以之后被更新或反驳。
- **疑点 doubt**：波洛当前正在思考的问题，用来引导玩家推理方向。

“察觉 notice”不作为长期笔记 Tab，而是作为一种交互过程存在。玩家点击人物立绘、对话文本或展示图片中的异常点后，进入察觉成功/失败节点，再通过 `rewards` 更新物证、证词或疑点。

## 新增文件

### `Assets/_Project/Scripts/Notebook/NotebookData.cs`

定义侦探笔记的数据结构：

- `NotebookDatabase`
- `PhysicalEvidenceDefinition`
- `TestimonyDefinition`
- `DoubtDefinition`
- 各类 `Update`
- `NotebookRewards`
- `NotebookRequirements`
- `NotebookItemState`

设计原则：

- 定义数据只说明“这个条目是什么”。
- 运行时状态单独记录“玩家是否获得它、当前更新到哪个阶段”。

### `Assets/_Project/Scripts/Notebook/DetectiveNotebookManager.cs`

侦探笔记管理器，负责：

- 读取 `Resources/Notebook/notebook_database.json`
- 添加物证、证词、疑点
- 更新物证、证词、疑点的阶段
- 检查选项或事件的前置条件
- 执行 `rewards`
- 给 UI 返回当前阶段覆盖后的显示内容

核心接口包括：

```csharp
AddEvidence(string itemId)
AddTestimony(string itemId)
AddDoubt(string itemId)

UpdateEvidenceStage(string itemId, string stageId)
UpdateTestimonyStage(string itemId, string stageId)
UpdateDoubtStage(string itemId, string stageId)

ApplyRewards(NotebookRewards rewards)
MeetsRequirements(NotebookRequirements requirements)
```

管理器会校验 `stageId` 是否存在。若 JSON 写错阶段 ID，会输出警告并拒绝更新。

### `Assets/_Project/Resources/Notebook/notebook_database.json`

新的侦探笔记数据库。

当前已迁入一部分旧物证数据，并加入一个疑点示例。

## 数据结构约定

### 物证

```json
{
  "id": "burned_paper_fragment",
  "type": "physical",
  "name": "烧焦纸片",
  "description": "在雷切特包厢的烟灰缸里发现的烧焦纸片。",
  "foundLocation": "雷切特包厢·烟灰缸",
  "icon": "icon_burnt_paper",
  "updates": [
    {
      "stageId": "mentions_daisy_armstrong",
      "description": "纸片残留文字中可以辨认出“黛西·阿姆斯特朗”。"
    }
  ]
}
```

规则：

- 初次获得时显示默认字段。
- 剧情推进后，通过 `stageId` 更新特定字段。
- 更新只覆盖写出的字段，没写的字段沿用默认值。

### 证词

```json
{
  "id": "mrs_hubbard_man_in_room",
  "type": "testimony",
  "name": "房间里的男人",
  "isConfirmed": false,
  "speakerCharacterId": "mrs_hubbard",
  "speakerName": "哈伯德太太",
  "summary": "哈伯德太太声称，昨夜有个男人进入过她的包厢。",
  "originalText": "有个男人在我的房间里面……",
  "source": "第一次询问哈伯德太太",
  "topic": "night_timeline",
  "updates": []
}
```

规则：

- 不使用 `reliability` 字段。
- `isConfirmed` 表示该证词在获得当下是否已可视为确证事实，例如法医判断、调查者亲见、多人共同确认的发现过程。后续 UI 可直接显示“确证”标签。
- 证词是否矛盾、是否被修正，写进后续 `summary` 更新里。
- `topic` 暂时作为内部分类标签保留。

### 疑点

```json
{
  "id": "watch_1_15_position",
  "type": "doubt",
  "name": "1:15 怀表",
  "question": "怀表为什么停在 1:15？",
  "description": "它看起来像是在指示雷切特的死亡时间。",
  "finalConclusion": "",
  "resolved": false,
  "updates": [
    {
      "stageId": "fake_time",
      "question": "这块怀表是否被用来误导死亡时间？",
      "description": "怀表停在 1:15，又被放在睡衣口袋里，更像是人为布置给调查者看的线索。",
      "finalConclusion": "怀表极有可能是被人为放在那里的，因此 1:15 这个时间也很可能是误导。",
      "resolved": true
    }
  ]
}
```

规则：

- 疑点只显示当前阶段，不展示完整历史思考链。
- `resolved` 玩家可见，用于显示“未解决/已解决”。
- 如果某个疑点更新阶段写了 `finalConclusion`，管理器会自动视为已解决。

## 对话 JSON 新格式

`DialogueData.cs` 已经扩展出新字段，但旧字段仍保留，以保证现有 JSON 能继续解析。

### 外层结构

新对话 JSON 推荐使用“剧情段落”语义，而不是旧的“角色对话”语义。

```json
{
  "sceneId": "opening",
  "startNodeId": "node1",
  "nodes": []
}
```

规则：

- `sceneId`：当前对话文件的剧情段落 ID，通常与文件名一致，例如 `opening.json` 对应 `opening`。
- `startNodeId`：进入该对话后播放的第一个节点。
- `nodes`：节点列表。
- 旧字段 `characterId`、`characterName`、`defaultPortrait` 暂时仍可解析，但新 JSON 不推荐继续使用。

### 节点字段规则

普通节点只写需要用到的字段，不需要把所有字段都写出来。

必填字段：

- `id`
- `node.nodeType`
- `node.speakerCharacterId`
- `node.speakerName`
- `node.text`
- `node.nextNodeId` 或 `node.options`

按需字段：

- 有人物显示时写 `stage`
- 有额外图片时写 `presentedImage`
- 有点击亮点时写 `hotspots`
- 有获得或更新内容时写 `rewards`
- 有分支选项时写 `options`

字段省略是合法的。Unity 的 JSON 解析会把缺失字段读成 `null` 或默认值，当前代码已经对 `rewards`、`hotspots`、`options` 等字段做了空值处理。

### 普通对话节点最小写法

```json
{
  "id": "node1",
  "node": {
    "nodeType": "dialogue",
    "speakerCharacterId": "poirot",
    "speakerName": "波洛",
    "text": "你了解得正确，先生。",
    "nextNodeId": "node2"
  }
}
```

### 普通对话节点：有人物显示

```json
{
  "id": "node1",
  "node": {
    "nodeType": "dialogue",
    "speakerCharacterId": "pierre",
    "speakerName": "列车员皮埃尔",
    "text": "我、我只是听见声音后去敲了门。",
    "nextNodeId": "node2",
    "stage": {
      "focusCharacterId": "pierre",
      "slots": [
        {
          "slotId": "right",
          "characterId": "pierre",
          "portrait": "pierre_anxious"
        }
      ]
    }
  }
}
```

### 舞台 `stage`

规则：

- `stage` 出现时，完整覆盖当前人物舞台。
- 只显示 `slots` 中列出的角色。
- `slots` 为空表示清空舞台。
- `focusCharacterId` 为空时，默认高亮当前说话人。

默认补全规则：

- 左侧默认放 `poirot`。
- 如果当前 `speakerCharacterId` 是 `poirot`，右侧不放人物，只保留左侧 `poirot`。
- 如果当前 `speakerCharacterId` 不是 `poirot`，右侧放当前说话人。
- 没有特殊标注时，不额外引入第三人，只按“左波洛 / 右说话人”处理。

立绘规则：

- 左侧波洛使用波洛对应立绘。
- 右侧人物使用该说话人的对应立绘。
- 如果 `speakerCharacterId` 不等于立绘资源名，优先沿用项目中已出现过的命名模式。

聚焦规则：

- 默认 `focusCharacterId = speakerCharacterId`。
- 特殊标注可以覆盖左右站位。
- 特殊标注也可以覆盖聚焦对象。
- 除特殊标注外，全部按默认规则处理。

### 展示图片 `presentedImage`

用于怀表、护照、房间局部图等非人物图片。

规则：

- 有 `imageId` 就显示。
- 没有 `imageId` 就不显示。
- 不再依赖 `visible` 字段。

### 对话内热点 `hotspots`

用于对话中的轻量察觉，例如点击人物表情或某段文字。

```json
"hotspots": [
  {
    "id": "pierre_expression_notice",
    "targetType": "portrait",
    "targetId": "pierre",
    "successNodeId": "notice_pierre_expression_success"
  }
]
```

规则：

- 普通 `dialogue` 节点里的 `hotspots` 只做单一成功型察觉。
- 点击亮点直接进入 `successNodeId`。
- 不处理失败。
- 不放 `prompt`。
- 真正的物证、证词、疑点更新写在成功节点的 `rewards` 中。

### 图片观察察觉节点

用于护照、哈伯德太太房间图这类“看图找破绽”的场景。

```json
{
  "id": "hubbard_room_notice",
  "node": {
    "nodeType": "notice",
    "targetType": "presented_image",
    "presentedImage": {
      "imageId": "hubbard_latch_diagram",
      "position": "full"
    },
    "prompt": "图中的情况与哈伯德太太的说法不符。请找出不自然之处。",
    "hotspots": [
      {
        "id": "latch",
        "label": "门闩",
        "correct": true,
        "successNodeId": "notice_hubbard_latch_success",
        "failureNodeId": ""
      },
      {
        "id": "bag",
        "label": "提包",
        "correct": false,
        "successNodeId": "",
        "failureNodeId": "notice_hubbard_latch_failure"
      }
    ]
  }
}
```

## Rewards 和 Requirements

### Rewards

`rewards` 表示进入节点或点击选项后，给玩家什么，或更新什么。

```json
{
  "evidenceToAdd": ["burned_paper_fragment"],
  "testimonyToAdd": ["mrs_hubbard_man_in_room"],
  "doubtToAdd": ["watch_1_15_position"],
  "evidenceStageToUpdate": [
    {
      "itemId": "burned_paper_fragment",
      "stageId": "mentions_daisy_armstrong"
    }
  ],
  "testimonyStageToUpdate": [],
  "doubtStageToUpdate": []
}
```

### Requirements

`requirements` 表示选项出现或事件触发需要哪些前置条件。

```json
{
  "requiredEvidenceIds": ["burned_paper_fragment"],
  "requiredTestimonyIds": [],
  "requiredDoubtIds": [],
  "requiredResolvedDoubtIds": []
}
```

## 当前代码落地状态

已经完成：

- 新侦探笔记数据结构。
- `DetectiveNotebookManager`。
- 新数据库文件。
- 对话数据结构扩展。
- `DialogueManager` 已接入 `node.rewards`、`option.rewards`、`option.requirements`。
- `DialogueManager` 已接入新 `stage`，并通过 `CharacterStageController` 渲染人物舞台。
- `DialogueManager` 已接入 `presentedImage` 的运行时显示逻辑。
- `DialogueManager` 已接入普通对话 `hotspots` 和 `nodeType: "notice"` 的点击跳转逻辑。
- `EvidencePanelUI` 已升级为侦探笔记列表逻辑，可显示物证、证词、疑点三类。
- 侦探笔记按钮会在玩家首次获得任意物证、证词或疑点后出现；物证、证词、疑点 Tab 会在对应类别首次有条目后显示。
- 运行时会优先加载 `Resources/UI/DetectiveNotebookRoot.prefab` 作为正式侦探笔记 UI；找不到该 Prefab 时才会生成基础兜底 UI。
- 侦探笔记打开时会阻止对话点击推进；点击侦探笔记按钮或面板区域时，也不会被 `DialogueManager` 当作继续对话的点击。
- 雷切特包厢和桌面调查脚本已同步把获得的物证写入 `DetectiveNotebookManager`。
- 旧 `clueToAdd / requiredClueId` 仍然兼容。

尚未完成：

- 当前场景里现有 Tab 按钮仍需要在 Unity Inspector 中补充绑定到 `ShowTestimonyTab` / `ShowDoubtTab`，旧 `ShowInferenceTab` 暂时映射到疑点 Tab。
- `presentedImage` 和 `hotspots` 目前使用运行时自动创建的基础 UI，后续仍需要美术化和 Inspector 精调。
- 旧对话 JSON 尚未迁移到新格式。

因此，当前改动属于“数据层和部分流程层先行”。后续应优先实现 UI 和新对话视觉表现层。

## 旧系统兼容说明

旧文件暂时保留：

- `Assets/_Project/Scripts/Evidence/EvidenceManager.cs`
- `Assets/_Project/Scripts/UI/EvidencePanelUI.cs`
- `Assets/_Project/Resources/Evidence/clues.json`

保留原因：

- 当前场景和 UI 仍引用旧系统。
- 直接删除会导致场景引用断裂。
- 新系统稳定后，再逐步迁移旧场景调查和 UI。

## 后续建议顺序

1. 在 Unity 场景或 Prefab 中补齐侦探笔记三类 Tab 按钮绑定。
2. 为 `presentedImage` 和 `hotspots` 制作正式 UI 样式并在 Inspector 中绑定。
3. 逐步迁移 `opening2.json`、`mrs_hubbard.json`、`count_andrenyi.json`。
4. 继续将其他场景调查脚本从 `EvidenceManager.AddClue` 迁移到 `DetectiveNotebookManager.AddEvidence` 或 `ApplyRewards`。

## UI 点击阻塞说明

当前为了避免玩家查看侦探笔记时误跳过对话，`DialogueManager` 会在推进对话前检查：

- 侦探笔记面板是否打开。
- 鼠标是否点击在侦探笔记按钮或面板的 `RectTransform` 区域内。

这是一种轻量实现，适合当前 `Screen Space Overlay` 的侦探笔记 UI。后续如果出现更多需要阻止对话推进的界面，例如设置菜单、存档菜单、证据大图、察觉图层或弹窗，建议抽出统一的 `UIInputBlocker`，用 EventSystem/UI Raycast 统一判断“这次点击是否应该被 UI 吃掉”，避免每个系统都单独写坐标或区域判断。
