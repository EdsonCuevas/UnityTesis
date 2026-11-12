using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Escribe las estadísticas de cada alumno en CSV dentro de persistentDataPath/estadisticas:
///   pasos.csv    — una fila por paso de cada intento (se escribe al terminar cada paso).
///   intentos.csv — una fila por intento (al completarlo, abandonarlo o saltarlo).
/// Solo agrega renglones, así un cierre inesperado no borra lo anterior.
/// En el Quest: Android/data/com.KamehouseO.VRElectricalSimulation/files/estadisticas
/// </summary>
public static class StatsLogger
{
    public const string FolderName = "estadisticas";
    const string Suffix220 = "_220";

    static readonly string[] StepColumns =
    {
        "intento_id", "alumno", "sesion", "nivel", "voltaje", "modo",
        "paso_n", "paso", "segundos", "avisos", "errores", "terminado"
    };

    static readonly string[] AttemptColumns =
    {
        "intento_id", "alumno", "sesion", "nivel", "voltaje", "modo", "inicio", "fin",
        "segundos", "pasos_completados", "pasos_totales", "avisos", "errores", "resultado"
    };

    public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);
    static string StepsPath => Path.Combine(Folder, "pasos.csv");
    static string AttemptsPath => Path.Combine(Folder, "intentos.csv");

    /// <summary>Abre un intento. levelId puede traer el sufijo _220; tutorial = sin voltaje.</summary>
    public static StatsAttempt BeginAttempt(string levelId, string mode, int stepCount, bool hasVoltage)
    {
        string level = levelId ?? "desconocido";
        string voltage = string.Empty;
        if (hasVoltage)
        {
            bool is220 = level.EndsWith(Suffix220);
            if (is220) level = level.Substring(0, level.Length - Suffix220.Length);
            voltage = is220 ? "220" : "127";
        }

        return new StatsAttempt
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 12),
            Student = StudentSession.IdOrDefault,
            Session = StudentSession.SessionId,
            Level = level,
            Voltage = voltage,
            Mode = mode,
            StepCount = stepCount,
            StartedAt = DateTime.Now
        };
    }

    internal static void WriteStep(StatsAttempt a, int index, string title, float seconds, int hints, int errors, bool done)
    {
        Append(StepsPath, StepColumns,
            a.Id, a.Student, a.Session, a.Level, a.Voltage, a.Mode,
            (index + 1).ToString(CultureInfo.InvariantCulture), title, Seconds(seconds),
            Int(hints), Int(errors), done ? "1" : "0");
    }

    internal static void WriteAttempt(StatsAttempt a, float seconds, string result)
    {
        Append(AttemptsPath, AttemptColumns,
            a.Id, a.Student, a.Session, a.Level, a.Voltage, a.Mode,
            Timestamp(a.StartedAt), Timestamp(DateTime.Now), Seconds(seconds),
            Int(a.CompletedSteps), Int(a.StepCount), Int(a.Hints), Int(a.Errors), result);
    }

    static void Append(string path, string[] header, params string[] fields)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            // El archivo nuevo empieza con BOM y encabezado: así Excel muestra bien los acentos
            // (pandas lo lee con encoding="utf-8-sig"). Los renglones siguientes se agregan sin BOM.
            if (!File.Exists(path)) File.WriteAllText(path, Row(header), new UTF8Encoding(true));
            File.AppendAllText(path, Row(fields), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogError($"No se pudieron guardar las estadísticas en {path}: {e.Message}");
        }
    }

    static string Row(string[] fields)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escape(fields[i]));
        }
        return sb.Append('\n').ToString();
    }

    static string Escape(string value)
    {
        value ??= string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string Seconds(float s) => s.ToString("0.00", CultureInfo.InvariantCulture);
    static string Int(int n) => n.ToString(CultureInfo.InvariantCulture);
    static string Timestamp(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>Un intento abierto de un nivel o del tutorial.</summary>
public class StatsAttempt
{
    public string Id;
    public string Student;
    public string Session;
    public string Level;
    public string Voltage;
    public string Mode;
    public int StepCount;
    public DateTime StartedAt;

    public int CompletedSteps { get; private set; }
    public int Hints { get; private set; }
    public int Errors { get; private set; }
    public bool IsClosed { get; private set; }

    public void LogStep(int index, string title, float seconds, int hints, int errors, bool done)
    {
        if (IsClosed) return;
        if (done) CompletedSteps++;
        Hints += hints;
        Errors += errors;
        StatsLogger.WriteStep(this, index, title, seconds, hints, errors, done);
    }

    /// <summary>resultado: completado, abandonado o saltado.</summary>
    public void Close(float seconds, string result)
    {
        if (IsClosed) return;
        IsClosed = true;
        StatsLogger.WriteAttempt(this, seconds, result);
    }
}
