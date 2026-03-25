using System;
using UnityEngine;

public partial class Breath
{
    private float ComputeMultiplierAndGate(float breathingVolume, float regularity01, float breathingRateBps)
    {
        _vMin = Mathf.Lerp(_vMin, Mathf.Min(_vMin, breathingVolume), calibLerp);
        _vMax = Mathf.Lerp(_vMax, Mathf.Max(_vMax, breathingVolume), calibLerp);
        if (_vMax <= _vMin + 1e-4f) _vMax = _vMin + 1e-4f;

        float normalizedV01 = Mathf.Clamp01((breathingVolume - _vMin) / (_vMax - _vMin));
        float boostedV01 = Mathf.Clamp01(Mathf.Max(0f, breathingVolume) * Mathf.Max(0f, rawVolumeSensitivity));
        _responsiveSignal01 = Mathf.Lerp(_responsiveSignal01, boostedV01, Mathf.Clamp01(rawSignalBlend));
        float controlV01 = Mathf.Max(normalizedV01, _responsiveSignal01);
        _lastV01 = controlV01;

        if (gatePainting)
        {
            if (controlV01 >= breathOnThreshold01)
            {
                _paintGateState = true;
                _lastBreathSeenTime = Time.unscaledTime;
            }
            else
            {
                bool holdActive = Time.unscaledTime - _lastBreathSeenTime < Mathf.Max(0f, breathHoldSec);
                if (_paintGateState && controlV01 <= breathOffThreshold01 && !holdActive)
                    _paintGateState = false;
            }

            painter.SetBreathPaintActive(_paintGateState);
        }
        else
        {
            painter.SetBreathPaintActive(true);
        }

        float mappedV01 = _paintGateState
            ? Mathf.Max(controlV01, Mathf.Clamp01(activeSignalFloor01))
            : controlV01;

        float m = minMultiplier + (maxMultiplier - minMultiplier) * Mathf.Pow(mappedV01, gamma);

        if (useRegularity)
        {
            float bonus = (1f - regularityWeight) + regularityWeight * Mathf.Clamp01(regularity01);
            m *= bonus;
        }

        if (useRateBonus)
        {
            float bpm = breathingRateBps * 60f;
            bool inRange = bpm >= targetBpmMin && bpm <= targetBpmMax;
            if (inRange)
                m *= (1f + Mathf.Max(0f, bpmBonus));
        }

        return Mathf.Clamp(m, minMultiplier, maxMultiplier * 3f);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return path ?? "";
        if (string.IsNullOrWhiteSpace(path)) return baseUrl;

        baseUrl = baseUrl.TrimEnd('/');
        path = path.TrimStart('/');
        return $"{baseUrl}/{path}";
    }

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
