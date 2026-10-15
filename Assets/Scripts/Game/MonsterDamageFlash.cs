using System.Collections;
using UnityEngine;

/// <summary>
/// 受击闪白表现：订阅 MonsterStats.OnDamaged，把拖入的贴图 SpriteRenderer
/// 临时替换为 Custom/SpriteFlashWhite 材质，扣血瞬间 _FlashAmount=1 再渐回 0。
/// 原理：SpriteRenderer.color 是乘法混合永远无法"变亮"，闪白必须由 Shader 加法插值实现。
/// </summary>
public class MonsterDamageFlash : MonoBehaviour
{
    //----------------------------
    // 可调参数（常修改）
    //----------------------------
    [Header("闪白时长")]
    [Tooltip("闪白从峰值 1 渐回 0 的时长（秒）")]
    [Range(0.05f, 1f)]
    [SerializeField] private float flashDuration = 0.15f;

    //----------------------------
    // 引用（Inspector 手动拖入）
    //----------------------------
    [Header("引用")]
    [Tooltip("受击闪白的贴图节点（奶蛙身体下的 5 个 SpriteRenderer）")]
    [SerializeField] private SpriteRenderer[] renderers;

    [Tooltip("闪白 Shader（Assets/Shaders/SpriteFlashWhite）")]
    [SerializeField] private Shader flashShader;

    //----------------------------
    // 运行时状态
    //----------------------------
    private Material _flashMat;                       // 运行时动态创建的共享闪白材质
    private Material[] _originalMats;                 // 各 renderer 原材质（销毁时恢复）
    private Coroutine _flashRoutine;
    private MonsterStats _stats;

    private void Awake()
    {
        if (flashShader == null || renderers == null || renderers.Length == 0)
            return;

        // 一个实例化材质驱动所有贴图节点：SetFloat 一次全部生效
        _flashMat = new Material(flashShader);
        _originalMats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            _originalMats[i] = r.sharedMaterial;
            r.sharedMaterial = _flashMat;
        }
    }

    private void Start()
    {
        _stats = GetComponent<MonsterStats>();
        if (_stats != null)
            _stats.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnDamaged -= HandleDamaged;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.sharedMaterial = _originalMats != null ? _originalMats[i] : null;
            }
        }

        if (_flashMat != null)
            Destroy(_flashMat);
    }

    /// <summary>受伤回调：触发一次闪白（连续受击时刷新进度，不叠加）</summary>
    private void HandleDamaged(float hpPercent)
    {
        if (_flashMat == null) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float t = 0f;
        _flashMat.SetFloat("_FlashAmount", 1f);

        while (t < flashDuration)
        {
            t += Time.deltaTime;
            _flashMat.SetFloat("_FlashAmount", 1f - t / flashDuration);
            yield return null;
        }

        _flashMat.SetFloat("_FlashAmount", 0f);
        _flashRoutine = null;
    }
}
