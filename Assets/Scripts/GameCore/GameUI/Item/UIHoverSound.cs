using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIHoverSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private string soundName = "UI_Hover3";
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [SerializeField] private bool ignoreDisabledButton = true;

    public void Configure(string newSoundName, float newVolume = 1f)
    {
        soundName = newSoundName;
        volume = Mathf.Clamp01(newVolume);
    }

    public void OnPointerEnter(PointerEventData eventData)
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
