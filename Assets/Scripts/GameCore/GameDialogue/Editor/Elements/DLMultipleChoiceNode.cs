using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DLMultipleChoiceNode : DLNode
{
    private const int ChoicePreviewMaxLength = 5;

    public override void Initialize(string nodeName, DialogueGraphView graphView, Vector2 position)
    {
        base.Initialize(nodeName, graphView, position);
        DialogueType = DialogueType.MultipleChoice;
        DLChoiceSaveData choiceData = new DLChoiceSaveData()
        {
            Text = "新选项"
        };

        Choices.Add(choiceData);
    }
    public override void Draw()
    {
        base.Draw();
        /* 核心容器 */
        Button addChoiceButton = DLElementUtility.CreateButton("创建选项", () =>
        {
            DLChoiceSaveData choiceData = new DLChoiceSaveData()
            {
                Text = "新选项"
            };

            Choices.Add(choiceData);
            Port choicePort = CreateChoicePort(choiceData);
            outputContainer.Add(choicePort);
        });
        addChoiceButton.AddToClassList("ds-node__button");
        mainContainer.Insert(1, addChoiceButton);

        /* 输出的包含的内容 */
        foreach (DLChoiceSaveData choice in Choices)
        {
            Port choicePort = CreateChoicePort(choice);

            outputContainer.Add(choicePort);
        }
        RefreshExpandedState();
    }
    #region Elements Creation
    private Port CreateChoicePort(object userData)
    {
        Port choicePort = this.CreatePort();
        choicePort.userData = userData;
        DLChoiceSaveData choiceData = (DLChoiceSaveData)userData;
        choicePort.portColor = new Color(0.35f, 0.64f, 0.88f, 1);
        choicePort.portName = string.Empty;
        choicePort.tooltip = choiceData.Text;

        Button deleteChoiceButton = DLElementUtility.CreateButton("X", () =>
        {
            if(Choices.Count == 1)
            {
                return;
            }
            if (choicePort.connected)
            {
                graphView.DeleteElements(choicePort.connections);
            }

            Choices.Remove(choiceData);
            graphView.RemoveElement(choicePort);
        });
        deleteChoiceButton.AddToClassList("ds-node__delete-button");
        deleteChoiceButton.style.marginLeft = 4;

        Label previewLabel = new Label(BuildChoicePreviewText(choiceData.Text));
        previewLabel.tooltip = choiceData.Text;
        previewLabel.style.maxWidth = 52;
        previewLabel.style.whiteSpace = WhiteSpace.NoWrap;
        previewLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        previewLabel.style.color = new Color(0.88f, 0.88f, 0.88f, 1f);

        Button editChoiceButton = DLElementUtility.CreateButton("...");
        editChoiceButton.clicked += () =>
        {
            Rect worldBound = editChoiceButton.worldBound;
            UnityEditor.PopupWindow.Show(worldBound, new ChoiceEditPopup(choiceData.Text, newText =>
            {
                choiceData.Text = newText;
                previewLabel.text = BuildChoicePreviewText(newText);
                previewLabel.tooltip = newText;
                choicePort.tooltip = newText;
            }));
        };
        editChoiceButton.style.minWidth = 28;
        editChoiceButton.style.width = 28;
        editChoiceButton.style.marginLeft = 4;
        editChoiceButton.style.marginRight = 2;

        choicePort.Add(previewLabel);
        choicePort.Add(editChoiceButton);
        choicePort.Add(deleteChoiceButton);

        choicePort.schedule.Execute(() =>
        {
            foreach (Label label in choicePort.Query<Label>().ToList())
            {
                if (label != previewLabel)
                {
                    label.style.display = DisplayStyle.None;
                }
            }
        }).ExecuteLater(1);

        return choicePort;
    }

    private static string BuildChoicePreviewText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string singleLineText = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (singleLineText.Length <= ChoicePreviewMaxLength)
        {
            return singleLineText;
        }

        return singleLineText.Substring(0, ChoicePreviewMaxLength) + "...";
    }

    private sealed class ChoiceEditPopup : PopupWindowContent
    {
        private readonly System.Action<string> onApply;
        private string currentText;

        public ChoiceEditPopup(string initialText, System.Action<string> onApply)
        {
            currentText = initialText ?? string.Empty;
            this.onApply = onApply;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(260f, 78f);
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            string newText = EditorGUILayout.TextArea(currentText, GUILayout.Height(66f));
            if (EditorGUI.EndChangeCheck())
            {
                currentText = newText;
                onApply?.Invoke(currentText);
            }
        }
    }
    #endregion

}
