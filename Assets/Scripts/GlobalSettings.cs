using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance;

    [Header("默认配置")]
    public string defaultLlmUrl = "http://localhost:8000/v1";

    public string defaultModelName = "Qwen/Qwen3-VL-8B-Instruct";
    public string defaultTtsUrl = "http://localhost:7800/audio/generate-audio-file";
    public string defaultAudioPrompt = "kanami_1.wav";
    public int defaultAnimatorType = 0; // 0: Female, 1: Male
    [TextArea(5, 10)]
    public string defaultSystemPrompt = @"
    
    你是小美，是用户的AI智能助手，性格活泼可爱。
    请用口语化、简短的句子回答用户的问题。
    同时根据回复的内容情感，在段首使用圆括号添加情感标签，支持的标签只有：(Neutral), (Happy), (Sad), (Angry), (Surprised), (Troubled), (Suspicious), (Fun), (Shock)。 
    如果需要做动作(如果不是非常需要，则不要添加)，请使用花括号在情感标签后添加动作标签，支持的动作只有：{Wave}, {Yawn}, {Stomp}。
    你一次回复最多只添加一个情感标签和一个动作标签，你必须确保你的标签是存在的。
    
    "; // 可以在Inspector里填完整的

    public int defaultCharIndex = 0; // 默认第0号角色

    [Header("默认配置")]
    // --- [新增] Agent 默认配置 ---
    public string defaultAgentWorkDir = "./workspace"; 
    // Agent LLM 默认留空，逻辑是：如果为空则使用通用 LLM，否则使用自己的
    public string defaultAgentLlmUrl = "http://localhost:8000/v1"; 
    public string defaultAgentModelName = "Qwen/Qwen3-VL-8B-Instruct";
    

    // 运行时变量
    [HideInInspector] public string llmUrl;
    [HideInInspector] public string modelName; 
    [HideInInspector] public string ttsUrl;
    [HideInInspector] public string systemPrompt;
    [HideInInspector] public string ttsAudioPrompt;
    
    

    [HideInInspector] public int characterIndex; // 新增：保存当前角色的索引

    // 为了兼容你原来的 CharacterIndex
    [HideInInspector] public int currentCharacterIndex = 0;

    // --- [新增] 运行时 Agent 与 动画变量 ---
    [HideInInspector] public string agentWorkDir;
    [HideInInspector] public string agentLlmUrl;
    [HideInInspector] public string agentModelName;
    [HideInInspector] public int animatorType; // 0=女, 1=男

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 切换场景不销毁
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSettings()
    {
        llmUrl = PlayerPrefs.GetString("LLM_URL", defaultLlmUrl);
        ttsUrl = PlayerPrefs.GetString("TTS_URL", defaultTtsUrl);
        systemPrompt = PlayerPrefs.GetString("SYS_PROMPT", defaultSystemPrompt);
        ttsAudioPrompt = PlayerPrefs.GetString("TTS_PROMPT", defaultAudioPrompt);
        // 如果你需要保存 modelName
        // modelName = PlayerPrefs.GetString("MODEL_NAME", "Qwen/Qwen3-8B");
        characterIndex = PlayerPrefs.GetInt("CHAR_INDEX", defaultCharIndex);

        // --- [新增] 读取 ---
        agentWorkDir = PlayerPrefs.GetString("AGENT_WORK_DIR", defaultAgentWorkDir);
        agentLlmUrl = PlayerPrefs.GetString("AGENT_LLM_URL", defaultAgentLlmUrl);
        animatorType = PlayerPrefs.GetInt("ANIMATOR_TYPE", defaultAnimatorType);

        modelName = PlayerPrefs.GetString("MODEL_NAME", defaultModelName);
        agentModelName = PlayerPrefs.GetString("AGENT_MODEL_NAME", defaultAgentModelName);
    }

    public void SaveSettings(string newLlm, string newTts, string newSys, string newAudio, int newCharIndex, 
                             string newAgentDir, string newAgentLlm, int newAnimType,
                             string newModelName, string newAgentModelName)
    {
        llmUrl = newLlm;
        ttsUrl = newTts;
        systemPrompt = newSys;
        ttsAudioPrompt = newAudio;
        characterIndex = newCharIndex;
        
        // --- [新增] 赋值 ---
        agentWorkDir = newAgentDir;
        agentLlmUrl = newAgentLlm;
        animatorType = newAnimType;

        modelName = newModelName;
        agentModelName = newAgentModelName;

        PlayerPrefs.SetString("LLM_URL", llmUrl);
        PlayerPrefs.SetString("TTS_URL", ttsUrl);
        PlayerPrefs.SetString("SYS_PROMPT", systemPrompt);
        PlayerPrefs.SetString("TTS_PROMPT", ttsAudioPrompt);
        PlayerPrefs.SetInt("CHAR_INDEX", characterIndex);

        // --- [新增] 保存 ---
        PlayerPrefs.SetString("AGENT_WORK_DIR", agentWorkDir);
        PlayerPrefs.SetString("AGENT_LLM_URL", agentLlmUrl);
        PlayerPrefs.SetInt("ANIMATOR_TYPE", animatorType);

        PlayerPrefs.SetString("MODEL_NAME", modelName);
        PlayerPrefs.SetString("AGENT_MODEL_NAME", agentModelName);
        
        PlayerPrefs.Save();
    }
}