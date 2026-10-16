using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 土地基础数据设置界面（文档 image4）。
    ///
    /// 【这一屏之前是缺口，现在补上】
    ///   场景里早就有 `ParamForm/Row_Param1..6/Input_1..6` 六个输入框，
    ///   但之前挂的是通用 <see cref="PlaceholderScreen"/>，**没有任何脚本去读它们** ——
    ///   也就是说玩家在这里填的六项数据根本没进入流程，模拟一直用代码里写死的默认值。
    ///   本脚本补上这个缺口。
    ///
    /// 【六项与输入框的对应关系（顺序严格按场景节点 Row_Param1..6）】
    ///   Row_Param1 → 土地面积（亩）
    ///   Row_Param2 → 酸衡值 (pH)
    ///   Row_Param3 → 腐殖丰度
    ///   Row_Param4 → 速效氮元
    ///   Row_Param5 → 磷活度
    ///   Row_Param6 → 钾储量
    ///
    /// 【何时启动数值内核】
    ///   点「选择植物」时读六项 → 写回 <see cref="GameContext"/> → 启动内核
    ///   （<see cref="GameFlowController.BeginSimulation"/>）→ 切到植物选择屏。
    ///   这样玩家从"返回上一步"改完参数再进来，内核会重新初始化，不会残留上一局状态。
    ///
    /// 【输入合法性】会按 <see cref="SimConfig.DynamicInputRanges"/> 裁剪到合法区间，
    ///   并把裁剪结果写回输入框，让玩家看见自己填的值被修正了。
    /// </summary>
    public class LandParamsScreen : PlaceholderScreen
    {
        private const int ParamCount = 6;

        [Header("六项基础数据输入框")]
        [Tooltip("顺序必须与场景一致：面积 / pH / 腐殖丰度 / 速效氮 / 磷活度 / 钾储量。")]
        public TMP_InputField[] paramInputs = new TMP_InputField[ParamCount];

        [Tooltip("每行的标题文本（可选，用于显示合法区间提示）。")]
        public TMP_Text[] paramLabels = new TMP_Text[ParamCount];

        [Header("底部")]
        [Tooltip("「选择植物」按钮。点击时读输入并启动数值内核。")]
        public Button plantSelectButton;

        [Header("选项")]
        [Tooltip("是否把输入裁剪进 SimConfig 定义的合法区间，并把结果写回输入框。")]
        public bool clampToConfigRange = true;

        [Tooltip("是否在行标题后追加合法区间提示，例如「酸衡值 (pH)  [5.8~8.0]」。")]
        public bool showRangeHint = true;

        private bool resolved;

        protected override bool OwnsButton(Button button) => button != null && button == plantSelectButton;

        public override void OnEnter(object args)
        {
            base.OnEnter(args);
            EnsureResolved();

            if (plantSelectButton != null)
            {
                plantSelectButton.onClick.RemoveListener(OnPlantSelectClicked);
                plantSelectButton.onClick.AddListener(OnPlantSelectClicked);
            }

            FillFromContext();
            RefreshRangeHints();
        }

        /// <summary>把 <see cref="GameContext"/> 里的当前值填进六个输入框。</summary>
        public void FillFromContext()
        {
            var context = Flow != null ? Flow.Context : null;
            if (context == null) return;

            SetInput(0, context.LandAreaMu);
            SetInput(1, context.Ph);
            SetInput(2, context.OrganicMatter);
            SetInput(3, context.AvailableNitrogen);
            SetInput(4, context.LabilePhosphorus);
            SetInput(5, context.Potassium);
        }

        /// <summary>点「选择植物」：读输入 → 启动内核 → 进入植物选择。</summary>
        public void OnPlantSelectClicked()
        {
            var flow = Flow;
            var context = flow != null ? flow.Context : null;
            if (flow == null || context == null)
            {
                LogPlaceholder("进入植物选择（没有拿到状态机）");
                return;
            }

            if (!ApplyInputsToContext(context)) return;
            if (!flow.BeginSimulation())
            {
                Debug.LogWarning("[LandParams] 模拟启动失败，请检查数值内核接线。", this);
                return;
            }

            flow.GoTo(GameState.PlantSelect);
        }

        /// <summary>
        /// 读六个输入框写回 <see cref="GameContext"/>。
        /// 六项全部有效才写入；任何一项无效则保持原状态。
        /// </summary>
        public bool ApplyInputsToContext(GameContext context)
        {
            if (context == null) return false;

            var session = context.Session;
            // 全部通过后再写 context，避免一半新值、一半旧值。
            var values = new float[ParamCount];
            for (int i = 0; i < ParamCount; i++)
            {
                var input = paramInputs != null && i < paramInputs.Length ? paramInputs[i] : null;
                if (input == null || !MathX.TryParseInput(input.text, out values[i]) || (i == 0 && values[i] <= 0f))
                {
                    if (paramLabels != null && i < paramLabels.Length && paramLabels[i] != null)
                        paramLabels[i].text = ParamLabel(i) + (i == 0 ? "：请输入大于 0 的有限数字" : "：请输入有效数字");
                    if (input != null) input.Select();
                    Debug.LogWarning("[LandParams] 第 " + (i + 1) + " 项无效，未启动模拟。", this);
                    return false;
                }
            }
            context.LandAreaMu = values[0];
            context.Ph = values[1];
            context.OrganicMatter = values[2];
            context.AvailableNitrogen = values[3];
            context.LabilePhosphorus = values[4];
            context.Potassium = values[5];

            if (clampToConfigRange && session != null)
            {
                var cfg = session.Config;
                context.Ph = ClampTo(cfg, SoilMetric.pH, context.Ph);
                context.OrganicMatter = ClampTo(cfg, SoilMetric.OM, context.OrganicMatter);
                context.AvailableNitrogen = ClampTo(cfg, SoilMetric.N, context.AvailableNitrogen);
                context.LabilePhosphorus = ClampTo(cfg, SoilMetric.P, context.LabilePhosphorus);
                context.Potassium = ClampTo(cfg, SoilMetric.K, context.Potassium);

                // 把裁剪后的值写回输入框，让玩家看见修正结果
                SetInput(1, context.Ph);
                SetInput(2, context.OrganicMatter);
                SetInput(3, context.AvailableNitrogen);
                SetInput(4, context.LabilePhosphorus);
                SetInput(5, context.Potassium);
                SetInput(0, context.LandAreaMu);
            }

            RefreshRangeHints();
            return true;
        }

        // ------------------------------------------------------------------
        // 区间提示
        // ------------------------------------------------------------------

        /// <summary>在行标题后追加合法区间，数据来自 SimConfig，与内核保持一致。</summary>
        private void RefreshRangeHints()
        {
            if (!showRangeHint || paramLabels == null) return;

            var session = Flow != null && Flow.Context != null ? Flow.Context.Session : null;
            if (session == null) return;

            var cfg = session.Config;
            var metrics = new[]
            {
                (SoilMetric) (-1),      // 面积：不在指标枚举里，单独处理
                SoilMetric.pH, SoilMetric.OM, SoilMetric.N, SoilMetric.P, SoilMetric.K,
            };

            for (var i = 0; i < paramLabels.Length && i < metrics.Length; i++)
            {
                if (paramLabels[i] == null) continue;

                string hint;
                if (i == 0)
                {
                    hint = "亩";
                }
                else
                {
                    var range = cfg.RangeOf(metrics[i]);
                    hint = range != null
                        ? "[" + range.Min.ToString("0.##") + "~" + range.Max.ToString("0.##") + "]"
                        : string.Empty;
                }

                if (string.IsNullOrEmpty(hint)) continue;

                var baseText = ParamLabel(i);
                paramLabels[i].text = baseText + "  " + hint;
            }
        }

        private static string ParamLabel(int index)
        {
            switch (index)
            {
                case 0: return "土地面积";
                case 1: return "酸衡值 (pH)";
                case 2: return "腐殖丰度";
                case 3: return "速效氮元";
                case 4: return "磷活度";
                case 5: return "钾储量";
                default: return "参数 " + (index + 1);
            }
        }

        private static float ClampTo(SimConfig cfg, SoilMetric metric, float value)
        {
            var range = cfg.RangeOf(metric);
            if (range == null) return value;
            return Mathf.Clamp(value, range.Min, range.Max);
        }

        // ------------------------------------------------------------------
        // 输入框读写
        // ------------------------------------------------------------------

        private void SetInput(int index, float value)
        {
            if (paramInputs == null || index < 0 || index >= paramInputs.Length) return;
            if (paramInputs[index] == null) return;
            paramInputs[index].SetTextWithoutNotify(value.ToString("0.##", CultureInfo.InvariantCulture));
        }

        // ------------------------------------------------------------------
        // 自动查找未绑定的字段
        // ------------------------------------------------------------------

        /// <summary>按节点名补齐没绑定的字段，避免场景重建后引用丢失导致本屏失效。</summary>
        public void EnsureResolved()
        {
            if (resolved) return;
            resolved = true;

            if (paramInputs == null || paramInputs.Length < ParamCount)
                paramInputs = new TMP_InputField[ParamCount];
            if (paramLabels == null || paramLabels.Length < ParamCount)
                paramLabels = new TMP_Text[ParamCount];

            for (var i = 0; i < ParamCount; i++)
            {
                var rowName = "Body/ParamForm/Row_Param" + (i + 1);
                var row = transform.Find(rowName);

                if (paramInputs[i] == null)
                {
                    var inputT = row != null ? row.Find("Input_" + (i + 1)) : null;
                    if (inputT != null) paramInputs[i] = inputT.GetComponent<TMP_InputField>();
                }

                if (paramLabels[i] == null)
                {
                    var labelT = row != null ? row.Find("Label") : null;
                    if (labelT != null) paramLabels[i] = labelT.GetComponent<TMP_Text>();
                }
            }

            if (plantSelectButton == null)
            {
                var t = transform.Find("Footer/Button_PlantSelect");
                if (t != null) plantSelectButton = t.GetComponent<Button>();
            }
        }
    }
}
