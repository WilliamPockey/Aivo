using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using TMPro;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.IO;
using System;
using System.Text.RegularExpressions;
using uLipSync;
using B83.Win32;

public class ChatManager : MonoBehaviour
{
    [Header("基础 UI 组件")]
    public TMP_InputField inputField;
    public Button sendButton;
    public Transform chatContent;
    public GameObject messagePrefab;
    public ScrollRect scrollRect;
    
    [Header("对话显示组件")]
    public GameObject currentDialoguePanel; 
    public TMP_Text currentDialogueText;
    public ScrollRect currentDialogueScrollRect;

    [Header("Agent 模式开关 (UI & 设置)")]
    public Toggle agentModeToggle;          // 对应 AgentSwitch 物体
    public RectTransform switchHandleRect;  // 对应 Handle (圆球)
    public Image switchBackgroundImage;     // 对应 Background (底座)

    [Header("Agent 开关外观设置")]
    public Color switchOffColor = new Color(0.5f, 0.5f, 0.5f); // 关闭时的颜色 (灰色)
    public Color switchOnColor = new Color(0.2f, 0.8f, 0.4f);  // 开启时的颜色 (绿色)
    public float handleOffX = 2f;    // 关闭时 Handle 的 X 坐标
    public float handleOnX = 32f;    // 开启时 Handle 的 X 坐标 (根据你的宽度调整，60宽-26球-2边距 ≈ 32)
    public float switchAnimationDuration = 0.2f; // 滑动动画时间

    private bool _isAgentMode = false;

    [Header("图片上传与预览 UI")]
    public Button uploadButton;           // 上传/加号按钮
    public GameObject imagePreviewPanel;  // 包裹预览图和删除按钮的容器
    public RawImage previewImage;         // 显示图片的组件
    public Button deleteImageButton;      // 右上角的删除按钮
    
    // 用于暂存当前选中的图片数据 (暂时用 Texture2D 模拟)
    private Texture2D _selectedImage = null;

    [Header("历史与设置 UI")]
    public GameObject chatHistoryPanel;       
    public Button toggleHistoryButton;    
    public Button closeHistoryButton;    
    public GameObject settingsPanel;          
    public Button openSettingsButton;         
    public Button saveSettingsButton;
    public Button cancelSettingsButton;           

    [Header("新的设置 UI 组件")]
    public TMP_Dropdown characterDropdown;
    public TMP_InputField llmUrlInput;
    public TMP_InputField ttsUrlInput;
    public TMP_InputField systemPromptInput; // 新增：系统提示词输入框
    public TMP_InputField ttsAudioInput;     // 新增：TTS音频文件名输入框
    public TMP_Dropdown ttsAudioDropdown;    // 新增：TTS音频下拉选择 
    

    [Header("设置面板 - Tab 导航")]
    public GameObject generalSettingsTab; // 对应原来的设置内容容器
    public GameObject agentSettingsTab;   // 新增的 Agent 设置容器
    public Button btnShowGeneralTab;      // "通用设置" 按钮
    public Button btnShowAgentTab;        // "Agent设置" 按钮
    public Color tabActiveColor = Color.white;
    public Color tabInactiveColor = Color.gray;

    [Header("设置面板 - 新增输入组件")]
    public TMP_InputField agentWorkDirInput;
    public TMP_InputField agentLlmUrlInput;
    public TMP_Dropdown animatorTypeDropdown; // 选项: Female, Male
    public TMP_InputField modelNameInput;      // 对应通用设置页的 Chat Model Name
    public TMP_InputField agentModelNameInput;  // 对应 Agent 设置页的 Agent Model Name

    public Button openWorkDirButton;

    [Header("动画控制器资源")]
    public RuntimeAnimatorController femaleAnimatorController; // 拖入原来的女性Controller
    public RuntimeAnimatorController maleAnimatorController;   // 拖入新的男性Controller

    [Header("人物与音频")]
    // public GameObject[] characters;
    public AudioSource audioSource;

    [Header("角色加载器")]
    public CharacterLoader characterLoader; // <--- 新增引用

    [Header("动态 Layer 设置")]
    public string targetLayerName = "Character";

    
    
    [Header("动画与表情控制")]
    public Animator characterAnimator;
    public AnimeFaceController faceController;

    [Header("口型同步核心")]
    public uLipSync.uLipSync audioAnalysis;
    
    [Tooltip("说话动画变体数量 (TalkIndex: 0 到 N-1)")]
    public int talkAnimationCount = 3; 

    [Header("桌宠模式设置")]
    public GameObject uiRoot; // 拖入刚才创建的 UIRoot
    public float uiAutoCloseTime = 5f; // 无操作多久自动隐藏 UI
    private float _lastInteractionTime;
    private bool _isUiVisible = true;

    [Header("随机待机设置 (Random Idle)")]
    [Tooltip("你有几个额外的 Idle 动画? (对应 IdleIndex 1 到 N)")]
    public int idleVariantCount = 3; 
    public float minIdleInterval = 8f;  // 最少隔多久动一次
    public float maxIdleInterval = 15f; // 最多隔多久动一次

    [Header("优化设置")]
    public float minUserDisplayTime = 4.0f; 
    public float audioPlaybackSpeed = 0.9f;

    private ClientWebSocket _cws;
    private CancellationTokenSource _cts;
    private string _serverUrl = "ws://localhost:8082/ws/chat"; // 请根据实际情况修改

    private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
    private Queue<DialogueFragment> _playbackQueue = new Queue<DialogueFragment>();
    private bool _isPlaying = false;
    private Coroutine _hidePanelCoroutine;

    // --- [新增] 专门用于追踪开关动画的协程引用 ---
    private Coroutine _agentSwitchCoroutine;

    private float _lastUserSendTime = 0f;
    private bool _isWaitingForBuffer = false; 

    // [新增] 标记服务端是否已经生成完毕
    private bool _isServerGenerating = false; 
    // [新增] 记住当前应该保持的表情
    private string _currentKeepEmotion = "Neutral";

    private class DialogueFragment
    {
        public string Text;
        public AudioClip Clip;
        public string Emotion;
        public string Action;
    }

    [Serializable]
    private class WSResponse
    {
        public string type;
        public string text;
        public string audio;
    }

    private uLipSyncBlendShape _currentLipSyncTarget;

    void Start()
    {
        Debug.Log(">>> ChatManager Start 开始执行！");
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.pitch = audioPlaybackSpeed;

        sendButton.onClick.AddListener(OnSendClick);
        inputField.onSubmit.AddListener(delegate { OnSendClick(); });

        if (toggleHistoryButton != null) toggleHistoryButton.onClick.AddListener(OnToggleHistoryClick);
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(ToggleSettingsPanel);
        if (closeHistoryButton != null) closeHistoryButton.onClick.AddListener(CloseHistoryPanel);

        if (chatHistoryPanel != null) chatHistoryPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);
        // --- [新增] 绑定打开文件夹按钮 ---
        if (openWorkDirButton != null)
        {
            openWorkDirButton.onClick.AddListener(OnOpenWorkDirClick);
        }

        if (GlobalSettings.Instance != null)
        {
            if (llmUrlInput) llmUrlInput.text = GlobalSettings.Instance.llmUrl;
            if (ttsUrlInput) ttsUrlInput.text = GlobalSettings.Instance.ttsUrl;
            if (systemPromptInput) systemPromptInput.text = GlobalSettings.Instance.systemPrompt;
            if (ttsAudioInput) ttsAudioInput.text = GlobalSettings.Instance.ttsAudioPrompt;
        }
        // 监听下拉菜单变化
        if (ttsAudioDropdown != null)
        {
            ttsAudioDropdown.onValueChanged.AddListener(OnAudioDropdownChanged);
            // 【新增】启动时立刻执行一次，确保界面状态正确
            // 比如上次保存的是 kanami_1.wav，那输入框应该是灰色的
            OnAudioDropdownChanged(ttsAudioDropdown.value); 
            
            // 但是！这里有个小逻辑冲突：
            // 如果 GlobalSettings 加载了一个自定义的名字（比如 user_upload.wav），
            // 但 Dropdown 默认停在第一个选项（比如 kanami_1.wav），
            // 上面这行代码会把输入框覆盖成 kanami_1.wav。
            // 在RestoreUiState中进行修复
        }

        if (btnShowGeneralTab) btnShowGeneralTab.onClick.AddListener(() => SwitchSettingsTab(0));
        if (btnShowAgentTab) btnShowAgentTab.onClick.AddListener(() => SwitchSettingsTab(1));

        // --- [新增] 绑定动画类型下拉框 ---
        if (animatorTypeDropdown != null)
        {
            animatorTypeDropdown.onValueChanged.AddListener(OnAnimatorTypeChanged);
        }

        
        // 绑定保存按钮
        if (saveSettingsButton != null)
        {
            saveSettingsButton.onClick.RemoveAllListeners(); // 防止重复绑定
            saveSettingsButton.onClick.AddListener(OnSaveSettingsClick);
        }

        // 绑定取消按钮
        if (cancelSettingsButton != null)
            cancelSettingsButton.onClick.AddListener(OnCancelSettingsClick);

        // --- Agent 模式开关初始化 ---
        if (agentModeToggle != null)
        {
            // 1. 读取保存的状态 (如果没有保存过，默认为 0/False)
            bool savedState = PlayerPrefs.GetInt("AgentMode", 0) == 1;
            _isAgentMode = savedState;
            agentModeToggle.isOn = savedState;

            // 2. 立即设置视觉状态 (不播放动画，直接就位)
            UpdateSwitchVisualsImmediate(savedState);

            // 3. 监听点击 (注意：用户点击时才播放动画)
            agentModeToggle.onValueChanged.AddListener(OnAgentToggleValueChanged);
        }

        // --- [新增 Step 2] 图片上传 UI 绑定 ---
        if (uploadButton != null)
            uploadButton.onClick.AddListener(OnUploadButtonClick);

        if (deleteImageButton != null)
            deleteImageButton.onClick.AddListener(OnDeleteImageClick);

        // 确保初始状态是隐藏的
        if (imagePreviewPanel != null)
            imagePreviewPanel.SetActive(false);


        Debug.Log(">>> 按钮绑定完成，准备恢复 UI 状态...");
        // 初始化界面状态（包括模型和TTS）
        RestoreUiState();
        Debug.Log(">>> UI 状态恢复完成，准备连接 WebSocket...");

        // 默认显示通用设置页
        SwitchSettingsTab(0);

        // UnityDragAndDropHook.InstallHook();
        // UnityDragAndDropHook.OnDroppedFiles += OnFilesDropped;

        // --- 【修改后】启动延迟挂载协程 ---
        StartCoroutine(InstallHookDelayed());

        // UpdateCharacterDisplay();
        
        // 自动获取 Animator
        // if (characterAnimator == null && characters.Length > 0 && characters[0] != null)
        //     characterAnimator = characters[0].GetComponent<Animator>();
        if (characterLoader != null)
        {
            // 初始化下拉菜单
            SetupCharacterDropdown();
            
            // 当加载器换人时，ChatManager 自动更新引用
            characterLoader.OnCharacterChanged += OnCharacterLoadedHandler;
        }

        // 【修复 1】启动时重置计时器，防止 UI 刚启动就自动关闭
        _lastInteractionTime = Time.time;

        // 启动 WebSocket
        ConnectToWebSocket();
        Debug.Log(">>> WebSocket 连接指令已发出");

        // 启动随机待机协程
        StartCoroutine(RandomIdleRoutine());
    }

    // 这个函数既用于 Start 初始化，也用于点击“取消”时还原界面
    void RestoreUiState()
    {
        if (GlobalSettings.Instance == null) return;

        // 1. 还原输入框文本
        if (llmUrlInput) llmUrlInput.text = GlobalSettings.Instance.llmUrl;
        if (ttsUrlInput) ttsUrlInput.text = GlobalSettings.Instance.ttsUrl;
        if (systemPromptInput) systemPromptInput.text = GlobalSettings.Instance.systemPrompt;
        // --- [新增] 还原 Agent 设置 ---
        if (agentWorkDirInput) agentWorkDirInput.text = GlobalSettings.Instance.agentWorkDir;
        if (agentLlmUrlInput) agentLlmUrlInput.text = GlobalSettings.Instance.agentLlmUrl;

        if (modelNameInput) modelNameInput.text = GlobalSettings.Instance.modelName;
        if (agentModelNameInput) agentModelNameInput.text = GlobalSettings.Instance.agentModelName;

        // 2. 还原模型选择 (解决你的问题 1)
        if (characterDropdown != null)
        {
            int savedIndex = GlobalSettings.Instance.characterIndex;
            // 设置下拉菜单的值
            characterDropdown.value = savedIndex;
            // 强制刷新 Dropdown 显示
            characterDropdown.RefreshShownValue();
            
            // 强制让 CharacterLoader 加载这个角色
            // (防止 Dropdown 初始值一样时不触发 onValueChanged)
            if (characterLoader != null)
            {
                characterLoader.LoadCharacter(savedIndex);
            }
        }

        // --- [新增] 还原动画控制器选择 ---
        if (animatorTypeDropdown) 
        {
            animatorTypeDropdown.value = GlobalSettings.Instance.animatorType;
            // 注意：这里不需要手动调用 OnAnimatorTypeChanged，
            // 因为在 OnCharacterLoadedHandler 里我们会根据 GlobalSettings 最终确定动画
        }

        // 3. 还原 TTS 下拉菜单和输入框状态 (之前的逻辑移到这里)
        if (GlobalSettings.Instance == null || ttsAudioDropdown == null) return;
        
        string savedAudio = GlobalSettings.Instance.ttsAudioPrompt;
        bool foundInList = false;

        // 遍历寻找匹配项
        for (int i = 0; i < ttsAudioDropdown.options.Count; i++)
        {
            if (ttsAudioDropdown.options[i].text == savedAudio)
            {
                ttsAudioDropdown.value = i;
                foundInList = true;
                break;
            }
        }

        // 如果没找到，说明是自定义
        if (!foundInList)
        {
            ttsAudioDropdown.value = ttsAudioDropdown.options.Count - 1; // 假设最后一项是 Custom
            if (ttsAudioInput) ttsAudioInput.text = savedAudio;
        }
        else
        {
            // 如果找到了，也要把输入框填上，保持一致
            if (ttsAudioInput) ttsAudioInput.text = savedAudio;
        }

        // 刷新变灰/变亮的状态
        OnAudioDropdownChanged(ttsAudioDropdown.value);
    }

    // --- 当角色加载器完成工作时，会自动调用这个方法 ---
    void OnCharacterLoadedHandler(GameObject newCharacter, uLipSyncBlendShape lipSyncReceiver)
    {
        Debug.Log("收到角色加载完成通知，开始绑定 ChatManager 逻辑");

        characterAnimator = newCharacter.GetComponent<Animator>();
        faceController = newCharacter.GetComponent<AnimeFaceController>();

        AssignLayerToCharacter(newCharacter);

        // 应用当前的 Animator Controller (男/女)
        ApplyAnimatorControllerToCurrentCharacter();

        // 绑定口型
        if (audioAnalysis != null && lipSyncReceiver != null)
        {
            if (_currentLipSyncTarget != null)
                audioAnalysis.onLipSyncUpdate.RemoveListener(_currentLipSyncTarget.OnLipSyncUpdate);

            _currentLipSyncTarget = lipSyncReceiver;
            audioAnalysis.onLipSyncUpdate.AddListener(_currentLipSyncTarget.OnLipSyncUpdate);
        }

        // 强制触发动画
        if (characterAnimator != null)
        {
            characterAnimator.SetBool("IsTalking", false);
            characterAnimator.Play("Idle", 0, 0f); // 立即播放
        }
    }

    IEnumerator KickstartAnimation()
    {
        // 等待 0.1秒，确保 Animator Rebind 完成
        yield return new WaitForSeconds(0.1f);
        
        if (characterAnimator != null)
        {
            // 设置初始参数，防止状态机卡住
            characterAnimator.SetInteger("IdleIndex", 0);
            characterAnimator.SetBool("IsTalking", false);
            // 触发 Idle
            characterAnimator.Play("Idle", 0, 0f); // 强制播放 Idle 状态
        }
    }

    void SetupCharacterDropdown()
    {
        if (characterDropdown == null || characterLoader == null) return;

        characterDropdown.ClearOptions();
        // 从 Loader 获取名字列表
        characterDropdown.AddOptions(characterLoader.GetCharacterNames());

        if (GlobalSettings.Instance != null)
        {
            characterDropdown.value = GlobalSettings.Instance.currentCharacterIndex;
        }

        characterDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDropdownChanged(int index)
    {
        // 通知 Loader 换人
        characterLoader.LoadCharacter(index);
    }

    // --- 随机待机逻辑 (解决问题 1) ---
    IEnumerator RandomIdleRoutine()
    {
        while (true)
        {
            // 随机等待一段时间
            float waitTime = UnityEngine.Random.Range(minIdleInterval, maxIdleInterval);
            yield return new WaitForSeconds(waitTime);

            // 只有在【不说话】且【没有在播放动作】且 Animator 存在时才触发
            // 注意：这里简单的用 !isPlaying 判定。更严谨可以用 animator.GetCurrentAnimatorStateInfo
            if (!_isPlaying && !_isWaitingForBuffer && characterAnimator != null && idleVariantCount > 0)
            {
                // 随机选择 0 到 idleVariantCount - 1
                int randIdle = UnityEngine.Random.Range(0, idleVariantCount);
                
                characterAnimator.SetInteger("IdleIndex", randIdle);
                characterAnimator.SetTrigger("TriggerIdleVariant");
                
                // 稍微重置一下参数，防止一直卡在这个 Index (虽然 Trigger 会自动复位)
                // 这里的 SetInteger 其实保留着也没事，只要 Trigger 复位了就行
            }
        }
    }

    // --- WebSocket ---
    async void ConnectToWebSocket()
    {
        _cws = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        try {
            await _cws.ConnectAsync(new Uri(_serverUrl), _cts.Token);
            Debug.Log("WebSocket 连接成功！");

            // 【新增】连接成功后，立即把本地保存的配置同步给后端
            // 这样就不怕后端重启了
            SyncConfigToBackend();

            ReceiveLoop();
        } catch (Exception e) { Debug.LogError("WS连接失败: " + e.Message); }
    }

    // 把原本 OnSaveSettingsClick 里发送的部分抽离出来，变成一个独立方法
    private async void SyncConfigToBackend()
    {
        if (GlobalSettings.Instance == null) return;
        if (_cws == null || _cws.State != WebSocketState.Open) return;

        // 从 GlobalSettings 获取当前保存的数据
        string newLlm = GlobalSettings.Instance.llmUrl;
        string newTts = GlobalSettings.Instance.ttsUrl;
        string newSys = GlobalSettings.Instance.systemPrompt;
        string newAudio = GlobalSettings.Instance.ttsAudioPrompt;

        // 构造发送包
        ConfigPayload payload = new ConfigPayload();
        payload.type = "config_update";
        payload.content = new ConfigContent();
        payload.content.llmUrl = newLlm;
        payload.content.ttsUrl = newTts;
        payload.content.systemPrompt = newSys;
        payload.content.ttsAudioPrompt = newAudio;

        payload.content.modelName = GlobalSettings.Instance.modelName;
        payload.content.agentLlmUrl = GlobalSettings.Instance.agentLlmUrl;
        payload.content.agentModelName = GlobalSettings.Instance.agentModelName;
        payload.content.agentWorkDir = GlobalSettings.Instance.agentWorkDir;

        string safeJson = JsonUtility.ToJson(payload);
        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(safeJson));
        
        // 发送
        await _cws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, _cts.Token);
        Debug.Log("自动同步配置完成");
    }

    // 替换原有的 ReceiveLoop 方法
    async void ReceiveLoop()
    {
        var buffer = new byte[1024 * 1024]; 
        Debug.Log(">>> [调试] WebSocket 接收循环已启动 (ReceiveLoop Started)");

        while (_cws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            try {
                // 1. 等待数据
                var result = await _cws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                
                // 2. 检查是否关闭
                if (result.MessageType == WebSocketMessageType.Close) 
                {
                    Debug.LogWarning(">>> [调试] 服务端请求关闭连接！");
                    break;
                }

                // 3. 转换字符串
                string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // !!! 关键调试日志 !!!
                Debug.Log($">>> [调试] 收到原始数据包 (长度:{result.Count}):\n{jsonString}");

                // 4. 处理数据
                HandleReceivedMessage(jsonString);
            } 
            catch (Exception e) 
            { 
                // 如果这里报错，说明 Socket 连接断了
                Debug.LogError($">>> [调试] WebSocket 接收层发生严重错误: {e.Message}\n{e.StackTrace}");
                break; 
            }
        }
        Debug.Log(">>> [调试] WebSocket 接收循环已退出 (连接断开)");
    }

    // 发送音频的方法 (给 AudioInputButton 调用)
    public async void SendAudioInput(string base64Audio)
    {
        if (_cws == null || _cws.State != WebSocketState.Open) return;

        string imageBase64 = null;
        if (_selectedImage != null)
        {
            imageBase64 = TextureToBase64(_selectedImage);
            OnDeleteImageClick(); 
        }

        // 【修正】使用 WsChatMessage 发送，注意这里的 type 稍有不同
        // 但为了复用结构，我们可以用同样的类，只要 Python 端解析没问题
        // 这里需要注意：AudioInput 的 content 结构不同（它是 audio 字段而不是 text 字段）
        
        // 为了省事，我们可以定义一个通用的 payload 或者再写一个类
        // 这里建议直接用字符串拼接法（最简单粗暴且不依赖类定义），或者定义一个新的 AudioPayload 类
        
        // 方案 B：手动拼接 JSON（最稳妥，避免定义太多类）
        string json;
        if (imageBase64 != null)
        {
            // 带图片的音频
             json = $"{{\"type\":\"audio_input\",\"content\":{{\"audio\":\"{base64Audio}\",\"image\":\"{imageBase64}\"}}}}";
        }
        else
        {
            // 纯音频
             json = $"{{\"type\":\"audio_input\",\"content\":\"{base64Audio}\"}}";
        }

        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        await _cws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, _cts.Token);
    }

    void HandleReceivedMessage(string json)
    {
        _mainThreadActions.Enqueue(() => 
        {
            try 
            {

                Debug.Log(">>> [调试] 主线程开始处理消息...");
                // 【修复 3】收到任何消息时，强制唤醒 UI 并重置计时器
                // 这样就算之前 UI 隐藏了，收到回复也会自动弹出来
                if (!_isUiVisible) ToggleUI(true);
                _lastInteractionTime = Time.time; // 重置倒计时，防止刚显示就消失

                WSResponse response = JsonUtility.FromJson<WSResponse>(json);

                if (response == null)
                {
                    Debug.LogError(">>> [调试] JSON 解析结果为 NULL！请检查 JSON 格式是否匹配 WSResponse 类。");
                    return;
                }

                Debug.Log($">>> [调试] 解析成功: Type={response.type}, Text长度={response.text?.Length}");

                // --- 新增：处理用户语音转写的文字 ---
                if (response.type == "user_transcription")
                {
                    // 这里的 response.text 是后端识别出来的 "你好"
                    // 我们把它当做用户刚刚发送的文字，显示在界面上
                    ShowSubtitle(response.text, true);
                    CreateChatBubble(response.text, true);
                    
                    // 记录时间，让 AI 回复稍后播放
                    _lastUserSendTime = Time.time;
                    _isWaitingForBuffer = true;
                    return; // 处理完就返回，不用进播放队列
                }

                // 【新增】处理结束信号
                if (response.type == "end")
                {
                    Debug.Log("服务端生成结束");
                    _isServerGenerating = false; // 只有收到这个，才允许在队列为空时重置
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.text)) return; 

                // 正则提取标签
                string rawText = response.text;
                string emotionTag = "";
                string actionTag = "";

                var emotionMatch = Regex.Match(rawText, @"[\[\(](Happy|Sad|Angry|Surprised|Suspicious|Fun|Shock|Neutral)[\]\)]", RegexOptions.IgnoreCase);
                if (emotionMatch.Success)
                {
                    emotionTag = emotionMatch.Value.Trim('[', ']', '(', ')'); 
                    rawText = rawText.Replace(emotionMatch.Value, "").Trim();
                }

                var actionMatch = Regex.Match(rawText, @"[\{](Wave|Nod|Bow|Cheer)[\}]", RegexOptions.IgnoreCase);
                if (actionMatch.Success)
                {
                    actionTag = actionMatch.Value.Trim('{', '}');
                    rawText = rawText.Replace(actionMatch.Value, "").Trim();
                }

                // 【关键修改】如果提取到了表情，更新“缓存表情”
                if (!string.IsNullOrEmpty(emotionTag))
                {
                    _currentKeepEmotion = emotionTag;
                }

                DialogueFragment fragment = new DialogueFragment 
                { 
                    Text = rawText, 
                    Clip = null,
                    Emotion = emotionTag,
                    Action = actionTag
                };

                if (response.type == "audio_sentence")
                {
                    byte[] audioBytes = Convert.FromBase64String(response.audio);
                    fragment.Clip = WavUtility.ToAudioClip(audioBytes);
                }

                _playbackQueue.Enqueue(fragment);
            }
            catch (Exception e) { Debug.LogWarning("解析错误: " + e.Message); }
        });
    }

    void Update()
    {
        // 1. 优先处理主线程队列 (确保 WebSocket 消息能被执行)
        while (_mainThreadActions.TryDequeue(out Action action)) action.Invoke();
        
        // 2. 处理播放队列 (动画和语音)
        ProcessPlaybackQueue();

        // 3. 自动隐藏 UI 的逻辑 (修改版)
        if (_isUiVisible)
        {
            // 只要鼠标在 UI 上，就重置计时 (需要 EventSystem)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _lastInteractionTime = Time.time;
            }

            // ---【核心修复】---
            // 定义什么叫 "忙碌状态"：
            // 1. 正在等待服务端回复 (_isWaitingForBuffer / _isServerGenerating)
            // 2. 正在播放语音 (_isPlaying)
            // 3. 输入框正在被聚焦
            bool isBusy = _isWaitingForBuffer || _isServerGenerating || _isPlaying || (inputField != null && inputField.isFocused);

            // 只有 "不忙" 且 "超时" 时，才允许隐藏 UI
            if (!isBusy && (Time.time - _lastInteractionTime > uiAutoCloseTime))
            {
                ToggleUI(false);
            }
        }
    }

    void ProcessPlaybackQueue()
    {
        // 1. 正在播放音频中 (正常状态，直接返回)
        if (_isPlaying && audioSource.isPlaying)
        {
            return; 
        }

        // 2. 音频刚播完 (或者处于两句话的间隙)
        if (_isPlaying && !audioSource.isPlaying)
        {
            if (faceController != null)
            {
                // A. 嘴巴闭上 (防止uLipSync停止更新导致嘴巴卡住)
                faceController.ResetMouth(); 

                // 【新增 / 关键修改】 B. 表情变回 Neutral
                // 这样在等待下一句话生成的空档期，人物会自然放松，而不是僵硬地维持上一句的表情
                faceController.SetExpression("Neutral");
            }

            // 保持 "IsTalking" 为 true，这样身体的 Idle 动画不会突然打断说话动作
        }

        // 3. 尝试播放下一句
        if (_playbackQueue.Count > 0)
        {
            // 等待用户输入的缓冲 (保持你原有的逻辑)
            if (_isWaitingForBuffer)
            {
                float timePassed = Time.time - _lastUserSendTime;
                if (timePassed < minUserDisplayTime) return; 
                _isWaitingForBuffer = false;
            }

            if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);

            _isPlaying = true;
            DialogueFragment fragment = _playbackQueue.Dequeue();

            ShowSubtitle(fragment.Text, false); 
            if (!string.IsNullOrWhiteSpace(fragment.Text)) CreateChatBubble(fragment.Text, false);

            // --- 设置新一句话的表情 ---
            if (faceController != null)
            {
                // 逻辑：如果有新标签就用新的，没有就沿用缓存的 (Context)
                // 但因为我们在上面已经 Reset 成了 Neutral，
                // 所以这里 SetExpression 会触发一次从 Neutral -> 新表情 的平滑过渡，看起来更生动
                string targetEmo = !string.IsNullOrEmpty(fragment.Emotion) ? fragment.Emotion : _currentKeepEmotion;
                faceController.SetExpression(targetEmo);
                
                // 更新缓存
                if (!string.IsNullOrEmpty(fragment.Emotion)) _currentKeepEmotion = fragment.Emotion;
            }

            // 设置身体动画
            if (characterAnimator != null)
            {
                characterAnimator.SetBool("IsTalking", true);
                if (talkAnimationCount > 0) characterAnimator.SetInteger("TalkIndex", UnityEngine.Random.Range(0, talkAnimationCount));
                if (!string.IsNullOrEmpty(fragment.Action)) characterAnimator.SetTrigger(fragment.Action);
            }

            // 播放音频
            if (fragment.Clip != null)
            {
                audioSource.clip = fragment.Clip;
                audioSource.pitch = audioPlaybackSpeed; 
                audioSource.Play();
            }
            else
            {
                StartCoroutine(WaitTextReading(Mathf.Max(1.5f, fragment.Text.Length * 0.2f)));
            }
        }
        else
        {
            // --- 队列为空：所有句子都播完了 ---
            if (_isPlaying) 
            {
                // 只有服务端也结束了，才算彻底结束
                if (!_isServerGenerating)
                {
                    _isPlaying = false;
                    
                    if (characterAnimator != null)
                    {
                        characterAnimator.SetBool("IsTalking", false);
                    }

                    if (faceController != null)
                    {
                        faceController.SetExpression("Neutral");
                        faceController.ResetMouth(); // 确保彻底闭嘴
                        _currentKeepEmotion = "Neutral"; 
                    }

                    if (!_isWaitingForBuffer)
                    {
                        if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);
                        _hidePanelCoroutine = StartCoroutine(HidePanelDelayed(3.0f));
                    }
                }
                else
                {
                    // 队列空了但还在等生成：
                    // 因为我们在上面第2步已经 SetExpression("Neutral") 了，
                    // 所以这里人物会保持 Neutral 乖乖等待，完美符合你的要求！
                    if (faceController != null) faceController.ResetMouth();
                }
            }
        }
    }

    IEnumerator WaitTextReading(float duration)
    {
        float timer = 0;
        while(timer < duration) { timer += Time.deltaTime; yield return null; }
    }

    IEnumerator HidePanelDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentDialoguePanel != null) currentDialoguePanel.SetActive(false);
    }

    async void OnSendClick()
    {
        // 1. 基础校验 (允许只有图片 或 只有文字)
        bool hasText = !string.IsNullOrEmpty(inputField.text);
        bool hasImage = _selectedImage != null;

        if (!hasText && !hasImage) return; // 什么都没有就不发
        if (_cws == null || _cws.State != WebSocketState.Open) return;

        string userText = inputField.text;
        
        // UI 显示
        string displayMsg = userText;
        if (string.IsNullOrEmpty(displayMsg) && hasImage) displayMsg += "[发送了一张图片]";
        ShowSubtitle(displayMsg, true); 
        CreateChatBubble(displayMsg, true);

        // 状态重置
        _lastUserSendTime = Time.time;
        _isWaitingForBuffer = true;
        _isServerGenerating = true; 
        _currentKeepEmotion = "Neutral"; // Instruct 模型通常不需要这个，但保留无妨

        inputField.text = ""; 
        inputField.ActivateInputField(); 

        // 停止播放
        _playbackQueue.Clear(); 
        audioSource.Stop();
        _isPlaying = false;
        if (characterAnimator != null) characterAnimator.SetBool("IsTalking", false);
        if (faceController != null) faceController.SetExpression("Neutral");

        // --- 图片处理 ---
        string imageBase64 = null;
        if (hasImage)
        {
            imageBase64 = TextureToBase64(_selectedImage);
            OnDeleteImageClick(); // 发送完清除 UI
        }

        // --- 【核心修正】使用显式类进行序列化 ---
        WsChatMessage msg = new WsChatMessage();
        msg.type = "chat";
        msg.content = new WsChatContent();
        msg.content.text = userText;
        msg.content.image = imageBase64; // 如果无图，这里是 null，JsonUtility 不会由它产生错误

        msg.content.isAgentMode = _isAgentMode;

        // 序列化
        string json = JsonUtility.ToJson(msg);
        
        // 调试：你可以取消注释下面这行来看看现在的 JSON 对不对
        // Debug.Log("发送的 JSON: " + json);

        _lastInteractionTime = Time.time;
        ToggleUI(true); // 确保 UI 是打开的

        ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        await _cws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, _cts.Token);
    }

    void ShowSubtitle(string text, bool isUser)
    {
        if (currentDialoguePanel == null) return;
        if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);

        currentDialoguePanel.SetActive(true);
        currentDialogueText.text = text;
        
        if (isUser)
        {
            currentDialogueText.color = Color.white;
        }
        else
        {
            currentDialogueText.color = new Color(1f, 1f, 0.8f);
            Canvas.ForceUpdateCanvases();
            if (currentDialogueScrollRect != null) 
                currentDialogueScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void OnToggleHistoryClick()
    {
        if (chatHistoryPanel != null)
        {
            bool isActive = !chatHistoryPanel.activeSelf;
            chatHistoryPanel.SetActive(isActive);
            
            // 如果打开了历史记录，强制刷新一下 ScrollView 的位置到底部
            if (isActive && scrollRect != null)
            {
                // 需要延迟一帧刷新，否则 LayoutGroup 可能还没算好高度
                StartCoroutine(ForceScrollDown());
            }
        }
        else
        {
            Debug.LogError("ChatHistoryPanel 未绑定！请在 Inspector 中赋值。");
        }
    }
    public void CloseHistoryPanel()
    {
        if (chatHistoryPanel != null)
            chatHistoryPanel.SetActive(false);
    }
    IEnumerator ForceScrollDown()
    {
        yield return new WaitForEndOfFrame();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);

        }
    }

    // --- 处理 TTS 下拉菜单 ---
    // 确保在 Start() 里调用一次这个方法，以初始化状态
// Start() { ... ttsAudioDropdown.onValueChanged.AddListener(OnAudioDropdownChanged); OnAudioDropdownChanged(ttsAudioDropdown.value); ... }

    void OnAudioDropdownChanged(int index)
    {
        if (ttsAudioDropdown == null || ttsAudioInput == null) return;

        string selectedOption = ttsAudioDropdown.options[index].text;
        
        // 判断是否选择了 "Custom" (或者你设定的 "自定义")
        // 建议在 Inspector 里把 Dropdown 的最后一项名字设为 "Custom"
        bool isCustom = (selectedOption == "Custom" || selectedOption == "自定义");

        if (isCustom)
        {
            // === 情况 A: 选择了自定义 ===
            
            // 1. 允许输入 (变亮)
            ttsAudioInput.interactable = true;
            
            // 2. 清空内容，方便用户输入
            ttsAudioInput.text = ""; 
            
            // 3. (可选) 自动激活输入框，弹起键盘或光标聚焦
            ttsAudioInput.ActivateInputField();
        }
        else
        {
            // === 情况 B: 选择了预设音频 ===
            
            // 1. 自动填入选中的文件名
            ttsAudioInput.text = selectedOption;
            
            // 2. 禁止修改 (变暗)
            ttsAudioInput.interactable = false;
        }
    }

    // --- 修改保存设置逻辑 ---
    public void OnSaveSettingsClick()
    {
        if (GlobalSettings.Instance != null)
        {
            // 1. 保存到本地 PlayerPrefs
            string newLlm = llmUrlInput != null ? llmUrlInput.text : "";
            string newTts = ttsUrlInput != null ? ttsUrlInput.text : "";
            string newSys = systemPromptInput != null ? systemPromptInput.text : "";
            string newAudio = ttsAudioInput != null ? ttsAudioInput.text : "";

            string newModelName = modelNameInput != null ? modelNameInput.text : "";
            string newAgentModelName = agentModelNameInput != null ? agentModelNameInput.text : "";

            // 获取当前选中的角色索引
            int newCharIndex = characterDropdown != null ? characterDropdown.value : 0;

            // --- [新增] 获取新的变量 ---
            string newAgentDir = agentWorkDirInput != null ? agentWorkDirInput.text : "";
            string newAgentLlm = agentLlmUrlInput != null ? agentLlmUrlInput.text : "";
            int newAnimType = animatorTypeDropdown != null ? animatorTypeDropdown.value : 0;

            
            // --- [修改] 调用新的 SaveSettings 方法 ---
            GlobalSettings.Instance.SaveSettings(newLlm, newTts, newSys, newAudio, newCharIndex, 
                                                 newAgentDir, newAgentLlm, newAnimType,
                                                 newModelName, newAgentModelName);

            Debug.Log("设置已保存到本地");

            // 如果修改了动画类型，立即应用到当前角色
            ApplyAnimatorControllerToCurrentCharacter();

            // 2. 发送给 Python 后端
            SyncConfigToBackend();
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnCancelSettingsClick()
    {
        // 1. 还原所有 UI 到上次保存的状态
        RestoreUiState();
        
        // 2. 关闭面板
        if (settingsPanel != null) settingsPanel.SetActive(false);
        
        Debug.Log("已取消修改，还原配置");
    }
    
    // 辅助类用于 JSON 序列化
    [Serializable]
    public class ConfigPayload
    {
        public string type;
        public ConfigContent content;
    }
    [Serializable]
    public class ConfigContent
    {
        public string llmUrl;
        public string ttsUrl;
        public string systemPrompt;
        public string ttsAudioPrompt;

        public string modelName;
        public string agentLlmUrl;
        public string agentModelName;
        public string agentWorkDir;
    }
    
    void CreateChatBubble(string text, bool isUser)
    {
        if (messagePrefab == null || chatContent == null) return;
        GameObject bubble = Instantiate(messagePrefab, chatContent);
        TMP_Text textComp = bubble.GetComponentInChildren<TMP_Text>();
        if (textComp != null)
        {
            textComp.text = (isUser ? "我: " : "AI: ") + text;
            textComp.color = isUser ? Color.white : Color.yellow;
        }
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    // --- 切换动画核心逻辑 ---
    private IEnumerator AnimateSwitch(bool isOn)
    {
        float timer = 0f;
        
        // 获取起始状态
        float startX = switchHandleRect.anchoredPosition.x;
        float targetX = isOn ? handleOnX : handleOffX;
        
        Color startColor = switchBackgroundImage.color;
        Color targetColor = isOn ? switchOnColor : switchOffColor;

        while (timer < switchAnimationDuration)
        {
            timer += Time.deltaTime;
            float t = timer / switchAnimationDuration;
            // 使用 SmoothStep 让动画更自然
            t = Mathf.SmoothStep(0.0f, 1.0f, t);

            // 1. 移动滑块
            Vector2 newPos = switchHandleRect.anchoredPosition;
            newPos.x = Mathf.Lerp(startX, targetX, t);
            switchHandleRect.anchoredPosition = newPos;

            // 2. 改变背景颜色
            switchBackgroundImage.color = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        // 确保最终位置准确
        Vector2 finalPos = switchHandleRect.anchoredPosition;
        finalPos.x = targetX;
        switchHandleRect.anchoredPosition = finalPos;
        switchBackgroundImage.color = targetColor;
    }
    // --- 立即设置状态 (用于初始化) ---
    void UpdateSwitchVisualsImmediate(bool isOn)
    {
        if (switchHandleRect == null || switchBackgroundImage == null) return;

        // 设置位置
        Vector2 pos = switchHandleRect.anchoredPosition;
        pos.x = isOn ? handleOnX : handleOffX;
        switchHandleRect.anchoredPosition = pos;

        // 设置颜色
        switchBackgroundImage.color = isOn ? switchOnColor : switchOffColor;
    }

    // --- Toggle 点击回调 ---
    void OnAgentToggleValueChanged(bool isOn)
    {
        _isAgentMode = isOn;
        
        // 保存状态
        PlayerPrefs.SetInt("AgentMode", isOn ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"Agent Mode 切换: {isOn}");

        // --- [优化后] 安全的动画触发逻辑 ---
        
        // 1. 如果当前正在播放滑动动画，先停止它，防止冲突
        if (_agentSwitchCoroutine != null)
        {
            StopCoroutine(_agentSwitchCoroutine);
        }

        // 2. 启动新的动画，并记录下来
        _agentSwitchCoroutine = StartCoroutine(AnimateSwitch(isOn));
    }

    // --- 图片处理逻辑 ---

    // 1. 点击上传按钮触发
    // void OnUploadButtonClick()
    // {
    //     // 过滤器格式: "描述\0*.后缀\0描述\0*.后缀"
    //     // 例如: "Image Files\0*.png;*.jpg;*.jpeg\0All Files\0*.*"
    //     string filter = "Image Files\0*.png;*.jpg;*.jpeg\0";

    //     WindowsFileBrowser.OpenFile("选择一张图片", filter, (path) => 
    //     {
    //         // 这是回调函数，当用户选中文件后执行
    //         StartCoroutine(LoadImageFromPath(path));
    //     });
    // }
    void OnUploadButtonClick()
    {
        // 修改过滤器，加入 VRM 支持
        string filter = "Supported Files\0*.png;*.jpg;*.jpeg;*.vrm;\0";

        WindowsFileBrowser.OpenFile("选择文件 (图片或 VRM 模型)", filter, (path) => 
        {
            if (string.IsNullOrEmpty(path)) return;

            string ext = Path.GetExtension(path).ToLower();

            // === 处理图片 ===
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                StartCoroutine(LoadImageFromPath(path));
            }
            // === 处理 VRM 模型 (新增保存到 Models 逻辑) ===
            else if (ext == ".vrm") 
            {
                HandleModelUpload(path);
            }
        });
    }

    private void HandleModelUpload(string sourcePath)
    {
        try
        {
            // 1. 确定工作目录 (优先从输入框获取最新值)
            string workDir = "";
            if (agentWorkDirInput != null && !string.IsNullOrEmpty(agentWorkDirInput.text))
                workDir = agentWorkDirInput.text.Trim();
            else if (GlobalSettings.Instance != null)
                workDir = GlobalSettings.Instance.agentWorkDir;

            if (string.IsNullOrEmpty(workDir))
            {
                ShowSubtitle("请先在设置中配置 Agent 工作目录！", false);
                return;
            }

            // 2. 建立 Models 文件夹路径
            string modelsFolder = Path.Combine(workDir, "Models");
            if (!Directory.Exists(modelsFolder))
            {
                Directory.CreateDirectory(modelsFolder);
            }

            // 3. 构建目标路径 (Models/文件名.vrm)
            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(modelsFolder, fileName);

            // 4. 执行文件拷贝 (如果已在目标位置则不重复拷贝)
            if (Path.GetFullPath(sourcePath) != Path.GetFullPath(destPath))
            {
                File.Copy(sourcePath, destPath, true); // true 表示覆盖同名文件
                Debug.Log($"模型已备份至: {destPath}");
            }

            // 5. 让 CharacterLoader 加载新路径下的模型
            if (characterLoader != null)
            {
                characterLoader.LoadCharacterFromFile(destPath);
                
                // 成功反馈
                // ShowSubtitle($"模型 {fileName} 已导入工作区并加载成功！", false);
                // if (characterAnimator != null) characterAnimator.SetTrigger("Nod"); 
                
                // 6. 自动刷新下拉菜单，让新模型出现在列表中
                RefreshCharacterDropdown(fileName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"模型处理失败: {e.Message}");
            ShowSubtitle("模型保存失败，请检查目录权限。", false);
        }
    }

    private void RefreshCharacterDropdown(string newlyAddedName)
    {
        if (characterDropdown == null || characterLoader == null) return;

        // 重新获取角色列表 (假设 CharacterLoader 会扫描本地文件夹)
        characterDropdown.ClearOptions();
        var names = characterLoader.GetCharacterNames();
        characterDropdown.AddOptions(names);

        // 找到刚刚添加的模型在列表中的索引并选中它
        int newIndex = names.FindIndex(n => n.Contains(newlyAddedName));
        if (newIndex != -1)
        {
            characterDropdown.value = newIndex;
            characterDropdown.RefreshShownValue();
        }
    }

    // --- 新增：从路径加载图片的协程 ---
    // --- [修正版] 从路径加载图片的协程 ---
    IEnumerator LoadImageFromPath(string path)
    {
        // 加上 file:// 前缀
        string url = "file:///" + path;
        
        // 【修正点】直接使用 UnityWebRequestTexture，不需要 UnityEngine.Network 前缀
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            // Unity 2020+ 使用 Result.Success，旧版本用 !uwr.isNetworkError && !uwr.isHttpError
            if (uwr.result == UnityWebRequest.Result.Success)
            {
                // 【修正点】直接使用 DownloadHandlerTexture
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                
                ShowSelectedImage(texture);
                Debug.Log("已加载图片: " + path);
            }
            else
            {
                Debug.LogError("图片加载失败: " + uwr.error);
            }
        }
    }

    // 2. 显示图片的通用方法
    public void ShowSelectedImage(Texture2D texture)
    {
        if (texture == null) return;

        _selectedImage = texture;
        
        if (previewImage != null)
        {
            previewImage.texture = texture;
            // 保持宽高比 (可选)
            // float ratio = (float)texture.width / texture.height;
            // previewImage.rectTransform.sizeDelta = new Vector2(200, 200 / ratio);
        }

        if (imagePreviewPanel != null)
        {
            imagePreviewPanel.SetActive(true); // 显示面板
        }
    }

    // 3. 点击删除按钮触发
    void OnDeleteImageClick()
    {
        Debug.Log("移除选中的图片");
        
        // 清除数据
        _selectedImage = null;
        if (previewImage != null) previewImage.texture = null;

        // 隐藏面板
        if (imagePreviewPanel != null)
        {
            imagePreviewPanel.SetActive(false);
        }
    }

    // 【新增】图片转 Base64 辅助方法
    string TextureToBase64(Texture2D texture)
    {
        if (texture == null) return null;
        // 压缩为 JPG 格式，质量 75，减少网络传输压力
        // 注意：texture 必须是 Readable 的。如果是从 WebRequest 加载的通常没问题。
        byte[] bytes = texture.EncodeToJPG(75); 
        return Convert.ToBase64String(bytes);
    }
    // --- [新增] Tab 切换逻辑 ---
    void SwitchSettingsTab(int tabIndex)
    {
        bool isGeneral = (tabIndex == 0);
        if (generalSettingsTab) generalSettingsTab.SetActive(isGeneral);
        if (agentSettingsTab) agentSettingsTab.SetActive(!isGeneral);

        // 更新按钮颜色示意当前选中状态
        if (btnShowGeneralTab) btnShowGeneralTab.image.color = isGeneral ? tabActiveColor : tabInactiveColor;
        if (btnShowAgentTab) btnShowAgentTab.image.color = !isGeneral ? tabActiveColor : tabInactiveColor;
    }
    // --- [新增] 下拉框变化回调 ---
    void OnAnimatorTypeChanged(int index)
    {
        // 仅仅是UI变化，实际应用在保存时，或者你可以选择在这里直接预览（可选）
        // 这里暂时不立即应用，因为用户可能会点击"取消"设置。
        // 如果你想实时预览，可以取消下面这行的注释：
        // ApplyAnimatorControllerToCurrentCharacter(index); 
    }

    // --- [新增] 执行动画控制器替换 ---
    void ApplyAnimatorControllerToCurrentCharacter()
    {
        if (characterAnimator == null) return;
        if (GlobalSettings.Instance == null) return;

        int type = GlobalSettings.Instance.animatorType;
        
        // 如果想要支持实时预览（未保存时），可以传参进来覆盖 GlobalSettings 的值
        // 但为了简单，我们以 GlobalSettings 为准
        
        RuntimeAnimatorController targetController = (type == 0) ? femaleAnimatorController : maleAnimatorController;

        if (targetController != null)
        {
            characterAnimator.runtimeAnimatorController = targetController;
            // 重新绑定 FaceController 或其他可能依赖 Animator 的组件
            // 注意：替换 Controller 后，Animator 的状态机不仅重置，参数也会重置
            // 所以我们需要确保 Idle 等参数被重新触发
            if (characterAnimator.gameObject.activeInHierarchy)
            {
                characterAnimator.Rebind();
            }
            Debug.Log($"已切换动画控制器为: {(type == 0 ? "Female" : "Male")}");
        }
    }
    
    // --- [新增] 打开工作目录逻辑 ---
    void OnOpenWorkDirClick()
    {
        // 1. 优先获取输入框的文本，如果为空则获取全局设置
        string path = "";
        if (agentWorkDirInput != null && !string.IsNullOrEmpty(agentWorkDirInput.text))
        {
            path = agentWorkDirInput.text.Trim();
        }
        else if (GlobalSettings.Instance != null)
        {
            path = GlobalSettings.Instance.agentWorkDir;
        }

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            // 2. 处理相对路径 (例如 "./workspace") 转为 绝对路径
            // 这样 Windows 资源管理器才能正确识别
            string fullPath = Path.GetFullPath(path);

            // 3. 如果文件夹不存在，自动创建它 (防止报错)
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"目录不存在，已自动创建: {fullPath}");
            }

            // 4. 打开文件夹
            // Application.OpenURL("file://...") 是 Unity 最通用的跨平台方式
            // 它会自动调用 Windows 资源管理器 或 Mac Finder
            Application.OpenURL("file://" + fullPath);
            
            Debug.Log($"正在打开文件夹: {fullPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"打开文件夹失败: {e.Message}");
        }
    }

    public void OnFileDroppedOnCharacter(string[] filePaths)
{
    if (filePaths == null || filePaths.Length == 0) return;

    string workDir = "";
    // 获取工作目录 (优先使用输入框，其次全局设置)
    if (agentWorkDirInput != null && !string.IsNullOrEmpty(agentWorkDirInput.text))
        workDir = agentWorkDirInput.text.Trim();
    else if (GlobalSettings.Instance != null)
        workDir = GlobalSettings.Instance.agentWorkDir;

    if (string.IsNullOrEmpty(workDir))
    {
        ShowSubtitle("请先在设置中配置 Agent 工作目录！", false); // 使用你的字幕系统提示
        return;
    }

    // 确保目录存在
    if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);

    int count = 0;
    foreach (var path in filePaths)
    {
        if (File.Exists(path))
        {
            string fileName = Path.GetFileName(path);
            string destPath = Path.Combine(workDir, fileName);
            try
            {
                File.Copy(path, destPath, true); // true = 覆盖同名文件
                count++;
            }
            catch (Exception e) { Debug.LogError("文件复制失败: " + e.Message); }
        }
    }

    // 触发反馈动画和语音
    if (characterAnimator != null) characterAnimator.SetTrigger("Cheer"); // 假设有个开心动作
    ShowSubtitle($"成功接收了 {count} 个文件！已放入工作区。", false);
    if (_hidePanelCoroutine != null) StopCoroutine(_hidePanelCoroutine);
        _hidePanelCoroutine = StartCoroutine(HidePanelDelayed(3.0f));
    
        // 这里甚至可以构造一个特殊的 Prompt 发送给 LLM，让它知道收到了文件
    }

    // --- [新增功能 1] UI 显隐逻辑 ---
    public void ToggleUI(bool show)
    {
        _isUiVisible = show;
        
        if (uiRoot != null) 
        {
            // 使用 CanvasGroup 来隐藏通常比 SetActive(false) 更安全，
            // 但如果用 SetActive，请确保 uiRoot 不是 GameManager 的父物体
            uiRoot.SetActive(show);
        }

        if (show)
        {
            _lastInteractionTime = Time.time;
            // 只有显式打开时才聚焦，收到消息自动弹开时不一定要聚焦，以免打断用户打字
            // if (inputField != null) inputField.ActivateInputField(); 
        }
    }

    void OnFilesDropped(List<string> aFiles, POINT aPos)
    {
        // 将 List<string> 转为 array 调用我们之前写的逻辑
        OnFileDroppedOnCharacter(aFiles.ToArray());
    }

    // --- 【新增】 辅助方法：递归设置 Layer ---
    void AssignLayerToCharacter(GameObject root)
    {
        // 1. 获取 Layer 的 ID
        int layerID = LayerMask.NameToLayer(targetLayerName);
        
        // 检查 Layer 是否存在
        if (layerID == -1)
        {
            Debug.LogError($"Layer '{targetLayerName}' 不存在！请在 Unity 编辑器 -> Layers -> Edit Layers 中添加该 Layer。");
            return;
        }

        // 2. 递归设置所有子物体
        SetLayerRecursively(root, layerID);
        Debug.Log($"已将角色 {root.name} 及其子物体设置为 Layer: {targetLayerName} ({layerID})");
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer; // 设置当前物体

        // 遍历所有子物体
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    // --- 【新增】延迟挂载钩子 ---
    IEnumerator InstallHookDelayed()
    {
        // 等待 2 秒，确保 DesktopPetController 彻底完成了窗口去边框和透明化操作
        // 这个时间必须比 SystemIntegration 的 0.5 秒更长，确保一切尘埃落定
        yield return new WaitForSeconds(2.0f);

        try 
        {
            Debug.Log(">>> 正在尝试延迟挂载文件拖拽钩子...");
            UnityDragAndDropHook.InstallHook();
            UnityDragAndDropHook.OnDroppedFiles += OnFilesDropped;
            Debug.Log(">>> 文件拖拽钩子挂载成功！");
        }
        catch (Exception e)
        {
            Debug.LogError($">>> 钩子挂载失败 (可能是窗口句柄冲突): {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (_cts != null) _cts.Cancel();
        if (_cws != null) _cws.Dispose();

        UnityDragAndDropHook.UninstallHook();
    }

    // 【新增】用于 JSON 序列化的数据结构
    [Serializable]
    public class WsChatMessage
    {
        public string type;
        public WsChatContent content;
    }

    [Serializable]
    public class WsChatContent
    {
        public string text;
        public string image; // 允许为 null

        public bool isAgentMode;
    }
}