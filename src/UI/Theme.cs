using System;
using System.Drawing;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal static class Theme
    {
        public static readonly Color WindowBackground = Color.FromArgb(0x00, 0x13, 0x1F);
        public static readonly Color Panel = Color.FromArgb(0x04, 0x1D, 0x2B);
        public static readonly Color PanelRaised = Color.FromArgb(0x08, 0x25, 0x36);
        public static readonly Color PanelSelected = Color.FromArgb(0x0A, 0x38, 0x50);
        public static readonly Color Border = Color.FromArgb(0x15, 0x52, 0x6B);
        public static readonly Color BorderSelected = Color.FromArgb(0x4A, 0xA4, 0xC4);
        public static readonly Color TextPrimary = Color.FromArgb(0xF2, 0xF3, 0xF0);
        public static readonly Color TextSecondary = Color.FromArgb(0x9E, 0xAD, 0xB3);
        public static readonly Color AccentSand = Color.FromArgb(0xE5, 0xC9, 0x91);
        public static readonly Color AccentCyan = Color.FromArgb(0x58, 0xC7, 0xF1);
        public static readonly Color InstalledGreen = Color.FromArgb(0x0B, 0x70, 0x48);
        public static readonly Color UpdateBlue = Color.FromArgb(0x0B, 0x57, 0x94);
        public static readonly Color Warning = Color.FromArgb(0xD5, 0x9A, 0x48);
        public static readonly Color Error = Color.FromArgb(0xB8, 0x55, 0x55);
        public static readonly Color Disabled = Color.FromArgb(0x52, 0x63, 0x6B);

        public static Font UiFont(float size, FontStyle style)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static void StyleNavButton(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = selected ? AccentSand : Border;
            button.FlatAppearance.MouseOverBackColor = PanelSelected;
            button.FlatAppearance.MouseDownBackColor = PanelSelected;
            button.BackColor = selected ? Color.FromArgb(45, 55, 48) : Panel;
            button.ForeColor = selected ? TextPrimary : TextSecondary;
            button.Font = UiFont(9F, selected ? FontStyle.Bold : FontStyle.Regular);
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(8, 0, 8, 0);
            button.Cursor = Cursors.Hand;
        }

        public static void StyleHeaderButton(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = selected ? AccentSand : Color.FromArgb(80, 255, 255, 255);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 0, 20, 30);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, 0, 20, 30);
            button.BackColor = selected ? Color.FromArgb(160, 32, 33, 27) : Color.FromArgb(145, 3, 22, 31);
            button.ForeColor = TextPrimary;
            button.Font = UiFont(9F, selected ? FontStyle.Bold : FontStyle.Regular);
            button.Cursor = Cursors.Hand;
        }

        public static void StyleActionButton(Button button, Color backColor, Color borderColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.MouseOverBackColor = Lighten(backColor, 12);
            button.FlatAppearance.MouseDownBackColor = Lighten(backColor, 5);
            button.BackColor = backColor;
            button.ForeColor = TextPrimary;
            button.Font = UiFont(9.5F, FontStyle.Bold);
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Cursor = Cursors.Hand;
        }

        public static Color StatusBackColor(PackageStatusKind kind)
        {
            switch (kind)
            {
                case PackageStatusKind.Installed:
                    return InstalledGreen;

                case PackageStatusKind.UpdateAvailable:
                    return UpdateBlue;

                case PackageStatusKind.NotInstalled:
                    return Color.FromArgb(0xB0, 0x86, 0x4E);

                case PackageStatusKind.DifferentBuild:
                case PackageStatusKind.Modified:
                    return Warning;

                case PackageStatusKind.PackageMissing:
                case PackageStatusKind.Error:
                    return Error;

                default:
                    return Disabled;
            }
        }

        private static Color Lighten(Color color, int amount)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(255, color.R + amount),
                Math.Min(255, color.G + amount),
                Math.Min(255, color.B + amount));
        }
    }
}
