using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    public class BorderlessForm : Form
    {
        private const int WmNcHitTest = 0x0084;

        private const int HtClient = 1;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            int wParam,
            int lParam);

        protected BorderlessForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(1);
            AllowBorderlessResize = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected bool AllowBorderlessResize
        {
            get;
            set;
        }

        protected void BeginWindowDrag()
        {
            if (WindowState == FormWindowState.Maximized)
                return;

            ReleaseCapture();

            SendMessage(
                Handle,
                WmNcLButtonDown,
                HtCaption,
                0);
        }

        protected void ToggleWindowMaximize()
        {
            WindowState =
                WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
        }

        protected override void WndProc(
            ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg != WmNcHitTest ||
                !AllowBorderlessResize ||
                WindowState == FormWindowState.Maximized)
            {
                return;
            }

            if ((int)m.Result != HtClient)
                return;

            long value =
                m.LParam.ToInt64();

            int screenX =
                unchecked((short)(value & 0xFFFF));

            int screenY =
                unchecked((short)((value >> 16) & 0xFFFF));

            Point point =
                PointToClient(
                    new Point(
                        screenX,
                        screenY));

            int grip = 7;

            bool left =
                point.X >= 0 &&
                point.X < grip;

            bool right =
                point.X <= ClientSize.Width &&
                point.X > ClientSize.Width - grip;

            bool top =
                point.Y >= 0 &&
                point.Y < grip;

            bool bottom =
                point.Y <= ClientSize.Height &&
                point.Y > ClientSize.Height - grip;

            if (left && top)
                m.Result = (IntPtr)HtTopLeft;
            else if (right && top)
                m.Result = (IntPtr)HtTopRight;
            else if (left && bottom)
                m.Result = (IntPtr)HtBottomLeft;
            else if (right && bottom)
                m.Result = (IntPtr)HtBottomRight;
            else if (left)
                m.Result = (IntPtr)HtLeft;
            else if (right)
                m.Result = (IntPtr)HtRight;
            else if (top)
                m.Result = (IntPtr)HtTop;
            else if (bottom)
                m.Result = (IntPtr)HtBottom;
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            using (Pen border =
                new Pen(
                    Theme.Border,
                    1F))
            {
                e.Graphics.DrawRectangle(
                    border,
                    0,
                    0,
                    ClientSize.Width - 1,
                    ClientSize.Height - 1);
            }
        }
    }
}