using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileInputController : MonoBehaviour
{
    [SerializeField] private LayerMask m_GroundMask = ~0;
    [SerializeField] private float m_StopDistance = 1.5f;

    private static MobileInputController s_Instance;

    private readonly List<TankMovement> m_TankMovements = new List<TankMovement>();
    private readonly List<TankShooting> m_TankShootings = new List<TankShooting>();

    private TankMovement m_PrimaryMovement;
    private TankShooting m_PrimaryShooting;
    private MobileFireButton m_FireButton;
    private Camera m_MainCamera;
    private bool m_IsInitialized;

    private static bool ShouldEnable => Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer || Application.isEditor;

    public static void RegisterTank(TankMovement movement, TankShooting shooting)
    {
        EnsureInstance();
        s_Instance.RegisterInternal(movement, shooting);
    }

    public static void UnregisterTank(TankMovement movement, TankShooting shooting)
    {
        if (s_Instance == null)
        {
            return;
        }

        s_Instance.UnregisterInternal(movement, shooting);
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
        if (!ShouldEnable)
        {
            enabled = false;
            return;
        }

        BuildUi();
        CacheCamera();
    }

    private void Update()
    {
        if (!ShouldEnable)
        {
            return;
        }

        if (!m_IsInitialized)
        {
            BuildUi();
        }

        CacheCamera();
        CleanupDestroyedTanks();
        HandlePointerCommands();
        HandleFireButton();
    }

    private void RegisterInternal(TankMovement movement, TankShooting shooting)
    {
        if (movement != null && !m_TankMovements.Contains(movement))
        {
            m_TankMovements.Add(movement);
        }

        if (shooting != null && !m_TankShootings.Contains(shooting))
        {
            m_TankShootings.Add(shooting);
        }

        UpdatePrimaryTargets();
    }

    private void UnregisterInternal(TankMovement movement, TankShooting shooting)
    {
        if (movement != null)
        {
            m_TankMovements.Remove(movement);
            if (m_PrimaryMovement == movement)
            {
                m_PrimaryMovement = null;
            }
        }

        if (shooting != null)
        {
            m_TankShootings.Remove(shooting);
            if (m_PrimaryShooting == shooting)
            {
                m_PrimaryShooting = null;
            }
        }

        UpdatePrimaryTargets();
    }

    private void CleanupDestroyedTanks()
    {
        bool removed = false;

        for (int i = m_TankMovements.Count - 1; i >= 0; i--)
        {
            if (m_TankMovements[i] == null)
            {
                m_TankMovements.RemoveAt(i);
                removed = true;
            }
        }

        for (int i = m_TankShootings.Count - 1; i >= 0; i--)
        {
            if (m_TankShootings[i] == null)
            {
                m_TankShootings.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            UpdatePrimaryTargets();
        }
    }

    private void UpdatePrimaryTargets()
    {
        m_PrimaryMovement = SelectPrimary(m_TankMovements);
        m_PrimaryShooting = SelectPrimary(m_TankShootings);
    }

    private static T SelectPrimary<T>(List<T> list) where T : Component
    {
        T candidate = null;
        int bestPlayer = int.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            T entry = list[i];
            if (entry == null)
            {
                continue;
            }

            int playerNumber = GetPlayerNumber(entry);
            if (candidate == null || playerNumber < bestPlayer)
            {
                candidate = entry;
                bestPlayer = playerNumber;
            }
        }

        return candidate;
    }

    private static int GetPlayerNumber(Component component)
    {
        switch (component)
        {
            case TankMovement movement:
                return movement.m_PlayerNumber;
            case TankShooting shooting:
                return shooting.m_PlayerNumber;
            default:
                return int.MaxValue;
        }
    }

    private void HandlePointerCommands()
    {
        if (m_PrimaryMovement == null)
        {
            return;
        }

        if (!TryGetPointerPosition(out Vector2 pointerPosition, out int pointerId))
        {
            return;
        }

        if (IsPointerOverUi(pointerId))
        {
            return;
        }

        if (!TryGetGroundPosition(pointerPosition, out Vector3 worldPosition))
        {
            return;
        }

        m_PrimaryMovement.SetMoveTarget(worldPosition, m_StopDistance);
    }

    private void HandleFireButton()
    {
        if (m_PrimaryShooting == null || m_FireButton == null)
        {
            return;
        }

        bool pressed = m_FireButton.ConsumePressed();
        bool released = m_FireButton.ConsumeReleased();
        bool held = m_FireButton.IsHeld;

        m_PrimaryShooting.SetExternalFireInput(pressed, held, released);
    }

    private bool TryGetPointerPosition(out Vector2 position, out int pointerId)
    {
        position = default;
        pointerId = -1;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
            {
                position = touch.position;
                pointerId = touch.fingerId;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pointerId = -1;
            return true;
        }

        return false;
    }

    private static bool IsPointerOverUi(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (pointerId >= 0)
        {
            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool TryGetGroundPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        Camera targetCamera = m_MainCamera != null ? m_MainCamera : Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, m_GroundMask))
        {
            worldPosition = hit.point;
            return true;
        }

        return false;
    }

    private void BuildUi()
    {
        if (m_IsInitialized)
        {
            return;
        }

        EnsureEventSystem();

        bool shouldShowButton = Application.isMobilePlatform || Input.touchSupported;
        if (!shouldShowButton)
        {
            m_IsInitialized = true;
            return;
        }

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

        m_FireButton = fireButtonObject.GetComponent<MobileFireButton>();
        m_IsInitialized = true;
    }

    private void CacheCamera()
    {
        if (m_MainCamera != null)
        {
            return;
        }

        var main = Camera.main;
        if (main != null)
        {
            m_MainCamera = main;
        }
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
