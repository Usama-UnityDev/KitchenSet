using UnityEngine;
using UnityEngine.Events;

public class ChildItem : MonoBehaviour
{
    public static ChildItem instance;

    public UnityEvent ChildItemsAnimator;
    void Start()
    {
        instance = this;
    }

}
