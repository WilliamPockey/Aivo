// using System.Collections;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Net.WebSockets;
// using System.Threading;
// using System.Text;
// using System;

// public class ChatManager : MonoBehaviour
// {
//     [Header("基础 UI 组件")]
//     public TMP_InputField inputField;
//     public Button sendButton;
//     public Transform chatContent;
//     public GameObject messagePrefab;
//     public ScrollRect scrollRect;
    
//     [Header("对话显示组件 (字幕)")]
//     public GameObject currentDialoguePanel; 
//     public TMP_Text currentDialogueText;
//     public ScrollRect currentDialogueScrollRect;

//     [Header("历史与设置 UI")]
//     public GameObject chatHistoryPanel;       
//     public Button toggleHistoryButton;        
//     public GameObject settingsPanel;          
//     public Button openSettingsButton;         
//     public Button saveSettingsButton;         
//     public TMP_InputField apiUrlInput;        
//     public TMP_InputField modelNameInput;     

//     [Header("人物与音频")]
//     public GameObject[] characters;
//     public AudioSource audioSource;

//     [Header("优化设置")]
//     [Tooltip("用户发送消息后，至少等待多少秒才开始播放AI语音")]
//     public float minUserDisplayTime = 4.0f; 
//     [Tooltip("语音播放速度")]
//     public float audioPlaybackSpeed = 0.9f;

//     private ClientWebSocket _cws;
//     private CancellationTokenSource _cts;
//     private string _serverUrl = "ws://10.82.1.242:8082/ws/chat";

//     private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
//     private Queue<DialogueFragment> _playbackQueue = new Queue<DialogueFragment>();
//     private bool _isPlaying = false;
//     private Coroutine _hidePanelCoroutine;

//     private float _lastUserSendTime = 0f;
//     private bool _isWaitingForBuffer = false; 

//     private class DialogueFragment
//     {
//         public string Text;
//         public AudioClip Clip;
//     }

//     [Serializable]
//     private class WSResponse
//     {
//         public string type;
//         public string text;
//         public string audio;
//     }

//     void Start()
//     {
//         if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
//         audioSource.playOnAwake = false;
//         audioSource.pitch = audioPlaybackSpeed;

//         sendButton.onClick.AddListener(OnSendClick);
//         inputField.onSubmit.AddListener(delegate { OnSendClick(); });

//         if (toggleHistoryButton != null)
//             toggleHistoryButton.onClick.AddListener(OnToggleHistoryClick);

//         if (saveSettingsButton != null)
//             saveSettingsButton.onClick.AddListener(OnSaveSettingsClick);
        
//         if (openSettingsButton != null)
//             openSettingsButton.onClick.AddListener(ToggleSettingsPanel);

//         if (chatHistoryPanel != null) chatHistoryPanel.SetActive(false);
//         if (settingsPanel != null) settingsPanel.SetActive(false);
//         if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);

//         if (GlobalSettings.Instance != null)
//         {
//             if (apiUrlInput != null) apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//             if (modelNameInput != null) modelNameInput.text = GlobalSettings.Instance.modelName;
//         }

//         UpdateCharacterDisplay();
//         ConnectToWebSocket();
//     }

//     // --- WebSocket ---
//     async void ConnectToWebSocket()
//     {
//         _cws = new ClientWebSocket();
//         _cts = new CancellationTokenSource();
//         try {
//             await _cws.ConnectAsync(new Uri(_serverUrl), _cts.Token);
//             Debug.Log("WebSocket 连接成功！");
//             ReceiveLoop();
//         } catch (Exception e) { Debug.LogError("WS连接失败: " + e.Message); }
//     }

//     async void ReceiveLoop()
//     {
//         var buffer = new byte[1024 * 1024]; 
//         while (_cws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
//         {
//             try {
//                 var result = await _cws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
//                 if (result.MessageType == WebSocketMessageType.Close) break;
//                 string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);
//                 HandleReceivedMessage(jsonString);
//             } catch { break; }
//         }
//     }

//     void HandleReceivedMessage(string json)
//     {
//         _mainThreadActions.Enqueue(() => 
//         {
//             try 
//             {
//                 WSResponse response = JsonUtility.FromJson<WSResponse>(json);

//                 // 【修复1】核心过滤：如果文本是空的或者全是空格/换行，直接丢弃，不进队列
//                 if (string.IsNullOrWhiteSpace(response.text)) 
//                 {
//                     return; 
//                 }

//                 if (response.type == "audio_sentence")
//                 {
//                     byte[] audioBytes = Convert.FromBase64String(response.audio);
//                     AudioClip clip = WavUtility.ToAudioClip(audioBytes);
//                     _playbackQueue.Enqueue(new DialogueFragment { Text = response.text, Clip = clip });
//                 }
//                 else if (response.type == "text_only")
//                 {
//                     _playbackQueue.Enqueue(new DialogueFragment { Text = response.text, Clip = null });
//                 }
//             }
//             catch (Exception e) { Debug.LogWarning("解析错误: " + e.Message); }
//         });
//     }

//     void Update()
//     {
//         while (_mainThreadActions.TryDequeue(out Action action)) action.Invoke();
//         ProcessPlaybackQueue();
//     }

//     void ProcessPlaybackQueue()
//     {
//         if (_isPlaying && audioSource.isPlaying) return;

//         if (_playbackQueue.Count > 0)
//         {
//             if (_isWaitingForBuffer)
//             {
//                 float timePassed = Time.time - _lastUserSendTime;
//                 if (timePassed < minUserDisplayTime) return; 
//                 _isWaitingForBuffer = false;
//             }

//             if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);

//             _isPlaying = true;
//             DialogueFragment fragment = _playbackQueue.Dequeue();

//             ShowSubtitle(fragment.Text, false); 
            
//             // 【双重保险】再次确认文本不为空才创建气泡
//             if (!string.IsNullOrWhiteSpace(fragment.Text))
//             {
//                 CreateChatBubble(fragment.Text, false);
//             }

//             if (fragment.Clip != null)
//             {
//                 audioSource.clip = fragment.Clip;
//                 audioSource.pitch = audioPlaybackSpeed; 
//                 audioSource.Play();
//             }
//             else
//             {
//                 StartCoroutine(WaitTextReading(Mathf.Max(1.5f, fragment.Text.Length * 0.2f)));
//             }
//         }
//         else
//         {
//             if (_isPlaying)
//             {
//                 _isPlaying = false;
//                 if (!_isWaitingForBuffer)
//                 {
//                     if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);
//                     _hidePanelCoroutine = StartCoroutine(HidePanelDelayed(3.0f));
//                 }
//             }
//         }
//     }

//     IEnumerator WaitTextReading(float duration)
//     {
//         float timer = 0;
//         while(timer < duration) { timer += Time.deltaTime; yield return null; }
//     }

//     IEnumerator HidePanelDelayed(float delay)
//     {
//         yield return new WaitForSeconds(delay);
//         if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);
//     }

//     async void OnSendClick()
//     {
//         if (string.IsNullOrEmpty(inputField.text)) return;
//         if (_cws == null || _cws.State != WebSocketState.Open) return;

//         string userText = inputField.text;
        
//         ShowSubtitle(userText, true); 
//         CreateChatBubble(userText, true);

//         _lastUserSendTime = Time.time;
//         _isWaitingForBuffer = true;

//         inputField.text = ""; 
//         inputField.ActivateInputField(); 

//         ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userText));
//         await _cws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, _cts.Token);
//     }

//     void ShowSubtitle(string text, bool isUser)
//     {
//         if (currentDialoguePanel == null) return;
//         if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);

//         currentDialoguePanel.SetActive(true);
//         currentDialogueText.text = text;
        
//         if (isUser)
//         {
//             currentDialogueText.color = Color.white;
//             _playbackQueue.Clear(); 
//             audioSource.Stop();
//             _isPlaying = false;
//         }
//         else
//         {
//             currentDialogueText.color = new Color(1f, 1f, 0.8f);
//             Canvas.ForceUpdateCanvases();
//             if (currentDialogueScrollRect != null) 
//                 currentDialogueScrollRect.verticalNormalizedPosition = 1f;
//         }
//     }

//     // --- 面板控制逻辑 ---

//     public void OnToggleHistoryClick()
//     {
//         if (chatHistoryPanel != null)
//             chatHistoryPanel.SetActive(!chatHistoryPanel.activeSelf);
//     }

//     public void ToggleSettingsPanel()
//     {
//         // 【修复2】添加调试日志，如果点了没反应请查看Console面板
//         Debug.Log("点击了设置按钮");

//         if (settingsPanel != null)
//         {
//             bool isActive = !settingsPanel.activeSelf;
//             settingsPanel.SetActive(isActive);
            
//             // 【优化】防止面板被其他UI遮挡，强制提到最上层
//             if (isActive) 
//             {
//                 Debug.Log("Setting Pannel is active");
//             }
            
//             if (isActive && GlobalSettings.Instance != null)
//             {
//                 if(apiUrlInput != null) apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//                 if(modelNameInput != null) modelNameInput.text = GlobalSettings.Instance.modelName;
//             }
//         }
//         else
//         {
//             Debug.LogError("设置面板未赋值！请检查 Inspector");
//         }
//     }

//     public void OnSaveSettingsClick()
//     {
//         if (GlobalSettings.Instance != null)
//         {
//             if(apiUrlInput != null) GlobalSettings.Instance.apiUrl = apiUrlInput.text;
//             if(modelNameInput != null) GlobalSettings.Instance.modelName = modelNameInput.text;
//         }
//         if (settingsPanel != null) settingsPanel.SetActive(false);
//     }

//     // --- 辅助方法 ---

//     public void UpdateCharacterDisplay()
//     {
//         if (GlobalSettings.Instance == null) return;
//         int index = GlobalSettings.Instance.currentCharacterIndex;
//         for(int i=0; i<characters.Length; i++)
//         {
//             if(characters[i] != null) characters[i].SetActive(i == index);
//         }
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

//     private void OnDestroy()
//     {
//         if (_cts != null) _cts.Cancel();
//         if (_cws != null) _cws.Dispose();
//     }
// }