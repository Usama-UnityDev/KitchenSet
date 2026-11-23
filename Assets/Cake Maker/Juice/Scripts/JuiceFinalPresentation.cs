using System.Collections;
using UnityEngine;

public sealed class JuiceFinalPresentation : ServingFinalPresentation
{
    [SerializeField, Min(0f)] private float glassExitDelay = 0.35f;
    [SerializeField, Min(0.1f)] private float glassExitDuration = 1.2f;
    [SerializeField, Min(0f)] private float glassExitOvershoot = 0.25f;

    private SlideOutObject glassExit;
    private bool glassExited;

    public IEnumerator PrepareAndPresentDrink(Transform glass, SpriteRenderer glassFront, SlideOutObject exit)
    {
        if (glassExitDelay > 0f)
            yield return new WaitForSeconds(glassExitDelay);

        if (exit != null)
        {
            glassExit = exit;
            glassExited = false;
            exit.direction = SlideOutObject.ExitDirection.Right;
            exit.duration = glassExitDuration;
            exit.extraDistance = glassExitOvershoot;
            exit.Completed += OnGlassExited;
            exit.enabled = true;
            while (!glassExited)
                yield return null;
            ClearGlassExit();
        }

        glass.gameObject.SetActive(false);
        BuildServedItem(glass, glassFront, true);
        yield return ArriveAndPhotograph(Vector3.right);
    }

    private void OnGlassExited() => glassExited = true;

    private void ClearGlassExit()
    {
        if (glassExit == null)
            return;

        glassExit.Completed -= OnGlassExited;
        glassExit.enabled = false;
        glassExit = null;
    }

    public void ResetDrinkPresentation()
    {
        ClearGlassExit();
        ResetPresentation();
    }

    protected override void OnDestroy()
    {
        ClearGlassExit();
        base.OnDestroy();
    }
}
