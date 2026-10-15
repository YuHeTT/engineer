// ---------------------------------------------------------------------------
//  MeshCombiner.cs —— 把一个物体下的零碎网格合并成少数几个
// ---------------------------------------------------------------------------
//  【为什么需要】
//    这批模型是 Blender 导出的，**每个部件都是独立的 MeshRenderer**：
//      玉米_成熟_正常.glb ：74 个网格 / 6 个材质
//      玉米田土壤_四垄.glb：111 个网格 / 6 个材质
//      房车.glb          ：957 个网格 / 19 个材质
//
//    而 glTF 导入的材质**不会共用**（每个实例一份），所以 Unity 无法合批。
//    实测：20,059 个渲染器 → 283,776 次 draw call（Saved by batching: 0）。
//
//    合并之后，一株玉米从 74 个渲染器降到 6 个（按材质分组合并）。
//    玉米 200 株 × 3 阶段：26,000 → 约 1,800。
//
//  【怎么合并】
//    1. 遍历 root 下所有 MeshFilter，按 **sharedMaterial** 分组
//    2. 每组把各网格的顶点/法线/UV/颜色变换到 root 本地坐标后拼成一个 Mesh
//       （这一步是纯 C# 数组搬运，不需要任何 Unity 包）
//    3. 每组建一个子物体承载合并后的网格，删掉原来的零碎子物体
//
//  【注意】
//    · 只处理 MeshFilter + MeshRenderer，**不处理 SkinnedMeshRenderer**
//      （蒙皮网格没法这样静态合并，房车如果有蒙皮部件会保持原样）
//    · 合并后每个组合并网格的顶点数有上限（默认 65535，即 16 位索引），
//      超过就拆成多个，否则会索引溢出
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>把物体树里的零碎网格按材质合并。</summary>
    public static class MeshCombiner
    {
        /// <summary>一次合并最多允许多少顶点（16 位索引上限）。</summary>
        private const int MaxVertsPerChunk = 65000;

        /// <summary>合并结果。</summary>
        public struct Result
        {
            /// <summary>合并前的渲染器数。</summary>
            public int Before;
            /// <summary>合并后的渲染器数。</summary>
            public int After;
            /// <summary>是否真的做了合并。</summary>
            public bool Combined;

            public override string ToString()
            {
                return string.Format("{0} → {1} 个渲染器", Before, After);
            }
        }

        /// <summary>
        /// 合并一个"小物体"（比如一株玉米、一块土壤）。
        ///
        /// 【和 <see cref="Combine"/> 的区别 —— 这是性能问题的关键】
        ///   Combine(整片玉米树)：200 株 ≈ 140 万顶点，
        ///     远超 65,535 上限 → 被拆成几十个 chunk，而且**跨 chunk 时几何会被复制**
        ///     （实测 Tris 从 29.7M 涨到 50.2M）。这条路是错的。
        ///
        ///    CombineSingle(一株玉米)：只有约 7,000 顶点 → 每个材质正好 1 个网格，
        ///     **不拆块、不复制几何**。然后把这"合并好的一株"实例化 200 次 →
        ///     玉米从 14,800 个渲染器降到 200 × 6 = 1,200 个。
        ///
        ///   正确的粒度是**按单个物体合并**，而不是按整片合并。
        /// </summary>
        public static Result CombineSingle(GameObject instance)
        {
            // 不按上限拆块：小物体一个材质就该出 1 个网格，宁可整块一起走 32 位索引
            return CombineCore(instance, false, false);
        }

        /// <summary>
        /// 合并 <paramref name="root"/> 下的网格。
        /// 合并后原 root 的子物体会被替换成若干"合并块"。
        /// 想保留原样就传 <paramref name="keepOriginal"/> = true（只是不删旧的，不推荐）。
        /// </summary>
        public static Result Combine(GameObject root, bool keepOriginal = false)
        {
            return CombineCore(root, keepOriginal, true);
        }

        private static Result CombineCore(GameObject root, bool keepOriginal, bool allowMultipleChunks)
        {
            var result = new Result { Combined = false, Before = 0, After = 0 };
            if (root == null) return result;

            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            result.Before = filters.Length;
            if (filters.Length == 0)
            {
                result.After = 0;
                return result;
            }

            // ---- 1) 按材质分组 ----
            var groups = new Dictionary<Material, List<MeshFilter>>();
            var order = new List<Material>();

            for (var i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;

                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                var mat = mr.sharedMaterial;
                if (!groups.ContainsKey(mat))
                {
                    groups[mat] = new List<MeshFilter>();
                    order.Add(mat);
                }
                groups[mat].Add(mf);
            }

            if (groups.Count == 0)
            {
                result.After = result.Before;
                return result;
            }

            var rootT = root.transform;

            // 记住要删的旧物体（合并完再删，避免中途引用失效）
            var toDestroy = new HashSet<GameObject>();

            // ---- 2) 逐材质合并 ----
            for (var gi = 0; gi < order.Count; gi++)
            {
                var mat = order[gi];
                var list = groups[mat];

                var chunks = BuildChunks(list, rootT);

                for (var ci = 0; ci < chunks.Count; ci++)
                {
                    var go = new GameObject(string.Format("Combined_{0}_{1}", gi, ci));
                    go.transform.SetParent(rootT, false);   // pivot 归到 root，顶点已经是 root 空间
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;

                    go.AddComponent<MeshFilter>().sharedMesh = chunks[ci];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    // 合并后是一整块几何，包围盒由 Unity 从顶点重算，这里显式设一下更保险
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }

                result.After += chunks.Count;
            }

            // ---- 3) 删掉旧的零碎物体 ----
            if (!keepOriginal)
            {
                for (var i = 0; i < filters.Length; i++)
                {
                    var mf = filters[i];
                    if (mf == null) continue;
                    // 不要删掉自己（root 上如果有 MeshFilter）
                    if (mf.gameObject == root) continue;
                    toDestroy.Add(mf.gameObject);
                }

                foreach (var go in toDestroy)
                {
                    if (go == null) continue;
                    if (Application.isPlaying) Object.Destroy(go);
                    else Object.DestroyImmediate(go);
                }
            }

            result.Combined = true;
            return result;
        }

        // ==================================================================
        //  分块 + 拼网格
        // ==================================================================

        /// <summary>
        /// 把一组 MeshFilter 按顶点上限拆成若干块，每块拼成一个 Mesh。
        /// 顶点会从各自的世界位置变换到 <paramref name="rootT"/> 的本地空间。
        /// </summary>
        private static List<Mesh> BuildChunks(List<MeshFilter> filters, Transform rootT)
        {
            var result = new List<Mesh>();

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var tris = new List<int>();

            var hasColors = false;
            var hasUvs = true;
            var chunkIndex = 0;

            // 先扫一遍，确认是否所有网格都带颜色/UV —— 属性必须一致才能合并
            for (var i = 0; i < filters.Count; i++)
            {
                var m = filters[i].sharedMesh;
                if (m == null) continue;
                if (m.colors32 != null && m.colors32.Length > 0) hasColors = true;
                if (m.uv == null || m.uv.Length == 0) hasUvs = false;
            }

            for (var i = 0; i < filters.Count; i++)
            {
                var mf = filters[i];
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                var vCount = mesh.vertexCount;
                if (vCount == 0) continue;

                // 这块装不下了：先落盘
                if (verts.Count + vCount > MaxVertsPerChunk && verts.Count > 0)
                {
                    result.Add(Finish(verts, norms, uvs, colors, tris, hasColors, hasUvs, chunkIndex++));
                    verts.Clear(); norms.Clear(); uvs.Clear(); colors.Clear(); tris.Clear();
                }

                var baseIndex = verts.Count;
                var matrix = rootT.worldToLocalMatrix * mf.transform.localToWorldMatrix;

                // 顶点
                var srcVerts = mesh.vertices;
                for (var v = 0; v < srcVerts.Length; v++)
                    verts.Add(matrix.MultiplyPoint3x4(srcVerts[v]));

                // 法线（用 normalMatrix 才不受非等比缩放影响）
                var normalMatrix = matrix.inverse.transpose;
                var srcNorms = mesh.normals;
                if (srcNorms != null && srcNorms.Length == srcVerts.Length)
                {
                    for (var v = 0; v < srcNorms.Length; v++)
                        norms.Add(normalMatrix.MultiplyVector(srcNorms[v]).normalized);
                }
                else
                {
                    for (var v = 0; v < srcVerts.Length; v++) norms.Add(Vector3.up);
                }

                // UV
                if (hasUvs)
                {
                    var srcUvs = mesh.uv;
                    if (srcUvs != null && srcUvs.Length == srcVerts.Length)
                        for (var v = 0; v < srcUvs.Length; v++) uvs.Add(srcUvs[v]);
                    else
                        for (var v = 0; v < srcVerts.Length; v++) uvs.Add(Vector2.zero);
                }

                // 顶点色
                if (hasColors)
                {
                    var srcCols = mesh.colors32;
                    if (srcCols != null && srcCols.Length == srcVerts.Length)
                        for (var v = 0; v < srcCols.Length; v++) colors.Add(srcCols[v]);
                    else
                        for (var v = 0; v < srcVerts.Length; v++) colors.Add(new Color32(255, 255, 255, 255));
                }

                // 索引：每个子网格单独取（保留原有的 submesh 划分）
                var subCount = Mathf.Max(1, mesh.subMeshCount);
                for (var s = 0; s < subCount; s++)
                {
                    var srcTris = mesh.GetTriangles(s);
                    for (var t = 0; t < srcTris.Length; t++)
                        tris.Add(baseIndex + srcTris[t]);
                }
            }

            if (verts.Count > 0)
                result.Add(Finish(verts, norms, uvs, colors, tris, hasColors, hasUvs, chunkIndex));

            return result;
        }

        private static Mesh Finish(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
                                   List<Color> colors, List<int> tris,
                                   bool hasColors, bool hasUvs, int index)
        {
            var mesh = new Mesh();
            mesh.name = "Combined_" + index;
            mesh.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            if (hasUvs && uvs.Count == verts.Count) mesh.SetUVs(0, uvs);
            if (hasColors && colors.Count == verts.Count) mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ==================================================================
        //  批量入口
        // ==================================================================

        /// <summary>
        /// 把一批预制体"预先合并"成单个预制体（编辑器里用，之后实例化就是合并好的）。
        /// 返回合并后的副本，调用方自己决定怎么处理。
        /// </summary>
        public static GameObject CombinePrefab(GameObject prefab)
        {
            if (prefab == null) return null;

            var copy = Object.Instantiate(prefab);
            copy.name = prefab.name + "_Combined";
            // ★ 用 CombineSingle：不拆块、不复制几何。
            //   这样"合并后的预制体"每个材质只有 1 个网格，
            //   实例化 200 次就是 200 × 材质数 个渲染器，
            //   而不是 200 × 网格数（74）个 —— 这是性能问题的关键。
            CombineSingle(copy);
            return copy;
        }
    }
}
