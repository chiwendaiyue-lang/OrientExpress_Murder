using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    public Button startButton;
    public Button quitButton;
    [SerializeField] private string startSceneName = SceneLoader.SCENE_TRAIN_CORRIDOR;

    void Start()
    {
        AutoBindButtonsIfNeeded();

        if (startButton == null)
        {
            Debug.LogError("MainMenuManager: 未找到开始按钮，请手动绑定 startButton。");
        }
        else
        {
            // 防止重复绑定
            startButton.onClick.RemoveListener(OnStartClick);
            startButton.onClick.AddListener(OnStartClick);
        }

        if (quitButton == null)
        {
            Debug.LogWarning("MainMenuManager: 未找到退出按钮，若无退出需求可忽略。");
        }
        else
        {
            quitButton.onClick.RemoveListener(OnQuitClick);
            quitButton.onClick.AddListener(OnQuitClick);
        }
    }

    public void OnStartClick()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(startSceneName);
        }
        else
        {
            // 兜底：即使 SceneLoader 没挂载，也可进入车厢场景
            SceneManager.LoadScene(startSceneName);
        }
    }

    public void OnQuitClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void AutoBindButtonsIfNeeded()
    {
        if (startButton != null && quitButton != null)
        {
            return;
        }

        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null)
            {
                continue;
            }

            string objName = btn.gameObject.name.ToLowerInvariant();
            Text legacyLabel = btn.GetComponentInChildren<Text>(true);
            TMP_Text tmpLabel = btn.GetComponentInChildren<TMP_Text>(true);
            string labelText = legacyLabel != null ? legacyLabel.text : string.Empty;
            if (string.IsNullOrEmpty(labelText) && tmpLabel != null)
            {
                labelText = tmpLabel.text;
            }

            bool isStart = objName.Contains("start") || labelText.Contains("开始");
            bool isQuit = objName.Contains("quit") || objName.Contains("exit") || labelText.Contains("退出");

            if (startButton == null && isStart)
            {
                startButton = btn;
            }
            else if (quitButton == null && isQuit)
            {
                quitButton = btn;
            }
        }
    }
}