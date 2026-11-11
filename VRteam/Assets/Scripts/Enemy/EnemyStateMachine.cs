
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum EnemyActionState
{
    Wait,
    Move,
    Attack,
    Stunned
}

public enum EnemyAttackType
{
    None,
    Melee,
    Ranged
}

[System.Serializable]
public sealed class EnemyDamageEvent : UnityEvent<float> { }

/// <summary>
/// 敵の行動を管理するステートマシン。
/// Unity 6対応・物理検索とMaterial生成を最適化。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class EnemyStateMachine : MonoBehaviour
{
    [Header("パラメーター")]
    [SerializeField, Min(1f)] private float maxHP = 100f;
    [SerializeField, Min(0f)] private float moveSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float actionInterval = 0.8f;
    [SerializeField, Min(0.5f)] private float preferredDistance = 3f;
    [SerializeField, Min(0.1f)] private float distanceTolerance = 0.35f;
    [SerializeField, Min(0.1f)] private float meleeRange = 2.2f;
    [SerializeField, Min(0.1f)] private float rangedRange = 9f;
    [SerializeField, Min(0f)] private float meleeDamage = 15f;
    [SerializeField, Min(0f)] private float rangedDamage = 10f;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
    [SerializeField, Min(0.05f)] private float projectileHitRadius = 0.3f;

    [Header("行動選択の重み")]
    [SerializeField, Min(0f)] private float moveWeight = 4f;
    [SerializeField, Min(0f)] private float waitWeight = 3f;
    [SerializeField, Min(0f)] private float attackWeight = 3f;

    [Header("行動時間")]
    [SerializeField] private Vector2 moveDurationRange = new Vector2(1.5f, 3f);
    [SerializeField] private Vector2 waitDurationRange = new Vector2(1f, 2f);
    [SerializeField, Min(0.1f)] private float attackWindupDuration = 1f;
    [SerializeField, Min(0.1f)] private float stunDuration = 2f;

    [Header("弱点攻撃")]
    [SerializeField, Min(0.1f)] private float minimumTraceSpeed = 1.4f;
    [SerializeField, Min(0f)] private float weakPointBonusDamage = 20f;
    [SerializeField] private UnityEvent onAttack = new UnityEvent();
    [SerializeField] private UnityEvent onStunned = new UnityEvent();
    [SerializeField] private EnemyDamageEvent onPlayerDamaged = new EnemyDamageEvent();
    [SerializeField] private Transform target;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("状態 (実行時)")]
    [SerializeField] private EnemyActionState currentState = EnemyActionState.Wait;
    [SerializeField] private EnemyAttackType currentAttackType = EnemyAttackType.None;

    // コンポーネント
    private Renderer cubeRenderer;
    private Material cubeMaterial;
    private Color normalColor;
    private LineRenderer weakPointLine;
    [SerializeField] private EnemyHealthBar healthBar;

    // ステータス
    private float currentHP;
    private float stateEndTime;
    private float stunEndTime;
    private float strafeDirection = 1f;
    private bool defeated;

    // 弱点判定用
    private readonly Dictionary<int, Vector3> previousColliderPositions =
        new Dictionary<int, Vector3>();

    // 物理検索用バッファ
    private Collider[] overlapBuffer = new Collider[32];

    // MaterialPropertyBlock
    private MaterialPropertyBlock propertyBlock;

    // 共有Material
    private static Material sharedWeakPointMaterial;
    private static Material sharedProjectileMaterial;

    // プロパティ
    public EnemyActionState CurrentState => currentState;
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public float RemainingStunTime => Mathf.Max(0f, stunEndTime - Time.time);
    public EnemyAttackType CurrentAttackType => currentAttackType;

    // --------------------------------------------------
    // 初期化
    // --------------------------------------------------

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (target == null && Camera.main != null)
            target = Camera.main.transform;

        currentHP = maxHP;

        cubeRenderer = GetComponent<MeshRenderer>();

        if (cubeRenderer != null)
        {
            // materialではなくsharedMaterialを使用
            // 不必要なMaterialインスタンス生成を防ぐ
            cubeMaterial = cubeRenderer.sharedMaterial;
            normalColor = GetCubeMaterialColor();
        }

        BoxCollider body = GetComponent<BoxCollider>();
        body.isTrigger = false;

        Rigidbody physicsBody = GetComponent<Rigidbody>();

        if (physicsBody == null)
            physicsBody = gameObject.AddComponent<Rigidbody>();

        physicsBody.isKinematic = true;
        physicsBody.useGravity = false;

        EnsureWeakPoint();

        if (healthBar == null)
            healthBar = GetComponentInChildren<EnemyHealthBar>(true);

        if (healthBar != null)
            healthBar.Initialize(maxHP, currentHP);
        else
            Debug.LogError("Enemy: 子オブジェクトに EnemyHealthBar が見つかりません。", this);

        currentState = EnemyActionState.Wait;
        currentAttackType = EnemyAttackType.None;

        stateEndTime = Time.time + actionInterval;
    }

    // --------------------------------------------------
    // 更新処理
    // --------------------------------------------------

    private void Update()
    {
        if (defeated)
            return;

        if (target == null && Camera.main != null)
            target = Camera.main.transform;

        if (target == null)
            return;

        FaceTarget();

        switch (currentState)
        {
            case EnemyActionState.Wait:

                if (Time.time >= stateEndTime)
                    ChooseNextAction();

                break;

            case EnemyActionState.Move:

                MoveToPreferredDistance();

                if (Time.time >= stateEndTime)
                    EnterCooldown();

                break;

            case EnemyActionState.Attack:

                if (Time.time >= stateEndTime)
                    ResolveAttack();

                break;

            case EnemyActionState.Stunned:

                if (Time.time >= stunEndTime)
                    EnterCooldown();

                break;
        }
    }

    // --------------------------------------------------
    // 物理演算
    // --------------------------------------------------

    private void FixedUpdate()
    {
        if (currentState != EnemyActionState.Attack)
        {
            previousColliderPositions.Clear();
            return;
        }

        UpdateNearbyColliders();
    }

    /// <summary>
    /// NonAlloc方式で周囲のColliderを検索する。
    /// バッファが満杯の場合は拡張して再検索する。
    /// </summary>
    private void UpdateNearbyColliders()
    {
        int count;

        float searchRadius = meleeRange + 1.5f;

        do
        {
            count = Physics.OverlapSphereNonAlloc(
                transform.position,
                searchRadius,
                overlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide
            );

            // バッファが満杯なら拡張
            if (count == overlapBuffer.Length)
            {
                System.Array.Resize(
                    ref overlapBuffer,
                    overlapBuffer.Length * 2
                );
            }

        } while (count == overlapBuffer.Length);

        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapBuffer[i];

            if (candidate == null)
                continue;

            if (candidate.transform.root == transform.root)
                continue;

            previousColliderPositions[candidate.GetInstanceID()] =
                candidate.bounds.center;
        }
    }

    // --------------------------------------------------
    // 弱点ライン
    // --------------------------------------------------

    private void EnsureWeakPoint()
    {
        Transform existing = transform.Find("WeakPointLine");

        if (existing != null)
        {
            weakPointLine = existing.GetComponent<LineRenderer>();
            return;
        }

        GameObject lineObject = new GameObject("WeakPointLine");

        lineObject.transform.SetParent(transform, false);
        lineObject.transform.localPosition =
            new Vector3(0f, 0f, 0.52f);

        lineObject.transform.localRotation =
            Quaternion.Euler(0f, 0f, -28f);

        weakPointLine = lineObject.AddComponent<LineRenderer>();

        weakPointLine.useWorldSpace = false;
        weakPointLine.positionCount = 2;

        weakPointLine.SetPosition(
            0,
            new Vector3(-0.42f, 0.28f, 0f)
        );

        weakPointLine.SetPosition(
            1,
            new Vector3(0.42f, -0.28f, 0f)
        );

        weakPointLine.startWidth = 0.055f;
        weakPointLine.endWidth = 0.055f;

        // Materialを敵ごとに生成せず共有する
        if (sharedWeakPointMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                sharedWeakPointMaterial = new Material(shader);
                sharedWeakPointMaterial.name = "SharedWeakPointMaterial";
            }
        }

        if (sharedWeakPointMaterial != null)
            weakPointLine.sharedMaterial = sharedWeakPointMaterial;

        weakPointLine.startColor =
            new Color(1f, 0.28f, 0.08f);

        weakPointLine.endColor =
            new Color(1f, 0.75f, 0.15f);

        weakPointLine.enabled = false;

        BoxCollider weakPointHitbox =
            lineObject.AddComponent<BoxCollider>();

        weakPointHitbox.isTrigger = true;
        weakPointHitbox.size =
            new Vector3(0.92f, 0.16f, 0.24f);
    }

    // --------------------------------------------------
    // プレイヤー方向
    // --------------------------------------------------

    private void FaceTarget()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                Time.deltaTime * 6f
            );
        }
    }

    private float TargetDistance()
    {
        Vector3 a = transform.position;
        a.y = 0f;

        Vector3 b = target.position;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    // --------------------------------------------------
    // 行動選択
    // --------------------------------------------------

    private void ChooseNextAction()
    {
        float total =
            moveWeight + waitWeight + attackWeight;

        if (total <= 0f)
        {
            EnterWait(
                Random.Range(
                    waitDurationRange.x,
                    waitDurationRange.y
                )
            );

            return;
        }

        float choice = Random.value * total;

        if (choice < moveWeight)
        {
            EnterMove();
        }
        else if (choice < moveWeight + waitWeight)
        {
            EnterWait(
                Random.Range(
                    waitDurationRange.x,
                    waitDurationRange.y
                )
            );
        }
        else if (TargetDistance() <= rangedRange)
        {
            EnterAttack();
        }
        else
        {
            EnterMove();
        }
    }

    // --------------------------------------------------
    // 移動
    // --------------------------------------------------

    private void EnterMove()
    {
        SetState(EnemyActionState.Move);

        strafeDirection =
            Random.value < 0.5f ? -1f : 1f;

        float duration = Random.Range(
            moveDurationRange.x,
            moveDurationRange.y
        );

        stateEndTime = Time.time + duration;

        Log(
            $"Enemy: 移動を開始。{duration:0.#}秒間、適正距離 {preferredDistance:0.#} を保ちます。"
        );
    }

    private void MoveToPreferredDistance()
    {
        Vector3 towardTarget =
            target.position - transform.position;

        towardTarget.y = 0f;

        if (towardTarget.sqrMagnitude < 0.001f)
            return;

        towardTarget.Normalize();

        float offset = TargetDistance() - preferredDistance;

        Vector3 direction;

        if (offset > distanceTolerance)
        {
            direction = towardTarget;
        }
        else if (offset < -distanceTolerance)
        {
            direction = -towardTarget;
        }
        else
        {
            direction =
                Vector3.Cross(Vector3.up, towardTarget)
                * strafeDirection;
        }

        transform.position +=
            direction * (moveSpeed * Time.deltaTime);
    }

    // --------------------------------------------------
    // 待機
    // --------------------------------------------------

    private void EnterWait(float duration)
    {
        SetState(EnemyActionState.Wait);

        stateEndTime =
            Time.time + Mathf.Max(0f, duration);

        Log($"Enemy: 待機します。{duration:0.#}秒後に次の行動を選びます。");
    }

    // --------------------------------------------------
    // 攻撃
    // --------------------------------------------------

    private void EnterAttack()
    {
        SetState(EnemyActionState.Attack);

        stateEndTime =
            Time.time + attackWindupDuration;

        currentAttackType =
            TargetDistance() <= meleeRange
            ? EnemyAttackType.Melee
            : EnemyAttackType.Ranged;

        if (currentAttackType == EnemyAttackType.Melee)
        {
            SetCubeColor(Color.red);

            if (weakPointLine != null)
                weakPointLine.enabled = true;

            Log("Enemy: 近距離攻撃の予告。赤色と弱点ラインを表示します。");
        }
        else
        {
            SetCubeColor(new Color(1f, 0.45f, 0.08f));

            Log("Enemy: 遠距離攻撃を予告しています。");
        }
    }

    private void EnterCooldown()
    {
        SetState(EnemyActionState.Wait);

        ResetAppearance();

        stateEndTime =
            Time.time + actionInterval;
    }

    private void ResolveAttack()
    {
        if (currentAttackType == EnemyAttackType.Melee)
        {
            if (TargetDistance() <= meleeRange + 0.25f)
            {
                Log($"Enemy: 近距離攻撃が命中。ダメージ {meleeDamage:0.#}");

                onAttack.Invoke();

                ApplyPlayerDamage(meleeDamage);
            }
            else
            {
                Log("Enemy: 近距離攻撃は届きませんでした。");
            }
        }
        else if (currentAttackType == EnemyAttackType.Ranged)
        {
            FireProjectile();
        }

        EnterCooldown();
    }

    // --------------------------------------------------
    // 遠距離攻撃
    // --------------------------------------------------

    private void FireProjectile()
    {
        if (target == null)
            return;

        Vector3 origin =
            transform.position
            + transform.forward * 0.58f
            + Vector3.up * 0.15f;

        Vector3 direction =
            (target.position - origin).normalized;

        GameObject projectileObject =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        projectileObject.name = "EnemyProjectile";

        projectileObject.transform.position = origin;

        projectileObject.transform.localScale =
            Vector3.one * 0.22f;

        Collider projectileCollider =
            projectileObject.GetComponent<Collider>();

        projectileCollider.isTrigger = true;

        Renderer projectileRenderer =
            projectileObject.GetComponent<Renderer>();

        // 共有Materialを利用
        Material projectileMaterial =
            GetSharedProjectileMaterial(
                projectileRenderer.sharedMaterial
            );

        if (projectileMaterial != null)
        {
            projectileRenderer.sharedMaterial =
                projectileMaterial;
        }

        Color projectileColor =
            new Color(1f, 0.24f, 0.04f);

        // Materialを複製せず色を変更
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        projectileRenderer.GetPropertyBlock(propertyBlock);

        SetPropertyBlockColor(
            projectileMaterial,
            propertyBlock,
            projectileColor
        );

        if (projectileMaterial != null &&
            projectileMaterial.HasProperty("_EmissionColor"))
        {
            propertyBlock.SetColor(
                "_EmissionColor",
                projectileColor * 1.5f
            );
        }

        projectileRenderer.SetPropertyBlock(propertyBlock);

        Rigidbody projectileBody =
            projectileObject.AddComponent<Rigidbody>();

        projectileBody.useGravity = false;
        projectileBody.isKinematic = true;

        EnemyProjectile projectile =
            projectileObject.AddComponent<EnemyProjectile>();

        // 既存の弾の処理をそのまま利用
        projectile.Initialize(
            this,
            target,
            direction,
            rangedDamage,
            projectileSpeed,
            projectileLifetime,
            projectileHitRadius
        );

        Log($"Enemy: 遠距離弾を発射。速度 {projectileSpeed:0.#}、ダメージ {rangedDamage:0.#}");
    }

    /// <summary>
    /// 全敵で共有する弾用Materialを取得する。
    /// </summary>
    private Material GetSharedProjectileMaterial(Material fallback)
    {
        if (sharedProjectileMaterial == null)
        {
            Material source =
                cubeMaterial != null
                ? cubeMaterial
                : fallback;

            if (source != null)
            {
                sharedProjectileMaterial =
                    new Material(source);

                sharedProjectileMaterial.name =
                    "SharedEnemyProjectileMaterial";

                sharedProjectileMaterial.EnableKeyword("_EMISSION");
            }
        }

        return sharedProjectileMaterial;
    }

    /// <summary>
    /// MaterialPropertyBlockで色を変更する。
    /// </summary>
    private static void SetPropertyBlockColor(
        Material material,
        MaterialPropertyBlock block,
        Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            block.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            block.SetColor("_Color", color);
    }

    // --------------------------------------------------
    // ダメージ
    // --------------------------------------------------

    public void ReportProjectileHit(float damage)
    {
        Log($"Enemy: 遠距離弾がプレイヤーに命中。ダメージ {damage:0.#}");

        ApplyPlayerDamage(damage);
    }

    private void ApplyPlayerDamage(float damage)
    {
        if (target != null)
        {
            target.SendMessageUpwards(
                "TakeDamage",
                damage,
                SendMessageOptions.DontRequireReceiver
            );
        }

        onPlayerDamaged.Invoke(damage);
    }

    // --------------------------------------------------
    // 状態管理
    // --------------------------------------------------

    private void SetState(EnemyActionState nextState)
    {
        if (currentState != nextState)
        {
            Log($"Enemy: 状態 {currentState} → {nextState}");
        }

        currentState = nextState;

        if (nextState != EnemyActionState.Attack)
            currentAttackType = EnemyAttackType.None;

        if (nextState != EnemyActionState.Attack &&
            weakPointLine != null)
        {
            weakPointLine.enabled = false;
        }

        if (nextState != EnemyActionState.Attack &&
            nextState != EnemyActionState.Stunned)
        {
            ResetAppearance();
        }
    }

    // --------------------------------------------------
    // 見た目
    // --------------------------------------------------

    private void ResetAppearance()
    {
        if (cubeRenderer != null)
            SetCubeColor(normalColor);

        if (weakPointLine != null)
            weakPointLine.enabled = false;
    }

    private Color GetCubeMaterialColor()
    {
        if (cubeMaterial == null)
            return Color.white;

        if (cubeMaterial.HasProperty("_BaseColor"))
            return cubeMaterial.GetColor("_BaseColor");

        if (cubeMaterial.HasProperty("_Color"))
            return cubeMaterial.GetColor("_Color");

        return Color.white;
    }

    private void SetCubeColor(Color color)
    {
        if (cubeRenderer == null || cubeMaterial == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        cubeRenderer.GetPropertyBlock(propertyBlock);

        SetPropertyBlockColor(
            cubeMaterial,
            propertyBlock,
            color
        );

        cubeRenderer.SetPropertyBlock(propertyBlock);
    }

    // --------------------------------------------------
    // HP・死亡処理
    // --------------------------------------------------

    /// <summary>
    /// 外部の武器やHP処理から敵に通常ダメージを与える。
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (defeated || amount <= 0f)
            return;

        currentHP =
            Mathf.Max(0f, currentHP - amount);

        if (healthBar != null)
            healthBar.SetHealth(currentHP);

        if (currentHP <= 0f)
        {
            defeated = true;

            gameObject.SetActive(false);
        }
    }

    // --------------------------------------------------
    // 弱点攻撃・スタン
    // --------------------------------------------------

    /// <summary>
    /// 弱点ラインをなぞる攻撃で呼び出す。
    /// 攻撃予告中のみスタンさせる。
    /// </summary>
    public void TraceWeakPoint(float swingSpeed)
    {
        if (currentState != EnemyActionState.Attack ||
            swingSpeed < minimumTraceSpeed)
        {
            return;
        }

        TakeDamage(
            Mathf.Max(8f, swingSpeed * 4f)
            + weakPointBonusDamage
        );

        if (defeated)
            return;

        SetState(EnemyActionState.Stunned);

        stunEndTime =
            Time.time + stunDuration;

        SetCubeColor(
            new Color(0.25f, 0.65f, 1f)
        );

        if (weakPointLine != null)
            weakPointLine.enabled = false;

        Log($"Enemy: 弱点をなぞられました。{stunDuration:0.#}秒スタンします。");

        onStunned.Invoke();
    }

    // --------------------------------------------------
    // トリガー判定
    // --------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        TryTrace(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTrace(other);
    }

    private void TryTrace(Collider other)
    {
        if (currentState != EnemyActionState.Attack)
            return;

        Rigidbody body = other.attachedRigidbody;

        float speed =
            body != null
            ? body.linearVelocity.magnitude
            : 0f;

        int id = other.GetInstanceID();

        Vector3 position = other.bounds.center;

        if (previousColliderPositions.TryGetValue(
            id,
            out Vector3 previousPosition))
        {
            float traceSpeed =
                Vector3.Distance(position, previousPosition)
                / Mathf.Max(Time.fixedDeltaTime, 0.001f);

            speed = Mathf.Max(speed, traceSpeed);
        }

        previousColliderPositions[id] = position;

        TraceWeakPoint(speed);
    }

    // --------------------------------------------------
    // 外部操作
    // --------------------------------------------------

    /// <summary>
    /// 外部から移動を強制する。
    /// </summary>
    public void ForceMove()
    {
        EnterMove();
    }

    /// <summary>
    /// 外部から待機を強制する。
    /// </summary>
    public void ForceWait()
    {
        EnterWait(
            Random.Range(
                waitDurationRange.x,
                waitDurationRange.y
            )
        );
    }

    /// <summary>
    /// 外部から攻撃を強制する。
    /// </summary>
    public void ForceAttack()
    {
        if (target != null &&
            TargetDistance() <= rangedRange)
        {
            EnterAttack();
        }
        else
        {
            EnterMove();
        }
    }

    // --------------------------------------------------
    // デバッグログ
    // --------------------------------------------------

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(message, this);
    }
}
