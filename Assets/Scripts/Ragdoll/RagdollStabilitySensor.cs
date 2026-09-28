using UnityEngine;

/// <summary>
/// 落地与平稳检测：
/// - IsGrounded：是否接触地面
/// - IsStable：接触地面 + 所有关节速度低于阈值 + 持续 stableDuration 秒
/// 注意：本组件在 Update 中无条件运行，由状态机决定何时重置计时器
/// </summary>
public class RagdollStabilitySensor : MonoBehaviour
{
    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollJoints  joints;
    [SerializeField] private RagdollProfile profile;

    //----------------------------
    // 运行时状态
    //----------------------------
    private float _stableTimer;

    /// <summary>当前是否接触地面</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>是否达到归位条件（落地 + 平稳 + 持续时长）</summary>
    public bool IsStable => IsGrounded && _stableTimer >= profile.stableDuration;

    /// <summary>重置平稳计时器（开始新一轮等待）</summary>
    public void ResetTimer() => _stableTimer = 0f;

    //============================================================
    // 落地检测 + 平稳计时
    //============================================================
    private void Update()
    {
        // 落地检测：取所有关节的最低点作为检测位置
        var rbArr = joints.Joints;
        IsGrounded = false;
        if (rbArr != null && rbArr.Length > 0)
        {
            float lowestY = float.MaxValue;
            foreach (var rb in rbArr)
                if (rb != null && rb.position.y < lowestY)
                    lowestY = rb.position.y;
            Vector2 groundingPos = new Vector2(transform.position.x, lowestY);
            IsGrounded = Physics2D.OverlapCircle(groundingPos, 0.8f, profile.groundLayers);
        }

        // 平稳计时：未落地直接清零
        if (!IsGrounded)
        {
            _stableTimer = 0f;
            return;
        }

        // 所有关节速度都低于阈值才算平稳
        bool allStable = true;
        foreach (var rb in rbArr)
        {
            if (rb != null && rb.velocity.magnitude > profile.stableThreshold)
            {
                allStable = false;
                break;
            }
        }

        // 平稳才累计，否则清零（修正原逻辑：原代码此时不清零会误触发归位）
        if (allStable)
            _stableTimer += Time.deltaTime;
        else
            _stableTimer = 0f;
    }
}
