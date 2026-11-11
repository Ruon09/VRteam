using UnityEngine;

// Animatorの更新後に、一人称用モデルの位置を合わせる。
[DefaultExecutionOrder(100)]
public class VRBodyFollow : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform vrCamera;
    [SerializeField] private Animator animator;

    [Header("追従設定")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;

    [Header("一人称の見え方")]
    [Tooltip("視点に対するモデルの補正。Yを下げると拳が下がり、Zを増やすと拳が遠ざかります。")]
    [SerializeField] private Vector3 bodyOffset = new Vector3(0f, -0.1f, 0.15f);
    [SerializeField] private bool hideHead = true;

    private Transform headBone;
    private Vector3 neutralEyePosition;
    private Vector3 originalHeadScale;
    private bool calibrated;

    private void LateUpdate()
    {
        if (vrCamera == null || animator == null || !animator.isHuman)
            return;

        if (!calibrated)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                return;

            var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            Vector3 eyePosition = headBone.position;
            if (leftEye != null && rightEye != null)
                eyePosition = (leftEye.position + rightEye.position) * 0.5f;

            neutralEyePosition = transform.InverseTransformPoint(eyePosition);
            originalHeadScale = headBone.localScale;
            calibrated = true;
        }

        Vector3 forward = Vector3.ProjectOnPlane(vrCamera.forward, Vector3.up);
        if (followRotation && forward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        if (followPosition)
        {
            // アニメーション中の頭の揺れでモデル全体を補正しない。
            Vector3 eyeOffset = transform.TransformVector(neutralEyePosition);
            transform.position = vrCamera.position - eyeOffset + transform.rotation * bodyOffset;
        }

        headBone.localScale = hideHead ? Vector3.zero : originalHeadScale;
    }

    private void OnDisable()
    {
        if (headBone != null && calibrated)
            headBone.localScale = originalHeadScale;
        calibrated = false;
    }
}
