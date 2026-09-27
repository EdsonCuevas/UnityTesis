using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class LevelStepResult
{
    public string paso;
    public float segundos;
    public int avisos;
}

[Serializable]
public class LevelAttempt
{
    public string modo;
    public string fecha;
    public float segundosTotales;
    public int avisos;
    public LevelStepResult[] pasos;
}

[Serializable]
public class LevelRecord
{
    public string nivel;
    public List<LevelAttempt> intentos = new List<LevelAttempt>();

    /// <summary>Menor tiempo registrado en ese modo, o 0 si no hay intentos.</summary>
    public float BestSeconds(string modo)
    {
        float best = 0f;
        foreach (var intento in intentos)
            if (intento.modo == modo && (best == 0f || intento.segundosTotales < best))
                best = intento.segundosTotales;
        return best;
    }
}

/// <summary>Guarda cada intento de un nivel en un JSON dentro de persistentDataPath.</summary>
public static class LevelResultsStore
{
    static string PathFor(string levelId) =>
        Path.Combine(Application.persistentDataPath, levelId + "_resultados.json");

    public static LevelRecord Load(string levelId)
    {
        string path = PathFor(levelId);
        if (File.Exists(path))
        {
            var record = JsonUtility.FromJson<LevelRecord>(File.ReadAllText(path));
            if (record != null) return record;
        }
        return new LevelRecord { nivel = levelId };
    }

    public static LevelRecord SaveAttempt(string levelId, LevelAttempt attempt)
    {
        var record = Load(levelId);
        record.intentos.Add(attempt);
        File.WriteAllText(PathFor(levelId), JsonUtility.ToJson(record, true));
        return record;
    }
}
