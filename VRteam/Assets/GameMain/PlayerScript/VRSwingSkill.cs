using UnityEngine;
using UnityEngine.InputSystem;

public class VRSwingSkill : MonoBehaviour
{
    [Header("入力設定")]
    [SerializeField] private InputActionReference skillButton;

    [Header("振り判定")]
    [SerializeField] private Transform swingGuide;
    [SerializeField, Min(0.01f)] private float swingThreshold = 2f;
    [SerializeField, Min(0.01f)] private float swingWindow = 0.18f;
    [SerializeField, Min(0.01f)] private float minimumSwingDistance = 0.18f;
    [SerializeField, Range(0f, 90f)] private float directionTolerance = 30f;
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    [Header("攻撃アニメーション")]
    [SerializeField] private Animator playerAnimator;
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");

    [Header("操作フィードバック")]
    [SerializeField] private Color readyColor = new Color(0.1f, 0.65f, 1f);
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failureColor = new Color(1f, 0.2f, 0.15f);
    [SerializeField] private Color cooldownColor = Color.gray;
    [SerializeField, Min(0.01f)] private float feedbackDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.35f;
    [SerializeField, Min(0f)] private float hapticDuration = 0.08f;

    private LineRenderer guideLine;
    private MaterialPropertyBlock guideProperties;
    private Color feedbackColor;
    private float feedbackUntil;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    private Vector3 previousPosition;
    private bool wasAboveThreshold;
    private bool isMeasuringSwing;
    private Vector3 swingStart;
    private Vector3 expectedDirection;
    private float swingStartedAt;
    private float nextSkillTime;

    private void OnEnable()
    {
        guideLine = swingGuide != null ? swingGuide.GetComponent<LineRenderer>() : null;
        guideProperties = new MaterialPropertyBlock();
        feedbackUntil = 0f;
        previousPosition = transform.localPosition;
        wasAboveThreshold = false;
        isMeasuringSwing = false;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f)
            return;

        // ロコモーションによるリグの移動を振り速度に含めない。
        Vector3 position = transform.localPosition;
        Vector3 displacement = position - previousPosition;
        bool isAboveThreshold = displacement.magnitude / Time.deltaTime >= swingThreshold;
        bool startedSwing = isAboveThreshold && !wasAboveThreshold;
        Vector3 lastPosition = previousPosition;
        previousPosition = position;
        wasAboveThreshold = isAboveThreshold;

        float now = Time.time;
        UpdateGuideColor(now);
        if (skillButton?.action?.IsPressed() != true || now < nextSkillTime || swingGuide == null)
        {
            isMeasuringSwing = false;
            return;
        }

        if (!isMeasuringSwing && startedSwing)
        {
            isMeasuringSwing = true;
            swingStart = lastPosition;
            swingStartedAt = now;
            expectedDirection = transform.parent != null
                ? transform.parent.InverseTransformDirection(swingGuide.right)
                : swingGuide.right;
        }

        if (!isMeasuringSwing)
            return;

        if (now - swingStartedAt > swingWindow)
        {
            isMeasuringSwing = false;
            ShowFeedback(failureColor);
            return;
        }

        Vector3 swing = position - swingStart;
        if (swing.sqrMagnitude < minimumSwingDistance * minimumSwingDistance)
            return;

        isMeasuringSwing = false;
        if (Vector3.Angle(swing, expectedDirection) <= directionTolerance)
        {
            nextSkillTime = now + cooldown;
            ActivateSkill();
        }
        else
        {
            ShowFeedback(failureColor);
        }
    }

    private void UpdateGuideColor(float now)
    {
        if (guideLine == null)
            return;

        Color color = readyColor;
        if (now < feedbackUntil)
            color = feedbackColor;
        else if (now < nextSkillTime)
            color = cooldownColor;

        // テンプレートのマテリアルを共有している他の表示には影響させない。
        guideLine.GetPropertyBlock(guideProperties);
        guideProperties.SetColor(ColorProperty, color);
        guideProperties.SetColor(BaseColorProperty, color);
        guideLine.SetPropertyBlock(guideProperties);
    }

    private void ShowFeedback(Color color)
    {
        feedbackColor = color;
        feedbackUntil = Time.time + feedbackDuration;
        UpdateGuideColor(Time.time);
    }

    private void OnDisable()
    {
        if (guideLine != null)
            guideLine.SetPropertyBlock(null);
    }

    private void ActivateSkill()
    {
        if (playerAnimator != null)
        {
            playerAnimator.ResetTrigger(AttackTrigger);
            playerAnimator.SetTrigger(AttackTrigger);
        }

        ShowFeedback(successColor);
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            device.SendHapticImpulse(0, hapticAmplitude, hapticDuration);

    }
}
