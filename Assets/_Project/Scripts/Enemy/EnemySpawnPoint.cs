using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private ThreatLevel minimumThreatLevel = ThreatLevel.Calm;
    [SerializeField] private float minimumPlayerDistance = 15f;
    [SerializeField] private float reuseCooldown = 15f;

    private float lastUsedTime = float.NegativeInfinity;

    public Vector3 Position => transform.position;

    public bool IsValid()
    {
        if (!gameObject.activeInHierarchy)
            return false;

        ThreatLevel currentLevel = ThreatManager.Instance != null
            ? ThreatManager.Instance.CurrentLevel
            : ThreatLevel.Calm;

        if (currentLevel < minimumThreatLevel)
            return false;

        if (Time.time - lastUsedTime < reuseCooldown)
            return false;

        // TEMP playtest: spawn even with players nearby.
        // return !IsAnyPlayerTooClose();
        return true;
    }

    // Higher score = safer/better pick; farthest nearby player wins among valid points.
    public float GetScore()
    {
        float nearestSqrDistance = float.PositiveInfinity;

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive)
                continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
                nearestSqrDistance = sqrDistance;
        }

        return float.IsPositiveInfinity(nearestSqrDistance) ? float.MaxValue : Mathf.Sqrt(nearestSqrDistance);
    }

    public void MarkUsed()
    {
        lastUsedTime = Time.time;
    }

    private bool IsAnyPlayerTooClose()
    {
        float minSqrDistance = minimumPlayerDistance * minimumPlayerDistance;

        foreach (PlayerController player in PlayerController.ActivePlayers)
        {
            if (player == null || !player.IsAlive)
                continue;

            float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < minSqrDistance)
                return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.forward
        );
    }
}