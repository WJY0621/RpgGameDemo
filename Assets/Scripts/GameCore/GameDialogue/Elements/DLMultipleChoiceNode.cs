using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DLMultipleChoiceNode : DLNode
{
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

        TextField choiceTextField = DLElementUtility.CreateTextField(choiceData.Text, null, callback =>
        {
            choiceData.Text = callback.newValue;
        });
        choiceTextField.AddClasses(
            "ds-node__textfield",
            "ds-node__choice-textfield",
            "ds-node__textfield__hidden"
        );

        choicePort.Add(choiceTextField);
        choicePort.Add(deleteChoiceButton);
        return choicePort;
    }
    #endregion

}
