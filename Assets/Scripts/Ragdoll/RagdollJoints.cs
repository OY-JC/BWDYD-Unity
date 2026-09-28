using UnityEngine;

/// <summary>
/// 布娃娃关节绑定与连接关系
/// 职责：缓存 16 个关节引用、保存 Bind Pose、提供调试辅助方法
/// 不含任何运行时逻辑，纯数据层
/// </summary>
public class RagdollJoints : MonoBehaviour
{
    //----------------------------
    // 关节绑定（按从下到上、从右到左排序）
    //----------------------------
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
    // 调试：关节连接关系（邻居列表）
    // 索引对应 16 个固定关节编号
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

    //----------------------------
    // 运行时缓存
    //----------------------------
    private Rigidbody2D[] _joints;
    private Vector3[]      _bindPoses;

    /// <summary>过滤空绑定后的关节引用数组（懒加载，首次访问时初始化）</summary>
    public Rigidbody2D[] Joints
    {
        get
        {
            if (_joints == null) Initialize();
            return _joints;
        }
    }

    /// <summary>每个关节对应的初始本地位置（Bind Pose）</summary>
    public Vector3[] BindPoses
    {
        get
        {
            if (_bindPoses == null) Initialize();
            return _bindPoses;
        }
    }

    /// <summary>骨盆刚体（用于读取 Rise_Look 旋转参数）</summary>
    public Rigidbody2D PelvisRb => joint_01_骨盆;

    /// <summary>初始化关节缓存与 Bind Pose（懒加载，供 Joints/BindPoses 属性调用）</summary>
    private void Initialize()
    {
        // 构建关节数组（过滤空绑定）
        var all = new[] {
            joint_01_骨盆, joint_02_胸口, joint_03_肩膀, joint_04_头部,
            joint_05_右大臂, joint_06_右小臂, joint_07_右手掌,
            joint_08_左大臂, joint_09_左小臂, joint_10_左手掌,
            joint_11_右大腿, joint_12_右小腿, joint_13_右脚掌,
            joint_14_左大腿, joint_15_左小腿, joint_16_左脚掌,
        };
        var valid = new System.Collections.Generic.List<Rigidbody2D>();
        foreach (var j in all) if (j != null) valid.Add(j);
        _joints = valid.ToArray();

        // 保存 Bind Pose（初始本地位置）
        _bindPoses = new Vector3[_joints.Length];
        for (int i = 0; i < _joints.Length; i++)
            if (_joints[i] != null)
                _bindPoses[i] = _joints[i].transform.localPosition;
    }

    //============================================================
    // 调试辅助
    //============================================================

    /// <summary>取关节编号对应的可读名</summary>
    public string GetName(int idx)
    {
        if (idx < 0 || idx >= 16) return "?";
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

    /// <summary>获取某关节的所有邻居索引（含反向连接）</summary>
    public int[] GetNeighborIndices(int idx)
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
        var unique = new System.Collections.Generic.List<int>(
            new System.Collections.Generic.HashSet<int>(list));
        return unique.ToArray();
    }
}
