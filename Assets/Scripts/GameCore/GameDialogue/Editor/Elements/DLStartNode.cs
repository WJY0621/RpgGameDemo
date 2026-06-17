using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DLStartNode : DLNode
{
    public override void Initialize(string nodeName, DialogueGraphView graphView, Vector2 position)
    {
        base.Initialize(nodeName, graphView, position);

        DialogueType = DialogueType.Start;
        DialogueName = string.IsNullOrWhiteSpace(nodeName) ? "Start" : nodeName;
        Text = string.Empty;
        Choices.Clear();
        Choices.Add(new DLChoiceSaveData
        {
            Text = "Start"
        });

        SetPosition(new Rect(position, new Vector2(150f, 90f)));
    }

    public override void Draw()
    {
        title = "Start";
        titleContainer.style.backgroundColor = new Color(0.18f, 0.45f, 0.28f, 0.95f);
        mainContainer.style.backgroundColor = new Color(0.1f, 0.18f, 0.12f, 0.98f);
        mainContainer.style.borderLeftColor = new Color(0.36f, 0.72f, 0.43f, 0.95f);
        mainContainer.style.borderRightColor = new Color(0.36f, 0.72f, 0.43f, 0.95f);
        mainContainer.style.borderTopColor = new Color(0.36f, 0.72f, 0.43f, 0.95f);
        mainContainer.style.borderBottomColor = new Color(0.36f, 0.72f, 0.43f, 0.95f);
        mainContainer.style.borderLeftWidth = 1f;
        mainContainer.style.borderRightWidth = 1f;
        mainContainer.style.borderTopWidth = 1f;
        mainContainer.style.borderBottomWidth = 1f;

        Port outputPort = this.CreatePort("Start");
        outputPort.portColor = new Color(0.38f, 0.82f, 0.45f, 1f);
        outputPort.userData = Choices[0];
        outputContainer.Add(outputPort);

        RefreshExpandedState();
        RefreshPorts();
    }
}
