using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地面平台标记：挂在每一块地板节点上
/// 自动收集自身（含子物体）的 Collider2D，并计算顶面世界高度
/// 运行时由静态注册表统一管理，供怪物绑定时"只保留与自己平台的碰撞"
/// </summary>
public class GroundPlatform : MonoBehaviour
{
    //----------------------------
    // 运行时数据
    //----------------------------
    private Collider2D[] _colliders;

    /// <summary>本平台包含的所有碰撞体</summary>
    public IReadOnlyList<Collider2D> Colliders => _colliders;

    /// <summary>平台顶面世界 Y（怪物站立面）</summary>
    public float TopY { get; private set; }

    //============================================================
    // 静态注册表：场景中所有地面平台
    //============================================================
    private static readonly List<GroundPlatform> _all = new List<GroundPlatform>();

    /// <summary>当前场景中所有已注册的地面平台</summary>
    public static IReadOnlyList<GroundPlatform> All => _all;

    private void Awake()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (!_all.Contains(this))
            _all.Add(this);
    }

    private void OnDisable()
    {
        _all.Remove(this);
    }

    /// <summary>
    /// 重新收集碰撞体并计算顶面高度
    /// （运行时动态增删子碰撞体后可手动调用，或在组件右键菜单执行）
    /// </summary>
    [ContextMenu("刷新碰撞体与顶面高度")]
    public void Refresh()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);

        float maxY = float.NegativeInfinity;
        foreach (var col in _colliders)
        {
            if (col == null) continue;
            float top = col.bounds.max.y;
            if (top > maxY) maxY = top;
        }
        TopY = maxY;
    }
}
