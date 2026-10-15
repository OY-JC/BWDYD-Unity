using UnityEngine;

/// <summary>
/// 布娃娃主控：状态机协调器，作为整个布娃娃系统的入口
/// 协调 RagdollJoints / ModeController / DragInput / StabilitySensor
///
/// 生命周期：
/// 生成 → WaitingToStable(物理掉落等平稳) → Rising(爬起) → Standing(站立1秒)
///      → Running(行走到攻击区域) → Attacking(停走，每秒攻击)
/// 起身后若已在攻击区域内则直接进入 Attacking；任意状态可被鼠标拖拽打断
/// → Dragging → 松手回到 WaitingToStable → 重新爬起
/// </summary>
[RequireComponent(typeof(RagdollJoints))]
[RequireComponent(typeof(RagdollModeController))]
[RequireComponent(typeof(RagdollDragInput))]
[RequireComponent(typeof(RagdollStabilitySensor))]
public class Ragdoll : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("起身后")]
    [Tooltip("进入站立(Idel)动画后，多少秒切换到奔跑(Run)动画")]
    [Range(0f, 10f)]
    [SerializeField] private float standDuration = 1f;

    [Tooltip("播爬起动画后多少秒才开始检测站立状态，防止动画切换首帧误判")]
    [Range(0f, 1f)]
    [SerializeField] private float riseCheckDelay = 0.2f;

    [Tooltip("爬起时根节点 Y 偏移量：最终 Y = 绑定地板Y + 绑定地板Y缩放/2 + (此值 × ScaleMultiplier)")]
    [Range(0f, 3f)]
    [SerializeField] private float standYOffset = 1f;

    [Header("攻击区域")]
    [Tooltip("可攻击区域 X 下界（小于此值有物理阻挡，正常不会到达）")]
    [SerializeField] private float attackZoneMinX = -5f;

    [Tooltip("可攻击区域 X 上界：走到此位置停走开始攻击")]
    [SerializeField] private float attackZoneMaxX = -3f;

    [Tooltip("攻击间隔（秒）：进入区域后每隔此时长触发一次攻击，第一次攻击也在此时长后")]
    [Range(0.1f, 5f)]
    [SerializeField] private float attackInterval = 1f;

    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollProfile profile;

    private RagdollJoints           _joints;
    private RagdollModeController   _mode;
    private RagdollDragInput        _drag;
    private RagdollStabilitySensor  _sensor;
    private RagdollGroundBinding    _ground;
    private MonsterImpactDamage     _impact;

    //----------------------------
    // 状态机
    //----------------------------
    private enum State { WaitingToStable, Rising, Standing, Running, Attacking, Dragging }
    private State _state = State.WaitingToStable;
    private float _stateTimer;

    /// <summary>Animator 中的站立状态名（控制器内拼写为 Idel）</summary>
    private const string AnimIdle = "Idel";

    private void Awake()
    {
        _joints = GetComponent<RagdollJoints>();
        _mode   = GetComponent<RagdollModeController>();
        _drag   = GetComponent<RagdollDragInput>();
        _sensor = GetComponent<RagdollStabilitySensor>();
        _ground = GetComponent<RagdollGroundBinding>();
        _impact = GetComponent<MonsterImpactDamage>();

        // 初始即物理布娃娃状态（与松手后一致），生成后自然掉落
        _mode.EnterPhysicsMode();

        // 生成掉落也处于飞行伤害窗口内，平稳后自动关闭
        if (_impact != null) _impact.SetAirborne(true);
    }

    private void Update()
    {
        switch (_state)
        {
            //----------------------------
            // 等待平稳（生成初始态 = 松手后）：可随时拖拽；落地稳定后自动爬起
            //----------------------------
            case State.WaitingToStable:
                if (TryBeginDrag()) break;
                if (_sensor.IsStable)
                    BeginGetUp();
                break;

            //----------------------------
            // 爬起中：动画进入 Idel 即视为起身完成
            //----------------------------
            case State.Rising:
                if (TryBeginDrag()) break;
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= riseCheckDelay && _mode.IsInState(AnimIdle))
                {
                    _stateTimer = 0f;
                    _state = State.Standing;
                }
                break;

            //----------------------------
            // 站立：standDuration 秒后，在攻击区内直接攻击，否则触发 IsWalk 走路
            //----------------------------
            case State.Standing:
                if (TryBeginDrag()) break;
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= standDuration)
                {
                    _stateTimer = 0f;
                    if (IsInAttackZone())
                        _state = State.Attacking;
                    else
                    {
                        _mode.SetRunning(true);
                        _state = State.Running;
                    }
                }
                break;

            //----------------------------
            // 奔跑：走入攻击区域后停走，转入攻击
            //----------------------------
            case State.Running:
                if (TryBeginDrag()) break;
                if (IsInAttackZone())
                {
                    _mode.SetRunning(false);
                    _stateTimer = 0f;
                    _state = State.Attacking;
                }
                break;

            //----------------------------
            // 攻击：每隔 attackInterval 秒触发一次 IsAttack，可被拖拽打断
            //----------------------------
            case State.Attacking:
                if (TryBeginDrag()) break;
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= attackInterval)
                {
                    _stateTimer -= attackInterval;
                    Debug.Log($"[攻击] IsAttack 触发，根节点世界X={transform.position.x}");
                    _mode.SetAttack();
                }
                break;

            //----------------------------
            // 拖拽中：跟随鼠标，松手转入等待平稳
            //----------------------------
            case State.Dragging:
                if (Input.GetMouseButton(0))
                    _drag.FollowMouse();
                else
                {
                    _drag.EndDrag();
                    _sensor.ResetTimer();
                    // 松手进入飞行窗口：高速撞击（环境/怪物）开始结算
                    if (_impact != null) _impact.SetAirborne(true);
                    _state = State.WaitingToStable;
                }
                break;
        }
    }

    //============================================================
    // 流程方法
    //============================================================

    /// <summary>根节点 X 是否处于可攻击区域 [attackZoneMinX, attackZoneMaxX]</summary>
    private bool IsInAttackZone()
    {
        float x = transform.position.x;
        return x >= attackZoneMinX && x <= attackZoneMaxX;
    }

    /// <summary>鼠标按下且命中关节时开始拖拽，返回是否已转入 Dragging</summary>
    private bool TryBeginDrag()
    {
        if (!Input.GetMouseButtonDown(0) || !_drag.TryBeginDrag()) return false;

        // 幂等：物理状态下调用无副作用；动画状态下会关闭 Animator
        _mode.EnterDragMode();
        // 被拎起：关闭飞行伤害窗口（拎着时碰到东西不算撞击）
        if (_impact != null) _impact.SetAirborne(false);
        _stateTimer = 0f;
        _state = State.Dragging;
        return true;
    }

    /// <summary>落地平稳后爬起：先把根节点 Y 对齐到站立高度，再进动画模式播 Rise_BT</summary>
    private void BeginGetUp()
    {
        // 平稳起身：关闭飞行伤害窗口
        if (_impact != null) _impact.SetAirborne(false);
        AdjustRootYToFloor();
        _mode.EnterAnimationMode();   // 内部复位 IsWalk 并播放 Rise_BT
        _stateTimer = 0f;
        _state = State.Rising;
    }

    /// <summary>
    /// 把根节点 Y 对齐到站立高度：
    /// 绑定地板Y + 绑定地板Y缩放/2 + (standYOffset × ScaleMultiplier)
    /// </summary>
    private void AdjustRootYToFloor()
    {
        var floor = _ground != null ? _ground.BoundFloor : null;
        if (floor == null) return;

        float scaleMultiplier = 1f;
        var stats = GetComponent<MonsterStats>();
        if (stats != null) scaleMultiplier = stats.ScaleMultiplier;

        float floorY       = floor.transform.position.y;
        float floorScaleY  = floor.transform.lossyScale.y;
        float offsetTerm   = standYOffset * scaleMultiplier;
        float targetY      = floorY + floorScaleY / 2f + offsetTerm;

        Debug.Log(
            $"[根节点Y对齐] 地板Y={floorY} + 地板Y缩放/2={floorScaleY / 2f} " +
            $"+ standYOffset({standYOffset}) × ScaleMultiplier({scaleMultiplier}) = {offsetTerm} " +
            $"=> 最终Y={targetY}");

        var pos = transform.position;
        pos.y = targetY;
        transform.position = pos;
    }
}
