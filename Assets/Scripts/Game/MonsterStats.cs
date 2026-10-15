using UnityEngine;

/// <summary>
/// 怪物运行时属性：生成器根据配置计算出最终数值后写入本组件
/// 预制体不需要手动挂载，生成器会自动添加
/// 后续战斗/移动逻辑统一从这里读血量、攻击力、移速
/// </summary>
public class MonsterStats : MonoBehaviour
{
    /// <summary>种类名称</summary>
    public string MonsterName { get; private set; }

    /// <summary>体型变体名称（普通/巨型/小型）</summary>
    public string VariantLabel { get; private set; }

    /// <summary>最大生命值 = 基础血量 × 变体血量倍率</summary>
    public float MaxHP { get; private set; }

    /// <summary>当前生命值</summary>
    public float CurrentHP { get; private set; }

    /// <summary>攻击力（不随体型缩放）</summary>
    public float Attack { get; private set; }

    /// <summary>最终移动速度 = 基础移速 × 变体移速倍率</summary>
    public float MoveSpeed { get; private set; }

    /// <summary>体型缩放倍率（生成器已同步到 localScale，留作逻辑读取）</summary>
    public float ScaleMultiplier { get; private set; }

    /// <summary>是否已死亡</summary>
    public bool IsDead { get; private set; }

    /// <summary>受伤事件（参数：剩余血量百分比 0~1）：受击闪白、血条、音效等表现统一订阅此处</summary>
    public event System.Action<float> OnDamaged;

    /// <summary>
    /// 初始化最终属性（仅由生成器在实例化后调用一次）
    /// </summary>
    public void Init(MonsterConfig config, SizeVariant variant)
    {
        MonsterName     = config.displayName;
        VariantLabel    = variant.label;
        ScaleMultiplier = variant.scaleMultiplier;
        MaxHP           = config.baseHP * variant.hpMultiplier;
        CurrentHP       = MaxHP;
        Attack          = config.baseAttack;
        MoveSpeed       = config.baseMoveSpeed * variant.speedMultiplier;
    }

    /// <summary>受到伤害，返回是否本次造成死亡</summary>
    public bool TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return false;

        CurrentHP = Mathf.Max(0f, CurrentHP - damage);
        OnDamaged?.Invoke(CurrentHP / MaxHP);
        if (CurrentHP <= 0f)
        {
            IsDead = true;
            // TODO: 后续接入死亡表现（动画/掉落/计分），目前先销毁
            Destroy(gameObject);
            return true;
        }
        return false;
    }
}
