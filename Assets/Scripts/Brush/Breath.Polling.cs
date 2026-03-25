using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class Breath
{
    private IEnumerator PollLoop()
    {
        while (enabled)
        {
            float reg = _lastRegularity;
            float rateBps = _lastRateBps;
            float? vol = null;
            yield return GetFloat(UrlBreathingVolume, "breathing_volume", v => vol = v);

            if (vol.HasValue)
            {
                float v = Mathf.Max(0f, vol.Value);
                _targetMultiplier = ComputeMultiplierAndGate(v, reg, rateBps);
            }

            if (useRegularity)
            {
                float? r = null;
                yield return GetFloat(UrlBreathingRegularity, "breathing_regularity", v => r = v);
                if (r.HasValue)
                    _lastRegularity = Mathf.Clamp01(r.Value);
            }

            if (useRateBonus)
            {
                float? rr = null;
                yield return GetFloat(UrlBreathingRate, "breathing_rate", v => rr = v);
                if (rr.HasValue)
                    _lastRateBps = Mathf.Max(0f, rr.Value);
            }

            yield return _pollWait;
        }

        _pollRoutine = null;
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
}
