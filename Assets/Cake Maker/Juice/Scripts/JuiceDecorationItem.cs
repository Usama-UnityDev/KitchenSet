using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Mouse and single-touch drag; rejected releases return to the pickup position.</summary>
public sealed class JuiceDecorationItem : MonoBehaviour
{
    private JuiceDecorationController owner;
    private Camera view;
    private Collider2D pickup;
    private Vector3 home, scale, offset;
    private bool dragging, returning, landed;
    private int finger = -1;
    public void Initialize(JuiceDecorationController controller, SpriteRenderer visual)
    {
        owner = controller;
        view = Camera.main;
        pickup = GetComponent<Collider2D>();
        home = transform.position;
        scale = transform.localScale;
    }

    private Vector3 Project(Vector2 screen) => view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, view.WorldToScreenPoint(home).z));

    private void Update()
    {
        if (view == null || owner == null || returning || landed) return;
        Vector2 screen = Input.mousePosition;
        bool pressed = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0);
        if (Input.touchCount > 0)
        {
            if (!dragging)
            {
                Touch first = Input.GetTouch(0);
                finger = first.fingerId;
                screen = first.position;
                pressed = first.phase == TouchPhase.Began;
            }
            held = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != finger) continue;
                screen = touch.position;
                held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                break;
            }
        }
        else if (finger >= 0 && dragging) held = false;
        else finger = -1;
        if (!dragging)
        {
            if (!pressed || !pickup.OverlapPoint(Project(screen))) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(finger)) return;
            dragging = true;
            offset = transform.position - Project(screen);
            transform.DOScale(scale * 1.1f, 0.14f);
            owner.SetDragging(true);
        }
        if (held)
        {
            transform.position = Project(screen) + offset;
            return;
        }
        dragging = false;
        transform.DOKill();
        if (owner.TryDrop(this, transform.position)) { landed = true; return; }
        returning = true;
        transform.DOScale(scale, 0.3f);
        transform.DOMove(home, 0.35f).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            returning = false;
            owner.SetDragging(false);
            owner.GuideItem();
        });
    }

    private void OnDestroy() => transform.DOKill();
}
