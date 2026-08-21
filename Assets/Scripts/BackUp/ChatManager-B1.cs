// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using UnityEngine.Networking;
// using System.Text;

// public class ChatManager : MonoBehaviour
// {
//     [Header("UI 组件 - 基础")]
//     public TMP_InputField inputField;
//     public Button sendButton;
    
//     [Header("UI 组件 - 历史记录")]
//     public Transform chatContent;
//     public GameObject messagePrefab;
//     public ScrollRect scrollRect;
//     public GameObject chatHistoryPanel; 
//     public Button toggleHistoryButton; 

//     [Header("UI 组件 - 当前对话字幕")]
//     public GameObject currentDialoguePanel; 
//     public TMP_Text currentDialogueText;

//     public ScrollRect currentDialogueScrollRect;    
//     // 【新增】设置 AI 回复显示的持续时间
//     public float displayDuration = 5.0f; 

//     [Header("设置面板组件")]
//     public GameObject settingsPanel;
//     public TMP_InputField apiUrlInput;
//     public TMP_InputField modelNameInput;
//     public Button saveSettingsButton; 

//     [Header("人物模型组")]
//     public GameObject[] characters;

//     // --- 数据结构 ---
//     [System.Serializable]
//     private class ChatMessage { public string role; public string content; }
//     [System.Serializable]
//     private class RequestData { public string model; public List<ChatMessage> messages; public bool stream = false; }
//     [System.Serializable]
//     private class ResponseData { public List<Choice> choices; }
//     [System.Serializable]
//     private class Choice { public ChatMessage message; }

//     private List<ChatMessage> messagesHistory = new List<ChatMessage>();
    
//     // 【新增】用于记录自动消失的协程，防止冲突
//     private Coroutine autoHideCoroutine;

//     void Start()
//     {
//         if(GlobalSettings.Instance != null) {
//             apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//             modelNameInput.text = GlobalSettings.Instance.modelName;
//         }

//         sendButton.onClick.AddListener(OnSendClick);
//         inputField.onSubmit.AddListener(delegate { OnSendClick(); });

//         if(toggleHistoryButton != null) toggleHistoryButton.onClick.AddListener(ToggleChatHistory);
//         if(saveSettingsButton != null) saveSettingsButton.onClick.AddListener(SaveAndCloseSettings);

//         UpdateCharacterDisplay();

//         messagesHistory.Add(new ChatMessage { role = "system", content = "你是香奈美，年龄19岁。是一个活泼可爱的偶像歌手" });

//         if (chatHistoryPanel != null) chatHistoryPanel.SetActive(false);
//         if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);
//     }

//     // ... UpdateCharacterDisplay, ToggleSettingsPanel, SaveAndCloseSettings, ToggleChatHistory 代码不变 ...
//     // 为了节省篇幅，这部分省略，请保留原有的 ...

//     public void UpdateCharacterDisplay()
//     {
//         if (GlobalSettings.Instance == null) return;
//         int index = GlobalSettings.Instance.currentCharacterIndex;
//         for(int i=0; i<characters.Length; i++)
//         {
//             if(characters[i] != null) characters[i].SetActive(i == index);
//         }
//     }

//     public void ToggleSettingsPanel()
//     {
//         bool isActive = !settingsPanel.activeSelf;
//         settingsPanel.SetActive(isActive);
//         if (isActive && GlobalSettings.Instance != null)
//         {
//             apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//             modelNameInput.text = GlobalSettings.Instance.modelName;
//         }
//     }

//     public void SaveAndCloseSettings()
//     {
//         if(GlobalSettings.Instance != null)
//         {
//             GlobalSettings.Instance.apiUrl = apiUrlInput.text;
//             GlobalSettings.Instance.modelName = modelNameInput.text;
//         }
//         settingsPanel.SetActive(false);
//     }

//     public void ToggleChatHistory()
//     {
//         if (chatHistoryPanel != null)
//             chatHistoryPanel.SetActive(!chatHistoryPanel.activeSelf);
//     }

//     public void SwitchNextCharacter()
//     {
//         GlobalSettings.Instance.currentCharacterIndex++;
//         if(GlobalSettings.Instance.currentCharacterIndex >= characters.Length)
//             GlobalSettings.Instance.currentCharacterIndex = 0;
//         UpdateCharacterDisplay();
//     }

//     void OnSendClick()
//     {
//         if (string.IsNullOrEmpty(inputField.text)) return;

//         string userText = inputField.text;
        
//         // 1. 【改进】立刻在底部字幕显示玩家说的话
//         ShowCurrentDialogue(userText, true);

//         CreateChatBubble(userText, true);
//         inputField.text = ""; 
//         inputField.ActivateInputField(); 

//         messagesHistory.Add(new ChatMessage { role = "user", content = userText });
//         StartCoroutine(PostRequest());
//     }

//     IEnumerator PostRequest()
//     {
//         string url = GlobalSettings.Instance.apiUrl;
//         var requestData = new RequestData
//         {
//             model = GlobalSettings.Instance.modelName,
//             messages = messagesHistory
//         };

//         string json = JsonUtility.ToJson(requestData);

//         using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
//         {
//             byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
//             request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//             request.downloadHandler = new DownloadHandlerBuffer();
//             request.SetRequestHeader("Content-Type", "application/json");

//             yield return request.SendWebRequest();

//             if (request.result == UnityWebRequest.Result.Success)
//             {
//                 HandleResponse(request.downloadHandler.text);
//             }
//             else
//             {
//                 string errorMsg = "错误: " + request.error;
//                 ShowCurrentDialogue(errorMsg, false); // 错误按 AI 消息处理
//                 CreateChatBubble(errorMsg, false);
//             }
//         }
//     }

//     void HandleResponse(string jsonResponse)
//     {
//         try
//         {
//             var response = JsonUtility.FromJson<ResponseData>(jsonResponse);
//             if(response != null && response.choices != null && response.choices.Count > 0)
//             {
//                 string aiText = response.choices[0].message.content;
                
//                 // 3. 【改进】清洗数据：去掉前后的空格和换行
//                 aiText = aiText.Trim();

//                 messagesHistory.Add(new ChatMessage { role = "assistant", content = aiText });
                
//                 CreateChatBubble(aiText, false);
                
//                 // 1. & 2. 【改进】显示 AI 回复，并开始计时消失
//                 ShowCurrentDialogue(aiText, false);
//             }
//         }
//         catch (System.Exception e)
//         {
//             ShowCurrentDialogue("解析出错", false);
//             CreateChatBubble("解析响应出错: " + e.Message, false);
//         }
//     }

//     // 【核心改进】统一控制底部字幕
//     void ShowCurrentDialogue(string text, bool isUser)
//     {
//         if (currentDialoguePanel != null && currentDialogueText != null)
//         {
//             // 如果有之前的隐藏倒计时在运行，先停止它，防止说话说一半被关掉
//             if (autoHideCoroutine != null) StopCoroutine(autoHideCoroutine);

//             currentDialoguePanel.SetActive(true);
            
//             // 设置颜色和格式区分身份
//             if (isUser)
//             {
//                 currentDialogueText.color = Color.white; // 玩家白色
//                 // 你可以在这里加前缀，也可以不加
//                 // currentDialogueText.text = "我: " + text; 
//                 currentDialogueText.text = text; 
//             }
//             else
//             {
//                 currentDialogueText.color = new Color(1f, 1f, 0.8f); // AI 也是淡黄色，区分一下
//                 // currentDialogueText.text = "AI: " + text;
//                 currentDialogueText.text = text;
                
//                 // 2. 【改进】如果是 AI 说完话，开启自动消失倒计时
//                 // 玩家说话时通常不需要消失，因为要等着 AI 回复覆盖它
//                 autoHideCoroutine = StartCoroutine(HideDialogueDelayed());
//             }
            
//             // 【新增 2】重置滚动条位置的代码加在这里
//             if (currentDialogueScrollRect != null)
//             {
//                 // 强制立即刷新 UI 布局（因为 Content Size Fitter 需要一帧来计算高度）
//                 Canvas.ForceUpdateCanvases();
                
//                 // 将滚动位置设为 1 (1 是顶部，0 是底部)
//                 currentDialogueScrollRect.verticalNormalizedPosition = 1f;
//             }
//         }
//     }

//     // 自动隐藏的协程
//     IEnumerator HideDialogueDelayed()
//     {
//         yield return new WaitForSeconds(displayDuration); // 等待 N 秒
//         if (currentDialoguePanel != null)
//         {
//             currentDialoguePanel.SetActive(false); // 隐藏面板
//         }
//     }

//     void CreateChatBubble(string text, bool isUser)
//     {
//         if (messagePrefab == null || chatContent == null) return;
//         GameObject bubble = Instantiate(messagePrefab, chatContent);
//         TMP_Text textComp = bubble.GetComponentInChildren<TMP_Text>();
//         if (textComp != null)
//         {
//             // 历史记录里保留 "我:" 和 "AI:" 前缀，方便回看
//             textComp.text = (isUser ? "我: " : "AI: ") + text;
//             textComp.color = isUser ? Color.white : Color.yellow;
//         }
//         Canvas.ForceUpdateCanvases();
//         if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
//     }
// }