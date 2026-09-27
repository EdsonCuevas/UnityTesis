using Oculus.Interaction.Locomotion;
using UnityEngine;

public static class ComfortSettings
{
    const string SnapTurnKey = "comfort_snap_turn";
    const string VignetteKey = "comfort_vignette";

    public static bool SnapTurn
    {
        get => PlayerPrefs.GetInt(SnapTurnKey, 0) == 1;
        set { PlayerPrefs.SetInt(SnapTurnKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool Vignette
    {
        get => PlayerPrefs.GetInt(VignetteKey, 1) == 1;
        set { PlayerPrefs.SetInt(VignetteKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void ApplyToScene()
    {
        var mode = SnapTurn ? TurnerEventBroadcaster.TurnMode.Snap : TurnerEventBroadcaster.TurnMode.Smooth;
        foreach (var turner in Object.FindObjectsByType<TurnerEventBroadcaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            turner.TurnMethod = mode;

        foreach (var tunneling in Object.FindObjectsByType<LocomotionTunneling>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            tunneling.enabled = Vignette;
    }
}
