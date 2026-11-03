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

