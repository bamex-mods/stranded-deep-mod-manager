using System.Drawing;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal sealed class DarkMenuColorTable :
        ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground
        {
            get { return Theme.PanelRaised; }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return Theme.PanelRaised; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return Theme.PanelRaised; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return Theme.PanelRaised; }
        }

        public override Color MenuItemSelected
        {
            get
            {
                return Color.FromArgb(
                    0x12,
                    0x3A,
                    0x4B);
            }
        }

        public override Color MenuItemBorder
        {
            get { return Theme.BorderSelected; }
        }

        public override Color MenuBorder
        {
            get { return Theme.BorderSelected; }
        }

        public override Color SeparatorDark
        {
            get { return Theme.Border; }
        }

        public override Color SeparatorLight
        {
            get { return Theme.PanelRaised; }
        }
    }

    internal static class DarkMenus
    {
        public static void Style(
            ContextMenuStrip menu)
        {
            menu.Renderer =
                new ToolStripProfessionalRenderer(
                    new DarkMenuColorTable());

            menu.BackColor =
                Theme.PanelRaised;

            menu.ForeColor =
                Theme.TextPrimary;

            menu.Font =
                Theme.UiFont(
                    9.5F,
                    FontStyle.Regular);

            menu.ShowImageMargin = true;
            menu.ShowCheckMargin = false;
            menu.Padding =
                new Padding(
                    3,
                    4,
                    3,
                    4);
        }

        public static void StyleItem(
            ToolStripMenuItem item,
            Image image,
            bool danger)
        {
            item.AutoSize = false;

            item.Size =
                new Size(
                    228,
                    36);

            item.Image =
                image;

            item.ImageScaling =
                ToolStripItemImageScaling.None;

            item.DisplayStyle =
                ToolStripItemDisplayStyle.ImageAndText;

            item.ForeColor =
                danger
                    ? Theme.Error
                    : Theme.TextPrimary;

            item.Padding =
                new Padding(
                    6,
                    0,
                    6,
                    0);
        }

        public static ToolStripSeparator Separator()
        {
            ToolStripSeparator separator =
                new ToolStripSeparator();

            separator.AutoSize = false;

            separator.Size =
                new Size(
                    228,
                    7);

            return separator;
        }
    }
}