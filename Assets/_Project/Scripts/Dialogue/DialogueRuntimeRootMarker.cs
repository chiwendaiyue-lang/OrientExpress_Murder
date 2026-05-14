using UnityEngine;

/// <summary>
/// 挂在「对话运行时」Prefab 根节点：在子物体 Awake 之前将整个对话 UI 树移入 DontDestroyOnLoad，
/// 避免子节点上的 <see cref="DialogueManager"/> 单独 DDOL 导致从父级脱离、引用断裂。
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class DialogueRuntimeRootMarker : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
