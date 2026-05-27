using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Añade este componente al GameObject del tubo (ducto).
/// Crea GameObjects hijo dentro del tubo como waypoints siguiendo
/// el recorrido interior, incluyendo la curva L.
/// Asigna los waypoints en orden en el array 'waypoints'.
/// </summary>
public class ConduitPathGuide : MonoBehaviour
{
    [Header("Trayectoria del ducto")]
    [Tooltip("Waypoints en orden desde la entrada hasta la salida. " +
             "Colócalos manualmente dentro del tubo 3D siguiendo la curva L.")]
    public Transform[] waypoints;

    [Header("Cable")]
    [Tooltip("WireController del cable que se insertará en este ducto.")]
    public WireController wireController;
    [Header("Progreso")]
    public int cableID = 1;

    [Header("Configuración")]
    [Tooltip("Radio de detección en la entrada del ducto.")]
    public float entryRadius = 0.15f;

    [Tooltip("Cuánto afecta la velocidad del segmento al avance por el ducto.")]
    [Range(0.1f, 5f)]
    public float speedMultiplier = 1f;

    [Tooltip("Máximo avance del cable por Physics frame (metros). Evita que el cable se dispare al soltar la mano.")]
    public float maxAdvancePerFrame = 0.02f;

    [Tooltip("Drag aplicado a los segmentos mientras están en el ducto. Amortigua rebotes y movimientos bruscos.")]
    [Range(0f, 20f)]
    public float activeDrag = 8f;

    [Header("Manos VR")]
    [Tooltip("Controller Tracking Izquierdo (BuildingBlock).")]
    public Transform pushReferenceLeft;
    [Tooltip("Controller Tracking Derecho (BuildingBlock).")]
    public Transform pushReferenceRight;

    // Estado privado (añadir a los ya existentes):
    private Vector3 _prevPosLeft;
    private Vector3 _prevPosRight;
    private bool _leftInit;
    private bool _rightInit;
    private float[] _originalDrags;
    private List<Collider> _disabledColliders;

    // ── Estado ────────────────────────────────────────────────────────────────
    private bool _active;
    private float _progress;       // distancia recorrida en el path (metros)
    private float _totalLen;
    private float[] _cumDist;       // distancias acumuladas por segmento del path
    private Rigidbody _tipRB;

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
            // Detectar si la punta del cable está cerca de la entrada
            float dist = Vector3.Distance(wireController.endAnchorTemp.position,
                                          waypoints[0].position);
            if (dist <= entryRadius)
                Activate();
            return;
        }

        // Calcular cuánto avanza la punta en este frame
        float advance = ComputeAdvanceDelta();
        // Cap por frame: evita que un release brusco dispare el cable de golpe
        advance = Mathf.Min(advance, maxAdvancePerFrame);
        _progress = Mathf.Clamp(_progress + advance, 0f, _totalLen);

        // Mover la punta kinematicamente a lo largo del path
        _tipRB.MovePosition(EvaluatePosition(_progress));
        _tipRB.MoveRotation(EvaluateRotation(_progress));

        // Llegó al final
        if (_progress >= _totalLen)
            FreezeAll();
    }

    // ── Avance: usa la velocidad del segmento que empuja la punta ────────────
    private float ComputeAdvanceDelta()
    {
        Vector3 entryDir = (waypoints[1].position - waypoints[0].position).normalized;

        // Si no hay manos asignadas, usar el primer segmento como fallback
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

        // La mano que más empuja gana; ignorar la que está retrocediendo para reposicionarse
        return Mathf.Max(advL, advR) * speedMultiplier;
    }

    private float GetHandAdvance(Transform hand, ref Vector3 prevPos, ref bool init, Vector3 dir)
    {
        if (hand == null) return 0f;
        if (!init) { prevPos = hand.position; init = true; return 0f; }

        Vector3 delta = hand.position - prevPos;
        prevPos = hand.position;

        // Solo avance positivo: cuando la mano retrocede para reposicionarse no se retrae el cable
        return Mathf.Max(0f, Vector3.Dot(delta, dir));
    }

    // ── Activar / Desactivar ──────────────────────────────────────────────────
    private void Activate()
    {
        _active = true;
        _progress = 0f;
        _tipRB = wireController.endAnchorTemp.GetComponent<Rigidbody>();
        if (_tipRB != null)
            _tipRB.isKinematic = true;

        Debug.Log("[ConduitPathGuide] Cable entrando al ducto.");
        _leftInit = false;
        _rightInit = false;

        // Aumentar drag de todos los segmentos para amortiguar movimientos bruscos
        var segs = wireController.segments;
        _originalDrags = new float[segs.Count];
        for (int i = 0; i < segs.Count; i++)
        {
            var rb = segs[i].GetComponent<Rigidbody>();
            if (rb == null) continue;
            _originalDrags[i] = rb.linearDamping;
            rb.linearDamping = activeDrag;
        }
    }

    private void Deactivate()
    {
        _active = false;
        if (_tipRB != null)
            _tipRB.isKinematic = false;

        // Restaurar drag original y limpiar velocidades para evitar spring-back
        var segs = wireController.segments;
        for (int i = 0; i < segs.Count; i++)
        {
            var rb = segs[i].GetComponent<Rigidbody>();
            if (rb == null) continue;
            if (_originalDrags != null && i < _originalDrags.Length)
                rb.linearDamping = _originalDrags[i];
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        _originalDrags = null;

        Debug.Log("[ConduitPathGuide] Cable atravesó el ducto.");
    }

    private void FreezeAll()
    {
        _active = false;
        _disabledColliders = new List<Collider>();

        // Congelar la punta (siempre está dentro al llegar al final)
        if (_tipRB != null)
        {
            if (!_tipRB.isKinematic)
            {
                _tipRB.linearVelocity = Vector3.zero;
                _tipRB.angularVelocity = Vector3.zero;
            }
            _tipRB.isKinematic = true;
        }

        // Congelar todos los segmentos en su posición actual y quitar colisiones
        var segs = wireController.segments;
        for (int i = 0; i < segs.Count; i++)
        {
            var rb = segs[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }

            // Quitar colisiones a TODOS los segmentos para que sea atravesable
            Collider[] cols = segs[i].GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    _disabledColliders.Add(col);
                }
            }
        }

        // Quitar colisiones de la punta también
        if (_tipRB != null)
        {
            Collider[] tipCols = _tipRB.GetComponentsInChildren<Collider>();
            foreach (var col in tipCols)
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    _disabledColliders.Add(col);
                }
            }
        }

        _originalDrags = null;

        if (LevelProgressManager.Instance != null)
        {
            LevelProgressManager.Instance.CompletarCable(cableID);
        }

        Debug.Log("[ConduitPathGuide] Cable congelado. Parte interna kinematic, todo el cable sin colisiones.");
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
        // Mostrar progreso actual
        if (_active && _cumDist != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(EvaluatePosition(_progress), 0.03f);
        }
    }
}