using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tornillo de una terminal del medidor. Se aprieta girándolo con el destornillador: gira y baja
/// un poco hasta llegar al tope.
/// </summary>
public class TerminalScrew : MonoBehaviour
{
    public static readonly List<TerminalScrew> All = new List<TerminalScrew>();

    [Tooltip("Malla del tornillo. Su pivote está en el centro de la cabeza y su eje Z apunta hacia afuera del medidor.")]
    public Transform head;
    public float requiredTurns = 1f;
    [Tooltip("Cuánto baja el tornillo al apretarlo por completo.")]
    public float sinkDepth = 0.003f;

    public float Turns { get; private set; }
    public bool IsTight => Turns >= requiredTurns - 0.001f;
    public float Progress01 => Mathf.Clamp01(Turns / requiredTurns);
    /// <summary>Eje del tornillo, hacia afuera.</summary>
    public Vector3 Axis => head.forward;
    /// <summary>Centro de la cabeza, donde se apoya la punta del destornillador.</summary>
    public Vector3 HeadPoint => head.position;

    Vector3 restPosition;
    Quaternion restRotation;

    void Awake()
    {
        restPosition = head.localPosition;
        restRotation = head.localRotation;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Gira el tornillo; positivo aprieta. Regresa los grados que sí giró, porque se detiene en los topes.</summary>
    public float Turn(float degrees)
    {
        float before = Turns;
        Turns = Mathf.Clamp(Turns + degrees / 360f, 0f, requiredTurns);

        // Seen from the front, tightening turns clockwise: positive around the outward axis.
        head.localRotation = restRotation * Quaternion.Euler(0f, 0f, Turns * 360f);
        head.localPosition = restPosition + restRotation * Vector3.back * (sinkDepth * Progress01);
        return (Turns - before) * 360f;
    }
}
