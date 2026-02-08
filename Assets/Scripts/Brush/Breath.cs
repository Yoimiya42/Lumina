using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Drives Painter secondsPerCell using breathing metrics from FastAPI.
/// - Primary control: breathing_volume (continuous)
/// - Optional quality bonus: breathing_regularity (0..1)
/// - Optional tempo bonus: breathing_rate -> BPM within target range
/// Also gates painting on/off via breathing_volume (v01) with hysteresis.
/// </summary>
public class Breath : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Painter painter;

    [Header("API Base")]
    [Tooltip("Example: http://127.0.0.1:8000")]
    [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";

    [Header("Polling")]
    [SerializeField] private float pollIntervalSec = 0.10f;
    [SerializeField] private int requestTimeoutSec = 2;

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

    // ----------- API Paths (centralized) -----------
    [Header("API Paths")]
    [SerializeField] private string pathBreathingVolume = "/webhooks/breathing-volume";
    [SerializeField] private string pathBreathingRegularity = "/webhooks/breathing-regularity";
    [SerializeField] private string pathBreathingRate = "/webhooks/breathing-rate";

    // ----------- Internal state -----------
    private float _vMin;
    private float _vMax;

    private float _targetMultiplier = 1f;
    private float _smoothedMultiplier = 1f;

    private float _lastRegularity = 1f;
    private float _lastRateBps = 0f;

    private bool _paintGateState = false;
    private float _lastV01 = 0f; // debug

    // ----------- Computed endpoints -----------
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

        // sanity: hysteresis order
        if (breathOffThreshold01 >= breathOnThreshold01)
            breathOffThreshold01 = Mathf.Max(0f, breathOnThreshold01 - 0.05f);

        _vMin = initialVMin;
        _vMax = Mathf.Max(initialVMax, initialVMin + 1e-4f);

        // Start disabled until first data arrives (optional; comment out if undesired)
        if (gatePainting)
            painter.SetBreathPaintActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(PollLoop());
    }

    private IEnumerator PollLoop()
    {
        var wait = new WaitForSeconds(pollIntervalSec);

        while (enabled)
        {
            // 1) breathing_volume
            float? vol = null;
            yield return GetFloat(UrlBreathingVolume, "breathing_volume", v => vol = v);

            // 2) breathing_regularity (optional)
            float reg = _lastRegularity;
            if (useRegularity)
            {
                float? r = null;
                yield return GetFloat(UrlBreathingRegularity, "breathing_regularity", v => r = v);
                if (r.HasValue) reg = Mathf.Clamp01(r.Value);
            }

            // 3) breathing_rate (optional) - backend returns breaths/second
            float rateBps = _lastRateBps;
            if (useRateBonus)
            {
                float? rr = null;
                yield return GetFloat(UrlBreathingRate, "breathing_rate", v => rr = v);
                if (rr.HasValue) rateBps = Mathf.Max(0f, rr.Value);
            }

            if (vol.HasValue)
            {
                float v = Mathf.Max(0f, vol.Value);
                _lastRegularity = reg;
                _lastRateBps = rateBps;

                _targetMultiplier = ComputeMultiplierAndGate(v, reg, rateBps);
            }

            yield return wait;
        }
    }

    private void Update()
    {
        // Smooth multiplier (frame-based)
        float alpha = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothTauSec));
        _smoothedMultiplier = Mathf.Lerp(_smoothedMultiplier, _targetMultiplier, alpha);

        // Apply to painter: secondsPerCell_eff = base / multiplier
        float effSeconds = baseSecondsPerCell / Mathf.Max(0.01f, _smoothedMultiplier);
        painter.SetSecondsPerCell(effSeconds);
    }

    private float ComputeMultiplierAndGate(float breathingVolume, float regularity01, float breathingRateBps)
    {
        // --- Auto calibration for volume normalization ---
        _vMin = Mathf.Lerp(_vMin, Mathf.Min(_vMin, breathingVolume), calibLerp);
        _vMax = Mathf.Lerp(_vMax, Mathf.Max(_vMax, breathingVolume), calibLerp);
        if (_vMax <= _vMin + 1e-4f) _vMax = _vMin + 1e-4f;

        // Normalize breathing_volume to 0..1
        float v01 = Mathf.Clamp01((breathingVolume - _vMin) / (_vMax - _vMin));
        _lastV01 = v01;

        // --- Gate painting by breath (with hysteresis) ---
        if (gatePainting)
        {
            if (!_paintGateState && v01 >= breathOnThreshold01) _paintGateState = true;
            else if (_paintGateState && v01 <= breathOffThreshold01) _paintGateState = false;

            painter.SetBreathPaintActive(_paintGateState);
        }
        else
        {
            // If gate disabled, keep painting enabled.
            painter.SetBreathPaintActive(true);
        }

        // Map to multiplier (soft curve)
        float m = minMultiplier + (maxMultiplier - minMultiplier) * Mathf.Pow(v01, gamma);

        // Optional: regularity quality bonus
        if (useRegularity)
        {
            float bonus = (1f - regularityWeight) + regularityWeight * Mathf.Clamp01(regularity01);
            m *= bonus;
        }

        // Optional: rate bonus based on BPM window
        if (useRateBonus)
        {
            float bpm = breathingRateBps * 60f; // backend is breaths/sec
            bool inRange = bpm >= targetBpmMin && bpm <= targetBpmMax;

            if (inRange)
                m *= (1f + Mathf.Max(0f, bpmBonus));
        }

        return Mathf.Clamp(m, minMultiplier, maxMultiplier * 3f);
    }

    private IEnumerator GetFloat(string url, string key, Action<float> onValue)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = requestTimeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                yield break;

            string json = req.downloadHandler.text;
            if (TryParseSingleFloat(json, key, out float value))
                onValue?.Invoke(value);
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return path ?? "";
        if (string.IsNullOrWhiteSpace(path)) return baseUrl;

        baseUrl = baseUrl.TrimEnd('/');
        path = path.TrimStart('/');
        return $"{baseUrl}/{path}";
    }

    // Minimal JSON parser for {"key": 0.123}
    private static bool TryParseSingleFloat(string json, string key, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;

        int k = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        if (k < 0) return false;

        int colon = json.IndexOf(':', k);
        if (colon < 0) return false;

        int start = colon + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;

        int end = start;
        while (end < json.Length && ("-+.0123456789eE".IndexOf(json[end]) >= 0)) end++;

        if (end <= start) return false;

        string num = json.Substring(start, end - start);
        return float.TryParse(num, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
