using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { MainMenu, Exploring, Dialogue, Investigating }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // 关卡完成标志
    public bool HasInvestigatedCrimeScene { get; set; } = false;
    public bool HasTalkedToMrsHubbard { get; set; } = false;
    public bool HasTalkedToCountAndrenyi { get; set; } = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"Game State: {CurrentState}");
    }

    // 检查是否可以进入下一关
    public bool CanProceedToNextLevel()
    {
        return HasInvestigatedCrimeScene && HasTalkedToMrsHubbard;
    }
}