using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class 刀光拖尾渲染器 : MonoBehaviour
{
    [Header("位置引用")]
    public Transform[] 刀身点;
    [Tooltip("用于检测移动距离的中心点，如果不设置则使用第一个刀身点")]
    public Transform 拖尾中心点;

    [Header("材质设置")]
    public Material 拖尾材质;

    [Header("颜色设置")]
    [Tooltip("控制拖尾颜色随时间变化")]
    public Gradient 拖尾颜色渐变 = new Gradient();

    [Header("时间参数")]
    public float 每次网格生成间隔时间 = 0.05f;
    public float 每段网格存在时间 = 0.5f;

    [Header("距离触发参数")]
    [Tooltip("基于距离触发时，移动多少距离才生成新顶点")]
    public float 触发移动距离 = 0.1f;
    [Tooltip("启用基于距离的触发模式（将忽略时间间隔）")]
    public bool 使用距离触发 = false;

    [Header("更新模式")]
    [Tooltip("开启后每帧更新网格，不受生成间隔时间影响")]
    public bool 每帧更新 = false;

    [Header("永久顶点设置")]
    [Tooltip("是否始终保留当前刀身位置作为最新顶点")]
    public bool 保留当前帧为永久顶点 = true;

    [Header("状态控制")]
    [SerializeField] private bool 是否启用 = true;
    public bool 启用状态
    {
        get { return 是否启用; }
        set { 是否启用 = value; }
    }

    [Header("编辑器设置")]
    public bool 编辑器模式下运行 = true;


    private struct 历史顶点
    {
        public Vector3[] 刀身点位置;
        public float 创建时间;

        public 历史顶点(Vector3[] positions, float time)
        {
            刀身点位置 = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                刀身点位置[i] = positions[i];
            }
            创建时间 = time;
        }
    }

    private List<历史顶点> 历史顶点列表 = new List<历史顶点>();
    private Mesh 刀光拖尾网格;
    private MeshFilter 网格过滤器;
    private float 上次生成时间 = 0f;
    private float 上次编辑器时间 = 0f;
    private bool 上次运行状态 = false;


    private Color[] 顶点颜色数组;

    private Vector3 上次中心点位置;
    private bool 首次记录中心点 = true;

    private Vector3[] 当前帧刀身点位置;

    void Start()
    {
        初始化组件();
        上次生成时间 = GetCurrentTime();
        上次编辑器时间 = GetCurrentTime();
        重置中心点记录();
        初始化渐变();
    }

    void OnEnable()
    {
        if (网格过滤器 == null || 刀光拖尾网格 == null)
        {
            初始化组件();
        }
        上次生成时间 = GetCurrentTime();
        上次编辑器时间 = GetCurrentTime();
        重置中心点记录();
        初始化渐变();
    }


    private void 初始化渐变()
    {
        if (拖尾颜色渐变 == null || 拖尾颜色渐变.colorKeys.Length == 0)
        {
            拖尾颜色渐变 = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0].color = Color.white;
            colorKeys[0].time = 0f;
            colorKeys[1].color = new Color(1, 1, 1, 0);
            colorKeys[1].time = 1f;

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0].alpha = 1f;
            alphaKeys[0].time = 0f;
            alphaKeys[1].alpha = 0f;
            alphaKeys[1].time = 1f;

            拖尾颜色渐变.SetKeys(colorKeys, alphaKeys);
        }
    }

    void OnDisable()
    {
        if (刀光拖尾网格 != null)
        {
            刀光拖尾网格.Clear();
        }
    }

    void Update()
    {
        if (Application.isPlaying != 上次运行状态)
        {
            if (!Application.isPlaying)
            {
                上次编辑器时间 = GetCurrentTime();
            }
            上次运行状态 = Application.isPlaying;
        }

        bool 应该执行 = Application.isPlaying || (编辑器模式下运行 && !Application.isPlaying);
        if (!应该执行)
        {
            if (刀光拖尾网格 != null && 刀光拖尾网格.vertexCount > 0)
            {
                刀光拖尾网格.Clear();
            }
            return;
        }

        更新当前帧位置();
        移除过期顶点对();

        if (是否启用)
        {
            float 当前时间 = GetCurrentTime();

            bool 应该生成新顶点 = false;

            if (每帧更新)
            {
                应该生成新顶点 = true;
            }
            else if (使用距离触发)
            {
                应该生成新顶点 = 检查距离触发();
            }
            else
            {
                应该生成新顶点 = (当前时间 - 上次生成时间 >= 每次网格生成间隔时间);
            }

            if (应该生成新顶点)
            {
                添加新顶点对();
                上次生成时间 = 当前时间;
            }
        }

        if (刀身点 != null && 刀身点.Length >= 2)
        {
            更新网格();
        }
        else
        {
            if (刀光拖尾网格 != null)
                刀光拖尾网格.Clear();
        }
    }

    private bool 检查距离触发()
    {
        if (刀身点 == null || 刀身点.Length == 0)
            return false;

        Vector3 当前中心位置;
        if (拖尾中心点 != null)
        {
            当前中心位置 = 拖尾中心点.position;
        }
        else
        {
            当前中心位置 = 刀身点[0].position;
        }

        if (首次记录中心点)
        {
            上次中心点位置 = 当前中心位置;
            首次记录中心点 = false;
            return true;
        }

        float 移动距离 = Vector3.Distance(当前中心位置, 上次中心点位置);

        if (移动距离 >= 触发移动距离)
        {
            上次中心点位置 = 当前中心位置;
            return true;
        }

        return false;
    }

    public void 重置中心点记录()
    {
        首次记录中心点 = true;
    }

    public void 更新中心点位置()
    {
        if (拖尾中心点 != null)
        {
            上次中心点位置 = 拖尾中心点.position;
        }
        else if (刀身点 != null && 刀身点.Length > 0)
        {
            上次中心点位置 = 刀身点[0].position;
        }
        首次记录中心点 = false;
    }

    public Vector3 获取当前中心点位置()
    {
        if (拖尾中心点 != null)
        {
            return 拖尾中心点.position;
        }
        else if (刀身点 != null && 刀身点.Length > 0)
        {
            return 刀身点[0].position;
        }
        return Vector3.zero;
    }

    public Vector3 获取上次中心点位置()
    {
        return 上次中心点位置;
    }

    public float 获取当前移动距离()
    {
        if (首次记录中心点)
            return 0f;

        Vector3 当前中心位置;
        if (拖尾中心点 != null)
        {
            当前中心位置 = 拖尾中心点.position;
        }
        else if (刀身点 != null && 刀身点.Length > 0)
        {
            当前中心位置 = 刀身点[0].position;
        }
        else
        {
            return 0f;
        }

        return Vector3.Distance(当前中心位置, 上次中心点位置);
    }

    private void 更新当前帧位置()
    {
        if (刀身点 == null || 刀身点.Length < 2)
            return;

        if (当前帧刀身点位置 == null || 当前帧刀身点位置.Length != 刀身点.Length)
        {
            当前帧刀身点位置 = new Vector3[刀身点.Length];
        }

        for (int i = 0; i < 刀身点.Length; i++)
        {
            if (刀身点[i] != null)
                当前帧刀身点位置[i] = 刀身点[i].position;
            else
                return;
        }
    }

    private void 初始化组件()
    {
        网格过滤器 = GetComponent<MeshFilter>();
        if (网格过滤器 == null)
            网格过滤器 = gameObject.AddComponent<MeshFilter>();

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<MeshRenderer>();

        if (刀光拖尾网格 == null)
        {
            刀光拖尾网格 = new Mesh();
            刀光拖尾网格.name = "刀光拖尾网格";
        }
        网格过滤器.mesh = 刀光拖尾网格;

        if (拖尾材质 != null && renderer.sharedMaterial != 拖尾材质)
        {
            renderer.sharedMaterial = 拖尾材质;
        }

        初始化渐变();
    }

    private float GetCurrentTime()
    {
        if (Application.isPlaying)
        {
            return Time.time;
        }
        else
        {
#if UNITY_EDITOR
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.timeSinceLevelLoad;
#endif
        }
    }

    public void 启用()
    {
        是否启用 = true;
        上次生成时间 = GetCurrentTime();
        重置中心点记录();
    }

    public void 禁用()
    {
        是否启用 = false;
    }

    public void 切换启用状态()
    {
        是否启用 = !是否启用;
        if (是否启用)
        {
            上次生成时间 = GetCurrentTime();
            重置中心点记录();
        }
    }

    public void 设置启用状态(bool 启用)
    {
        是否启用 = 启用;
        if (是否启用)
        {
            上次生成时间 = GetCurrentTime();
            重置中心点记录();
        }
    }

    public void 立即清除拖尾()
    {
        历史顶点列表.Clear();
        if (刀光拖尾网格 != null)
            刀光拖尾网格.Clear();
        重置中心点记录();
    }

    public void 设置使用距离触发(bool 启用)
    {
        使用距离触发 = 启用;
        if (启用)
        {
            重置中心点记录();
        }
    }

    public void 切换触发模式()
    {
        使用距离触发 = !使用距离触发;
        if (使用距离触发)
        {
            重置中心点记录();
        }
    }

    public bool 获取使用距离触发状态()
    {
        return 使用距离触发;
    }

    public void 设置触发移动距离(float 距离)
    {
        触发移动距离 = Mathf.Max(0.01f, 距离);
    }

    public float 获取触发移动距离()
    {
        return 触发移动距离;
    }

    public void 设置拖尾中心点(Transform 中心点)
    {
        拖尾中心点 = 中心点;
        重置中心点记录();
    }

    public void 设置保留当前帧(bool 保留)
    {
        保留当前帧为永久顶点 = 保留;
    }

    public bool 获取保留当前帧状态()
    {
        return 保留当前帧为永久顶点;
    }

    public void 设置生成间隔时间(float 间隔时间)
    {
        每次网格生成间隔时间 = Mathf.Max(0.01f, 间隔时间);
    }

    public void 设置拖尾存在时间(float 存在时间)
    {
        每段网格存在时间 = Mathf.Max(0.1f, 存在时间);
    }

    public void 设置拖尾持续时间(float 持续时间)
    {
        每段网格存在时间 = Mathf.Max(0.1f, 持续时间);
    }

    public void 设置每帧更新(bool 启用每帧更新)
    {
        每帧更新 = 启用每帧更新;
    }

    public void 切换每帧更新()
    {
        每帧更新 = !每帧更新;
    }

    public bool 获取每帧更新状态()
    {
        return 每帧更新;
    }

    public void 设置刀身点(Transform[] 新刀身点)
    {
        刀身点 = 新刀身点;
    }

    public void 设置刀身点AtIndex(int 索引, Transform 刀身点Transform)
    {
        if (刀身点 != null && 索引 >= 0 && 索引 < 刀身点.Length)
        {
            刀身点[索引] = 刀身点Transform;
        }
    }

    public void 清空历史数据()
    {
        历史顶点列表.Clear();
        重置中心点记录();
    }

    public void 设置拖尾材质(Material 新材质)
    {
        拖尾材质 = 新材质;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && 拖尾材质 != null)
        {
            renderer.sharedMaterial = 拖尾材质;
        }
    }

    // 设置渐变
    public void 设置拖尾渐变(Gradient 新渐变)
    {
        拖尾颜色渐变 = 新渐变;
    }

    // 获取渐变
    public Gradient 获取拖尾渐变()
    {
        return 拖尾颜色渐变;
    }

    // 重置渐变到默认值
    public void 重置渐变()
    {
        初始化渐变();
    }

    public void 设置材质颜色(Color 颜色)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                renderer.sharedMaterial.SetColor("_Color", 颜色);
            }
            else if (renderer.sharedMaterial.HasProperty("_TintColor"))
            {
                renderer.sharedMaterial.SetColor("_TintColor", 颜色);
            }
            else if (renderer.sharedMaterial.HasProperty("_MainColor"))
            {
                renderer.sharedMaterial.SetColor("_MainColor", 颜色);
            }
        }
    }

    public void 设置材质透明度(float 透明度)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            透明度 = Mathf.Clamp01(透明度);

            if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                Color color = renderer.sharedMaterial.GetColor("_Color");
                color.a = 透明度;
                renderer.sharedMaterial.SetColor("_Color", color);
            }
            else if (renderer.sharedMaterial.HasProperty("_TintColor"))
            {
                Color color = renderer.sharedMaterial.GetColor("_TintColor");
                color.a = 透明度;
                renderer.sharedMaterial.SetColor("_TintColor", color);
            }
        }
    }

    public void 设置编辑器模式下运行(bool 运行)
    {
        编辑器模式下运行 = 运行;
    }

    public void 重置为默认设置()
    {
        每次网格生成间隔时间 = 0.05f;
        每段网格存在时间 = 0.5f;
        每帧更新 = false;
        保留当前帧为永久顶点 = true;
        使用距离触发 = false;
        触发移动距离 = 0.1f;
        初始化渐变();
        立即清除拖尾();
        重置中心点记录();
    }

    public void 快速设置拖尾(float 持续时间, float 生成间隔 = 0.05f)
    {
        每段网格存在时间 = Mathf.Max(0.1f, 持续时间);
        每次网格生成间隔时间 = Mathf.Max(0.01f, 生成间隔);
    }

    private void 添加新顶点对()
    {
        if (刀身点 == null || 刀身点.Length < 2)
            return;

        Vector3[] positions = new Vector3[刀身点.Length];
        for (int i = 0; i < 刀身点.Length; i++)
        {
            if (刀身点[i] != null)
                positions[i] = 刀身点[i].position;
            else
                return;
        }

        历史顶点列表.Add(new 历史顶点(positions, GetCurrentTime()));
    }

    private void 移除过期顶点对()
    {
        float 当前时间 = GetCurrentTime();
        for (int i = 历史顶点列表.Count - 1; i >= 0; i--)
        {
            if (当前时间 - 历史顶点列表[i].创建时间 > 每段网格存在时间)
            {
                历史顶点列表.RemoveAt(i);
            }
        }
    }

    private void 更新网格()
    {
        if (历史顶点列表.Count == 0 && 当前帧刀身点位置 == null)
        {
            刀光拖尾网格.Clear();
            return;
        }

        int 刀身分段数 = 刀身点.Length;

        int 历史帧数量 = 历史顶点列表.Count;
        int 总帧数 = 历史帧数量;
        bool 包含当前帧 = 保留当前帧为永久顶点 && 当前帧刀身点位置 != null;

        if (包含当前帧)
        {
            总帧数++;
        }

        if (总帧数 < 2)
        {
            刀光拖尾网格.Clear();
            return;
        }

        Vector3[] 顶点数组 = new Vector3[总帧数 * 刀身分段数];
        Vector2[] uv数组 = new Vector2[总帧数 * 刀身分段数];
        Color[] 颜色数组 = new Color[总帧数 * 刀身分段数];  // 颜色数组
        int[] 三角形数组 = new int[(总帧数 - 1) * (刀身分段数 - 1) * 6];

        float 当前时间 = GetCurrentTime();
        Matrix4x4 世界转局部矩阵 = transform.worldToLocalMatrix;

        // 处理历史顶点
        for (int 帧索引 = 0; 帧索引 < 历史帧数量; 帧索引++)
        {
            历史顶点 当前帧 = 历史顶点列表[帧索引];
            float 帧年龄 = 当前时间 - 当前帧.创建时间;
            float 帧时间比例 = 帧年龄 / 每段网格存在时间;  // 0 = 最新, 1 = 最旧

            for (int 刀身索引 = 0; 刀身索引 < 刀身分段数; 刀身索引++)
            {
                int 顶点索引 = 帧索引 * 刀身分段数 + 刀身索引;
                Vector3 世界位置 = 当前帧.刀身点位置[刀身索引];
                Vector3 局部位置 = 世界转局部矩阵.MultiplyPoint3x4(世界位置);

                顶点数组[顶点索引] = 局部位置;

                // 计算UV
                float u = (float)帧索引 / (总帧数 - 1);
                float v = (float)刀身索引 / (刀身分段数 - 1);
                uv数组[顶点索引] = new Vector2(u, v);

                // 根据时间比例设置颜色
                颜色数组[顶点索引] = 拖尾颜色渐变.Evaluate(帧时间比例);
            }
        }

        // 处理当前帧（如果包含）
        if (包含当前帧)
        {
            int 当前帧索引 = 总帧数 - 1;

            for (int 刀身索引 = 0; 刀身索引 < 刀身分段数; 刀身索引++)
            {
                int 顶点索引 = 当前帧索引 * 刀身分段数 + 刀身索引;
                Vector3 世界位置 = 当前帧刀身点位置[刀身索引];
                Vector3 局部位置 = 世界转局部矩阵.MultiplyPoint3x4(世界位置);

                顶点数组[顶点索引] = 局部位置;

                float u = 1f;
                float v = (float)刀身索引 / (刀身分段数 - 1);
                uv数组[顶点索引] = new Vector2(u, v);

                // 当前帧颜色（最新顶点，时间比例=0）
                颜色数组[顶点索引] = 拖尾颜色渐变.Evaluate(0f);
            }
        }

        // 构建三角形
        int 三角形索引 = 0;
        for (int 帧索引 = 0; 帧索引 < 总帧数 - 1; 帧索引++)
        {
            for (int 刀身索引 = 0; 刀身索引 < 刀身分段数 - 1; 刀身索引++)
            {
                int 当前帧当前点 = 帧索引 * 刀身分段数 + 刀身索引;
                int 当前帧下一点 = 帧索引 * 刀身分段数 + 刀身索引 + 1;
                int 下一帧当前点 = (帧索引 + 1) * 刀身分段数 + 刀身索引;
                int 下一帧下一点 = (帧索引 + 1) * 刀身分段数 + 刀身索引 + 1;

                三角形数组[三角形索引++] = 当前帧当前点;
                三角形数组[三角形索引++] = 下一帧当前点;
                三角形数组[三角形索引++] = 当前帧下一点;

                三角形数组[三角形索引++] = 下一帧当前点;
                三角形数组[三角形索引++] = 下一帧下一点;
                三角形数组[三角形索引++] = 当前帧下一点;
            }
        }

        刀光拖尾网格.Clear();
        刀光拖尾网格.vertices = 顶点数组;
        刀光拖尾网格.triangles = 三角形数组;
        刀光拖尾网格.uv = uv数组;
        刀光拖尾网格.colors = 颜色数组;  // 设置顶点颜色

        刀光拖尾网格.RecalculateNormals();
        刀光拖尾网格.RecalculateBounds();
    }

    void OnDrawGizmosSelected()
    {
        if (历史顶点列表 == null) return;

        for (int 帧索引 = 0; 帧索引 < 历史顶点列表.Count; 帧索引++)
        {
            历史顶点 当前帧 = 历史顶点列表[帧索引];

            float alpha = 1f - (float)帧索引 / 历史顶点列表.Count;
            Gizmos.color = new Color(1, 0.5f, 0, alpha);

            for (int 刀身索引 = 0; 刀身索引 < 当前帧.刀身点位置.Length; 刀身索引++)
            {
                Gizmos.DrawSphere(当前帧.刀身点位置[刀身索引], 0.03f);

                if (刀身索引 > 0)
                {
                    Gizmos.DrawLine(
                        当前帧.刀身点位置[刀身索引 - 1],
                        当前帧.刀身点位置[刀身索引]
                    );
                }
            }

            if (帧索引 > 0)
            {
                历史顶点 上一帧 = 历史顶点列表[帧索引 - 1];
                Gizmos.color = new Color(0, 1, 0, alpha * 0.5f);

                for (int 刀身索引 = 0; 刀身索引 < 当前帧.刀身点位置.Length; 刀身索引++)
                {
                    Gizmos.DrawLine(
                        上一帧.刀身点位置[刀身索引],
                        当前帧.刀身点位置[刀身索引]
                    );
                }
            }
        }

        if (保留当前帧为永久顶点 && 当前帧刀身点位置 != null)
        {
            Gizmos.color = Color.red;
            for (int 刀身索引 = 0; 刀身索引 < 当前帧刀身点位置.Length; 刀身索引++)
            {
                Gizmos.DrawSphere(当前帧刀身点位置[刀身索引], 0.05f);

                if (刀身索引 > 0)
                {
                    Gizmos.DrawLine(
                        当前帧刀身点位置[刀身索引 - 1],
                        当前帧刀身点位置[刀身索引]
                    );
                }
            }

            if (历史顶点列表.Count > 0)
            {
                历史顶点 最后一帧 = 历史顶点列表[历史顶点列表.Count - 1];
                Gizmos.color = Color.yellow;
                for (int 刀身索引 = 0; 刀身索引 < 当前帧刀身点位置.Length; 刀身索引++)
                {
                    Gizmos.DrawLine(
                        最后一帧.刀身点位置[刀身索引],
                        当前帧刀身点位置[刀身索引]
                    );
                }
            }
        }

        if (拖尾中心点 != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(拖尾中心点.position, 0.08f);

            if (刀身点 != null && 刀身点.Length > 0 && 刀身点[0] != null)
            {
                Gizmos.color = new Color(0, 1, 1, 0.5f);
                Gizmos.DrawLine(拖尾中心点.position, 刀身点[0].position);
            }
        }
    }
}

