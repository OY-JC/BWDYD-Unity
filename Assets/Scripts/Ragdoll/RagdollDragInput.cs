using UnityEngine;

/// <summary>
/// 鼠标拖拽输入：点击命中检测 → 抓住关节 → 跟随鼠标 → 松开
/// 内部管理 TargetJoint2D 的创建与销毁
/// 不关心状态机，只暴露 TryBeginDrag / FollowMouse / EndDrag
/// </summary>
public class RagdollDragInput : MonoBehaviour
{
    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollJoints  joints;
    [SerializeField] private RagdollProfile profile;

    //----------------------------
    // 运行时状态
    //----------------------------
    private TargetJoint2D _dragJoint;
    private Rigidbody2D  _dragLimbRb;

    /// <summary>当前是否正在拖拽</summary>
    public bool IsDragging => _dragJoint != null;

    /// <summary>当前被抓住的关节刚体</summary>
    public Rigidbody2D DraggedLimb => _dragLimbRb;

    //============================================================
    // 拖拽流程
    //============================================================

    /// <summary>
    /// 尝试开始拖拽：检测鼠标点击是否命中关节
    /// 成功返回 true，并在该关节上创建 TargetJoint2D
    /// </summary>
    public bool TryBeginDrag()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld)
                       ?? Physics2D.OverlapCircle(mouseWorld, profile.touchRadius);

        if (hit == null) return false;

        // 找命中的肢体对应的关节
        _dragLimbRb = null;
        if (hit.attachedRigidbody != null)
        {
            foreach (var j in joints.Joints)
            {
                if (j == hit.attachedRigidbody)
                {
                    _dragLimbRb = j;
                    break;
                }
            }
        }
        if (_dragLimbRb == null) return false;

        // 在被抓住的关节上动态添加 TargetJoint2D
        _dragJoint = _dragLimbRb.gameObject.AddComponent<TargetJoint2D>();
        _dragJoint.autoConfigureTarget = false;
        _dragJoint.anchor = _dragLimbRb.transform.InverseTransformPoint(mouseWorld);
        _dragJoint.target = mouseWorld;
        _dragJoint.frequency     = profile.dragFrequency;
        _dragJoint.dampingRatio = profile.dragDamping;
        _dragJoint.maxForce      = profile.maxForceFactor * _dragLimbRb.mass;
        return true;
    }

    /// <summary>拖拽中：让 TargetJoint 跟随鼠标</summary>
    public void FollowMouse()
    {
        if (_dragJoint != null)
            _dragJoint.target = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }

    /// <summary>结束拖拽：销毁 TargetJoint2D（不切换状态）</summary>
    public void EndDrag()
    {
        if (_dragJoint != null)
        {
            Destroy(_dragJoint);
            _dragJoint = null;
        }
        _dragLimbRb = null;
    }
}
