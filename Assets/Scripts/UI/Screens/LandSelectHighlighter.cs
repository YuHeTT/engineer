using TMPro;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>
    /// 土地选择页的「● 已选」标记。
    ///
    /// 【为什么需要】套了 EPS 皮肤之后四张卡用的是同一张卡面，颜色也被统一成白色，
    /// 光看卡片分不出哪张被选过。这里在读档/返回时按 <see cref="GameContext.LandTypeId"/>
    /// 点亮对应卡片右上角的小字。
    ///
    /// 【为什么不直接写在 OnEnable 里】本组件挂在屏根节点上，屏每次 SetActive 都会走 OnEnable，
    /// 正好覆盖"从观察页返回"这个时机。
    /// </summary>
    [DisallowMultipleComponent]
    public class LandSelectHighlighter : MonoBehaviour
    {
        [Tooltip("四张卡右上角的「● 已选」，顺序与 Cell_Land1..4 一致。")]
        public TMP_Text[] selectedTags = new TMP_Text[4];

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>按当前选择刷新四张卡的标记。没选过时全部隐藏。</summary>
        public void Refresh()
        {
            var flow = ResolveFlow();
            var context = flow != null ? flow.Context : null;

            var index = -1;
            if (LandPreviewContent.LandChosen && context != null)
                index = LandPreviewContent.IndexFor(context.LandTypeId);

            for (var i = 0; i < selectedTags.Length; i++)
            {
                if (selectedTags[i] == null) continue;
                selectedTags[i].gameObject.SetActive(i == index);
            }
        }

        private GameFlowController ResolveFlow()
        {
            var screen = GetComponent<PlaceholderScreen>();
            if (screen != null && screen.Flow != null) return screen.Flow;

            return FindObjectOfType<GameFlowController>();
        }
    }
}
