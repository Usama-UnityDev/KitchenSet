using UnityEngine;
using System.Collections;

public class SlideOutObject : MonoBehaviour
{
    public enum ExitDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Header("Exit Direction")]
    public ExitDirection direction = ExitDirection.Left;

    [Header("Animation Settings")]
    public float duration = 0.6f;
    [Tooltip("Turn off the GameObject after it leaves the screen.")]
    [SerializeField] private bool deactivateOnComplete = true;

    [Header("Extra Distance")]
    [Tooltip("Screen se kitna extra bahar jana hai")]
    public float extraDistance = 1f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isPlaying = false;
    public event System.Action Completed;

    private void OnEnable()
    {
        if (!isPlaying)
        {
            StartCoroutine(SlideOut());
        }
    }
    private void Start()
    {
        //if(this !=null)
        //    this.gameObject.SetActive(true);
    }
    private IEnumerator SlideOut()
    {
        isPlaying = true;

        startPosition = transform.position;

        targetPosition = GetTargetPosition();

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / duration);

            // Smooth animation
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t
            );

            yield return null;
        }

        // Exact final position
        transform.position = targetPosition;

        isPlaying = false;
        if (deactivateOnComplete)
            gameObject.SetActive(false);
        Completed?.Invoke();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isPlaying = false;
    }

    private Vector3 GetTargetPosition()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("Main Camera nahi mili!");
            return startPosition;
        }

        Vector3 screenPosition = cam.WorldToScreenPoint(startPosition);

        switch (direction)
        {
            case ExitDirection.Left:
                screenPosition.x = -Screen.width * extraDistance;
                break;

            case ExitDirection.Right:
                screenPosition.x = Screen.width * (1f + extraDistance);
                break;

            case ExitDirection.Top:
                screenPosition.y = Screen.height * (1f + extraDistance);
                break;

            case ExitDirection.Bottom:
                screenPosition.y = -Screen.height * extraDistance;
                break;
        }

        Vector3 worldPosition = cam.ScreenToWorldPoint(screenPosition);

        // Z same rakho
        worldPosition.z = startPosition.z;

        return worldPosition;
    }
}
