using UnityEngine;

/// <summary>
/// 布娃娃拖拽系统的可调参数集合（ScriptableObject）
/// 在 Project 视图右键 → BWDYD → Ragdoll Profile 创建实例
/// 多个敌人可共用同一个 Profile 实例，便于统一调参
/// </summary>
[CreateAssetMenu(fileName = "RagdollProfile", menuName = "BWDYD/Ragdoll Profile", order = 0)]
public class RagdollProfile : ScriptableObject
{
    //----------------------------
    // 点击检测
    //----------------------------
    [Header("点击检测")]
    [Tooltip("鼠标点击容差半径，命中半径内的关节即可拖拽")]
    public float touchRadius = 0.5f;

    //----------------------------
    // 拖拽弹簧（TargetJoint2D 参数）
    //----------------------------
    [Header("拖拽弹簧")]
    [Tooltip("弹簧频率：值越大，跟随越紧")]
    public float dragFrequency  = 6f;
    [Tooltip("阻尼比：0~1，越大越不抖")]
    public float dragDamping    = 0.7f;
    [Tooltip("最大作用力 = 该值 × 关节质量")]
    public float maxForceFactor = 1000f;

    //----------------------------
    // 落地恢复
    //----------------------------
    [Header("落地恢复")]
    [Tooltip("地面层（默认 Layer 6）")]
    public LayerMask groundLayers = (1 << 6);
    [Tooltip("关节速度低于此值视为静止")]
    public float stableThreshold  = 0.3f;
    [Tooltip("持续静止多久才触发归位（秒）")]
    public float stableDuration   = 2f;
}
