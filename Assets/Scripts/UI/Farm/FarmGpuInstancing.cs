// ---------------------------------------------------------------------------
//  FarmGpuInstancing.cs —— 用 GPU Instancing 代替网格合并
// ---------------------------------------------------------------------------
//  【为什么改用 Instancing 而不是合并】
//    一开始我用 MeshCombiner 把零碎网格合并成少数几个大 Mesh。
//    实测发现这条路在这个数据量下是错的：
//      · 土壤一块就 26,528 顶点，36 块 = 95 万顶点
//      · 而单个 Mesh 的顶点上限是 65,535（16 位索引），所以每 2~3 块就得分一个 chunk
//      · 结果「36 块 → 49 个块」，几乎没减少
//      · 而且在跨 chunk 时几何会被**复制**，Tris 反而从 29.7M 涨到 50.2M
//
//    Instancing 才是对的：
//      · 200 株玉米共用同一份 6 个 Mesh（内存不涨）
//      · 每株只是"换个矩阵再画一次"，由 GPU 批处理
//      · 200 株玉米 ≈ 6 次 draw call（而不是 6 个合并块 × 几十个 chunk）
//      · **不会复制几何**，Tris 保持真实值
//
//  【前提】材质的 `enableInstancing` 必须为 true。
//    glTF 导入的材质默认是关的，所以这里逐个打开。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>给场景里的材质打开 GPU Instancing。</summary>
    public static class FarmGpuInstancing
    {
        /// <summary>
        /// 打开 <paramref name="root"/> 下所有渲染器所用材质的 Instancing。
        /// </summary>
        /// <returns>本次改动过的材质数。</returns>
        public static int Enable(GameObject root)
        {
            if (root == null) return 0;

            var seen = new HashSet<Material>();
            var changed = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (var m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || seen.Contains(mat)) continue;
                    seen.Add(mat);

                    if (mat.enableInstancing) continue;
                    mat.enableInstancing = true;
                    changed++;

                    // 运行时改材质会影响所有引用者 —— 这是我们要的效果，
                    // 但要把改动标脏，否则编辑器退出时可能不保存。
                    // 注意：UnityEditor 不能出现在运行时程序集里，所以要加守卫。
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        UnityEditor.EditorUtility.SetDirty(mat);
#endif
                }
            }

            return changed;
        }

        /// <summary>
        /// 统计某个根下的统计信息，用来核对优化效果。
        /// </summary>
        public struct Stats
        {
            public int Renderers;
            public int UniqueMeshes;
            public int UniqueMaterials;
            public long TotalVertices;
            public long TotalTriangles;
            public int InstancingEnabledMaterials;

            public override string ToString()
            {
                return string.Format(
                    "渲染器 {0}，唯一网格 {1}，唯一材质 {2}（已开 Instancing {3}），" +
                    "顶点 {4:N0}，三角面 {5:N0}",
                    Renderers, UniqueMeshes, UniqueMaterials, InstancingEnabledMaterials,
                    TotalVertices, TotalTriangles);
            }
        }

        public static Stats Measure(GameObject root)
        {
            var s = new Stats();
            if (root == null) return s;

            var meshes = new HashSet<Mesh>();
            var mats = new HashSet<Material>();
            var instancingOn = new HashSet<Material>();

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;

                var mesh = mf.sharedMesh;
                if (meshes.Add(mesh))
                {
                    s.TotalVertices += mesh.vertexCount;
                    // 用 indexCount/3 更准（比 triCount 之类的 API 稳）
                    s.TotalTriangles += (long)mesh.GetIndexCount(0) / 3L;
                }

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                var list = mr.sharedMaterials;
                for (var k = 0; k < list.Length; k++)
                {
                    var mat = list[k];
                    if (mat == null) continue;
                    mats.Add(mat);
                    if (mat.enableInstancing) instancingOn.Add(mat);
                }
            }

            s.Renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            s.UniqueMeshes = meshes.Count;
            s.UniqueMaterials = mats.Count;
            s.InstancingEnabledMaterials = instancingOn.Count;
            return s;
        }
    }
}
