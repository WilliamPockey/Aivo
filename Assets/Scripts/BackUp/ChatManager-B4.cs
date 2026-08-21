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
// using System.Text.RegularExpressions;

// public class ChatManager : MonoBehaviour
// {
//     [Header("基础 UI 组件")]
//     public TMP_InputField inputField;
//     public Button sendButton;
//     public Transform chatContent;
//     public GameObject messagePrefab;
//     public ScrollRect scrollRect;
    
//     [Header("对话显示组件")]
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
    
//     [Header("动画与表情控制")]
//     public Animator characterAnimator;
//     public AnimeFaceController faceController;
    
//     [Tooltip("说话动画变体数量 (TalkIndex: 0 到 N-1)")]
//     public int talkAnimationCount = 3; 

//     [Header("随机待机设置 (Random Idle)")]
//     [Tooltip("你有几个额外的 Idle 动画? (对应 IdleIndex 1 到 N)")]
//     public int idleVariantCount = 3; 
//     public float minIdleInterval = 8f;  // 最少隔多久动一次
//     public float maxIdleInterval = 15f; // 最多隔多久动一次

//     [Header("优化设置")]
//     public float minUserDisplayTime = 4.0f; 
//     public float audioPlaybackSpeed = 0.9f;

//     private ClientWebSocket _cws;
//     private CancellationTokenSource _cts;
//     private string _serverUrl = "ws://10.82.1.242:8082/ws/chat"; // 请根据实际情况修改

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
//         public string Emotion;
//         public string Action;
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

//         if (toggleHistoryButton != null) toggleHistoryButton.onClick.AddListener(OnToggleHistoryClick);
//         if (saveSettingsButton != null) saveSettingsButton.onClick.AddListener(OnSaveSettingsClick);
//         if (openSettingsButton != null) openSettingsButton.onClick.AddListener(ToggleSettingsPanel);

//         if (chatHistoryPanel != null) chatHistoryPanel.SetActive(false);
//         if (settingsPanel != null) settingsPanel.SetActive(false);
//         if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);

//         if (GlobalSettings.Instance != null)
//         {
//             if (apiUrlInput != null) apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//             if (modelNameInput != null) modelNameInput.text = GlobalSettings.Instance.modelName;
//         }

//         UpdateCharacterDisplay();
        
//         // 自动获取 Animator
//         if (characterAnimator == null && characters.Length > 0 && characters[0] != null)
//             characterAnimator = characters[0].GetComponent<Animator>();
        
//         // 启动 WebSocket
//         ConnectToWebSocket();

//         // 启动随机待机协程
//         StartCoroutine(RandomIdleRoutine());
//     }

//     // --- 随机待机逻辑 (解决问题 1) ---
//     IEnumerator RandomIdleRoutine()
//     {
//         while (true)
//         {
//             // 随机等待一段时间
//             float waitTime = UnityEngine.Random.Range(minIdleInterval, maxIdleInterval);
//             yield return new WaitForSeconds(waitTime);

//             // 只有在【不说话】且【没有在播放动作】且 Animator 存在时才触发
//             // 注意：这里简单的用 !isPlaying 判定。更严谨可以用 animator.GetCurrentAnimatorStateInfo
//             if (!_isPlaying && !_isWaitingForBuffer && characterAnimator != null && idleVariantCount > 0)
//             {
//                 // 随机选择 0 到 idleVariantCount - 1
//                 int randIdle = UnityEngine.Random.Range(0, idleVariantCount);
                
//                 characterAnimator.SetInteger("IdleIndex", randIdle);
//                 characterAnimator.SetTrigger("TriggerIdleVariant");
                
//                 // 稍微重置一下参数，防止一直卡在这个 Index (虽然 Trigger 会自动复位)
//                 // 这里的 SetInteger 其实保留着也没事，只要 Trigger 复位了就行
//             }
//         }
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

//                 if (string.IsNullOrWhiteSpace(response.text)) return; 

//                 // 正则提取标签
//                 string rawText = response.text;
//                 string emotionTag = "";
//                 string actionTag = "";

//                 var emotionMatch = Regex.Match(rawText, @"[\[\(](Happy|Sad|Angry|Surprised|Suspicious|Fun|Shock|Neutral)[\]\)]", RegexOptions.IgnoreCase);
//                 if (emotionMatch.Success)
//                 {
//                     emotionTag = emotionMatch.Value.Trim('[', ']', '(', ')'); 
//                     rawText = rawText.Replace(emotionMatch.Value, "").Trim();
//                 }

//                 var actionMatch = Regex.Match(rawText, @"[\{](Wave|Nod|Bow|Cheer)[\}]", RegexOptions.IgnoreCase);
//                 if (actionMatch.Success)
//                 {
//                     actionTag = actionMatch.Value.Trim('{', '}');
//                     rawText = rawText.Replace(actionMatch.Value, "").Trim();
//                 }

//                 DialogueFragment fragment = new DialogueFragment 
//                 { 
//                     Text = rawText, 
//                     Clip = null,
//                     Emotion = emotionTag,
//                     Action = actionTag
//                 };

//                 if (response.type == "audio_sentence")
//                 {
//                     byte[] audioBytes = Convert.FromBase64String(response.audio);
//                     fragment.Clip = WavUtility.ToAudioClip(audioBytes);
//                 }

//                 _playbackQueue.Enqueue(fragment);
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
//         // 如果正在播放音频，暂不处理
//         if (_isPlaying && audioSource.isPlaying) return;

//         if (_playbackQueue.Count > 0)
//         {
//             // 等待用户输入后的缓冲时间
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
//             if (!string.IsNullOrWhiteSpace(fragment.Text)) CreateChatBubble(fragment.Text, false);

//             // 1. 设置表情
//             if (faceController != null && !string.IsNullOrEmpty(fragment.Emotion))
//             {
//                 faceController.SetExpression(fragment.Emotion);
//             }

//             // 2. 设置身体动画
//             if (characterAnimator != null)
//             {
//                 // 始终保持说话状态为 True，直到整个队列播完
//                 characterAnimator.SetBool("IsTalking", true);
                
//                 // 只有当这是一个新的话题开始，或者动作发生变化时才随机切换 TalkIndex
//                 // 这里为了简单，每句话都随机一下，也可以接受
//                 if (talkAnimationCount > 0)
//                 {
//                     int randIndex = UnityEngine.Random.Range(0, talkAnimationCount);
//                     characterAnimator.SetInteger("TalkIndex", randIndex);
//                 }

//                 // 触发特定动作 (解决问题 3: 动作会覆盖 Talking，结束后由于 IsTalking=true 会自动切回 Talking)
//                 if (!string.IsNullOrEmpty(fragment.Action))
//                 {
//                     characterAnimator.SetTrigger(fragment.Action);
//                 }
//             }

//             // 播放音频
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
//             // --- 队列为空：所有句子都播完了 ---
//             if (_isPlaying)
//             {
//                 // 这里是关键修改 (解决问题 2)
//                 // 只有当真的没有下一句时，才停止说话状态
                
//                 _isPlaying = false;
                
//                 if (characterAnimator != null)
//                 {
//                     characterAnimator.SetBool("IsTalking", false);
//                 }

//                 // 表情复位
//                 if (faceController != null)
//                 {
//                     faceController.SetExpression("Neutral");
//                 }

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

//         // 用户打断：立即停止
//         _playbackQueue.Clear(); 
//         audioSource.Stop();
//         _isPlaying = false;
//         if (characterAnimator != null) characterAnimator.SetBool("IsTalking", false);
//         if (faceController != null) faceController.SetExpression("Neutral");

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
//         }
//         else
//         {
//             currentDialogueText.color = new Color(1f, 1f, 0.8f);
//             Canvas.ForceUpdateCanvases();
//             if (currentDialogueScrollRect != null) 
//                 currentDialogueScrollRect.verticalNormalizedPosition = 1f;
//         }
//     }

//     public void OnToggleHistoryClick()
//     {
//         if (chatHistoryPanel != null)
//             chatHistoryPanel.SetActive(!chatHistoryPanel.activeSelf);
//     }

//     public void ToggleSettingsPanel()
//     {
//         if (settingsPanel != null)
//         {
//             settingsPanel.SetActive(!settingsPanel.activeSelf);
//             if (settingsPanel.activeSelf && GlobalSettings.Instance != null)
//             {
//                 if(apiUrlInput != null) apiUrlInput.text = GlobalSettings.Instance.apiUrl;
//                 if(modelNameInput != null) modelNameInput.text = GlobalSettings.Instance.modelName;
//             }
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

//     public void UpdateCharacterDisplay()
//     {
//         if (GlobalSettings.Instance == null) return;
//         int index = GlobalSettings.Instance.currentCharacterIndex;
//         for(int i=0; i<characters.Length; i++)
//         {
//             if(characters[i] != null) 
//             {
//                 characters[i].SetActive(i == index);
//                 if (i == index) characterAnimator = characters[i].GetComponent<Animator>();
//             }
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