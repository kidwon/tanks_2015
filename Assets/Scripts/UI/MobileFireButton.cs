using UnityEngine;
using UnityEngine.EventSystems;

public class MobileFireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool m_IsHeld;
    private bool m_PressedThisFrame;
    private bool m_ReleasedThisFrame;

    public bool IsHeld => m_IsHeld;

    public bool ConsumePressed()
    {
        if (!m_PressedThisFrame)
        {
            return false;
        }

        m_PressedThisFrame = false;
        return true;
    }

    public bool ConsumeReleased()
    {
        if (!m_ReleasedThisFrame)
        {
            return false;
        }

        m_ReleasedThisFrame = false;
        return true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (m_IsHeld)
        {
            return;
        }

        m_IsHeld = true;
        m_PressedThisFrame = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!m_IsHeld)
        {
            return;
        }

        m_IsHeld = false;
        m_ReleasedThisFrame = true;
    }
}
