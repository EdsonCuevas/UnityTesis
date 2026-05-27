using UnityEngine;
using UnityEngine.UI;

public class LevelUIManager : MonoBehaviour
{
    [Header("Toggles")]
    public Toggle cable1Toggle;
    public Toggle cable2Toggle;

    private void Start()
    {
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (LevelProgressManager.Instance == null)
            return;

        LevelData data = LevelProgressManager.Instance.currentData;

        cable1Toggle.isOn = data.cable1Completo;
        cable2Toggle.isOn = data.cable2Completo;
    }
}