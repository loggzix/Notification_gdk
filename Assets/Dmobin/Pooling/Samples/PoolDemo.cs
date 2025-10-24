using System.Collections;
using System.Collections.Generic;
using DSDK.Pooling;
using UnityEngine;

public class PoolDemo : MonoBehaviour, IPoolable
{
    public string PoolKey { get; set; }

    public GameObject obj => gameObject;

    public Transform trans => transform;
    public Transform poolTransform { get; set; }

    public void OnPool()
    {
        Debug.Log("OnPool");
    }

    public void OnReturnPool(bool setToPoolTransform = true)
    {
        Debug.Log("OnReturnPool");

        if (setToPoolTransform)
        {
            if (poolTransform != null)
            {
                transform.SetParent(poolTransform);
            }
        }
    }
}
