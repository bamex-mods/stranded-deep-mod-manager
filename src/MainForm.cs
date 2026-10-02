using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StrandedDeepModManager
{
    public sealed class MainForm : Form
    {
        private readonly TextBox _gameRoot = new TextBox();
        private readonly Button _browseGame = new Button();
        private readonly Button _detectGame = new Button();
        private readonly Button _refresh = new Button();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();

        private readonly ListView _list = new ListView();
        private readonly TextBox _details = new TextBox();
        private readonly Label _summary = new Label();
        private readonly Label _catalogStatus = new Label();

        private ManagerEngine _engine;
        private IList<PackageStatus> _statuses = new List<PackageStatus>();

        private string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BamEx",
                    "StrandedDeepModManager");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
        }

        public MainForm()
        {
            Text = AppInfo.ProductName + " v" + AppInfo.Version;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);
            Size = new Size(1180, 760);

            BuildUi();
            LoadSettings();

            Shown += delegate
            {
                if (!String.IsNullOrWhiteSpace(_gameRoot.Text))
                    RefreshCatalog(true);
                else
                    ShowNoGameMessage();
            };
        }

        private void BuildUi()
        {
            Font = new Font("Segoe UI", 9F);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            Controls.Add(root);

            TableLayoutPanel paths = new TableLayoutPanel();
            paths.Dock = DockStyle.Top;
            paths.AutoSize = true;
            paths.Padding = new Padding(10, 10, 10, 4);
            paths.ColumnCount = 4;
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75F));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));

            Label gameLabel = new Label();
            gameLabel.Text = "Game:";
            gameLabel.TextAlign = ContentAlignment.MiddleLeft;
            gameLabel.Dock = DockStyle.Fill;
            gameLabel.AutoSize = true;

            _gameRoot.Dock = DockStyle.Fill;

            _browseGame.Text = "Browse...";
            _browseGame.Dock = DockStyle.Fill;
            _browseGame.Click += delegate { BrowseGameRoot(); };

            _detectGame.Text = "Auto-detect";
            _detectGame.Dock = DockStyle.Fill;
            _detectGame.Click += delegate { DetectGameRoot(true); };

            paths.Controls.Add(gameLabel, 0, 0);
            paths.Controls.Add(_gameRoot, 1, 0);
            paths.Controls.Add(_browseGame, 2, 0);
            paths.Controls.Add(_detectGame, 3, 0);

            root.Controls.Add(paths, 0, 0);

            Panel catalogPanel = new Panel();
            catalogPanel.Dock = DockStyle.Fill;
            catalogPanel.Height = 27;
            catalogPanel.Padding = new Padding(10, 0, 10, 4);

            _catalogStatus.AutoSize = true;
            _catalogStatus.Text = "Catalog: " + AppInfo.CatalogUrl;
            _catalogStatus.Dock = DockStyle.Fill;
            catalogPanel.Controls.Add(_catalogStatus);

            root.Controls.Add(catalogPanel, 0, 1);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.AutoSize = true;
            toolbar.Padding = new Padding(10, 0, 10, 6);

            _refresh.Text = "Refresh";
            _refresh.AutoSize = true;
            _refresh.Click += delegate { RefreshCatalog(true); };

            _install.Text = "Install / Update";
            _install.AutoSize = true;
            _install.Click += delegate { InstallSelected(); };

            _uninstall.Text = "Uninstall";
            _uninstall.AutoSize = true;
            _uninstall.Click += delegate { UninstallSelected(); };

            _summary.AutoSize = true;
            _summary.Margin = new Padding(18, 7, 0, 0);

            toolbar.Controls.Add(_refresh);
            toolbar.Controls.Add(_install);
            toolbar.Controls.Add(_uninstall);
            toolbar.Controls.Add(_summary);

            root.Controls.Add(toolbar, 0, 2);

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.GridLines = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.Columns.Add("Mod", 300);
            _list.Columns.Add("Available", 90);
            _list.Columns.Add("Installed", 90);
            _list.Columns.Add("Status", 160);
            _list.Columns.Add("Category", 120);
            _list.SelectedIndexChanged += delegate { ShowSelectedDetails(); };

            root.Controls.Add(_list, 0, 3);

            _details.Dock = DockStyle.Fill;
            _details.Multiline = true;
            _details.ReadOnly = true;
            _details.ScrollBars = ScrollBars.Vertical;
            _details.Font = new Font("Consolas", 9F);
            _details.Margin = new Padding(10);

            root.Controls.Add(_details, 0, 4);
        }

        private void LoadSettings()
        {
            ManagerSettings settings = null;

            try
            {
                if (File.Exists(SettingsPath))
                    settings = JsonUtil.ReadFile<ManagerSettings>(SettingsPath);
            }
            catch
            {
            }

            if (settings == null)
                settings = new ManagerSettings();

            if (!String.IsNullOrWhiteSpace(settings.gameRoot) && GameLocator.LooksLikeGameRoot(settings.gameRoot))
            {
                _gameRoot.Text = settings.gameRoot;
                return;
            }

            DetectGameRoot(false);
        }

        private void SaveSettings()
        {
            ManagerSettings settings = new ManagerSettings();
            settings.gameRoot = _gameRoot.Text.Trim();
            JsonUtil.WriteFile(SettingsPath, settings);
        }

        private void DetectGameRoot(bool showResult)
        {
            string detected = GameLocator.FindGameRoot();

            if (!String.IsNullOrWhiteSpace(detected))
            {
                _gameRoot.Text = detected;
                SaveSettings();

                if (showResult)
                {
                    MessageBox.Show(
                        this,
                        "Stranded Deep found:\r\n" + detected,
                        "Game detected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            else if (showResult)
            {
                MessageBox.Show(
                    this,
                    "Stranded Deep could not be found automatically. Use Browse... to select the game directory.",
                    "Game not found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void BrowseGameRoot()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the Stranded Deep game directory";
                dialog.ShowNewFolderButton = false;

                if (Directory.Exists(_gameRoot.Text.Trim()))
                    dialog.SelectedPath = _gameRoot.Text.Trim();

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (!GameLocator.LooksLikeGameRoot(dialog.SelectedPath))
                {
                    MessageBox.Show(
                        this,
                        "The selected folder does not look like the Stranded Deep game directory.",
                        "Invalid game directory",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _gameRoot.Text = dialog.SelectedPath;
                SaveSettings();
                RefreshCatalog(true);
            }
        }

        private void RefreshCatalog(bool adoptExactMatches)
        {
            SetBusy(true);

            try
            {
                SaveSettings();

                _engine = new ManagerEngine(
                    _gameRoot.Text.Trim(),
                    AppInfo.CatalogUrl);

                _engine.LoadCatalog();
                _catalogStatus.Text = "Catalog: " + _engine.CatalogStatus;

                _statuses = _engine.ScanAll(adoptExactMatches);
                RenderStatuses();
            }
            catch (Exception ex)
            {
                _catalogStatus.Text = "Catalog: unavailable";

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Refresh failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RenderStatuses()
        {
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();

                foreach (PackageStatus status in _statuses)
                {
                    string available = status.CatalogPackage.latest == null
                        ? ""
                        : status.CatalogPackage.latest.version;

                    ListViewItem item = new ListViewItem(status.CatalogPackage.name);
                    item.SubItems.Add(available);
                    item.SubItems.Add(status.InstalledVersion ?? "");
                    item.SubItems.Add(StatusText(status.Kind));
                    item.SubItems.Add(status.CatalogPackage.category ?? "");
                    item.Tag = status;
                    _list.Items.Add(item);
                }
            }
            finally
            {
                _list.EndUpdate();
            }

            int installed = _statuses.Count(
                s => s.Kind == PackageStatusKind.Installed ||
                     s.Kind == PackageStatusKind.UpdateAvailable);

            int updates = _statuses.Count(s => s.Kind == PackageStatusKind.UpdateAvailable);
            int problems = _statuses.Count(
                s => s.Kind == PackageStatusKind.DifferentBuild ||
                     s.Kind == PackageStatusKind.Modified ||
                     s.Kind == PackageStatusKind.PackageMissing ||
                     s.Kind == PackageStatusKind.Error);

            _summary.Text =
                "Packages: " + _statuses.Count +
                "   Installed: " + installed +
                "   Updates: " + updates +
                "   Problems: " + problems;

            ShowSelectedDetails();
        }

        private static string StatusText(PackageStatusKind kind)
        {
            switch (kind)
            {
                case PackageStatusKind.NotInstalled: return "Not installed";
                case PackageStatusKind.Installed: return "Installed";
                case PackageStatusKind.UpdateAvailable: return "Update available";
                case PackageStatusKind.DifferentBuild: return "Different build";
                case PackageStatusKind.Modified: return "Modified / unknown";
                case PackageStatusKind.PackageMissing: return "Package unavailable";
                default: return "Error";
            }
        }

        private PackageStatus SelectedStatus
        {
            get
            {
                if (_list.SelectedItems.Count != 1)
                    return null;

                return _list.SelectedItems[0].Tag as PackageStatus;
            }
        }

        private void ShowSelectedDetails()
        {
            PackageStatus status = SelectedStatus;

            if (status == null)
            {
                _details.Text =
                    "Select a package.\r\n\r\n" +
                    "v0.2 uses the verified public stable catalog and GitHub Release packages.\r\n" +
                    "Downloaded packages are cached under LocalAppData.";
                _install.Enabled = false;
                _uninstall.Enabled = false;
                return;
            }

            CatalogPackage p = status.CatalogPackage;

            _details.Text =
                p.name + "\r\n" +
                "ID: " + p.id + "\r\n" +
                "Available: " + (p.latest == null ? "" : p.latest.version) + "\r\n" +
                "Installed: " + (status.InstalledVersion ?? "-") + "\r\n" +
                "Status: " + StatusText(status.Kind) + "\r\n" +
                "Category: " + (p.category ?? "") + "\r\n" +
                "Cached ZIP: " + (status.PackageZipPath ?? "<not available>") + "\r\n\r\n" +
                (p.description ?? "") + "\r\n\r\n" +
                status.Detail;

            _install.Enabled =
                status.Kind == PackageStatusKind.NotInstalled ||
                status.Kind == PackageStatusKind.UpdateAvailable ||
                status.Kind == PackageStatusKind.DifferentBuild ||
                status.Kind == PackageStatusKind.Modified;

            _uninstall.Enabled =
                status.Kind == PackageStatusKind.Installed ||
                status.Kind == PackageStatusKind.UpdateAvailable ||
                status.Kind == PackageStatusKind.DifferentBuild ||
                status.Kind == PackageStatusKind.Modified;
        }

        private void InstallSelected()
        {
            PackageStatus status = SelectedStatus;
            if (status == null || _engine == null)
                return;

            DialogResult answer = MessageBox.Show(
                this,
                "Install/update " + status.CatalogPackage.name + "?\r\n\r\n" +
                "Stranded Deep must be closed. The catalog, package SHA-256, manifest and package files are verified before installation.",
                "Confirm install",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (answer != DialogResult.OK)
                return;

            SetBusy(true);

            try
            {
                _engine.InstallOrUpdate(status.CatalogPackage);
                MessageBox.Show(
                    this,
                    "Install/update completed successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                RefreshCatalog(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Install/update failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                RefreshCatalog(false);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UninstallSelected()
        {
            PackageStatus status = SelectedStatus;
            if (status == null || _engine == null)
                return;

            DialogResult answer = MessageBox.Show(
                this,
                "Uninstall " + status.CatalogPackage.name + "?\r\n\r\n" +
                "Only package-owned runtime paths will be removed. Persistent save/config data is not deleted.",
                "Confirm uninstall",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.OK)
                return;

            SetBusy(true);

            try
            {
                _engine.Uninstall(status.CatalogPackage);
                MessageBox.Show(
                    this,
                    "Uninstall completed successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                RefreshCatalog(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Uninstall failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                RefreshCatalog(false);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowNoGameMessage()
        {
            _details.Text =
                "Stranded Deep was not found automatically.\r\n\r\n" +
                "Use Auto-detect or Browse... to select the game directory.";
            _catalogStatus.Text = "Catalog: waiting for game directory";
            _install.Enabled = false;
            _uninstall.Enabled = false;
        }

        private void SetBusy(bool busy)
        {
            UseWaitCursor = busy;
            _refresh.Enabled = !busy;
            _browseGame.Enabled = !busy;
            _detectGame.Enabled = !busy;
            _list.Enabled = !busy;

            if (busy)
            {
                _install.Enabled = false;
                _uninstall.Enabled = false;
            }
            else
            {
                ShowSelectedDetails();
            }

            Application.DoEvents();
        }
    }
}
