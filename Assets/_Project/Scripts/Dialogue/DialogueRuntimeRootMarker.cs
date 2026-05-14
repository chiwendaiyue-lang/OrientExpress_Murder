using UnityEngine;

/// <summary>
/// 挂在「对话运行时」Prefab 根节点：仅作身份标记（射线穿透、隐藏装饰底图等逻辑会识别该根）。
/// 不再将整个树移入 DontDestroyOnLoad，以免把场景里摆好的 Canvas 从 TrainCorridor 等场景「挪走」；
/// 需要对话时由 <see cref="DialogueManager.EnsureExists"/> 从 Resources 实例化，或随当前场景生命周期存在。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueRuntimeRootMarker : MonoBehaviour
{
}
