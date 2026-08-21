using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.IO;
using System.Linq; // 需要引用 Linq
using System.Threading.Tasks;
using uLipSync;
using VRM;       
using UniGLTF;   

public class CharacterLoader : MonoBehaviour
{
    [Header("容器设置")]
    public Transform characterContainer;

    [Header("通用配置")]
    public RuntimeAnimatorController sharedAnimatorController; 

    public event Action<GameObject, uLipSyncBlendShape> OnCharacterChanged;

    private RuntimeGltfInstance _currentInstance;
    
    // --- 新增：用于管理下拉菜单的文件列表 ---
    private List<string> _modelPaths = new List<string>();

    void Awake()
    {
        // 确保模型文件夹存在
        string dir = GetModelDirectory();
        Debug.Log(dir);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        // 启动时刷新列表
        RefreshModelList();
    }

    // 获取模型存放文件夹路径 (通常是 C:/Users/你的名字/AppData/LocalLow/公司名/产品名/Models)
    public string GetModelDirectory()
    {
        string basePath = "";

        // 1. 优先尝试从 GlobalSettings 获取工作目录
        if (GlobalSettings.Instance != null && !string.IsNullOrEmpty(GlobalSettings.Instance.agentWorkDir))
        {
            basePath = GlobalSettings.Instance.agentWorkDir;
        }
        else
        {
            // 回退方案
            basePath = Application.persistentDataPath;
        }

        

        // 2. 拼接 "Models" 子文件夹
        return Path.Combine(basePath, "Models");
    }

    // --- 1. 修复 ChatManager 报错的方法 (兼容旧逻辑) ---

    // 供 ChatManager 的 Dropdown 调用
    public List<string> GetCharacterNames()
    {
        RefreshModelList();
        // 返回文件名（不带路径和后缀）
        return _modelPaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToList();
    }

    // 供 ChatManager 调用 (根据索引加载)
    public void LoadCharacter(int index)
    {
        RefreshModelList();
        if (_modelPaths.Count == 0) return;

        // 安全检查
        if (index < 0) index = 0;
        if (index >= _modelPaths.Count) index = _modelPaths.Count - 1;

        string path = _modelPaths[index];
        LoadCharacterFromFile(path);
    }

    // 扫描文件夹更新列表
    public void RefreshModelList()
    {
        _modelPaths.Clear();
        string dir = GetModelDirectory();
        
        if (Directory.Exists(dir))
        {
            // 获取所有支持的格式
            var files = Directory.GetFiles(dir, "*.*")
                .Where(s => s.EndsWith(".vrm", StringComparison.OrdinalIgnoreCase) || 
                            s.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || 
                            s.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase));
            
            _modelPaths.AddRange(files);
        }
    }

    // --- 2. 核心加载逻辑 (文件路径版) ---

    public void LoadCharacterFromFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        // 如果是从外部上传的文件 (不在 Models 文件夹里)，我们可以选择把它复制过去，或者直接加载
        // 这里直接加载，保持简单
        
        string ext = Path.GetExtension(filePath).ToLower();

        if (ext == ".vrm")
        {
            LoadVRM(filePath);
        }
        else if (ext == ".glb" || ext == ".gltf")
        {
            LoadGLB(filePath);
        }
        else
        {
            Debug.LogError("不支持的文件格式: " + ext);
        }
    }

    private async void LoadVRM(string path)
    {
        UnloadCurrentCharacter();
        try 
        {
            byte[] bytes = File.ReadAllBytes(path);
            // 使用 VrmUtility 加载 (UniVRM 0.100+ 写法)
            _currentInstance = await VrmUtility.LoadBytesAsync(path, bytes);
            _currentInstance.ShowMeshes();
            SetupAndNotify(_currentInstance.Root, path);
        }
        catch (Exception e) { Debug.LogError($"VRM加载失败: {e.Message}"); UnloadCurrentCharacter(); }
    }

    private async void LoadGLB(string path)
    {
        UnloadCurrentCharacter();
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            // 使用 GltfUtility 加载
            _currentInstance = await GltfUtility.LoadBytesAsync(path, bytes);
            _currentInstance.ShowMeshes();
            SetupAndNotify(_currentInstance.Root, path);
        }
        catch (Exception e) { Debug.LogError($"GLB加载失败: {e.Message}"); UnloadCurrentCharacter(); }
    }

    private void SetupAndNotify(GameObject root, string path)
    {
        root.transform.SetParent(characterContainer, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // 【关键】这里不再直接 Invoke OnCharacterChanged，而是启动初始化协程
        StartCoroutine(SetupCharacterCoroutine(root));
        
        Debug.Log($"模型文件已加载: {Path.GetFileName(path)}，正在初始化组件...");
    }

    private void UnloadCurrentCharacter()
    {
        if (_currentInstance != null)
        {
            _currentInstance.Dispose();
            _currentInstance = null;
        }
        // 清理残留子物体
        foreach (Transform child in characterContainer) Destroy(child.gameObject);
    }

    // --- 3. 智能组件配置 (口型/表情) ---

    private IEnumerator SetupCharacterCoroutine(GameObject root)
    {
        // 1. 等待一帧，确保 UniVRM 骨骼生成完毕
        yield return new WaitForEndOfFrame();

        SkinnedMeshRenderer faceMesh = FindFaceMesh(root);
        uLipSyncBlendShape lipSync = null;

        if (faceMesh != null)
        {
            // --- 配置 LipSync ---
            lipSync = faceMesh.GetComponent<uLipSyncBlendShape>();
            if (lipSync == null) lipSync = faceMesh.gameObject.AddComponent<uLipSyncBlendShape>();
            lipSync.skinnedMeshRenderer = faceMesh;
            lipSync.blendShapes = new List<uLipSyncBlendShape.BlendShapeInfo>();
            ConfigureLipSyncBlendShapes(faceMesh.sharedMesh, lipSync);

            // --- 配置 表情控制器 ---
            AnimeFaceController faceCtrl = root.GetComponent<AnimeFaceController>();
            if (faceCtrl == null) faceCtrl = root.AddComponent<AnimeFaceController>();
            faceCtrl.targetMesh = faceMesh;
            
            // 查找眨眼
            faceCtrl.blinkIndex = FindBlendShapeIndex(faceMesh.sharedMesh, new string[] { 
                "Fcl_EYE_Close",  // <--- 你的模型用的名字
                "Blink", 
                "eye_close", 
                "Fcl_EYE_Closed", // 防备变体
                "wink" 
            }); // 优先匹配 Blink
            Debug.Log($"[眨眼绑定] 索引: {faceCtrl.blinkIndex}");
            
            // 填充忽略列表
            faceCtrl.ignoredIndices.Clear();
            foreach (var info in lipSync.blendShapes) faceCtrl.ignoredIndices.Add(info.index);
            if (faceCtrl.blinkIndex != -1) faceCtrl.ignoredIndices.Add(faceCtrl.blinkIndex);
        }

        // --- 配置 Animator (在 EndOfFrame 之后执行，绝对安全) ---
        Animator anim = root.GetComponent<Animator>();
        if (anim == null) anim = root.AddComponent<Animator>();

        if (sharedAnimatorController != null)
        {
            // 彻底重置状态
            anim.runtimeAnimatorController = null; 
            yield return null; // 再等一帧，确保状态清空
            
            anim.runtimeAnimatorController = sharedAnimatorController;
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            
            anim.Rebind();
            anim.Update(0f);
        }

        // =========================================================
        // 【新增】 自动添加并计算碰撞体 (CapsuleCollider)
        // =========================================================
        SetupCollider(root);

        Debug.Log("角色初始化完成，正在通知 ChatManager...");

        // =========================================================
        // 【最终修复】 所有的初始化都做完了，现在才通知外部！
        // =========================================================
        OnCharacterChanged?.Invoke(root, lipSync);
    }

    // 自动计算并添加胶囊碰撞体
    private void SetupCollider(GameObject root)
    {
        // 1. 防止重复添加
        var oldCol = root.GetComponent<Collider>();
        if (oldCol != null) Destroy(oldCol);

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();

        // 2. 尝试计算模型的包围盒 (Bounds)
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();

        if (renderers.Length > 0)
        {
            // 以第一个Renderer为基准，合并所有Renderer的包围盒
            bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        else
        {
            // 如果没找到Renderer，给一个默认高度 1.6m
            bounds.size = new Vector3(0.5f, 1.6f, 0.5f);
            bounds.center = root.transform.position + Vector3.up * 0.8f;
        }

        // 3. 将世界坐标的 bounds 转换为基于 root 的局部尺寸
        // 注意：VRM/GLB 模型原点通常在脚底，所以 center.y 大约是高度的一半
        float height = bounds.size.y;
        float radius = bounds.size.x > bounds.size.z ? bounds.size.x / 4.0f : bounds.size.z / 4.0f;

        // 4. 应用设置
        capsule.direction = 1; // Y-Axis
        
        // 简单的高度修正：假设原点在脚底
        capsule.height = height;
        capsule.radius = Mathf.Clamp(radius, 0.1f, 0.5f); // 限制半径范围，防止过胖或过瘦
        capsule.center = new Vector3(0, height / 2.0f, 0);

        // 保底逻辑：如果计算出来太小（比如模型本身缩放有问题），给一套标准数值
        if (capsule.height < 0.5f)
        {
            capsule.height = 1.6f;
            capsule.center = new Vector3(0, 0.8f, 0);
            capsule.radius = 0.25f;
        }

        Debug.Log($"已自动生成碰撞体: Height={capsule.height:F2}, Center={capsule.center}");
    }

    // 【新增】 异步初始化 Animator
    private IEnumerator SetupAnimatorAsync(GameObject root)
    {
        // 1. 等待一帧：让 UniVRM 完成所有的骨骼构建和初始 Pose 重置
        yield return new WaitForEndOfFrame();

        Animator anim = root.GetComponent<Animator>();
        if (anim == null) anim = root.AddComponent<Animator>();

        if (sharedAnimatorController != null)
        {
            // 2. 先禁用再启用，强制重置状态
            anim.enabled = false;
            
            anim.runtimeAnimatorController = sharedAnimatorController;
            anim.applyRootMotion = false; 
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 防止因为视锥剔除导致不更新

            anim.enabled = true;

            // 3. 强制 Rebind 和 Update
            // Rebind 会重新扫描骨骼名称并与 Avatar 进行匹配
            anim.Rebind();
            anim.Update(0f); // 强制执行一次 Update，让 Curves 数据立即生效
            
            Debug.Log("Animator 已强制重置并重新绑定");
        }
    }

    private SkinnedMeshRenderer FindFaceMesh(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var r in renderers)
        {
            if (r.sharedMesh == null) continue;
            string name = r.name.ToLower();
            if (name.Contains("head") || name.Contains("face")) return r;
            if (r.sharedMesh.blendShapeCount > 10) return r;
        }
        foreach (var r in renderers) if (r.sharedMesh.blendShapeCount > 0) return r;
        return null;
    }

    private void ConfigureLipSyncBlendShapes(Mesh mesh, uLipSyncBlendShape lipSync)
    {
        var mapping = new Dictionary<string, string[]>
        {
            { "A", new[] { "Fcl_MTH_A", "Mouth_A", "viseme_aa", "A", "aa" } },
            { "I", new[] { "Fcl_MTH_I", "Mouth_I", "viseme_ih", "I", "ih" } },
            { "U", new[] { "Fcl_MTH_U", "Mouth_U", "viseme_U", "U", "ou" } },
            { "E", new[] { "Fcl_MTH_E", "Mouth_E", "viseme_E", "E", "ee" } },
            { "O", new[] { "Fcl_MTH_O", "Mouth_O", "viseme_oh", "O", "oh" } }
        };

        Debug.Log("========== 开始绑定口型 ==========");
        foreach (var kvp in mapping)
        {
            int index = FindBlendShapeIndex(mesh, kvp.Value);
            if (index != -1)
            {
                string realName = mesh.GetBlendShapeName(index);
                lipSync.blendShapes.Add(new uLipSyncBlendShape.BlendShapeInfo { phoneme = kvp.Key, index = index });
                
                // 【调试信息】 这里会打印到底绑了哪个
                Debug.Log($"<color=green>[绑定成功]</color> 音素 '{kvp.Key}' 绑定到了 BlendShape: '{realName}' (Index: {index})");
            }
            else
            {
                Debug.LogError($"<color=red>[绑定失败]</color> 音素 '{kvp.Key}' 在模型中未找到对应 BlendShape！请检查模型。");
            }
        }
        Debug.Log("=================================");
    }

    private int FindBlendShapeIndex(Mesh mesh, string[] candidateNames)
    {
        if (mesh == null) return -1;
        
        // 1. 第一轮：尝试精确全字匹配 (解决 "A" 匹配到 "Angry" 的风险)
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shapeName = mesh.GetBlendShapeName(i);
            foreach (var candidate in candidateNames)
            {
                if (shapeName.Equals(candidate, StringComparison.Ordinal)) // 严格区分大小写
                    return i;
            }
        }

        // 2. 第二轮：如果没找到，再尝试模糊匹配 (兼容不规范命名)
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shapeName = mesh.GetBlendShapeName(i);
            foreach (var candidate in candidateNames)
            {
                // 排除单字母匹配引起的误判 (比如 'A' 不应该模糊匹配 'Angry')
                if (candidate.Length == 1) continue; 

                if (shapeName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
        }
        return -1;
    }
}