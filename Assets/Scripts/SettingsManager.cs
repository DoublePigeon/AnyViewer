using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    public AudioMixer audioMixer;
    public UnityEngine.UI.Toggle fullscreenToggle;
    public UnityEngine.UI.Slider volumeSlider;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        float savedVolume = PlayerPrefs.GetFloat("Volume", 1f);

        fullscreenToggle.isOn = isFullscreen;
        volumeSlider.value = savedVolume;

        SetFullscreen(isFullscreen);
        SetVolume(savedVolume);

        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        volumeSlider.onValueChanged.AddListener(SetVolume);

    }

    // Update is called once per frame
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetVolume(float volume)
    {
        audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20);

        PlayerPrefs.SetFloat("Volume", volume);
        PlayerPrefs.Save();
    }
}
