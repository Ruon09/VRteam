
using UnityEngine;
using UnityEngine.InputSystem;

public class VRSwingSkill : MonoBehaviour
{
    [Header("入力設定")]
    [SerializeField]
    private InputActionReference skillButton;

    [Header("振り判定")]
    [SerializeField]
    private float swingThreshold = 2.0f;

    [SerializeField]
    private float cooldown = 0.5f;

    [Header("技の設定")]
    [SerializeField]
    private GameObject skillPrefab;

    [SerializeField]
    private float spawnDistance = 0.3f;

    [SerializeField]
    private float skillSpeed = 8.0f;

    // 前フレームのコントローラー位置
    private Vector3 previousPosition;

    // 前フレームの振り速度
    private float previousSpeed;

    // 最後に技を発動した時刻
    private float lastSkillTime = -999f;

    private void Start()
    {
        previousPosition = transform.position;
    }

    private void Update()
    {

        if (skillButton != null &&
            skillButton.action != null &&
            skillButton.action.WasPressedThisFrame())
        {
            Debug.Log("右トリガーが押されました！");
        }


        // コントローラーの移動量から速度を計算
        Vector3 currentPosition = transform.position;

        float speed = Vector3.Distance(
            currentPosition,
            previousPosition
        ) / Mathf.Max(Time.deltaTime, 0.0001f);

        previousPosition = currentPosition;

        // ボタンが押されているか確認
        bool isPressed =
            skillButton != null &&
            skillButton.action != null &&
            skillButton.action.IsPressed();

        // 技を再使用できるか
        bool canUseSkill =
            Time.time - lastSkillTime >= cooldown;

        // 振り始めの判定
        bool isSwinging =
            speed >= swingThreshold &&
            previousSpeed < swingThreshold;

        if (isPressed && isSwinging && canUseSkill)
        {
            ActivateSkill();
            lastSkillTime = Time.time;
        }

        previousSpeed = speed;
    }

    private void ActivateSkill()
    {
        Debug.Log("技発動！");

        if (skillPrefab == null)
            return;

        // コントローラーの前方に生成
        Vector3 spawnPosition =
            transform.position +
            transform.forward * spawnDistance;

        GameObject skill = Instantiate(
            skillPrefab,
            spawnPosition,
            transform.rotation
        );

        // Rigidbodyがあれば前方に飛ばす
        if (skill.TryGetComponent(
            out Rigidbody rb))
        {
            rb.AddForce(
                transform.forward * skillSpeed,
                ForceMode.VelocityChange
            );
        }

        Destroy(skill, 3.0f);
    }
}
