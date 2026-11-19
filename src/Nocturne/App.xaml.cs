using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Nocturne;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Contains("--self-test")&&e.Args.Contains("--software-render"))System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if(e.Args.Contains("--game-probe"))
        {
            var output=e.Args.SkipWhile(x=>x!="--game-probe").Skip(1).First();Directory.CreateDirectory(output);ShutdownMode=ShutdownMode.OnExplicitShutdown;
            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException+=(_,a)=>{File.AppendAllText(Path.Combine(output,"game-error.log"),a.Exception+"\n");Shutdown(1);};
            DispatcherUnhandledException+=(_,a)=>{File.AppendAllText(Path.Combine(output,"game-error.log"),a.Exception+"\n");a.Handled=true;Shutdown(1);};
            new GameProbe(output,e.Args.Contains("--borderless")).Show();return;
        }
        DispatcherUnhandledException += (_, a) => { File.AppendAllText(Path.Combine(State.Home,"errors.log"), DateTime.Now+" "+a.Exception+"\n"); MessageBox.Show("操作未完成："+a.Exception.Message,Brand.Name,MessageBoxButton.OK,MessageBoxImage.Warning); a.Handled=true; };
        var selfTest=e.Args.Contains("--self-test");var startupProbe=e.Args.Contains("--startup-probe");var test=selfTest||startupProbe;
        if(test) State.Home=Path.GetFullPath(e.Args.SkipWhile(x=>x!=(selfTest?"--self-test":"--startup-probe")).Skip(1).FirstOrDefault() ?? "test-output");
        if(test)ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(State.Home);
        StartupWindow? splash=null;if(!test||e.Args.Contains("--with-splash")){splash=new StartupWindow();MainWindow=splash;splash.Show();await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);await System.Threading.Tasks.Task.Run(()=>{State.BootSettings=State.Read<Settings>("settings.json");State.BootFolders=State.Read<System.Collections.Generic.Dictionary<string,Folder>>("index.json");});splash.SetReducedMotion(State.BootSettings?.ReduceMotion==true,State.BootSettings?.Sakura??true,State.BootSettings?.SakuraStrength??.65);splash.SetStatus("正在整理分类与封面…");}
        var window = new MainWindow(test,startupProbe);State.BootFolders=null; MainWindow=window;
        if(!test)
        {
            ShutdownMode=ShutdownMode.OnMainWindowClose;
            SessionEnding+=(_,_)=>window.PrepareForSessionEnd();
        }
        if(selfTest) window.Loaded += async (_,_) => await Verification.Run(window);
        if(startupProbe)window.Loaded+=async(_,_)=>await Verification.ProbeStartup(window,e.Args.SkipWhile(x=>x!="--resume-probe").Skip(1).FirstOrDefault());
        if(splash!=null){var startup=splash;window.Opacity=0;Action? complete=null;complete=()=>{window.StartupCompleted-=complete;window.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(1,TimeSpan.FromMilliseconds(400)));if(startupProbe)State.Write("startup-stages.json",new{indexLoaded=true,categoryReady=true,page=window.CurrentPage});startup.Close();startup.Content=null;};window.StartupCompleted+=complete;}
        window.Show();
    }
}
