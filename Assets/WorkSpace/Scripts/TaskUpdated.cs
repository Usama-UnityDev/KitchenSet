using System.Collections;
using UnityEngine;

public class TaskUpdated : MonoBehaviour//SpriteHoldAndMove
{
    [Header("Batch Settings")]
    public bool isBatchItem = false;
    public int totalBatchItems = 2;

    [Header("Slide Out Target (Khali Plate)")]
    [Tooltip("Yahan 'Eggs Takeover' assign karein jise screen se bahar bhejna hai")]
    public SlideOutObject plateSlideOut;

    // Static counter jo dono eggs ke darmian count yaad rakhega
    private static int currentBrokenCount = 0;

    public static TaskUpdated instance { get; private set; }
    // Jab Animation Event is function ko call karega:
    public void TaskUpdate()
    {
        if (isBatchItem)
        {
            currentBrokenCount++;

            // Agar abhi pehla anda toota hai, toh yahin ruk jao
            if (currentBrokenCount < totalBatchItems)
            {
                return;
            }

            // Dono anday toot gaye, counter zero karein
            currentBrokenCount = 0;
        }

        // Dono anday tootne par plate ko bahar bhejna aur next task call karna
        StartCoroutine(CompleteStepRoutine());
    }

    private IEnumerator CompleteStepRoutine()
    {
        // 1. Agar plate par SlideOutObject laga hai, toh usko play karein
        if (plateSlideOut != null)
        {
            plateSlideOut.enabled = true;
            // Plate ko screen se nikalne ka time dein
            yield return new WaitForSeconds(plateSlideOut.duration > 0f ? plateSlideOut.duration : 1f);
        }

        // 2. Uske baad agla task (Next Ingredient) shuru karein
        if (SequentialAnimatorController.Instance != null)
        {
            SequentialAnimatorController.Instance.UpdateTask();
        }
    }
}