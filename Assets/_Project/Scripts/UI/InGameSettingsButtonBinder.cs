using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在编辑器里绑定「游戏内设置」入口：可拖 <see cref="Button"/>，也可留空并在同一根下用符合命名的子按钮自动查找。
/// 建议把本脚本与按钮放在同一 Prefab 层级下（例如父物体挂 Binder，子物体为 <c>InGameSettingsButton</c>）。
/// </summary>
[DefaultExecutionOrder(-500)]
public class InGameSettingsButtonBinder : MonoBehaviour
{
    [Tooltip("直接拖入你的设置按钮。留空时会在「查找根」下按命名自动找一个 Button。")]
    [SerializeField] private Button settingsButton;

    [Tooltip("留空则用本物体；若 Binder 与按钮不在同一物体上，把包含按钮的父物体拖到这里（例如 HUD_Root）。")]
    [SerializeField] private Transform searchRoot;

    [Tooltip("勾选后不再生成代码里的 RuntimeSettingsButton 占位按钮。")]
    [SerializeField] private bool disableRuntimeSpawn = true;

    private void Awake()
    {
        if (disableRuntimeSpawn)
        {
            InGamePauseMenuController.DisableAutoSpawnSettingsButton = true;
        }

        Transform root = searchRoot != null ? searchRoot : transform;
        Button target = settingsButton != null ? settingsButton : InGamePauseMenuController.FindPreferredSettingsButtonUnder(root);

        if (target != null)
        {
            InGamePauseMenuController.UseManualSettingsButton(target);
        }
        else if (disableRuntimeSpawn)
        {
            Debug.LogWarning(
                "InGameSettingsButtonBinder: 未找到可用的设置按钮。请在子级放置命名为 InGameSettingsButton 的 Button，"
                + "或拖入 settingsButton，或为「查找根」指定包含该按钮的父物体。当前物体: " + gameObject.name,
                this);
        }
    }
}
