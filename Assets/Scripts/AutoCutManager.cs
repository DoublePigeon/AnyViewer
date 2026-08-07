using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Video;
using TMPro;
using UnityEngine.UI;
using System;
using System.Linq;
using Unity.VisualScripting;

[Serializable]
public class AutoCutManager : MonoBehaviour
{
    public string videoFolder = "";
    public string videoPath;
    public string ffmpegExePath;
    public string saveDirectory;

    public long startFrame = 0;
    public float thresholdSimValue = 0.98f; //0~1
    public int thumbnailSize = 320;

    public bool isTranscodeMode = true;

    //组件与缓存
    public VideoPlayer videoPlayer;
    public RenderTexture originalRT;
    private RenderTexture thumbnailRT;
    private Texture2D tempTex;
    
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
    public InputField ffmpegInputField;
    public TMP_Dropdown videosDropDown;
    public List<string> videosDropDownOptions;

    void Start()
    {
        logQueue = new Queue<string>();
        videoPlayer = GetComponent<VideoPlayer>();
        
        string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        string videoFolder = Path.Combine(rootPath, "RawVideos");

        if (!Directory.Exists(videoFolder))
        {
            Directory.CreateDirectory(videoFolder);
            UnityEngine.Debug.Log("Created RawVideos folder since it does not exists:" + videoFolder);
        }

        //初始化Dropdown
        videosDropDown.onValueChanged.AddListener(OnVideoDropdownValChanged);

        //初始化ffmpeg输入
        ffmpegInputField.onEndEdit.AddListener(OnFfmpgeInputChanged);

        // 准备缩略图渲染纹理，自动处理Resize操作
        thumbnailRT = new RenderTexture(originalRT.width / 4, originalRT.height / 4, 0, RenderTextureFormat.ARGB32);
        tempTex = new Texture2D(originalRT.width / 4, originalRT.height / 4, TextureFormat.ARGB32, false);
        //记得加给thumbnail赋值的逻辑
/*
        videoPlayer.url = videoPath;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = thumbnailRT;
*/
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
    }

    private IEnumerator FindLoopRoutine() //记得改
    {
        UnityEngine.Debug.Log("开始准备视频...");
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared) yield return null;

        long totalFrames = (long)videoPlayer.frameCount;
        float fps = videoPlayer.frameRate;
        float totalDuration = totalFrames / fps;
        
        UnityEngine.Debug.Log($"视频准备完毕。总帧数: {totalFrames}, 帧率: {fps}");

        // 1. 获取起始关键帧
        videoPlayer.frame = startFrame;
        videoPlayer.Pause(); // 暂停以确保精准读取当前帧
        
        isFrameReady = false;
        yield return new WaitUntil(() => isFrameReady);

        firstFrameVector = GetImageVectorFromRT();
        firstFrameNorm = CalculateNorm(firstFrameVector);
        UnityEngine.Debug.Log($"已提取第 {startFrame} 帧作为基准关键帧");

        float highestSim = 0f;
        long bestMatchFrame = startFrame;
        bool found = false;

        // 2. 逐帧步进寻找循环点 (为了避开自身，从 startFrame + 10 开始寻找)
        for (long frame = startFrame + 10; frame < totalFrames; frame++)
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

            // 输出进度
            if (frame % 30 == 0) 
                UnityEngine.Debug.Log($"正在匹配第 {frame} 帧, 当前相似度: {simValue:F4}");

            // 达到阈值，匹配成功
            if (simValue >= thresholdSimValue)
            {
                found = true;
                float p_start_time = startFrame / fps;
                float p_end_time = frame / fps;
                float p_len_time = p_end_time - p_start_time;

                UnityEngine.Debug.Log($"<color=green>匹配成功！</color>\n" +
                                      $"循环起止时间：{p_start_time:F2}s ~ {p_end_time:F2}s\n" +
                                      $"起止帧：{startFrame} ~ {frame}\n" +
                                      $"总时长：{p_len_time:F2}s, 相似度：{simValue:F4}");

                // 3. 自动调用FFmpeg截取视频
                CutVideo(p_start_time, p_end_time);
                break;
            }
        }

        if (!found)
        {
            UnityEngine.Debug.LogWarning($"未找到完美循环点。最高相似度发生在第 {bestMatchFrame} 帧, 相似度: {highestSim:F4}");
        }
    }

    /// <summary>
    /// 从RenderTexture读取像素，转换为灰度，并返回一维向量
    /// 相当于Python中的 get_thum 功能
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
            // 将RGB转为灰度值 (近似人眼感知或简单的平均值计算)
            // Python代码中是 average(pixel_tuple)，这里我们取平均
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
    private void CutVideo(float beginSec, float endSec)
    {
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        string fileName = Path.GetFileNameWithoutExtension(videoPath) + "_loop.mp4";
        string savePath = Path.Combine(saveDirectory, fileName);
        float duration = endSec - beginSec;
        string cmdArgs = "";

        if (isTranscodeMode)
        {
            // 转码模式 (准确, 耗时)
            cmdArgs = $"-y -ss {beginSec} -t {duration} -i \"{videoPath}\" -c:v libx264 -c:a aac -strict experimental -b:a 640k \"{savePath}\"";
        }
        else
        {
            // Copy模式 (极快, 时间不一定完美贴合关键帧)
            cmdArgs = $"-y -accurate_seek -ss {beginSec} -t {duration} -i \"{videoPath}\" -acodec copy -vcodec copy -async 1 -avoid_negative_ts 1 \"{savePath}\"";
        }

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
            process.Exited += (sender, e) =>
            {
                UnityEngine.Debug.Log($"<color=cyan>截取完成！文件保存在：{savePath}</color>");
            };
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("FFmpeg 运行失败，请检查路径。错误信息：" + ex.Message);
        }
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

        if (newOptions.SequenceEqual(videosDropDownOptions))
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
        string selectedText = videosDropDown.options[index].text;

        AddPlayerLog("选中了视频：" + selectedText);

        string newVideoPath = Path.Combine(videoFolder, selectedText);

        if (Directory.Exists(newVideoPath))
        {
            videoPath = newVideoPath;
        }else
        {
            AddPlayerLog("这一视频在目录中不存在: " + newVideoPath, "#ff0000");
            UnityEngine.Debug.LogWarning("Trying to choose a non-existing video:" + newVideoPath);
        }
    }

    public void OnFfmpgeInputChanged(string userInput)
    {
        strip(userInput);
        UnityEngine.Debug.Log("Input path:" + userInput);
        if (Directory.Exists(userInput))
        {
            ffmpegExePath = userInput;
            AddPlayerLog("设置了ffmpeg路径:" + userInput);
        }else
        {
            AddPlayerLog("不存在的ffmpeg路径:" + userInput, "#ff0000");
        }
    }

    private void strip(string str)
    {
        for (int i = 0; i < str.Length; ++i)
        {
            if (str[i] == '"')
            {
                str.Remove(i);
            }
        }
    }

    private void OnDestroy()
    {
        if (thumbnailRT != null) thumbnailRT.Release();
        if (tempTex != null) Destroy(tempTex);
    }
}
