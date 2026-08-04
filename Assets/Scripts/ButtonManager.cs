using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ButtonManager: MonoBehaviour
{
    public Button[] buttons;
    public ScreenManager screenManager;
    public VideoManager videoManager;

    void Start()
    {
        screenManager = GetComponent<ScreenManager>();
        videoManager = GetComponent<VideoManager>();
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
            case "btnMainStart":
                Debug.Log("Clicked start");
                screenManager.SetCanvasState("GameScreen Canv", true);
                screenManager.SetCanvasState("MainScreen Canv", false);
                screenManager.SetCanvasState("SettingScreen Canv", false);

                videoManager.SwitchTo("Vloop1");
                break;
            case "btnMainSettings":
                Debug.Log("Clicked setting");
                screenManager.SetCanvasState("SettingScreen Canv", true);
                screenManager.SetCanvasState("MainScreen Canv", false);
                break;
            case "btnMainExit":
                Debug.Log("Clicked exit");
                screenManager.SetCanvasState("GameScreen Canv", false);
                screenManager.SetCanvasState("MainScreen Canv", false);
                screenManager.SetCanvasState("SettingScreen Canv", false);
                Application.Quit();
                break;
            case "btnSettingsReturn":
                Debug.Log("Clicked settings");
                screenManager.SetCanvasState("MainScreen Canv", true);
                screenManager.SetCanvasState("SettingScreen Canv", false);
                break;
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
                
            default:
                Debug.LogWarning("ButtonManager Encounter a name that points to a non-existing button:" + btn.name);
                break;
        }
    }
}
