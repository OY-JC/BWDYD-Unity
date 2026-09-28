using UnityEngine;

/// <summary>
/// 布娃娃模式切换：动画模式（Kinematic + Animator） ↔ 拖拽模式（Dynamic）
/// 同时负责：Bind Pose 重置、重心归位、Animator 参数同步
/// </summary>
public class RagdollModeController : MonoBehaviour
{
    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollJoints joints;
    [SerializeField] private Animator animator;

    /// <summary>当前是否处于动画模式（false 表示拖拽模式）</summary>
    public bool IsAnimationMode { get; private set; } = true;

    //============================================================
    // 模式切换
    //============================================================

    /// <summary>
    /// 进入动画模式：关节设为 Kinematic，重置到 Bind Pose，开启 Animator
    /// </summary>
    public void EnterAnimationMode()
    {
        IsAnimationMode = true;

        var rbArr = joints.Joints;
        var poses = joints.BindPoses;

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
            var pelvis = joints.PelvisRb;
            if (pelvis != null)
            {
                float z = pelvis.rotation;
                // 确保 Z 旋转在 [-90, 270) 范围内
                while (z > 270f) z -= 360f;
                while (z < -90f) z += 360f;
                animator.SetFloat("Rise_Look", z);
                Debug.Log($"Rise_Look: {z}");
            }
            animator.enabled = true;
            //强制播放动画
            animator.Play("Rise_BT");
        }
    }

    /// <summary>
    /// 进入拖拽模式：关闭 Animator，所有关节设为 Dynamic
    /// </summary>
    public void EnterDragMode()
    {
        IsAnimationMode = false;
        if (animator != null) animator.enabled = false;
        foreach (var rb in joints.Joints)
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
    }

    //============================================================
    // 归位
    //============================================================

    /// <summary>
    /// 将根节点瞬移到所有关节重心
    /// 避免动画把怪物"拉回"初始位置
    /// </summary>
    public void MoveRootToCentroid(Transform root)
    {
        var rbArr = joints.Joints;
        if (rbArr == null || rbArr.Length == 0) return;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var rb in rbArr)
        {
            if (rb == null) continue;
            sum += rb.transform.position;
            count++;
        }
        if (count > 0)
            root.position = sum / count;
    }
}
