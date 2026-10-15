using UnityEngine;

/// <summary>
/// 撞击转发器：挂在布娃娃各关节（带 Collider2D）上，
/// 把碰撞事件连同相对速度上报给根节点的 MonsterImpactDamage 结算。
/// 由 MonsterImpactDamage 运行时自动添加，不手动配置。
/// </summary>
public class ImpactReporter : MonoBehaviour
{
    private System.Action<Collision2D> _onImpact;

    /// <summary>注入根节点结算回调</summary>
    public void Init(System.Action<Collision2D> onImpact) => _onImpact = onImpact;

    private void OnCollisionEnter2D(Collision2D collision) => _onImpact?.Invoke(collision);
}
