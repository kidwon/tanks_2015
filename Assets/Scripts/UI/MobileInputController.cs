using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileInputController : MonoBehaviour
{
    private static MobileInputController s_Instance;

    private readonly List<TankMovement> m_TankMovements = new List<TankMovement>();
    private readonly List<TankShooting> m_TankShootings = new List<TankShooting>();

    private VirtualJoystick m_Joystick;
    private MobileFireButton m_FireButton;
    private bool m_IsInitialized;

    private static bool IsMobileRuntime
    {
        get
        {
            if (Application.isMobilePlatform)
            {
                return true;
            }

            if (SystemInfo.deviceType == DeviceType.Handheld)
            {
                return true;
            }

            if (Application.platform == RuntimePlatform.WebGLPlayer && Input.touchSupported)
            {
                return true;
            }

            return Input.touchSupported;
        }
    }

    public static void RegisterTank(TankMovement movement, TankShooting shooting)
    {
        EnsureInstance();

        if (!IsMobileRuntime)
        {
            return;
        }

        if (movement == null || shooting == null)
        {
            return;
        }
        s_Instance.AddTank(movement, shooting);
    }

    public static void UnregisterTank(TankMovement movement, TankShooting shooting)
    {
        if (s_Instance == null)
        {
            return;
        }

        s_Instance.RemoveTank(movement, shooting);
    }

    private static void EnsureInstance()
    {
        if (s_Instance != null)
        {
            return;
        }

        var controllerObject = new GameObject("MobileInputController");
        DontDestroyOnLoad(controllerObject);
        s_Instance = controllerObject.AddComponent<MobileInputController>();
    }

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
    }

    private void Start()
    {
        if (!IsMobileRuntime)
        {
            gameObject.SetActive(false);
            return;
        }

        BuildUi();
    }

    private void Update()
    {
        if (!m_IsInitialized)
        {
            return;
        }

        CleanupDestroyedTanks();

        Vector2 input = m_Joystick != null ? m_Joystick.Direction : Vector2.zero;
        for (int i = 0; i < m_TankMovements.Count; i++)
        {
            TankMovement movement = m_TankMovements[i];
            if (movement != null)
            {
                movement.SetExternalInput(input.y, input.x);
            }
        }

        bool pressed = m_FireButton != null && m_FireButton.ConsumePressed();
        bool released = m_FireButton != null && m_FireButton.ConsumeReleased();
        bool held = m_FireButton != null && m_FireButton.IsHeld;

        for (int i = 0; i < m_TankShootings.Count; i++)
        {
            TankShooting shooting = m_TankShootings[i];
            if (shooting != null)
            {
                shooting.SetExternalFireInput(pressed, held, released);
            }
        }
    }

    private void OnDestroy()
    {
        if (s_Instance == this)
        {
            s_Instance = null;
        }
    }

    private void AddTank(TankMovement movement, TankShooting shooting)
    {
        if (!m_TankMovements.Contains(movement))
        {
            m_TankMovements.Add(movement);
        }

        if (!m_TankShootings.Contains(shooting))
        {
            m_TankShootings.Add(shooting);
        }

        if (m_IsInitialized)
        {
            movement.SetExternalInput(0f, 0f);
            shooting.SetExternalFireInput(false, false, false);
        }
    }

    private void RemoveTank(TankMovement movement, TankShooting shooting)
    {
        if (movement != null)
        {
            movement.DisableExternalInput();
        }

        if (shooting != null)
        {
            shooting.DisableExternalFireInput();
        }

        m_TankMovements.Remove(movement);
        m_TankShootings.Remove(shooting);
    }

    private void CleanupDestroyedTanks()
    {
        for (int i = m_TankMovements.Count - 1; i >= 0; i--)
        {
            if (m_TankMovements[i] == null)
            {
                m_TankMovements.RemoveAt(i);
            }
        }

        for (int i = m_TankShootings.Count - 1; i >= 0; i--)
        {
            if (m_TankShootings[i] == null)
            {
                m_TankShootings.RemoveAt(i);
            }
        }
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        Sprite backgroundSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        Sprite knobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        Sprite buttonSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        Font labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvasObject = new GameObject("MobileControlsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var joystickObject = new GameObject("VirtualJoystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
        joystickObject.transform.SetParent(canvasObject.transform, false);
        var joystickRect = joystickObject.GetComponent<RectTransform>();
        joystickRect.anchorMin = new Vector2(0f, 0f);
        joystickRect.anchorMax = new Vector2(0f, 0f);
        joystickRect.pivot = new Vector2(0.5f, 0.5f);
        joystickRect.anchoredPosition = new Vector2(200f, 200f);
        joystickRect.sizeDelta = new Vector2(220f, 220f);
        var joystickImage = joystickObject.GetComponent<Image>();
        joystickImage.sprite = backgroundSprite;
        joystickImage.color = new Color(1f, 1f, 1f, 0.35f);
        joystickImage.raycastTarget = true;

        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(joystickObject.transform, false);
        var handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(110f, 110f);
        var handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = knobSprite;
        handleImage.color = new Color(1f, 1f, 1f, 0.8f);
        handleImage.raycastTarget = false;

        var joystick = joystickObject.GetComponent<VirtualJoystick>();
        joystick.Configure(handleRect, joystickRect.sizeDelta.x * 0.5f);

        var fireButtonObject = new GameObject("FireButton", typeof(RectTransform), typeof(Image), typeof(MobileFireButton));
        fireButtonObject.transform.SetParent(canvasObject.transform, false);
        var fireRect = fireButtonObject.GetComponent<RectTransform>();
        fireRect.anchorMin = new Vector2(1f, 0f);
        fireRect.anchorMax = new Vector2(1f, 0f);
        fireRect.pivot = new Vector2(0.5f, 0.5f);
        fireRect.anchoredPosition = new Vector2(-200f, 200f);
        fireRect.sizeDelta = new Vector2(180f, 180f);
        var fireImage = fireButtonObject.GetComponent<Image>();
        fireImage.sprite = buttonSprite;
        fireImage.color = new Color(1f, 1f, 1f, 0.45f);
        fireImage.type = Image.Type.Sliced;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(fireButtonObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<Text>();
        label.text = "FIRE";
        label.alignment = TextAnchor.MiddleCenter;
        label.font = labelFont;
        label.fontSize = 36;
        label.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        m_Joystick = joystick;
        m_FireButton = fireButtonObject.GetComponent<MobileFireButton>();
        m_IsInitialized = true;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
        var module = eventSystemObject.GetComponent<StandaloneInputModule>();
        module.forceModuleActive = true;
    }
}
