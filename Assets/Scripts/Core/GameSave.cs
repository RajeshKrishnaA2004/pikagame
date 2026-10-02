using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON save file in <see cref="Application.persistentDataPath"/>.
///
/// Deliberately NOT PlayerPrefs: PlayerPrefs is a flat key/value store with a
/// platform size limit, is shared across unrelated saves, and gives no room to
/// evolve the payload. A single JSON file is inspectable, versionable and easy
/// to delete from a bug report.
/// </summary>
public static class GameSave
{
    /// <summary>File name inside persistentDataPath.</summary>
    public const string FileName = "borrowed_time_save.json";

    /// <summary>Bumped if the payload shape changes in an incompatible way.</summary>
    public const int Version = 1;

    /// <summary>Full path to the save file. Does not touch the disk.</summary>
    public static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, FileName); }
    }

    /// <summary>True when a save file exists on disk.</summary>
    public static bool Exists()
    {
        try
        {
            return File.Exists(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[GameSave] Exists() failed: " + e.Message);
            return false;
        }
    }

    /// <summary>Writes the save, replacing any existing one.</summary>
    public static bool Save(GameSaveData data)
    {
        if (data == null)
            return false;

        try
        {
            string dir = Application.persistentDataPath;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
            Debug.Log("[GameSave] saved to " + FilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[GameSave] Save() failed: " + e.Message);
            return false;
        }
    }

    /// <summary>Reads the save, or null when absent / unreadable / inconsistent.</summary>
    public static GameSaveData Load()
    {
        if (!Exists())
            return null;

        try
        {
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrEmpty(json))
                return null;

            var data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null)
            {
                Debug.LogError("[GameSave] save file did not deserialise");
                return null;
            }
            if (data.party == null)
                data.party = new List<string>();

            if (!data.IsSane())
            {
                Debug.LogError("[GameSave] save file is inconsistent and was rejected");
                return null;
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError("[GameSave] Load() failed: " + e.Message);
            return null;
        }
    }

    /// <summary>Deletes the save. Returns true when a file was actually removed.</summary>
    public static bool Delete()
    {
        try
        {
            if (!File.Exists(FilePath))
                return false;

            File.Delete(FilePath);
            Debug.Log("[GameSave] deleted " + FilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[GameSave] Delete() failed: " + e.Message);
            return false;
        }
    }
}