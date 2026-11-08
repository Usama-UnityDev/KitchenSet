using UnityEngine;
using System.Collections;

public class SpriteEnterAnimation : MonoBehaviour
{

    public enum EnterDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Header("Direction")]
    public EnterDirection direction = EnterDirection.Left;

    [Header("Animation")]
    public float duration = 0.6f;
    public float delay = 0f;
    [SerializeField] private bool replayOnEnable;

    public bool IsComplete { get; private set; }

    private Vector3 originalPosition;
    private Vector3 startPosition;
    private Coroutine entrance;

    private void Awake()
    {
        // Scene mein sprite ki original/final position save karo
        originalPosition = transform.position;

        if (replayOnEnable)
            return;

        // Khud calculate karo ke sprite kahan se start hogi
        startPosition = GetStartPosition();

        // Sprite ko automatically screen ke bahar move karo
        transform.position = startPosition;
    }

    private void Start()
    {
        if (!replayOnEnable)
            StartCoroutine(EnterAnimation());
    }

    private void OnEnable()
    {
        if (!replayOnEnable)
            return;

        IsComplete = false;
        startPosition = GetStartPosition();
        transform.position = startPosition;
        entrance = StartCoroutine(EnterAnimation());
    }

    private void OnDisable()
    {
        if (!replayOnEnable)
            return;

        if (entrance != null)
            StopCoroutine(entrance);
        entrance = null;
        transform.position = originalPosition;
    }

    private Vector3 GetStartPosition()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Main Camera nahi mili!");
            return originalPosition;
        }

        Vector3 screenPosition = cam.WorldToScreenPoint(originalPosition);

        // Sprite ki screen position ke hisaab se
        // usay screen ke completely bahar bhejo
        switch (direction)
        {
            case EnterDirection.Left:
                screenPosition.x = -Screen.width;
                break;

            case EnterDirection.Right:
                screenPosition.x = Screen.width * 2f;
                break;

            case EnterDirection.Top:
                screenPosition.y = Screen.height * 2f;
                break;

            case EnterDirection.Bottom:
                screenPosition.y = -Screen.height;
                break;
        }

        Vector3 worldPosition = cam.ScreenToWorldPoint(screenPosition);

        // Z original jaisa hi rakho
        worldPosition.z = originalPosition.z;

        return worldPosition;
    }

    private IEnumerator EnterAnimation()
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);

            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                startPosition,
                originalPosition,
                t
            );
            
            yield return null;
        }

        transform.position = originalPosition;

        // [NEW CODE] SpriteClickMover ko actual shelf position set karwayein
        SpriteClickMover mover = GetComponent<SpriteClickMover>();
        if (mover == null)
            mover = GetComponentInChildren<SpriteClickMover>();

        if (mover != null)
        {
            mover.SetOriginPosition(mover.transform.position);
        }
        // [NEW CODE] Loop khatam hone ke baad, animation complete par script ko disable karein
        IsComplete = true;
        entrance = null;
        if (!replayOnEnable)
            enabled = false;
    }
}
