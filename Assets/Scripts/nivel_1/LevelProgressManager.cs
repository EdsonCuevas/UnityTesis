using UnityEngine;
using System.IO;

public class LevelProgressManager : MonoBehaviour
{
    public static LevelProgressManager Instance;

    [Header("Nivel actual")]
    public string nombreNivel = "nivel1";

    private string FilePath => Path.Combine(Application.persistentDataPath, nombreNivel + ".json");

    public LevelData currentData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadLevel();
    }

    public void LoadLevel()
    {
        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            currentData = JsonUtility.FromJson<LevelData>(json);

            Debug.Log("JSON cargado: " + json);
        }
        else
        {
            currentData = new LevelData
            {
                nivel = nombreNivel,
                cable1Completo = false,
                cable2Completo = false,
                nivelCompletado = false
            };

            SaveLevel();
        }
    }

    public void SaveLevel()
    {
        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(FilePath, json);

        Debug.Log("JSON guardado en: " + FilePath);
        Debug.Log(json);
    }

    public void CompletarCable(int cableID)
    {
        if (cableID == 1)
            currentData.cable1Completo = true;

        if (cableID == 2)
            currentData.cable2Completo = true;

        if (currentData.cable1Completo && currentData.cable2Completo)
        {
            currentData.nivelCompletado = true;

            Debug.Log("Nivel completado");
        }

        SaveLevel();
        FindObjectOfType<LevelUIManager>()?.UpdateUI();
    }
}