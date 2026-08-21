using UnityEngine;
using UnityEditor;
using System.IO;

public class FixBlenderDlls : MonoBehaviour
{
    [MenuItem("Tools/一键修复 Blender 报错 (V2.0)")]
    public static void FixDLLs()
    {
        // 1. 获取 StreamingAssets 的完整硬盘路径
        string streamingPath = Application.streamingAssetsPath;
        
        // 2. 扫描所有文件
        string[] files = Directory.GetFiles(streamingPath, "*.*", SearchOption.AllDirectories);
        
        Debug.Log($"正在扫描文件夹: {streamingPath}，共发现 {files.Length} 个文件...");

        int count = 0;
        foreach (string sysPath in files)
        {
            // --- 核心修复：把 Windows 的反斜杠 \ 统一替换成 Unity 的正斜杠 / ---
            string normalizedPath = sysPath.Replace("\\", "/");
            string normalizedDataPath = Application.dataPath.Replace("\\", "/");

            // 检查后缀（忽略大小写）
            string ext = Path.GetExtension(normalizedPath).ToLower();
            if (ext == ".dll" || ext == ".pyd" || ext == ".so")
            {
                // 把 "C:/Projects/.../Assets/StreamingAssets/..." 截取为 "Assets/StreamingAssets/..."
                string assetPath = "Assets" + normalizedPath.Replace(normalizedDataPath, "");
                
                // 获取导入设置
                PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
                
                if (importer != null)
                {
                    // 只要有任何平台是被勾选的，就强制关掉
                    bool needSave = false;
                    
                    if(importer.GetCompatibleWithAnyPlatform()) { importer.SetCompatibleWithAnyPlatform(false); needSave = true; }
                    if(importer.GetCompatibleWithEditor()) { importer.SetCompatibleWithEditor(false); needSave = true; }
                    if(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)) { importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false); needSave = true; }

                    if (needSave)
                    {
                        importer.SaveAndReimport();
                        count++;
                        Debug.Log($"已修复: {assetPath}");
                    }
                }
                else
                {
                    // 如果 Importer 是空，说明 Unity 还没把这个文件当成资源导入
                    // 强制刷新一下这个文件
                    AssetDatabase.ImportAsset(assetPath);
                }
            }
        }
        
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("修复报告", $"扫描结束！\n本次成功修复了 {count} 个插件文件。\n请查看 Console 控制台获取详细列表。", "确定");
    }
}