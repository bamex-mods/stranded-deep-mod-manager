using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using StrandedDeepModManager.Assets;
using StrandedDeepModManager.Content;
using StrandedDeepModManager.UI;

namespace StrandedDeepModManager
{
    public sealed class MainForm : BorderlessForm
    {
        private enum ModFilter
        {
            All,
            Installed,
            Updates
        }

        private readonly BackgroundWorker _pageWorker =
            new BackgroundWorker();

        private readonly Panel _header =
            new Panel();

        private readonly Button _langRu =
            new Button();

        private readonly Button _langEn =
            new Button();

        private readonly Button _settings =
            new Button();

        private readonly Button _minimizeWindow =
            new Button();

        private readonly Button _maximizeWindow =
            new Button();

        private readonly Button _closeWindow =
            new Button();

        private readonly ToolTip _windowToolTip =
            new ToolTip();

        private readonly Button _navAll =
            new Button();

        private readonly Button _navInstalled =
            new Button();

        private readonly Button _navUpdates =
            new Button();

        private readonly Button _refresh =
            new Button();

        private readonly TextBox _search =
            new TextBox();

        private readonly Label _searchPlaceholder =
            new Label();

        private readonly Label _catalogStatus =
            new Label();

        private readonly ModListViewport _modList =
            new ModListViewport();

        private readonly StyledVScrollBar _modScroll =
            new StyledVScrollBar();

        private readonly PictureBox _selectedIdentity =
            new PictureBox();

        private readonly Label _selectedTitle =
            new Label();

        private readonly Label _selectedSubtitle =
            new Label();

        private readonly Label _selectedStatus =
            new Label();

        private readonly RichTextBox _details =
            new RichTextBox();

        private ModDetailsControl _modDetails;

        private readonly Button _install =
            new Button();

        private readonly Button _manage =
            new Button();

        private readonly List<ModListItemControl> _rows =
            new List<ModListItemControl>();

        private ManagerEngine _engine;

        private IList<PackageStatus> _statuses =
            new List<PackageStatus>();

        private PackageStatus _selected;

        private ModFilter _filter =
            ModFilter.All;

        private string _locale =
            "ru";

        private string _gameRoot;

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
                string dir =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "BamEx",
                        "StrandedDeepModManager");

                Directory.CreateDirectory(dir);

                return Path.Combine(
                    dir,
                    "settings.json");
            }
        }

        public MainForm()
        {
            Text =
                AppInfo.ProductName +
                " v" +
                AppInfo.Version;

            StartPosition =
                FormStartPosition.CenterScreen;

            FormBorderStyle =
                FormBorderStyle.None;

            MinimumSize =
                new Size(
                    1100,
                    720);

            Size =
                new Size(
                    1200,
                    800);

            BackColor =
                Theme.WindowBackground;

            ForeColor =
                Theme.TextPrimary;

            Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            _pageWorker.DoWork +=
                PageWorkerDoWork;

            _pageWorker.RunWorkerCompleted +=
                PageWorkerCompleted;

            BuildUi();
            LoadSettings();

            Shown += delegate
            {
                if (!String.IsNullOrWhiteSpace(
                    _gameRoot))
                {
                    RefreshCatalog(true);
                }
                else
                {
                    ShowNoGameMessage();
                }
            };
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
            SuspendLayout();

            TableLayoutPanel root =
                new TableLayoutPanel();

            root.Dock = DockStyle.Fill;
            root.BackColor =
                Theme.WindowBackground;

            root.ColumnCount = 1;
            root.RowCount = 3;

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    120F));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    58F));

            root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            Controls.Add(root);

            BuildHeader();
            root.Controls.Add(
                _header,
                0,
                0);

            Control nav =
                BuildGlobalNavigation();

            root.Controls.Add(
                nav,
                0,
                1);

            Control content =
                BuildContent();

            root.Controls.Add(
                content,
                0,
                2);

            ApplyLanguage();

            ResumeLayout(true);
        }

        private void BuildHeader()
        {
            _header.Dock =
                DockStyle.Fill;

            _header.BackgroundImage =
                null;

            _header.BackColor =
                Theme.Panel;

            _header.Paint += HeaderPaint;

            Label manager =
                new Label();

            manager.Text =
                "M O D   M A N A G E R";

            manager.ForeColor =
                Color.FromArgb(
                    220,
                    240,
                    240,
                    238);

            manager.BackColor =
                Color.Transparent;

            manager.Font =
                Theme.UiFont(
                    10.5F,
                    FontStyle.Regular);

            manager.AutoSize =
                true;

            manager.Location =
                new Point(
                    64,
                    78);

            _header.Controls.Add(
                manager);

            _langRu.Size =
                new Size(
                    46,
                    34);

            _langRu.Location =
                new Point(
                    996,
                    16);

            _langRu.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Right;

            _langRu.Text = "RU";

            _langRu.Click += delegate
            {
                SetLanguage("ru");
            };

            _langEn.Size =
                new Size(
                    46,
                    34);

            _langEn.Location =
                new Point(
                    1044,
                    16);

            _langEn.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Right;

            _langEn.Text = "EN";

            _langEn.Click += delegate
            {
                SetLanguage("en");
            };

            _settings.Size =
                new Size(
                    132,
                    34);

            _settings.Location =
                new Point(
                    958,
                    60);

            _settings.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Right;

            _settings.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Settings);

            _settings.TextImageRelation =
                TextImageRelation.ImageBeforeText;

            _settings.Click += delegate
            {
                OpenSettings();
            };

            _header.Controls.Add(
                _langRu);

            _header.Controls.Add(
                _langEn);

            _header.Controls.Add(
                _settings);

            ConfigureWindowButton(
                _minimizeWindow,
                AssetKeys.SystemUi.Minimize);

            ConfigureWindowButton(
                _maximizeWindow,
                AssetKeys.SystemUi.Maximize);

            ConfigureWindowButton(
                _closeWindow,
                AssetKeys.SystemUi.Close);

            _minimizeWindow.Click += delegate
            {
                WindowState =
                    FormWindowState.Minimized;
            };

            _maximizeWindow.Click += delegate
            {
                ToggleWindowMaximize();
            };

            _closeWindow.Click += delegate
            {
                Close();
            };

            _closeWindow.FlatAppearance.MouseOverBackColor =
                Theme.Error;

            _windowToolTip.SetToolTip(
                _minimizeWindow,
                "Minimize");

            _windowToolTip.SetToolTip(
                _maximizeWindow,
                "Maximize / Restore");

            _windowToolTip.SetToolTip(
                _closeWindow,
                "Close");

            _header.Controls.Add(
                _minimizeWindow);

            _header.Controls.Add(
                _maximizeWindow);

            _header.Controls.Add(
                _closeWindow);

            _header.MouseDown += HeaderMouseDown;
            _header.DoubleClick += HeaderDoubleClick;

            manager.MouseDown += HeaderMouseDown;
            manager.DoubleClick += HeaderDoubleClick;

            _header.Resize += delegate
            {
                _header.Invalidate();

                int right =
                    _header.ClientSize.Width - 12;

                _closeWindow.Left =
                    right -
                    _closeWindow.Width;

                _maximizeWindow.Left =
                    _closeWindow.Left -
                    _maximizeWindow.Width -
                    2;

                _minimizeWindow.Left =
                    _maximizeWindow.Left -
                    _minimizeWindow.Width -
                    2;

                _langEn.Left =
                    _minimizeWindow.Left -
                    _langEn.Width -
                    14;

                _langRu.Left =
                    _langEn.Left -
                    _langRu.Width -
                    2;

                _settings.Left =
                    right -
                    _settings.Width;

                _settings.Top = 60;
            };
        }

        private void ConfigureWindowButton(
            Button button,
            string iconKey)
        {
            button.Size =
                new Size(
                    34,
                    30);

            button.Top = 10;
            button.Text = "";

            button.Image =
                UiAssets.SystemIcon(
                    iconKey);

            Theme.StyleHeaderButton(
                button,
                false);

            button.FlatAppearance.BorderSize = 0;
        }

        private void HeaderMouseDown(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button ==
                MouseButtons.Left)
            {
                BeginWindowDrag();
            }
        }

        private void HeaderDoubleClick(
            object sender,
            EventArgs e)
        {
            ToggleWindowMaximize();
        }

        private void HeaderPaint(
            object sender,
            PaintEventArgs e)
        {
            Image image =
                UiAssets.HeaderBackground();

            if (image == null ||
                _header.ClientSize.Width <= 0 ||
                _header.ClientSize.Height <= 0)
            {
                return;
            }

            DrawCoverImage(
                e.Graphics,
                image,
                _header.ClientRectangle);
        }

        private static void DrawCoverImage(
            Graphics graphics,
            Image image,
            Rectangle bounds)
        {
            if (graphics == null ||
                image == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0 ||
                image.Width <= 0 ||
                image.Height <= 0)
            {
                return;
            }

            float scaleToWidth =
                (float)bounds.Width /
                (float)image.Width;

            float designScale =
                1200F /
                (float)image.Width;

            float scale =
                Math.Min(
                    scaleToWidth,
                    designScale);

            int width =
                (int)Math.Ceiling(
                    image.Width * scale);

            int height =
                (int)Math.Ceiling(
                    image.Height * scale);

            int x =
                bounds.Left;

            int y =
                bounds.Top;

            InterpolationMode previous =
                graphics.InterpolationMode;

            PixelOffsetMode previousPixel =
                graphics.PixelOffsetMode;

            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            if (x > bounds.Left)
            {
                int leftGap =
                    x - bounds.Left;

                graphics.DrawImage(
                    image,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top,
                        leftGap,
                        bounds.Height),
                    new Rectangle(
                        0,
                        0,
                        Math.Min(
                            24,
                            image.Width),
                        image.Height),
                    GraphicsUnit.Pixel);
            }

            int rightStart =
                x + width;

            int rightEdge =
                bounds.Right;

            graphics.DrawImage(
                image,
                new Rectangle(
                    x,
                    y,
                    width,
                    height));

            if (rightStart < rightEdge)
            {
                using (SolidBrush fill =
                    new SolidBrush(
                        Theme.Panel))
                {
                    graphics.FillRectangle(
                        fill,
                        new Rectangle(
                            rightStart,
                            bounds.Top,
                            rightEdge - rightStart,
                            bounds.Height));
                }

                int fadeWidth =
                    Math.Min(
                        220,
                        Math.Max(
                            80,
                            width / 5));

                int fadeLeft =
                    Math.Max(
                        bounds.Left,
                        rightStart - fadeWidth);

                using (LinearGradientBrush fade =
                    new LinearGradientBrush(
                        new Rectangle(
                            fadeLeft,
                            bounds.Top,
                            Math.Max(
                                1,
                                rightStart - fadeLeft),
                            bounds.Height),
                        Color.FromArgb(
                            0,
                            Theme.Panel),
                        Theme.Panel,
                        LinearGradientMode.Horizontal))
                {
                    graphics.FillRectangle(
                        fade,
                        new Rectangle(
                            fadeLeft,
                            bounds.Top,
                            Math.Max(
                                1,
                                rightStart - fadeLeft),
                            bounds.Height));
                }
            }

            graphics.InterpolationMode =
                previous;

            graphics.PixelOffsetMode =
                previousPixel;
        }
        private Control BuildGlobalNavigation()
        {
            TableLayoutPanel nav =
                new TableLayoutPanel();

            nav.Dock =
                DockStyle.Fill;

            nav.BackColor =
                Theme.Panel;

            nav.Padding =
                new Padding(
                    46,
                    10,
                    46,
                    8);

            nav.ColumnCount = 3;

            nav.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.AutoSize));

            nav.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            nav.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    270F));

            FlowLayoutPanel filters =
                new FlowLayoutPanel();

            filters.Dock =
                DockStyle.Fill;

            filters.AutoSize = true;
            filters.WrapContents = false;
            filters.Margin = Padding.Empty;

            ConfigureNavButton(
                _navAll,
                AssetKeys.SystemUi.AllMods,
                146);

            ConfigureNavButton(
                _navInstalled,
                AssetKeys.SystemUi.Installed,
                166);

            ConfigureNavButton(
                _navUpdates,
                AssetKeys.SystemUi.Updates,
                144);

            _navAll.Click += delegate
            {
                SetFilter(
                    ModFilter.All);
            };

            _navInstalled.Click += delegate
            {
                SetFilter(
                    ModFilter.Installed);
            };

            _navUpdates.Click += delegate
            {
                SetFilter(
                    ModFilter.Updates);
            };

            filters.Controls.Add(
                _navAll);

            filters.Controls.Add(
                _navInstalled);

            filters.Controls.Add(
                _navUpdates);

            nav.Controls.Add(
                filters,
                0,
                0);

            FlowLayoutPanel status =
                new FlowLayoutPanel();

            status.Dock =
                DockStyle.Fill;

            status.FlowDirection =
                FlowDirection.RightToLeft;

            status.WrapContents =
                false;

            status.Margin =
                new Padding(
                    8,
                    0,
                    8,
                    0);

            _refresh.Size =
                new Size(
                    36,
                    34);

            _refresh.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Refresh);

            _refresh.Text = "";

            Theme.StyleNavButton(
                _refresh,
                false);

            _refresh.Click += delegate
            {
                RefreshCatalog(true);
            };

            _catalogStatus.AutoSize =
                true;

            _catalogStatus.ForeColor =
                Theme.TextSecondary;

            _catalogStatus.TextAlign =
                ContentAlignment.MiddleRight;

            _catalogStatus.Margin =
                new Padding(
                    8,
                    9,
                    2,
                    0);

            status.Controls.Add(
                _refresh);

            status.Controls.Add(
                _catalogStatus);

            nav.Controls.Add(
                status,
                1,
                0);

            Panel searchPanel =
                new Panel();

            searchPanel.Dock =
                DockStyle.Fill;

            searchPanel.BackColor =
                Theme.WindowBackground;

            searchPanel.Padding =
                new Padding(
                    10,
                    6,
                    8,
                    4);

            PictureBox searchIcon =
                new PictureBox();

            searchIcon.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Search);

            searchIcon.SizeMode =
                PictureBoxSizeMode.Zoom;

            searchIcon.Size =
                new Size(
                    20,
                    20);

            searchIcon.Location =
                new Point(
                    10,
                    9);

            searchIcon.BackColor =
                Color.Transparent;

            _search.BorderStyle =
                BorderStyle.None;

            _search.BackColor =
                Theme.WindowBackground;

            _search.ForeColor =
                Theme.TextPrimary;

            _search.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);

            _search.Location =
                new Point(
                    38,
                    9);

            _search.Width = 214;

            _search.TextChanged += delegate
            {
                _searchPlaceholder.Visible =
                    _search.TextLength == 0 &&
                    !_search.Focused;

                RenderStatuses();
            };

            _search.Enter += delegate
            {
                _searchPlaceholder.Visible = false;
            };

            _search.Leave += delegate
            {
                _searchPlaceholder.Visible =
                    _search.TextLength == 0;
            };

            _searchPlaceholder.AutoSize = true;
            _searchPlaceholder.ForeColor =
                Theme.TextSecondary;
            _searchPlaceholder.BackColor =
                Color.Transparent;
            _searchPlaceholder.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);
            _searchPlaceholder.Location =
                new Point(
                    38,
                    9);
            _searchPlaceholder.Cursor =
                Cursors.IBeam;
            _searchPlaceholder.Click += delegate
            {
                _search.Focus();
            };

            searchPanel.Controls.Add(
                searchIcon);

            searchPanel.Controls.Add(
                _search);

            searchPanel.Controls.Add(
                _searchPlaceholder);

            _searchPlaceholder.BringToFront();

            searchPanel.Paint += delegate(
                object sender,
                PaintEventArgs e)
            {
                using (Pen pen =
                    new Pen(
                        Theme.Border,
                        1F))
                {
                    e.Graphics.DrawRectangle(
                        pen,
                        0,
                        0,
                        searchPanel.Width - 1,
                        searchPanel.Height - 1);
                }
            };

            nav.Controls.Add(
                searchPanel,
                2,
                0);

            return nav;
        }

        private void ConfigureNavButton(
            Button button,
            string iconKey,
            int width)
        {
            button.Width = width;
            button.Height = 36;
            button.Margin =
                new Padding(
                    0,
                    0,
                    10,
                    0);

            button.Image =
                UiAssets.SystemIcon(
                    iconKey);

            Theme.StyleNavButton(
                button,
                false);
        }

        private Control BuildContent()
        {
            TableLayoutPanel content =
                new TableLayoutPanel();

            content.Dock =
                DockStyle.Fill;

            content.BackColor =
                Theme.WindowBackground;

            content.Padding =
                new Padding(
                    46,
                    10,
                    46,
                    32);

            content.ColumnCount = 3;

            content.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    410F));

            content.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    20F));

            content.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            Panel listBorder =
                new Panel();

            listBorder.Dock =
                DockStyle.Fill;

            listBorder.BackColor =
                Theme.Border;

            listBorder.Padding =
                new Padding(1);

            _modList.Dock =
                DockStyle.Fill;

            _modList.AutoScroll =
                false;

            _modList.BackColor =
                Theme.Panel;

            _modList.Padding =
                new Padding(
                    8,
                    8,
                    8,
                    8);

            _modList.Resize += delegate
            {
                LayoutModRows();
            };

            _modList.MouseWheel += delegate(
                object sender,
                MouseEventArgs e)
            {
                int step =
                    Math.Max(
                        42,
                        Math.Abs(e.Delta) / 3);

                _modScroll.ScrollBy(
                    e.Delta > 0
                        ? -step
                        : step);
            };

            _modScroll.Width = 10;
            _modScroll.Dock =
                DockStyle.Right;

            _modScroll.ValueChanged += delegate
            {
                LayoutModRows();
            };

            Panel listHost =
                new Panel();

            listHost.Dock =
                DockStyle.Fill;

            listHost.BackColor =
                Theme.Panel;

            _modList.Dock =
                DockStyle.Fill;

            listHost.Controls.Add(
                _modList);

            listHost.Controls.Add(
                _modScroll);

            _modScroll.BringToFront();

            listBorder.Controls.Add(
                listHost);

            content.Controls.Add(
                listBorder,
                0,
                0);

            Panel rightBorder =
                new Panel();

            rightBorder.Dock =
                DockStyle.Fill;

            rightBorder.BackColor =
                Theme.Border;

            rightBorder.Padding =
                new Padding(1);

            Panel right =
                new Panel();

            right.Dock =
                DockStyle.Fill;

            right.BackColor =
                Theme.Panel;

            rightBorder.Controls.Add(
                right);

            BuildRightStageOne(
                right);

            content.Controls.Add(
                rightBorder,
                2,
                0);

            return content;
        }

        private void BuildRightStageOne(
            Panel parent)
        {
            _install.Click += delegate
            {
                InstallSelected();
            };

            _manage.Click += delegate
            {
                ShowManageMenu();
            };

            _modDetails =
                new ModDetailsControl(
                    _selectedIdentity,
                    _selectedTitle,
                    _selectedSubtitle,
                    _selectedStatus,
                    _install,
                    _manage);

            parent.Controls.Add(
                _modDetails);
        }

        private void LoadSettings()
        {
            ManagerSettings settings =
                null;

            try
            {
                if (File.Exists(
                    SettingsPath))
                {
                    settings =
                        JsonUtil.ReadFile<ManagerSettings>(
                            SettingsPath);
                }
            }
            catch
            {
            }

            if (settings == null)
                settings =
                    new ManagerSettings();

            if (!String.IsNullOrWhiteSpace(
                    settings.gameRoot) &&
                GameLocator.LooksLikeGameRoot(
                    settings.gameRoot))
            {
                _gameRoot =
                    settings.gameRoot;

                return;
            }

            string detected =
                GameLocator.FindGameRoot();

            if (!String.IsNullOrWhiteSpace(
                detected))
            {
                _gameRoot =
                    detected;

                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            ManagerSettings settings =
                new ManagerSettings();

            settings.gameRoot =
                _gameRoot;

            JsonUtil.WriteFile(
                SettingsPath,
                settings);
        }

        private void OpenSettings()
        {
            string catalog =
                _engine == null
                    ? _catalogStatus.Text
                    : _engine.CatalogStatus;

            string dataRoot =
                _engine == null
                    ? Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "BamEx",
                        "StrandedDeepModManager")
                    : _engine.DataRoot;

            using (SettingsForm form =
                new SettingsForm(
                    _gameRoot,
                    catalog,
                    dataRoot,
                    _locale))
            {
                if (form.ShowDialog(this) !=
                    DialogResult.OK)
                {
                    return;
                }

                if (String.Equals(
                    form.GameRoot,
                    _gameRoot,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!GameLocator.LooksLikeGameRoot(
                    form.GameRoot))
                {
                    MessageBox.Show(
                        this,
                        IsRu
                            ? "Выбранная папка не похожа на папку Stranded Deep."
                            : "The selected folder does not look like the Stranded Deep game directory.",
                        IsRu
                            ? "Неверная папка игры"
                            : "Invalid game directory",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                _gameRoot =
                    form.GameRoot;

                SaveSettings();
                RefreshCatalog(true);
            }
        }

        private void RefreshCatalog(
            bool adoptExactMatches)
        {
            if (String.IsNullOrWhiteSpace(
                _gameRoot) ||
                !GameLocator.LooksLikeGameRoot(
                    _gameRoot))
            {
                ShowNoGameMessage();
                return;
            }

            SetBusy(true);

            string selectedId =
                _selected == null ||
                _selected.CatalogPackage == null
                    ? null
                    : _selected.CatalogPackage.id;

            try
            {
                SaveSettings();

                _engine =
                    new ManagerEngine(
                        _gameRoot,
                        AppInfo.CatalogUrl);

                _engine.LoadCatalog();

                _statuses =
                    _engine.ScanAll(
                        adoptExactMatches);

                RenderStatuses();

                if (!String.IsNullOrWhiteSpace(
                    selectedId))
                {
                    PackageStatus previous =
                        _statuses.FirstOrDefault(
                            x =>
                                x.CatalogPackage != null &&
                                String.Equals(
                                    x.CatalogPackage.id,
                                    selectedId,
                                    StringComparison.Ordinal));

                    if (previous != null)
                        SelectStatus(previous);
                }

                UpdateCatalogSummary();
            }
            catch (Exception ex)
            {
                _catalogStatus.Text =
                    IsRu
                        ? "Каталог недоступен"
                        : "Catalog unavailable";

                MessageBox.Show(
                    this,
                    ex.Message,
                    IsRu
                        ? "Ошибка обновления"
                        : "Refresh failed",
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
            string selectedId =
                _selected == null ||
                _selected.CatalogPackage == null
                    ? null
                    : _selected.CatalogPackage.id;

            _modList.SuspendLayout();

            try
            {
                _modList.Controls.Clear();
                _rows.Clear();

                string query =
                    _search.Text.Trim();

                IEnumerable<PackageStatus> source =
                    _statuses;

                if (_filter ==
                    ModFilter.Installed)
                {
                    source =
                        source.Where(
                            x =>
                                x.Kind != PackageStatusKind.NotInstalled &&
                                x.Kind != PackageStatusKind.PackageMissing);
                }
                else if (_filter ==
                    ModFilter.Updates)
                {
                    source =
                        source.Where(
                            x =>
                                x.Kind == PackageStatusKind.UpdateAvailable);
                }

                if (!String.IsNullOrWhiteSpace(
                    query))
                {
                    source =
                        source.Where(
                            x =>
                                ContainsIgnoreCase(
                                    x.CatalogPackage.name,
                                    query) ||
                                ContainsIgnoreCase(
                                    x.CatalogPackage.id,
                                    query) ||
                                ContainsIgnoreCase(
                                    x.CatalogPackage.description,
                                    query));
                }

                foreach (PackageStatus status
                    in source)
                {
                    ModListItemControl row =
                        new ModListItemControl(
                            status,
                            _locale);

                    row.Width =
                        Math.Max(
                            300,
                            _modList.ClientSize.Width -
                            20);

                    row.Anchor =
                        AnchorStyles.Left |
                        AnchorStyles.Top;

                    row.ItemSelected += delegate(
                        object sender,
                        EventArgs e)
                    {
                        ModListItemControl selectedRow =
                            sender as ModListItemControl;

                        if (selectedRow != null)
                            SelectStatus(
                                selectedRow.Status);
                    };

                    _rows.Add(row);
                    _modList.Controls.Add(row);

                    if (!String.IsNullOrWhiteSpace(
                        selectedId) &&
                        String.Equals(
                            status.CatalogPackage.id,
                            selectedId,
                            StringComparison.Ordinal))
                    {
                        row.SetSelected(true);
                    }
                }
            }
            finally
            {
                LayoutModRows();
                _modList.ResumeLayout(true);
            }

            PackageStatus selected =
                _statuses.FirstOrDefault(
                    x =>
                        x.CatalogPackage != null &&
                        String.Equals(
                            x.CatalogPackage.id,
                            selectedId,
                            StringComparison.Ordinal));

            if (selected == null &&
                _rows.Count > 0)
            {
                selected =
                    _rows[0].Status;
            }

            if (selected != null)
                SelectStatus(selected);
            else
                ClearSelection();

            UpdateCatalogSummary();
        }

        private void LayoutModRows()
        {
            if (_modList == null ||
                _modScroll == null)
            {
                return;
            }

            int rowHeight = 78;
            int rowStep = 84;

            int contentHeight =
                (_rows.Count * rowStep) + 16;

            int maximum =
                Math.Max(
                    0,
                    contentHeight -
                    _modList.ClientSize.Height);

            _modScroll.SetRange(
                maximum,
                Math.Max(
                    1,
                    _modList.ClientSize.Height));

            int width =
                Math.Max(
                    280,
                    _modList.ClientSize.Width - 16);

            int y =
                8 -
                _modScroll.Value;

            foreach (ModListItemControl row
                in _rows)
            {
                row.SetBounds(
                    8,
                    y,
                    width,
                    rowHeight);

                y += rowStep;
            }
        }
        private void SelectStatus(
            PackageStatus status)
        {
            _selected =
                status;

            foreach (ModListItemControl row
                in _rows)
            {
                row.SetSelected(
                    Object.ReferenceEquals(
                        row.Status,
                        status) ||
                    (
                        row.Status.CatalogPackage != null &&
                        status != null &&
                        status.CatalogPackage != null &&
                        String.Equals(
                            row.Status.CatalogPackage.id,
                            status.CatalogPackage.id,
                            StringComparison.Ordinal)
                    ));
            }

            ShowSelectedDetails();
            BeginLoadSelectedPage();
        }

        private void ClearSelection()
        {
            _selected = null;

            foreach (ModListItemControl row
                in _rows)
            {
                row.SetSelected(false);
            }

            ShowSelectedDetails();
        }

        private void SetFilter(
            ModFilter filter)
        {
            _filter =
                filter;

            UpdateFilterButtons();
            RenderStatuses();
        }

        private void SetLanguage(
            string locale)
        {
            if (String.Equals(
                _locale,
                locale,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _locale =
                locale;

            ApplyLanguage();
            RenderStatuses();
            ShowSelectedDetails();
            BeginLoadSelectedPage();
        }

        private void ApplyLanguage()
        {
            _settings.Text =
                IsRu
                    ? "Настройки"
                    : "Settings";

            _navAll.Text =
                IsRu
                    ? "Все моды"
                    : "All Mods";

            _navInstalled.Text =
                IsRu
                    ? "Установленные"
                    : "Installed";

            _navUpdates.Text =
                IsRu
                    ? "Обновления"
                    : "Updates";

            _search.Text =
                _search.Text;

            _langRu.Text = "RU";
            _langEn.Text = "EN";

            _searchPlaceholder.Text =
                IsRu
                    ? "Поиск модов..."
                    : "Search mods...";

            _searchPlaceholder.Visible =
                _search.TextLength == 0 &&
                !_search.Focused;

            Theme.StyleHeaderButton(
                _langRu,
                IsRu);

            Theme.StyleHeaderButton(
                _langEn,
                !IsRu);

            Theme.StyleHeaderButton(
                _settings,
                false);

            UpdateFilterButtons();
            UpdateActionButtons();

            foreach (ModListItemControl row
                in _rows)
            {
                row.UpdateContent(
                    _locale);
            }
        }

        private void UpdateFilterButtons()
        {
            Theme.StyleNavButton(
                _navAll,
                _filter ==
                    ModFilter.All);

            Theme.StyleNavButton(
                _navInstalled,
                _filter ==
                    ModFilter.Installed);

            Theme.StyleNavButton(
                _navUpdates,
                _filter ==
                    ModFilter.Updates);
        }

        private void UpdateCatalogSummary()
        {
            int installed =
                _statuses.Count(
                    x =>
                        x.Kind == PackageStatusKind.Installed ||
                        x.Kind == PackageStatusKind.UpdateAvailable);

            int updates =
                _statuses.Count(
                    x =>
                        x.Kind == PackageStatusKind.UpdateAvailable);

            string source =
                _engine == null
                    ? ""
                    : _engine.CatalogStatus;

            _catalogStatus.Text =
                String.Format(
                    IsRu
                        ? "{0} модов • {1} установлено • {2} обновлений"
                        : "{0} mods • {1} installed • {2} updates",
                    _statuses.Count,
                    installed,
                    updates);

            if (!String.IsNullOrWhiteSpace(
                source) &&
                source.IndexOf(
                    "cache",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _catalogStatus.Text +=
                    IsRu
                        ? " • кэш"
                        : " • cache";
            }
        }

        private void ShowSelectedDetails()
        {
            PackageStatus status =
                SelectedStatus;

            if (status == null)
            {
                _selectedIdentity.Image =
                    null;

                _selectedTitle.Text =
                    IsRu
                        ? "Выберите мод"
                        : "Select a mod";

                _selectedSubtitle.Text =
                    IsRu
                        ? "Страница выбранного мода появится здесь."
                        : "The selected mod page will appear here.";

                _selectedStatus.Text = "";

                _details.Text =
                    IsRu
                        ? "Менеджер использует проверенный публичный stable catalog и GitHub Releases."
                        : "The Manager uses the verified public stable catalog and GitHub Releases.";

                UpdateActionButtons();
                UpdateProductView();
                return;
            }

            CatalogPackage package =
                status.CatalogPackage;

            _selectedIdentity.Image =
                UiAssets.Identity(
                    package.id);

            _selectedTitle.Text =
                DisplayNames.Mod(
                    package.name ?? package.id);

            _selectedStatus.Text =
                LocalizedStatus(
                    status.Kind);

            _selectedStatus.BackColor =
                Theme.StatusBackColor(
                    status.Kind);

            _selectedSubtitle.Text =
                package.description ?? "";

            StringBuilder text =
                new StringBuilder();

            text.AppendLine(
                String.Format(
                    IsRu
                        ? "Доступная версия: {0}"
                        : "Available version: {0}",
                    package.latest == null
                        ? "-"
                        : package.latest.version));

            text.AppendLine(
                String.Format(
                    IsRu
                        ? "Установленная версия: {0}"
                        : "Installed version: {0}",
                    status.InstalledVersion ?? "-"));

            text.AppendLine(
                String.Format(
                    IsRu
                        ? "Статус: {0}"
                        : "Status: {0}",
                    LocalizedStatus(
                        status.Kind)));

            text.AppendLine();

            if (!String.IsNullOrWhiteSpace(
                status.Detail))
            {
                text.AppendLine(
                    status.Detail);

                text.AppendLine();
            }

            if (package.page == null)
            {
                text.AppendLine(
                    IsRu
                        ? "Страница мода: базовые данные каталога"
                        : "Product page: catalog fallback only");

                text.AppendLine();
                text.AppendLine(
                    package.description ?? "");
            }
            else if (LoadedPageMatches(
                package,
                _locale))
            {
                AppendLoadedPage(
                    text,
                    _loadedPage);

                if (_loadedPage != null &&
                    _loadedPage.Locale != null &&
                    !String.IsNullOrWhiteSpace(
                        _loadedPage.Locale.subtitle))
                {
                    _selectedSubtitle.Text =
                        _loadedPage.Locale.subtitle;
                }
            }
            else
            {
                text.AppendLine(
                    IsRu
                        ? "Загрузка проверенной страницы мода..."
                        : "Loading validated product page...");

                if (!String.IsNullOrWhiteSpace(
                    _pageLoadError))
                {
                    text.AppendLine();
                    text.AppendLine(
                        IsRu
                            ? "Предупреждение:"
                            : "Warning:");

                    text.AppendLine(
                        _pageLoadError);
                }

                text.AppendLine();
                text.AppendLine(
                    package.description ?? "");
            }

            _details.Text =
                text.ToString();

            UpdateActionButtons();
            UpdateProductView();
        }

        private void UpdateProductView()
        {
            if (_modDetails == null)
                return;

            PackageStatus status =
                SelectedStatus;

            if (status == null ||
                status.CatalogPackage == null)
            {
                _modDetails.Bind(
                    null,
                    null,
                    _locale,
                    null,
                    false,
                    null);

                return;
            }

            CatalogPackage package =
                status.CatalogPackage;

            ModPageLoadResult page =
                LoadedPageMatches(
                    package,
                    _locale)
                    ? _loadedPage
                    : null;

            bool loading =
                package.page != null &&
                page == null &&
                String.IsNullOrWhiteSpace(
                    _pageLoadError);

            ModPageService service =
                _engine == null
                    ? null
                    : _engine.ModPages;

            _modDetails.Bind(
                status,
                page,
                _locale,
                service,
                loading,
                _pageLoadError);
        }
        private void AppendLoadedPage(
            StringBuilder text,
            ModPageLoadResult page)
        {
            if (page == null)
                return;

            if (page.Locale != null &&
                !String.IsNullOrWhiteSpace(
                    page.Locale.description))
            {
                text.AppendLine(
                    page.Locale.description);

                text.AppendLine();
            }

            if (page.Page != null &&
                page.Page.highlights != null &&
                page.Page.highlights.Count > 0)
            {
                text.AppendLine(
                    IsRu
                        ? "Ключевые особенности:"
                        : "Highlights:");

                text.AppendLine(
                    String.Join(
                        "  •  ",
                        page.Page.highlights.ToArray()));

                text.AppendLine();
            }

            if (page.Locale != null &&
                page.Locale.features != null &&
                page.Locale.features.Count > 0)
            {
                text.AppendLine(
                    String.Format(
                        IsRu
                            ? "Возможности ({0})"
                            : "Features ({0})",
                        page.Locale.features.Count));

                foreach (string feature
                    in page.Locale.features)
                {
                    text.AppendLine(
                        "  • " +
                        feature);
                }

                text.AppendLine();
            }

            int faq =
                page.Locale == null ||
                page.Locale.faq == null
                    ? 0
                    : page.Locale.faq.Count;

            int screenshots =
                page.Page == null ||
                page.Page.media == null ||
                page.Page.media.screenshots == null
                    ? 0
                    : page.Page.media.screenshots.Count;

            text.AppendLine(
                String.Format(
                    "FAQ: {0}    Screenshots: {1}",
                    faq,
                    screenshots));

            text.AppendLine(
                String.Format(
                    IsRu
                        ? "Источник страницы: {0}"
                        : "Page source: {0}",
                    LocalizedPageSource(
                        page.SourceKind)));

            if (!String.IsNullOrWhiteSpace(
                page.Warning))
            {
                text.AppendLine();
                text.AppendLine(
                    page.Warning);
            }

            if (!String.IsNullOrWhiteSpace(
                page.MediaWarning))
            {
                text.AppendLine();
                text.AppendLine(
                    page.MediaWarning);
            }
        }

        private string LocalizedPageSource(
            ModPageSourceKind kind)
        {
            if (!IsRu)
                return kind.ToString();

            switch (kind)
            {
                case ModPageSourceKind.Online:
                    return "Онлайн";

                case ModPageSourceKind.ExactCache:
                    return "Точный кэш";

                case ModPageSourceKind.LastGoodCache:
                    return "Последний рабочий кэш";

                default:
                    return kind.ToString();
            }
        }

        private string LocalizedStatus(
            PackageStatusKind kind)
        {
            switch (kind)
            {
                case PackageStatusKind.NotInstalled:
                    return IsRu
                        ? "Не установлен"
                        : "Not installed";

                case PackageStatusKind.Installed:
                    return IsRu
                        ? "Установлен"
                        : "Installed";

                case PackageStatusKind.UpdateAvailable:
                    return IsRu
                        ? "Доступно обновление"
                        : "Update available";

                case PackageStatusKind.DifferentBuild:
                    return IsRu
                        ? "Другой билд"
                        : "Different build";

                case PackageStatusKind.Modified:
                    return IsRu
                        ? "Изменён / неизвестен"
                        : "Modified / unknown";

                case PackageStatusKind.PackageMissing:
                    return IsRu
                        ? "Пакет недоступен"
                        : "Package unavailable";

                default:
                    return IsRu
                        ? "Ошибка"
                        : "Error";
            }
        }

        private void UpdateActionButtons()
        {
            PackageStatus status =
                SelectedStatus;

            bool canInstall =
                status != null &&
                (
                    status.Kind == PackageStatusKind.NotInstalled ||
                    status.Kind == PackageStatusKind.UpdateAvailable ||
                    status.Kind == PackageStatusKind.DifferentBuild ||
                    status.Kind == PackageStatusKind.Modified
                );

            bool canUninstall =
                status != null &&
                (
                    status.Kind == PackageStatusKind.Installed ||
                    status.Kind == PackageStatusKind.UpdateAvailable ||
                    status.Kind == PackageStatusKind.DifferentBuild ||
                    status.Kind == PackageStatusKind.Modified
                );

            _manage.Visible =
                canUninstall;

            _manage.Enabled =
                canUninstall;

            _manage.Text =
                IsRu
                    ? "Управление"
                    : "Manage";

            if (status != null &&
                status.Kind ==
                    PackageStatusKind.Installed)
            {
                _install.Visible =
                    false;
            }
            else if (status != null &&
                status.Kind ==
                    PackageStatusKind.UpdateAvailable)
            {
                _install.Visible =
                    true;

                Theme.StyleActionButton(
                    _install,
                    Theme.UpdateBlue,
                    Theme.AccentCyan);

                _install.Text =
                    IsRu
                        ? "Обновить"
                        : "Update";

                _install.Image =
                    UiAssets.SystemIcon(
                        AssetKeys.SystemUi.Updates);

                _install.Enabled =
                    true;
            }
            else if (status != null &&
                (
                    status.Kind ==
                        PackageStatusKind.DifferentBuild ||
                    status.Kind ==
                        PackageStatusKind.Modified
                ))
            {
                _install.Visible =
                    true;

                Theme.StyleActionButton(
                    _install,
                    Color.FromArgb(
                        0x9B,
                        0x77,
                        0x48),
                    Theme.AccentSand);

                _install.Text =
                    IsRu
                        ? "Переустановить"
                        : "Reinstall";

                _install.Image =
                    UiAssets.SystemIcon(
                        AssetKeys.SystemUi.Reinstall);

                _install.Enabled =
                    true;
            }
            else
            {
                _install.Visible =
                    canInstall;

                Theme.StyleActionButton(
                    _install,
                    Color.FromArgb(
                        0x9B,
                        0x77,
                        0x48),
                    Theme.AccentSand);

                _install.Text =
                    IsRu
                        ? "Установить"
                        : "Install";

                _install.Image =
                    UiAssets.SystemIcon(
                        AssetKeys.SystemUi.Install);

                _install.Enabled =
                    canInstall;
            }
        }

        private PackageStatus SelectedStatus
        {
            get
            {
                return _selected;
            }
        }

        private void ShowManageMenu()
        {
            PackageStatus status =
                SelectedStatus;

            if (status == null ||
                _engine == null)
            {
                return;
            }

            ContextMenuStrip menu =
                new ContextMenuStrip();

            DarkMenus.Style(
                menu);

            ToolStripMenuItem reinstall =
                new ToolStripMenuItem(
                    IsRu
                        ? "Переустановить"
                        : "Reinstall");

            DarkMenus.StyleItem(
                reinstall,
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Reinstall),
                false);

            reinstall.Click += delegate
            {
                InstallSelected();
            };

            ToolStripMenuItem uninstall =
                new ToolStripMenuItem(
                    IsRu
                        ? "Удалить мод"
                        : "Uninstall mod");

            DarkMenus.StyleItem(
                uninstall,
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Uninstall),
                true);

            uninstall.Click += delegate
            {
                UninstallSelected();
            };

            menu.Items.Add(
                reinstall);

            menu.Items.Add(
                DarkMenus.Separator());

            menu.Items.Add(
                uninstall);

            menu.Show(
                _manage,
                new Point(
                    _manage.Width -
                    menu.PreferredSize.Width,
                    -menu.PreferredSize.Height));
        }
        private void InstallSelected()
        {
            PackageStatus status =
                SelectedStatus;

            if (status == null ||
                _engine == null)
            {
                return;
            }

            DialogResult answer =
                MessageBox.Show(
                    this,
                    String.Format(
                        IsRu
                            ? "Установить или обновить {0}?\r\n\r\nStranded Deep должна быть закрыта. Каталог, SHA-256 пакета, manifest и файлы проверяются до установки."
                            : "Install/update {0}?\r\n\r\nStranded Deep must be closed. The catalog, package SHA-256, manifest and package files are verified before installation.",
                        DisplayNames.Mod(status.CatalogPackage.name)),
                    IsRu
                        ? "Подтверждение"
                        : "Confirm install",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

            if (answer !=
                DialogResult.OK)
            {
                return;
            }

            SetBusy(true);

            try
            {
                _engine.InstallOrUpdate(
                    status.CatalogPackage);

                MessageBox.Show(
                    this,
                    IsRu
                        ? "Установка/обновление завершена."
                        : "Install/update completed successfully.",
                    IsRu
                        ? "Готово"
                        : "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                RefreshCatalog(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    IsRu
                        ? "Ошибка установки"
                        : "Install/update failed",
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
            PackageStatus status =
                SelectedStatus;

            if (status == null ||
                _engine == null)
            {
                return;
            }

            DialogResult answer =
                MessageBox.Show(
                    this,
                    String.Format(
                        IsRu
                            ? "Удалить {0}?\r\n\r\nБудут удалены только пути, принадлежащие этому пакету. Persistent save/config data не удаляются."
                            : "Uninstall {0}?\r\n\r\nOnly package-owned runtime paths will be removed. Persistent save/config data is not deleted.",
                        DisplayNames.Mod(status.CatalogPackage.name)),
                    IsRu
                        ? "Подтверждение удаления"
                        : "Confirm uninstall",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

            if (answer !=
                DialogResult.OK)
            {
                return;
            }

            SetBusy(true);

            try
            {
                _engine.Uninstall(
                    status.CatalogPackage);

                MessageBox.Show(
                    this,
                    IsRu
                        ? "Мод удалён."
                        : "Uninstall completed successfully.",
                    IsRu
                        ? "Готово"
                        : "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                RefreshCatalog(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    IsRu
                        ? "Ошибка удаления"
                        : "Uninstall failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                RefreshCatalog(false);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BeginLoadSelectedPage()
        {
            PackageStatus status =
                SelectedStatus;

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

            if (LoadedPageMatches(
                package,
                _locale))
            {
                return;
            }

            if (_pageWorker.IsBusy)
            {
                _pageReloadPending = true;
                return;
            }

            _loadedPage = null;
            _loadedPagePackageId =
                package.id;

            _loadedPageLocale =
                _locale;

            _pageLoadError = null;
            _pageReloadPending = false;

            ShowSelectedDetails();

            PageLoadRequest request =
                new PageLoadRequest();

            request.Package =
                package;

            request.Locale =
                _locale;

            request.Service =
                _engine.ModPages;

            _pageWorker.RunWorkerAsync(
                request);
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

        private void PageWorkerDoWork(
            object sender,
            DoWorkEventArgs e)
        {
            PageLoadRequest request =
                e.Argument as PageLoadRequest;

            PageLoadOutcome outcome =
                new PageLoadOutcome();

            outcome.Request =
                request;

            try
            {
                if (request == null ||
                    request.Service == null)
                {
                    throw new InvalidOperationException(
                        "Invalid mod-page load request.");
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

            e.Result =
                outcome;
        }

        private void PageWorkerCompleted(
            object sender,
            RunWorkerCompletedEventArgs e)
        {
            PageLoadOutcome outcome =
                e.Result as PageLoadOutcome;

            PackageStatus selected =
                SelectedStatus;

            bool matches =
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
                    _locale,
                    StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                if (e.Error != null)
                {
                    _loadedPage = null;
                    _pageLoadError =
                        e.Error.Message;
                }
                else if (outcome.Error != null)
                {
                    _loadedPage = null;
                    _pageLoadError =
                        outcome.Error.Message;
                }
                else
                {
                    _loadedPage =
                        outcome.Result;

                    _loadedPagePackageId =
                        selected.CatalogPackage.id;

                    _loadedPageLocale =
                        outcome.Request.Locale;

                    _pageLoadError = null;
                }

                ShowSelectedDetails();
            }

            bool reload =
                _pageReloadPending;

            _pageReloadPending =
                false;

            if (reload)
                BeginLoadSelectedPage();
        }

        private void ShowNoGameMessage()
        {
            _catalogStatus.Text =
                IsRu
                    ? "Не выбрана папка игры"
                    : "Game directory not configured";

            _details.Text =
                IsRu
                    ? "Stranded Deep не найдена автоматически.\r\n\r\nОткройте Настройки и выберите папку игры."
                    : "Stranded Deep was not found automatically.\r\n\r\nOpen Settings and select the game directory.";

            _selectedTitle.Text =
                IsRu
                    ? "Требуется настройка"
                    : "Setup required";

            _selectedSubtitle.Text =
                IsRu
                    ? "Укажите папку Stranded Deep."
                    : "Select the Stranded Deep game directory.";

            _selectedStatus.Text = "";

            UpdateActionButtons();
            UpdateProductView();
        }

        private void SetBusy(
            bool busy)
        {
            UseWaitCursor =
                busy;

            _refresh.Enabled =
                !busy;

            _settings.Enabled =
                !busy;

            _navAll.Enabled =
                !busy;

            _navInstalled.Enabled =
                !busy;

            _navUpdates.Enabled =
                !busy;

            _search.Enabled =
                !busy;

            _modList.Enabled =
                !busy;

            if (busy)
            {
                _install.Enabled = false;
                _manage.Enabled = false;
            }
            else
            {
                ShowSelectedDetails();
            }

            Application.DoEvents();
        }

        private static bool ContainsIgnoreCase(
            string value,
            string query)
        {
            if (String.IsNullOrEmpty(
                value) ||
                String.IsNullOrEmpty(
                    query))
            {
                return false;
            }

            return
                value.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
