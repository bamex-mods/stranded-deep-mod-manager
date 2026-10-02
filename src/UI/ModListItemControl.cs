using System;
using System.Drawing;
using System.Windows.Forms;
using StrandedDeepModManager.Assets;

namespace StrandedDeepModManager.UI
{
    internal sealed class ModListItemControl : UserControl
    {
        private readonly PictureBox _identity = new PictureBox();
        private readonly Label _title = new Label();
        private readonly Label _subtitle = new Label();
        private readonly Label _version = new Label();
        private readonly Panel _badge = new Panel();
        private readonly PictureBox _badgeIcon = new PictureBox();
        private readonly Label _badgeText = new Label();
        private readonly ToolTip _statusToolTip = new ToolTip();

        private bool _selected;
        private string _locale;

        public event EventHandler ItemSelected;

        public PackageStatus Status { get; private set; }

        public ModListItemControl(
            PackageStatus status,
            string locale)
        {
            if (status == null)
                throw new ArgumentNullException("status");

            Status = status;
            _locale = locale;

            Height = 78;
            Margin = new Padding(0, 0, 0, 6);
            BackColor = Theme.Panel;
            Cursor = Cursors.Hand;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            _identity.Location = new Point(8, 8);
            _identity.Size = new Size(62, 62);
            _identity.SizeMode = PictureBoxSizeMode.Zoom;
            _identity.Image = UiAssets.Identity(
                status.CatalogPackage.id);
            _identity.BackColor = Color.Transparent;

            _title.Location = new Point(82, 8);
            _title.Size = new Size(188, 20);
            _title.ForeColor = Theme.TextPrimary;
            _title.Font = Theme.UiFont(
                9.5F,
                FontStyle.Bold);
            _title.AutoEllipsis = true;

            _subtitle.Location = new Point(82, 29);
            _subtitle.Size = new Size(188, 31);
            _subtitle.ForeColor = Theme.TextSecondary;
            _subtitle.Font = Theme.UiFont(
                8.25F,
                FontStyle.Regular);
            _subtitle.AutoEllipsis = true;

            _version.Location = new Point(82, 59);
            _version.Size = new Size(120, 16);
            _version.ForeColor = Theme.TextSecondary;
            _version.Font = Theme.UiFont(
                8F,
                FontStyle.Regular);

            _badge.Size = new Size(30, 30);
            _badge.BackColor =
                BackColor;

            _badgeIcon.Location = new Point(2, 2);
            _badgeIcon.Size = new Size(26, 26);
            _badgeIcon.SizeMode =
                PictureBoxSizeMode.Zoom;
            _badgeIcon.BackColor =
                Color.Transparent;

            _badgeText.Location = new Point(25, 0);
            _badgeText.Size = new Size(73, 24);
            _badgeText.TextAlign =
                ContentAlignment.MiddleLeft;
            _badgeText.ForeColor =
                StatusTextColor(
                    status.Kind);
            _badgeText.Font =
                Theme.UiFont(
                    7.75F,
                    FontStyle.Regular);
            _badgeText.AutoEllipsis = true;
            _badgeText.Visible = false;

            _badge.Controls.Add(_badgeIcon);
            _badge.Controls.Add(_badgeText);

            Controls.Add(_identity);
            Controls.Add(_title);
            Controls.Add(_subtitle);
            Controls.Add(_version);
            Controls.Add(_badge);

            WireSelection(this);
            UpdateContent(locale);
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            BackColor =
                selected
                    ? Theme.PanelSelected
                    : Theme.Panel;

            _badge.BackColor =
                BackColor;

            Invalidate();
        }

        public void UpdateContent(string locale)
        {
            _locale = locale;

            CatalogPackage package =
                Status.CatalogPackage;

            _title.Text =
                DisplayNames.Mod(
                    package.name ?? package.id);
            _subtitle.Text =
                package.description ?? "";

            _version.Text =
                package.latest == null ||
                String.IsNullOrWhiteSpace(
                    package.latest.version)
                    ? ""
                    : "v" + package.latest.version;

            _badgeText.Text =
                LocalizedStatus(
                    Status.Kind,
                    locale);

            _badge.BackColor =
                BackColor;

            _badgeIcon.Image =
                UiAssets.SystemIcon(
                    StatusIconKey(
                        Status.Kind));

            string statusText =
                LocalizedStatus(
                    Status.Kind,
                    locale);

            _statusToolTip.SetToolTip(
                _badge,
                statusText);

            _statusToolTip.SetToolTip(
                _badgeIcon,
                statusText);
        }

        protected override void OnResize(
            EventArgs e)
        {
            base.OnResize(e);

            _badge.Location =
                new Point(
                    Math.Max(
                        220,
                        Width - 38),
                    24);

            int rightEdge =
                _badge.Left - 8;

            _title.Width =
                Math.Max(
                    100,
                    rightEdge - _title.Left);

            _subtitle.Width =
                Math.Max(
                    100,
                    rightEdge - _subtitle.Left);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_selected)
            {
                using (Pen border =
                    new Pen(
                        Theme.BorderSelected,
                        1.5F))
                {
                    e.Graphics.DrawRectangle(
                        border,
                        new Rectangle(
                            0,
                            0,
                            Width - 1,
                            Height - 1));
                }

                return;
            }

            using (Pen separator =
                new Pen(
                    Color.FromArgb(
                        85,
                        Theme.Border),
                    1F))
            {
                e.Graphics.DrawLine(
                    separator,
                    8,
                    Height - 1,
                    Width - 8,
                    Height - 1);
            }
        }

        private void WireSelection(
            Control control)
        {
            control.Click += delegate
            {
                EventHandler handler =
                    ItemSelected;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            };

            foreach (Control child
                in control.Controls)
            {
                WireSelection(child);
            }
        }

        private static Color StatusTextColor(
            PackageStatusKind kind)
        {
            switch (kind)
            {
                case PackageStatusKind.Installed:
                    return Color.FromArgb(
                        0x6D,
                        0xD7,
                        0xA2);

                case PackageStatusKind.UpdateAvailable:
                    return Theme.AccentCyan;

                case PackageStatusKind.NotInstalled:
                    return Theme.AccentSand;

                case PackageStatusKind.DifferentBuild:
                case PackageStatusKind.Modified:
                    return Theme.Warning;

                case PackageStatusKind.PackageMissing:
                case PackageStatusKind.Error:
                    return Theme.Error;

                default:
                    return Theme.TextSecondary;
            }
        }
        private static string StatusIconKey(
            PackageStatusKind kind)
        {
            switch (kind)
            {
                case PackageStatusKind.Installed:
                    return AssetKeys.SystemUi.Success;

                case PackageStatusKind.UpdateAvailable:
                    return AssetKeys.SystemUi.Updates;

                case PackageStatusKind.NotInstalled:
                    return AssetKeys.SystemUi.Download;

                case PackageStatusKind.DifferentBuild:
                case PackageStatusKind.Modified:
                    return AssetKeys.SystemUi.Warning;

                case PackageStatusKind.PackageMissing:
                case PackageStatusKind.Error:
                    return AssetKeys.SystemUi.Error;

                default:
                    return AssetKeys.SystemUi.Info;
            }
        }

        private static string LocalizedStatus(
            PackageStatusKind kind,
            string locale)
        {
            bool ru =
                String.Equals(
                    locale,
                    "ru",
                    StringComparison.OrdinalIgnoreCase);

            switch (kind)
            {
                case PackageStatusKind.NotInstalled:
                    return ru
                        ? "Установить"
                        : "Install";

                case PackageStatusKind.Installed:
                    return ru
                        ? "Установлен"
                        : "Installed";

                case PackageStatusKind.UpdateAvailable:
                    return ru
                        ? "Обновление"
                        : "Update";

                case PackageStatusKind.DifferentBuild:
                    return ru
                        ? "Другой билд"
                        : "Different";

                case PackageStatusKind.Modified:
                    return ru
                        ? "Изменён"
                        : "Modified";

                case PackageStatusKind.PackageMissing:
                    return ru
                        ? "Недоступен"
                        : "Unavailable";

                default:
                    return ru
                        ? "Ошибка"
                        : "Error";
            }
        }
    }
}
