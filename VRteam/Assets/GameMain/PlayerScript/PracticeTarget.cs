using UnityEngine;

public class PracticeTarget : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0.1f)] private float resetDelay = 3f;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private TextMesh healthLabel;

    [SerializeField] private bool autoReset = true;
    public event System.Action Defeated;
    public int CurrentHealth { get; private set; }
    private float hitUntil;
    private float resetAt;
    private MaterialPropertyBlock properties;
    private Camera viewer;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void OnEnable()
    {
        CurrentHealth = maxHealth;
        resetAt = hitUntil = 0f;
        properties = new MaterialPropertyBlock();
        viewer = Camera.main;
        RefreshDisplay();
    }

    public bool TryTakeDamage(int damage)
    {
        if (!isActiveAndEnabled || damage <= 0 || CurrentHealth == 0)
            return false;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        hitUntil = Time.time + 0.15f;
        if (CurrentHealth == 0)
        {
            resetAt = Time.time + resetDelay;
            Defeated?.Invoke();
        }
        RefreshDisplay();
        return true;
    }

    private void Update()
    {
        if (autoReset && CurrentHealth == 0 && Time.time >= resetAt)
            CurrentHealth = maxHealth;
        RefreshDisplay();
        if (viewer != null && healthLabel != null)
            healthLabel.transform.rotation = viewer.transform.rotation;
    }

    private void RefreshDisplay()
    {
        Color color = new Color(0.2f, 0.7f, 0.8f);
        if (CurrentHealth == 0) color = Color.gray;
        else if (Time.time < hitUntil) color = Color.red;
        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            properties.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(properties);
        }
        if (healthLabel != null)
            healthLabel.text = CurrentHealth == 0 ? "DOWN" : "HP " + CurrentHealth + " / " + maxHealth;
    }
}
