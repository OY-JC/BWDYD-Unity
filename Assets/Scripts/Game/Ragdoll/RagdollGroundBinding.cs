using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物与地面平台的碰撞绑定：
/// 生成器生成怪物时调用 Bind(平台)，怪物只会与该平台发生物理碰撞，
/// 自动忽略其他平台；所有怪物之间也互不碰撞。
/// 组件挂在怪物预制体根节点，Bind 由生成器在实例化后立即调用。
/// </summary>
public class RagdollGroundBinding : MonoBehaviour
{
    //----------------------------
    // 运行时数据
    //----------------------------
    private Collider2D[] _bodyColliders;

    /// <summary>当前绑定的地面平台（未绑定为 null）</summary>
    public GroundPlatform BoundFloor { get; private set; }

    //============================================================
    // 静态注册表：场上所有怪物，用于互相忽略碰撞
    //============================================================
    private static readonly List<RagdollGroundBinding> _allMonsters = new List<RagdollGroundBinding>();

    private void Awake()
    {
        // 收集全身（含手掌/头等子碰撞体）的碰撞体
        _bodyColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void OnEnable()
    {
        if (!_allMonsters.Contains(this))
            _allMonsters.Add(this);
    }

    private void OnDisable()
    {
        _allMonsters.Remove(this);
    }

    /// <summary>
    /// 绑定指定地面平台：
    /// 1. 忽略与"其他平台"的碰撞
    /// 2. 忽略与"其他怪物"的碰撞
    /// 3. 把平台传给平稳传感器作为落地判定依据
    /// </summary>
    public void Bind(GroundPlatform floor)
    {
        if (floor == null)
        {
            Debug.LogWarning($"[{nameof(RagdollGroundBinding)}] 传入的平台为空，绑定失败", this);
            return;
        }

        if (_bodyColliders == null)
            _bodyColliders = GetComponentsInChildren<Collider2D>(true);

        BoundFloor = floor;

        // 1. 忽略其他所有平台（只保留与传入平台的碰撞）
        foreach (var platform in GroundPlatform.All)
        {
            if (platform == null || platform == floor) continue;
            IgnorePairs(_bodyColliders, platform.Colliders);
        }

        // 2. 怪物之间互不碰撞（IgnoreCollision 对双方生效，只需新怪物单向设置）
        foreach (var monster in _allMonsters)
        {
            if (monster == null || monster == this) continue;
            IgnorePairs(_bodyColliders, monster._bodyColliders);
        }

        // 3. 通知平稳传感器：落地只认这块平台
        var sensor = GetComponent<RagdollStabilitySensor>();
        if (sensor != null)
            sensor.BindFloor(floor);
    }

    /// <summary>批量忽略两组碰撞体之间的物理碰撞</summary>
    private static void IgnorePairs(IReadOnlyList<Collider2D> a, IReadOnlyList<Collider2D> b)
    {
        if (a == null || b == null) return;
        foreach (var ca in a)
        {
            if (ca == null) continue;
            foreach (var cb in b)
            {
                if (cb == null) continue;
                Physics2D.IgnoreCollision(ca, cb, true);
            }
        }
    }
}
