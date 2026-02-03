using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class ProgressStore
{
    [Serializable]
    public class Entry
    {
        public string imageKey;      // sha1(imagePath)
        public int lockedDifficulty; // 0..2
        public float progress01;     // 0..1
        public long lastUpdatedUtcTicks;
    }

    [Serializable]
    private class Db
    {
        public List<Entry> entries = new();
    }

    private static readonly string FilePath =
        Path.Combine(Application.persistentDataPath, "luminate_progress_db.json");

    private static Db _db;
    private static Dictionary<string, Entry> _map;

    private static void EnsureLoaded()
    {
        if (_db != null && _map != null) return;

        _db = new Db();
        _map = new Dictionary<string, Entry>();

        if (!File.Exists(FilePath)) return;

        try
        {
            string json = File.ReadAllText(FilePath);
            var loaded = JsonUtility.FromJson<Db>(json);
            if (loaded?.entries != null)
            {
                _db = loaded;
                foreach (var e in _db.entries)
                    if (!string.IsNullOrEmpty(e.imageKey))
                        _map[e.imageKey] = e;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProgressStore] Failed to load db: {e}");
            _db = new Db();
            _map = new Dictionary<string, Entry>();
        }
    }

    public static string MakeImageKey(string imagePath) => Sha1Hex(imagePath ?? "");

    public static bool TryGet(string imagePath, out Entry entry)
    {
        EnsureLoaded();
        string key = MakeImageKey(imagePath);

        if (_map.TryGetValue(key, out entry))
        {
            entry.progress01 = Mathf.Clamp01(entry.progress01);
            entry.lockedDifficulty = Mathf.Clamp(entry.lockedDifficulty, 0, 2);
            return true;
        }

        entry = null;
        return false;
    }

    public static void Set(string imagePath, Difficulty difficulty, float progress01)
    {
        EnsureLoaded();
        string key = MakeImageKey(imagePath);

        progress01 = Mathf.Clamp01(progress01);

        if (progress01 <= 0f && !_map.ContainsKey(key))
            return;

        if (!_map.TryGetValue(key, out var e))
        {
            e = new Entry { imageKey = key };
            _db.entries.Add(e);
            _map[key] = e;
        }

        if (progress01 > 0f)
            e.lockedDifficulty = (int)difficulty;

        e.progress01 = progress01;
        e.lastUpdatedUtcTicks = DateTime.UtcNow.Ticks;

        Save();
    }

    public static void Reset(string imagePath)
    {
        EnsureLoaded();
        string key = MakeImageKey(imagePath);

        if (!_map.TryGetValue(key, out var e))
            return;

        _map.Remove(key);
        _db.entries.Remove(e);
        Save();
    }

    private static void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(_db, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProgressStore] Failed to save db: {e}");
        }
    }

    private static string Sha1Hex(string input)
    {
        using var sha1 = SHA1.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = sha1.ComputeHash(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }
}
