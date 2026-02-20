using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageMgr
{
    public Queue<GameMessage> gameMessagesQueue = new Queue<GameMessage>();
    public bool processPushMessage;
    //消息显示时长
    public float showTime = 2;
    public float timer = 0;
    private PlayerMainPanel panel;
    //TODO 游戏的消息面板

    public MessageMgr()
    {
        GameMgr.Event.Register("ShowMessge", new GameEventThreeParam<string, string, MessagePriority>
                                            (new GameActionThreeParam<string, string, MessagePriority>(RegisterMessage)));
    }

    public void RegisterMessage(string message,string sound = "noone",MessagePriority priority = MessagePriority.Normal)
	{
		gameMessagesQueue.Enqueue(new GameMessage() 
		{
			message = message,
			sound = sound,
			priority = priority
		});
        if (!processPushMessage)
        {
            processPushMessage = true;
            ProcessPushMessage();
        }
	}

    public void ProcessPushMessage()
    {
        if(panel == null)
        {
            panel = GameMgr.UI.GetPanelWithoutLoad<PlayerMainPanel>();
            return;
        }
        if(gameMessagesQueue.Count > 0)
        {
            processPushMessage = true;
        }
        if (!processPushMessage)
        {
            timer = 0f;
            if(panel.MessagePanel.alpha != 0)
            {
                panel.MessagePanel.alpha -= Time.deltaTime * 5;
            }
            return;
        }
        if(timer <= 0)
        {
            if(panel.MessagePanel.alpha > 0)
            {
                panel.MessagePanel.alpha -= Time.deltaTime * 5;
                processPushMessage = gameMessagesQueue.Count > 0;
            }
            else
            {
                GameMessage message = gameMessagesQueue.Dequeue();
                string messageText = message.message;
                if(message.priority == MessagePriority.Medium)
                {
                    messageText = $"<color=#00C0FF>{messageText}</color>";
                }
                if(message.priority == MessagePriority.High)
                {
                    messageText = $"<color=#D4BB00>{messageText}</color>";
                }
                panel.MessageText.text = messageText;
                if(message.sound != "noone")
                {
                    GameMgr.Audio.PlayUIEffect(message.sound);
                }
                timer = showTime;
            }
        }
        else
        {
            if(panel.MessagePanel.alpha < 1)
            {
                panel.MessagePanel.alpha += Time.deltaTime * 5;
            }
            else
            {
                timer -= Time.deltaTime;
            }
        }
    }
}
