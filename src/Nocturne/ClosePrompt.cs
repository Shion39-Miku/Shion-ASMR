using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nocturne;

sealed class ClosePrompt : Window
{
    public bool RunInBackground { get; private set; }

    public ClosePrompt(MainWindow owner)
    {
        Owner = owner;
        Title = "关闭" + Brand.Name;
        Icon = Brand.Artwork;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = owner.FontFamily;
        Foreground = (Brush)FindResource("Ink");

        var content = new Grid();
        content.Children.Add(new StarField());
        var body = new StackPanel { Margin = new Thickness(26) };
        content.Children.Add(body);
        var header = new DockPanel();
        var cross = new Button { Content = new Symbol { Kind = "Close" }, Width = 35, Height = 35, Padding = new Thickness(7), ToolTip = "取消" };
        System.Windows.Automation.AutomationProperties.SetName(cross, "取消关闭");
        cross.Click += (_, _) => Close();
        DockPanel.SetDock(cross, Dock.Right);
        header.Children.Add(cross);
        var mark = new Image { Source = Brand.Artwork, Width = 38, Height = 38, Margin = new Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(mark, Dock.Left);
        header.Children.Add(mark);
        header.Children.Add(new TextBlock { Text = Title, FontSize = 23, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        body.Children.Add(header);
        body.Children.Add(new TextBlock
        {
            Text = "要在后台继续运行吗？可以通过系统托盘重新打开窗口。",
            FontSize = 15, Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 17, 0, 20)
        });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var background = new Button { Content = "后台运行", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(133, 93, 130)) };
        background.Click += (_, _) => { RunInBackground = true; DialogResult = true; };
        var exit = new Button { Content = "退出软件" };
        exit.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "取消", IsCancel = true };
        actions.Children.Add(background);
        actions.Children.Add(exit);
        actions.Children.Add(cancel);
        body.Children.Add(actions);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(20, 21, 40)),
            BorderBrush = (Brush)FindResource("PanelStroke"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18), Child = content
        };
        Loaded += (_, _) => Keyboard.Focus(background);
    }
}
