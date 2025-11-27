using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class EnvLoader
{
    private static Dictionary<string, string> envVars = new Dictionary<string, string>();
    private static bool isLoaded = false;

    public static void LoadEnv()
    {
        if (isLoaded) return;

        string envPath = Path.Combine(Application.dataPath, "Scripts/env.env");
        if (!File.Exists(envPath))
        {
            Debug.LogWarning(".env file not found at: " + envPath);
            return;
        }

        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var split = line.Split('=', 2);
            if (split.Length == 2)
            {
                string key = split[0].Trim();
                string value = split[1].Trim();
                envVars[key] = value;
            }
        }

        isLoaded = true;
    }

    public static string GetEnv(string key)
    {
        if (!isLoaded)
        {
            LoadEnv();
        }

        return envVars.TryGetValue(key, out string value) ? value : null;
    }
}
