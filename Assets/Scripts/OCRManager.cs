using UnityEngine;
using System.Collections;
using System.IO;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine.Networking;

// 别名定义
using UnityApp = UnityEngine.Application;

public class OCRManager : MonoBehaviour
{
    [Header("UI 引用")]
    public TMPro.TMP_Text notificationText;
    public GameObject notificationPanel;

    // 窗口控制 API
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    private string tesseractExePath;
    private string tessDataPath;
    private string processPath;

    private bool isCapturing = false;
    private Coroutine hideNotificationCoroutine; // 记录当前的隐藏协程

    void Start()
    {
        string basePath = Path.Combine(UnityApp.streamingAssetsPath, "OCR");
        tesseractExePath = Path.Combine(basePath, "tesseract.exe");
        tessDataPath = Path.Combine(basePath, "tessdata");

        // 使用持久化路径，避免权限问题
        processPath = Path.Combine(UnityApp.persistentDataPath, "temp_ocr_input.png");

        if (notificationPanel) notificationPanel.SetActive(false);
    }

    public void StartScreenOCR()
    {
        if (isCapturing) return;
        StartCoroutine(CaptureRoutine());
    }

    IEnumerator CaptureRoutine()
    {
        isCapturing = true;

        IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
        
        // 1. 最小化 Unity 窗口
        ShowWindow(hWnd, SW_MINIMIZE);
        yield return new WaitForSeconds(0.2f);

        // 2. 启动截图工具
        try
        {
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"启动截图失败: {e.Message}");
            isCapturing = false;
            ShowWindow(hWnd, SW_RESTORE);
            ShowNotification("无法启动截图工具", true);
            yield break;
        }

        // 3. 恢复 Unity 窗口
        ShowWindow(hWnd, SW_RESTORE);
        SetForegroundWindow(hWnd);
        yield return new WaitForEndOfFrame();

        // === 优化部分：倒计时显示，且不自动消失 ===
        float waitTime = 5.0f;
        while (waitTime > 0)
        {
            // autoHide = false，保证气泡一直存在
            ShowNotification($"正在等待图片缓存... (剩余 {Mathf.CeilToInt(waitTime)} 秒)", false);
            yield return new WaitForSeconds(1.0f);
            waitTime -= 1.0f;
        }

        ShowNotification("正在读取剪贴板...", false); // 继续保持显示

        // 4. 处理剪贴板
        ProcessClipboardUnified();

        isCapturing = false;
    }

    void ProcessClipboardUnified()
    {
        // 使用 ClipboardHelper 获取 Texture2D (假设你有这个类)
        Texture2D texture = ClipboardHelper.GetImage();

        if (texture != null)
        {
            try
            {
                // === 优化部分：垂直翻转图片 ===
                FlipTextureVertically(texture);

                // 保存为 PNG 文件供 Tesseract 读取
                byte[] pngBytes = texture.EncodeToPNG();
                File.WriteAllBytes(processPath, pngBytes);
                
                UnityEngine.Debug.Log($"图像已保存到: {processPath}");
                
                // 销毁 Texture2D 释放内存
                Destroy(texture);

                ShowNotification("正在识别中...", false); // 保持显示
                RunTesseractAsync(processPath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"保存图片文件失败: {e.Message}");
                ShowNotification("保存文件失败", true);
            }
        }
        else
        {
            ShowNotification("未检测到截图 (或格式不支持)", true);
            UnityEngine.Debug.LogWarning("剪贴板中没有图像数据，或者获取失败。");
        }
    }

    /// <summary>
    /// 垂直翻转 Texture2D (高效算法)
    /// </summary>
    private void FlipTextureVertically(Texture2D original)
    {
        int width = original.width;
        int height = original.height;
        
        // 使用 GetPixels32 比 GetPixel 快得多
        Color32[] pixels = original.GetPixels32();
        Color32[] newPixels = new Color32[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            // 将原图的第 y 行，复制到新图的倒数第 y 行
            // Array.Copy(源数组, 源索引, 目标数组, 目标索引, 长度)
            Array.Copy(pixels, y * width, newPixels, (height - 1 - y) * width, width);
        }

        original.SetPixels32(newPixels);
        original.Apply();
    }

    async void RunTesseractAsync(string imagePath)
    {
        if (!File.Exists(tesseractExePath))
        {
            ShowNotification("找不到 Tesseract.exe", true);
            return;
        }

        string resultText = "";

        string outFileBase = Path.Combine(UnityApp.persistentDataPath, "ocr_result");
        string tExe = tesseractExePath; 
        string tData = tessDataPath;

        await Task.Run(() =>
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = tExe;
                string args = $@"""{imagePath}"" ""{outFileBase}"" -l chi_sim+eng --psm 3 --tessdata-dir ""{tData}""";

                process.StartInfo.Arguments = args;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;

                process.Start();
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(errorOutput) && process.ExitCode != 0)
                {
                    UnityEngine.Debug.LogWarning($"Tesseract Log: {errorOutput}");
                }

                string resultTxtPath = outFileBase + ".txt";
                if (File.Exists(resultTxtPath))
                {
                    resultText = File.ReadAllText(resultTxtPath).Trim();
                    File.Delete(resultTxtPath);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"OCR 运行错误: {e}");
            }
        });

        // 回到主线程处理结果
        if (!string.IsNullOrEmpty(resultText))
        {
            GUIUtility.systemCopyBuffer = resultText;
            ShowNotification("识别成功，已复制到剪贴板", true); // 成功后自动消失
            UnityEngine.Debug.Log($"OCR 结果: {resultText}");
        }
        else
        {
            ShowNotification("未能识别文字", true); // 失败后自动消失
        }
    }

    /// <summary>
    /// 显示通知
    /// </summary>
    /// <param name="msg">消息内容</param>
    /// <param name="autoHide">是否自动隐藏（true=3秒后消失，false=一直显示）</param>
    void ShowNotification(string msg, bool autoHide = true)
    {
        if (notificationPanel && notificationText)
        {
            notificationText.text = msg;
            notificationPanel.SetActive(true);

            // 只有当需要自动隐藏时，才启动协程
            if (hideNotificationCoroutine != null)
            {
                StopCoroutine(hideNotificationCoroutine);
                hideNotificationCoroutine = null;
            }

            if (autoHide)
            {
                hideNotificationCoroutine = StartCoroutine(HideNotificationDelay());
            }
        }
    }

    IEnumerator HideNotificationDelay()
    {
        yield return new WaitForSeconds(3f);
        if (notificationPanel) notificationPanel.SetActive(false);
        hideNotificationCoroutine = null;
    }

    void OnApplicationQuit()
    {
        if (File.Exists(processPath)) File.Delete(processPath);
    }
}