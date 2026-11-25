using UnityEngine;
using UnityEngine.UI;

/// <summary>シーンに配置したワールド空間HPバー。EnemyHealthBarオブジェクトに付けて使用。</summary>
public sealed class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Transform target;
    private float maxHealth = 1f;

    public void Initialize(float maximum, float current)
    {
        maxHealth = Mathf.Max(1f, maximum);
        target = Camera.main != null ? Camera.main.transform : null;
        SetHealth(current);
    }

    public void SetHealth(float current)
    {
        float ratio = Mathf.Clamp01(current / maxHealth);
        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
            fillImage.color = Color.Lerp(Color.red, new Color(0.15f, 0.9f, 0.22f), ratio);
        }
    }

    private void LateUpdate()
    {
        if (target == null && Camera.main != null) target = Camera.main.transform;
        if (target == null) return;
        Vector3 direction = target.position - transform.position;
        if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
    }
}
