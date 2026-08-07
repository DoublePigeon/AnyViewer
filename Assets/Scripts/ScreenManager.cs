using System;
using UnityEngine;

[Serializable]
public class ScreenManager : MonoBehaviour
{
    public Canvas[] canvases;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string MyMsg = "Current Canvas Items:";

        foreach (Canvas canv in canvases)
        {
            if (canv.name == "MainScreen Canv")
            {
                canv.enabled = true;
            }else
            {
                canv.enabled = false;
            }

            MyMsg += (canv.name + ",");
        }

        Debug.Log(MyMsg);
    }

    public void SwitchToCanv(string name)
    {
        foreach (Canvas canv in canvases)
        {
            if (canv.name == name)
            {
                canv.enabled = true;
            }else
            {
                canv.enabled = false;
            }
        }
    }

    public void SetCanvasState(string name, bool onNoff)
    {
        foreach (Canvas canv in canvases)
        {
            if (canv.name == name)
            {
                canv.enabled = onNoff;
                return;
            }
        }
        Debug.LogWarning("ScreenManager Encountered a name that points to a Non-existing Canvasitem:" + name);
    }
}
