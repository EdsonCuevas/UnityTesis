using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Funda de una herramienta en el cinto. Soltarla cerca la guarda; soltarla lejos la deja caer. Regresa
/// sola si se pierde: si cae fuera del mapa o se queda lejos del jugador un rato.
/// </summary>
[DefaultExecutionOrder(101)]
public class ToolHolster : MonoBehaviour
{
    public ToolBelt belt;
    public Grabbable tool;
    [Tooltip("Agarre a distancia de la herramienta; se apaga mientras está guardada para que el rayo no la tome. Puede ir vacío.")]
    public GameObject distanceGrab;
    [Tooltip("Pieza de la funda que cambia de color cuando se puede guardar la herramienta. Puede ir vacío.")]
    public Renderer pouch;
    public Color readyColor = new Color(1f, 0.8f, 0.3f);

    [Tooltip("Falso: la herramienta empieza donde está en la escena (por ejemplo, en una mesa) hasta que el jugador la agarre.")]
    public bool startHolstered = true;

    [Header("Guardar")]
    [Tooltip("Distancia máxima de la herramienta a la funda para guardarla al soltarla.")]
    public float snapRadius = 0.2f;

    [Header("Regreso")]
    [Tooltip("Metros por debajo de los pies a partir de los cuales la herramienta se da por perdida.")]
    public float fallDistance = 2f;
    [Tooltip("Metros del cinto a partir de los cuales la herramienta suelta empieza a contar como olvidada.")]
    public float forgottenDistance = 2.5f;
    [Tooltip("Segundos que debe seguir lejos para regresar a la funda.")]
    public float forgottenSeconds = 8f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public bool IsHolstered { get; private set; }
    /// <summary>Veces que el jugador guardó la herramienta soltándola en la funda.</summary>
    public int StoredCount { get; private set; }
    /// <summary>Último momento en que el jugador la soltó lejos de la funda.</summary>
    public float LastDropTime { get; private set; } = float.NegativeInfinity;

    Rigidbody body;
    bool taken;
    Collider[] colliders;
    bool[] colliderWasTrigger;
    RigidbodyInterpolation interpolation;
    MaterialPropertyBlock pouchBlock;
    bool wasHeld;
    bool pouchLit;
    float awaySince = float.PositiveInfinity;

    bool IsHeld => tool.SelectingPointsCount > 0;
    bool InSnapRange => Vector3.Distance(tool.transform.position, transform.position) <= snapRadius;

    void Awake()
    {
        body = tool.GetComponent<Rigidbody>();
        interpolation = body.interpolation;
        colliders = tool.GetComponentsInChildren<Collider>(true);
        colliderWasTrigger = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderWasTrigger[i] = colliders[i].isTrigger;
        pouchBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (!startHolstered) return;
        taken = true;
        belt.FollowHead();
        Holster();
    }

    void Update()
    {
        bool held = IsHeld;
        if (held && IsHolstered) Unholster();
        if (held)
        {
            taken = true;
            awaySince = float.PositiveInfinity;
            // A tool taken from a table carries solid colliders until now.
            SetSolid(false);
        }

        if (!held && wasHeld && InSnapRange)
        {
            Holster();
            StoredCount++;
            belt.Vibrate(transform.position, 0.5f, 0.08f);
        }
        else if (!held && wasHeld)
        {
            // Dropped: only now does it collide again, so a carried tool never blocks the player's capsule.
            SetSolid(true);
            LastDropTime = Time.time;
        }
        wasHeld = held;

        SetPouchLit(held && InSnapRange);
        if (!held && !IsHolstered) CheckLost();
    }

    void LateUpdate()
    {
        if (IsHolstered && !IsHeld) PlaceInPouch();
    }

    void CheckLost()
    {
        Vector3 toolPosition = tool.transform.position;
        Vector3 beltPosition = transform.position;
        bool fell = toolPosition.y < belt.head.position.y - belt.hipDrop - 1f - fallDistance;

        if (Vector3.Distance(toolPosition, beltPosition) < forgottenDistance) awaySince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(awaySince)) awaySince = Time.time;

        // A tool still waiting on its table isn't forgotten, however far the player walks.
        if (fell || (taken && Time.time - awaySince >= forgottenSeconds)) Holster();
    }

    void Holster()
    {
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        // The Meta lock is reference counted, so it coexists with the one Grabbable takes while held.
        body.LockKinematic();
        // Interpolation would fight the pose set every frame in LateUpdate.
        body.interpolation = RigidbodyInterpolation.None;
        SetSolid(false);
        if (distanceGrab != null) distanceGrab.SetActive(false);
        IsHolstered = true;
        awaySince = float.PositiveInfinity;
        PlaceInPouch();
    }

    void Unholster()
    {
        IsHolstered = false;
        body.UnlockKinematic();
        body.interpolation = interpolation;
        if (distanceGrab != null) distanceGrab.SetActive(true);
    }

    void PlaceInPouch() => tool.transform.SetPositionAndRotation(transform.position, transform.rotation);

    // Triggers still count for the grab interactors, but the player's capsule ignores them.
    void SetSolid(bool solid)
    {
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].isTrigger = !solid || colliderWasTrigger[i];
    }

    void SetPouchLit(bool lit)
    {
        if (pouch == null || lit == pouchLit) return;
        pouchLit = lit;
        pouch.GetPropertyBlock(pouchBlock);
        if (lit) pouchBlock.SetColor(BaseColor, readyColor);
        else pouchBlock.Clear();
        pouch.SetPropertyBlock(pouchBlock);
    }
}
