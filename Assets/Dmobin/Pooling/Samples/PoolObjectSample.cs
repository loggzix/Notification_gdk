using DSDK.Pooling;
using UnityEngine;

public class PoolObjectSample : MonoBehaviour, IPoolable
{
    public string PoolKey { get; set; }
    public GameObject obj => this.gameObject;
    public Transform trans => this.transform;
    public Transform poolTransform { get; set; }
    public void OnPool()
    {
        this.gameObject.SetActive(true);
    }

    public void OnReturnPool(bool setToPoolTransform = true)
    {
        this.gameObject.SetActive(false);

        if (setToPoolTransform)
        {
            if (poolTransform != null)
            {
                transform.SetParent(poolTransform);
            }
        }
    }
}
