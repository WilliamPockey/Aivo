using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // 必须引用 UI
using TMPro;
using System.Text;

public class LoadingController : MonoBehaviour
{
    [Header("UI 组件")]
    public Slider progressBar;
    public Image fillImage; // 【新增】这里需要拖入 Fill 对象的 Image 组件
    public TMP_Text statusText;

    [Header("设置")]
    public float smoothSpeed = 2f;
    // 设置一个阈值，当进度大于这个值时才显示进度条
    // 0.05f 大约是 5%，此时通常已经足够放下圆角了，你可以根据实际情况微调
    private float showThreshold = 0.05f; 

    private float targetProgress = 0f;

    [System.Serializable]
    private class TestRequestData
    {
        public string model;
        public TestMessage[] messages;
        public int max_tokens = 1;
    }

    [System.Serializable]
    private class TestMessage
    {
        public string role;
        public string content;
    }

    void Start()
    {
        progressBar.value = 0f;
        targetProgress = 0f;
        
        // 初始状态下隐藏 Fill，防止出现“瘪椭圆”
        if (fillImage != null) fillImage.enabled = false;

        StartCoroutine(CheckApiAndLoad());
    }

    void Update()
    {
        // 平滑插值
        progressBar.value = Mathf.Lerp(progressBar.value, targetProgress, Time.deltaTime * smoothSpeed);

        // 【核心逻辑】只有当进度值大于阈值时，才显示 Fill 图片
        if (fillImage != null)
        {
            // 如果当前进度 > 0.05，则显示；否则隐藏
            fillImage.enabled = progressBar.value > showThreshold;
        }
    }

    IEnumerator CheckApiAndLoad()
    {
        string url = GlobalSettings.Instance.llmUrl;
        statusText.text = "正在连接大脑...";
        
        // 第一阶段：直接设置目标为 0.2
        SetProgress(0.2f); 

        var testData = new TestRequestData
        {
            model = GlobalSettings.Instance.modelName,
            messages = new TestMessage[] { 
                new TestMessage { role = "user", content = "hi" } 
            }
        };

        string json = JsonUtility.ToJson(testData);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            statusText.text = "正在握手...";
            yield return request.SendWebRequest();

            // 请求完成后，进度推进到 0.8
            SetProgress(0.8f);
            yield return new WaitForSeconds(0.5f); 

            if (request.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "连接成功！准备进入...";
                SetProgress(1.0f);
                yield return new WaitForSeconds(2.5f);
                SceneManager.LoadScene("Main");
            }
            else
            {
                statusText.text = "连接失败: " + request.error + "\n请检查 API 地址或 VPN";
                Debug.LogError(request.downloadHandler.text);
            }
        }
    }

    private void SetProgress(float progress)
    {
        targetProgress = progress;
    }
}