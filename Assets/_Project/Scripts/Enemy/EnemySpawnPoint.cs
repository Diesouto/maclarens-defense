using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    public Vector3 Position => transform.position;

    public bool IsValid()
    {
        return gameObject.activeInHierarchy;
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