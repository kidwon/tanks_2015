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

    private static MobileInputController s_Instance;
    private static Sprite s_ButtonSprite;
    private static Font s_DebugFont;
    private static readonly string[] s_FontCandidates =
    {
        "LegacyRuntime",
        "Arial",
        "Helvetica",
        "Liberation Sans",
        "Noto Sans"
    };

    private readonly List<TankMovement> m_TankMovements = new List<TankMovement>();
    private readonly List<TankShooting> m_TankShootings = new List<TankShooting>();

    private TankMovement m_PrimaryMovement;
    private TankShooting m_PrimaryShooting;
    private MobileFireButton m_FireButton;
    private Camera m_MainCamera;
    private bool m_IsInitialized;
    private Text m_DebugText;
    private ScrollRect m_DebugScroll;
    private RectTransform m_DebugContent;
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
            if (m_TankMovements[i] == null)
            {
                m_TankMovements.RemoveAt(i);
                removed = true;
                LogDebug("Removed null movement reference", 1.5f);
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
            m_PrimaryMovement.SetLookTarget(enemyTank.transform.position);
            LogDebug($"Aiming at enemy tank: Player {enemyTank.m_PlayerNumber}", 1.5f);
            return;
        }

        if (!TryGetGroundPosition(pointerPosition, targetCamera, out Vector3 worldPosition))
        {
            return;
        }

        m_PrimaryMovement.SetMoveTarget(worldPosition, m_StopDistance);
        LogDebug("Moving towards tapped position", 1.5f);
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
        bool shouldCreateCanvas = shouldShowButton || m_EnableDebugOverlay;
        if (!shouldCreateCanvas)
        {
            m_IsInitialized = true;
            return;
        }

        Sprite buttonSprite = LoadButtonSprite();
        Font labelFont = LoadFont();

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
        }
        else
        {
            m_FireButton = null;
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

        try
        {
            Sprite builtin = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            if (builtin != null)
            {
                s_ButtonSprite = builtin;
                return s_ButtonSprite;
            }
        }
        catch
        {
            // ignored - will fall back to generated sprite
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "GeneratedMobileButton",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color top = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        Color bottom = new Color(0.7f, 0.7f, 0.7f, 0.95f);
        texture.SetPixels(new[] { top, top, bottom, bottom });
        texture.Apply(false, true);

        s_ButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        s_ButtonSprite.name = "GeneratedMobileButtonSprite";
        return s_ButtonSprite;
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
