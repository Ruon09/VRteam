using UnityEngine;
using UnityEngine.XR;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float invulnerabilitySeconds = 1f;
    [SerializeField] private Transform head;
    [SerializeField] private VRSwingSkill skill;
    [SerializeField] private PunchCombat combat;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject swingGuide;
    [SerializeField] private TextMesh healthLabel;
    [SerializeField] private Transform healthFill;

    public event System.Action Defeated;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public Transform Head => head;

    private float invulnerableUntil;
    private float feedbackUntil;
    private Vector3 spawnPosition;
    private Vector3 spawnHeadPosition;
    private Quaternion spawnRotation;
    private Vector3 fillScale;
    private bool skillEnabled;
    private bool combatEnabled;
    private bool guideEnabled;
    private bool retryWasPressed;
    private static readonly int Attack = Animator.StringToHash("Attack");

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnHeadPosition = head != null ? head.position : spawnPosition;
        spawnRotation = transform.rotation;
        skillEnabled = skill != null && skill.enabled;
        combatEnabled = combat != null && combat.enabled;
        guideEnabled = swingGuide != null && swingGuide.activeSelf;
        fillScale = healthFill != null ? healthFill.localScale : Vector3.one;
        CurrentHealth = maxHealth;
        RefreshDisplay();
    }

    public bool TryTakeDamage(int damage)
    {
        if (!isActiveAndEnabled || !IsAlive || damage <= 0 || Time.time < invulnerableUntil)
            return false;
        CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
        invulnerableUntil = Time.time + invulnerabilitySeconds;
        feedbackUntil = Time.time + 0.25f;
        var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            device.SendHapticImpulse(0, 0.5f, 0.12f);
        if (!IsAlive)
        {
            if (skill != null) skill.enabled = false;
            if (combat != null) combat.enabled = false;
            if (swingGuide != null) swingGuide.SetActive(false);
            if (animator != null)
            {
                animator.ResetTrigger(Attack);
                animator.Play("Idle", 0, 0f);
            }
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out retryWasPressed);
            Defeated?.Invoke();
        }
        RefreshDisplay();
        return true;
    }

    private void Update()
    {
        var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed);
        // BattleFlow owns the result/replay flow when installed.
        if (GetComponent<BattleFlow>()==null && !IsAlive && pressed && !retryWasPressed)
            Retry();
        retryWasPressed = pressed;
        RefreshDisplay();
    }

    [ContextMenu("Retry")]
    public void Retry()
    {
        if (IsAlive || GetComponent<BattleFlow>()!=null)
            return;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (head != null)
        {
            // 実際に歩いた距離も補正し、視点を開始地点へ戻す。
            Vector3 correction = spawnHeadPosition - head.position;
            correction.y = 0f;
            transform.position += correction;
        }
        CurrentHealth = maxHealth;
        invulnerableUntil = Time.time + invulnerabilitySeconds;
        feedbackUntil = 0f;
        if (animator != null)
        {
            animator.ResetTrigger(Attack);
            animator.Play("Idle", 0, 0f);
        }
        if (skill != null) skill.enabled = skillEnabled;
        if (combat != null) combat.enabled = combatEnabled;
        if (swingGuide != null) swingGuide.SetActive(guideEnabled);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (healthLabel != null)
        {
            healthLabel.text = IsAlive ? "LIFE " + CurrentHealth + " / " + maxHealth
                : "DEFEAT";
            healthLabel.color = !IsAlive || Time.time < feedbackUntil ? Color.red : Color.white;
        }
        if (healthFill != null)
            healthFill.localScale = new Vector3(fillScale.x * CurrentHealth / maxHealth, fillScale.y, fillScale.z);
    }
}

