// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using UnityEngine.Networking;
// using System.Text;
// using System.Text.RegularExpressions; // 【新增】引用正则表达式命名空间

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

//     [Header("阅读体验设置")]
//     [Tooltip("一句话最少显示几秒，防止短句一闪而过")]
//     public float minSentenceDuration = 0.2f; 
//     [Tooltip("每个字增加多少秒显示时间")]
//     public float secondsPerCharacter = 0.1f; 

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
    
//     // 用于控制当前的播放序列
//     private Coroutine displayCoroutine;

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

//     // ... 省略未修改的辅助方法 (UpdateCharacterDisplay, ToggleSettingsPanel 等) ...
//     // ... 请保持原来的这些方法不变，为了节省篇幅我只列出核心修改部分 ...
    
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

//     // ================= 核心修改区域 =================

//     void OnSendClick()
//     {
//         if (string.IsNullOrEmpty(inputField.text)) return;

//         string userText = inputField.text;
        
//         // 1. UI 显示：只显示纯净的文本，不显示 </no_think>
//         ShowCurrentDialogue(userText, true);
//         CreateChatBubble(userText, true);

//         inputField.text = ""; 
//         inputField.ActivateInputField(); 

//         // 2. 发送给后台：静默添加 </no_think>
//         // 这样存入历史记录和发送给 API 的数据都包含这个指令
//         messagesHistory.Add(new ChatMessage { role = "user", content = userText + "</no_think>" });
        
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
//                 ShowCurrentDialogue(errorMsg, false); 
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

//                 // 3. 处理 <think> 标签逻辑
//                 // RegexOptions.Singleline 让点号(.)可以匹配换行符，确保多行思考也能被捕获
//                 string thinkPattern = @"<think>(.*?)</think>";
//                 Match match = Regex.Match(aiText, thinkPattern, RegexOptions.Singleline);

//                 if (match.Success)
//                 {
//                     // (1) 后台打印思考过程
//                     string thoughtContent = match.Groups[1].Value;
//                     Debug.Log("<color=cyan>【AI 思考过程】:</color> " + thoughtContent);

//                     // (2) 从最终文本中移除思考块
//                     aiText = aiText.Replace(match.Value, "");
//                 }

//                 // 4. 清洗数据：去除可能残留的首尾空格
//                 aiText = aiText.Trim();

//                 // 5. 如果移除思考后内容为空（极端情况），给一个默认回复
//                 if (string.IsNullOrEmpty(aiText)) aiText = "...";

//                 // 存入历史记录（存清洗后的，还是存原始带think的？通常存清洗后的以免上下文混乱）
//                 messagesHistory.Add(new ChatMessage { role = "assistant", content = aiText });
                
//                 CreateChatBubble(aiText, false);
//                 ShowCurrentDialogue(aiText, false);
//             }
//         }
//         catch (System.Exception e)
//         {
//             ShowCurrentDialogue("解析出错", false);
//             CreateChatBubble("解析响应出错: " + e.Message, false);
//         }
//     }

//     // ===============================================

//     void ShowCurrentDialogue(string text, bool isUser)
//     {
//         if (currentDialoguePanel != null && currentDialogueText != null)
//         {
//             if (displayCoroutine != null) StopCoroutine(displayCoroutine);

//             currentDialoguePanel.SetActive(true);
            
//             if (currentDialogueScrollRect != null) 
//             {
//                 Canvas.ForceUpdateCanvases();
//                 currentDialogueScrollRect.verticalNormalizedPosition = 1f;
//             }

//             if (isUser)
//             {
//                 currentDialogueText.color = Color.white; 
//                 currentDialogueText.text = text; 
//             }
//             else
//             {
//                 currentDialogueText.color = new Color(1f, 1f, 0.8f); 
//                 displayCoroutine = StartCoroutine(DisplayAIResponseSequence(text));
//             }
//         }
//     }

//     IEnumerator DisplayAIResponseSequence(string fullText)
//     {
//         // 增加对中文顿号、分号的支持，分句更自然
//         string[] sentences = fullText.Split(new char[] { '。', '！', '？', '!', '?', '\n', '；', '、' }, System.StringSplitOptions.RemoveEmptyEntries);

//         if (sentences.Length == 0) sentences = new string[] { fullText };

//         foreach (string sentence in sentences)
//         {
//             currentDialogueText.text = sentence;

//             Canvas.ForceUpdateCanvases();
//             if (currentDialogueScrollRect != null) currentDialogueScrollRect.verticalNormalizedPosition = 1f;

//             float duration = minSentenceDuration + (sentence.Length * secondsPerCharacter);
//             yield return new WaitForSeconds(duration);
//         }

//         currentDialoguePanel.SetActive(false);
//         displayCoroutine = null;
//     }

//     void CreateChatBubble(string text, bool isUser)
//     {
//         if (messagePrefab == null || chatContent == null) return;
//         GameObject bubble = Instantiate(messagePrefab, chatContent);
//         TMP_Text textComp = bubble.GetComponentInChildren<TMP_Text>();
//         if (textComp != null)
//         {
//             textComp.text = (isUser ? "我: " : "AI: ") + text;
//             textComp.color = isUser ? Color.white : Color.yellow;
//         }
//         Canvas.ForceUpdateCanvases();
//         if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
//     }
// }