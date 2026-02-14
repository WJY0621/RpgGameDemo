using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LogoPanel : BasePanel
{
    public TMP_Text logoText;
    public override void Init()
    {
        SetAlphaSpeed(1.0f);
    }
    protected override void Awake()
    {
        HideLogo();
        base.Awake();
    }
    public void ShowLogo()
    {
        logoText.gameObject.SetActive(true);
    }
    public void HideLogo()
    {
        logoText.gameObject.SetActive(false);
    }
    
}
