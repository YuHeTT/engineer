using UnityEngine;

namespace Engineer.UI
{
    /// <summary>
    /// 白盒元素的「皮肤角色」标签。
    ///
    /// 白盒构建器创建元素时会顺手贴上这个角色（<see cref="UiWhiteboxBuilder"/> 走 UiFactory，
    /// 工厂只负责 Create + 上色，角色在这里登记）。编辑器里的 Tools ▸ UI ▸ 皮肤 系列菜单据此
    /// 把 Assets/UIresources/EPS素材 下的美术图自动填到 <c>Image.sprite</c> ——
    /// 于是「重跑构建 → 一键套皮」就能拿回带素材的界面，不用手工再拖一遍。
    ///
    /// 运行时这个组件不产生任何行为，只是一张记号牌。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSkinSlot : MonoBehaviour
    {
        [Tooltip("该元素在界面里承担的角色；编辑器据此挑素材。")]
        public UiSkinRole role = UiSkinRole.None;

        /// <summary>给 GameObject 打角色标签；<paramref name="newRole"/> 为 None 时不添加组件。</summary>
        public static UiSkinSlot Set(GameObject target, UiSkinRole newRole)
        {
            if (target == null || newRole == UiSkinRole.None) return null;

            var slot = target.GetComponent<UiSkinSlot>();
            if (slot == null) slot = target.AddComponent<UiSkinSlot>();
            slot.role = newRole;
            return slot;
        }
    }

    /// <summary>白盒界面里需要套素材的元素角色。<see cref="None"/> = 不套（纯色块）。</summary>
    public enum UiSkinRole
    {
        None = 0,

        /// <summary>全屏底图：初始界面主视觉、3D 画面占位。</summary>
        ScreenBackdrop = 1,

        /// <summary>弹窗背后压暗的一层。</summary>
        ScreenDim = 2,

        /// <summary>主面板（弹窗、卡片墙、结算页）。</summary>
        Panel = 3,

        /// <summary>次级面板（HUD 数据面板，半透明那种）。</summary>
        PanelSoft = 4,

        /// <summary>选项卡（土地 / 植物选择）。</summary>
        Card = 5,

        /// <summary>普通按钮底图。</summary>
        Button = 6,

        /// <summary>主行动按钮底图。</summary>
        ButtonPrimary = 7,

        /// <summary>进度条底槽。</summary>
        BarTrack = 8,

        /// <summary>进度条填充。</summary>
        BarFill = 9,

        /// <summary>分隔线。</summary>
        Divider = 10,

        /// <summary>数值输入框。</summary>
        InputField = 11,

        /// <summary>圆形仪表盘（给「所有数据」这种格子用）。</summary>
        Gauge = 12,
    }
}
