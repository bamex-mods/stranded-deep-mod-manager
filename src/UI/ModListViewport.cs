using System;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal sealed class ModListViewport : Panel
    {
        public ModListViewport()
        {
            TabStop = true;

            SetStyle(
                ControlStyles.Selectable |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        protected override void OnMouseEnter(
            EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }
    }
}