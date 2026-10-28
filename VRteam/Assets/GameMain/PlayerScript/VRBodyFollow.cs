
using UnityEngine;

public class VRBodyFollow : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField]
    private Transform vrCamera;

    [SerializeField]
    private Animator animator;

    [Header("追従設定")]
    [SerializeField]
    private bool followPosition = true;

    [SerializeField]
    private bool followRotation = true;

    private Transform headBone;

    private void Start()
    {
        // Humanoidモデルの頭部ボーンを取得
        if (animator != null &&
            animator.avatar != null &&
            animator.avatar.isValid &&
            animator.avatar.isHuman)
        {
            headBone = animator.GetBoneTransform(
                HumanBodyBones.Head
            );
        }
    }

    private void LateUpdate()
    {
        if (vrCamera == null || headBone == null)
            return;

        // カメラが向いている水平方向
        Vector3 forward = vrCamera.forward;
        forward.y = 0f;

        // 身体の向きを合わせる
        if (followRotation &&
            forward.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up
                );
        }

        // 頭部の水平位置をカメラに合わせる
        if (followPosition)
        {
            Vector3 offset =
                vrCamera.position - headBone.position;

            // 高さは変更しない
            offset.y = 0f;

            transform.position += offset;
        }
    }
}
