using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    // ³¡¾°Ãû³Æ³£Á¿£¨±ÜÃâÊÖÐ´Æ´´í£©
    public const string SCENE_MAIN_MENU = "MainMenu";
    public const string SCENE_TRAIN_CORRIDOR = "TrainCorridor";
    public const string SCENE_CRIME_SCENE = "CrimeScene";
    public const string SCENE_RATCHETT_TABLE = "RatchettTable";
    public const string SCENE_MRS_HUBBARD = "MrsHubbardRoom";
    public const string SCENE_COUNT_ANDRENYI = "CountAndrenyiRoom";
    public const string SCENE_POIROT_ROOM = "PoirotRoom";

    void Awake() { Instance = this; }

    public void LoadScene(string sceneName)
    {
        ScreenFader.LoadSceneWithFade(sceneName);
    }

    public void LoadMainMenu() => LoadScene(SCENE_MAIN_MENU);
    public void LoadTrainCorridor() => LoadScene(SCENE_TRAIN_CORRIDOR);
    public void LoadCrimeScene() => LoadScene(SCENE_CRIME_SCENE);
    public void LoadRatchettTable() => LoadScene(SCENE_RATCHETT_TABLE);
    public void LoadMrsHubbardRoom() => LoadScene(SCENE_MRS_HUBBARD);
    public void LoadCountAndrenyiRoom() => LoadScene(SCENE_COUNT_ANDRENYI);
    public void LoadPoirotRoom() => LoadScene(SCENE_POIROT_ROOM);
}
