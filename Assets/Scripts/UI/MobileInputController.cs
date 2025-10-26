using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MobileInputController : MonoBehaviour
{
    [SerializeField] private LayerMask m_GroundMask = ~0;
    [SerializeField] private float m_StopDistance = 1.5f;
    [SerializeField] private bool m_EnableDebugOverlay = true;
    [SerializeField] private int m_DebugHistorySize = 12;
    [SerializeField] private float m_TargetMarkerHeight = 2.5f;
    [SerializeField] private float m_TargetMarkerFloatAmplitude = 0.35f;
    [SerializeField] private float m_TargetMarkerFloatSpeed = 4.2f;
    [SerializeField] private Vector2 m_TargetMarkerSize = new Vector2(68f, 96f);
    [SerializeField] private Vector2 m_FullscreenButtonSize = new Vector2(180f, 80f);
    [SerializeField] private float m_TargetMarkerEntryDuration = 0.18f;
    [SerializeField] private float m_TargetMarkerExitDuration = 0.18f;
    [SerializeField] private float m_TargetMarkerEntryOffset = 220f;
    [SerializeField] private float m_TargetMarkerExitOffset = 220f;

    private static MobileInputController s_Instance;
    private static Sprite s_ButtonSprite;
    private static Sprite s_TargetSprite;
    private static Sprite s_MenuButtonSprite;
    private static Font s_DebugFont;
    private static readonly string[] s_FontCandidates =
    {
        "LegacyRuntime",
        "Arial",
        "Helvetica",
        "Liberation Sans",
        "Noto Sans"
    };

    private enum TargetMarkerState
    {
        Hidden,
        Entering,
        Active,
        Exiting
    }

    private readonly List<TankMovement> m_TankMovements = new List<TankMovement>();
    private readonly List<TankShooting> m_TankShootings = new List<TankShooting>();

    private TankMovement m_PrimaryMovement;
    private TankShooting m_PrimaryShooting;
    private TankMovement m_TargetedEnemy;
    private MobileFireButton m_FireButton;
    private Camera m_MainCamera;
    private bool m_IsInitialized;
    private Text m_DebugText;
    private ScrollRect m_DebugScroll;
    private RectTransform m_DebugContent;
    private RectTransform m_TargetMarker;
    private Image m_TargetMarkerImage;
    private TargetMarkerState m_TargetMarkerState = TargetMarkerState.Hidden;
    private float m_TargetMarkerTimer;
    private bool m_TargetMarkerAnimationInitialized;
    private Vector2 m_TargetMarkerAnchorCurrent;
    private Vector2 m_TargetMarkerAnchorFrom;
    private Vector2 m_TargetMarkerAnchorTarget;
    private Button m_FullscreenButton;
    private Text m_FullscreenLabel;
    private readonly List<string> m_DebugEntries = new List<string>();

    private static bool ShouldEnable => Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer || Application.isEditor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void RegisterTank(TankMovement movement, TankShooting shooting)
    {
        EnsureInstance();

        if (movement == null && shooting == null)
        {
            s_Instance?.LogDebug("RegisterTank called with null references", 2f);
            return;
        }

        s_Instance.RegisterInternal(movement, shooting);
        if (movement != null)
        {
            s_Instance.LogDebug($"Registered tank movement: Player {movement.m_PlayerNumber}", 2f);
        }
        else if (shooting != null)
        {
            s_Instance.LogDebug($"Registered tank shooting: Player {shooting.m_PlayerNumber}", 2f);
        }
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
        EnsurePrimaryTargets();
        HandlePointerCommands();
        HandleFireButton();
        UpdateTargetMarker();
        RefreshFullscreenButton();

    }

    private void RegisterInternal(TankMovement movement, TankShooting shooting)
    {
        if (movement != null && !m_TankMovements.Contains(movement))
        {
            m_TankMovements.Add(movement);
            LogDebug($"Movement registered. Total: {m_TankMovements.Count}", 1.5f);
        }

        if (shooting != null && !m_TankShootings.Contains(shooting))
        {
            m_TankShootings.Add(shooting);
            LogDebug($"Shooting registered. Total: {m_TankShootings.Count}", 1.5f);
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
            TankMovement movement = m_TankMovements[i];
            if (movement == null)
            {
                m_TankMovements.RemoveAt(i);
                removed = true;
                LogDebug("Removed null movement reference", 1.5f);
                if (m_TargetedEnemy == movement)
                {
                    ClearTargetedEnemy();
                }
            }
        }

        for (int i = m_TankShootings.Count - 1; i >= 0; i--)
        {
            if (m_TankShootings[i] == null)
            {
                m_TankShootings.RemoveAt(i);
                removed = true;
                LogDebug("Removed null shooting reference", 1.5f);
            }
        }

        if (removed)
        {
            UpdatePrimaryTargets();
        }

        if (m_TargetedEnemy == null || !m_TargetedEnemy.isActiveAndEnabled)
        {
            ClearTargetedEnemy();
        }
    }

    private void EnsurePrimaryTargets()
    {
        if (m_PrimaryMovement != null && m_PrimaryShooting != null)
        {
            return;
        }

        if (m_TankMovements.Count == 0 || m_TankShootings.Count == 0)
        {
            AutoDiscoverTanks();
        }

        UpdatePrimaryTargets();
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
            AutoDiscoverTanks();
            if (m_PrimaryMovement == null)
            {
                TankMovement fallbackMovement = FindActiveMovement();
                if (fallbackMovement != null)
                {
                    RegisterInternal(fallbackMovement, fallbackMovement.GetComponent<TankShooting>());
                    LogDebug($"Fallback registered movement: Player {fallbackMovement.m_PlayerNumber}", 2f);
                }
            }

            if (m_PrimaryMovement == null)
            {
                LogDebug("No tank movement registered yet", 2f);
                return;
            }
        }

        if (!TryGetPointerPosition(out Vector2 pointerPosition, out int pointerId))
        {
            return;
        }

        if (IsPointerOverUi(pointerPosition, pointerId))
        {
            LogDebug("Pointer over UI – ignoring move", 1.5f);
            return;
        }

        Camera targetCamera = m_MainCamera != null ? m_MainCamera : Camera.main;
        if (targetCamera == null)
        {
            LogDebug("No camera available for click-to-move", 2.5f);
            return;
        }

        if (TryGetEnemyTank(pointerPosition, targetCamera, out TankMovement enemyTank))
        {
            SetTargetedEnemy(enemyTank);
            m_PrimaryMovement.SetLookTarget(enemyTank.transform.position);
            LogDebug($"Aiming at enemy tank: Player {enemyTank.m_PlayerNumber}", 1.5f);
            return;
        }

        if (!TryGetGroundPosition(pointerPosition, targetCamera, out Vector3 worldPosition))
        {
            return;
        }

        ClearTargetedEnemy();
        m_PrimaryMovement.SetMoveTarget(worldPosition, m_StopDistance);
        LogDebug("Moving towards tapped position", 1.5f);
    }

    private void SetTargetedEnemy(TankMovement enemy)
    {
        if (enemy == null)
        {
            ClearTargetedEnemy();
            return;
        }

        m_TargetedEnemy = enemy;
        m_TargetMarkerState = TargetMarkerState.Entering;
        m_TargetMarkerTimer = 0f;
        m_TargetMarkerAnimationInitialized = false;

        if (m_TargetMarker != null)
        {
            m_TargetMarker.gameObject.SetActive(false);
            SetMarkerAlpha(0f);
        }
    }

    private void ClearTargetedEnemy(bool immediate = false)
    {
        m_TargetedEnemy = null;

        if (m_TargetMarker == null)
        {
            return;
        }

        if (immediate)
        {
            m_TargetMarkerState = TargetMarkerState.Hidden;
            m_TargetMarkerTimer = 0f;
            m_TargetMarkerAnimationInitialized = false;
            m_TargetMarker.gameObject.SetActive(false);
            SetMarkerAlpha(0f);
            return;
        }

        if (m_TargetMarkerState == TargetMarkerState.Hidden || m_TargetMarkerState == TargetMarkerState.Exiting)
        {
            return;
        }

        m_TargetMarkerState = TargetMarkerState.Exiting;
        m_TargetMarkerTimer = 0f;
        m_TargetMarkerAnimationInitialized = false;
    }

    private TankMovement FindActiveMovement()
    {
        try
        {
#if UNITY_2021_2_OR_NEWER
            TankMovement[] candidates = FindObjectsOfType<TankMovement>(true);
#else
            TankMovement[] candidates = FindObjectsOfType<TankMovement>();
#endif
            for (int i = 0; i < candidates.Length; i++)
            {
                TankMovement movement = candidates[i];
                if (movement == null)
                {
                    continue;
                }

                if (!movement.isActiveAndEnabled)
                {
                    continue;
                }

                return movement;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private void AutoDiscoverTanks()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogDebug("Attempting to auto-discover tank controls", 1.5f);
#endif
        TankMovement[] movements;
        try
        {
#if UNITY_2021_2_OR_NEWER
            movements = FindObjectsOfType<TankMovement>(true);
#else
            movements = FindObjectsOfType<TankMovement>();
#endif
        }
        catch
        {
            return;
        }

        bool anyRegistered = false;
        for (int i = 0; i < movements.Length; i++)
        {
            TankMovement movement = movements[i];
            if (movement == null)
            {
                continue;
            }

            if (!m_TankMovements.Contains(movement))
            {
                TankShooting shooting = movement.GetComponent<TankShooting>();
                RegisterInternal(movement, shooting);
                anyRegistered = true;
            }
        }

        if (anyRegistered)
        {
            LogDebug($"Auto-discovered {m_TankMovements.Count} movement scripts", 1.5f);
        }
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

        if (pressed)
        {
            LogDebug("Fire button pressed", 1.25f);
        }
        else if (released)
        {
            LogDebug("Fire button released", 1.25f);
        }
    }

    private void UpdateTargetMarker()
    {
        if (m_TargetMarker == null)
        {
            return;
        }

        if (m_TargetMarkerState == TargetMarkerState.Hidden && m_TargetedEnemy == null)
        {
            if (m_TargetMarker.gameObject.activeSelf)
            {
                m_TargetMarker.gameObject.SetActive(false);
            }

            SetMarkerAlpha(0f);
            return;
        }

        RectTransform canvasRect = m_TargetMarker.parent as RectTransform;
        bool hasTargetPosition = false;

        if (m_TargetedEnemy != null && canvasRect != null)
        {
            Camera targetCamera = m_MainCamera != null ? m_MainCamera : Camera.main;
            if (targetCamera != null && m_TargetedEnemy.isActiveAndEnabled)
            {
                float floatOffset = 0f;
                if (m_TargetMarkerFloatAmplitude > 0f && m_TargetMarkerFloatSpeed > 0f)
                {
                    floatOffset = Mathf.Sin(Time.unscaledTime * m_TargetMarkerFloatSpeed) * m_TargetMarkerFloatAmplitude;
                }

                Vector3 worldPosition = m_TargetedEnemy.transform.position + Vector3.up * (m_TargetMarkerHeight + floatOffset);
                Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPosition);
                if (screenPoint.z > 0f)
                {
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(screenPoint.x, screenPoint.y), null, out Vector2 localPoint))
                    {
                        m_TargetMarkerAnchorTarget = localPoint;
                        hasTargetPosition = true;
                    }
                }
            }
        }

        switch (m_TargetMarkerState)
        {
            case TargetMarkerState.Hidden:
                if (m_TargetedEnemy != null)
                {
                    m_TargetMarkerState = TargetMarkerState.Entering;
                    m_TargetMarkerTimer = 0f;
                    m_TargetMarkerAnimationInitialized = false;
                }
                break;
            case TargetMarkerState.Entering:
                HandleMarkerEntering(hasTargetPosition);
                break;
            case TargetMarkerState.Active:
                HandleMarkerActive(hasTargetPosition);
                break;
            case TargetMarkerState.Exiting:
                HandleMarkerExiting();
                break;
        }

        if (m_TargetMarker.gameObject.activeSelf)
        {
            m_TargetMarker.anchoredPosition = m_TargetMarkerAnchorCurrent;
        }
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
                LogDebug($"Touch began: id {pointerId}", 1f);
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            pointerId = -1;
            LogDebug("Mouse click detected", 1f);
            return true;
        }

        return false;
    }

    private void HandleMarkerEntering(bool hasTargetPosition)
    {
        if (!hasTargetPosition)
        {
            return;
        }

        if (!m_TargetMarkerAnimationInitialized)
        {
            m_TargetMarkerAnchorFrom = m_TargetMarkerAnchorTarget + new Vector2(0f, m_TargetMarkerEntryOffset);
            m_TargetMarkerAnchorCurrent = m_TargetMarkerAnchorFrom;
            m_TargetMarkerAnimationInitialized = true;
            m_TargetMarkerTimer = 0f;

            if (!m_TargetMarker.gameObject.activeSelf)
            {
                m_TargetMarker.gameObject.SetActive(true);
            }

            SetMarkerAlpha(0f);
        }

        m_TargetMarkerTimer += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, m_TargetMarkerEntryDuration);
        float t = Mathf.Clamp01(m_TargetMarkerTimer / duration);
        float eased = EaseOutCubic(t);

        m_TargetMarkerAnchorCurrent = Vector2.Lerp(m_TargetMarkerAnchorFrom, m_TargetMarkerAnchorTarget, eased);
        SetMarkerAlpha(eased);

        if (t >= 1f - Mathf.Epsilon)
        {
            m_TargetMarkerState = TargetMarkerState.Active;
            m_TargetMarkerTimer = 0f;
            m_TargetMarkerAnimationInitialized = false;
            m_TargetMarkerAnchorCurrent = m_TargetMarkerAnchorTarget;
            SetMarkerAlpha(1f);
        }
    }

    private void HandleMarkerActive(bool hasTargetPosition)
    {
        if (!m_TargetMarker.gameObject.activeSelf)
        {
            m_TargetMarker.gameObject.SetActive(true);
        }

        SetMarkerAlpha(1f);

        if (!hasTargetPosition)
        {
            ClearTargetedEnemy();
            return;
        }

        float followFactor = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f);
        followFactor = Mathf.Clamp01(followFactor);
        m_TargetMarkerAnchorCurrent = Vector2.Lerp(m_TargetMarkerAnchorCurrent, m_TargetMarkerAnchorTarget, followFactor);
    }

    private void HandleMarkerExiting()
    {
        if (!m_TargetMarkerAnimationInitialized)
        {
            if (!m_TargetMarker.gameObject.activeSelf)
            {
                m_TargetMarker.gameObject.SetActive(true);
            }

            m_TargetMarkerAnchorFrom = m_TargetMarkerAnchorCurrent;
            m_TargetMarkerAnchorTarget = m_TargetMarkerAnchorCurrent + new Vector2(0f, m_TargetMarkerExitOffset);
            m_TargetMarkerAnimationInitialized = true;
            m_TargetMarkerTimer = 0f;
        }

        m_TargetMarkerTimer += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, m_TargetMarkerExitDuration);
        float t = Mathf.Clamp01(m_TargetMarkerTimer / duration);
        float eased = EaseInCubic(t);

        m_TargetMarkerAnchorCurrent = Vector2.Lerp(m_TargetMarkerAnchorFrom, m_TargetMarkerAnchorTarget, eased);
        SetMarkerAlpha(1f - t);

        if (t >= 1f - Mathf.Epsilon)
        {
            m_TargetMarkerState = TargetMarkerState.Hidden;
            m_TargetMarkerTimer = 0f;
            m_TargetMarkerAnimationInitialized = false;
            m_TargetMarker.gameObject.SetActive(false);
            SetMarkerAlpha(0f);
        }
    }

    private void ToggleFullscreen()
    {
        bool targetState = !Screen.fullScreen;
        try
        {
            Screen.fullScreen = targetState;
        }
        catch
        {
            // ignored – some platforms may not support toggling
        }

        RefreshFullscreenButton(true);
    }

    private void RefreshFullscreenButton(bool force = false)
    {
        if (m_FullscreenLabel == null)
        {
            return;
        }

        string desired = Screen.fullScreen ? "EXIT" : "FULL";
        if (force || m_FullscreenLabel.text != desired)
        {
            m_FullscreenLabel.text = desired;
        }
    }

    private void SetMarkerAlpha(float alpha)
    {
        if (m_TargetMarkerImage == null)
        {
            return;
        }

        Color color = m_TargetMarkerImage.color;
        color.a = Mathf.Clamp01(alpha);
        m_TargetMarkerImage.color = color;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static readonly List<RaycastResult> s_RaycastResults = new List<RaycastResult>();

    private static bool IsPointerOverUi(Vector2 screenPosition, int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition,
            pointerId = pointerId
        };

        s_RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, s_RaycastResults);

        for (int i = 0; i < s_RaycastResults.Count; i++)
        {
            GameObject hit = s_RaycastResults[i].gameObject;
            if (hit == null)
            {
                continue;
            }

            if (hit.GetComponent<MobileFireButton>() != null)
            {
                return true;
            }

            if (hit.GetComponent<Button>() != null)
            {
                return true;
            }

            if (hit.GetComponent<Toggle>() != null)
            {
                return true;
            }

            if (hit.GetComponent<Slider>() != null)
            {
                return true;
            }

            if (hit.GetComponent<Scrollbar>() != null)
            {
                return true;
            }

            if (hit.GetComponent<InputField>() != null || hit.GetComponent<TMP_InputField>() != null)
            {
                return true;
            }

            if (hit.GetComponent<Selectable>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetGroundPosition(Vector2 screenPosition, Camera targetCamera, out Vector3 worldPosition)
    {
        worldPosition = default;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, m_GroundMask, QueryTriggerInteraction.Ignore))
        {
            worldPosition = hit.point;
            LogDebug($"Move target set: {worldPosition.x:F1}, {worldPosition.z:F1}", 2f);
            return true;
        }

        LogDebug("Pointer raycast missed ground", 2f);
        return false;
    }

    private bool TryGetEnemyTank(Vector2 screenPosition, Camera targetCamera, out TankMovement enemyTank)
    {
        enemyTank = null;

        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null)
        {
            return false;
        }

        TankMovement movement = hit.collider.GetComponentInParent<TankMovement>();
        if (movement == null)
        {
            return false;
        }

        if (!movement.isActiveAndEnabled)
        {
            return false;
        }

        if (movement == m_PrimaryMovement)
        {
            return false;
        }

        if (movement.m_PlayerNumber == m_PrimaryMovement.m_PlayerNumber)
        {
            return false;
        }

        enemyTank = movement;
        return true;
    }

    private void LogDebug(string message, float duration)
    {
        if (!m_EnableDebugOverlay)
        {
            return;
        }

        if (m_DebugText == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (m_DebugHistorySize < 1)
        {
            m_DebugHistorySize = 1;
        }

        if (m_DebugEntries.Count >= m_DebugHistorySize)
        {
            int removeCount = m_DebugEntries.Count - m_DebugHistorySize + 1;
            m_DebugEntries.RemoveRange(0, removeCount);
        }

        m_DebugEntries.Add(message);
        m_DebugText.text = string.Join("\n", m_DebugEntries);

        if (m_DebugContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_DebugContent);
        }

        if (m_DebugScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            m_DebugScroll.verticalNormalizedPosition = 0f;
        }
    }

    private void BuildUi()
    {
        if (m_IsInitialized)
        {
            return;
        }

        EnsureEventSystem();

        bool shouldShowButton = Application.isMobilePlatform || Input.touchSupported;

        Sprite buttonSprite = LoadButtonSprite();
        Font labelFont = LoadFont();
        bool shouldShowFullscreen = Application.platform == RuntimePlatform.WebGLPlayer || Application.isEditor;

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

        if (shouldShowButton)
        {
            var fireButtonObject = new GameObject("FireButton", typeof(RectTransform), typeof(Image), typeof(MobileFireButton));
            fireButtonObject.transform.SetParent(canvasObject.transform, false);
            var fireRect = fireButtonObject.GetComponent<RectTransform>();
            fireRect.anchorMin = new Vector2(1f, 0f);
            fireRect.anchorMax = new Vector2(1f, 0f);
            fireRect.pivot = new Vector2(0.5f, 0.5f);
            fireRect.anchoredPosition = new Vector2(-200f, 200f);
            fireRect.sizeDelta = new Vector2(188f, 188f);

            var fireImage = fireButtonObject.GetComponent<Image>();
            fireImage.sprite = buttonSprite;
            fireImage.color = Color.white;
            fireImage.type = Image.Type.Simple;
            fireImage.preserveAspect = true;

            var buttonShadow = fireButtonObject.AddComponent<Shadow>();
            buttonShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            buttonShadow.effectDistance = new Vector2(0f, -6f);

            var buttonOutline = fireButtonObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0.2f, 0.05f, 0.03f, 0.4f);
            buttonOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(fireButtonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.text = "FIRE!";
            label.alignment = TextAnchor.MiddleCenter;
            label.font = labelFont;
            label.fontSize = 42;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.98f, 0.95f, 0.95f);
            label.raycastTarget = false;

            var labelShadow = labelObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0.28f, 0.05f, 0.02f, 0.75f);
            labelShadow.effectDistance = new Vector2(0f, -3f);

            m_FireButton = fireButtonObject.GetComponent<MobileFireButton>();
        }
        else
        {
            m_FireButton = null;
        }

        if (shouldShowFullscreen)
        {
            var fullscreenObject = new GameObject("FullscreenButton", typeof(RectTransform), typeof(Image), typeof(Button));
            fullscreenObject.transform.SetParent(canvasObject.transform, false);
            var fullscreenRect = fullscreenObject.GetComponent<RectTransform>();
            fullscreenRect.anchorMin = new Vector2(1f, 1f);
            fullscreenRect.anchorMax = new Vector2(1f, 1f);
            fullscreenRect.pivot = new Vector2(0.5f, 0.5f);
            fullscreenRect.anchoredPosition = new Vector2(-140f, -120f);
            fullscreenRect.sizeDelta = m_FullscreenButtonSize;

            var fullscreenImage = fullscreenObject.GetComponent<Image>();
            fullscreenImage.sprite = LoadMenuButtonSprite();
            fullscreenImage.color = Color.white;
            fullscreenImage.type = Image.Type.Simple;
            fullscreenImage.preserveAspect = true;

            var fullscreenShadow = fullscreenObject.AddComponent<Shadow>();
            fullscreenShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            fullscreenShadow.effectDistance = new Vector2(0f, -3f);

            var fullscreenOutline = fullscreenObject.AddComponent<Outline>();
            fullscreenOutline.effectColor = new Color(0.08f, 0.12f, 0.22f, 0.35f);
            fullscreenOutline.effectDistance = new Vector2(1.2f, -1.2f);

            var fullscreenButton = fullscreenObject.GetComponent<Button>();
            ColorBlock colors = fullscreenButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.colorMultiplier = 1f;
            fullscreenButton.colors = colors;

            var fullscreenLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            fullscreenLabelObject.transform.SetParent(fullscreenObject.transform, false);
            var fullscreenLabelRect = fullscreenLabelObject.GetComponent<RectTransform>();
            fullscreenLabelRect.anchorMin = new Vector2(0f, 0f);
            fullscreenLabelRect.anchorMax = new Vector2(1f, 1f);
            fullscreenLabelRect.offsetMin = Vector2.zero;
            fullscreenLabelRect.offsetMax = Vector2.zero;

            var fullscreenLabel = fullscreenLabelObject.GetComponent<Text>();
            fullscreenLabel.alignment = TextAnchor.MiddleCenter;
            fullscreenLabel.font = labelFont;
            fullscreenLabel.fontSize = 28;
            fullscreenLabel.fontStyle = FontStyle.Bold;
            fullscreenLabel.color = new Color(0.12f, 0.19f, 0.35f, 0.95f);
            fullscreenLabel.raycastTarget = false;

            var fullscreenLabelShadow = fullscreenLabelObject.AddComponent<Shadow>();
            fullscreenLabelShadow.effectColor = new Color(1f, 1f, 1f, 0.35f);
            fullscreenLabelShadow.effectDistance = new Vector2(0f, -1.5f);

            fullscreenButton.onClick.AddListener(ToggleFullscreen);
            m_FullscreenButton = fullscreenButton;
            m_FullscreenLabel = fullscreenLabel;
            RefreshFullscreenButton();
        }

        if (m_EnableDebugOverlay)
        {
            var debugPanelObject = new GameObject("DebugOverlay", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            debugPanelObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = debugPanelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.offsetMin = new Vector2(20f, -160f);
            panelRect.offsetMax = new Vector2(-20f, -20f);

            var panelImage = debugPanelObject.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.45f);
            panelImage.raycastTarget = false;

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(debugPanelObject.transform, false);
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);

            var viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = false;

            var contentObject = new GameObject("Content", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            m_DebugText = contentObject.GetComponent<Text>();
            m_DebugText.font = labelFont;
            m_DebugText.fontSize = 24;
            m_DebugText.alignment = TextAnchor.UpperLeft;
            m_DebugText.color = new Color(1f, 1f, 1f, 0.95f);
            m_DebugText.supportRichText = false;
            m_DebugText.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_DebugText.verticalOverflow = VerticalWrapMode.Overflow;
            m_DebugText.text = string.Empty;
            m_DebugText.raycastTarget = false;

            var scrollRect = debugPanelObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;

            m_DebugScroll = scrollRect;
            m_DebugContent = contentRect;
            m_DebugEntries.Clear();
        }
        else
        {
            m_DebugText = null;
            m_DebugScroll = null;
            m_DebugContent = null;
            m_DebugEntries.Clear();
        }

        if (m_TargetMarker == null)
        {
            var markerObject = new GameObject("TargetMarker", typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(canvasObject.transform, false);
            var markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0f);
            markerRect.sizeDelta = m_TargetMarkerSize;

            var markerImage = markerObject.GetComponent<Image>();
            markerImage.sprite = LoadTargetSprite();
            markerImage.color = Color.white;
            markerImage.raycastTarget = false;

            markerObject.SetActive(false);
            m_TargetMarker = markerRect;
            m_TargetMarkerImage = markerImage;
            m_TargetMarkerState = TargetMarkerState.Hidden;
            m_TargetMarkerTimer = 0f;
            m_TargetMarkerAnimationInitialized = false;
            m_TargetMarkerAnchorCurrent = Vector2.zero;
            m_TargetMarkerAnchorTarget = Vector2.zero;
            SetMarkerAlpha(0f);
        }

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

    private static Sprite LoadButtonSprite()
    {
        if (s_ButtonSprite != null)
        {
            return s_ButtonSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "GeneratedFireButton",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color border = new Color(0.18f, 0.03f, 0.02f, 1f);
        Color fill = new Color(0.95f, 0.35f, 0.1f, 1f);
        Color highlight = new Color(1f, 0.64f, 0.36f, 1f);
        Color shadow = new Color(0f, 0f, 0f, 0.32f);
        Color transparent = new Color(0f, 0f, 0f, 0f);

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 4f;
        float borderThickness = 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                int index = (y * size) + x;

                if (distance > radius)
                {
                    pixels[index] = transparent;
                    continue;
                }

                bool isBorder = distance >= radius - borderThickness;
                float verticalT = Mathf.Clamp01((center - dy + 10f) / (radius * 2f));
                Color baseColor = isBorder ? border : Color.Lerp(fill, highlight, Mathf.Pow(verticalT, 1.6f));
                pixels[index] = baseColor;

                if (!isBorder)
                {
                    int shadowX = x + 3;
                    int shadowY = y - 4;
                    if (shadowX >= 0 && shadowX < size && shadowY >= 0 && shadowY < size)
                    {
                        int shadowIndex = (shadowY * size) + shadowX;
                        if (pixels[shadowIndex].a < shadow.a)
                        {
                            pixels[shadowIndex] = shadow;
                        }
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        s_ButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        s_ButtonSprite.name = "GeneratedFireButtonSprite";
        return s_ButtonSprite;
    }

    private static Sprite LoadMenuButtonSprite()
    {
        if (s_MenuButtonSprite != null)
        {
            return s_MenuButtonSprite;
        }

        const int width = 256;
        const int height = 112;
        const float cornerRadius = 24f;
        const float borderThickness = 5f;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedMenuButton",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color border = new Color(0.08f, 0.12f, 0.22f, 1f);
        Color fill = new Color(0.6f, 0.78f, 1f, 1f);
        Color highlight = new Color(0.84f, 0.92f, 1f, 1f);
        Color shadow = new Color(0f, 0f, 0f, 0.25f);
        Color transparent = new Color(0f, 0f, 0f, 0f);

        float halfWidth = (width - 1) * 0.5f;
        float halfHeight = (height - 1) * 0.5f;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float localX = Mathf.Abs(x - halfWidth);
                float localY = Mathf.Abs(y - halfHeight);
                float innerWidth = halfWidth - cornerRadius;
                float innerHeight = halfHeight - cornerRadius;

                float dx = Mathf.Max(localX - innerWidth, 0f);
                float dy = Mathf.Max(localY - innerHeight, 0f);
                float distanceCorner = (dx * dx) + (dy * dy);
                bool insideRounded = distanceCorner <= cornerRadius * cornerRadius;

                if (!(localX <= innerWidth || localY <= innerHeight || insideRounded))
                {
                    pixels[(y * width) + x] = transparent;
                    continue;
                }

                bool inBorder;
                if (localX <= innerWidth || localY <= innerHeight)
                {
                    float minToEdge = Mathf.Min(halfWidth - localX, halfHeight - localY);
                    inBorder = minToEdge <= borderThickness;
                }
                else
                {
                    float cornerDist = Mathf.Sqrt(distanceCorner);
                    inBorder = cornerDist >= cornerRadius - borderThickness;
                }

                float verticalT = Mathf.Clamp01((halfHeight - (y - halfHeight) + 6f) / (height));
                Color baseColor = Color.Lerp(fill, highlight, Mathf.Pow(verticalT, 1.6f));
                if (inBorder)
                {
                    baseColor = border;
                }

                pixels[(y * width) + x] = baseColor;

                if (!inBorder)
                {
                    int shadowX = x + 2;
                    int shadowY = y - 3;
                    if (shadowX >= 0 && shadowX < width && shadowY >= 0 && shadowY < height)
                    {
                        int shadowIndex = (shadowY * width) + shadowX;
                        if (pixels[shadowIndex].a < shadow.a)
                        {
                            pixels[shadowIndex] = shadow;
                        }
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        s_MenuButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        s_MenuButtonSprite.name = "GeneratedMenuButtonSprite";
        return s_MenuButtonSprite;
    }

    private static Sprite LoadTargetSprite()
    {
        if (s_TargetSprite != null)
        {
            return s_TargetSprite;
        }

        const int width = 68;
        const int height = 96;
        const int shaftWidth = 14;
        const int headHeight = 32;
        const int headHalfWidth = 30;
        const int shadowOffsetX = 3;
        const int shadowOffsetY = -2;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedTargetMarker",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color outline = new Color(0.27f, 0.02f, 0.02f, 1f);
        Color body = new Color(0.92f, 0.18f, 0.18f, 1f);
        Color highlight = new Color(1f, 0.55f, 0.48f, 1f);
        Color dropShadow = new Color(0f, 0f, 0f, 0.35f);
        Color transparent = new Color(0f, 0f, 0f, 0f);

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        float centerX = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                int yFromBottom = y;

                bool fill = false;
                bool edge = false;

                if (yFromBottom < headHeight)
                {
                    float t = (yFromBottom + 0.5f) / Mathf.Max(1f, headHeight);
                    float halfWidth = Mathf.Lerp(3f, headHalfWidth, t);
                    fill = Mathf.Abs(dx) <= halfWidth;
                    edge = Mathf.Abs(dx) >= halfWidth - 1.3f && fill;
                }
                else
                {
                    float halfWidth = shaftWidth * 0.5f;
                    fill = Mathf.Abs(dx) <= halfWidth;
                    edge = Mathf.Abs(dx) >= halfWidth - 1f && fill;
                }

                if (!fill)
                {
                    continue;
                }

                int index = (y * width) + x;

                Color baseColor = edge ? outline : body;
                if (!edge)
                {
                    float accent = Mathf.Clamp01(1f - (Mathf.Abs(dx) / Mathf.Max(1f, headHalfWidth)));
                    baseColor = Color.Lerp(body, highlight, accent * 0.55f);
                }

                pixels[index] = baseColor;

                int shadowX = x + shadowOffsetX;
                int shadowY = y + shadowOffsetY;
                if (shadowX >= 0 && shadowX < width && shadowY >= 0 && shadowY < height)
                {
                    int shadowIndex = (shadowY * width) + shadowX;
                    if (pixels[shadowIndex].a < dropShadow.a)
                    {
                        pixels[shadowIndex] = dropShadow;
                    }
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        s_TargetSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f));
        s_TargetSprite.name = "GeneratedTargetMarkerSprite";
        return s_TargetSprite;
    }

    private static Font LoadFont()
    {
        if (s_DebugFont != null)
        {
            return s_DebugFont;
        }

        string[] builtinPaths = { "LegacyRuntime.ttf", "Arial.ttf" };
        foreach (string path in builtinPaths)
        {
            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>(path);
                if (builtin != null)
                {
                    s_DebugFont = builtin;
                    return s_DebugFont;
                }
            }
            catch
            {
                // ignored - try next option
            }
        }

        foreach (string candidate in s_FontCandidates)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            try
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(candidate, 32);
                if (osFont != null)
                {
                    s_DebugFont = osFont;
                    return s_DebugFont;
                }
            }
            catch
            {
                // ignored - try next name
            }
        }

        try
        {
            s_DebugFont = Font.CreateDynamicFontFromOSFont("Arial", 32);
        }
        catch
        {
            s_DebugFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (s_DebugFont == null)
        {
            s_DebugFont = new Font();
        }

        return s_DebugFont;
    }
}
