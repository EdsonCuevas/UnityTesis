using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public OVRScreenFade screenFade;

    bool loading;

    public void LoadScene(string sceneName)
    {
        if (loading) return;
        loading = true;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    /// <summary>Carga un nivel en el voltaje elegido en el menú (127 V o 220 V).</summary>
    public void LoadLevel(string sceneName) => LoadScene(VoltageMode.SceneFor(sceneName));

    public void StartBtn()
    {
        SceneManager.LoadScene("TutorialScene");
    }

    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        if (screenFade != null)
        {
            screenFade.FadeOut();
            yield return new WaitForSeconds(screenFade.fadeTime);
        }
        SceneManager.LoadScene(sceneName);
    }
}

