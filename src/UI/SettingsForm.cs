using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StrandedDeepModManager.Assets;

namespace StrandedDeepModManager.UI
{
    internal sealed class SettingsForm : BorderlessForm
    {
        private readonly TextBox _gameRoot = new TextBox();
        private readonly Label _catalog = new Label();
        private readonly Label _cache = new Label();

        private readonly string _locale;

        public string GameRoot
        {
            get
            {
                return _gameRoot.Text.Trim();
            }
        }

        public SettingsForm(
            string currentGameRoot,
            string catalogStatus,
            string dataRoot,
            string locale)
        {
            _locale = locale;

            Text =
                IsRu
                    ? "Настройки"
                    : "Settings";

            StartPosition =
                FormStartPosition.CenterParent;

            FormBorderStyle =
                FormBorderStyle.None;

            AllowBorderlessResize =
                false;

            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(720, 276);

            BackColor = Theme.WindowBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.UiFont(
                9F,
                FontStyle.Regular);

            BuildUi();

            _gameRoot.Text =
                currentGameRoot ?? "";

            _catalog.Text =
                catalogStatus ?? "";

            _cache.Text =
                dataRoot ?? "";
        }

        private bool IsRu
        {
            get
            {
                return String.Equals(
                    _locale,
                    "ru",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private void BuildUi()
        {
            TableLayoutPanel root =
                new TableLayoutPanel();

            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(
                18,
                46,
                18,
                18);
            root.ColumnCount = 1;
            root.RowCount = 7;
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));
            root.RowStyles.Add(
                new RowStyle(
                    SizeType.AutoSize));

            Controls.Add(root);

            Label gameLabel = new Label();
            gameLabel.AutoSize = true;
            gameLabel.ForeColor = Theme.TextPrimary;
            gameLabel.Text =
                IsRu
                    ? "Папка Stranded Deep"
                    : "Stranded Deep folder";

            root.Controls.Add(
                gameLabel,
                0,
                0);

            TableLayoutPanel pathRow =
                new TableLayoutPanel();

            pathRow.Dock = DockStyle.Top;
            pathRow.AutoSize = true;
            pathRow.ColumnCount = 3;
            pathRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));
            pathRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    104F));
            pathRow.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    116F));

            _gameRoot.Dock =
                DockStyle.Fill;

            Button browse =
                new Button();

            browse.Text =
                IsRu
                    ? "Обзор..."
                    : "Browse...";

            browse.Dock =
                DockStyle.Fill;

            Theme.StyleNavButton(
                browse,
                false);

            browse.Click += delegate
            {
                Browse();
            };

            Button detect =
                new Button();

            detect.Text =
                IsRu
                    ? "Найти"
                    : "Auto-detect";

            detect.Dock =
                DockStyle.Fill;

            detect.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Search);

            Theme.StyleNavButton(
                detect,
                false);

            detect.Click += delegate
            {
                Detect();
            };

            pathRow.Controls.Add(
                _gameRoot,
                0,
                0);

            pathRow.Controls.Add(
                browse,
                1,
                0);

            pathRow.Controls.Add(
                detect,
                2,
                0);

            root.Controls.Add(
                pathRow,
                0,
                1);

            Label catalogLabel =
                new Label();

            catalogLabel.AutoSize = true;
            catalogLabel.Margin =
                new Padding(
                    0,
                    16,
                    0,
                    2);

            catalogLabel.ForeColor =
                Theme.TextPrimary;

            catalogLabel.Text =
                IsRu
                    ? "Каталог"
                    : "Catalog";

            root.Controls.Add(
                catalogLabel,
                0,
                2);

            _catalog.AutoSize = true;
            _catalog.ForeColor =
                Theme.TextSecondary;

            root.Controls.Add(
                _catalog,
                0,
                3);

            Label cacheLabel =
                new Label();

            cacheLabel.AutoSize = true;
            cacheLabel.Margin =
                new Padding(
                    0,
                    12,
                    0,
                    2);

            cacheLabel.ForeColor =
                Theme.TextPrimary;

            cacheLabel.Text =
                IsRu
                    ? "Локальный кэш"
                    : "Local cache";

            root.Controls.Add(
                cacheLabel,
                0,
                4);

            _cache.AutoSize = true;
            _cache.ForeColor =
                Theme.TextSecondary;

            root.Controls.Add(
                _cache,
                0,
                5);

            FlowLayoutPanel actions =
                new FlowLayoutPanel();

            actions.FlowDirection =
                FlowDirection.RightToLeft;

            actions.Dock =
                DockStyle.Fill;

            actions.AutoSize = true;

            Button ok =
                new Button();

            ok.Text = "OK";
            ok.DialogResult =
                DialogResult.OK;
            ok.Width = 100;

            Theme.StyleActionButton(
                ok,
                Theme.InstalledGreen,
                Theme.BorderSelected);

            Button cancel =
                new Button();

            cancel.Text =
                IsRu
                    ? "Отмена"
                    : "Cancel";

            cancel.DialogResult =
                DialogResult.Cancel;
            cancel.Width = 100;

            Theme.StyleNavButton(
                cancel,
                false);

            actions.Controls.Add(ok);
            actions.Controls.Add(cancel);

            root.Controls.Add(
                actions,
                0,
                6);

            AcceptButton = ok;
            CancelButton = cancel;

            Panel chrome =
                new Panel();

            chrome.Dock =
                DockStyle.Top;

            chrome.Height = 30;

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
            title.ForeColor =
                Theme.TextPrimary;

            title.BackColor =
                Color.Transparent;

            title.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Bold);

            title.Text =
                IsRu
                    ? "Настройки"
                    : "Settings";

            title.Location =
                new Point(
                    10,
                    7);

            title.MouseDown += delegate(
                object sender,
                MouseEventArgs e)
            {
                if (e.Button ==
                    MouseButtons.Left)
                {
                    BeginWindowDrag();
                }
            };

            Button close =
                new Button();

            close.Size =
                new Size(
                    34,
                    30);

            close.Dock =
                DockStyle.Right;

            close.Text = "";

            close.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Close);

            Theme.StyleHeaderButton(
                close,
                false);

            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor =
                Theme.Error;

            close.Click += delegate
            {
                DialogResult =
                    DialogResult.Cancel;

                Close();
            };

            chrome.Controls.Add(title);
            chrome.Controls.Add(close);

            Controls.Add(chrome);
            chrome.BringToFront();
        }

        private void Detect()
        {
            string detected =
                GameLocator.FindGameRoot();

            if (!String.IsNullOrWhiteSpace(
                detected))
            {
                _gameRoot.Text =
                    detected;

                return;
            }

            MessageBox.Show(
                this,
                IsRu
                    ? "Stranded Deep не найдена автоматически."
                    : "Stranded Deep could not be found automatically.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void Browse()
        {
            using (FolderBrowserDialog dialog =
                new FolderBrowserDialog())
            {
                dialog.Description =
                    IsRu
                        ? "Выберите папку Stranded Deep"
                        : "Select the Stranded Deep game directory";

                dialog.ShowNewFolderButton =
                    false;

                if (Directory.Exists(
                    _gameRoot.Text.Trim()))
                {
                    dialog.SelectedPath =
                        _gameRoot.Text.Trim();
                }

                if (dialog.ShowDialog(this) !=
                    DialogResult.OK)
                {
                    return;
                }

                if (!GameLocator.LooksLikeGameRoot(
                    dialog.SelectedPath))
                {
                    MessageBox.Show(
                        this,
                        IsRu
                            ? "Выбранная папка не похожа на папку Stranded Deep."
                            : "The selected folder does not look like the Stranded Deep game directory.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                _gameRoot.Text =
                    dialog.SelectedPath;
            }
        }
    }
}
