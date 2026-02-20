using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickUp : MonoBehaviour
{
    [Header("物品ID")]
    public int itemId;
    [Header("物品数量")]
    public int count = 1;
    [Header("是否自动旋转")]
    public bool autoRotate = true;

    private void Update()
    {
        if (autoRotate)
        {
            transform.Rotate(Vector3.up * 50 * Time.deltaTime);
        }
    }

    // 触发器进入检测（当玩家走进物品范围）
    private void OnTriggerEnter(Collider other)
    {
        // 假设玩家身上有 Tag "Player" 或者具体的 PlayerController 脚本
        if (other.CompareTag("Player"))
        {
            PickUp();
        }
    }

    private async void PickUp()
    {
        // 1. 添加到背包数据
        await GameMgr.Package.AddItem(itemId, count);

        // 2. (可选) 播放拾取音效
        // GameMgr.Audio.PlayEffect("PickUpSound");

        // 3. (可选) 飘字提示 UI
        Debug.Log($"拾取了物品 ID:{itemId}, 数量:{count}");

        // 4. 销毁场景物体
        Destroy(gameObject);
    }
}
