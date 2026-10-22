using UnityEngine;

public class ChildItemAnimationPlayer : MonoBehaviour
{
    [Header("Sprite Click Mover")]
    [SerializeField] private SpriteClickMover[] spriteClickMover;

    public void PlayChildItemAnimation()
    {
        if (spriteClickMover == null)
        {
            Debug.LogWarning(
                "SpriteClickMover reference is not assigned.",
                this
            );
            return;
        }

        spriteClickMover[0].PlayChildItemAnimation();
        spriteClickMover[1].PlayChildItemAnimation();
    }
}
