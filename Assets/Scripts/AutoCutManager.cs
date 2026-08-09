using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;

[Serializable]
public class AutoCutManager : MonoBehaviour
{
    public string videoFolder = "";
    public string videoPath;
    public string ffmpegExePath;
    public string saveDirectory;
    public float fps;

    public long startFrame = 0;
    public long endFrame = 0;
    public float thresholdSimValue = 0.98f; //0~1
    public int thumbnailSize = 320;

    public bool isProcessing = false;
    public bool isCutting = false;

    //组件与缓存
    public VideoPlayer videoPlayer;
    public RenderTexture originalRT;
    public RenderTexture referenceRT;
    private RenderTexture thumbnailRT;
    private Texture2D tempTex;

    public RawImage referenceIMG;
    
    //分析状态标识
    private bool isFrameReady = false;
    private bool hasLastFrame = false;
    private float[] firstFrameVector;
    private float firstFrameNorm;

    //Log
    public TextMeshProUGUI logText;
    public ScrollRect scrollRect;

    public int maxLogLines = 100;
    private Queue<string> logQueue;

    //UI Misc
    public TMP_InputField ffmpegInputField;
    public TMP_Dropdown videosDropDown;
    public List<string> videosDropDownOptions;
    public Toggle togglePlaceRef;
    public Slider videoSlider;
    public bool isSliderDragging;
    public Canvas RightPanelUI;
    public Canvas HelpUI;

    void Start()
    {
        logQueue = new Queue<string>();
        
        string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        videoFolder = Path.Combine(rootPath, "RawVideos");
        saveDirectory = Path.Combine(rootPath, "ProcessedVideos");

        if (!Directory.Exists(videoFolder))
        {
            Directory.CreateDirectory(videoFolder);
            UnityEngine.Debug.Log("Created RawVideos folder since it does not exists:" + videoFolder);
        }

        //初始化窗口
        ChangeHelpState(false);

        //初始化Dropdown
        videosDropDown.onValueChanged.AddListener(OnVideoDropdownValChanged);
        InitDropdown();

        //初始化ffmpeg输入
        ffmpegInputField.onEndEdit.AddListener(OnFfmpgeInputChanged);
        ffmpegInputField.text = PlayerPrefs.GetString("ffmpegPath", "");
        if (!string.IsNullOrEmpty(ffmpegInputField.text) && File.Exists(ffmpegInputField.text))
        {
            ffmpegExePath = PlayerPrefs.GetString("ffmpegPath");
        }

        //初始化ref
        togglePlaceRef.onValueChanged.AddListener(SetRefState);
        togglePlaceRef.isOn = false;

        //初始化进度条
        videoSlider.onValueChanged.AddListener(OnSliderValChanged);
        EventTrigger trigger = videoSlider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = videoSlider.gameObject.AddComponent<EventTrigger>();

            //进度条的拖拽监听;
        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDownEntry.callback.AddListener((data) => { isSliderDragging = true; });
        trigger.triggers.Add(pointerDownEntry);

        EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUpEntry.callback.AddListener((data) => { isSliderDragging = false; });
        trigger.triggers.Add(pointerUpEntry);

        // 准备缩略图渲染纹理，自动处理Resize操作
        thumbnailRT = new RenderTexture(originalRT.width / 4, originalRT.height / 4, 0, RenderTextureFormat.ARGB32);
        tempTex = new Texture2D(originalRT.width / 4, originalRT.height / 4, TextureFormat.ARGB32, false);

        videoPlayer.enabled = true;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady += OnFrameReady;
    }

    public void StartFindLoop()
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
        {
            AddPlayerLog("视频路径错误或文件不存在:" + videoPath, "#ff0000");
            return;
        }
        StartCoroutine(FindLoopRoutine());
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        isFrameReady = true;
        Graphics.Blit(originalRT, thumbnailRT);
    }

    private IEnumerator FindLoopRoutine()  //修一下这个seek
    {
        if (isProcessing)
        {
            AddPlayerLog("已经正在自动选取结尾帧，不能进重复进行这一操作", "#ff0000");
            yield break;
        }

        isProcessing = true;
        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;
        }
        
        long totalFrames = (long)videoPlayer.frameCount;
        fps = videoPlayer.frameRate;
        float totalDuration = totalFrames / fps;
        
        AddPlayerLog($"已经载入完成视频: {Path.GetFileName(videoPath)}, fps:{fps}, 总帧数:{totalFrames}");

        videoPlayer.Pause(); // 暂停以确保精准读取当前帧

        bool seekDone = false;
        VideoPlayer.EventHandler onSeek = (vp) => { seekDone = true; };

        if (videoPlayer.frame != startFrame)
        {
            videoPlayer.seekCompleted += onSeek;
            videoPlayer.frame = startFrame;
            yield return new WaitUntil(() => seekDone);
            videoPlayer.seekCompleted -= onSeek;
        }
        
        yield return null; 
        yield return new WaitForEndOfFrame(); 

        Graphics.Blit(originalRT, thumbnailRT);

        firstFrameVector = GetImageVectorFromRT();
        firstFrameNorm = CalculateNorm(firstFrameVector);
        AddPlayerLog($"已提取第 {startFrame} 帧作为基准关键帧");

        float highestSim = 0f;
        long bestMatchFrame = startFrame;
        bool found = false;

        //跳过一些与基准帧接近的帧
        long checkStartFrame = startFrame + 10;
        if (checkStartFrame >= totalFrames) 
        {
            checkStartFrame = totalFrames - 1;
        }
        
        seekDone = false;
        if (videoPlayer.frame != checkStartFrame - 1)
        {
            videoPlayer.seekCompleted += onSeek;
            videoPlayer.frame = checkStartFrame - 1;
            yield return new WaitUntil(() => seekDone);
            videoPlayer.seekCompleted -= onSeek;
        }

        // 逐帧步进寻找循环点 
        for (long frame = checkStartFrame; frame < totalFrames; ++frame)
        {
            videoPlayer.StepForward(); // 向前步进一帧
            isFrameReady = false;
            yield return new WaitUntil(() => isFrameReady);

            float[] currentVector = GetImageVectorFromRT();
            float simValue = CalculateCosineSimilarity(firstFrameVector, currentVector, firstFrameNorm);

            // 记录最高相似度
            if (simValue > highestSim)
            {
                highestSim = simValue;
                bestMatchFrame = frame;
            }

            if (frame % 300 == 0) 
            {
                UnityEngine.Debug.Log($"处理进度: {frame} / {totalFrames}");
            }
        }

        if (highestSim >= thresholdSimValue)
            {
                found = true;
                float p_start_time = startFrame / fps;
                float p_end_time = bestMatchFrame / fps;
                float p_len_time = p_end_time - p_start_time;

                AddPlayerLog($"匹配成功\n" +
                                      $"循环起止时间：{p_start_time:F2}s ~ {p_end_time:F2}s\n" +
                                      $"起止帧：{startFrame} ~ {bestMatchFrame}\n" +
                                      $"总时长：{p_len_time:F2}s, 相似度：{highestSim:F4}", "#33ff00");

                endFrame = bestMatchFrame;
                hasLastFrame = true;
            }

        if (!found)
        {
            AddPlayerLog($"未找到循环点,但是最高相似度发生在第 {bestMatchFrame} 帧, 相似度: {highestSim:F4}", "#ff0000");
            SeekToFrame(bestMatchFrame);
        }
        isProcessing = false;
    }

    /// <summary>
    /// 从RenderTexture读取像素，转换为灰度，并返回一维向量
    /// </summary>
    private float[] GetImageVectorFromRT()
    {
        RenderTexture.active = thumbnailRT;
        tempTex.ReadPixels(new Rect(0, 0, thumbnailSize, thumbnailSize), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;

        Color32[] pixels = tempTex.GetPixels32();
        float[] vector = new float[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            // 将RGB转为灰度值
            float gray = (pixels[i].r + pixels[i].g + pixels[i].b) / 3f;
            vector[i] = gray;
        }

        return vector;
    }

    /// <summary>
    /// 计算向量的范数 (L2 Norm)
    /// </summary>
    private float CalculateNorm(float[] vector)
    {
        float sum = 0f;
        for (int i = 0; i < vector.Length; i++)
            sum += vector[i] * vector[i];
        return Mathf.Sqrt(sum);
    }

    /// <summary>
    /// 计算两个向量的余弦相似度
    /// </summary>
    private float CalculateCosineSimilarity(float[] vecA, float[] vecB, float normA)
    {
        float dotProduct = 0f;
        float normB = CalculateNorm(vecB);

        if (normA == 0 || normB == 0) return 0f;

        for (int i = 0; i < vecA.Length; i++)
        {
            dotProduct += vecA[i] * vecB[i];
        }

        return dotProduct / (normA * normB);
    }

    /// <summary>
    /// 使用FFmpeg截取视频
    /// </summary>
    public void CutVideo()
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }

        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        if (!hasLastFrame)
        {
            AddPlayerLog("还没有选择结尾帧！", "#ff0000");
            return;
        }

        if (isCutting)
        {
            AddPlayerLog("已经正在截取视频，不能重复进行这一操作", "#ff0000");
            return;
        }
        isCutting = true;

        string fileName = Path.GetFileNameWithoutExtension(videoPath) + "_loop.mp4";
        string savePath = Path.Combine(saveDirectory, fileName);

        double beginSec = (double)startFrame / fps;
        double endSec = (double)endFrame / fps;

        string filterComplex = "";
        string mapArgs = "";


        filterComplex = $"[0:v]trim=start_frame={startFrame}:end_frame={endFrame},setpts=PTS-STARTPTS[v];" +
                            $"[0:a]atrim=start={beginSec:F6}:end={endSec:F6},asetpts=PTS-STARTPTS[a]";
            mapArgs = "-map \"[v]\" -map \"[a]\"";


        string cmdArgs = $"-y -i \"{videoPath}\" -filter_complex \"{filterComplex}\" {mapArgs} -c:v libx264 -crf 18 -preset fast -c:a aac -b:a 320k \"{savePath}\"";
        UnityEngine.Debug.Log("开始调用FFmpeg进行截取: ffmpeg " + cmdArgs);

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = ffmpegExePath,
            Arguments = cmdArgs,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            Process process = Process.Start(psi);
            
            process.EnableRaisingEvents = true; 
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            StartCoroutine(WaitForFFmpegProcess(process, savePath));
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("FFmpeg 运行失败，请检查路径。错误信息：" + ex.Message);
        }
        isCutting = false;
    }

    private IEnumerator WaitForFFmpegProcess(Process process, string savePath)
    {
        while (!process.HasExited)
        {
            yield return null; 
        }
        AddPlayerLog($"截取完成！文件保存在：{savePath}", "#33ff00");
        process.Dispose(); 
    }

    public void AddPlayerLog(string message, string color)
    {
        string coloredMessage = $"<color={color}>{message}</color>";
        EnqueueAndDisplay(coloredMessage);
    }

    public void AddPlayerLog(string message)
    {
        AddPlayerLog(message, "#ffffff");
    }

    public void SeekToFrame(long targetFrame)
    {
        if (!videoPlayer.isPrepared)
        {
            UnityEngine.Debug.LogWarning("Can't seek when the video is not ready");
            return;
        }

        long totalFrames = (long)videoPlayer.frameCount;

        if (targetFrame < 0) 
        {
            targetFrame = 0;
        }
        else if (targetFrame >= totalFrames) 
        {
            targetFrame = totalFrames - 1;
        }

        videoPlayer.frame = targetFrame;
        
        // videoPlayer.Pause(); 
    }

    public void SeekToPercentage(float percentage)
    {
        if (!videoPlayer.isPrepared)
        {
            UnityEngine.Debug.LogWarning("Can't seek when the video is not ready");
            AddPlayerLog("视频尚未准备好", "#ffe600");
            return;
        }

        if (percentage > 1)
        {
            percentage = 1f;
        }else if (percentage < 0)
        {
            percentage = 0;
        }

        long totalFrames = (long)videoPlayer.frameCount;

        SeekToFrame((long)(totalFrames * percentage));
    }

    private void EnqueueAndDisplay(string message)
    {
        logQueue.Enqueue(message);

        if (logQueue.Count > maxLogLines)
        {
            logQueue.Dequeue();
        }

        logText.text = string.Join("\n", logQueue);

        //强制刷新UI并滚动到底部 
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void InitDropdown()
    {
        videosDropDown.ClearOptions();

        if (videoFolder.Length == 0)
        {
            return;
        }

        List<string> newOptions = new List<string>();

        string[] videos = Directory.GetFiles(videoFolder);

        foreach (string path in videos)
        {
            newOptions.Add(Path.GetFileName(path));
        }

        videosDropDownOptions = newOptions;
        videosDropDown.AddOptions(newOptions);

        if (videos.Length > 0)
        {
            videosDropDown.value = 0;
        }
    }

    public void UpdateDropdown()
    {
        string[] videos = Directory.GetFiles(videoFolder);
        List<string> newOptions = new List<string>();

        foreach (string path in videos)
        {
            newOptions.Add(Path.GetFileName(path));
        }

        if (!newOptions.SequenceEqual(videosDropDownOptions))
        {
            videosDropDown.ClearOptions();
            videosDropDown.AddOptions(newOptions);
            videosDropDownOptions = newOptions;
            if (videos.Length > 0)
            {
                videosDropDown.value = 0;
            }
        }
    }

    public void OnVideoDropdownValChanged(int index)
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        string selectedText = videosDropDown.options[index].text;

        AddPlayerLog("选中了视频：" + selectedText);

        string newVideoPath = Path.Combine(videoFolder, selectedText);

        if (File.Exists(newVideoPath))
        {
            videoPath = newVideoPath;
            videoPlayer.url = newVideoPath;
        }else
        {
            AddPlayerLog("这一视频在目录中不存在: " + newVideoPath, "#ff0000");
            UnityEngine.Debug.LogWarning("Trying to choose a non-existing video:" + newVideoPath);
        }
    }

    public void OnFfmpgeInputChanged(string userInput)
    {
        userInput = userInput.Replace("\"", "").Trim();
        UnityEngine.Debug.Log("Input path:" + userInput);
        if (File.Exists(userInput))
        {
            ffmpegExePath = userInput;
            PlayerPrefs.SetString("ffmpegPath", ffmpegExePath);
            AddPlayerLog("设置了ffmpeg路径:" + userInput, "#33ff00");
        }else
        {
            AddPlayerLog("不存在的ffmpeg路径:" + userInput, "#ff0000");
        }
    }

    public void ChooseCurrentAsStart()
    {
        if (videoPlayer.isPlaying)
        {
            AddPlayerLog("不能在视频正在播放时选取", "#ff0000");
            return;
        }
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        if (startFrame < 0 || startFrame > (long)videoPlayer.frameCount)
        {
            AddPlayerLog($"选择了无效的帧数: {startFrame}", "#ff0000");
            return;
        }
        startFrame = videoPlayer.frame;
        Graphics.Blit(originalRT, referenceRT);
        AddPlayerLog($"选取了第 {videoPlayer.frame} 帧作为起始");
    }

    public void ChooseCurrenAsEnd()
    {
        if (videoPlayer.isPlaying)
        {
            AddPlayerLog("不能在视频正在播放时选取", "#ff0000");
            return;
        }
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        endFrame = videoPlayer.frame;
        AddPlayerLog($"选取了第 {videoPlayer.frame} 帧作为起始");
    }

    public void SetRefState(bool state)
    {
        if (state)
        {
            referenceIMG.enabled = true;
        }else
        {
            referenceIMG.enabled = false;
        }
    }

    public void OnSliderValChanged(float percentage)
    {
        SeekToPercentage(percentage);
    }

    public void PlayVid()
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        StartCoroutine(PrepareAndPlayRoutine());
    }

    private IEnumerator PrepareAndPlayRoutine()
    {
        videoPlayer.enabled = true;
        if (!videoPlayer.isPrepared)
        {
            AddPlayerLog("正在载入视频...");
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }
            AddPlayerLog("视频载入完成");
        }

        videoPlayer.Play();
        yield return null; 
    }

    public void PauseVid()
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        videoPlayer.Pause();
    }

    public void NextFrameVid()
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        if (videoPlayer.isPlaying)
        {
            AddPlayerLog("不能在播放时逐帧操作", "#ff0000");
            return;
        }
        videoPlayer.StepForward();
    }

    public void LastFrameVid()
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        if (videoPlayer.isPlaying)
        {
            AddPlayerLog("不能在播放时逐帧操作", "#ff0000");
            return;
        }
        long currentFrame = videoPlayer.frame;
        if (currentFrame > 0)
        {
            videoPlayer.frame = currentFrame - 1;
        }
    }

    public void ChangeHelpState(bool stateType)
    {
        if (isProcessing)
        {
            AddPlayerLog("正在自动选取结尾帧，不能进行这一操作", "#ff0000");
            return;
        }
        if (stateType)
        {
            RightPanelUI.enabled = false;
            HelpUI.enabled = true;
        }else
        {
            RightPanelUI.enabled = true;
            HelpUI.enabled = false;
        }
    }

    //在返回时调用
    public void ClearState()
    {
        videoPlayer.Pause();
        isSliderDragging = false;
    }

    void Update()
    {
        //更新进度条
        if (videoPlayer != null && videoPlayer.isPrepared && !isProcessing && !isSliderDragging)
        {
            long totalFrames = (long)videoPlayer.frameCount;
            if (totalFrames > 0)
            {
                float progress = (float)videoPlayer.frame / totalFrames;
                
                videoSlider.SetValueWithoutNotify(progress);
            }
        }
    }

    private void OnDestroy()
    {
        if (thumbnailRT != null) thumbnailRT.Release();
        if (tempTex != null) Destroy(tempTex);
    }
}
