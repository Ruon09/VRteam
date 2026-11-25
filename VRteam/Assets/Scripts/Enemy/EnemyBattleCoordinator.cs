using UnityEngine;

/// <summary>Connects the merged enemy to the existing battle flow and player health.</summary>
[DisallowMultipleComponent]
public sealed class EnemyBattleCoordinator : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private EnemyStateMachine enemy;
    [SerializeField] private BattleFlow battleFlow;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (enemy == null)
            enemy = FindFirstObjectByType<EnemyStateMachine>();
        if (battleFlow == null)
            battleFlow = FindFirstObjectByType<BattleFlow>();
    }

    private void OnEnable()
    {
        if (enemy != null)
            enemy.Defeated += HandleEnemyDefeated;
        if (battleFlow != null)
            battleFlow.Finished += HandleBattleFinished;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.Defeated -= HandleEnemyDefeated;
        if (battleFlow != null)
            battleFlow.Finished -= HandleBattleFinished;
    }

    /// <summary>Receives EnemyStateMachine.SendMessageUpwards(float).</summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || playerHealth == null)
            return;

        // Keep the existing life-based rule: each accepted hit costs one life.
        playerHealth.TryTakeDamage(1);
    }

    private void HandleEnemyDefeated()
    {
        if (battleFlow == null)
            battleFlow = FindFirstObjectByType<BattleFlow>();

        if (battleFlow != null)
            battleFlow.OpponentDefeated();
        else
            Debug.LogError("Enemy was defeated, but BattleFlow could not be found.", this);
    }

    private void HandleBattleFinished(bool playerWon)
    {
        if (!playerWon && enemy != null && enemy.gameObject.activeSelf)
            enemy.gameObject.SetActive(false);
    }
}
