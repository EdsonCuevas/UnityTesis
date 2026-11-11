using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Asigna las voces generadas con generar_voces_220.ps1 a los niveles de 220 V: la narración de los pasos que
/// no tienen (por el nombre del paso), la intro, el atasco de la guía y los avisos (por avisos.txt).
/// </summary>
public static class AsignarVoces220
{
    static readonly string[] Levels = { "Nivel1_220", "Nivel2_220" };

    [MenuItem("Tesis/Asignar voces 220 V")]
    static void Assign()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Asignar voces 220 V", "Sal del modo Play primero.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string previous = EditorSceneManager.GetActiveScene().path;
        var report = new List<string>();
        foreach (var level in Levels) report.Add(AssignLevel(level));
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);

        EditorUtility.DisplayDialog("Asignar voces 220 V", string.Join("\n\n", report), "OK");
    }

    static string AssignLevel(string level)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity");
        var manager = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        string folder = $"Assets/Audio/{level}";
        int assigned = 0;
        var missing = new List<string>();

        AudioClip Load(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{name}.wav");
            if (clip == null) missing.Add(name);
            return clip;
        }

        if (manager.introNarration == null && (manager.introNarration = Load("00_intro")) != null) assigned++;

        foreach (var step in manager.steps)
        {
            if (step.narration == null && (step.narration = Load(step.name)) != null)
            {
                assigned++;
                EditorUtility.SetDirty(step);
            }
            if (step is GuidePullStep pull && pull.jamNarration == null && (pull.jamNarration = Load(step.name + "_atasco")) != null)
            {
                assigned++;
                EditorUtility.SetDirty(step);
            }
        }

        string list = Path.Combine(folder, "Avisos", "avisos.txt");
        if (File.Exists(list))
        {
            var cues = new List<GuidedFlowManager.VoiceCue>(manager.voiceCues);
            foreach (var line in File.ReadAllLines(list))
            {
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string name = line.Substring(0, bar);
                string text = line.Substring(bar + 1);
                if (cues.Exists(c => c.text == text && c.clip != null)) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/Avisos/{name}.wav");
                if (clip == null)
                {
                    missing.Add("Avisos/" + name);
                    continue;
                }
                cues.RemoveAll(c => c.text == text);
                cues.Add(new GuidedFlowManager.VoiceCue { text = text, clip = clip });
                assigned++;
            }
            manager.voiceCues = cues.ToArray();
        }
        else missing.Add("Avisos/avisos.txt");

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        return $"{level}: {assigned} asignados." +
               (missing.Count > 0 ? $"\nFaltan: {string.Join(", ", missing)}" : string.Empty);
    }
}
