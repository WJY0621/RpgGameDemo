using TMPro;
using UnityEngine;

public sealed class PlayerMainTaskController
{
    private readonly Transform root;

    private Transform taskRoot;
    private TMP_Text taskNameText;
    private TMP_Text taskStepNameText;
    private TMP_Text taskStepDescriptionText;

    private TaskDataSO previewTaskDataSO;
    private int previewTaskID = -1;
    private int previewStepIndex;

    public PlayerMainTaskController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init(TaskDataSO taskDataSO, int taskID, int stepIndex)
    {
        taskRoot = root.Find("Task");
        taskNameText = root.Find("Task/TaskName")?.GetComponent<TMP_Text>();
        taskStepNameText = root.Find("Task/TaskStepName")?.GetComponent<TMP_Text>();
        taskStepDescriptionText = root.Find("Task/TaskStepDescription")?.GetComponent<TMP_Text>();

        previewTaskDataSO = taskDataSO;
        previewTaskID = taskID;
        previewStepIndex = Mathf.Max(0, stepIndex);
        RefreshPreview();
    }

    public void SetTrackedTask(TaskDataSO taskDataSO, int taskID, int stepIndex = 0)
    {
        previewTaskDataSO = taskDataSO;
        previewTaskID = taskID;
        previewStepIndex = Mathf.Max(0, stepIndex);
        RefreshPreview();
    }

    public void SetTrackedTaskRuntime(TaskRuntime runtime)
    {
        if (runtime == null)
        {
            SetTexts(string.Empty, string.Empty, string.Empty);
            return;
        }

        TaskStepRuntime currentStep = runtime.GetCurrentStep();
        SetTexts(
            runtime.taskName,
            currentStep != null ? currentStep.stepName : string.Empty,
            currentStep != null ? currentStep.BuildDisplayText() : runtime.taskDescription);
    }

    public void RefreshPreview()
    {
        TaskData taskData = previewTaskDataSO != null && previewTaskID >= 0
            ? previewTaskDataSO.GetTaskData(previewTaskID)
            : null;

        if (taskData == null)
        {
            SetTexts(string.Empty, string.Empty, string.Empty);
            return;
        }

        TaskStepData stepData = null;
        if (taskData.stepList != null && taskData.stepList.Count > 0)
        {
            int stepIndex = Mathf.Clamp(previewStepIndex, 0, taskData.stepList.Count - 1);
            stepData = taskData.stepList[stepIndex];
        }

        SetTexts(
            taskData.taskName,
            stepData != null ? stepData.stepName : string.Empty,
            stepData != null ? stepData.stepDescription : taskData.taskDescription);
    }

    private void SetTexts(string taskName, string stepName, string stepDescription)
    {
        if (taskNameText != null)
        {
            taskNameText.text = taskName;
        }

        if (taskStepNameText != null)
        {
            taskStepNameText.text = stepName;
        }

        if (taskStepDescriptionText != null)
        {
            taskStepDescriptionText.text = stepDescription;
        }

        if (taskRoot != null)
        {
            bool hasTask = !string.IsNullOrEmpty(taskName) || !string.IsNullOrEmpty(stepName) || !string.IsNullOrEmpty(stepDescription);
            taskRoot.gameObject.SetActive(hasTask);
        }
    }
}
