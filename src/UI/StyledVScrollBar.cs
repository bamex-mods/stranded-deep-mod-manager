using System;
using System.Drawing;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal sealed class StyledVScrollBar : Control
    {
        private int _value;
        private int _maximum;
        private int _largeChange = 1;
        private bool _dragging;
        private int _dragOffset;

        public event EventHandler ValueChanged;

        public StyledVScrollBar()
        {
            Width = 10;
            TabStop = false;
            Cursor = Cursors.Hand;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped =
                    Math.Max(
                        0,
                        Math.Min(
                            _maximum,
                            value));

                if (_value == clamped)
                    return;

                _value = clamped;
                Invalidate();

                EventHandler handler =
                    ValueChanged;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public void SetRange(
            int maximum,
            int largeChange)
        {
            _maximum =
                Math.Max(
                    0,
                    maximum);

            _largeChange =
                Math.Max(
                    1,
                    largeChange);

            if (_value > _maximum)
                _value = _maximum;

            Visible =
                _maximum > 0;

            Invalidate();
        }

        public void ScrollBy(
            int delta)
        {
            Value =
                _value + delta;
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(
                Theme.Panel);

            using (SolidBrush track =
                new SolidBrush(
                    Theme.Panel))
            {
                e.Graphics.FillRectangle(
                    track,
                    ClientRectangle);
            }

            Rectangle thumb =
                GetThumbRectangle();

            if (thumb.Height <= 0)
                return;

            Color thumbColor =
                _dragging
                    ? Theme.BorderSelected
                    : Theme.Border;

            using (SolidBrush brush =
                new SolidBrush(
                    thumbColor))
            {
                e.Graphics.FillRectangle(
                    brush,
                    thumb);
            }
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button !=
                MouseButtons.Left)
            {
                return;
            }

            Rectangle thumb =
                GetThumbRectangle();

            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset =
                    e.Y - thumb.Top;

                Capture = true;
                Invalidate();
                return;
            }

            if (e.Y < thumb.Top)
                ScrollBy(-_largeChange);
            else
                ScrollBy(_largeChange);
        }

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging)
                return;

            Rectangle thumb =
                GetThumbRectangle();

            int trackHeight =
                Math.Max(
                    1,
                    ClientSize.Height -
                    thumb.Height);

            int y =
                e.Y -
                _dragOffset;

            y =
                Math.Max(
                    0,
                    Math.Min(
                        trackHeight,
                        y));

            if (_maximum <= 0)
            {
                Value = 0;
                return;
            }

            Value =
                (int)Math.Round(
                    ((double)y /
                     (double)trackHeight) *
                    (double)_maximum);
        }

        protected override void OnMouseUp(
            MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button ==
                MouseButtons.Left)
            {
                _dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        private Rectangle GetThumbRectangle()
        {
            if (_maximum <= 0 ||
                ClientSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int viewport =
                Math.Max(
                    1,
                    _largeChange);

            int content =
                viewport +
                _maximum;

            int thumbHeight =
                Math.Max(
                    34,
                    (int)Math.Round(
                        ((double)viewport /
                         (double)content) *
                        ClientSize.Height));

            thumbHeight =
                Math.Min(
                    ClientSize.Height,
                    thumbHeight);

            int track =
                Math.Max(
                    0,
                    ClientSize.Height -
                    thumbHeight);

            int top =
                _maximum <= 0
                    ? 0
                    : (int)Math.Round(
                        ((double)_value /
                         (double)_maximum) *
                        track);

            return new Rectangle(
                2,
                top,
                Math.Max(
                    2,
                    ClientSize.Width - 4),
                thumbHeight);
        }
    }
}