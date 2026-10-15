using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 布娃娃模式切换：动画模式（Kinematic + Animator） ↔ 拖拽模式（Dynamic）
/// 同时负责：Bind Pose 重置、重心归位、Animator 参数同步
/// </summary>
public class RagdollModeController : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("奔跑移动")]
    [Tooltip("匀速基准速度（世界单位/秒）：根节点目标速度 = SpeedMultiplier × 此值，方向向左")]
    [Range(0f, 20f)]
    [SerializeField] private float runSpeedBase = 5f;

    [Tooltip("起步加速度（单位/秒²）：越大越快从 0 加速到匀速")]
    [Range(0.1f, 50f)]
    [SerializeField] private float runAcceleration = 10f;

    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollJoints joints;
    [SerializeField] private Animator animator;

    /// <summary>当前是否处于动画模式（false 表示拖拽模式）</summary>
    public bool IsAnimationMode { get; private set; } = true;

    //----------------------------
    // 奔跑驱动运行时状态
    //----------------------------
    private Rigidbody2D _rootRb;
    private Joint2D     _rootLinkJoint;
    private bool  _isWalking;
    private bool  _isDecelerating;   // 停走后的惯性减速：把水平速度减到 0
    private float _targetWalkVx;
    private float _deceleration;     // 减速率 = runAcceleration × runSpeedBase × SpeedMultiplier（停走时缓存）

    private Rigidbody2D RootRb => _rootRb != null ? _rootRb : (_rootRb = GetComponent<Rigidbody2D>());

    /// <summary>根节点与骨盆之间的连接铰链：物理模式下让根节点跟随身体，动画模式下必须禁用否则根节点被拴死无法移动</summary>
    private Joint2D RootLinkJoint =>
        _rootLinkJoint != null ? _rootLinkJoint : (_rootLinkJoint = GetComponent<Joint2D>());

    //============================================================
    // 模式切换
    //============================================================

    /// <summary>
    /// 进入动画模式：关节设为 Kinematic，重置到 Bind Pose，开启 Animator
    /// </summary>
    public void EnterAnimationMode()
    {
        IsAnimationMode = true;
        _isWalking = false;
        _isDecelerating = false;

        // 根节点被关节黏着甩动后可能残留速度，爬起时清零，防止身体（运动学子关节随父走）整体滑动
        var rootRb = RootRb;
        if (rootRb != null)
        {
            rootRb.velocity = Vector2.zero;
            rootRb.angularVelocity = 0f;
        }

        var rbArr = joints.Joints;
        var poses = joints.BindPoses;

        // 断开根节点→骨盆的铰链：动画模式下骨盆是 Kinematic（无限质量），
        // 铰链会把施加了速度的根节点瞬间拉回骨盆锚点，导致奔跑位移被完全抵消
        var linkJoint = RootLinkJoint;
        if (linkJoint != null) linkJoint.enabled = false;

        var pelvisRb = joints.PelvisRb;

        // ★ 先重置所有关节的本地位置到 Bind Pose，再开动画
        // 这样 Animator 播动画时从正确位置开始，不会出现偏移
        for (int i = 0; i < rbArr.Length; i++)
        {
            var rb = rbArr[i];
            if (rb == null) continue;
            if (i < poses.Length)
                rb.transform.localPosition = poses[i];
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (animator != null)
        {
            // 读取骨盆的 Z 旋转，赋值给动画参数 Rise_Look
            if (pelvisRb != null)
            {
                float z = pelvisRb.rotation;
                // 确保 Z 旋转在 [-90, 270) 范围内
                while (z > 270f) z -= 360f;
                while (z < -90f) z += 360f;
                animator.SetFloat("Rise_Look", z);
            }
            animator.enabled = true;
            // 复位为站立参数：Rise_BT 播完自动停在 Idel，不会转 Walk
            animator.ResetTrigger("IsWalk");
            // 清掉上一轮可能残留的攻击 Trigger，否则进入 Idel 后会立刻误触发攻击
            animator.ResetTrigger("IsAttack");
            //强制播放动画
            animator.Play("Rise_BT");
        }
    }

    /// <summary>
    /// 进入物理布娃娃模式：关闭 Animator，所有关节设为 Dynamic
    /// 怪物生成初始状态、被拖拽时均使用此模式
    /// </summary>
    public void EnterPhysicsMode()
    {
        IsAnimationMode = false;
        if (animator != null) animator.enabled = false;
        foreach (var rb in joints.Joints)
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        // 打断奔跑：停止起步加速驱动，并清零根节点水平速度，避免布娃娃带着奔跑惯性滑走
        _isWalking = false;
        _isDecelerating = false;
        var rootRb = RootRb;
        if (rootRb != null)
            rootRb.velocity = new Vector2(0f, rootRb.velocity.y);

        // 恢复根节点→骨盆的铰链，让根节点重新被物理身体黏着拖带
        var linkJoint = RootLinkJoint;
        if (linkJoint != null) linkJoint.enabled = true;
    }

    /// <summary>进入拖拽模式（等同物理布娃娃模式，保留方法名以表达语义）</summary>
    public void EnterDragMode() => EnterPhysicsMode();

    //============================================================
    // 奔跑起步加速 / 停走减速：FixedUpdate 中把水平速度推向目标（加速→匀速→减速→0）
    // 加速率 = runAcceleration；减速率 = runAcceleration × runSpeedBase × SpeedMultiplier
    //============================================================
    private void FixedUpdate()
    {
        if (!_isWalking && !_isDecelerating) return;

        var rb = RootRb;
        if (rb == null) return;

        // 走路：逼近目标匀速；减速：逼近 0。MoveTowards 逐帧线性变化
        float targetVx = _isWalking ? _targetWalkVx : 0f;
        float acc = _isWalking ? runAcceleration : _deceleration;
        float vx = Mathf.MoveTowards(
            rb.velocity.x, targetVx,
            acc * Time.fixedDeltaTime);
        rb.velocity = new Vector2(vx, rb.velocity.y);

        // 减速到 0 后结束驱动
        if (_isDecelerating && Mathf.Approximately(vx, 0f))
            _isDecelerating = false;
    }

    //============================================================
    // 动画状态辅助
    //============================================================

    /// <summary>切换奔跑/站立：running=true 触发 IsWalk 播 Walk 并开始起步加速，false 停止（通过 IsWalk Trigger）</summary>
    public void SetRunning(bool running)
    {
        if (animator == null || !animator.enabled) return;

        if (running)
        {
            animator.SetTrigger("IsWalk");

            //获取MonsterStats移动速度参数
            var stats = this.GetComponent<MonsterStats>();
            if (stats != null)
            {
                // Walk_Speed：混合树参数（0.5~2），只控制走路动画混合
                animator.SetFloat("Walk_Speed", stats.MoveSpeed);
                // 根节点物理目标速度 = SpeedMultiplier（即 MoveSpeed）× runSpeedBase，方向向左
                // 不在此瞬间赋值，交给 FixedUpdate 起步加速
                _targetWalkVx = -stats.MoveSpeed * runSpeedBase;
                _isWalking = true;
            }
        }
        else
        {
            animator.ResetTrigger("IsWalk");
            _isWalking = false;
            // 不瞬间清零：进入惯性减速，FixedUpdate 中平滑减到 0
            // （根节点无 LinearDrag，不驱动会永久匀速滑行）
            if (RootRb != null)
            {
                // 减速率 = 加速参数 × 基础速度 × 速度倍数（与当前移动速度成正比，速度越快滑行越远、减速时间恒定）
                var stats = GetComponent<MonsterStats>();
                float mult = stats != null ? stats.MoveSpeed : 1f;
                _deceleration = runAcceleration * runSpeedBase * mult;
                _isDecelerating = true;
            }
        }
    }

    /// <summary>触发一次攻击（IsAttack Trigger）：Attack 动画播完由控制器自动回到 Idel</summary>
    public void SetAttack()
    {
        if (animator != null && animator.enabled)
            animator.SetTrigger("IsAttack");
    }

    /// <summary>当前动画是否正处于指定状态（如 "Idel" / "Rise_BT" / "Run"）</summary>
    public bool IsInState(string stateName, int layer = 0)
    {
        return animator != null
            && animator.enabled
            && animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName);
    }
}
