using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

[Serializable]
public class VideoManager : MonoBehaviour
{
    public VideoPlayer[] videoplayers;
    public Dictionary<VideoPlayer, Coroutine> fadeCoroutines;
    private VideoPlayer currentVideo = null;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fadeCoroutines = new Dictionary<VideoPlayer, Coroutine>();
        string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        string videoFolder = Path.Combine(rootPath, "CustomVideos");

        if (!Directory.Exists(videoFolder))
        {
            Directory.CreateDirectory(videoFolder);
            Debug.Log("Created CustomVideo folder since it does not exists:" + videoFolder);
        }

        string[] videoFiles = Directory.GetFiles(videoFolder);

        if (videoFiles.Length > 0)
        {
            for (int i = 0; i < videoFiles.Length; ++i)
                {
                   for (int j = 0; j < videoplayers.Length; ++j)
                    {
                        if (videoFiles[i].Contains(videoplayers[j].name))
                        {
                            videoplayers[j].url = videoFiles[i];
                        }
                    }
                }
        }else
        {
            Debug.LogWarning("Found NO videos in CustomVideo folder! Perhaps you should fill'em up?");
        }
        
        foreach (VideoPlayer vid in videoplayers)
        {
            vid.enabled = false;
        }
    }

    public void SwitchTo(string vidname)
    {
        foreach(VideoPlayer vid in videoplayers)
        {
            if (vid.name == vidname)
            {
                if (currentVideo == vid)
                {
                    return;
                }

                vid.enabled = true;
                currentVideo = vid;
                StartCoroutine(PrepareAndPlayRoutine(vid));
            }else
            {
                FadeTo(0f, vid);
                if (vid.isPlaying)
                {
                    vid.Pause();
                }
                vid.enabled = false;
            }
        }
    }

    private IEnumerator PrepareAndPlayRoutine(VideoPlayer vid)
    {
        // 1. 如果视频还没准备好，调用 Prepare 并等待
        if (!vid.isPrepared)
        {
            vid.Prepare();
            // 在这里挂起，直到视频准备完毕。这期间游戏依然流畅，不会死机
            while (!vid.isPrepared)
            {
                yield return null;
            }
        }

        vid.transform.SetAsFirstSibling();
        vid.Play();
        yield return null; 

        FadeTo(1f, vid);
    }

    public void FadeTo(float targetAlpha, VideoPlayer vid, float duration = 1f)
    {
        if (fadeCoroutines.ContainsKey(vid) && fadeCoroutines[vid] != null)
        {
            StopCoroutine(fadeCoroutines[vid]);
        }

        fadeCoroutines[vid] = StartCoroutine(FadeRoutine(targetAlpha, duration, vid));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, VideoPlayer vid)
    {
        CanvasGroup canvasGroup = vid.GetComponent<CanvasGroup>();
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normTime = Mathf.Clamp01(elapsedTime / duration);
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, targetAlpha, normTime);

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutines[vid] = null;
    }
    // Update is called once per frame
    public void PlayVideo(string vidname)
    {
        foreach (VideoPlayer vid in videoplayers)
        {
            if (vidname == vid.name)
            {
                if (!vid.isPlaying)
                {
                    vid.Play();
                }
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void PauseVideo(string vidname)
    {
        foreach (VideoPlayer vid in videoplayers)
        {
            if (vidname == vid.name)
            {
                if (vid.isPlaying)
                {
                    vid.Pause();
                }
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void StopVideo(string vidname)
    {
        foreach (VideoPlayer vid in videoplayers)
        {
            if (vidname == vid.name)
            {
                vid.Stop();
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void StopAllVideo()
    {
        foreach (VideoPlayer vid in videoplayers)
        {
            vid.Stop();
        }
    } 
}
