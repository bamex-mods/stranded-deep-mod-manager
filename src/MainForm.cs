using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using StrandedDeepModManager.Content;

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
        private readonly ComboBox _language = new ComboBox();
        private readonly BackgroundWorker _pageWorker = new BackgroundWorker();

        private ManagerEngine _engine;
        private IList<PackageStatus> _statuses = new List<PackageStatus>();

        private ModPageLoadResult _loadedPage;
        private string _loadedPagePackageId;
        private string _loadedPageLocale;
        private string _pageLoadError;
        private bool _pageReloadPending;

        private sealed class PageLoadRequest
        {
            public CatalogPackage Package;
            public string Locale;
            public ModPageService Service;
        }

        private sealed class PageLoadOutcome
        {
            public PageLoadRequest Request;
            public ModPageLoadResult Result;
            public Exception Error;
        }

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

            _pageWorker.DoWork += PageWorkerDoWork;
            _pageWorker.RunWorkerCompleted += PageWorkerCompleted;

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

            Label languageLabel = new Label();
            languageLabel.Text = "Page:";
            languageLabel.AutoSize = true;
            languageLabel.Margin = new Padding(18, 7, 3, 0);

            _language.DropDownStyle = ComboBoxStyle.DropDownList;
            _language.Width = 64;
            _language.Items.Add("RU");
            _language.Items.Add("EN");
            _language.SelectedIndex = 0;
            _language.SelectedIndexChanged += delegate
            {
                ShowSelectedDetails();
                BeginLoadSelectedPage();
            };

            _summary.AutoSize = true;
            _summary.Margin = new Padding(18, 7, 0, 0);

            toolbar.Controls.Add(_refresh);
            toolbar.Controls.Add(_install);
            toolbar.Controls.Add(_uninstall);
            toolbar.Controls.Add(languageLabel);
            toolbar.Controls.Add(_language);
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
            _list.SelectedIndexChanged += delegate
            {
                ShowSelectedDetails();
                BeginLoadSelectedPage();
            };

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
                    "Downloaded packages and product pages are cached under LocalAppData.";
                _install.Enabled = false;
                _uninstall.Enabled = false;
                return;
            }

            CatalogPackage p = status.CatalogPackage;
            StringBuilder text = new StringBuilder();

            text.AppendLine(p.name);
            text.AppendLine("ID: " + p.id);
            text.AppendLine("Available: " + (p.latest == null ? "" : p.latest.version));
            text.AppendLine("Installed: " + (status.InstalledVersion ?? "-"));
            text.AppendLine("Status: " + StatusText(status.Kind));
            text.AppendLine("Category: " + (p.category ?? ""));
            text.AppendLine("Cached ZIP: " + (status.PackageZipPath ?? "<not available>"));
            text.AppendLine();
            text.AppendLine("Runtime status:");
            text.AppendLine(status.Detail ?? "");
            text.AppendLine();

            if (p.page == null)
            {
                text.AppendLine("Product page: catalog fallback only");
                text.AppendLine();
                text.AppendLine(p.description ?? "");
            }
            else if (LoadedPageMatches(p, CurrentLocaleCode))
            {
                AppendLoadedPage(text, _loadedPage);
            }
            else
            {
                bool loading =
                    _pageWorker.IsBusy &&
                    String.Equals(
                        _loadedPagePackageId,
                        p.id,
                        StringComparison.Ordinal);

                text.AppendLine(
                    loading
                        ? "Product page: loading " + CurrentLocaleCode.ToUpperInvariant() + "..."
                        : "Product page: waiting for validated content");

                if (!String.IsNullOrWhiteSpace(_pageLoadError))
                {
                    text.AppendLine();
                    text.AppendLine("Page warning:");
                    text.AppendLine(_pageLoadError);
                    text.AppendLine();
                    text.AppendLine("Catalog fallback:");
                }

                text.AppendLine();
                text.AppendLine(p.description ?? "");
            }

            _details.Text = text.ToString();

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

        private string CurrentLocaleCode
        {
            get
            {
                return _language.SelectedIndex == 1
                    ? "en"
                    : "ru";
            }
        }

        private bool LoadedPageMatches(
            CatalogPackage package,
            string locale)
        {
            if (_loadedPage == null ||
                package == null ||
                package.page == null)
            {
                return false;
            }

            return
                String.Equals(
                    _loadedPagePackageId,
                    package.id,
                    StringComparison.Ordinal) &&
                String.Equals(
                    _loadedPageLocale,
                    locale,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    _loadedPage.RequestedCommit,
                    package.page.commit,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void AppendLoadedPage(
            StringBuilder text,
            ModPageLoadResult page)
        {
            text.AppendLine(
                "Product page source: " +
                page.SourceKind);

            text.AppendLine(
                "Page commit: " +
                page.ActualCommit);

            text.AppendLine(
                "Locale: " +
                page.ResolvedLocale.ToUpperInvariant());

            text.AppendLine();

            if (page.Locale != null)
            {
                if (!String.IsNullOrWhiteSpace(page.Locale.subtitle))
                {
                    text.AppendLine(page.Locale.subtitle);
                    text.AppendLine();
                }

                if (!String.IsNullOrWhiteSpace(page.Locale.description))
                {
                    text.AppendLine(page.Locale.description);
                    text.AppendLine();
                }
            }

            if (page.Page != null &&
                page.Page.highlights != null &&
                page.Page.highlights.Count > 0)
            {
                text.AppendLine(
                    "Highlights: " +
                    String.Join(
                        ", ",
                        page.Page.highlights.ToArray()));

                text.AppendLine();
            }

            if (page.Locale != null &&
                page.Locale.features != null)
            {
                text.AppendLine(
                    "Features (" +
                    page.Locale.features.Count +
                    "):");

                foreach (string feature
                    in page.Locale.features)
                {
                    text.AppendLine(
                        "  - " +
                        feature);
                }

                text.AppendLine();
            }

            int faqCount =
                page.Locale == null ||
                page.Locale.faq == null
                    ? 0
                    : page.Locale.faq.Count;

            int screenshotCount =
                page.Page == null ||
                page.Page.media == null ||
                page.Page.media.screenshots == null
                    ? 0
                    : page.Page.media.screenshots.Count;

            text.AppendLine(
                "FAQ: " +
                faqCount);

            text.AppendLine(
                "Screenshots: " +
                screenshotCount);

            text.AppendLine(
                "Cover: " +
                (
                    !String.IsNullOrWhiteSpace(page.CoverPath) &&
                    File.Exists(page.CoverPath)
                        ? "validated cache"
                        : "not available"));

            if (!String.IsNullOrWhiteSpace(page.Warning))
            {
                text.AppendLine();
                text.AppendLine(
                    "Cache/network warning: " +
                    page.Warning);
            }

            if (!String.IsNullOrWhiteSpace(page.MediaWarning))
            {
                text.AppendLine();
                text.AppendLine(
                    "Media warning: " +
                    page.MediaWarning);
            }
        }

        private void BeginLoadSelectedPage()
        {
            PackageStatus status = SelectedStatus;

            if (status == null ||
                status.CatalogPackage == null ||
                status.CatalogPackage.page == null ||
                _engine == null ||
                _engine.ModPages == null)
            {
                _loadedPage = null;
                _loadedPagePackageId = null;
                _loadedPageLocale = null;
                _pageLoadError = null;
                return;
            }

            CatalogPackage package =
                status.CatalogPackage;

            string locale =
                CurrentLocaleCode;

            if (LoadedPageMatches(
                package,
                locale))
            {
                return;
            }

            if (_pageWorker.IsBusy)
            {
                _pageReloadPending = true;
                return;
            }

            _loadedPage = null;
            _loadedPagePackageId = package.id;
            _loadedPageLocale = locale;
            _pageLoadError = null;
            _pageReloadPending = false;

            ShowSelectedDetails();

            PageLoadRequest request =
                new PageLoadRequest();

            request.Package = package;
            request.Locale = locale;
            request.Service = _engine.ModPages;

            _pageWorker.RunWorkerAsync(request);
        }

        private void PageWorkerDoWork(
            object sender,
            DoWorkEventArgs e)
        {
            PageLoadRequest request =
                e.Argument as PageLoadRequest;

            PageLoadOutcome outcome =
                new PageLoadOutcome();

            outcome.Request = request;

            try
            {
                if (request == null ||
                    request.Service == null)
                {
                    throw new InvalidOperationException(
                        "Mod-page load request is invalid.");
                }

                outcome.Result =
                    request.Service.Load(
                        request.Package,
                        request.Locale);
            }
            catch (Exception ex)
            {
                outcome.Error = ex;
            }

            e.Result = outcome;
        }

        private void PageWorkerCompleted(
            object sender,
            RunWorkerCompletedEventArgs e)
        {
            PageLoadOutcome outcome =
                e.Result as PageLoadOutcome;

            if (e.Error != null)
            {
                _pageLoadError =
                    e.Error.Message;
            }

            PackageStatus selected =
                SelectedStatus;

            bool matchesCurrent =
                outcome != null &&
                outcome.Request != null &&
                selected != null &&
                selected.CatalogPackage != null &&
                _engine != null &&
                Object.ReferenceEquals(
                    outcome.Request.Service,
                    _engine.ModPages) &&
                String.Equals(
                    outcome.Request.Package.id,
                    selected.CatalogPackage.id,
                    StringComparison.Ordinal) &&
                String.Equals(
                    outcome.Request.Locale,
                    CurrentLocaleCode,
                    StringComparison.OrdinalIgnoreCase);

            if (matchesCurrent)
            {
                if (outcome.Error == null)
                {
                    _loadedPage =
                        outcome.Result;

                    _loadedPagePackageId =
                        selected.CatalogPackage.id;

                    _loadedPageLocale =
                        outcome.Request.Locale;

                    _pageLoadError = null;
                }
                else
                {
                    _loadedPage = null;
                    _pageLoadError =
                        outcome.Error.Message;
                }

                ShowSelectedDetails();
            }

            bool reload =
                _pageReloadPending;

            _pageReloadPending = false;

            if (reload)
                BeginLoadSelectedPage();
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
