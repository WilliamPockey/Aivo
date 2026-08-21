using UnityEngine;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;
using System;

public class DesktopPetController : MonoBehaviour
{
    [Header("核心引用")]
    public ChatManager chatManager; 
    public Transform characterTransform; 
    [Tooltip("请在 Inspector 中设置角色的 Layer，并在这里选中它")]
    public LayerMask characterLayer = 1; // 默认为 Default，建议专门设置一个 Layer

    [Header("窗口设置")]
    public int targetWidth = 500;
    public int targetHeight = 700;

    [Header("摄像机控制")]
    public float zoomSpeed = 10f;       
    public float minZoom = 20f;         
    public float maxZoom = 60f;         
    public float panSpeed = 0.5f;       

    [Header("交互设置")]
    public float dragThreshold = 10f;   

    private Camera _mainCamera;
    
    // 状态变量
    private bool _isMouseDown = false;
    private bool _isDraggingWindow = false;
    private Vector2 _mouseDownPosition; 
    private Vector3 _lastRightClickPos;

    // --- Windows API 定义 (保持不变) ---
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    
    [DllImport("user32.dll")]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex); 

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    private const int GWL_STYLE = -16;
    private const uint WS_POPUP = 0x80000000; 
    private const uint WS_VISIBLE = 0x10000000;
    
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    public struct POINT { public int X; public int Y; }

    private POINT _startDragCursorPos;
    private RECT _startDragWindowRect;
    private bool _hasInitiatedWindowDrag = false;

    void Start()
    {
        _mainCamera = Camera.main;

        // 【修正 1】千万不要在 Start 里强制关闭 UI！
        // 之前这里调用了 ToggleUI(false)，这会导致 ChatManager 刚初始化好 UI，就被这边强制关掉
        // 导致状态不同步（ChatManager 以为开着，Controller 以为关着）
        // if (chatManager != null) chatManager.ToggleUI(false); <--- 删除这行

#if !UNITY_EDITOR
        SetupWindow();
#endif
    }

    void SetupWindow()
    {
        // 保持原有的窗口去边框逻辑
        IntPtr hWnd = GetActiveWindow();
        int screenWidth = GetSystemMetrics(0); 
        int screenHeight = GetSystemMetrics(1); 
        int posX = (screenWidth - targetWidth) / 2;
        int posY = (screenHeight - targetHeight) / 2;
        SetWindowLong(hWnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowPos(hWnd, IntPtr.Zero, posX, posY, targetWidth, targetHeight, SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    void Update()
    {
        // 【修正 2】安全地检测 UI 阻挡
        // 必须判空 EventSystem.current，否则在某些瞬间（如场景切换或 UI 重建）会报 NullReference 导致脚本停止运行
        if (IsPointerOverUI()) 
        {
             // 如果鼠标在 UI 上，重置拖拽状态，并直接返回
             // 这样确保当你点击“发送”按钮时，不会触发背后的角色点击事件
             _isDraggingWindow = false;
             _isMouseDown = false;
             _hasInitiatedWindowDrag = false;
             return; 
        }

        HandleLeftClickInput(); 
        HandleCameraControl();  
    }

    // 封装一个安全的 UI 检测方法
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    void HandleLeftClickInput()
    {
        // 1. 按下
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            
            // 【修正 3】增加 LayerMask，只检测角色层！
            // 之前的 Physics.Raycast 会检测所有物体。如果场景里有透明墙或者其他碰撞体，会挡住点击。
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, characterLayer))
            {
                // 双重确认点击到的是 Character
                if (characterTransform != null && (hit.transform.root == characterTransform || hit.transform.IsChildOf(characterTransform)))
                {
                    _isMouseDown = true;
                    _mouseDownPosition = Input.mousePosition;
                    _isDraggingWindow = false;
                    _hasInitiatedWindowDrag = false;
                }
            }
        }

        // 2. 按住
        if (_isMouseDown && Input.GetMouseButton(0))
        {
            float dist = Vector2.Distance(_mouseDownPosition, Input.mousePosition);
            if (dist > dragThreshold || _isDraggingWindow)
            {
                _isDraggingWindow = true; 
#if !UNITY_EDITOR
                MoveWindowByMouse();
#endif
            }
        }

        // 3. 抬起
        if (Input.GetMouseButtonUp(0))
        {
            if (_isMouseDown)
            {
                // 只有在【没有发生拖拽】的情况下，才视为“点击唤醒 UI”
                if (!_isDraggingWindow)
                {
                    OnCharacterClicked();
                }
                _isMouseDown = false;
                _isDraggingWindow = false;
                _hasInitiatedWindowDrag = false;
            }
        }
    }

    void HandleCameraControl()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        // 增加阈值防止微小抖动
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // 同样增加 LayerMask 限制，只有指着人物时才能缩放
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, characterLayer))
            {
                if (characterTransform != null && (hit.transform.root == characterTransform || hit.transform.IsChildOf(characterTransform)))
                {
                    if (_mainCamera.orthographic)
                    {
                        _mainCamera.orthographicSize -= scroll * zoomSpeed;
                        _mainCamera.orthographicSize = Mathf.Clamp(_mainCamera.orthographicSize, 1f, 20f);
                    }
                    else
                    {
                        float fov = _mainCamera.fieldOfView;
                        fov -= scroll * zoomSpeed * 50f; 
                        fov = Mathf.Clamp(fov, minZoom, maxZoom);
                        _mainCamera.fieldOfView = fov;
                    }
                }
            }
        }

        // 右键平移逻辑
        if (Input.GetMouseButtonDown(1))
        {
            _lastRightClickPos = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - _lastRightClickPos;
            // 降低平移速度，防止一下移飞了
            float moveX = -delta.x * panSpeed * 0.1f * Time.deltaTime; 
            float moveY = -delta.y * panSpeed * 0.1f * Time.deltaTime;
            _mainCamera.transform.Translate(moveX, moveY, 0);
            _lastRightClickPos = Input.mousePosition;
        }
    }

    void OnCharacterClicked()
    {
        // 【修正 4】交互逻辑优化
        // 点击人物时，无脑强制打开 UI，并重置 ChatManager 的计时器
        if (chatManager != null)
        {
            // 如果 UI 是隐藏的，打开它
            // 如果 UI 已经是打开的，不要关闭它（防止误操作），除非你想做开关效果
            if (!chatManager.uiRoot.activeSelf)
            {
                chatManager.ToggleUI(true);
            }
        }
    }

    void MoveWindowByMouse()
    {
        // Windows API 移动窗口逻辑保持不变
        IntPtr hWnd = GetActiveWindow();
        POINT currentCursorPos;
        GetCursorPos(out currentCursorPos);

        if (!_hasInitiatedWindowDrag)
        {
             GetWindowRect(hWnd, out _startDragWindowRect);
             _startDragCursorPos = currentCursorPos;
             _hasInitiatedWindowDrag = true;
        }
        else
        {
            int deltaX = currentCursorPos.X - _startDragCursorPos.X;
            int deltaY = currentCursorPos.Y - _startDragCursorPos.Y;

            int width = _startDragWindowRect.Right - _startDragWindowRect.Left;
            int height = _startDragWindowRect.Bottom - _startDragWindowRect.Top;

            MoveWindow(hWnd, _startDragWindowRect.Left + deltaX, _startDragWindowRect.Top + deltaY, width, height, true);
        }
    }
}