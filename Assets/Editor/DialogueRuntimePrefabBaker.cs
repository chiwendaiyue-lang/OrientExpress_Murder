using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 从 TrainCorridor 烘焙 Resources/UI/DialogueRuntimeRoot.prefab（Canvas + DialogueManager + CharacterStageController）。
/// 菜单：Tools 下多条入口（含英文），避免子路径找不到。
/// </summary>
public static class DialogueRuntimePrefabBaker
{
    private const string OutputPrefabPath = "Assets/_Project/Resources/UI/DialogueRuntimeRoot.prefab";
    private const string TrainCorridorScenePath = "Assets/_Project/Scenes/TrainCorridor.unity";

    [MenuItem("Tools/烘焙 DialogueRuntimeRoot（对话 Prefab）", false, 0)]
    [MenuItem("Tools/Orient Express/Bake DialogueRuntimeRoot Prefab", false, 1)]
    [MenuItem("Tools/东方快车/烘焙 DialogueRuntimeRoot（对话 UI Prefab）", false, 2)]
    public static void BakeFromTrainCorridor()
    {
        if (!EditorUtility.DisplayDialog(
                "烘焙 DialogueRuntimeRoot",
                "将从 TrainCorridor 读取 Canvas、DialogueManager、CharacterStageController，生成\n" +
                "Resources/UI/DialogueRuntimeRoot.prefab。\n\n" +
                "完成后会重新加载 TrainCorridor。若 Unity 询问是否保存 TrainCorridor，请选择「不要保存」，以免把临时父级写进场景。",
                "继续",
                "取消"))
        {
            return;
        }

        string dir = Path.GetDirectoryName(OutputPrefabPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        SceneSetup[] setups = EditorSceneManager.GetSceneManagerSetup();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(TrainCorridorScenePath, OpenSceneMode.Single);

        GameObject canvas = GameObject.Find("Canvas");
        GameObject dmGo = GameObject.Find("DialogueManager");
        GameObject stage = GameObject.Find("CharacterStageController");

        if (canvas == null || dmGo == null || stage == null)
        {
            EditorUtility.DisplayDialog(
                "烘焙失败",
                "在 TrainCorridor 中未找到名为 Canvas / DialogueManager / CharacterStageController 的根物体，请检查场景。",
                "确定");
            EditorSceneManager.RestoreSceneManagerSetup(setups);
            return;
        }

        if (canvas.transform.IsChildOf(dmGo.transform) || dmGo.transform.IsChildOf(canvas.transform))
        {
            EditorUtility.DisplayDialog("烘焙失败", "Canvas 与 DialogueManager 的层级异常，已中止。", "确定");
            EditorSceneManager.RestoreSceneManagerSetup(setups);
            return;
        }

        GameObject wrapper = new GameObject("DialogueRuntimeRoot");
        if (wrapper.GetComponent<DialogueRuntimeRootMarker>() == null)
        {
            wrapper.AddComponent<DialogueRuntimeRootMarker>();
        }

        canvas.transform.SetParent(wrapper.transform, true);
        dmGo.transform.SetParent(wrapper.transform, true);
        stage.transform.SetParent(wrapper.transform, true);

        if (File.Exists(OutputPrefabPath))
        {
            AssetDatabase.DeleteAsset(OutputPrefabPath);
        }

        PrefabUtility.SaveAsPrefabAsset(wrapper, OutputPrefabPath);

        EditorSceneManager.OpenScene(TrainCorridorScenePath, OpenSceneMode.Single);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"已生成：\n{OutputPrefabPath}", "确定");

        EditorSceneManager.RestoreSceneManagerSetup(setups);
    }
}
