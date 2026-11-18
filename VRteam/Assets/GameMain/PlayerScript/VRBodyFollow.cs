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
    [System.Serializable]
    private class BodyMesh
    {
        public SkinnedMeshRenderer renderer;
        public Mesh originalMesh;
        public Mesh firstPersonMesh;
    }

    [SerializeField] private BodyMesh[] bodyMeshes = new BodyMesh[0];
    private bool calibrated;
    private Quaternion facingCorrection = Quaternion.identity;
    private AnimatorCullingMode previousCullingMode;

    private void OnEnable()
    {
        if (animator != null)
        {
            previousCullingMode = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

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

            // 待機アニメーションの顔の向きを、視線の正面に合わせる。
            Vector3 neutralForward = Vector3.ProjectOnPlane(
                transform.InverseTransformDirection(headBone.forward), Vector3.up);
            if (neutralForward.sqrMagnitude > 0.001f)
                facingCorrection = Quaternion.Inverse(
                    Quaternion.LookRotation(neutralForward.normalized, Vector3.up));
            calibrated = true;
        }

        Vector3 forward = Vector3.ProjectOnPlane(vrCamera.forward, Vector3.up);
        Quaternion viewRotation = transform.rotation * Quaternion.Inverse(facingCorrection);
        if (forward.sqrMagnitude > 0.001f)
            viewRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        if (followRotation && forward.sqrMagnitude > 0.001f)
            transform.rotation = viewRotation * facingCorrection;

        if (followPosition)
        {
            // アニメーション中の頭の揺れでモデル全体を補正しない。
            Vector3 eyeOffset = transform.TransformVector(neutralEyePosition);
            transform.position = vrCamera.position - eyeOffset + viewRotation * bodyOffset;
        }

        foreach (var bodyMesh in bodyMeshes)
        {
            if (bodyMesh.renderer != null)
                bodyMesh.renderer.sharedMesh = hideHead ? bodyMesh.firstPersonMesh : bodyMesh.originalMesh;
        }
    }

    private void OnDisable()
    {
        foreach (var bodyMesh in bodyMeshes)
        {
            if (bodyMesh.renderer != null)
                bodyMesh.renderer.sharedMesh = bodyMesh.originalMesh;
        }
        if (animator != null)
            animator.cullingMode = previousCullingMode;
        calibrated = false;
    }
}
