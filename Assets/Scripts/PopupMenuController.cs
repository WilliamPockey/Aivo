using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PopupMenuController : MonoBehaviour
{
    [Header("组件引用")]
    public GameObject menuPanel; // 拖入你做好的 PopupMenu
    public Button moreButton;    // 拖入你的“更多”按钮

    [Header("动画设置")]
    public float animationSpeed = 10f;

    private CanvasGroup canvasGroup;
    private bool isOpen = false;

    void Start()
    {
        // 自动给 Panel 加 CanvasGroup 以便控制透明度（如果还没有的话）
        canvasGroup = menuPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = menuPanel.AddComponent<CanvasGroup>();

        // 初始化状态：隐藏、透明、缩放为0（实现从按钮里弹出的感觉）
        menuPanel.SetActive(false);
        canvasGroup.alpha = 0;
        menuPanel.transform.localScale = new Vector3(1, 0, 1); // 垂直压扁

        // 绑定点击事件
        moreButton.onClick.AddListener(OnMoreBtnClick);

        // 【新增逻辑】自动查找 menuPanel 下的所有按钮，并绑定关闭事件
        // true 参数表示即使按钮被隐藏也能找到（保险起见），也可以填 false
        Button[] menuButtons = menuPanel.GetComponentsInChildren<Button>(true);

        foreach (Button btn in menuButtons)
        {
            // 给每个按钮添加监听：点击时执行 CloseMenu
            btn.onClick.AddListener(CloseMenu);
        }
    }

    void OnMoreBtnClick()
    {
        // 修改：将逻辑拆分，复用 CloseMenu 方法
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    // 【新增方法】专门负责打开菜单
    public void OpenMenu()
    {
        isOpen = true;
        menuPanel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(AnimateMenu(1f, 1f)); 
    }

    // 【新增方法】专门负责关闭菜单（设为 Public 以便外部也可以调用）
    public void CloseMenu()
    {
        // 如果已经是关闭状态，就不重复执行
        if (!isOpen) return;

        isOpen = false;
        StopAllCoroutines();
        StartCoroutine(AnimateMenu(0f, 0f, true));
    }

    // 一个简单的协程来实现平滑动画
    IEnumerator AnimateMenu(float targetAlpha, float targetScaleY, bool hideOnFinish = false)
    {
        float currentAlpha = canvasGroup.alpha;
        float currentScaleY = menuPanel.transform.localScale.y;

        // 使用插值循环直到接近目标
        while (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f || Mathf.Abs(currentScaleY - targetScaleY) > 0.01f)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * animationSpeed);
            currentScaleY = Mathf.Lerp(currentScaleY, targetScaleY, Time.deltaTime * animationSpeed);

            canvasGroup.alpha = currentAlpha;
            // 这一步让菜单看起来是从底部向上“长”出来的
            // 确保你的 PopupMenu 的 Pivot（轴心）设置在 Y=0 (底部)，这样才会向上生长
            menuPanel.transform.localScale = new Vector3(1, currentScaleY, 1);

            yield return null;
        }

        // 强制设为最终值
        canvasGroup.alpha = targetAlpha;
        menuPanel.transform.localScale = new Vector3(1, targetScaleY, 1);

        if (hideOnFinish)
        {
            menuPanel.SetActive(false);
        }
    }
}