using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ControllerPart
{
    LeftThumbstick,
    RightThumbstick,
    LeftTrigger,
    RightTrigger,
    LeftGrip,
    RightGrip,
    ButtonA,
    ButtonB,
    ButtonX,
    ButtonY
}

public class ControllerHints : MonoBehaviour
{
    [Header("Controles del rig")]
    [Tooltip("OVRLeftControllerVisual: el hueso del botón se busca dentro del modelo activo.")]
    public Transform leftControllerVisual;
    public Transform rightControllerVisual;
    public Transform leftControllerAnchor;
    public Transform rightControllerAnchor;
    public Transform head;

    [Header("Indicador")]
    [Tooltip("Plantilla inactiva: flecha con la punta hacia -Y y un TextMeshPro hijo para la etiqueta.")]
    public GameObject hintTemplate;
    public float hoverDistance = 0.06f;

    class Hint
    {
        public ControllerPart Part;
        public GameObject Root;
        public TMP_Text Label;
        public Transform Bone;
    }

    readonly List<Hint> hints = new List<Hint>();

    public void Show(ControllerPart[] parts)
    {
        HideAll();
        foreach (var part in parts)
        {
            var root = Instantiate(hintTemplate, transform);
            root.SetActive(true);
            var label = root.GetComponentInChildren<TMP_Text>(true);
            label.text = LabelFor(part);
            hints.Add(new Hint { Part = part, Root = root, Label = label });
        }
    }

    public void HideAll()
    {
        foreach (var hint in hints)
            Destroy(hint.Root);
        hints.Clear();
    }

    void LateUpdate()
    {
        foreach (var hint in hints)
        {
            if (hint.Bone == null || !hint.Bone.gameObject.activeInHierarchy)
                hint.Bone = FindBone(hint.Part);

            Transform anchor = IsLeft(hint.Part) ? leftControllerAnchor : rightControllerAnchor;
            Vector3 target = hint.Bone != null ? hint.Bone.position : anchor.position;
            Vector3 outward = target - anchor.position;
            Vector3 direction = (outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.zero) + Vector3.up;
            direction.Normalize();

            hint.Root.transform.SetPositionAndRotation(
                target + direction * hoverDistance,
                Quaternion.FromToRotation(Vector3.down, -direction));
            hint.Label.transform.rotation = Quaternion.LookRotation(hint.Label.transform.position - head.position);
        }
    }

    Transform FindBone(ControllerPart part)
    {
        Transform visual = IsLeft(part) ? leftControllerVisual : rightControllerVisual;
        Transform[] activeChildren = visual.GetComponentsInChildren<Transform>(false);

        foreach (string boneName in BoneNames(part))
            foreach (var child in activeChildren)
                if (child.name == boneName || child.name == "left_" + boneName || child.name == "right_" + boneName)
                    return child;

        return null;
    }

    static string[] BoneNames(ControllerPart part) => part switch
    {
        ControllerPart.LeftThumbstick or ControllerPart.RightThumbstick => new[] { "b_thumbstick", "b_stick" },
        ControllerPart.LeftTrigger or ControllerPart.RightTrigger => new[] { "b_trigger_front", "b_trigger" },
        ControllerPart.LeftGrip => new[] { "b_trigger_grip", "b_hold1" },
        ControllerPart.RightGrip => new[] { "b_trigger_grip", "b_hold" },
        ControllerPart.ButtonA => new[] { "b_button_a", "b_button01" },
        ControllerPart.ButtonB => new[] { "b_button_b", "b_button02" },
        ControllerPart.ButtonX => new[] { "b_button_x", "b_button01" },
        _ => new[] { "b_button_y", "b_button02" }
    };

    static bool IsLeft(ControllerPart part) =>
        part is ControllerPart.LeftThumbstick or ControllerPart.LeftTrigger or ControllerPart.LeftGrip
            or ControllerPart.ButtonX or ControllerPart.ButtonY;

    static string LabelFor(ControllerPart part) => part switch
    {
        ControllerPart.LeftThumbstick or ControllerPart.RightThumbstick => "Joystick",
        ControllerPart.LeftTrigger or ControllerPart.RightTrigger => "Gatillo",
        ControllerPart.LeftGrip or ControllerPart.RightGrip => "Grip",
        ControllerPart.ButtonA => "A",
        ControllerPart.ButtonB => "B",
        ControllerPart.ButtonX => "X",
        _ => "Y"
    };
}
