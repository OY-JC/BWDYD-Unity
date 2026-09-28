using UnityEngine;

/// <summary>
/// 布娃娃主控：状态机协调器，作为整个布娃娃系统的入口
/// 协调 RagdollJoints / ModeController / DragInput / StabilitySensor
/// 状态：Idle → Dragging → WaitingToIdle → Idle
/// </summary>
[RequireComponent(typeof(RagdollJoints))]
[RequireComponent(typeof(RagdollModeController))]
[RequireComponent(typeof(RagdollDragInput))]
[RequireComponent(typeof(RagdollStabilitySensor))]
public class Ragdoll : MonoBehaviour
{
    //----------------------------
    // 引用
    //----------------------------
    [SerializeField] private RagdollProfile profile;

    private RagdollJoints          _joints;
    private RagdollModeController _mode;
    private RagdollDragInput      _drag;
    private RagdollStabilitySensor _sensor;

    //----------------------------
    // 状态机
    //----------------------------
    private enum State { Idle, Dragging, WaitingToIdle }
    private State _state = State.Idle;

    private void Awake()
    {
        _joints = GetComponent<RagdollJoints>();
        _mode   = GetComponent<RagdollModeController>();
        _drag   = GetComponent<RagdollDragInput>();
        _sensor = GetComponent<RagdollStabilitySensor>();

        // 启动时进入动画模式
        _mode.EnterAnimationMode();
    }

    private void Update()
    {
        switch (_state)
        {
            //----------------------------
            // 空闲：等待鼠标按下并命中关节
            //----------------------------
            case State.Idle:
                if (Input.GetMouseButtonDown(0) && _drag.TryBeginDrag())
                {
                    _mode.EnterDragMode();
                    _state = State.Dragging;
                }
                break;

            //----------------------------
            // 拖拽中：跟随鼠标，松手转入等待归位
            //----------------------------
            case State.Dragging:
                if (Input.GetMouseButton(0))
                    _drag.FollowMouse();
                else
                {
                    _drag.EndDrag();
                    _sensor.ResetTimer();
                    _state = State.WaitingToIdle;
                }
                break;

            //----------------------------
            // 等待归位：检测平稳 → 重置 → 回到动画模式
            //----------------------------
            case State.WaitingToIdle:
                if (_sensor.IsStable)
                    ResetDrag();
                break;
        }
    }

    //============================================================
    // 归位：将根节点瞬移到关节重心，再切回动画模式
    //============================================================
    private void ResetDrag()
    {
        _mode.MoveRootToCentroid(transform);
        _mode.EnterAnimationMode();
        _state = State.Idle;
    }
}
