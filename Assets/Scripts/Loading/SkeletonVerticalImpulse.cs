using UnityEngine;

/// <summary>
/// 骨骼静止检测 + 竖直速度施加：
/// 挂载物体上的 12 根骨骼速度全部低于阈值并持续 stableDuration 秒后，
/// 给所有骨骼施加一个向上(+Y)的竖直速度，循环触发。
/// 骨骼引用需在 Inspector 手动拖拽。
/// </summary>
public class SkeletonVerticalImpulse : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("触发条件")]
    [Tooltip("骨骼速度低于此值视为趋于静止")]
    [Range(0.01f, 2f)]
    [SerializeField] private float stableThreshold = 0.3f;

    [Tooltip("持续静止多久才触发（秒）")]
    [Range(0.1f, 5f)]
    [SerializeField] private float stableDuration = 1f;

    [Header("施加的竖直速度")]
    [Tooltip("触发后给骨骼施加的向上速度大小")]
    [Range(1f, 30f)]
    [SerializeField] private float verticalVelocity = 8f;

    [Header("冷却")]
    [Tooltip("两次触发之间的最小间隔（秒），防止抖动")]
    [Range(0f, 5f)]
    [SerializeField] private float cooldown = 0.5f;

    //----------------------------
    // 引用
    //----------------------------
    [Header("骨骼引用（手动拖拽，12 根）")]
    [SerializeField] private Rigidbody2D[] bones = new Rigidbody2D[12];

    //----------------------------
    // 运行时状态
    //----------------------------
    private float _stableTimer;
    private float _cooldownTimer;

    //============================================================
    // 静止检测循环
    //============================================================
    private void Update()
    {
        // 冷却中不检测
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            _stableTimer = 0f;
            return;
        }

        // 所有骨骼速度都低于阈值才算趋于静止
        bool allStable = false;
        if (bones != null && bones.Length > 0)
        {
            allStable = true;
            foreach (var rb in bones)
            {
                if (rb == null) continue;
                if (rb.velocity.magnitude > stableThreshold)
                {
                    allStable = false;
                    break;
                }
            }
        }

        if (allStable)
        {
            _stableTimer += Time.deltaTime;
            if (_stableTimer >= stableDuration)
            {
                ApplyVerticalVelocity();
                _stableTimer = 0f;
                _cooldownTimer = cooldown;
            }
        }
        else
        {
            _stableTimer = 0f;
        }
    }

    //============================================================
    // 给所有骨骼施加向上的竖直速度（保留水平速度）
    //============================================================
    private void ApplyVerticalVelocity()
    {
        if (bones == null) return;
        foreach (var rb in bones)
        {
            if (rb == null) continue;
            rb.velocity = new Vector2(rb.velocity.x, verticalVelocity);
        }
    }
}
