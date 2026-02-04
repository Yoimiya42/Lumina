using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ImageProgressRepository
{
    private const string FileName = "luminate_image_progress_db.json";

    private static string _filePath;  // runtime-resolved
    private static bool _configured;

    [Serializable]
    public class Entry
    {
        public string imageId;          // sha1(file bytes)
        public int lockedDifficulty;    // 0..2
        public float progress01;        // 0..1
        public int gridX;
        public int gridY;
        public float[] cells;           // length = gridX * gridY
        public long lastUpdatedUtcTicks;
    }

    [Serializable]
    private class Db
    {
        public int version = 1;
        public List<Entry> entries = new();
    }

    private static Db _db;
    private static Dictionary<string, Entry> _map;

    /// <summary>
    /// Call once on startup (Menu scene / first scene).
    /// Preferred path: UserContent/Lumina/Saves/FileName
    /// Fallback path: Application.persistentDataPath/FileName
    /// </summary>
    public static void Configure(PathSettings pathSettings)
    {
        if (_configured) return;

        string preferredDir = null;
        try
        {
            ContentPaths.EnsureFolders(pathSettings);
            preferredDir = ContentPaths.GetSavesFolder(pathSettings);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImageProgressRepository] Configure: failed to resolve preferred save dir.\n{e}");
        }

        if (!string.IsNullOrEmpty(preferredDir) && EnsureWritable(preferredDir))
        {
            _filePath = Path.Combine(preferredDir, FileName);
        }
        else
        {
            _filePath = Path.Combine(Application.persistentDataPath, FileName);
        }

        _configured = true;
    }

    public static string DebugGetFilePath()
    {
        EnsureConfigured();
        return _filePath;
    }

    private static void EnsureConfigured()
    {
        // 如果你忘了在启动时 Configure，我们也不让它崩：直接 fallback
        if (_configured) return;
        _filePath = Path.Combine(Application.persistentDataPath, FileName);
        _configured = true;
    }

    private static bool EnsureWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureLoaded()
    {
        EnsureConfigured();
        if (_db != null && _map != null) return;

        _db = new Db();
        _map = new Dictionary<string, Entry>();

        if (!File.Exists(_filePath)) return;

        try
        {
            string json = File.ReadAllText(_filePath);
            var loaded = JsonUtility.FromJson<Db>(json);
            if (loaded?.entries != null)
            {
                _db = loaded;
                foreach (var e in _db.entries)
                {
                    if (!string.IsNullOrEmpty(e.imageId))
                        _map[e.imageId] = Sanitize(e);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ImageProgressRepository] Failed to load db: {e}");
            _db = new Db();
            _map = new Dictionary<string, Entry>();
        }
    }

    private static Entry Sanitize(Entry e)
    {
        if (e == null) return null;
        e.progress01 = Mathf.Clamp01(e.progress01);
        e.lockedDifficulty = Mathf.Clamp(e.lockedDifficulty, 0, 2);
        e.gridX = Mathf.Max(1, e.gridX);
        e.gridY = Mathf.Max(1, e.gridY);
        e.cells ??= Array.Empty<float>();
        return e;
    }

    public static bool TryGet(string imageId, out Entry entry)
    {
        EnsureLoaded();

        if (string.IsNullOrEmpty(imageId))
        {
            entry = null;
            return false;
        }

        if (_map.TryGetValue(imageId, out entry) && entry != null)
        {
            entry = Sanitize(entry);
            return true;
        }

        entry = null;
        return false;
    }

    public static void Set(string imageId, Difficulty difficulty, int gridX, int gridY, float[] cells, float progress01)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(imageId)) return;

        progress01 = Mathf.Clamp01(progress01);
        gridX = Mathf.Max(1, gridX);
        gridY = Mathf.Max(1, gridY);

        if (!_map.TryGetValue(imageId, out var e) || e == null)
        {
            e = new Entry { imageId = imageId };
            _db.entries.Add(e);
            _map[imageId] = e;
        }

        if (progress01 > 0f)
            e.lockedDifficulty = (int)difficulty;

        e.progress01 = progress01;
        e.gridX = gridX;
        e.gridY = gridY;

        // store a copy (avoid external mutation)
        e.cells = cells != null ? (float[])cells.Clone() : Array.Empty<float>();

        e.lastUpdatedUtcTicks = DateTime.UtcNow.Ticks;

        Save();
    }

    public static void Reset(string imageId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(imageId)) return;

        if (!_map.TryGetValue(imageId, out var e) || e == null)
            return;

        _map.Remove(imageId);
        _db.entries.Remove(e);
        Save();
    }

    private static void Save()
    {
        EnsureConfigured();
        try
        {
            string json = JsonUtility.ToJson(_db, true);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ImageProgressRepository] Failed to save db: {e}");
        }
    }
}
