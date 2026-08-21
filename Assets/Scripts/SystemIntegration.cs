using UnityEngine;
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Drawing;
using System.Windows.Forms;
using Application = UnityEngine.Application;
using System.Collections; // 引入协程

public class SystemIntegration : MonoBehaviour
{
    [Header("设置")]
    public bool minimizeToTrayOnStart = false; 

    private NotifyIcon _notifyIcon;
    private bool _isWindowVisible = true;
    private IntPtr _windowHandle; 

    // --- Windows API ---
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    private const int GWL_EXSTYLE = -20; 
    private const int WS_EX_APPWINDOW = 0x00040000;  
    private const int WS_EX_TOOLWINDOW = 0x00000080; 

    // 改为协程 Start
    IEnumerator Start()
    {
        // 1. 【关键修复】只在打包后执行，防止在编辑器里把 Unity 窗口搞没了
#if !UNITY_EDITOR
        
        // 2. 【关键修复】等待 0.5 秒
        // 让 DesktopPetController 先把窗口变成透明、无边框
        // 否则这里 GetWindowLong 会读取到旧的样式，覆盖掉透明效果
        yield return new WaitForSeconds(0.5f);

        // 3. 获取窗口句柄
        _windowHandle = FindWindow(null, Application.productName);
        if (_windowHandle == IntPtr.Zero)
        {
            // 回退查找
            _windowHandle = FindWindow("UnityWndClass", null);
        }

        if (_windowHandle != IntPtr.Zero)
        {
            // 4. 将窗口从任务栏移除
            RemoveFromTaskbar();

            // 5. 初始化托盘
            InitTrayIcon();

            // 6. 启动状态
            if (minimizeToTrayOnStart)
            {
                ToggleWindow(false);
            }
        }
        else
        {
            Debug.LogError("SystemIntegration: 未能找到窗口句柄！");
        }

#else
        yield return null; // 编辑器模式下什么都不做，防止报错
#endif
    }

    void RemoveFromTaskbar()
    {
        if (_windowHandle == IntPtr.Zero) return;

        // 获取当前的扩展风格（此时包含了 PetController 设置的透明属性）
        int style = GetWindowLong(_windowHandle, GWL_EXSTYLE);

        // 在现有风格基础上：移除 APPWINDOW，添加 TOOLWINDOW
        // 这样就不会覆盖掉透明属性了
        style = (style & ~WS_EX_APPWINDOW) | WS_EX_TOOLWINDOW;

        SetWindowLong(_windowHandle, GWL_EXSTYLE, style);
        
        // 刷新窗口
        ShowWindow(_windowHandle, SW_SHOW); 
    }

    void InitTrayIcon()
    {
        // 运行在副线程以防卡顿主线程 (WinForms 有时会有微小卡顿)
        _notifyIcon = new NotifyIcon();
        try 
        {
            // 尝试获取 exe 图标
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            _notifyIcon.Icon = Icon.ExtractAssociatedIcon(exePath);
        }
        catch 
        {
            _notifyIcon.Icon = SystemIcons.Application; 
        }

        _notifyIcon.Text = Application.productName;
        _notifyIcon.Visible = true;

        _notifyIcon.Click += (s, e) => 
        {
            var args = e as MouseEventArgs;
            if (args != null && args.Button == MouseButtons.Left)
            {
                // 需要回到主线程操作 Unity 窗口吗？
                // ShowWindow 是线程安全的 API，可以直接调用，但为了保险起见，建议只是改变状态标记
                ToggleWindow(!_isWindowVisible);
            }
        };

        var contextMenu = new System.Windows.Forms.ContextMenu();
        contextMenu.MenuItems.Add("显示/隐藏", (s, e) => ToggleWindow(!_isWindowVisible));
        contextMenu.MenuItems.Add("-");
        contextMenu.MenuItems.Add("退出", (s, e) => 
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            // 在主线程退出
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Application.Quit();
            });
        });

        _notifyIcon.ContextMenu = contextMenu;
    }

    public void ToggleWindow(bool show)
    {
        _isWindowVisible = show;
        if (_windowHandle == IntPtr.Zero) return;

        if (show)
        {
            ShowWindow(_windowHandle, SW_RESTORE);
            // 恢复时重新应用 ToolWindow 风格，防止任务栏图标跑出来
            RemoveFromTaskbar(); 
        }
        else
        {
            ShowWindow(_windowHandle, SW_HIDE);
        }
    }

    // --- 简单的从子线程调度到主线程的辅助类 (可选，防止退出时卡死) ---
    // 你可以直接把 Application.Quit() 放在回调里，如果没报错就不需要这个
    
    void OnApplicationQuit()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}

// 简单的单例，用于在 WinForms 事件中回调 Unity 主线程（粘贴在同一个文件最下方即可）
public class UnityMainThreadDispatcher : MonoBehaviour {
    private static readonly System.Collections.Generic.Queue<Action> _executionQueue = new System.Collections.Generic.Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;
    public static UnityMainThreadDispatcher Instance() {
        if (!_instance) {
            _instance = new GameObject("UnityMainThreadDispatcher").AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(_instance.gameObject);
        }
        return _instance;
    }
    public void Enqueue(Action action) {
        lock (_executionQueue) { _executionQueue.Enqueue(action); }
    }
    void Update() {
        lock (_executionQueue) {
            while (_executionQueue.Count > 0) {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
}