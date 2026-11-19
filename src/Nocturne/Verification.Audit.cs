using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Nocturne;

public static partial class Verification
{
    static async Task VerifyAudit(MainWindow w)
    {
        w.AuditForeground = true;
        w.Width=1180;
        w.Height=800;
        w.Left=30;
        w.Top=25;
        w.settings.ReduceMotion=false;
        w.settings.Stars=2;
        w.settings.Visualizer=true;
        w.Volume.Value=.025;
        await Delay(420);
        var root=Path.Combine(State.Home,"演示媒体 · 案内");
        var media=Path.Combine(root,"月夜来信","CD1");
        Directory.CreateDirectory(media);
        var audio=Path.Combine(media,"01 星明かり.wav");
        var movie=Path.Combine(root,"演示短片.mp4");
        WriteWave(audio,35);
        WriteWave(Path.Combine(media,"02 风声.wav"),8);
        File.Copy(Path.Combine(Environment.CurrentDirectory,"test-fixtures","generated.mp4"),movie);
        File.WriteAllText(Path.ChangeExtension(audio,".vtt"),"WEBVTT\n\n00:01.000 --> 00:03.000\n听风经过窗边。\n\n00:05.000 --> 00:07.500\n夜色静了，星光落在手边。\n\n"+string.Join("\n\n",Enumerable.Range(8,25).Select(i=>$"00:{i:00}.000 --> 00:{i:00}.850\n第 {i} 句 · 星月相伴。")));
        File.WriteAllText(Path.ChangeExtension(movie,".vtt"),"WEBVTT\n\n00:00.000 --> 00:22.000\n演示视频字幕\n");
        var pics=Path.Combine(root,"月夜来信","插图","夜空");
        Directory.CreateDirectory(pics);
        MakeDemoCover(Path.Combine(pics,"封面.png"));
        MakeDemoCover(Path.Combine(pics,"星光.png"));
        const string circle="audit-circle";
        w.settings.Organization[circle]=new(){Id=circle,Name="演示社团"};
        var night=Path.Combine(root,"月夜来信");
        w.settings.WorkAssignments[media]=night;
        w.settings.WorkLocations[night]=circle;
        MetadataStore.AddTag(w.settings,new[]{night},"夜读");
        for(int i=0;i<36;i++)
        {
            var id="audit-series-"+i;
            w.settings.Organization[id]=new(){Id=id,Parent=circle,Name=$"系列 {i:00}"};
            for(int j=0;j<32;j++)
            {
                var directory=Path.Combine(root,$"作品 {i:00}-{j:00} 日本語");
                Directory.CreateDirectory(directory);
                var file=Path.Combine(directory,"短音轨.wav");
                WriteLargeWave(file,1);
                w.settings.WorkAssignments[directory]=directory;
                w.settings.WorkLocations[directory]=id;
            }
        }

        var clock=Stopwatch.StartNew();
        await w.ImportFolders(new[]{root});
        var firstScan=clock.Elapsed.TotalMilliseconds;
        var firstReads=w.library.FilesEnumerated;
        Check("导入演示文件夹并生成大型目录",w.works.Count>=1153&&firstReads>=1156);
        clock.Restart();
        await w.Scan(false);
        var nextScan=clock.Elapsed.TotalMilliseconds;
        Check("增量扫描复用文件索引",w.library.FilesEnumerated==0);
        var indexStamp=File.GetLastWriteTimeUtc(Path.Combine(State.Home,"index.json"));
        var rebuilds=w.HomeRebuilds;
        await w.Scan(false);
        Check("索引无变化时不写磁盘也不重建分类",indexStamp==File.GetLastWriteTimeUtc(Path.Combine(State.Home,"index.json"))&&rebuilds==w.HomeRebuilds);
        File.AppendAllText(Path.ChangeExtension(audio,".vtt"),"\n");
        await w.SetBrowseMode(false);

        await w.EnterFolder(circle);
        await Delay(420);
        var filterTimes=new List<double>();
        async Task Browse()
        {
            for(int i=0;i<14;i++)
            {
                var started=Stopwatch.GetTimestamp();
                await w.EnterFolder(i%2==0?"audit-series-"+(i/2):circle);
                filterTimes.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                var scroll=FindScroll(w.WorksList);
                scroll?.ScrollToVerticalOffset(i%2==0?180:0);
                await Delay(320);
            }
        }
        await MeasureFrames11(w,"分类切换与列表滚动",5200,Browse);
        await w.FilterForTest(null,"夜读","月夜");
        Check("跨目录筛选标签及中日文标题",Tiles(w).Count(t=>t.Work!=null)==1);
        var work=w.works.Single(x=>x.Path==night);
        w.SelectWork(work);
        await w.ReturnBrowse();
        Check("从作品返回保留标签与搜索",w.CurrentPage=="home"&&w.Search.Text=="月夜"&&w.CurrentFilters.Text.Contains("夜读"));
        w.Search.Text="";
        Check("WAV播放和自动字幕",w.OpenTrack(audio,true,8.2));
        await w.SubtitleReady;
        await w.ImageReady;
        await Delay(450);
        Check("父子目录图集与页码",w.PicturePaths.Count==2&&w.LyricsPicture.Source!=null);
        w.BrowsePicture(1);
        await w.ImageReady;
        Check("图片翻页",w.PictureIndex==1&&w.LyricsPicture.Source!=null);
        var selectedCover=w.PicturePaths[1];
        w.ChooseCover(selectedCover);
        await w.HomeReady;
        await w.Scan(true);
        await w.ImageReady;
        Check("刷新后保留手动封面和当前图片",w.works.Single(x=>x.Path==night).Cover==selectedCover&&w.PicturePaths[w.PictureIndex]==selectedCover);
        Snapshot(w,Path.Combine(State.Home,"audio-lyrics.png"));
        await MeasureFrames11(w,"音频、字幕、星空、樱花和波形",6200);
        w.PausePlayback();
        w.Seek(4.1);
        await Delay(150);
        Check("音频跳入字幕空档，保留并居中",w.CurrentSubtitle.Contains("听风")&&CenterError(w)<5);
        w.SetLyricsFollow(false);
        w.Seek(6);
        await Delay(150);
        Check("定位刷新字幕并居中",w.CurrentSubtitle.Contains("夜色")&&CenterError(w)<5);
        var watch=Stopwatch.StartNew();
        w.RequestPlayback(true);
        await w.FadeReady;
        var fadeIn=watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        w.RequestPlayback(false);
        await w.FadeReady;
        var fadeOut=watch.Elapsed.TotalMilliseconds;
        Check("音频淡入淡出后状态与用户音量正确",fadeIn is >=340 and <=900&&fadeOut is >=450 and <=950&&!w.PlaybackPlaying&&Math.Abs(w.Volume.Value-.025)<.001);
        w.RequestPlayback(true);
        await w.FadeReady;
        var position=w.PlaybackPosition;
        var damaged=Path.Combine(media,"损坏音轨.wav");
        File.WriteAllText(damaged,"incomplete file");
        bool failed=!w.OpenTrack(damaged,true);
        await Delay(150);
        Check("损坏音轨不丢失当前播放会话",failed&&w.currentTrack==audio&&w.PlaybackPlaying&&w.PlaybackPosition>position+.08&&w.CurrentSubtitle.Contains("夜色"));
        Check("文件缺失提示且原会话可继续",!w.OpenTrack(Path.Combine(media,"不存在.wav"),true)&&w.currentTrack==audio);

        var offline=Path.Combine(State.Home,"独立演示目录");
        Directory.CreateDirectory(offline);
        var previous=Path.Combine(offline,"测试音轨.wav");
        WriteWave(previous,3);
        await w.ImportFolders(new[]{offline});
        w.OpenTrack(previous,false);
        var temporarilyDisconnected=offline+"-离线";
        w.CloseVideoSession();
        w.OpenTrack(audio,false,position);
        Directory.Move(offline,temporarilyDisconnected);
        await w.Scan(false);
        Check("暂时不可用的媒体目录保留已索引分类",w.library.Folders.ContainsKey(offline)&&w.settings.Roots.Contains(offline));
        Directory.Move(temporarilyDisconnected,offline);
        w.OpenTrack(previous,false);
        await w.RemoveMediaRoot(offline);
        Check("移除目录同时移除旧索引与继续收听入口",
            !w.library.Folders.ContainsKey(offline)&&w.ContinueTrack()!=previous&&
            !State.Read<Dictionary<string,Folder>>("index.json").ContainsKey(offline)&&File.Exists(previous)&&w.settings.Tracks.ContainsKey(previous));

        using var proc=Process.GetCurrentProcess();
        var retention=new List<(long Private,long Managed,int Handles)>();
        for(int pass=0;pass<4;pass++)
        {
            await Delay(450);
            var managed=GC.GetTotalMemory(true);
            proc.Refresh();
            retention.Add((proc.PrivateMemorySize64,managed,proc.HandleCount));
            if(pass==3)break;
            for(int i=0;i<24;i++)
            {
                w.OpenTrack(i%2==0?audio:Path.Combine(media,"02 风声.wav"),false);
                await w.SubtitleReady;
                await w.ImageReady;
                w.Navigate(i%2==0?"home":"lyrics");
            }
        }
        var managedDelta=retention[^1].Managed-retention[1].Managed;
        var handleDelta=retention[^1].Handles-retention[1].Handles;
        Check("72次切轨后图像缓存、托管对象和句柄有界",w.imageCache.Count<=4&&CoverView.Cache.Count<=24&&managedDelta<12*1048576&&handleDelta<24,$"warm managed delta={managedDelta}; handles={handleDelta}");

        w.settings.Loop=3;
        w.OpenTrack(audio,true,34.6);
        var next=Path.Combine(media,"02 风声.wav");
        for(int i=0;i<40&&w.currentTrack!=next;i++)await Delay(40);
        Check("文件夹暂停模式续播同文件夹下一首",w.currentTrack==next&&w.PlaybackPlaying);
        w.Seek(7.65);
        for(int i=0;i<40&&w.PlaybackPlaying;i++)await Delay(40);
        Check("文件夹暂停模式在末尾保留进度",w.currentTrack==next&&!w.PlaybackPlaying&&w.PlaybackPosition>7.85);
        w.settings.Loop=0;

        w.OpenTrack(audio,true,5.5);
        await w.SubtitleReady;
        w.overlay.ShowPassive();
        w.Hide();
        var beforeHide=w.PlaybackPosition;
        await Delay(580);
        Check("隐藏主窗暂停绘制，后台继续播放及字幕",!w.Stars.Running&&!w.Wave.Running&&w.PlaybackPosition>beforeHide+.2&&w.overlay.DisplayedText.Contains("夜色"));
        w.overlay.Hide();
        w.Show();
        w.PausePlayback();
        Check("视频可打开并显示匹配字幕",await w.OpenVideo(movie,true,false,2)&&w.VideoCaptionText.Contains("演示视频"));
        w.EnterVideoFullscreen();
        await Delay(250);
        Check("全屏显示字幕和进度控件",w.FullscreenCaptionText.Contains("演示视频")&&w.FullscreenProgress?.ActualWidth>200);
        w.Seek(9);
        await Delay(120);
        Check("视频定位保持暂停状态",Math.Abs(w.PlaybackPosition-9)<.25&&!w.PlaybackPlaying);
        w.ExitVideoFullscreen();
        w.CloseVideoSession();
        w.OpenTrack(audio,false,18.4);
        await w.SubtitleReady;
        w.SetOffset(.2);
        w.Save();

        State.Write("audit-summary.json",new
        {
            works=w.works.Count,firstReads,firstScanMs=firstScan,incrementalScanMs=nextScan,
            filterMs=filterTimes,fadeInMs=fadeIn,fadeOutMs=fadeOut,
            retention=retention.Select(x=>new{privateMiB=x.Private/1048576d,managedMiB=x.Managed/1048576d,handles=x.Handles})
        });
    }
}
