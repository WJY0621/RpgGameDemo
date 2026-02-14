using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MessagePriority 
{
	Normal,
    Medium,
	High,
	Highest
}
public class GameMessage
{
    //消息本体
    public string message;
    //播放音效
    public string sound;
    public MessagePriority priority;
}
