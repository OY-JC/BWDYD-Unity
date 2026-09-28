using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 2D拖拽控制：点击检测碰撞体，拖拽跟随，松手投掷
/// 遍历16个绑定的关节 Rigidbody2D 切换运动学状态
/// </summary>
public class Drag2D : MonoBehaviour
{
    //----------------------------
    // 可调参数
    //----------------------------
    [Header("点击检测")]
    [SerializeField] private float touchRadius = 0.5f;

    [Header("拖拽弹簧")]
    [SerializeField] private float dragFrequency = 6f;
    [SerializeField] private float dragDamping   = 0.7f;
    [SerializeField] private float maxForceFactor = 1000f;

    [Header("落地恢复")]
    [SerializeField] private int groundLayers = (1 << 6);
    [SerializeField] private float stableThreshold = 0.3f;
    [SerializeField] private float stableDuration  = 2f;

    //----------------------------
    // 引用字段
    //----------------------------
    [SerializeField] private Animator animator;

    // 16个可拖拽关节部位 → 各自对应的 Rigidbody2D
    [Header("关节绑定（按从下到上、从右到左排序）")]
    [SerializeField] private Rigidbody2D joint_01_骨盆;
    [SerializeField] private Rigidbody2D joint_02_胸口;
    [SerializeField] private Rigidbody2D joint_03_肩膀;
    [SerializeField] private Rigidbody2D joint_04_头部;
    [SerializeField] private Rigidbody2D joint_05_右大臂;
    [SerializeField] private Rigidbody2D joint_06_右小臂;
    [SerializeField] private Rigidbody2D joint_07_右手掌;
    [SerializeField] private Rigidbody2D joint_08_左大臂;
    [SerializeField] private Rigidbody2D joint_09_左小臂;
    [SerializeField] private Rigidbody2D joint_10_左手掌;
    [SerializeField] private Rigidbody2D joint_11_右大腿;
    [SerializeField] private Rigidbody2D joint_12_右小腿;
    [SerializeField] private Rigidbody2D joint_13_右脚掌;
    [SerializeField] private Rigidbody2D joint_14_左大腿;
    [SerializeField] private Rigidbody2D joint_15_左小腿;
    [SerializeField] private Rigidbody2D joint_16_左脚掌;

    //----------------------------
    // 状态字段
    //----------------------------
    private enum State { Idle, Dragging, WaitingToIdle }
    private State state;

    private TargetJoint2D dragJoint;
    private Rigidbody2D   dragLimbRb;
    private bool          isGrounded;
    private float         stableTimer;
    private Rigidbody2D[] joints;
    // 落地检测点：取所有关节的中心位置
    private Vector2 groundingPos;

    // 保存每个关节的初始本地位置（Bind Pose）
    private Vector3[] _bindPoses;

    //----------------------------
    // 关节连接关系（邻居列表）
    // 索引对应 joints 数组的下标
    //----------------------------
    [Header("调试：关节连接关系")]
    [SerializeField] private int[] joint_connections = new int[] {
        // 0:骨盆  -> 胸口
        1,
        // 1:胸口  -> 肩膀
        2,
        // 2:肩膀  -> 头部, 左大臂, 右大臂
        3, 7, 4,
        // 3:头部  -> (无)
        -1,
        // 4:右大臂 -> 右小臂
        5,
        // 5:右小臂 -> 右手掌
        6,
        // 6:右手掌 -> (无)
        -1,
        // 7:左大臂 -> 左小臂
        8,
        // 8:左小臂 -> 左手掌
        9,
        // 9:左手掌 -> (无)
        -1,
        // 10:右大腿 -> 右小腿
        11,
        // 11:右小腿 -> 右脚掌
        12,
        // 12:右脚掌 -> (无)
        -1,
        // 13:左大腿 -> 左小腿
        14,
        // 14:左小腿 -> 左脚掌
        15,
        // 15:左脚掌 -> (无)
        -1,
    };
    private float[] _prevDistances;

    private void Awake()
    {
        // 构建关节数组（只含非空的绑定）
        joints = new[]
        {
            joint_01_骨盆, joint_02_胸口, joint_03_肩膀, joint_04_头部,
            joint_05_右大臂, joint_06_右小臂, joint_07_右手掌,
            joint_08_左大臂, joint_09_左小臂, joint_10_左手掌,
            joint_11_右大腿, joint_12_右小腿, joint_13_右脚掌,
            joint_14_左大腿, joint_15_左小腿, joint_16_左脚掌,
        };
        var validJoints = new System.Collections.Generic.List<Rigidbody2D>();
        foreach (var j in joints) if (j != null) validJoints.Add(j);
        joints = validJoints.ToArray();

        // 初始化距离数组和 Bind Pose
        _prevDistances = new float[joints.Length];
        _bindPoses = new Vector3[joints.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null)
            {
                _prevDistances[i] = Vector2.Distance(joints[i].position, joints[i].transform.position);
                _bindPoses[i] = joints[i].transform.localPosition; // 保存初始本地位置
            }
        }

        SetAnimationMode();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle:
                if (Input.GetMouseButtonDown(0)) TryBeginDrag();
                break;
            case State.Dragging:
                if (Input.GetMouseButton(0))
                    FollowMouse();
                else
                    EndDrag();
                break;
            case State.WaitingToIdle:
                CheckStable();
                break;
        }

        // 落地检测：取所有关节的最低点作为检测位置
        if (joints.Length > 0)
        {
            float lowestY = float.MaxValue;
            foreach (var rb in joints)
                if (rb != null && rb.position.y < lowestY)
                    lowestY = rb.position.y;
            groundingPos = new Vector2(transform.position.x, lowestY);
            isGrounded = Physics2D.OverlapCircle(groundingPos, 0.8f, groundLayers);
        }


    }

  


    private int[] GetNeighborIndices(int idx)
    {
        if (idx < 0 || idx >= joint_connections.Length) return new int[0];

        // 收集直接连接的邻居
        var list = new System.Collections.Generic.List<int>();
        foreach (int n in joint_connections)
            if (n >= 0) list.Add(n);

        // 同时收集反向连接（谁连向了我）
        for (int i = 0; i < joint_connections.Length; i++)
        {
            foreach (int n in joint_connections)
            {
                if (n == idx && i != idx)
                    list.Add(i);
            }
        }

        // 去重
        var unique = new System.Collections.Generic.List<int>(new System.Collections.Generic.HashSet<int>(list));
        return unique.ToArray();
    }

    private string GetName(int idx)
    {
        if (idx < 0 || idx >= joints.Length) return "?";
        return idx switch
        {
            0  => "骨盆",   1  => "胸口",   2  => "肩膀",   3  => "头部",
            4  => "右大臂", 5  => "右小臂", 6  => "右手掌",
            7  => "左大臂", 8  => "左小臂", 9  => "左手掌",
            10 => "右大腿", 11 => "右小腿", 12 => "右脚掌",
            13 => "左大腿", 14 => "左小腿", 15 => "左脚掌",
            _  => $"joint_{idx}",
        };
    }

    //============================================================
    // 模式切换：遍历所有绑定关节切换刚体类型
    //============================================================

    private void SetAnimationMode()
    {
        state = State.Idle;
        
        // ★ 先重置所有关节的本地位置到 Bind Pose，再开动画
        // 这样 Animator 播动画时从正确位置开始，不会出现偏移
        foreach (var rb in joints)
        {
            if (rb == null) continue;
            int idx = System.Array.IndexOf(joints, rb);
            if (idx >= 0 && idx < _bindPoses.Length)
                rb.transform.localPosition = _bindPoses[idx];
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        
        if (animator != null)
        {
            // 读取骨盆的 Z 旋转，赋值给动画参数 Rise_Look
            if (joint_01_骨盆 != null){
                float z = joint_01_骨盆.rotation;
                if (z > 270f) z -= 360f;
                animator.SetFloat("Rise_Look", z);
                Debug.Log($"Rise_Look: {z}");
            }
            animator.enabled = true;
            
        }
           }

    private void EnterDragMode()
    {
        if (animator != null) animator.enabled = false;
        foreach (var rb in joints)
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
    }

    //============================================================
    // 拖拽流程
    //============================================================

    private void TryBeginDrag()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld)
                       ?? Physics2D.OverlapCircle(mouseWorld, touchRadius);

        if (hit == null) return;

        // 找到命中的肢体对应的绑定关节
        dragLimbRb = null;
        if (hit.attachedRigidbody != null)
        {
            foreach (var j in joints)
            {
                if (j == hit.attachedRigidbody)
                {
                    dragLimbRb = j;
                    break;
                }
            }
        }
        if (dragLimbRb == null) return;

        EnterDragMode();
        state = State.Dragging;

        // 在被抓肢体上动态添加 TargetJoint2D
        dragJoint = dragLimbRb.gameObject.AddComponent<TargetJoint2D>();
        dragJoint.autoConfigureTarget = false;
        dragJoint.anchor = dragLimbRb.transform.InverseTransformPoint(mouseWorld);
        dragJoint.target = mouseWorld;
        dragJoint.frequency = dragFrequency;
        dragJoint.dampingRatio = dragDamping;
        dragJoint.maxForce = maxForceFactor * dragLimbRb.mass;
    }

    private void FollowMouse()
    {
        dragJoint.target = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }

    private void EndDrag()
    {
        if (dragJoint != null)
        {
            Destroy(dragJoint);
            dragJoint = null;
        }

        state = State.WaitingToIdle;
        stableTimer = 0f;
    }

    //============================================================
    // 等待归位
    //============================================================

    private void CheckStable()
    {
        stableTimer += Time.deltaTime;

        if (!isGrounded)
        {
            stableTimer = 0f;
        }
        else
        {
            // 所有绑定关节速度都低于阈值才算平稳
            bool allStable = true;
            foreach (var rb in joints)
            {
                if (rb != null && rb.velocity.magnitude > stableThreshold)
                {
                    allStable = false;
                    break;
                }
            }

            if (allStable && stableTimer >= stableDuration)
                ResetDrag();
        }
    }

    private void ResetDrag()
    {
        // 把根节点瞬移到所有关节重心，避免动画把怪物"拉回"初始位置
        if (joints.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var rb in joints)
            {
                if (rb != null) { sum += rb.transform.position; count++; }
            }
            if (count > 0)
                transform.position = sum / count;
        }
        SetAnimationMode();
    }
}
