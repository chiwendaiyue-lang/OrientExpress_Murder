using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    public Button startButton;
    public Button continueButton;
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
            startButton.onClick.RemoveListener(OnStartClick);
            startButton.onClick.AddListener(OnStartClick);
        }

        if (continueButton == null)
        {
            Debug.LogWarning("MainMenuManager: 未找到继续按钮，可在 Canvas 上添加名为 Continue/继续 的按钮并绑定 continueButton。");
        }
        else
        {
            continueButton.onClick.RemoveListener(OnContinueClick);
            continueButton.onClick.AddListener(OnContinueClick);
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
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MainMenuManager: 找不到 Canvas，无法显示存档槽。");
            return;
        }

        SaveSlotModalUI.Show(
            canvas.transform,
            SaveSlotModalUI.PickMode.NewGame,
            OnNewGameSlotPicked,
            null);
    }

    public void OnContinueClick()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MainMenuManager: 找不到 Canvas。");
            return;
        }

        SaveSlotModalUI.Show(
            canvas.transform,
            SaveSlotModalUI.PickMode.Continue,
            OnContinueSlotPicked,
            null);
    }

    private void OnNewGameSlotPicked(int slot)
    {
        GameSaveService.DeleteSlot(slot);
        GameSaveService.ResetAllProgressForNewGame();
        GameSaveService.ActiveSaveSlot = slot;
        GameResumeCoordinator.PendingResume = null;
        GameResumeCoordinator.SuppressOpeningFlowOnce = false;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(startSceneName);
        }
        else
        {
            SceneManager.LoadScene(startSceneName);
        }
    }

    private void OnContinueSlotPicked(int slot)
    {
        SaveGameData data = GameSaveService.TryLoadSlot(slot);
        if (data == null)
        {
            Debug.LogWarning($"MainMenuManager: 槽位 {slot + 1} 没有存档。");
            return;
        }

        GameSaveService.ActiveSaveSlot = slot;
        GameResumeCoordinator.PendingResume = data;
        GameResumeCoordinator.SuppressOpeningFlowOnce = false;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(data.activeSceneName);
        }
        else
        {
            SceneManager.LoadScene(data.activeSceneName);
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
            bool isContinue = objName.Contains("continue") || labelText.Contains("继续");

            if (startButton == null && isStart)
            {
                startButton = btn;
            }
            else if (continueButton == null && isContinue)
            {
                continueButton = btn;
            }
            else if (quitButton == null && isQuit)
            {
                quitButton = btn;
            }
        }
    }
}
