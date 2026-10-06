// This project uses both WPF and WinForms (UseWindowsForms=true for tray icon support).
// Both assemblies export types with the same short name. These global aliases resolve every
// ambiguity project-wide so individual files never need to repeat them.
global using Application        = System.Windows.Application;
global using Brush               = System.Windows.Media.Brush;
global using Brushes             = System.Windows.Media.Brushes;
global using Button              = System.Windows.Controls.Button;
global using Color               = System.Windows.Media.Color;
global using ComboBox            = System.Windows.Controls.ComboBox;
global using FontFamily          = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Image               = System.Windows.Controls.Image;
global using Pen                 = System.Windows.Media.Pen;
global using Point               = System.Windows.Point;
global using Rect                = System.Windows.Rect;
global using Size                = System.Windows.Size;
global using UserControl         = System.Windows.Controls.UserControl;
