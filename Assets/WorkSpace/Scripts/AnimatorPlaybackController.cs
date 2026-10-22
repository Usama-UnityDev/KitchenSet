using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable controls for playing, pausing, and positioning an Animator.
/// Normalized time uses 0 for the beginning and 1 for the end.
/// </summary>
[DisallowMultipleComponent]
public class AnimatorPlaybackController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float playSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float stopPoint = 0.5f;
    [SerializeField, Min(0)] private int layerIndex;

    private Coroutine pauseAtPointCoroutine;

    public bool IsPaused =>
        animator != null && Mathf.Approximately(animator.speed, 0f);

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void PlayAnimator()
    {
        if (!TryGetAnimator())
            return;

        CancelScheduledPause();
        animator.enabled = true;
        animator.speed = playSpeed;
    }

    public void PauseAnimator()
    {
        if (!TryGetAnimator())
            return;

        CancelScheduledPause();
        animator.speed = 0f;
    }

    public void PauseAtConfiguredPoint()
    {
        PauseAtPoint(stopPoint);
    }

    public void PauseAtPoint(float normalizedTime)
    {
        if (!TryGetAnimator())
            return;

        CancelScheduledPause();
        animator.enabled = true;

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(layerIndex);

        animator.Play(
            state.fullPathHash,
            layerIndex,
            Mathf.Clamp01(normalizedTime)
        );

        animator.Update(0f);
        animator.speed = 0f;
    }

    public void PlayUntilConfiguredPoint()
    {
        PlayUntilPoint(stopPoint);
    }

    public void PlayUntilPoint(float normalizedTime)
    {
        if (!TryGetAnimator())
            return;

        CancelScheduledPause();
        animator.enabled = true;
        animator.speed = playSpeed;

        pauseAtPointCoroutine = StartCoroutine(
            PauseWhenPointIsReached(Mathf.Clamp01(normalizedTime))
        );
    }

    public void PlayState(string stateName)
    {
        if (!TryGetAnimator() || string.IsNullOrWhiteSpace(stateName))
            return;

        CancelScheduledPause();
        animator.enabled = true;
        animator.speed = playSpeed;
        animator.Play(stateName, layerIndex, 0f);
    }

    public void RestartAnimator()
    {
        if (!TryGetAnimator())
            return;

        CancelScheduledPause();
        animator.enabled = true;

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(layerIndex);

        animator.Play(state.fullPathHash, layerIndex, 0f);
        animator.speed = playSpeed;
    }

    public void SetPlaySpeed(float speed)
    {
        playSpeed = Mathf.Max(0f, speed);

        if (animator != null && !IsPaused)
            animator.speed = playSpeed;
    }

    private IEnumerator PauseWhenPointIsReached(float normalizedTime)
    {
        int stateHash =
            animator.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash;

        while (animator != null && animator.enabled)
        {
            AnimatorStateInfo state =
                animator.GetCurrentAnimatorStateInfo(layerIndex);

            if (state.fullPathHash != stateHash)
                break;

            if (state.normalizedTime >= normalizedTime)
            {
                animator.Play(
                    state.fullPathHash,
                    layerIndex,
                    normalizedTime
                );

                animator.Update(0f);
                animator.speed = 0f;
                break;
            }

            yield return null;
        }

        pauseAtPointCoroutine = null;
    }

    private void CancelScheduledPause()
    {
        if (pauseAtPointCoroutine == null)
            return;

        StopCoroutine(pauseAtPointCoroutine);
        pauseAtPointCoroutine = null;
    }

    private bool TryGetAnimator()
    {
        if (animator != null)
            return true;

        animator = GetComponent<Animator>();
        if (animator != null)
            return true;

        Debug.LogWarning(
            $"{nameof(AnimatorPlaybackController)} on '{name}' has no Animator assigned.",
            this
        );

        return false;
    }
}
