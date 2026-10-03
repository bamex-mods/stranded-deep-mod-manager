using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StrandedDeepModManager.Assets;

namespace StrandedDeepModManager.UI
{
    internal sealed class ScreenshotViewerForm :
        BorderlessForm
    {
        private readonly IList<Image> _images;
        private readonly IList<string> _captions;

        private readonly PictureBox _image =
            new PictureBox();

        private readonly Label _caption =
            new Label();

        private readonly Label _counter =
            new Label();

        private readonly Button _previous =
            new Button();

        private readonly Button _next =
            new Button();

        private readonly Button _close =
            new Button();

        private int _index;

        public ScreenshotViewerForm(
            IList<Image> images,
            IList<string> captions,
            int startIndex)
        {
            _images =
                images ??
                new List<Image>();

            _captions =
                captions ??
                new List<string>();

            _index =
                Math.Max(
                    0,
                    Math.Min(
                        _images.Count - 1,
                        startIndex));

            Text =
                "Screenshots";

            BackColor =
                Theme.WindowBackground;

            ClientSize =
                new Size(
                    1000,
                    700);

            MinimumSize =
                new Size(
                    720,
                    500);

            StartPosition =
                FormStartPosition.CenterParent;

            KeyPreview = true;

            BuildUi();
            ShowCurrent();

            KeyDown += ViewerKeyDown;
        }

        private void BuildUi()
        {
            Panel chrome =
                new Panel();

            chrome.Dock =
                DockStyle.Top;

            chrome.Height = 38;

            chrome.BackColor =
                Theme.PanelRaised;

            chrome.MouseDown += delegate(
                object sender,
                MouseEventArgs e)
            {
                if (e.Button ==
                    MouseButtons.Left)
                {
                    BeginWindowDrag();
                }
            };

            Label title =
                new Label();

            title.AutoSize = true;

            title.Text =
                "Screenshots";

            title.ForeColor =
                Theme.TextPrimary;

            title.Font =
                Theme.UiFont(
                    10F,
                    FontStyle.Bold);

            title.Location =
                new Point(
                    12,
                    10);

            _close.Dock =
                DockStyle.Right;

            _close.Width = 42;

            _close.Text = "";

            _close.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Close);

            Theme.StyleHeaderButton(
                _close,
                false);

            _close.FlatAppearance.BorderSize = 0;

            _close.Click += delegate
            {
                Close();
            };

            chrome.Controls.Add(
                title);

            chrome.Controls.Add(
                _close);

            Controls.Add(
                chrome);

            Panel footer =
                new Panel();

            footer.Dock =
                DockStyle.Bottom;

            footer.Height = 58;

            footer.BackColor =
                Theme.PanelRaised;

            _previous.Size =
                new Size(
                    54,
                    38);

            _previous.Location =
                new Point(
                    12,
                    10);

            _previous.Text = "";

            _previous.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Previous);

            Theme.StyleHeaderButton(
                _previous,
                false);

            _previous.Click += delegate
            {
                MoveBy(-1);
            };

            _next.Size =
                new Size(
                    54,
                    38);

            _next.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Right;

            _next.Location =
                new Point(
                    footer.Width -
                    _next.Width -
                    12,
                    10);

            _next.Text = "";

            _next.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Next);

            Theme.StyleHeaderButton(
                _next,
                false);

            _next.Click += delegate
            {
                MoveBy(1);
            };

            _counter.AutoSize = false;

            _counter.Size =
                new Size(
                    90,
                    38);

            _counter.Location =
                new Point(
                    72,
                    10);

            _counter.ForeColor =
                Theme.TextSecondary;

            _counter.TextAlign =
                ContentAlignment.MiddleCenter;

            _counter.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);

            _caption.Anchor =
                AnchorStyles.Left |
                AnchorStyles.Right |
                AnchorStyles.Top;

            _caption.Location =
                new Point(
                    172,
                    10);

            _caption.Size =
                new Size(
                    footer.Width - 344,
                    38);

            _caption.ForeColor =
                Theme.TextPrimary;

            _caption.TextAlign =
                ContentAlignment.MiddleCenter;

            _caption.AutoEllipsis = true;

            _caption.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);

            footer.Resize += delegate
            {
                _next.Left =
                    footer.ClientSize.Width -
                    _next.Width -
                    12;

                _caption.Left = 172;

                _caption.Width =
                    Math.Max(
                        120,
                        _next.Left -
                        _caption.Left -
                        12);
            };

            footer.Controls.Add(
                _previous);

            footer.Controls.Add(
                _counter);

            footer.Controls.Add(
                _caption);

            footer.Controls.Add(
                _next);

            Controls.Add(
                footer);

            _image.Dock =
                DockStyle.Fill;

            _image.BackColor =
                Color.Black;

            _image.SizeMode =
                PictureBoxSizeMode.Zoom;

            _image.Cursor =
                Cursors.Hand;

            _image.DoubleClick += delegate
            {
                ToggleWindowMaximize();
            };

            Controls.Add(
                _image);

            _image.BringToFront();
            chrome.BringToFront();
            footer.BringToFront();
        }

        private void ViewerKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode ==
                Keys.Escape)
            {
                Close();
                return;
            }

            if (e.KeyCode ==
                Keys.Left)
            {
                MoveBy(-1);
                return;
            }

            if (e.KeyCode ==
                Keys.Right)
            {
                MoveBy(1);
                return;
            }
        }

        private void MoveBy(
            int delta)
        {
            if (_images.Count == 0)
                return;

            _index += delta;

            if (_index < 0)
                _index = _images.Count - 1;

            if (_index >= _images.Count)
                _index = 0;

            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (_images.Count == 0)
            {
                _image.Image = null;
                _caption.Text = "";
                _counter.Text = "0 / 0";
                return;
            }

            _image.Image =
                _images[_index];

            _counter.Text =
                String.Format(
                    "{0} / {1}",
                    _index + 1,
                    _images.Count);

            if (_index <
                _captions.Count)
            {
                _caption.Text =
                    _captions[_index] ?? "";
            }
            else
            {
                _caption.Text = "";
            }
        }
    }
}