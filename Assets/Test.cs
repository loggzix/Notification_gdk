using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    private void OnValidate()
    {
        Debug.Log("OnValidate");
        var txt = GetComponent<Text>();
        if (txt != null)
        {
            txt.text = transform.parent.name;
        }
    }
}
