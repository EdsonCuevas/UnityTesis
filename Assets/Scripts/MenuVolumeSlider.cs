using UnityEngine;
using UnityEngine.UI;

/// <summary>Controla el volumen general. AudioListener.volume se conserva al cambiar de escena.</summary>
[RequireComponent(typeof(Slider))]
public class MenuVolumeSlider : MonoBehaviour
{
    const string VolumeKey = "menu_volume";

    void Start()
    {
        var slider = GetComponent<Slider>();
        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        AudioListener.volume = volume;
        slider.SetValueWithoutNotify(volume);
        slider.onValueChanged.AddListener(OnVolumeChanged);
    }

    void OnVolumeChanged(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
    }

    void OnDisable() => PlayerPrefs.Save();
}
