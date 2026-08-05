using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class VideoTuple
{
    public VideoPlayer videoPlayer;
    public RawImage rawImage;
}

[Serializable]
public class VideoManager : MonoBehaviour
{
    public VideoTuple[] videoplayers;
    public Dictionary<RawImage, Coroutine> fadeCoroutines;
    private VideoPlayer currentVideo = null;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fadeCoroutines = new Dictionary<RawImage, Coroutine>();
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
            //匹配具有与对象名字相同的前缀的视频
            for (int i = 0; i < videoFiles.Length; ++i)
                {
                   for (int j = 0; j < videoplayers.Length; ++j)
                    {
                        if (videoFiles[i].Contains(videoplayers[j].videoPlayer.name))
                        {
                            videoplayers[j].videoPlayer.url = videoFiles[i];
                        }
                    }
                }
        }else
        {
            Debug.LogWarning("Found NO videos in CustomVideo folder! Perhaps you should fill'em up?");
        }
        
        foreach (var vid in videoplayers)
        {
            vid.videoPlayer.enabled = false;
            vid.rawImage.enabled = false;
        }
    }

    public void SwitchTo(string vidname)
    {
        foreach(var vid in videoplayers)
        {
            if (vid.videoPlayer.name == vidname)
            {
                if (currentVideo == vid.videoPlayer)
                {
                    return;
                }
                
//vid.rawImage.transform.SetAsLastSibling();
                currentVideo = vid.videoPlayer;
                StartCoroutine(PrepareAndPlayRoutine(vid));
            }else
            {
                FadeTo(0f, vid, true);
            }
        }
    }

    private IEnumerator PrepareAndPlayRoutine(VideoTuple vid)
    {
        vid.videoPlayer.enabled = true;
        if (!vid.videoPlayer.isPrepared)
        {
            vid.videoPlayer.Prepare();
            while (!vid.videoPlayer.isPrepared)
            {
                yield return null;
            }
        }

        vid.videoPlayer.time = 0;
        vid.videoPlayer.Play();
        yield return null; 

        vid.rawImage.enabled = true;
        
        FadeTo(1f, vid, false);
    }

    public void FadeTo(float targetAlpha, VideoTuple vid, bool disableWhenFinish, float duration = 0.5f)
    {
        if (fadeCoroutines.ContainsKey(vid.rawImage) && fadeCoroutines[vid.rawImage] != null)
        {
            StopCoroutine(fadeCoroutines[vid.rawImage]);
        }

        fadeCoroutines[vid.rawImage] = StartCoroutine(FadeRoutine(targetAlpha, duration, vid, disableWhenFinish));
    }

//协程实现的渐变过程
    private IEnumerator FadeRoutine(float targetAlpha, float duration, VideoTuple vid, bool disableWhenFinish)
    {
        CanvasGroup canvasGroup = vid.rawImage.GetComponent<CanvasGroup>();
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
        fadeCoroutines[vid.rawImage] = null;

        if (disableWhenFinish)
        {
            vid.rawImage.enabled = false;
            vid.videoPlayer.Pause();
        }
    }
    public void PlayVideo(string vidname)
    {
        foreach (var vid in videoplayers)
        {
            if (vidname == vid.videoPlayer.name)
            {
                if (!vid.videoPlayer.isPlaying)
                {
                    vid.videoPlayer.Play();
                }
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void PauseVideo(string vidname)
    {
        foreach (var vid in videoplayers)
        {
            if (vidname == vid.videoPlayer.name)
            {
                if (vid.videoPlayer.isPlaying)
                {
                    vid.videoPlayer.Pause();
                }
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void StopVideo(string vidname)
    {
        foreach (var vid in videoplayers)
        {
            if (vidname == vid.videoPlayer.name)
            {
                vid.videoPlayer.Stop();
                return;
            }
        }
        Debug.LogWarning("Trying to control a non-existing video player:" + vidname);
    }

    public void StopAllVideo()
    {
        foreach (var vid in videoplayers)
        {
            vid.videoPlayer.enabled = false;
            vid.rawImage.enabled = false;
        }
    } 
}
