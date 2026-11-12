using System;

/// <summary>
/// Alumno que está usando el visor, identificado por su número de cuenta de la universidad.
/// Vive solo en memoria: se conserva al cambiar de escena, pero al reiniciar la app hay que volver
/// a escribirlo, para no mezclar alumnos.
/// </summary>
public static class StudentSession
{
    public const string NoStudent = "sin_id";
    public const int AccountDigits = 8;

    /// <summary>Número de cuenta completo, como texto para conservar los ceros a la izquierda.</summary>
    public static string StudentId { get; private set; } = string.Empty;

    /// <summary>Se genera cada vez que un alumno escribe su número; agrupa lo que jugó de corrido.</summary>
    public static string SessionId { get; private set; } = string.Empty;

    public static bool HasStudent => !string.IsNullOrEmpty(StudentId);

    public static string IdOrDefault => HasStudent ? StudentId : NoStudent;

    /// <summary>Para mostrar en pantalla: solo los últimos 4 dígitos.</summary>
    public static string Label => HasStudent ? $"Alumno ••••{Last4(StudentId)}" : "Sin alumno";

    public static event Action Changed;

    public static bool IsValid(string digits)
    {
        if (digits == null || digits.Length != AccountDigits) return false;
        foreach (char c in digits)
            if (c < '0' || c > '9') return false;
        return true;
    }

    public static void Begin(string digits)
    {
        if (!IsValid(digits)) return;
        StudentId = digits;
        SessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Changed?.Invoke();
    }

    /// <summary>"31245678" → "3124 5678"; los dígitos que faltan se muestran como guion bajo.</summary>
    public static string Format(string digits)
    {
        string padded = (digits ?? string.Empty).PadRight(AccountDigits, '_');
        return padded.Substring(0, 4) + " " + padded.Substring(4);
    }

    static string Last4(string id) => id.Length <= 4 ? id : id.Substring(id.Length - 4);
}
