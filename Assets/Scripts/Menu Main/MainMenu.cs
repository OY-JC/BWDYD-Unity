using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MainMenu : MonoBehaviour
{

    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [ Header("运动区域（圆形，世界坐标单位）") ]
    [ Tooltip("眼仁圆形区域半径，眼仁最多偏移这个距离") ]
    [ Range(-0.5f, 0.5f) ] 
    [ SerializeField ] private float radius = 0.25f;

    [ Tooltip("嘴巴圆形区域半径，偏移方向与眼睛同步、幅度按比例缩放") ]
    [ Range(-0.5f, 0.5f) ] 
    [ SerializeField ] private float mouthRadius = 0.1f;

    [ Tooltip("眼仁跟随的平滑速度，越大越跟手") ]
    [ Range(20f, 50f) ] 
    [ SerializeField ] private float smoothSpeed = 15f;

    //----------------------------
    // 引用
    //----------------------------
    [ Header("眼")]
    [ SerializeField ] private Transform rightEye;
    [ SerializeField ] private Transform leftEye;

    [ Header("嘴")]
    [ SerializeField ] private Transform mouth;


    [ Header("按钮")]
    [ SerializeField ] private Button startButton;
    [ SerializeField ] private Button settingsButton;
    [ SerializeField ] private Button exitButton;

    //----------------------------
    // 运行时数据
    //----------------------------
    private Vector3 rightEyeInitPos;  // 右眼仁初始位置（圆心）
    private Vector3 leftEyeInitPos;   // 左眼仁初始位置（圆心）
    private Vector3 mouthInitPos;     // 嘴巴初始位置（圆心）
    private Vector2 currentOffset;    // 当前平滑后的眼睛偏移
    private Camera mainCam;

    private void Start()
    {
        // 以初始位置作为各自圆形区域的圆心
        rightEyeInitPos = rightEye.position;
        leftEyeInitPos  = leftEye.position;
        mouthInitPos    = mouth.position;
        mainCam = Camera.main;

        
    }

    private void Update()
    {
        // 鼠标屏幕坐标 → 世界坐标（z 取眼睛到相机的距离，保证转换准确）
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = mainCam.WorldToScreenPoint(rightEyeInitPos).z;
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(mouseScreen);

        // 参考点：两眼中间，计算指向鼠标的向量
        Vector2 center  = ( (Vector2)rightEyeInitPos + (Vector2)leftEyeInitPos ) * 0.5f;
        Vector2 toMouse = mouseWorld - center;

        // 限制在圆形区域内：偏移长度不超过半径
        if ( toMouse.sqrMagnitude > radius * radius )
            toMouse = toMouse.normalized * radius;

        // 平滑过渡，避免眼仁瞬移
        currentOffset = Vector2.Lerp(currentOffset, toMouse, smoothSpeed * Time.deltaTime);

        // 两眼同步平移
        rightEye.position = rightEyeInitPos + (Vector3)currentOffset;
        leftEye.position  = leftEyeInitPos  + (Vector3)currentOffset;

        // 嘴巴与眼睛同方向平移，幅度按 mouthRadius/radius 比例缩放
        mouth.position = mouthInitPos + (Vector3)( currentOffset / radius * mouthRadius );
    }

}
