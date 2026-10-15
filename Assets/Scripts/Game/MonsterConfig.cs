using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物种类配置（内嵌在生成器的列表中，无需单独创建资产）
/// 一种怪物 = 预制体 + 基础属性 + 生成权重 + 若干体型变体
/// </summary>
[Serializable]
public class MonsterConfig
{
    //----------------------------
    // 基础信息
    //----------------------------
    [Tooltip("种类名称，仅用于识别")]
    public string displayName = "奶蛙";

    [Tooltip("怪物预制体（根节点需带 RagdollGroundBinding）")]
    public RagdollGroundBinding prefab;

    [Tooltip("生成权重：多种怪物时按权重随机抽取，当前只有一种填 100 即可")]
    [Min(0)]
    public int weight = 100;

    //----------------------------
    // 基础属性（普通体型的数值）
    //----------------------------
    [Tooltip("基础生命值（巨型/小型按变体倍率缩放）")]
    [Min(1f)]
    public float baseHP = 100f;

    [Tooltip("基础攻击力（不随体型变化）")]
    [Min(0f)]
    public float baseAttack = 10f;

    [Tooltip("基础移动速度（巨型/小型按变体倍率缩放，供后续移动逻辑读取）")]
    [Min(0f)]
    public float baseMoveSpeed = 1f;

    //----------------------------
    // 体型变体（普通/巨型/小型……概率之和建议为 100）
    //----------------------------
    [Tooltip("体型变体列表：按概率随机出现，如普通70%、巨型15%、小型15%")]
    public List<SizeVariant> variants = new List<SizeVariant>();
}

/// <summary>
/// 体型变体配置：决定一只怪物生成时的概率与缩放倍率
/// 例：巨型 scale=2 / hp=3 / speed=0.6；小型 scale=0.5 / hp=0.5 / speed=1.6
/// </summary>
[Serializable]
public class SizeVariant
{
    [Tooltip("变体名称，仅用于识别（普通/巨型/小型）")]
    public string label = "普通";

    [Tooltip("出现概率（0~100，所有变体之和建议为 100；不为 100 时按相对比例归一化）")]
    [Range(0f, 100f)]
    public float probability = 100f;

    [Tooltip("体型缩放：整体 localScale 倍率（2=巨型两倍大，0.5=小型一半）")]
    [Min(0.1f)]
    public float scaleMultiplier = 1f;

    [Tooltip("血量倍率：最终血量 = 基础血量 × 此值（巨型建议 >1，小型建议 <1）")]
    [Min(0.1f)]
    public float hpMultiplier = 1f;

    [Tooltip("移速倍率：最终移速 = 基础移速 × 此值（巨型建议 <1，小型建议 >1）")]
    [Min(0.1f)]
    public float speedMultiplier = 1f;
}
