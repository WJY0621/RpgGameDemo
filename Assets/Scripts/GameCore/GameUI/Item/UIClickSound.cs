using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIClickSound : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private string soundName = "UI_Click2";
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [SerializeField] private bool ignoreDisabledButton = true;

    public void Configure(string newSoundName, float newVolume = 1f)
    {
        soundName = newSoundName;
        volume = Mathf.Clamp01(newVolume);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ignoreDisabledButton &&
            TryGetComponent(out Button button) &&
            !button.interactable)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        GameMgr.Audio?.PlayUIEffect(soundName, volume);
    }
}
