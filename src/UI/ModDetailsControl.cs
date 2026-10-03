using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using StrandedDeepModManager.Assets;
using StrandedDeepModManager.Content;

namespace StrandedDeepModManager.UI
{
    internal sealed class ModDetailsControl : UserControl
    {
        private enum ProductTab
        {
            About,
            Features,
            Screenshots,
            Compatibility,
            Faq,
            Changelog
        }

        private sealed class MediaLoadRequest
        {
            public int Generation;
            public CatalogPackage Package;
            public ModPageLoadResult Page;
            public ModPageService Service;
        }

        private sealed class LoadedScreenshot
        {
            public string Id;
            public Image Image;
        }

        private sealed class MediaLoadResult
        {
            public MediaLoadRequest Request;
            public List<LoadedScreenshot> Screenshots;
        }

        private readonly PictureBox _identity;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _status;
        private readonly Button _install;
        private readonly Button _manage;

        private readonly TableLayoutPanel _root =
            new TableLayoutPanel();

        private readonly TableLayoutPanel _gallery =
            new TableLayoutPanel();

        private readonly TableLayoutPanel _thumbnailGrid =
            new TableLayoutPanel();

        private readonly PictureBox _hero =
            new PictureBox();

        private readonly PictureBox[] _thumbs =
            new PictureBox[4];

        private readonly TableLayoutPanel _tabs =
            new TableLayoutPanel();

        private readonly Button[] _tabButtons =
            new Button[6];

        private readonly ProductTextView _content =
            new ProductTextView();

        private readonly TableLayoutPanel _highlights =
            new TableLayoutPanel();

        private readonly Panel _actionHost =
            new Panel();

        private readonly ToolTip _toolTip =
            new ToolTip();

        private readonly BackgroundWorker _mediaWorker =
            new BackgroundWorker();

        private readonly List<Image> _ownedImages =
            new List<Image>();

        private readonly List<Image> _galleryImages =
            new List<Image>();

        private readonly List<string> _galleryCaptions =
            new List<string>();

        private RowStyle _galleryRow;
        private RowStyle _tabsRow;
        private RowStyle _highlightRow;

        private PackageStatus _statusModel;
        private ModPageLoadResult _page;
        private ModPageService _service;
        private string _locale;
        private string _mediaKey;
        private ProductTab _activeTab =
            ProductTab.About;

        private int _generation;
        private MediaLoadRequest _pendingMedia;

        public ModDetailsControl(
            PictureBox identity,
            Label title,
            Label subtitle,
            Label status,
            Button install,
            Button manage)
        {
            _identity = identity;
            _title = title;
            _subtitle = subtitle;
            _status = status;
            _install = install;
            _manage = manage;

            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;

            BuildUi();

            _mediaWorker.DoWork +=
                MediaWorkerDoWork;

            _mediaWorker.RunWorkerCompleted +=
                MediaWorkerCompleted;
        }

        public void Bind(
            PackageStatus status,
            ModPageLoadResult page,
            string locale,
            ModPageService service,
            bool loading,
            string error)
        {
            _statusModel = status;
            _page = page;
            _service = service;
            _locale =
                String.IsNullOrWhiteSpace(locale)
                    ? "ru"
                    : locale;

            LocalizeTabs();

            if (status == null ||
                status.CatalogPackage == null)
            {
                _galleryRow.Height = 0F;
                _tabsRow.Height = 0F;
                _highlightRow.Height = 0F;

                _content.SetText(
                    IsRu
                        ? "Выберите мод."
                        : "Select a mod.");

                ClearGallery();
                return;
            }

            if (page == null)
            {
                _galleryRow.Height = 0F;
                _tabsRow.Height = 0F;
                _highlightRow.Height = 0F;

                if (loading)
                {
                    _content.SetText(
                        IsRu
                            ? "Загрузка проверенной страницы мода..."
                            : "Loading validated product page...");
                }
                else
                {
                    StringBuilder fallback =
                        new StringBuilder();

                    if (!String.IsNullOrWhiteSpace(
                        status.CatalogPackage.description))
                    {
                        fallback.AppendLine(
                            status.CatalogPackage.description);
                    }

                    if (!String.IsNullOrWhiteSpace(error))
                    {
                        if (fallback.Length > 0)
                            fallback.AppendLine();

                        fallback.AppendLine(
                            IsRu
                                ? "Расширенная страница сейчас недоступна."
                                : "The rich product page is currently unavailable.");
                    }

                    _content.SetText(
                        fallback.ToString());
                }

                ClearGallery();
                return;
            }

            _galleryRow.Height = 204F;
            _tabsRow.Height = 46F;

            bool hasScreenshots =
                page.Page != null &&
                page.Page.media != null &&
                page.Page.media.screenshots != null &&
                page.Page.media.screenshots.Count > 0;

            ApplyGalleryMode(
                hasScreenshots);

            if (_activeTab ==
                ProductTab.About &&
                page.Page != null &&
                page.Page.highlights != null &&
                page.Page.highlights.Count > 0)
            {
                _highlightRow.Height = 78F;
            }
            else
            {
                _highlightRow.Height = 0F;
            }

            string newMediaKey =
                page.PackageId + "|" +
                page.ActualCommit;

            if (!String.Equals(
                _mediaKey,
                newMediaKey,
                StringComparison.Ordinal))
            {
                _mediaKey =
                    newMediaKey;

                _generation++;

                ClearGallery();
                LoadCover(page);
                QueueScreenshots();
            }

            RenderHighlights();
            RenderContent();
            UpdateTabStyles();
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                DisposeOwnedImages();
            }

            base.Dispose(disposing);
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
            _root.Dock =
                DockStyle.Fill;

            _root.BackColor =
                Theme.Panel;

            _root.ColumnCount = 1;
            _root.RowCount = 6;

            _root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    88F));

            _galleryRow =
                new RowStyle(
                    SizeType.Absolute,
                    0F);

            _root.RowStyles.Add(
                _galleryRow);

            _tabsRow =
                new RowStyle(
                    SizeType.Absolute,
                    0F);

            _root.RowStyles.Add(
                _tabsRow);

            _root.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            _highlightRow =
                new RowStyle(
                    SizeType.Absolute,
                    0F);

            _root.RowStyles.Add(
                _highlightRow);

            _root.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    62F));

            Controls.Add(
                _root);

            BuildHeader();
            BuildGallery();
            BuildTabs();
            BuildHighlights();
            BuildActions();

            _content.Dock =
                DockStyle.Fill;

            _content.Margin =
                new Padding(0);

            _root.Controls.Add(
                _content,
                0,
                3);
        }

        private void BuildHeader()
        {
            TableLayoutPanel header =
                new TableLayoutPanel();

            header.Dock =
                DockStyle.Fill;

            header.BackColor =
                Theme.PanelRaised;

            header.Padding =
                new Padding(
                    14,
                    8,
                    14,
                    8);

            header.ColumnCount = 3;

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    76F));

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    100F));

            header.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    142F));

            header.RowCount = 2;

            header.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    45F));

            header.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    55F));

            _identity.Dock =
                DockStyle.Fill;

            _identity.Margin =
                new Padding(
                    0,
                    0,
                    10,
                    0);

            _identity.SizeMode =
                PictureBoxSizeMode.Zoom;

            _identity.BackColor =
                Color.Transparent;

            _title.Dock =
                DockStyle.Fill;

            _title.Margin =
                new Padding(
                    0,
                    1,
                    8,
                    0);

            _title.ForeColor =
                Theme.TextPrimary;

            _title.Font =
                Theme.UiFont(
                    15F,
                    FontStyle.Bold);

            _title.TextAlign =
                ContentAlignment.BottomLeft;

            _title.AutoEllipsis = true;

            _subtitle.Dock =
                DockStyle.Fill;

            _subtitle.Margin =
                new Padding(
                    0,
                    2,
                    8,
                    0);

            _subtitle.ForeColor =
                Theme.TextSecondary;

            _subtitle.Font =
                Theme.UiFont(
                    9F,
                    FontStyle.Regular);

            _subtitle.TextAlign =
                ContentAlignment.TopLeft;

            _subtitle.AutoEllipsis = true;

            _status.Dock =
                DockStyle.Fill;

            _status.Margin =
                new Padding(
                    8,
                    13,
                    0,
                    13);

            _status.TextAlign =
                ContentAlignment.MiddleCenter;

            _status.ForeColor =
                Theme.TextPrimary;

            _status.Font =
                Theme.UiFont(
                    8.5F,
                    FontStyle.Regular);

            header.Controls.Add(
                _identity,
                0,
                0);

            header.SetRowSpan(
                _identity,
                2);

            header.Controls.Add(
                _title,
                1,
                0);

            header.Controls.Add(
                _subtitle,
                1,
                1);

            header.Controls.Add(
                _status,
                2,
                0);

            header.SetRowSpan(
                _status,
                2);

            _root.Controls.Add(
                header,
                0,
                0);
        }

        private void BuildGallery()
        {
            _gallery.Dock =
                DockStyle.Fill;

            _gallery.BackColor =
                Theme.Panel;

            _gallery.Padding =
                new Padding(
                    6,
                    6,
                    6,
                    4);

            _gallery.ColumnCount = 2;

            _gallery.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    66F));

            _gallery.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    34F));

            _gallery.RowCount = 1;
            _gallery.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));

            Panel heroFrame =
                new Panel();

            heroFrame.Dock =
                DockStyle.Fill;

            heroFrame.Margin =
                new Padding(
                    0,
                    0,
                    5,
                    0);

            heroFrame.BackColor =
                Theme.Border;

            heroFrame.Padding =
                new Padding(1);

            _hero.Dock =
                DockStyle.Fill;

            _hero.BackColor =
                Theme.WindowBackground;

            _hero.SizeMode =
                PictureBoxSizeMode.Zoom;

            _hero.Cursor =
                Cursors.Hand;

            _hero.Click += HeroClick;

            _toolTip.SetToolTip(
                _hero,
                "Click to open screenshot");

            heroFrame.Controls.Add(
                _hero);

            _gallery.Controls.Add(
                heroFrame,
                0,
                0);

            _thumbnailGrid.Dock =
                DockStyle.Fill;

            _thumbnailGrid.Margin = Padding.Empty;
            _thumbnailGrid.ColumnCount = 2;
            _thumbnailGrid.RowCount = 2;

            _thumbnailGrid.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50F));

            _thumbnailGrid.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Percent,
                    50F));

            _thumbnailGrid.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    50F));

            _thumbnailGrid.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    50F));

            for (int i = 0; i < 4; i++)
            {
                Panel frame =
                    new Panel();

                frame.Dock =
                    DockStyle.Fill;

                frame.Margin =
                    new Padding(
                        i % 2 == 0 ? 0 : 3,
                        i < 2 ? 0 : 3,
                        i % 2 == 0 ? 3 : 0,
                        i < 2 ? 3 : 0);

                frame.BackColor =
                    Theme.Border;

                frame.Padding =
                    new Padding(1);

                PictureBox thumb =
                    new PictureBox();

                thumb.Dock =
                    DockStyle.Fill;

                thumb.BackColor =
                    Theme.WindowBackground;

                thumb.SizeMode =
                    PictureBoxSizeMode.Zoom;

                thumb.Cursor =
                    Cursors.Hand;

                thumb.Click += ThumbnailClick;

                _thumbs[i] = thumb;

                frame.Controls.Add(
                    thumb);

                _thumbnailGrid.Controls.Add(
                    frame,
                    i % 2,
                    i / 2);
            }

            _gallery.Controls.Add(
                _thumbnailGrid,
                1,
                0);

            _root.Controls.Add(
                _gallery,
                0,
                1);
        }

        private void BuildTabs()
        {
            _tabs.Dock =
                DockStyle.Fill;

            _tabs.BackColor =
                Theme.PanelRaised;

            _tabs.ColumnCount = 6;
            _tabs.RowCount = 1;

            for (int i = 0; i < 6; i++)
            {
                _tabs.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        16.6667F));

                Button button =
                    new Button();

                button.Dock =
                    DockStyle.Fill;

                button.Margin = Padding.Empty;

                button.FlatStyle =
                    FlatStyle.Flat;

                button.FlatAppearance.BorderSize = 0;

                button.BackColor =
                    Theme.PanelRaised;

                button.ForeColor =
                    Theme.TextSecondary;

                button.Font =
                    Theme.UiFont(
                        8.5F,
                        FontStyle.Regular);

                button.TextImageRelation =
                    TextImageRelation.ImageBeforeText;

                button.Cursor =
                    Cursors.Hand;

                button.Tag =
                    (ProductTab)i;

                button.Click += TabClick;

                _tabButtons[i] = button;

                _tabs.Controls.Add(
                    button,
                    i,
                    0);
            }

            _tabButtons[0].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.About);

            _tabButtons[1].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Features);

            _tabButtons[2].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Screenshots);

            _tabButtons[3].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Compatibility);

            _tabButtons[4].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Faq);

            _tabButtons[5].Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Changelog);

            _root.Controls.Add(
                _tabs,
                0,
                2);
        }

        private void BuildHighlights()
        {
            _highlights.Dock =
                DockStyle.Fill;

            _highlights.BackColor =
                Theme.Panel;

            _highlights.Padding =
                new Padding(
                    10,
                    4,
                    10,
                    4);

            _highlights.ColumnCount = 4;
            _highlights.RowCount = 1;

            for (int i = 0; i < 4; i++)
            {
                _highlights.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        25F));
            }

            _root.Controls.Add(
                _highlights,
                0,
                4);
        }

        private void BuildActions()
        {
            _actionHost.Dock =
                DockStyle.Fill;

            _actionHost.BackColor =
                Theme.PanelRaised;

            _actionHost.Padding =
                new Padding(
                    12,
                    10,
                    12,
                    10);

            _manage.Dock =
                DockStyle.Right;

            _manage.Width = 170;

            _manage.Margin =
                new Padding(
                    8,
                    0,
                    0,
                    0);

            _manage.Image =
                UiAssets.SystemIcon(
                    AssetKeys.SystemUi.Manage);

            Theme.StyleActionButton(
                _manage,
                Color.FromArgb(
                    0x36,
                    0x31,
                    0x29),
                Theme.AccentSand);

            _install.Dock =
                DockStyle.Right;

            _install.Width = 190;

            Theme.StyleActionButton(
                _install,
                Theme.UpdateBlue,
                Theme.AccentCyan);

            _actionHost.Controls.Add(
                _manage);

            _actionHost.Controls.Add(
                _install);

            _root.Controls.Add(
                _actionHost,
                0,
                5);
        }

        private void ApplyGalleryMode(
            bool hasScreenshots)
        {
            if (_gallery.ColumnStyles.Count < 2)
                return;

            if (hasScreenshots)
            {
                _gallery.ColumnStyles[0].Width = 66F;
                _gallery.ColumnStyles[1].Width = 34F;

                _thumbnailGrid.Visible = true;

                _hero.Cursor =
                    Cursors.Hand;

                _toolTip.SetToolTip(
                    _hero,
                    IsRu
                        ? "Открыть скриншот"
                        : "Open screenshot");
            }
            else
            {
                _gallery.ColumnStyles[0].Width = 100F;
                _gallery.ColumnStyles[1].Width = 0F;

                _thumbnailGrid.Visible = false;

                _hero.Cursor =
                    Cursors.Default;

                _toolTip.SetToolTip(
                    _hero,
                    null);
            }
        }
        private void LocalizeTabs()
        {
            if (IsRu)
            {
                _tabButtons[0].Text = "О моде";
                _tabButtons[1].Text = "Функции";
                _tabButtons[2].Text = "Галерея";
                _tabButtons[3].Text = "Совмест.";
                _tabButtons[4].Text = "FAQ";
                _tabButtons[5].Text = "Версии";
            }
            else
            {
                _tabButtons[0].Text = "About";
                _tabButtons[1].Text = "Features";
                _tabButtons[2].Text = "Gallery";
                _tabButtons[3].Text = "Compat.";
                _tabButtons[4].Text = "FAQ";
                _tabButtons[5].Text = "Versions";
            }
        }

        private void TabClick(
            object sender,
            EventArgs e)
        {
            Button button =
                sender as Button;

            if (button == null ||
                !(button.Tag is ProductTab))
            {
                return;
            }

            _activeTab =
                (ProductTab)button.Tag;

            if (_activeTab ==
                ProductTab.About &&
                _page != null &&
                _page.Page != null &&
                _page.Page.highlights != null &&
                _page.Page.highlights.Count > 0)
            {
                _highlightRow.Height = 78F;
            }
            else
            {
                _highlightRow.Height = 0F;
            }

            UpdateTabStyles();
            RenderContent();
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                bool selected =
                    (ProductTab)i ==
                    _activeTab;

                Button button =
                    _tabButtons[i];

                button.BackColor =
                    selected
                        ? Color.FromArgb(
                            0x32,
                            0x31,
                            0x29)
                        : Theme.PanelRaised;

                button.ForeColor =
                    selected
                        ? Theme.TextPrimary
                        : Theme.TextSecondary;

                button.Font =
                    Theme.UiFont(
                        8F,
                        selected
                            ? FontStyle.Bold
                            : FontStyle.Regular);

                button.FlatAppearance.BorderSize =
                    selected ? 1 : 0;

                button.FlatAppearance.BorderColor =
                    Theme.AccentSand;
            }
        }

        private void RenderHighlights()
        {
            _highlights.Controls.Clear();

            if (_page == null ||
                _page.Page == null ||
                _page.Page.highlights == null)
            {
                return;
            }

            int count =
                Math.Min(
                    4,
                    _page.Page.highlights.Count);

            for (int i = 0; i < count; i++)
            {
                string key =
                    _page.Page.highlights[i];

                TableLayoutPanel item =
                    new TableLayoutPanel();

                item.Dock =
                    DockStyle.Fill;

                item.Margin =
                    new Padding(
                        4,
                        0,
                        4,
                        0);

                item.BackColor =
                    Color.Transparent;

                item.ColumnCount = 2;

                item.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Absolute,
                        48F));

                item.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        100F));

                PictureBox icon =
                    new PictureBox();

                icon.Dock =
                    DockStyle.Fill;

                icon.Margin =
                    new Padding(
                        5,
                        8,
                        7,
                        8);

                icon.SizeMode =
                    PictureBoxSizeMode.Zoom;

                icon.Image =
                    UiAssets.Highlight(
                        key);

                Label label =
                    new Label();

                label.Dock =
                    DockStyle.Fill;

                label.Margin = Padding.Empty;

                label.ForeColor =
                    Theme.TextSecondary;

                label.Font =
                    Theme.UiFont(
                        8.75F,
                        FontStyle.Regular);

                label.TextAlign =
                    ContentAlignment.MiddleLeft;

                label.Text =
                    HighlightLabels.Get(
                        key,
                        _locale);

                item.Controls.Add(
                    icon,
                    0,
                    0);

                item.Controls.Add(
                    label,
                    1,
                    0);

                _highlights.Controls.Add(
                    item,
                    i,
                    0);
            }
        }

        private void RenderContent()
        {
            if (_statusModel == null ||
                _statusModel.CatalogPackage == null)
            {
                _content.SetText("");
                return;
            }

            if (_page == null ||
                _page.Locale == null)
            {
                _content.SetText(
                    _statusModel.CatalogPackage.description ?? "");
                return;
            }

            StringBuilder text =
                new StringBuilder();

            switch (_activeTab)
            {
                case ProductTab.About:
                    text.Append(
                        CompactAbout(
                            _page.Locale.description));

                    break;

                case ProductTab.Features:
                    AppendFeatures(
                        text);

                    break;

                case ProductTab.Screenshots:
                    AppendScreenshots(
                        text);

                    break;

                case ProductTab.Compatibility:
                    text.Append(
                        IsRu
                            ? "Отдельная структурированная информация о совместимости пока не опубликована на странице этого мода."
                            : "No separate structured compatibility information is published on this mod page yet.");

                    break;

                case ProductTab.Faq:
                    AppendFaq(
                        text);

                    break;

                case ProductTab.Changelog:
                    AppendChangelog(
                        text);

                    break;
            }

            _content.SetText(
                text.ToString());
        }

        private void AppendFeatures(
            StringBuilder text)
        {
            if (_page.Locale.features == null ||
                _page.Locale.features.Count == 0)
            {
                text.Append(
                    IsRu
                        ? "Список возможностей пока не опубликован."
                        : "No feature list has been published yet.");

                return;
            }

            foreach (string feature
                in _page.Locale.features)
            {
                text.Append("• ");
                text.AppendLine(feature);
                text.AppendLine();
            }
        }

        private void AppendScreenshots(
            StringBuilder text)
        {
            if (_page.Page == null ||
                _page.Page.media == null ||
                _page.Page.media.screenshots == null ||
                _page.Page.media.screenshots.Count == 0)
            {
                text.Append(
                    IsRu
                        ? "Скриншоты пока не опубликованы."
                        : "No screenshots have been published yet.");

                return;
            }

            text.AppendLine(
                IsRu
                    ? "Скриншоты страницы:"
                    : "Page screenshots:");

            text.AppendLine();

            foreach (ModPageScreenshot screenshot
                in _page.Page.media.screenshots)
            {
                if (screenshot == null)
                    continue;

                string caption = null;

                if (_page.Locale.screenshotCaptions != null)
                {
                    _page.Locale.screenshotCaptions.TryGetValue(
                        screenshot.id,
                        out caption);
                }

                text.Append("• ");

                if (!String.IsNullOrWhiteSpace(caption))
                    text.AppendLine(caption);
                else
                    text.AppendLine(screenshot.id);
            }

            text.AppendLine();

            text.Append(
                IsRu
                    ? "Миниатюры в галерее можно нажимать, чтобы открыть кадр крупно."
                    : "Click a gallery thumbnail to show that screenshot in the large preview.");
        }

        private void AppendFaq(
            StringBuilder text)
        {
            if (_page.Locale.faq == null ||
                _page.Locale.faq.Count == 0)
            {
                text.Append(
                    IsRu
                        ? "FAQ пока не опубликован."
                        : "No FAQ has been published yet.");

                return;
            }

            for (int i = 0;
                i < _page.Locale.faq.Count;
                i++)
            {
                ModPageFaqItem item =
                    _page.Locale.faq[i];

                if (item == null)
                    continue;

                text.AppendLine(
                    item.question ?? "");

                text.AppendLine(
                    item.answer ?? "");

                if (i <
                    _page.Locale.faq.Count - 1)
                {
                    text.AppendLine();
                }
            }
        }

        private void AppendChangelog(
            StringBuilder text)
        {
            if (_page.Locale.changelog == null ||
                _page.Locale.changelog.Count == 0)
            {
                text.Append(
                    IsRu
                        ? "История версий пока не опубликована."
                        : "No changelog has been published yet.");

                return;
            }

            foreach (ModPageChangelogEntry entry
                in _page.Locale.changelog)
            {
                if (entry == null)
                    continue;

                text.Append(
                    entry.version ?? "");

                if (!String.IsNullOrWhiteSpace(
                    entry.date))
                {
                    text.Append("  •  ");
                    text.Append(entry.date);
                }

                text.AppendLine();

                if (entry.items != null)
                {
                    foreach (string item
                        in entry.items)
                    {
                        text.Append("• ");
                        text.AppendLine(item);
                    }
                }

                text.AppendLine();
            }
        }

        private void LoadCover(
            ModPageLoadResult page)
        {
            if (page == null ||
                String.IsNullOrWhiteSpace(
                    page.CoverPath) ||
                !File.Exists(
                    page.CoverPath))
            {
                return;
            }

            try
            {
                Image image =
                    LoadUnlocked(
                        page.CoverPath);

                _ownedImages.Add(
                    image);

                _hero.Image =
                    image;
            }
            catch
            {
            }
        }

        private void QueueScreenshots()
        {
            if (_statusModel == null ||
                _statusModel.CatalogPackage == null ||
                _page == null ||
                _page.Page == null ||
                _page.Page.media == null ||
                _page.Page.media.screenshots == null ||
                _page.Page.media.screenshots.Count == 0 ||
                _service == null)
            {
                return;
            }

            MediaLoadRequest request =
                new MediaLoadRequest();

            request.Generation =
                _generation;

            request.Package =
                _statusModel.CatalogPackage;

            request.Page =
                _page;

            request.Service =
                _service;

            if (_mediaWorker.IsBusy)
            {
                _pendingMedia =
                    request;

                return;
            }

            _mediaWorker.RunWorkerAsync(
                request);
        }

        private void MediaWorkerDoWork(
            object sender,
            DoWorkEventArgs e)
        {
            MediaLoadRequest request =
                e.Argument as MediaLoadRequest;

            MediaLoadResult result =
                new MediaLoadResult();

            result.Request =
                request;

            result.Screenshots =
                new List<LoadedScreenshot>();

            if (request == null ||
                request.Page == null ||
                request.Page.Page == null ||
                request.Page.Page.media == null ||
                request.Page.Page.media.screenshots == null)
            {
                e.Result = result;
                return;
            }

            IEnumerable<ModPageScreenshot> screenshots =
                request.Page.Page.media.screenshots.Take(5);

            foreach (ModPageScreenshot screenshot
                in screenshots)
            {
                if (screenshot == null)
                    continue;

                try
                {
                    string path =
                        request.Service.EnsureScreenshot(
                            request.Package,
                            request.Page,
                            screenshot.id);

                    Image image =
                        LoadUnlocked(
                            path);

                    LoadedScreenshot loaded =
                        new LoadedScreenshot();

                    loaded.Id =
                        screenshot.id;

                    loaded.Image =
                        image;

                    result.Screenshots.Add(
                        loaded);
                }
                catch
                {
                }
            }

            e.Result =
                result;
        }

        private void MediaWorkerCompleted(
            object sender,
            RunWorkerCompletedEventArgs e)
        {
            MediaLoadResult result =
                e.Result as MediaLoadResult;

            bool current =
                result != null &&
                result.Request != null &&
                result.Request.Generation ==
                    _generation;

            if (current)
            {
                ApplyScreenshots(
                    result.Screenshots);
            }
            else if (result != null &&
                result.Screenshots != null)
            {
                foreach (LoadedScreenshot item
                    in result.Screenshots)
                {
                    if (item != null &&
                        item.Image != null)
                    {
                        item.Image.Dispose();
                    }
                }
            }

            if (_pendingMedia != null)
            {
                MediaLoadRequest pending =
                    _pendingMedia;

                _pendingMedia = null;

                _mediaWorker.RunWorkerAsync(
                    pending);
            }
        }

        private void ApplyScreenshots(
            List<LoadedScreenshot> screenshots)
        {
            if (screenshots == null ||
                screenshots.Count == 0)
            {
                return;
            }

            foreach (LoadedScreenshot item
                in screenshots)
            {
                if (item != null &&
                    item.Image != null)
                {
                    _ownedImages.Add(
                        item.Image);
                }
            }

            _galleryImages.Clear();
            _galleryCaptions.Clear();

            foreach (LoadedScreenshot item
                in screenshots)
            {
                if (item == null ||
                    item.Image == null)
                {
                    continue;
                }

                _galleryImages.Add(
                    item.Image);

                _galleryCaptions.Add(
                    GetScreenshotCaption(
                        item.Id));
            }

            _hero.Image =
                screenshots[0].Image;

            _hero.Tag = 0;

            for (int i = 0;
                i < _thumbs.Length;
                i++)
            {
                int sourceIndex =
                    i + 1;

                PictureBox thumb =
                    _thumbs[i];

                if (sourceIndex <
                    screenshots.Count)
                {
                    LoadedScreenshot item =
                        screenshots[sourceIndex];

                    thumb.Image =
                        item.Image;

                    thumb.Tag =
                        sourceIndex;

                    string caption =
                        GetScreenshotCaption(
                            item.Id);

                    _toolTip.SetToolTip(
                        thumb,
                        caption);
                }
                else
                {
                    thumb.Image = null;
                    thumb.Tag = null;
                    _toolTip.SetToolTip(
                        thumb,
                        null);
                }
            }
        }

        private string GetScreenshotCaption(
            string id)
        {
            if (_page == null ||
                _page.Locale == null ||
                _page.Locale.screenshotCaptions == null ||
                String.IsNullOrWhiteSpace(id))
            {
                return id ?? "";
            }

            string caption;

            if (_page.Locale.screenshotCaptions.TryGetValue(
                id,
                out caption))
            {
                return caption ?? id;
            }

            return id;
        }

        private void HeroClick(
            object sender,
            EventArgs e)
        {
            if (_galleryImages.Count == 0)
                return;

            int index = 0;

            if (_hero.Tag is int)
                index = (int)_hero.Tag;

            using (ScreenshotViewerForm viewer =
                new ScreenshotViewerForm(
                    _galleryImages,
                    _galleryCaptions,
                    index))
            {
                viewer.ShowDialog(
                    FindForm());
            }
        }

        private static string CompactAbout(
            string text)
        {
            if (String.IsNullOrWhiteSpace(text))
                return "";

            string compact =
                text.Trim();

            const int maxLength = 360;

            if (compact.Length <= maxLength)
                return compact;

            int cut =
                compact.LastIndexOf(
                    ' ',
                    maxLength);

            if (cut < 220)
                cut = maxLength;

            return
                compact.Substring(
                    0,
                    cut).TrimEnd() +
                "…";
        }
        private void ThumbnailClick(
            object sender,
            EventArgs e)
        {
            PictureBox thumb =
                sender as PictureBox;

            if (thumb == null ||
                thumb.Tag == null)
            {
                return;
            }

            if (!(thumb.Tag is int))
                return;

            int index =
                (int)thumb.Tag;

            if (index < 0 ||
                index >= _galleryImages.Count)
            {
                return;
            }

            _hero.Image =
                _galleryImages[index];

            _hero.Tag =
                index;
        }

        private void ClearGallery()
        {
            _generation++;

            _pendingMedia = null;
            _mediaKey = null;

            _hero.Image = null;
            _hero.Tag = null;

            _galleryImages.Clear();
            _galleryCaptions.Clear();

            for (int i = 0;
                i < _thumbs.Length;
                i++)
            {
                _thumbs[i].Image = null;
                _thumbs[i].Tag = null;
            }

            DisposeOwnedImages();
        }

        private void DisposeOwnedImages()
        {
            foreach (Image image
                in _ownedImages)
            {
                if (image != null)
                    image.Dispose();
            }

            _ownedImages.Clear();
        }

        private static Image LoadUnlocked(
            string path)
        {
            using (Image source =
                Image.FromFile(path))
            {
                Bitmap copy =
                    new Bitmap(
                        source.Width,
                        source.Height);

                using (Graphics graphics =
                    Graphics.FromImage(copy))
                {
                    graphics.DrawImage(
                        source,
                        new Rectangle(
                            0,
                            0,
                            source.Width,
                            source.Height));
                }

                return copy;
            }
        }
    }
}