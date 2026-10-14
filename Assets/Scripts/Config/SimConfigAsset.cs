// ---------------------------------------------------------------------------
//  SimConfigAsset.cs —— 数值配置的 Unity 资产（ScriptableObject）
// ---------------------------------------------------------------------------
//  作用：把纯 C# 的 SimConfig 包成 Unity 资产，让它可以：
//    1. 在 Project 窗口里被引用（场景/预制体不用硬编码数值）
//    2. 在 Inspector 里直接查看，方便策划对照
//    3. 通过右键菜单导出 / 导入 JSON，与 CSV 源文件对账
//
//  【为什么 Core 不能直接用 ScriptableObject】
//    Engineer.Core 程序集关闭了 UnityEngine 引用（见 asmdef），
//    所以 SimConfig 只能是纯 C# 类。包装层放在 Config 程序集里，
//    两个程序集通过 asmdef 的 references 单向依赖：Config -> Core。
//
//  【当前实现方式】
//    运行时数值由 SimConfig.CreateDefault() 生成（已含文档全部数值），
//    本资产额外承担"覆盖"职责：Inspector 里改了哪个字段就覆盖哪个。
//    导入器（ConfigImporter）会从 CSV/JSON 重新生成覆盖数据。
//
//  【使用方式】
//      var cfg = asset.BuildSimConfig();   // 拿到 Core 用的纯数据配置
//      var sim = new Simulation(cfg);
// ---------------------------------------------------------------------------

using UnityEngine;
using Engineer.Core;

namespace Engineer.Config
{
    /// <summary>数值配置资产。挂在场景里或放 Resources 下均可。</summary>
    [CreateAssetMenu(fileName = "SimConfig", menuName = "Engineer/数值配置", order = 0)]
    public sealed class SimConfigAsset : ScriptableObject
    {
        [Header("有效性")]
        [Tooltip("勾选后 BuildSimConfig() 会返回 Built 数据而不是文档默认值")]
        public bool useCustomValues;

        [Header("覆盖数据（JSON）")]
        [Tooltip("由导入器写入。留空则使用 SimConfig.CreateDefault() 的文档默认值。")]
        [TextArea(5, 20)]
        public string configJson;

        [Header("运行时状态（只读）")]
        [SerializeField, Tooltip("最近一次 BuildSimConfig 的校验结果。为空表示全部通过。")]
        private string lastValidationMessage;

        /// <summary>构建供数值内核使用的配置。</summary>
        public SimConfig BuildSimConfig()
        {
            SimConfig cfg = SimConfig.CreateDefault();

            if (useCustomValues && !string.IsNullOrEmpty(configJson))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(configJson, cfg);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SimConfigAsset] 覆盖 JSON 解析失败，回退到文档默认值：{e.Message}");
                }
            }

            Validate(cfg);
            return cfg;
        }

        /// <summary>
        /// 校验当前配置。导入后务必调用一次，能挡住绝大多数低级错误。
        /// 错误（errors）必须修；警告（warnings）是文档规则表的空洞，已由兜底接管。
        /// </summary>
        public bool Validate(SimConfig cfg)
        {
            var errors = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();
            bool ok = cfg != null && cfg.Validate(errors, warnings);

            var sb = new System.Text.StringBuilder();
            if (!ok)
            {
                sb.AppendLine("【错误】配置非法，必须修复：");
                foreach (var e in errors) sb.AppendLine("  " + e);
            }
            if (warnings.Count > 0)
            {
                sb.AppendLine($"【警告】规则表不完备（{warnings.Count} 条，已由兜底规则接管）：");
                foreach (var w in warnings) sb.AppendLine("  " + w);
            }
            if (ok && warnings.Count == 0) sb.Append("校验通过，无错误无警告。");

            lastValidationMessage = sb.ToString();
            return ok;
        }

        /// <summary>把当前配置导出为 JSON，供对账或备份。</summary>
        public string ExportJson()
        {
            var cfg = BuildSimConfig();
            return JsonUtility.ToJson(cfg, true);
        }

#if UNITY_EDITOR
        [ContextMenu("校验数值配置")]
        private void MenuValidate()
        {
            var cfg = BuildSimConfig();
            if (Validate(cfg))
                Debug.Log("[SimConfigAsset] 校验通过。");
            else
                Debug.LogError("[SimConfigAsset] 校验失败：\n" + lastValidationMessage);
        }

        [ContextMenu("打印推荐方案表")]
        private void MenuDumpPlanTable()
        {
            var cfg = BuildSimConfig();
            var table = new PlanTable(cfg);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 推荐方案表（按判定优先级）===");
            foreach (var s in Stages.All)
            {
                sb.AppendLine($"--- {Stages.DisplayName(s)} ---");
                foreach (var r in table.RulesOf(s))
                    sb.AppendLine("  " + r);
            }
            Debug.Log(sb.ToString());
        }

        [ContextMenu("检查规则覆盖性（阈值采样，约 25 万组 × 4 阶段）")]
        private void MenuCheckCoverage()
        {
            var cfg = BuildSimConfig();
            var table = new PlanTable(cfg);
            bool ok = table.CheckCoverage(out var report);
            if (ok) Debug.Log("[SimConfigAsset] 规则覆盖性检查通过，文档规则表完备。");
            else Debug.LogWarning("[SimConfigAsset] 规则表存在判定空洞（已由兜底接管）：\n"
                                  + string.Join("\n", report));
        }
#endif
    }
}
