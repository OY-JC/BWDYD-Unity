using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 撞击伤害结算（挂怪物根节点）：
/// - 飞行窗口内（拖拽松手后 → 平稳起身前）碰撞才结算，走路/攻击状态互挤不扣血
/// - 撞环境（地板/墙）：自己扣血，碰撞瞬间立即结算（闪白即时）；冷却窗口内后续碰撞丢弃防弹跳连扣
/// - 撞其他怪：双方都按相对速度扣血，一次飞行对同一目标只结算一次
/// 伤害线性公式：damage = (相对速度 - impactMinSpeed) × damagePerSpeed，不设上限
/// </summary>
public class MonsterImpactDamage : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("撞击判定")]
    [Tooltip("最小撞击速度：相对速度低于此值不结算（轻放/轻碰不扣血）")]
    [Range(0f, 30f)]
    [SerializeField] private float impactMinSpeed = 8f;

    [Tooltip("伤害系数：damage = (相对速度 - 最小撞击速度) × 此值")]
    [Range(0f, 20f)]
    [SerializeField] private float damagePerSpeed = 2f;

    [Tooltip("环境碰撞冷却（秒）：碰撞瞬间立即扣血，冷却内的后续碰撞丢弃，防止弹跳着地连扣")]
    [Range(0.05f, 1f)]
    [SerializeField] private float mergeWindow = 0.25f;

    //----------------------------
    // 运行时状态
    //----------------------------
    /// <summary>全局怪物对撞去重：两只都在飞行中的怪互撞时，双方结算器各收到一次上报，这里保证一对怪只结算一次</summary>
    private static readonly Dictionary<(int, int), float> s_recentPairs = new Dictionary<(int, int), float>();
    private const float PairDedupTime = 0.5f;

    private MonsterStats _stats;
    private readonly HashSet<MonsterStats> _hitTargets = new HashSet<MonsterStats>();
    private float _lastEnvHitTime = -999f;   // 上次环境撞击结算时间（冷却用，防止弹跳着地连扣）
    private bool _airborne;

    /// <summary>开关飞行伤害窗口（由 Ragdoll 状态机在松手/平稳时调用）；关闭时清空命中记录</summary>
    public void SetAirborne(bool airborne)
    {
        _airborne = airborne;
        if (!airborne)
            _hitTargets.Clear();
    }

    private void Start()
    {
        _stats = GetComponent<MonsterStats>();

        // 给所有关节（含根节点自身）挂碰撞转发器，判重防重复添加
        var joints = GetComponent<RagdollJoints>();
        if (joints != null && joints.Joints != null)
        {
            foreach (var rb in joints.Joints)
            {
                if (rb == null) continue;
                AddReporter(rb.gameObject);
            }
        }
        AddReporter(gameObject);
    }

    private void AddReporter(GameObject go)
    {
        if (go.GetComponent<Collider2D>() == null) return;
        if (go.GetComponent<ImpactReporter>() != null) return;

        var reporter = go.AddComponent<ImpactReporter>();
        reporter.Init(ReportImpact);
    }

    //============================================================
    // 撞击结算
    //============================================================

    private void ReportImpact(Collision2D collision)
    {
        if (!_airborne) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed <= impactMinSpeed) return;

        float damage = (speed - impactMinSpeed) * damagePerSpeed;

        // 对方是否为另一只怪物（注意排除自己身体部位间的碰撞）
        var otherRb = collision.collider != null ? collision.collider.attachedRigidbody : null;
        var otherStats = otherRb != null ? otherRb.GetComponentInParent<MonsterStats>() : null;

        if (otherStats != null)
        {
            // 自己布娃娃部位间的自碰：不算任何撞击
            if (otherStats == _stats) return;

            // 一对怪在去重时间内只结算一次（防两只飞行怪互撞时双方各扣两次）
            var pairKey = PairKey(_stats, otherStats);
            float now = Time.time;
            if (s_recentPairs.TryGetValue(pairKey, out float lastTime) && now - lastTime < PairDedupTime)
                return;
            if (_stats != null) s_recentPairs[pairKey] = now;

            // 撞怪：双方都扣，一次飞行对同一目标只结算一次
            if (_hitTargets.Add(otherStats))
            {
                otherStats.TakeDamage(damage);
                if (_stats != null) _stats.TakeDamage(damage);
            }
        }
        else
        {
            // 撞环境（地板/墙/阻挡）：碰撞瞬间立即扣血（闪白即时反馈）；
            // 冷却窗口内的后续碰撞丢弃，防止弹跳着地连扣
            if (Time.time - _lastEnvHitTime < mergeWindow) return;
            _lastEnvHitTime = Time.time;

            if (_stats != null)
                _stats.TakeDamage(damage);
        }
    }

    /// <summary>构造两只怪的有序去重键（GetInstanceID 只存 int，不持有引用，无泄漏）</summary>
    private static (int, int) PairKey(MonsterStats a, MonsterStats b)
    {
        int ia = a ? a.GetInstanceID() : 0;
        int ib = b ? b.GetInstanceID() : 0;
        return ia < ib ? (ia, ib) : (ib, ia);
    }
}
