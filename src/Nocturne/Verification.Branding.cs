using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SkiaSharp;

namespace Nocturne;

public static partial class Verification
{
    static async Task VerifyBrandingRelease(MainWindow window)
    {
        window.Width = 1210;
        window.Height = 850;
        window.Left = 30;
        window.Top = 30;
        window.settings.Volume = .05;
        window.Volume.Value = .05;
        window.settings.Stars = 2;
        window.settings.ReduceMotion = false;
        var handle = new WindowInteropHelper(window).Handle;
        var exe = Environment.ProcessPath!;
        Check("品牌名称、窗口及任务栏图标",window.Title==Brand.Name&&window.Icon==Brand.Artwork
            &&FileVersionInfo.GetVersionInfo(exe).ProductName==Brand.Name
            &&SendMessage(handle,0x7f,IntPtr.Zero,IntPtr.Zero)!=IntPtr.Zero);
        Check("托盘设置品牌名称并成功注册",window.TrayTitle==Brand.Name&&window.Status.Text.IndexOf("系统托盘不可用",StringComparison.Ordinal)<0);
        Check("原程序集及数据目录标识",typeof(MainWindow).Assembly.GetName().Name=="Nocturne"&&Path.GetFileName(State.DefaultHome)== "Nocturne");
        Check("欢迎页品牌和文案",window.WelcomeTitle.Text=="欢迎来到月咲 Shion"&&Visuals<TextBlock>(window.LandingPage).Any(t=>t.Text=="让声音，在夜里轻轻盛开。"));

        var root = Path.Combine(State.Home,"演示媒体");
        var workPath = Path.Combine(root,"月畔的回声");
        var movieDirectory = Path.Combine(root,"星光短片");
        Directory.CreateDirectory(workPath);
        Directory.CreateDirectory(movieDirectory);
        var audio = Path.Combine(workPath,"01 月夜来信.wav");
        var movie = Path.Combine(movieDirectory,"星光短片.mp4");
        WriteWave(audio,30);
        WriteWave(Path.Combine(workPath,"02 远处的风.wav"),5);
        File.Copy(Path.Combine(Environment.CurrentDirectory,"test-fixtures","generated.mp4"),movie);
        const string subtitles="WEBVTT\n\n00:00.000 --> 00:03.500\n把白天的忙碌留在门外。\n\n00:04.000 --> 00:11.500\n闭上眼睛，听风经过窗边。\n\n00:12.000 --> 00:21.000\n今晚的月光，安静得刚刚好。\n\n00:22.000 --> 00:27.000\n让夜色慢慢沉下来。\n";
        File.WriteAllText(Path.ChangeExtension(audio,".vtt"),subtitles);
        File.WriteAllText(Path.ChangeExtension(movie,".vtt"),"WEBVTT\n\n00:00.000 --> 00:19.000\n演示短片 · 视频字幕跟随画面\n");
        MakeDemoCover(Path.Combine(workPath,"封面.png"));
        window.settings.Roots.Add(root);
        window.settings.WorkAssignments[workPath]=workPath;
        const string circle="demo-circle",series="demo-series";
        window.settings.Organization[circle]=new(){Id=circle,Name="星河录音室"};
        window.settings.Organization[series]=new(){Id=series,Parent=circle,Name="夜色来信"};
        window.settings.WorkLocations[workPath]=series;
        MetadataStore.SetCircle(window.settings,new[]{workPath},"星河录音室");
        MetadataStore.AddTag(window.settings,new[]{workPath},"夜读");
        await window.Scan(true);
        await Delay(4100);
        Snapshot(window,Path.Combine(State.Home,"welcome.png"));
        await window.SetBrowseMode(false);
        await window.EnterFolder(circle);
        await Delay(550);
        Check("演示分类包含系列文件夹",Tiles(window).Any(t=>t.Folder?.Id==series));
        Snapshot(window,Path.Combine(State.Home,"library.png"));
        await window.EnterFolder(series);
        window.SelectWork(window.works.Single(x=>x.Groups.Any(g=>g.Audio.Contains(audio))));
        Check("发布版播放 WAV",window.OpenTrack(audio,true,6));
        await window.SubtitleReady;
        await window.ImageReady;
        await Delay(520);
        Check("播放页显示同名字幕与作品图",window.CurrentSubtitle.Contains("听风")&&window.LyricsPicture.Source!=null);
        Snapshot(window,Path.Combine(State.Home,"audio-lyrics.png"));

        window.overlay.Left=165;window.overlay.Top=170;window.overlay.Width=670;
        window.RestoreFromTrayForTest();
        await Delay(350);
        Snapshot(window.overlay,Path.Combine(State.Home,"desktop-edit.png"));
        window.overlay.EndEditing();
        await Delay(350);
        if(NativeProbe.TargetTestWindow(handle))
        {
            var backdrop=new Window
            {
                Width=710,Height=260,Left=145,Top=155,WindowStyle=WindowStyle.None,ShowInTaskbar=false,
                Background=new SolidColorBrush(Color.FromRgb(20,23,42)),Content=new StarField()
            };
            backdrop.Show();
            await Delay(160);
            var glyph = window.overlay.GlyphBounds;
            var cursor=window.overlay.PointToScreen(new Point(glyph.Left+glyph.Width/2,glyph.Top+glyph.Height/2));
            NativeProbe.SetCursorPos((int)cursor.X,(int)cursor.Y);
            await Delay(350);
            Check("桌面字幕锁定透明并穿透",window.overlay.NativeClickThrough&&window.overlay.TransparentSurface&&window.overlay.UnlockVisible);
            SaveDesktopComposition(window.overlay,Path.Combine(State.Home,"desktop-locked.png"));
            backdrop.Close();
        }
        else
        {
            Check("桌面字幕锁定透明并穿透",window.overlay.NativeClickThrough&&window.overlay.TransparentSurface);
            Snapshot(window.overlay,Path.Combine(State.Home,"desktop-locked.png"));
        }

        window.PausePlayback();
        Check("发布版播放 MP4",await window.OpenVideo(movie,true,true,2));
        await window.SubtitleReady;
        await Delay(600);
        Check("视频帧与字幕同时显示",window.VideoImage.Source!=null&&window.VideoCaptionText.Contains("演示短片"));
        Snapshot(window,Path.Combine(State.Home,"video.png"));
        window.CloseVideoSession();
        window.OpenTrack(audio,true,10.2);
        await window.SubtitleReady;
        using(var unused = new System.Drawing.Bitmap(1,1))
        {
            var settings = new SettingsWindow(window.settings,window.overlay){Owner=window};
            settings.Show();
            await Delay(220);
            Snapshot(settings,Path.Combine(State.Home,"settings.png"));
            settings.Close();
        }
        var page=window.CurrentPage;
        var track=window.currentTrack;
        var before=window.PlaybackPosition;
        await RunClosePrompt(window,prompt=>
        {
            Check("默认强调后台运行并且重复关闭仅一个弹窗",Visuals<Button>(prompt).Single(b=>Equals(b.Content,"后台运行")).IsDefault
                &&Application.Current.Windows.OfType<ClosePrompt>().Count()==1&&window.PlaybackPlaying);
            Snapshot(prompt,Path.Combine(State.Home,"close-prompt.png"));
            Visuals<Button>(prompt).Single(b=>Equals(b.Content,"取消")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            prompt.Close();
        },repeat:true);
        Check("取消保留播放及主窗口",window.IsVisible&&window.PlaybackPlaying&&window.PlaybackPosition>before);
        await RunClosePrompt(window,prompt=>prompt.Close(),systemClose:true);
        Check("系统关闭请求弹窗，关闭弹窗继续使用",window.IsVisible&&window.PlaybackPlaying);
        await RunClosePrompt(window,prompt=>ClickPrompt(prompt,"后台运行"));
        var hiddenPosition=window.PlaybackPosition;
        await Delay(1900);
        Check("后台播放及桌面字幕继续，主窗绘制停止",!window.IsVisible&&window.PlaybackPlaying
            &&window.PlaybackPosition>hiddenPosition+1&&!window.Stars.Running&&!window.Wave.Running
            &&window.overlay.IsVisible&&window.overlay.DisplayedText.Contains("今晚的月光"));
        // Replay the Windows tray callback rather than calling the restore method directly.
        NativeProbe.PostMessage(handle,0x8001,new IntPtr(1),new IntPtr(0x203));
        await Delay(230);
        Check("托盘双击消息恢复同一窗口、页面与音轨",window.IsVisible&&window.CurrentPage==page
            &&window.currentTrack==track&&window.PlaybackPlaying&&new WindowInteropHelper(window).Handle==handle);
        if(Environment.GetCommandLineArgs().Contains("--tray-exit")) window.InvokeTrayExitForTest();
        else await RunClosePrompt(window,prompt=>ClickPrompt(prompt,"退出软件"));
        await Delay(170);
        var saved=State.Read<Settings>("settings.json");
        Check("完整退出关闭主窗、桌面字幕和播放引擎并保存进度",window.ShuttingDown&&!window.IsVisible
            &&!window.overlay.IsVisible&&window.engine.FilePath==null&&saved.Tracks[audio].Position>hiddenPosition,allowClosed:true);
        using var probe = StartProbe("--startup-probe",State.Home,"--resume-probe",audio);
        await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
        using var result=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));
        Check("新进程读回旧标识下的媒体库和进度",probe.ExitCode==0&&result.RootElement.GetProperty("opened").GetBoolean()
            &&result.RootElement.GetProperty("position").GetDouble()>hiddenPosition,allowClosed:true);
    }

    static void ClickPrompt(ClosePrompt prompt,string title) =>
        Visuals<Button>(prompt).Single(b=>Equals(b.Content,title)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static async Task RunClosePrompt(MainWindow window,Action<ClosePrompt> answer,bool repeat=false,bool systemClose=false)
    {
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        Exception? error=null;
        var prompted=false;
        timer.Tick+=(_,_)=>
        {
            var prompt=Application.Current.Windows.OfType<ClosePrompt>().SingleOrDefault();
            if(prompt==null)return;
            timer.Stop();
            prompted=true;
            try
            {
                if(repeat)window.Close();
                answer(prompt);
            }
            catch(Exception ex){error=ex;prompt.Close();}
        };
        timer.Start();
        if(systemClose) NativeProbe.PostMessage(new WindowInteropHelper(window).Handle,0x112,new IntPtr(0xF060),IntPtr.Zero);
        else window.CloseWindowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for(int i=0;i<100&&(!prompted||Application.Current.Windows.OfType<ClosePrompt>().Any());i++)await Delay(30);
        timer.Stop();
        if(error!=null)throw error;
        if(!prompted)throw new InvalidOperationException("关闭提示未打开。");
    }

    static void MakeDemoCover(string path)
    {
        using var bitmap=new SKBitmap(540,720);
        using var canvas=new SKCanvas(bitmap);
        using var sky=new SKPaint{Shader=SKShader.CreateLinearGradient(new SKPoint(0,0),new SKPoint(540,720),new[]{new SKColor(20,24,53),new SKColor(71,53,95),new SKColor(180,101,129)},null,SKShaderTileMode.Clamp),IsAntialias=true};
        canvas.DrawRect(0,0,540,720,sky);
        using var paint=new SKPaint{Color=new SKColor(250,221,226),IsAntialias=true};
        canvas.DrawCircle(350,221,67,paint);
        var random=new Random(52);
        for(int i=0;i<65;i++)canvas.DrawCircle(random.Next(540),random.Next(480),random.Next(1,4),paint);
        paint.Color=new SKColor(27,28,55);
        using var mountains=new SKPath();
        mountains.MoveTo(0,530);mountains.LineTo(166,423);mountains.LineTo(304,501);
        mountains.LineTo(443,412);mountains.LineTo(540,530);mountains.LineTo(540,720);mountains.LineTo(0,720);mountains.Close();
        canvas.DrawPath(mountains,paint);
        using var image=SKImage.FromBitmap(bitmap);
        using var data=image.Encode(SKEncodedImageFormat.Png,100);
        File.WriteAllBytes(path,data.ToArray());
    }

    static void SaveDesktopComposition(OverlayWindow overlay,string path)
    {
        var start=overlay.PointToScreen(new Point());
        var end=overlay.PointToScreen(new Point(overlay.ActualWidth,overlay.ActualHeight));
        using var bitmap=new System.Drawing.Bitmap((int)(end.X-start.X),(int)(end.Y-start.Y));
        using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen((int)start.X,(int)start.Y,0,0,bitmap.Size);
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
}
