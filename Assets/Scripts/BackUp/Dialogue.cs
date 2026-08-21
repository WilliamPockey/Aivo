using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Text;

public class DialogueManager : MonoBehaviour
{
    public TMP_InputField userInputField;
    public Button sendButton;
    public TMP_Text llmResponseText;

    private string apiURL = "http://10.82.1.242:8000/v1/chat/completions";

    void Start()
    {
        // 检查组件引用
        if (sendButton == null)
        {
            Debug.LogError("SendButton 未在Inspector中分配！");
            return;
        }
        if (userInputField == null)
        {
            Debug.LogError("UserInputField 未在Inspector中分配！");
            return;
        }
        if (llmResponseText == null)
        {
            Debug.LogError("LLMResponseText 未在Inspector中分配！");
            return;
        }

        sendButton.onClick.AddListener(OnSendButtonClick);
        Debug.Log("DialogueManager 初始化完成");
    }

    void OnSendButtonClick()
    {
        Debug.Log("=== 按钮点击事件触发 ===");
        string userInput = userInputField.text;
        Debug.Log("用户输入内容: '" + userInput + "'");
        Debug.Log("输入是否为空: " + string.IsNullOrEmpty(userInput));
        Debug.Log("输入长度: " + userInput?.Length);

        if (!string.IsNullOrEmpty(userInput))
        {
            Debug.Log("开始协程发送请求...");
            StartCoroutine(SendDialogueRequest(userInput));
        }
        else
        {
            Debug.LogWarning("输入为空，不发送请求");
            llmResponseText.text = "请输入内容";
        }
    }

    [System.Serializable]
    private class RequestData
    {
        public string model = "Qwen/Qwen3-8B";
        public Message[] messages;
        public float temperature = 0.7f;
        public int max_tokens = 2048;
    }

    [System.Serializable]
    private class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class ResponseData
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }

    IEnumerator SendDialogueRequest(string userInput)
    {
        Debug.Log("=== 开始发送请求 ===");
        llmResponseText.text = "思考中...";

        // 准备请求数据
        RequestData requestData = new RequestData();
        requestData.messages = new Message[]
        {
            new Message { role = "user", content = userInput }
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log("发送的JSON数据: " + jsonData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest request = null;
        
        try
        {
            Debug.Log("创建UnityWebRequest...");
            request = new UnityWebRequest(apiURL, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer abc-123");

            Debug.Log("发送请求到: " + apiURL);
            yield return request.SendWebRequest();

            Debug.Log("请求完成，结果: " + request.result);

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log("原始响应: " + responseJson);
                
                try
                {
                    ResponseData responseData = JsonUtility.FromJson<ResponseData>(responseJson);
                    
                    if (responseData.choices != null && responseData.choices.Length > 0)
                    {
                        string reply = responseData.choices[0].message.content;
                        Debug.Log("解析后的回复: " + reply);
                        llmResponseText.text = reply;
                    }
                    else
                    {
                        Debug.LogWarning("choices数组为空或null");
                        llmResponseText.text = "未收到有效回复";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("JSON解析错误: " + e.Message);
                    llmResponseText.text = "解析回复时出错: " + e.Message;
                }
            }
            else
            {
                Debug.LogError("请求失败: " + request.error);
                Debug.LogError("HTTP状态码: " + request.responseCode);
                llmResponseText.text = "连接服务器失败: " + request.error;
            }
        }
        finally
        {
            if (request != null)
            {
                request.Dispose();
                Debug.Log("请求资源已释放");
            }
        }
    }
}