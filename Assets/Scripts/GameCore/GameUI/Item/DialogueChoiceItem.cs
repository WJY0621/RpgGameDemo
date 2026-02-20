using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueChoiceItem : MonoBehaviour
{
    public Button button;
    public TMP_Text text;
    public int id;

    void Awake()
    {
        button = GetComponent<Button>();
        text = GetComponentInChildren<TMP_Text>();
    }
}
