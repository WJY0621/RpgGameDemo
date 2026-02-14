using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DL : MonoBehaviour
{
    /* Dialogue Scriptable Objects */
    [SerializeField] private DLContainerSO dialogueContainer;
    [SerializeField] private DLGroupSO dialogueGroup;
    [SerializeField] private DLSO dialogue;

    /* Filters */
    [SerializeField] private bool groupedDialogues;
    [SerializeField] private bool startingDialoguesOnly;

    /* Indexes */
    [SerializeField] private int selectedDialogueGroupIndex;
    [SerializeField] private int selectedDialogueIndex;
}
