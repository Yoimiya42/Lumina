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
            float? vol = null;
            yield return GetFloat(UrlBreathingVolume, "breathing_volume", v => vol = v);

            float reg = _lastRegularity;
            if (useRegularity)
            {
                float? r = null;
                yield return GetFloat(UrlBreathingRegularity, "breathing_regularity", v => r = v);
                if (r.HasValue) reg = Mathf.Clamp01(r.Value);
            }

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
