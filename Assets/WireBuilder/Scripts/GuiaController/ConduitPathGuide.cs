using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Guía un cable físico a través de un ducto siguiendo waypoints.
/// Los segmentos entran al ducto de uno en uno, en orden, desde la punta.
/// </summary>
public class ConduitPathGuide : MonoBehaviour
{
    [Header("Trayectoria del ducto")]
    public Transform[] waypoints;

    [Header("Cable")]
    public WireController wireController;

    [Header("Progreso")]
    public int cableID = 1;

    [Header("Configuración")]
    [Tooltip("Radio de detección en la entrada del ducto.")]
    public float entryRadius = 0.15f;

    [Tooltip("Cuánto afecta la velocidad de la mano al avance.")]
    [Range(0.1f, 5f)]
    public float speedMultiplier = 1f;

    [Tooltip("Máximo avance de la PUNTA por FixedUpdate (metros).")]
    public float maxAdvancePerFrame = 0.02f;

    [Tooltip("Máximo de segmentos nuevos que pueden entrar por FixedUpdate. " +
             "Mantener en 1 para evitar la cascada.")]
    [Range(1, 5)]
    public int maxSegmentsPerFrame = 1;

    [Tooltip("Drag aplicado a segmentos dentro del ducto.")]
    [Range(0f, 20f)]
    public float activeDrag = 8f;

    [Header("Manos VR")]
    public Transform pushReferenceLeft;
    public Transform pushReferenceRight;

    [Header("Spacing (auto-calculado)")]
    [Tooltip("Distancia entre segmentos. Se sobreescribe en runtime si autoComputeSpacing=true.")]
    public float segmentSpacing = 0.05f;
    public bool autoComputeSpacing = true;

    // ── Estado privado ────────────────────────────────────────────────────────
    private bool _active;
    private float _progress;
    private float _totalLen;
    private float[] _cumDist;
    private Rigidbody _tipRB;

    // Cuántos segmentos han entrado ya al ducto (excluyendo la punta/endAnchorTemp)
    private int _insideCount;

    // Dirección de recorrido de la lista wireController.segments
    private bool _segsReversed;

    private float[] _originalDrags;
    private List<Collider> _disabledColliders;

    private Vector3 _prevPosLeft, _prevPosRight;
    private bool _leftInit, _rightInit;

    // ── Inicialización ────────────────────────────────────────────────────────
    private void Start()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogError("[ConduitPathGuide] Necesitas al menos 2 waypoints.", this);
            enabled = false;
            return;
        }
        BuildPath();
    }

    private void BuildPath()
    {
        _cumDist = new float[waypoints.Length];
        _cumDist[0] = 0f;
        for (int i = 1; i < waypoints.Length; i++)
            _cumDist[i] = _cumDist[i - 1] +
                          Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);
        _totalLen = _cumDist[waypoints.Length - 1];
    }

    // ── Loop principal ────────────────────────────────────────────────────────
    private void FixedUpdate()
    {
        if (wireController == null || wireController.endAnchorTemp == null) return;

        if (!_active)
        {
            float dist = Vector3.Distance(wireController.endAnchorTemp.position,
                                          waypoints[0].position);
            if (dist <= entryRadius)
                Activate();
            return;
        }

        // 1. Avanzar la punta
        float advance = ComputeAdvanceDelta();
        advance = Mathf.Min(advance, maxAdvancePerFrame);
        _progress = Mathf.Clamp(_progress + advance, 0f, _totalLen);

        _tipRB.MovePosition(EvaluatePosition(_progress));
        _tipRB.MoveRotation(EvaluateRotation(_progress));

        // 2. Meter segmentos de uno en uno (máx. maxSegmentsPerFrame)
        UpdateSegmentsInConduit();

        if (_progress >= _totalLen)
            FreezeAll();
    }

    // ── Entrada ordenada de segmentos ─────────────────────────────────────────
    /// <summary>
    /// Un segmento solo entra al ducto cuando _progress ha avanzado lo suficiente
    /// para "justificarlo" Y como máximo maxSegmentsPerFrame por frame.
    /// Así se rompe la cascada de joints.
    /// </summary>
    private void UpdateSegmentsInConduit()
    {
        var segs = wireController.segments;
        if (segs == null || segs.Count == 0) return;

        int count = segs.Count;
        int newThisFrame = 0;

        // ── Entrar segmentos nuevos (de uno en uno) ───────────────────────────
        while (newThisFrame < maxSegmentsPerFrame && _insideCount < count)
        {
            // ¿Ha avanzado lo suficiente para que entre el siguiente segmento?
            float required = (_insideCount + 1) * segmentSpacing;
            if (_progress < required) break;

            // Hacer kinematic el siguiente segmento en orden
            int realIdx = _segsReversed ? (count - 1 - _insideCount) : _insideCount;
            var rb = segs[realIdx].GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Parar su velocidad justo antes de capturarlo para evitar rebotes
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }

            _insideCount++;
            newThisFrame++;
        }

        // ── Posicionar todos los segmentos que ya están dentro ────────────────
        for (int i = 0; i < _insideCount; i++)
        {
            int realIdx = _segsReversed ? (count - 1 - i) : i;
            var rb = segs[realIdx].GetComponent<Rigidbody>();
            if (rb == null) continue;

            float segProgress = _progress - ((i + 1) * segmentSpacing);
            segProgress = Mathf.Max(0f, segProgress);

            rb.MovePosition(EvaluatePosition(segProgress));
            rb.MoveRotation(EvaluateRotation(segProgress));
        }
    }

    // ── Avance de la punta ────────────────────────────────────────────────────
    private float ComputeAdvanceDelta()
    {
        Vector3 entryDir = (waypoints[1].position - waypoints[0].position).normalized;

        if (pushReferenceLeft == null && pushReferenceRight == null)
        {
            var segs = wireController.segments;
            if (segs == null || segs.Count == 0) return 0f;
            Transform refT = segs[0];
            if (!_leftInit) { _prevPosLeft = refT.position; _leftInit = true; return 0f; }
            Vector3 d = refT.position - _prevPosLeft;
            _prevPosLeft = refT.position;
            return Vector3.Dot(d, entryDir) * speedMultiplier;
        }

        float advL = GetHandAdvance(pushReferenceLeft, ref _prevPosLeft, ref _leftInit, entryDir);
        float advR = GetHandAdvance(pushReferenceRight, ref _prevPosRight, ref _rightInit, entryDir);
        return Mathf.Max(advL, advR) * speedMultiplier;
    }

    private float GetHandAdvance(Transform hand, ref Vector3 prevPos, ref bool init, Vector3 dir)
    {
        if (hand == null) return 0f;
        if (!init) { prevPos = hand.position; init = true; return 0f; }
        Vector3 delta = hand.position - prevPos;
        prevPos = hand.position;
        return Mathf.Max(0f, Vector3.Dot(delta, dir));
    }

    // ── Activar ───────────────────────────────────────────────────────────────
    private void Activate()
    {
        _active = true;
        _progress = 0f;
        _insideCount = 0;

        _tipRB = wireController.endAnchorTemp.GetComponent<Rigidbody>();
        if (_tipRB != null)
        {
            _tipRB.linearVelocity = Vector3.zero;
            _tipRB.angularVelocity = Vector3.zero;
            _tipRB.isKinematic = true;
        }

        var segs = wireController.segments;

        // ── Detectar orden de la lista ────────────────────────────────────────
        if (segs != null && segs.Count >= 2)
        {
            Vector3 tipPos = wireController.endAnchorTemp.position;
            float distFirst = Vector3.Distance(segs[0].position, tipPos);
            float distLast = Vector3.Distance(segs[segs.Count - 1].position, tipPos);
            _segsReversed = distLast < distFirst;
            Debug.Log($"[ConduitPathGuide] segsReversed={_segsReversed} " +
                      $"(distFirst={distFirst:F3} distLast={distLast:F3})");
        }

        // ── Calcular spacing como longitud total / nº segmentos ───────────────
        // Más robusto que medir solo los primeros 5: evita el problema de
        // segmentos apilados o con spacing cercano a 0.
        if (autoComputeSpacing && segs != null && segs.Count >= 2)
        {
            float totalChainLen = 0f;
            for (int i = 0; i < segs.Count - 1; i++)
                totalChainLen += Vector3.Distance(segs[i].position, segs[i + 1].position);

            float computed = totalChainLen / (segs.Count - 1);

            // Mínimo 1 cm para evitar division/spacing ≈ 0 que mete todo a la vez
            segmentSpacing = Mathf.Max(computed, 0.01f);
            Debug.Log($"[ConduitPathGuide] segmentSpacing={segmentSpacing:F4}m " +
                      $"(chainLen={totalChainLen:F3}m, segs={segs.Count})");
        }

        _leftInit = false;
        _rightInit = false;

        // Aplicar drag a todos los segmentos pero NO hacerlos kinematic todavía
        if (segs != null)
        {
            _originalDrags = new float[segs.Count];
            for (int i = 0; i < segs.Count; i++)
            {
                var rb = segs[i].GetComponent<Rigidbody>();
                if (rb == null) continue;
                _originalDrags[i] = rb.linearDamping;
                rb.linearDamping = activeDrag;
                rb.isKinematic = false;   // garantizar que ninguno empieza kinematic
            }
        }
    }

    private void FreezeAll()
    {
        _active = false;
        _disabledColliders = new List<Collider>();

        if (_tipRB != null)
        {
            if (!_tipRB.isKinematic)
            {
                _tipRB.linearVelocity = Vector3.zero;
                _tipRB.angularVelocity = Vector3.zero;
            }
            _tipRB.isKinematic = true;
        }

        var segs = wireController.segments;
        for (int i = 0; i < segs.Count; i++)
        {
            var rb = segs[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            foreach (var col in segs[i].GetComponentsInChildren<Collider>())
            {
                if (!col.enabled) continue;
                col.enabled = false;
                _disabledColliders.Add(col);
            }
        }

        if (_tipRB != null)
        {
            foreach (var col in _tipRB.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled) continue;
                col.enabled = false;
                _disabledColliders.Add(col);
            }
        }

        _originalDrags = null;

        if (LevelProgressManager.Instance != null)
            LevelProgressManager.Instance.CompletarCable(cableID);

        Debug.Log("[ConduitPathGuide] Cable congelado.");
    }

    // ── Evaluación del path ───────────────────────────────────────────────────
    private Vector3 EvaluatePosition(float dist)
    {
        if (dist <= 0f) return waypoints[0].position;
        if (dist >= _totalLen) return waypoints[waypoints.Length - 1].position;

        for (int i = 1; i < waypoints.Length; i++)
        {
            if (_cumDist[i] >= dist)
            {
                float t = (dist - _cumDist[i - 1]) / (_cumDist[i] - _cumDist[i - 1]);
                return Vector3.Lerp(waypoints[i - 1].position, waypoints[i].position, t);
            }
        }
        return waypoints[waypoints.Length - 1].position;
    }

    private Quaternion EvaluateRotation(float dist)
    {
        Vector3 dir = GetPathDirection(dist);
        return dir != Vector3.zero ? Quaternion.LookRotation(dir) : _tipRB.rotation;
    }

    private Vector3 GetPathDirection(float dist)
    {
        for (int i = 1; i < waypoints.Length; i++)
        {
            if (_cumDist[i] >= dist)
                return (waypoints[i].position - waypoints[i - 1].position).normalized;
        }
        int last = waypoints.Length - 1;
        return (waypoints[last].position - waypoints[last - 1].position).normalized;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        Gizmos.color = _active ? Color.green : Color.yellow;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
        if (waypoints[0] != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(waypoints[0].position, entryRadius);
        }
        if (_active && _cumDist != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(EvaluatePosition(_progress), 0.03f);
        }
    }
}