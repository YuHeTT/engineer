// ---------------------------------------------------------------------------
//  RoverModelCleaner.cs —— 清理小车模型里混进来的"影棚场景"
// ---------------------------------------------------------------------------
//  【问题】`reference_rover_lux3d_wheels.fbx` 里除了小车本身，还混进了
//          Blender 的**整套影棚展示环境**：
//
//             Studio floor                  ← 影棚地面（一大块灰色平面）
//             Large studio softbox          ← 影棚柔光箱
//             Front fill                    ← 影棚补光灯
//             Rear edge light               ← 影棚轮廓灯
//             Three quarter reference view  ← 影棚相机
//             材质：Warm gray studio / Designer_Logic_PBR
//
//          **`Studio floor` 就是那块盖在田上的灰色平面** —— 它有几米到几十米大，
//          摆在田里就是一大块灰板，把土壤和玉米全挡住。
//
//  【为什么在代码里清理而不是等重新导入】
//    重新导出 FBX 需要人操作 Blender。在那之前，先用代码把这几样去掉，
//    这样 demo 立刻能用；等新的 FBX 到位后本组件会自动"无事可做"。
//
//  【清理规则】
//    ① 按名字：命中 Studio / Softbox / Fill / Edge light / Reference view 等关键词
//    ② 按尺寸：包围盒 X/Z 明显大于小车本体（超过阈值倍数）的薄板状物体
//    两条都命中才删，避免误删零件（比如"Front fill"也可能是车头填充件，
//    所以尺寸判定必须一起满足）。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>去掉小车模型自带的影棚地面 / 灯光 / 相机。</summary>
    public static class RoverModelCleaner
    {
        /// <summary>影棚物件常见关键词（小写匹配）。</summary>
        private static readonly string[] StudioKeywords =
        {
            "studio", "softbox", "reference view", "backdrop",
            "edge light", "studio floor", "fill light",
        };

        /// <summary>清理结果。</summary>
        public struct Result
        {
            public int Removed;
            public List<string> Names;

            public override string ToString()
            {
                if (Removed == 0) return "没有需要清理的影棚物件";
                return string.Format("清理了 {0} 个：{1}", Removed, string.Join("、", Names.ToArray()));
            }
        }

        /// <summary>
        /// 清理 <paramref name="rover"/> 下的影棚物件。
        /// </summary>
        /// <param name="sizeRatioThreshold">
        /// 尺寸判定阈值：某物体的 X/Z 包围盒超过"小车本体 X/Z"的这个倍数，
        /// 且它同时命中影棚关键词或呈明显薄板状，才会被删。
        /// </param>
        public static Result Clean(GameObject rover, float sizeRatioThreshold = 1.6f)
        {
            var result = new Result { Removed = 0, Names = new List<string>() };
            if (rover == null) return result;

            // 先算出小车本体（排除候选物件）的最大水平尺寸，作为参照
            var bodySize = MeasureBodySize(rover);

            var renderers = rover.GetComponentsInChildren<Renderer>(true);
            var toRemove = new HashSet<GameObject>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var go = r.gameObject;
                var nameLower = go.name.ToLowerInvariant();

                var nameHit = false;
                for (var k = 0; k < StudioKeywords.Length; k++)
                {
                    if (nameLower.Contains(StudioKeywords[k])) { nameHit = true; break; }
                }

                if (!nameHit) continue;

                // 名字命中了，再看尺寸：明显比车体大的才算"影棚环境"
                var b = r.bounds;
                var horizontal = Mathf.Max(b.size.x, b.size.z);
                var bigger = horizontal > bodySize * sizeRatioThreshold;

                // 也接受"特别扁"的：Y 很薄但 X/Z 很大（典型的地面/背景板）
                var flatAndWide = b.size.y < horizontal * 0.15f && bigger;

                if (!bigger && !flatAndWide) continue;

                toRemove.Add(go);
                result.Names.Add(string.Format("{0}（{1:0.#}×{2:0.#}×{3:0.#} m）",
                    go.name, b.size.x, b.size.y, b.size.z));
            }

            foreach (var go in toRemove)
            {
                if (go == null) continue;
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
                result.Removed++;
            }

            return result;
        }

        /// <summary>量出"小车本体"的水平尺寸（排除掉影棚关键词命中的大物件）。</summary>
        private static float MeasureBodySize(GameObject rover)
        {
            var renderers = rover.GetComponentsInChildren<Renderer>(true);
            var maxHorizontal = 1f;

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var nameLower = r.gameObject.name.ToLowerInvariant();
                var isStudio = false;
                for (var k = 0; k < StudioKeywords.Length; k++)
                {
                    if (nameLower.Contains(StudioKeywords[k])) { isStudio = true; break; }
                }
                if (isStudio) continue;      // 影棚物件不参与"本体尺寸"统计

                var b = r.bounds;
                maxHorizontal = Mathf.Max(maxHorizontal, Mathf.Max(b.size.x, b.size.z));
            }

            return maxHorizontal;
        }
    }
}
