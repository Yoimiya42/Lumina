using UnityEngine;

/// <summary>
/// Drives Painter secondsPerCell using breathing metrics from FastAPI.
/// - Primary control: breathing_volume (continuous)
/// - Optional quality bonus: breathing_regularity (0..1)
/// - Optional tempo bonus: breathing_rate -> BPM within target range
/// Also gates painting on/off via breathing_volume (v01) with hysteresis.
/// </summary>
public partial class Breath : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Painter painter;

    [Header("API Base")]
    [Tooltip("Example: http://127.0.0.1:8000")]
    [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";

    [Header("Polling")]
    [SerializeField] private float pollIntervalSec = 0.10f;
    [SerializeField] private int requestTimeoutSec = 2;

    [Header("Responsive Detection")]
    [Tooltip("Boost raw breathing_volume before gating so quieter breaths still register.")]
    [SerializeField] private float rawVolumeSensitivity = 20f;
    [Tooltip("How quickly the responsive signal follows new samples. Higher = snappier.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float rawSignalBlend = 0.18f;
    [Tooltip("Keeps breathing active briefly when polling leaves tiny gaps between updates.")]
    [SerializeField] private float breathHoldSec = 0.5f;
    [Tooltip("Minimum control signal while breathing is active, similar to the web prototype's floor.")]
    [Range(0f, 1f)]
    [SerializeField] private float activeSignalFloor01 = 0.25f;

    [Header("Base Speed")]
    [Tooltip("Your preferred baseline seconds-per-cell (multiplier=1).")]
    [SerializeField] private float baseSecondsPerCell = 1.5f;

    [Header("Volume -> Multiplier Mapping")]
    [SerializeField] private float minMultiplier = 0.2f;
    [SerializeField] private float maxMultiplier = 2.0f;
    [Tooltip("Gamma < 1 boosts sensitivity for weak signals.")]
    [SerializeField] private float gamma = 0.75f;

    [Header("Regularity Bonus (optional)")]
    [SerializeField] private bool useRegularity = true;
    [Range(0f, 1f)]
    [SerializeField] private float regularityWeight = 0.3f; // m *= (1-w)+w*r

    [Header("Rate (BPM) Bonus (optional)")]
    [SerializeField] private bool useRateBonus = true;
    [Tooltip("API breathing_rate is breaths/second in your backend; BPM = rate * 60.")]
    [SerializeField] private float targetBpmMin = 6f;
    [SerializeField] private float targetBpmMax = 10f;
    [Tooltip("Extra multiplier when BPM is within target range. Example 0.2 => +20%.")]
    [SerializeField] private float bpmBonus = 0.2f;

    [Header("Breath Gate (Painting On/Off)")]
    [Tooltip("If true: breathing_volume (normalized v01) will gate painting via Painter.SetBreathPaintActive().")]
    [SerializeField] private bool gatePainting = true;
    [Range(0f, 1f)]
    [SerializeField] private float breathOnThreshold01 = 0.20f;
    [Range(0f, 1f)]
    [SerializeField] private float breathOffThreshold01 = 0.12f; // must be < On (hysteresis)

    [Header("Smoothing")]
    [Tooltip("Seconds; larger = smoother but more lag.")]
    [SerializeField] private float smoothTauSec = 0.25f;

    [Header("Auto Calibration (simple)")]
    [Tooltip("How fast vMin/vMax adapt. Smaller = more stable.")]
    [SerializeField] private float calibLerp = 0.02f;
    [SerializeField] private float initialVMin = 0.00f;
    [SerializeField] private float initialVMax = 0.05f;

    [Header("API Paths")]
    [SerializeField] private string pathBreathingVolume = "/webhooks/breathing-volume";
    [SerializeField] private string pathBreathingRegularity = "/webhooks/breathing-regularity";
    [SerializeField] private string pathBreathingRate = "/webhooks/breathing-rate";

    private float _vMin;
    private float _vMax;

    private float _targetMultiplier = 1f;
    private float _smoothedMultiplier = 1f;

    private float _lastRegularity = 1f;
    private float _lastRateBps = 0f;
    private float _responsiveSignal01 = 0f;
    private float _lastBreathSeenTime = float.NegativeInfinity;

    private bool _paintGateState = false;
    private float _lastV01 = 0f; // debug
    private Coroutine _pollRoutine;
    private WaitForSeconds _pollWait;

    private string UrlBreathingVolume => CombineUrl(apiBaseUrl, pathBreathingVolume);
    private string UrlBreathingRegularity => CombineUrl(apiBaseUrl, pathBreathingRegularity);
    private string UrlBreathingRate => CombineUrl(apiBaseUrl, pathBreathingRate);

    private void Awake()
    {
        if (painter == null)
        {
            Debug.LogError("[Breath] painter not assigned.");
            enabled = false;
            return;
        }

        if (breathOffThreshold01 >= breathOnThreshold01)
            breathOffThreshold01 = Mathf.Clamp01(breathOnThreshold01 * 0.5f);

        _vMin = initialVMin;
        _vMax = Mathf.Max(initialVMax, initialVMin + 1e-4f);

        if (gatePainting)
            painter.SetBreathPaintActive(false);
    }

    private void OnEnable()
    {
        if (_pollRoutine != null)
            return;

        _pollWait = new WaitForSeconds(Mathf.Max(0.02f, pollIntervalSec));
        _pollRoutine = StartCoroutine(PollLoop());
    }

    private void OnDisable()
    {
        if (_pollRoutine == null)
            return;

        StopCoroutine(_pollRoutine);
        _pollRoutine = null;
    }

    private void Update()
    {
        float alpha = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothTauSec));
        _smoothedMultiplier = Mathf.Lerp(_smoothedMultiplier, _targetMultiplier, alpha);

        float effSeconds = baseSecondsPerCell / Mathf.Max(0.01f, _smoothedMultiplier);
        painter.SetSecondsPerCell(effSeconds);
    }
}
