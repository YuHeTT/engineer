using UnityEngine;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒界面的摆放工具。
    ///
    /// 统一用 anchorMin/anchorMax + offsetMin/offsetMax 定位：
    ///   * 与 pivot 无关，读代码时"这个盒子的四条边在哪"一目了然；
    ///   * 全部相对父级四角说话，改分辨率不会错位。
    /// </summary>
    public static class UiLayout
    {
        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>四边留边铺满父级。</summary>
        public static RectTransform Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            return Place(rt, Vector2.zero, Vector2.one, new Vector2(left, bottom), new Vector2(-right, -top));
        }

        /// <summary>左上角：(x, y) 为距左上角的距离。</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float width, float height)
        {
            return Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -y - height), new Vector2(x + width, -y));
        }

        /// <summary>右上角：(x, y) 为距右上角的距离。</summary>
        public static RectTransform TopRight(RectTransform rt, float x, float y, float width, float height)
        {
            return Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-x - width, -y - height), new Vector2(-x, -y));
        }

        /// <summary>左下角：(x, y) 为距左下角的距离。</summary>
        public static RectTransform BottomLeft(RectTransform rt, float x, float y, float width, float height)
        {
            return Place(rt, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(x + width, y + height));
        }

        /// <summary>右下角：(x, y) 为距右下角的距离。</summary>
        public static RectTransform BottomRight(RectTransform rt, float x, float y, float width, float height)
        {
            return Place(rt, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-x - width, y), new Vector2(-x, y + height));
        }

        /// <summary>相对父级中心定位。</summary>
        public static RectTransform Center(RectTransform rt, float x, float y, float width, float height)
        {
            return Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x - width * 0.5f, y - height * 0.5f), new Vector2(x + width * 0.5f, y + height * 0.5f));
        }

        /// <summary>顶部横条：左右拉伸，距顶 top，高 height。</summary>
        public static RectTransform TopBand(RectTransform rt, float left, float top, float right, float height)
        {
            return Place(rt, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(left, -top - height), new Vector2(-right, -top));
        }

        /// <summary>底部横条：左右拉伸，距底 bottom，高 height。</summary>
        public static RectTransform BottomBand(RectTransform rt, float left, float bottom, float right, float height)
        {
            return Place(rt, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(left, bottom), new Vector2(-right, bottom + height));
        }

        /// <summary>左侧竖条：上下拉伸，宽 width。</summary>
        public static RectTransform LeftBand(RectTransform rt, float left, float top, float width, float bottom)
        {
            return Place(rt, new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(left, bottom), new Vector2(left + width, -top));
        }

        /// <summary>右侧竖条：上下拉伸，宽 width。</summary>
        public static RectTransform RightBand(RectTransform rt, float right, float top, float width, float bottom)
        {
            return Place(rt, new Vector2(1f, 1f), new Vector2(1f, 0f),
                new Vector2(-right - width, bottom), new Vector2(-right, -top));
        }

        /// <summary>
        /// 在父级里按「词条列表」纵向排布：行高固定（顶边对齐，不需要知道父级高度）。
        /// </summary>
        public static RectTransform Row(RectTransform rt, float left, float top, float right, float rowHeight, float rowGap, int index)
        {
            var y = top + index * (rowHeight + rowGap);
            return Place(rt, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(left, -y - rowHeight), new Vector2(-right, -y));
        }

        /// <summary>在父级里按「列」横向排布：列宽固定（左上角对齐）。</summary>
        public static RectTransform Column(RectTransform rt, float left, float top, float columnWidth, float height, float columnGap, int index)
        {
            var x = left + index * (columnWidth + columnGap);
            return Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -top - height), new Vector2(x + columnWidth, -top));
        }

        /// <summary>
        /// 等比网格：把父级横向分 columns 份、纵向分 rows 份，取第 (column, row) 格。
        ///
        /// 用**分数锚点**实现，所以不管画布实际多宽多高都不会溢出 ——
        /// 卡片墙、3×3 数据格、5 行输入列表全部走这里。
        /// 相邻格之间留 gapX / gapY，最外圈再叠加 left / top / right / bottom 内边距。
        /// </summary>
        public static RectTransform GridCell(RectTransform rt, int column, int row, int columns, int rows,
            float left, float top, float right, float bottom, float gapX, float gapY)
        {
            var minX = (float)column / columns;
            var maxX = (float)(column + 1) / columns;
            // 纵向锚点用「从下往上」的比例：row 0 在顶部。
            var maxY = 1f - (float)row / rows;
            var minY = 1f - (float)(row + 1) / rows;

            var insetLeft = left + (column == 0 ? 0f : gapX * 0.5f);
            var insetRight = right + (column == columns - 1 ? 0f : gapX * 0.5f);
            var insetTop = top + (row == 0 ? 0f : gapY * 0.5f);
            var insetBottom = bottom + (row == rows - 1 ? 0f : gapY * 0.5f);

            return Place(rt, new Vector2(minX, minY), new Vector2(maxX, maxY),
                new Vector2(insetLeft, insetBottom), new Vector2(-insetRight, -insetTop));
        }

        /// <summary>
        /// 左半区：按比例切分（fraction=0.5 即左半），右侧留 gap 与右半区隔开。
        /// 用分数锚点，因此不需要知道父级有多宽。
        /// </summary>
        public static RectTransform SplitLeft(RectTransform rt, float fraction, float gap, float left, float top, float right, float bottom)
        {
            return Place(rt, new Vector2(0f, 0f), new Vector2(fraction, 1f),
                new Vector2(left, bottom), new Vector2(-right - gap, -top));
        }

        /// <summary>右半区：与 <see cref="SplitLeft"/> 成对使用。</summary>
        public static RectTransform SplitRight(RectTransform rt, float fraction, float gap, float left, float top, float right, float bottom)
        {
            return Place(rt, new Vector2(fraction, 0f), new Vector2(1f, 1f),
                new Vector2(left + gap, bottom), new Vector2(-right, -top));
        }
    }
}
