using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameDialoguePanel : BasePanel
{
    public TMP_Text speakerName;
    public TMP_Text contentText;
    public Action OnMoveNext;
    private InputAction moveNextAction;

    public GameObject ChoiceContainer;
    public GameObject choiceItemPrefab;
    public Action<int> OnChoiceSelected;
    private int choiceCount = 0;
    private bool IsShooce;

    [Header("打字机设置")]
    [SerializeField] private float charactersPerSecond = 25f; // 打字速度（字符/秒）
    
    private string originalText = "";       // 原始完整文本
    private bool isTyping = false;          // 是否正在打字
    private Coroutine typingCoroutine;      // 打字协程引用
    private DLSO currentNode;              // 当前对话节点

    public override void Init()
    {
        OnMoveNext?.Invoke();
        this.Hide();
    }

    void OnEnable()
    {
        moveNextAction = GameMgr.input.playerInput.UI.PushDialogue;
        // 绑定回调函数
        moveNextAction.performed += OnMoveNextInput;
    }
    private void OnDisable()
    {
        // 取消绑定并禁用
        moveNextAction.performed -= OnMoveNextInput;
        StopTyping();
    }

    private void OnMoveNextInput(InputAction.CallbackContext context)
    {
        // 确保只在按下时触发一次（避免长按重复触发）
        // 确保只在按下时触发一次（避免长按重复触发）
        if (context.performed)
        {
            if (IsShooce == false)
            {
                // 如果正在打字，则跳过打字
                if (isTyping)
                {
                    SkipTyping();
                }
                else
                {
                    OnMoveNext?.Invoke();
                }
            }
        }
    }

    public override void Show()
    {
        base.Show();
        GameMgr.input.EnableUIActionMap();
    }

    public void UpdatePanel(DLSO node)
    {
        // 使用打字机效果显示文本
        currentNode = node;
        StartTypewriterEffect(node.Text);
        if(node.DialogueType != DialogueType.MultipleChoice)
        {
            HideChoices();
        }
    }

    public void ShowChoices()
    {
        ChoiceContainer.SetActive(true);
        IsShooce = true;
    }

    public void HideChoices()
    {
        ChoiceContainer.SetActive(false);
        for (int i = 0; i < ChoiceContainer.transform.childCount; i++)
        {
            Destroy(ChoiceContainer.transform.GetChild(i).gameObject);
        }
        choiceCount = 0;
        IsShooce = false;
    }

    public void AddChoiceItem(DLSO node)
    {
        for (int i = 0; i < node.Choices.Count; i++)
        {
            GameObject choiceItem = Instantiate(choiceItemPrefab);
            DialogueChoiceItem dialogueChoiceItem = choiceItem.GetComponent<DialogueChoiceItem>();
            dialogueChoiceItem.button.onClick.AddListener(() =>
            {
                OnChoiceSelected?.Invoke(dialogueChoiceItem.id);
            });
            dialogueChoiceItem.text.text = node.Choices[i].Text;
            dialogueChoiceItem.id = choiceCount;
            choiceItem.transform.SetParent(ChoiceContainer.transform, false);
            choiceCount++;
        }
    }
    public void SetSpeakerName(string speakerName)
    {
        this.speakerName.text = speakerName;
    }

    #region 打字机效果方法
    
    /// <summary>
    /// 开始打字机效果
    /// </summary>
    /// <param name="text">要显示的文本</param>
    public void StartTypewriterEffect(string text)
    {
        originalText = text;
        
        // 如果已经有打字协程在运行，先停止它
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        // 重置文本显示
        contentText.text = "";
        contentText.maxVisibleCharacters = 0;
        
        // 开始新的打字协程
        typingCoroutine = StartCoroutine(TypeTextCoroutine(text));
    }
    
    /// <summary>
    /// 打字机协程
    /// </summary>
    private IEnumerator TypeTextCoroutine(string text)
    {
        isTyping = true;
        
        int totalCharacters = text.Length;
        float delay = 1f / charactersPerSecond;
        
        // 处理富文本标签
        bool inTag = false;
        int visibleCharCount = 0;
        
        for (int i = 0; i < totalCharacters; i++)
        {
            char c = text[i];
            
            // 处理标签
            if (c == '<')
            {
                inTag = true;
            }
            else if (c == '>')
            {
                inTag = false;
                contentText.text = text.Substring(0, i + 1);
                yield return null; // 等待一帧
                continue;
            }
            
            if (inTag)
            {
                contentText.text = text.Substring(0, i + 1);
                yield return null; // 等待一帧
                continue;
            }
            
            // 增加可见字符计数
            visibleCharCount++;
            contentText.text = text.Substring(0, i + 1);
            contentText.maxVisibleCharacters = visibleCharCount;
            
            // 等待固定时间
            yield return new WaitForSeconds(delay);
        }
        
        // 打字完成
        isTyping = false;
        typingCoroutine = null;
        
        // 如果当前节点是选择节点，打字完成后才显示选择项
        if (currentNode != null && currentNode.DialogueType == DialogueType.MultipleChoice)
        {
            ShowChoices();
            // 重新添加选择项（因为在打字期间可能已经清除了）
            AddChoiceItem(currentNode);
        }
    }
    
    /// <summary>
    /// 跳过打字，直接显示完整文本
    /// </summary>
    public void SkipTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        // 显示完整文本
        contentText.text = originalText;
        contentText.maxVisibleCharacters = originalText.Length;
        isTyping = false;
        
        // 如果当前节点是选择节点，立即显示选择项
        if (currentNode != null && currentNode.DialogueType == DialogueType.MultipleChoice)
        {
            ShowChoices();
            AddChoiceItem(currentNode);
        }
    }
    
    /// <summary>
    /// 停止打字
    /// </summary>
    public void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            isTyping = false;
        }
    }
    
    #endregion
}
