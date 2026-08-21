using UnityEngine;

public class FaceDebugUI : MonoBehaviour
{
    // 引用你的控制器
    public AnimeFaceController faceController;

    private void Start()
    {
        // 如果没手动拖拽，尝试自动获取
        if (faceController == null)
            faceController = FindObjectOfType<AnimeFaceController>();
    }

    private void OnGUI()
    {
        if (faceController == null) return;

        // 设置左上角的区域
        GUILayout.BeginArea(new Rect(20, 20, 150, 400));
        
        // 标题
        GUILayout.Label("表情调试面板");
        GUILayout.Space(10);

        // 显示当前过渡速度的滑块，方便你实时调整
        GUILayout.Label($"过渡速度: {faceController.transitionSpeed:F1}");
        faceController.transitionSpeed = GUILayout.HorizontalSlider(faceController.transitionSpeed, 0.1f, 10f);
        
        GUILayout.Space(10);

        // 获取所有定义的表情枚举
        var expressions = System.Enum.GetValues(typeof(AnimeFaceController.ExpressionType));

        foreach (AnimeFaceController.ExpressionType expr in expressions)
        {
            // 如果是当前表情，按钮变色提示
            if (faceController.currentExpression == expr)
            {
                GUI.color = Color.green; // 当前选中的显示为绿色
            }
            else
            {
                GUI.color = Color.white;
            }

            // 生成按钮，点击后切换表情
            if (GUILayout.Button(expr.ToString(), GUILayout.Height(30)))
            {
                faceController.currentExpression = expr;
            }
        }
        
        GUILayout.EndArea();
    }
}