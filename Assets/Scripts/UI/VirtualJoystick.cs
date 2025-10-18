using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private float m_MaxRadius = 80f;

    private RectTransform m_BaseRect;
    private RectTransform m_Handle;
    private Vector2 m_Input;

    public Vector2 Direction => m_Input;

    private void Awake()
    {
        m_BaseRect = transform as RectTransform;
    }

    public void Configure(RectTransform handle, float maxRadius)
    {
        m_Handle = handle;
        if (maxRadius > 0f)
        {
            m_MaxRadius = maxRadius;
        }
        else if (m_BaseRect != null)
        {
            m_MaxRadius = Mathf.Max(m_BaseRect.sizeDelta.x, m_BaseRect.sizeDelta.y) * 0.5f;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (m_BaseRect == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_BaseRect, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            return;
        }

        float clampRadius = m_MaxRadius > 0f ? m_MaxRadius : Mathf.Max(m_BaseRect.sizeDelta.x, m_BaseRect.sizeDelta.y) * 0.5f;
        localPoint = Vector2.ClampMagnitude(localPoint, clampRadius);
        m_Input = clampRadius > 0f ? localPoint / clampRadius : Vector2.zero;

        if (m_Handle != null)
        {
            m_Handle.anchoredPosition = localPoint;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        m_Input = Vector2.zero;
        if (m_Handle != null)
        {
            m_Handle.anchoredPosition = Vector2.zero;
        }
    }
}
