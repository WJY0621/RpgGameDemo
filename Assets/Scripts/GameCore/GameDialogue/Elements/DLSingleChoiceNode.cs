using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class DLSingleChoiceNode : DLNode
{
    public override void Initialize(string nodeName, DialogueGraphView graphView, Vector2 position)
    {
        base.Initialize(nodeName, graphView, position);

        DialogueType = DialogueType.SingleChoice;
        DLChoiceSaveData choiceData = new DLChoiceSaveData()
        {
            Text = "下个对话"
        };
        Choices.Add(choiceData);
    }
    public override void Draw()
    {
        base.Draw();
        /* 输出的包含的内容 */
        foreach(DLChoiceSaveData choice in Choices)
        {
            Port choicePort = this.CreatePort(choice.Text);
            choicePort.portColor = new Color(0.35f, 0.64f, 0.88f, 1);

            choicePort.userData = choice;

            outputContainer.Add(choicePort);
        }
        RefreshExpandedState();
    }
}
