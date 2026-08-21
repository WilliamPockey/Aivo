using UnityEngine;
using System.Collections;

public class SimpleBlinkController : MonoBehaviour
{
    // 必须手动指定：角色面部的蒙皮网格渲染器（存放BlendShapes的组件）
    public SkinnedMeshRenderer faceMesh;

    // 眨眼对应的BlendShape索引（在faceMesh的BlendShapes列表中查看）
    public int blinkShapeIndex;

    // 眨眼参数（可在Inspector调节）
    [Range(0.1f, 0.5f)] public float blinkTime = 0.2f; // 眨眼一次的时间（闭眼+睁眼）
    [Range(2f, 10f)] public float minInterval = 3f;   // 最小间隔
    [Range(2f, 10f)] public float maxInterval = 7f;   // 最大间隔

    private float timer; // 间隔计时器

    void Update()
    {
        // 检查必要组件是否配置
        if (faceMesh == null || blinkShapeIndex < 0)
        {
            Debug.LogWarning("请先配置faceMesh和blinkShapeIndex！", this);
            return;
        }

        // 累计时间，到间隔后触发眨眼
        timer += Time.deltaTime;
        float randomInterval = Random.Range(minInterval, maxInterval);
        if (timer >= randomInterval)
        {
            StartCoroutine(Blink());
            timer = 0;
        }
    }

    // 眨眼协程：0→100→0权重变化
    IEnumerator Blink()
    {
        // 闭眼（0→100）
        for (float t = 0; t < blinkTime / 2; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(0, 100, t / (blinkTime / 2));
            faceMesh.SetBlendShapeWeight(blinkShapeIndex, weight);
            yield return null;
        }

        // 睁眼（100→0）
        for (float t = 0; t < blinkTime / 2; t += Time.deltaTime)
        {
            float weight = Mathf.Lerp(100, 0, t / (blinkTime / 2));
            faceMesh.SetBlendShapeWeight(blinkShapeIndex, weight);
            yield return null;
        }
    }
}