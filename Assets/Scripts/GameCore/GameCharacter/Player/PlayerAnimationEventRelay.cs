using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    private PlayerAttackController attackController;
    private PlayerWeaponAttachmentController weaponAttachmentController;
    private PlayerWeaponAttackEffectController weaponAttackEffectController;
    private PlayerAudioController audioController;

    public void Bind(PlayerAttackController controller)
    {
        attackController = controller;
        weaponAttachmentController = GetComponent<PlayerWeaponAttachmentController>();
        weaponAttackEffectController = GetComponentInParent<PlayerWeaponAttackEffectController>();
        audioController = GetComponentInParent<PlayerAudioController>();
    }

    // Animation Event
    public void BeginAttackWindow()
    {
        attackController?.BeginAttackWindow();
    }

    // Animation Event
    public void ApplyAttackDamage()
    {
        attackController?.ApplyAttackDamage();
    }

    // Animation Event
    public void EndAttackWindow()
    {
        attackController?.EndAttackWindow();
    }

    // Animation Event
    public void WeaponAttackVfxEvent(int eventIndex)
    {
        EnsureWeaponAttackEffectController();
        weaponAttackEffectController?.TriggerVfxEventByAnimation(eventIndex);
    }

    // Animation Event
    public void WeaponAttackAudioEvent(int eventIndex)
    {
        EnsureWeaponAttackEffectController();
        weaponAttackEffectController?.TriggerAudioEventByAnimation(eventIndex);
    }

    // Animation Event
    public void WeaponAttackHitboxBegin(int eventIndex)
    {
        EnsureWeaponAttackEffectController();
        weaponAttackEffectController?.BeginHitboxEventByAnimation(eventIndex);
    }

    // Animation Event
    public void WeaponAttackHitboxApply(int eventIndex)
    {
        EnsureWeaponAttackEffectController();
        weaponAttackEffectController?.ApplyHitboxEventByAnimation(eventIndex);
    }

    // Animation Event
    public void WeaponAttackHitboxEnd(int eventIndex)
    {
        EnsureWeaponAttackEffectController();
        weaponAttackEffectController?.EndHitboxEventByAnimation(eventIndex);
    }

    // Animation Event
    public void Footstep()
    {
        PlayFootstep();
    }

    // Animation Event
    public void PlayerFootstep()
    {
        PlayFootstep();
    }

    // Animation Event
    public void PlayFootstep()
    {
        EnsureAudioController();
        audioController?.PlayFootstep();
    }

    // Animation Event
    public void AttachWeaponToHand()
    {
        EnsureWeaponAttachmentController();
        weaponAttachmentController?.AttachWeaponToHand();
    }

    // Animation Event
    public void AttachWeaponToBack()
    {
        EnsureWeaponAttachmentController();
        weaponAttachmentController?.AttachWeaponToBack();
    }

    private void EnsureWeaponAttachmentController()
    {
        if (weaponAttachmentController == null)
        {
            weaponAttachmentController = GetComponent<PlayerWeaponAttachmentController>();
        }
    }

    private void EnsureWeaponAttackEffectController()
    {
        if (weaponAttackEffectController == null)
        {
            weaponAttackEffectController = GetComponentInParent<PlayerWeaponAttackEffectController>();
        }
    }

    private void EnsureAudioController()
    {
        if (audioController == null)
        {
            audioController = GetComponentInParent<PlayerAudioController>();
        }
    }
}
