using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物生成器：读取 Inspector 内嵌配置生成怪物
/// 流程：按权重选种类 → 按概率选体型变体 → 随机选地板 → 实例化并绑定
/// 怪物只与被分配的地板碰撞，忽略其他地板；怪物之间互不碰撞（RagdollGroundBinding）
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("生成数量")]
    [Tooltip("生成怪物总数（按各类型权重随机分配）")]
    [Range(1, 100)]
    [SerializeField] private int spawnCount = 20;

    [Tooltip("每只怪物的生成间隔（秒），在[minInterval, maxInterval]区间内随机")]
    [Range(0f, 5f)]
    [SerializeField] private float minInterval = 0.3f;

    [Tooltip("生成间隔上限（秒），必须大于等于 minInterval")]
    [Range(0f, 10f)]
    [SerializeField] private float maxInterval = 1f;

    [Tooltip("怪物根节点距离地板顶面的基础高度（实际高度 = 基础高度 × 体型倍率）")]
    [Range(0f, 5f)]
    [SerializeField] private float rootHeightAboveFloor = 1f;

    //----------------------------
    // 配置（内嵌列表，直接在 Inspector 编辑）
    //----------------------------
    [Header("怪物配置（可加多种，当前只配一种即可）")]
    [SerializeField] private List<MonsterConfig> monsterTypes = new List<MonsterConfig>();

    [Header("地板节点（每个需带 GroundPlatform 组件）")]
    [SerializeField] private List<GroundPlatform> floors = new List<GroundPlatform>();

    //============================================================
    // 生命周期
    //============================================================
    private int _spawned = 0;

    private void Start()
    {
        if (minInterval <= 0f && maxInterval <= 0f)
        {
            for (int i = 0; i < spawnCount; i++) SpawnOne();
        }
        else
        {
            _spawned = 0;
            Invoke(nameof(ScheduleSpawn), Random.Range(minInterval, maxInterval));
        }
    }

    private void ScheduleSpawn()
    {
        if (_spawned >= spawnCount) return;

        SpawnOne();
        _spawned++;

        // 下一只在 [minInterval, maxInterval] 内随机延迟后生成
        Invoke(nameof(ScheduleSpawn), Random.Range(minInterval, maxInterval));
    }

    //============================================================
    // 生成单只怪物
    //============================================================
    private void SpawnOne()
    {
        if (floors == null || floors.Count == 0)
        {
            Debug.LogWarning($"[{nameof(MonsterSpawner)}] 地板列表为空", this);
            return;
        }

        // 1. 按权重选怪物种类
        MonsterConfig config = PickWeightedType();
        if (config == null || config.prefab == null)
        {
            Debug.LogWarning($"[{nameof(MonsterSpawner)}] 缺少有效怪物配置或未拖入预制体", this);
            return;
        }

        // 2. 按概率选体型变体（无配置时用 1 倍兜底）
        SizeVariant variant = PickVariant(config);

        // 3. 随机选地板（x 坐标固定为生成器节点的 x）
        GroundPlatform floor = floors[Random.Range(0, floors.Count)];
        if (floor == null || floor.Colliders.Count == 0) return;

        // 站立高度随体型缩放，保证不同体型脚都贴在地板上
        float y = floor.TopY + rootHeightAboveFloor * variant.scaleMultiplier;
        float x = transform.position.x;

        // 4. 实例化 → 缩放 → 绑定地板 → 写入属性
        RagdollGroundBinding monster = Instantiate(
            config.prefab, new Vector3(x, y, 0f), Quaternion.identity, transform);

        monster.transform.localScale *= variant.scaleMultiplier;
        monster.Bind(floor);

        MonsterStats stats = monster.GetComponent<MonsterStats>();
        if (stats == null) stats = monster.gameObject.AddComponent<MonsterStats>();
        stats.Init(config, variant);

        // 日志：输出生成信息，方便确认生成成功
        Debug.Log(
            $"[生成] {config.displayName}({variant.label}) " +
            $"地板:{floor.name} 位置:({x:F1},{y:F1}) " +
            $"体型×{variant.scaleMultiplier} HP:{stats.MaxHP:F0} " +
            $"速度:{stats.MoveSpeed:F1}", this);
    }

    //============================================================
    // 权重抽取
    //============================================================

    /// <summary>按 weight 加权随机选一种怪物；总权重为 0 时返回 null</summary>
    private MonsterConfig PickWeightedType()
    {
        int total = 0;
        foreach (var c in monsterTypes)
            if (c != null) total += Mathf.Max(0, c.weight);
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        int acc = 0;
        foreach (var c in monsterTypes)
        {
            if (c == null) continue;
            acc += Mathf.Max(0, c.weight);
            if (roll < acc) return c;
        }
        return null;
    }

    /// <summary>按 probability 加权随机选一个体型变体；列表为空时返回 1 倍兜底变体</summary>
    private SizeVariant PickVariant(MonsterConfig config)
    {
        var list = config.variants;
        if (list == null || list.Count == 0)
            return FallbackVariant;

        float total = 0f;
        foreach (var v in list)
            if (v != null) total += Mathf.Max(0f, v.probability);
        if (total <= 0f) return list[0] ?? FallbackVariant;

        float roll = Random.Range(0f, total);
        float acc = 0f;
        foreach (var v in list)
        {
            if (v == null) continue;
            acc += Mathf.Max(0f, v.probability);
            if (roll < acc) return v;
        }
        return list[0] ?? FallbackVariant;
    }

    /// <summary>无体型配置时的兜底变体（普通 1 倍）</summary>
    private static readonly SizeVariant FallbackVariant = new SizeVariant
    {
        label = "普通",
        probability = 100f,
        scaleMultiplier = 1f,
        hpMultiplier = 1f,
        speedMultiplier = 1f,
    };

    //============================================================
    // 编辑器：首次挂载组件时自动填入示例配置（奶蛙：普通/巨型/小型）
    //============================================================
#if UNITY_EDITOR
    private void Reset()
    {
        spawnCount = 20;
        minInterval = 0.3f;
        maxInterval = 1f;
        rootHeightAboveFloor = 1f;

        monsterTypes = new List<MonsterConfig>
        {
            new MonsterConfig
            {
                displayName = "奶蛙",
                weight = 100,
                baseHP = 100f,
                baseAttack = 10f,
                baseMoveSpeed = 3f,
                variants = new List<SizeVariant>
                {
                    new SizeVariant { label = "普通", probability = 70f, scaleMultiplier = 1f,
                                      hpMultiplier = 1f,   speedMultiplier = 1f },
                    new SizeVariant { label = "巨型", probability = 15f, scaleMultiplier = 2f,
                                      hpMultiplier = 3f,   speedMultiplier = 0.6f },
                    new SizeVariant { label = "小型", probability = 15f, scaleMultiplier = 0.5f,
                                      hpMultiplier = 0.5f, speedMultiplier = 1.6f },
                },
            },
        };
    }
#endif
}
