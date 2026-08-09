using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ButtonManager: MonoBehaviour
{
    public Button[] buttons;
    public ScreenManager screenManager;
    public VideoManager videoManager;
    public AutoCutManager autoCutManager;

    void Start()
    {
        screenManager = GetComponent<ScreenManager>();
        videoManager = GetComponent<VideoManager>();
        autoCutManager = GetComponent<AutoCutManager>();
        foreach (Button btn in buttons)
        {
            btn.onClick.AddListener(() => OnButtonClicked(btn.gameObject));
        }
    }

    private void OnButtonClicked(GameObject btn)
    {
        Debug.Log("Clicked:" + btn.name);
        switch (btn.name)
        {
            //Mainscreen buttons
            case "btnMainStart":
                Debug.Log("Clicked start");
                screenManager.SwitchTo("GameScreen Canv");
                videoManager.SwitchTo("Vloop1");
                break;
            case "btnMainSettings":
                Debug.Log("Clicked setting");
                screenManager.SwitchTo("SettingScreen Canv");
                break;
            case "btnMainExit":
                Debug.Log("Clicked exit");
                Application.Quit();
                break;
            case "btnMainAutoCut":
                Debug.Log("Clicked Auto Cutter");
                screenManager.SwitchTo("AutoCutterScreen Canv");
                break;

            //Settingscreen buttons
            case "btnSettingsReturn":
                Debug.Log("Clicked settings");
                screenManager.SwitchTo("MainScreen Canv");
                break;

            //Gamescreen buttons
            case "btnGameP1":
                Debug.Log("Clicked loop1");
                videoManager.SwitchTo("Vloop1");
                break;
            case "btnGameP2":
                Debug.Log("Clicked loop2");
                videoManager.SwitchTo("Vloop2");
                break;
            case "btnGameP3":
                Debug.Log("Clicked loop3");
                videoManager.SwitchTo("Vloop3");
                break;
            case "btnGameP4":
                Debug.Log("Clicked loop4");
                videoManager.SwitchTo("Vloop4");
                break;
            case "btnGameP5":
                Debug.Log("Clicked loop5");
                videoManager.SwitchTo("Vloop5");
                break;
            case "btnGameP6":
                Debug.Log("Clicked Final");
                videoManager.SwitchTo("VFinal");
                break;
            case "btnGameExit":
                Debug.Log("Clicked Exit");
                videoManager.StopAllCoroutines();
                videoManager.StopAllVideo();
                screenManager.SetCanvasState("MainScreen Canv", true);
                screenManager.SetCanvasState("GameScreen Canv", false);
                screenManager.SetCanvasState("SettingScreen Canv", false);
                break;

            //Autocutter screen buttons
            case "btnACLastFrame":
                autoCutManager.LastFrameVid();
                break;

            case "btnACNextFrame":
                autoCutManager.NextFrameVid();
                break;

            case "btnACPause":
                autoCutManager.PauseVid();
                break;

            case "btnACPlay":
                autoCutManager.PlayVid();
                break;

            case "btnAChelp":
                autoCutManager.ChangeHelpState(true);
                break;
            case "btnACHelpReturn":
                autoCutManager.ChangeHelpState(false);
                break;

            case "btnACrefresh":
                autoCutManager.UpdateDropdown();
                break;

            case "btnACChooseCurrentFrame":
                autoCutManager.ChooseCurrentAsStart();
                break;

            case "btnACAutoFind":
                autoCutManager.StartFindLoop();
                break;

            case "btnACManualFind":
                autoCutManager.ChooseCurrenAsEnd();
                break;

            case "btnACProcess":
                autoCutManager.CutVideo();
                break;
            case "btnACreturn":
                screenManager.SwitchTo("MainScreen Canv");
                autoCutManager.ClearState();
                break;

            default:
                Debug.LogWarning($"ButtonManager encountered a name that points to a non-existing button: '{btn.name}'");
                break;
        }
    }
}
