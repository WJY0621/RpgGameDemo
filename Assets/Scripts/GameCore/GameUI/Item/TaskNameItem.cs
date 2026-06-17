using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TaskNameItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image taskNameBK;
    [SerializeField] private TMP_Text taskNameText;
    [SerializeField] private GameObject highlightIcon;

    private TaskPanel owner;
    private TaskRuntime runtime;
    private Color normalBackgroundColor = Color.white;
    private Color normalTextColor = Color.white;
    private bool hasCachedColors;
    private bool isSelected;

    public int TaskID => runtime != null ? runtime.taskID : -1;

    private void Awake()
    {
        CacheReferences();
    }

    public void Bind(TaskPanel panel, TaskRuntime taskRuntime)
    {
        owner = panel;
        runtime = taskRuntime;
        CacheReferences();
        SetSelected(false);

        if (taskNameText != null)
        {
            taskNameText.text = taskRuntime != null ? taskRuntime.taskName : string.Empty;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        CacheReferences();

        if (taskNameBK != null)
        {
            taskNameBK.color = selected ? Color.white : normalBackgroundColor;
        }

        if (taskNameText != null)
        {
            taskNameText.color = selected ? Color.black : normalTextColor;
        }

        if (highlightIcon != null && !selected)
        {
            highlightIcon.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightIcon != null)
        {
            highlightIcon.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightIcon != null)
        {
            highlightIcon.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.SelectTask(runtime);
    }

    private void CacheReferences()
    {
        if (taskNameBK == null)
        {
            Transform bk = transform.Find("TaskNameBK");
            if (bk != null)
            {
                taskNameBK = bk.GetComponent<Image>();
            }
        }

        if (taskNameText == null)
        {
            Transform textTransform = transform.Find("TaskName");
            if (textTransform != null)
            {
                taskNameText = textTransform.GetComponent<TMP_Text>();
            }
        }

        if (highlightIcon == null)
        {
            Transform highlightTransform = transform.Find("TaskNameBK/HighLightIcon");
            if (highlightTransform == null)
            {
                highlightTransform = transform.Find("HighLightIcon");
            }

            if (highlightTransform != null)
            {
                highlightIcon = highlightTransform.gameObject;
            }
        }

        if (!hasCachedColors)
        {
            if (taskNameBK != null)
            {
                normalBackgroundColor = taskNameBK.color;
            }

            if (taskNameText != null)
            {
                normalTextColor = taskNameText.color;
            }

            hasCachedColors = true;
        }

        if (highlightIcon != null && !isSelected)
        {
            highlightIcon.SetActive(false);
        }
    }
}
