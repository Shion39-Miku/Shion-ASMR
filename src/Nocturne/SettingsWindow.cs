using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nocturne;
public sealed class SettingsWindow : Window
{
 const string Fallback=", Microsoft YaHei, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI";
 public SettingsWindow(Settings s,OverlayWindow overlay,Action? live=null)
 {
  NativeTheme.Attach(this);Title="月咲 Shion · 设置";Background=new SolidColorBrush(Color.FromRgb(15,21,38));Foreground=(Brush)Application.Current.FindResource("Ink");FontFamily=new FontFamily("Microsoft YaHei UI");Width=540;Height=740;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
  var panel=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  void Label(string title)=>panel.Children.Add(new TextBlock{Text=title,FontSize=20,Margin=new Thickness(0,22,0,12)});
  void Choice(string[] names,int index,Action<int> changed){var c=new ComboBox{ItemsSource=names,SelectedIndex=Math.Clamp(index,0,names.Length-1)};c.SelectionChanged+=(_,_)=>{changed(c.SelectedIndex);live?.Invoke();};panel.Children.Add(c);}
  void Check(string title,bool value,Action<bool> changed){var c=new CheckBox{Content=title,IsChecked=value};c.Click+=(_,_)=>{changed(c.IsChecked==true);live?.Invoke();};panel.Children.Add(c);}
  void Slide(string title,double min,double max,double value,Action<double> changed){var label=new TextBlock{Text=$"{title}   {value:0.##}",Margin=new Thickness(6,10,0,0)};panel.Children.Add(label);var slider=new Slider{Minimum=min,Maximum=max,Value=value};slider.ValueChanged+=(_,e)=>{label.Text=$"{title}   {e.NewValue:0.##}";changed(e.NewValue);live?.Invoke();};panel.Children.Add(slider);}
  void FontChoice(string current,Action<string> changed){var names=new[]{"Microsoft YaHei","Microsoft YaHei UI","Microsoft JhengHei UI","Yu Gothic UI","Yu Mincho","SimSun","Segoe UI"};var selected=current.Split(',')[0].Trim();var c=new ComboBox{ItemsSource=names,SelectedItem=names.Contains(selected)?selected:names[0]};c.SelectionChanged+=(_,_)=>{changed((string)c.SelectedItem+Fallback);live?.Invoke();};panel.Children.Add(c);}
  Label("字幕页媒体布局");Check("显示图片",s.ShowPictures,v=>s.ShowPictures=v);Check("显示视频",s.ShowVideo,v=>s.ShowVideo=v);Choice(new[]{"图片在左侧","图片在右侧"},s.PictureOnRight?1:0,i=>s.PictureOnRight=i==1);Choice(new[]{"视频在左侧","视频在右侧"},s.VideoOnRight?1:0,i=>s.VideoOnRight=i==1);Label("视频内字幕");Check("显示视频内字幕",s.VideoSubtitles,v=>s.VideoSubtitles=v);Slide("视频字幕字号比例",.6,1.8,s.VideoSubtitleScale,v=>s.VideoSubtitleScale=v);panel.Children.Add(new TextBlock{Text="视频画面 CC 菜单可选择字幕、调整字号和逐轨偏移；全屏同样有效。",FontSize=12});
  Label("星空与樱花");Check("樱花点缀",s.Sakura,v=>s.Sakura=v);Slide("樱花强度",.2,1.5,s.SakuraStrength,v=>s.SakuraStrength=v);Choice(new[]{"静态星空","节能星空 · 5 FPS","流畅星空 · 60 FPS 目标"},s.Stars,i=>s.Stars=i);
  Check("减少动态效果",s.ReduceMotion,v=>s.ReduceMotion=v);Check("显示真实音频波形",s.Visualizer,v=>s.Visualizer=v);Slide("波形高度",16,48,s.WaveHeight,v=>s.WaveHeight=v);Slide("星空效果强度",.25,1.6,s.StarIntensity,v=>s.StarIntensity=v);
  var low=new Button{Content="低占用：静态星空、关闭波形与过渡"};low.Click+=(_,_)=>{s.Stars=0;s.Visualizer=false;s.ReduceMotion=true;live?.Invoke();};panel.Children.Add(low);
  Label("播放交互");Check("暂停淡出 / 恢复淡入 · 约 0.5 秒",s.PauseFade,v=>s.PauseFade=v);panel.Children.Add(new TextBlock{Text="仅应用前台生效（文本输入时除外）：\n← 后退 3 秒    → 前进 5 秒\n空格 暂停 / 继续\n↑ 音量 +5%    ↓ 音量 −5%\n字幕：单击选中，双击台词跳转。",FontSize=13,LineHeight=25,Margin=new Thickness(6,8,0,4)});
  Label("主字幕排版");var fontPreview=new TextBlock{Text="星光落在耳边 · おやすみなさい",FontSize=23,FontFamily=FontBook.For(s),Margin=new Thickness(6,10,0,10)};Check("艺术字体：内置霞鹜文楷",s.ArtLyrics,v=>{s.ArtLyrics=v;fontPreview.FontFamily=FontBook.For(s);});panel.Children.Add(fontPreview);FontChoice(s.LyricFontFamily,f=>s.LyricFontFamily=f);Slide("字号",18,44,s.FontSize,v=>s.FontSize=v);Choice(new[]{"常规","中等","半粗","粗体"},Array.IndexOf(new[]{400,500,600,700},s.LyricWeight),i=>s.LyricWeight=new[]{400,500,600,700}[i]);Slide("行距倍数",1.1,2.5,s.LyricLineSpacing,v=>s.LyricLineSpacing=v);Check("轻量文字阴影",s.LyricShadow,v=>s.LyricShadow=v);
  Label("桌面字幕");Check("艺术字体：内置霞鹜文楷",s.OverlayArt,v=>{s.OverlayArt=v;overlay.Apply();});FontChoice(s.OverlayFontFamily,f=>{s.OverlayFontFamily=f;s.OverlayArt=false;overlay.Apply();});Slide("字号",16,64,s.OverlayFont,v=>{s.OverlayFont=v;overlay.Apply();});Choice(new[]{"常规","中等","半粗","粗体"},Array.IndexOf(new[]{400,500,600,700},s.OverlayWeight),i=>{s.OverlayWeight=new[]{400,500,600,700}[i];overlay.Apply();});Slide("行距倍数",1.1,2.1,s.OverlayLineSpacing,v=>{s.OverlayLineSpacing=v;overlay.Apply();});
  Slide("不透明度",0.15,1,s.OverlayOpacity,v=>{s.OverlayOpacity=v;overlay.Apply();});var colors=new[]{"#FFB2D9","#FFFFFF","#FFE2AB","#B9E6E2"};Choice(new[]{"柔粉","白色","暖黄","浅青"},Array.IndexOf(colors,s.OverlayColor),i=>{s.OverlayColor=colors[i];overlay.Apply();});
  var colorRow=new DockPanel();var hex=new TextBox{Text=s.OverlayColor,Width=140};colorRow.Children.Add(hex);var colorApply=new Button{Content="应用颜色"};colorApply.Click+=(_,_)=>{try{var color=(Color)ColorConverter.ConvertFromString(hex.Text);s.OverlayColor=color.ToString();overlay.Apply();}catch{hex.Text=s.OverlayColor;}};colorRow.Children.Add(colorApply);panel.Children.Add(colorRow);
  Check("显示文字阴影",s.OverlayShadow,v=>{s.OverlayShadow=v;overlay.Apply();});Check("置顶显示",s.OverlayTopmost,v=>{s.OverlayTopmost=v;overlay.Apply();});
  panel.Children.Add(new TextBlock{Text="在播放栏点击“桌面字幕”开启编辑，再次点击即可关闭。按住黑底上的台词或空白处可拖动，工具栏可打开样式设置。锁定后恢复透明和鼠标穿透，将鼠标移到字幕附近即可点击小锁解锁。",FontSize=11,Foreground=(Brush)Application.Current.FindResource("Muted"),Margin=new Thickness(4,12,0,0)});
  Label("关于月咲 Shion");panel.Children.Add(new TextBlock{Text=Brand.Name+" "+typeof(SettingsWindow).Assembly.GetName().Version!.ToString(3)+" · 本地音声播放器",FontSize=15,FontWeight=FontWeights.SemiBold});Label("本地存储与格式");panel.Children.Add(new TextBlock{Text="媒体索引和设置沿用 %LOCALAPPDATA%\\Nocturne。各音轨独立保存进度与字幕，原始媒体不修改。\n\nWAV / MP3 / FLAC / M4A（AAC）及 Ogg Vorbis 的解码支持与第一版一致。已内置霞鹜文楷，按 SIL OFL 1.1 分发，授权见 Assets/Fonts/OFL.txt；清晰默认模式使用系统字体回退。",FontSize=12,Foreground=(Brush)Application.Current.FindResource("Muted")});
  var done=new Button{Content="完成",Margin=new Thickness(0,22,0,4)};done.Click+=(_,_)=>Close();panel.Children.Add(done);
 }
}
