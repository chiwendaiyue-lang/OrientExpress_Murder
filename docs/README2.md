# 第二次大更新总结

本次更新的重点不是重新定义“物证、证词、疑点、察觉”四个系统，而是把第一次大更新中确定的数据结构接入实际流程：让新的对话 JSON、侦探笔记 UI、对话串联、图片展示和察觉点击可以在当前 Unity 原型中跑通。

## 1. 对话 JSON 新格式落地

当前对话文件已经开始使用新的外层结构：

```json
{
  "sceneId": "opening",
  "startNodeId": "node1",
  "nodes": []
}
```

每个节点以 `id` 包裹 `node` 内容。节点核心字段包括：

- `nodeType`：节点类型，普通对话通常是 `dialogue`，察觉节点是 `notice`。
- `speakerCharacterId`：说话角色 ID，程序用。
- `speakerName`：说话人中文名，玩家可见。
- `text`：对话正文。
- `nextNodeId` 或 `options`：控制后续流程。

按需使用的字段：

- `stage`：控制人物立绘舞台。
- `presentedImage`：显示额外图片。
- `hotspots`：显示可点击亮点，用于察觉。
- `rewards`：获得或更新物证、证词、疑点。
- `options`：分支选项。

工程设计上的好处是：普通节点保持简洁，只有需要图片、亮点、奖励、分支时才写对应字段，不会让每个 JSON 节点都变得臃肿。

玩家体验上的好处是：剧本可以按“看对话、看图、点异常、获得笔记内容”的实际游玩节奏组织，而不是把所有功能都塞进传统选项里。

## 2. 已转换的对话文件

### opening.json

位置：`Assets/_Project/Resources/Dialogue/opening.json`

完成内容：

- 转换为新 JSON 格式。
- 雷切特对话结束后获得第一个疑点 `ratchett_enemy`。
- 结束选项“离开餐车”后直接进入 `the_night.json`。

这使侦探笔记第一次出现的时机和剧情动机一致：玩家听完雷切特的求助后，才第一次拥有需要记录和思考的问题。

### the_night.json

位置：`Assets/_Project/Resources/Dialogue/the_night.json`

完成内容：

- 转换为新 JSON 格式。
- 支持两个额外图片展示：`clock`、`man_in_red`。
- 在更完整的信息点获得证词 `french_answer`：有人喊叫、列车员敲门、随后有人用法语回答“没事”，时间约为 00:37。
- 获得证词 `red_sleepwear_person`。
- 任意结束选项都会继续进入 `mr_MacQueen.json`。

### mr_MacQueen.json

位置：`Assets/_Project/Resources/Dialogue/mr_MacQueen.json`

完成内容：

- 转换为新 JSON 格式。
- 实现两次察觉：
  - 第一次是对麦克昆人物立绘/情绪异常的察觉。
  - 第二次是对麦克昆话语停顿的察觉。
- 测试阶段用 `hotspots` 生成简单按钮模拟亮点。
- 不点击亮点时对话会阻塞，点击成功后进入察觉成功节点，再继续主线。
- 获得新证词 `macqueen_alibi`。
- 更新证词 `red_sleepwear_person` 的阶段 `macqueen_saw_red_silk_person`。
- 修复转换过程中丢失的关键对话段落。

这种处理方式的好处是：察觉不是一个普通选项，而是玩家必须主动“抓住异常”后剧情才继续，更接近目标玩法。

### mrs_hubbard.json

位置：`Assets/_Project/Resources/Dialogue/mrs_hubbard.json`

完成内容：

- 转换为新 JSON 格式。
- 获得证词 `mrs_hubbard_man_in_room`。
- 获得物证 `metal_button`。
- 实现房间图片察觉节点。
- 测试阶段用三个热点按钮模拟图片上的三个可点位置：
  - 两个错误点进入失败节点，再返回察觉节点。
  - 一个正确点进入成功节点。

注意：当前项目中实际存在 `latch_position_contradiction` 证词阶段更新：

- `Assets/_Project/Resources/Notebook/notebook_database.json` 中有该 stage。
- `Assets/_Project/Resources/Dialogue/mrs_hubbard.json` 中成功察觉节点会触发该更新。

如果后续决定不做这个证词更新，需要同步删除数据库中的 stage 和对话 rewards 中的触发项。

## 3. 侦探笔记 UI 接入

相关文件：

- `Assets/_Project/Scripts/UI/EvidencePanelUI.cs`
- `Assets/_Project/Scripts/UI/EvidenceSlotUI.cs`
- `Assets/_Project/Resources/UI/DetectiveNotebookRoot.prefab`

完成内容：

- 将原来的测试用 `EvidenceRoot` 逐步升级为正式的“侦探笔记”。
- 侦探笔记按钮在玩家第一次获得任意物证、证词或疑点后出现。
- 三个 Tab 按首次获得对应类型条目自动显示：
  - 物证
  - 证词
  - 疑点
- 条目列表中显示名称和摘要预览。
- 点击条目后，在面板内部显示更完整的详情：
  - 物证显示 `description`。
  - 证词显示 `summary` 和原话等信息。
  - 疑点显示 `description`、当前问题和结论状态。

工程设计上的好处是：Tab 解锁不需要额外写剧情开关，只依赖玩家是否已经获得对应类型的内容，逻辑更自然、维护成本更低。

玩家体验上的好处是：玩家不会一开始看到空的物证/证词页，而是在剧情真正给出某类信息后，侦探笔记才逐步展开。

## 4. 对话与侦探笔记的输入保护

完成内容：

- 侦探笔记打开时，对话不会继续推进。
- 点击侦探笔记面板或按钮时，不会误触发下一句对话。
- 热点点击在侦探笔记打开时也会被忽略。

当前实现采用的是对侦探笔记按钮和面板区域做坐标判断。这个方案能解决目前问题，但后续如果 UI 变多，建议抽象成统一的 `UIInputBlocker`，用 Unity 的 EventSystem / Raycast 判断“当前点击是否落在 UI 上”，避免每新增一个 UI 都手动加坐标保护。

## 5. 对话串联

完成内容：

- `opening.json` 结束后自动进入 `the_night.json`。
- `the_night.json` 结束后自动进入 `mr_MacQueen.json`。

这种方式适合当前测试阶段：同一个 TrainCorridor 场景中连续播放多段对话，不必为了每个角色或每一段剧情新建 Unity 场景。

后续是否新建场景，可以按规则判断：

- 只是换一段对话、换角色、换立绘：复用当前场景。
- 需要切换可点击地图、背景结构、调查物件布局：新建场景或新建场景控制器。
- 需要进入独立调查画面：可以新建场景，也可以用 `presentedImage + hotspots` 先做过渡实现。

## 6. 当前仍需 Unity 侧确认的内容

以下内容需要在 Unity 编辑器中实际运行确认：

- `DetectiveNotebookRoot` prefab 是否已经放在正确 Canvas/UI 层级下。
- 侦探笔记按钮、面板、Tab、列表、详情文字是否绑定正确。
- `clock`、`man_in_red`、`hubbard_room` 等 `presentedImage.imageId` 对应图片资源是否已放入 `Resources` 可加载路径。
- 人物立绘 `portrait` 名称是否和 Resources 中资源名一致。
- 所有 converted JSON 是否能被 Unity 的 `JsonUtility` 正常解析。
- Unity Console 是否有 C# 编译错误或资源加载警告。

## 7. 本次更新的核心结果

从工程上看，本次更新把“新数据结构”真正接到了游戏流程里：对话可以发放和更新侦探笔记内容，图片可以展示，亮点可以点击，多个 JSON 可以连续播放，侦探笔记可以随着剧情逐步出现。

从玩家体验上看，本次更新让原型开始接近目标玩法：玩家不只是读文字，而是会在对话中获得疑点，在夜间事件中记录证词，在人物异常处主动察觉，并通过侦探笔记持续整理案件信息。
