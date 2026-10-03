using System;
using System.Drawing;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal sealed class ProductTextView : UserControl
    {
        private readonly Panel _viewport =
            new Panel();

        private readonly Label _text =
            new Label();

        private readonly StyledVScrollBar _scroll =
            new StyledVScrollBar();

        public ProductTextView()
        {
            BackColor = Theme.Panel;
            ForeColor = Theme.TextPrimary;

            _viewport.Dock =
                DockStyle.Fill;

            _viewport.BackColor =
                Theme.Panel;

            _viewport.TabStop = true;

            _text.AutoSize = true;
            _text.ForeColor =
                Theme.TextPrimary;

            _text.BackColor =
                Color.Transparent;

            _text.Font =
                Theme.UiFont(
                    9.75F,
                    FontStyle.Regular);

            _text.Location =
                new Point(
                    14,
                    12);

            _viewport.Controls.Add(
                _text);

            _scroll.Dock =
                DockStyle.Right;

            _scroll.Width = 9;

            _scroll.ValueChanged += delegate
            {
                LayoutText();
            };

            Controls.Add(
                _viewport);

            Controls.Add(
                _scroll);

            _scroll.BringToFront();

            _viewport.MouseWheel +=
                ViewportMouseWheel;

            _text.MouseWheel +=
                ViewportMouseWheel;

            _viewport.MouseEnter += delegate
            {
                _viewport.Focus();
            };

            Resize += delegate
            {
                LayoutText();
            };
        }

        public void SetText(
            string text)
        {
            _text.Text =
                text ?? "";

            _scroll.Value = 0;

            LayoutText();
        }

        private void ViewportMouseWheel(
            object sender,
            MouseEventArgs e)
        {
            int step =
                Math.Max(
                    36,
                    Math.Abs(e.Delta) / 3);

            _scroll.ScrollBy(
                e.Delta > 0
                    ? -step
                    : step);
        }

        private void LayoutText()
        {
            int width =
                Math.Max(
                    100,
                    _viewport.ClientSize.Width - 28);

            _text.MaximumSize =
                new Size(
                    width,
                    0);

            _text.Location =
                new Point(
                    14,
                    12 - _scroll.Value);

            int contentHeight =
                _text.Height + 24;

            int maximum =
                Math.Max(
                    0,
                    contentHeight -
                    _viewport.ClientSize.Height);

            _scroll.SetRange(
                maximum,
                Math.Max(
                    1,
                    _viewport.ClientSize.Height));
        }
    }
}