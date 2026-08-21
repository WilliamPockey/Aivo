using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimeFaceController : MonoBehaviour
{
    [Header("核心配置")]
    public SkinnedMeshRenderer targetMesh;
    public float transitionSpeed = 5.0f; // 稍微调快一点，响应更灵敏
    [Range(0.1f, 2.0f)]
    public float lipSyncIntensity = 1.0f; 

    [Header("自动眨眼")]
    public bool enableAutoBlink = true;
    public int blinkIndex = -1;
    public float minBlinkInterval = 2f;
    public float maxBlinkInterval = 5f;

    // 忽略列表
    public HashSet<int> ignoredIndices = new HashSet<int>();

    public enum ExpressionType { Neutral, Happy, Sad, Angry, Surprised, Fun, Joy, Shock }
    public ExpressionType currentExpression = ExpressionType.Neutral;

    [System.Serializable]
    public class EmotionPreset
    {
        public string name;
        public List<int> indices = new List<int>(); // 改用 List 方便动态添加
        public List<float> weights = new List<float>(); 
    }

    private Dictionary<ExpressionType, EmotionPreset> emotionData = new Dictionary<ExpressionType, EmotionPreset>();

    void Start()
    {
        if (targetMesh == null) targetMesh = GetComponent<SkinnedMeshRenderer>();

        // 【解决冲突的核心】: 
        // 不再注册 Fcl_ALL_Joy，而是注册 Fcl_EYE_Joy + Fcl_BRW_Joy
        // 这样嘴巴就解放出来了，不会干扰口型！
        RegisterSeparatedEmotion(ExpressionType.Happy, "Joy");
        RegisterSeparatedEmotion(ExpressionType.Sad, "Sorrow");
        RegisterSeparatedEmotion(ExpressionType.Angry, "Angry");
        RegisterSeparatedEmotion(ExpressionType.Surprised, "Surprised");
        RegisterSeparatedEmotion(ExpressionType.Fun, "Fun");
        RegisterSeparatedEmotion(ExpressionType.Joy, "Joy");

        // 震惊 = 惊讶眼 + 瞳孔缩小
        int irisIndex = FindIndex("Iris_Hide");
        int surpriseEye = FindIndex("Fcl_EYE_Surprised");
        if (irisIndex != -1 && surpriseEye != -1)
        {
            RegisterEmotion(ExpressionType.Shock, new int[] { surpriseEye, irisIndex }, new float[] { 100f, 100f });
        }

        StartCoroutine(BlinkRoutine());
    }

    void Update()
    {
        UpdateExpression();
    }

    void LateUpdate()
    {
        // 口型幅度控制
        if (targetMesh == null || ignoredIndices.Count == 0 || Mathf.Approximately(lipSyncIntensity, 1.0f)) return;
        foreach (int index in ignoredIndices)
        {
            float current = targetMesh.GetBlendShapeWeight(index);
            if (current > 0.01f) targetMesh.SetBlendShapeWeight(index, current * lipSyncIntensity);
        }
    }

    // 【新增】 供 ChatManager 调用：强制把嘴巴闭上
    // 当音频播放结束时，uLipSync 会停止更新，我们需要手动把嘴巴 Lerp 回 0
    public void ResetMouth()
    {
        if (targetMesh == null) return;
        foreach (int index in ignoredIndices)
        {
            float current = targetMesh.GetBlendShapeWeight(index);
            if (current > 0.1f)
            {
                // 快速归零
                float next = Mathf.Lerp(current, 0f, Time.deltaTime * 15f);
                targetMesh.SetBlendShapeWeight(index, next);
            }
        }
    }

    void UpdateExpression()
    {
        if (targetMesh == null) return;

        EmotionPreset targetPreset = null;
        if (currentExpression != ExpressionType.Neutral && emotionData.ContainsKey(currentExpression))
        {
            targetPreset = emotionData[currentExpression];
        }

        int count = targetMesh.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
        {
            if (i == blinkIndex || ignoredIndices.Contains(i)) continue;

            float targetWeight = 0f;
            if (targetPreset != null)
            {
                // 查找当前 BlendShape 是否在预设里
                int matchIdx = targetPreset.indices.IndexOf(i);
                if (matchIdx != -1) targetWeight = targetPreset.weights[matchIdx];
            }

            float currentWeight = targetMesh.GetBlendShapeWeight(i);
            
            // 平滑插值
            if (Mathf.Abs(currentWeight - targetWeight) > 0.01f)
            {
                float smoothWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * transitionSpeed);
                targetMesh.SetBlendShapeWeight(i, smoothWeight);
            }
            else if (currentWeight != targetWeight)
            {
                targetMesh.SetBlendShapeWeight(i, targetWeight);
            }
        }
    }

    // --- 智能注册：拆分 眼、眉、嘴 ---
    void RegisterSeparatedEmotion(ExpressionType type, string suffix)
    {
        List<int> foundIndices = new List<int>();
        List<float> foundWeights = new List<float>();

        // 1. 找眼睛 (EYE) - 必须有
        int eyeIdx = FindIndex("Fcl_EYE_" + suffix);
        if (eyeIdx != -1) { foundIndices.Add(eyeIdx); foundWeights.Add(100f); }

        // 2. 找眉毛 (BRW) - 必须有
        int brwIdx = FindIndex("Fcl_BRW_" + suffix);
        if (brwIdx != -1) { foundIndices.Add(brwIdx); foundWeights.Add(100f); }

        // 3. 找嘴巴 (MTH) - 【关键】 这里我们不加！或者加一点点？
        // 为了防止口型冲突，我们完全不加 MTH。
        // 结果：角色会做“开心的眼睛”和“开心的眉毛”，但嘴巴留给 LipSync 控制。
        // 效果：非常自然，像 V-Tuber。
        
        if (foundIndices.Count > 0)
        {
            EmotionPreset preset = new EmotionPreset();
            preset.name = type.ToString();
            preset.indices = foundIndices;
            preset.weights = foundWeights;
            emotionData[type] = preset;
            Debug.Log($"[表情拆分] {type} 已绑定 (Eye: {eyeIdx}, Brw: {brwIdx}) - 嘴巴已分离");
        }
        else
        {
            // 如果没找到拆分的，尝试回退到 ALL
            int allIdx = FindIndex("Fcl_ALL_" + suffix);
            if (allIdx != -1)
            {
                RegisterEmotion(type, new int[] { allIdx }, new float[] { 100f });
            }
        }
    }

    int FindIndex(string namePart)
    {
        for (int i = 0; i < targetMesh.sharedMesh.blendShapeCount; i++)
            if (targetMesh.sharedMesh.GetBlendShapeName(i).Contains(namePart)) return i;
        return -1;
    }

    void RegisterEmotion(ExpressionType type, int[] idx, float[] wts)
    {
        EmotionPreset preset = new EmotionPreset { name = type.ToString() };
        preset.indices.AddRange(idx);
        preset.weights.AddRange(wts);
        emotionData[type] = preset;
    }

    // Blink Routine 保持不变...
    IEnumerator BlinkRoutine() { 
        while(true) {
             if(enableAutoBlink && blinkIndex != -1) {
                 yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));
                 for(float t=0;t<1f;t+=Time.deltaTime/0.1f) { targetMesh.SetBlendShapeWeight(blinkIndex, Mathf.Lerp(0,100,t)); yield return null; }
                 for(float t=0;t<1f;t+=Time.deltaTime/0.1f) { targetMesh.SetBlendShapeWeight(blinkIndex, Mathf.Lerp(100,0,t)); yield return null; }
             } else yield return null;
        }
    }

    public void SetExpression(string expressionName)
    {
        try { currentExpression = (ExpressionType)System.Enum.Parse(typeof(ExpressionType), expressionName, true); }
        catch { currentExpression = ExpressionType.Neutral; }
    }
}