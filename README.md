# 东方快车谋杀案 · Unity 文字推理原型

基于 Unity 的 2D 文字推理游戏原型，题材来自阿加莎·克里斯蒂《东方快车谋杀案》。玩法方向参考《山河旅探》《大逆转裁判》等：调查、对话、察觉异常与阶段性推理，逐步推进案情。

---

## 环境与运行

| 项目 | 说明 |
|------|------|
| **Unity** | `2022.3.62f3c1`（见 `ProjectSettings/ProjectVersion.txt`） |
| **打开方式** | 用上述版本打开本仓库根目录（含 `Assets`、`ProjectSettings` 的文件夹） |

在 **File → Build Settings** 中，工程已登记若干场景；实际游玩入口一般为 **`Assets/_Project/Scenes/MainMenu.unity`**，再进入列车走廊、餐车/包厢调查等流程。根目录下另有模板场景 `Assets/Scenes/SampleScene.unity`，可按需从 Build 列表中关闭。

---

## 仓库结构（与玩法相关）

```
Assets/_Project/
├── Scenes/          # MainMenu、TrainCorridor、RatchettTable、CrimeScene 等
├── Resources/
│   ├── Dialogue/    # 对话 JSON（sceneId、nodes、rewards 等）
│   ├── Notebook/    # notebook_database.json — 物证 / 证词 / 疑点定义
│   ├── Evidence/    # clues.json 等（旧物证系统残留）
│   └── UI/          # DetectiveNotebookRoot.prefab 等
└── Scripts/
    ├── Dialogue/    # DialogueManager、DialogueData、CharacterStageController
    ├── Notebook/    # DetectiveNotebookManager、NotebookData
    ├── Evidence/    # 场景调查、InteractableItem、与旧 EvidenceManager 兼容层
    ├── UI/          # EvidencePanelUI（侦探笔记面板）、EvidenceSlotUI 等
    └── Managers/    # SceneLoader、ScreenFader、OpeningFlowController、DialogueProgressBridge、GameManager 等
```

---

## 核心系统概览

### 侦探笔记（新体系）

长期信息分为三类：

- **物证（physical）**：可检视的实体线索。  
- **证词（testimony）**：角色陈述，可随剧情更新阶段。  
- **疑点（doubt）**：当前推理问题；可有「未解决 / 已解决」状态。

**察觉（notice）** 不作为长期 Tab，而是对话或看图过程中的交互；成功后通常在节点的 `rewards` 里写入或更新上述三类条目。

运行时由 **`DetectiveNotebookManager`** 读取 **`Resources/Notebook/notebook_database.json`**，负责添加条目、更新阶段、校验 `requirements`、执行 `rewards`。UI 侧优先加载 **`Resources/UI/DetectiveNotebookRoot.prefab`**；侦探笔记打开时会阻断对话点击推进（避免误触下一句）。

### 对话与 JSON

对话文件推荐使用外层结构：`sceneId`、`startNodeId`、`nodes`。每个节点含 `id` 与内层 `node`，常用字段包括：

- `nodeType`：`dialogue` / `notice` 等  
- `speakerCharacterId`、`speakerName`、`text`  
- `nextNodeId` 或 `options`  
- 按需：`stage`（立绘舞台）、`presentedImage`（额外插图）、`hotspots`（察觉热点）、`rewards`、`requirements`

旧字段仍可能被解析，以保证过渡期的兼容性。详细字段约定与示例见 **`docs/README.md`**（历史文档）。

### 场景与流程串联

- **`SceneLoader`** + **`ScreenFader`**：场景切换时的黑屏淡入淡出。  
- **`OpeningFlowController`**、**`DialogueProgressBridge`**：在走廊等场景中接续对话进度（例如调查结束后回到审讯对话）。  
- 餐车开场、夜间片段、麦克昆/哈伯德等对话位于 **`Resources/Dialogue/`**（如 `opening.json`、`the_night.json`、`mr_MacQueen.json`、`mrs_hubbard.json`）。  
- 雷切特相关调查由 **`RatchettCabinSceneController`**、**`RatchettTableSceneController`** 等驱动，并与侦探笔记写入逻辑衔接。

### 旧系统（兼容）

以下仍保留，避免场景引用断裂；新内容优先走侦探笔记与对话 `rewards`：

- `Scripts/Evidence/EvidenceManager.cs`  
- `Resources/Evidence/clues.json`  

---

## 文档归档

此前较长版本的设计说明与更新记录已移至 **`docs/`**：

- [`docs/README.md`](docs/README.md) — 数据结构、对话 JSON 规则、Rewards/Requirements、落地状态与后续建议  
- [`docs/README2.md`](docs/README2.md) — 第二次大更新总结（已转换对话文件、UI 接入、串联方式等）

若本根目录 README 与归档内容不一致，**以当前工程代码与 JSON 为准**，归档仅供查阅历史决策与迭代笔记。

---

## 常见操作提示

1. **新增一条笔记本条目**：在 `notebook_database.json` 中定义，再在对话节点的 `rewards` 里 `evidenceToAdd` / `testimonyToAdd` / `doubtToAdd`（或对应 stage 更新）。  
2. **图片与立绘**：`presentedImage.imageId`、`stage.slots[].portrait` 等需与 **`Resources`** 下可加载资源命名一致。  
3. **侦探笔记 Tab**：三类 Tab 随玩家首次获得对应类型条目后显示；Inspector 中部分按钮绑定仍以场景中实际引用为准（详见 `docs/README.md`「尚未完成」小节）。

---

## 许可与原作说明

本项目为学习与原型的非商业用途实现；角色与情节归属原作及合法权利人。若对外分发，请自行确认版权与商标合规。
