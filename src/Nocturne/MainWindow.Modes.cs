using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using Microsoft.Win32;
namespace Nocturne;
public partial class MainWindow
{
 internal bool videoBrowse;string queueFolder="";List<string> playbackQueue=new();
 static bool ModeFile(string file,bool video)=>(video?Library.VideoTypes:Library.AudioTypes).Contains(Path.GetExtension(file));
 bool ModeFile(string file)=>ModeFile(file,videoBrowse);
 static bool ModeWork(Work work,bool video)=>work.Groups.Any(g=>g.Audio.Any(file=>ModeFile(file,video)));
 bool ModeWork(Work work)=>ModeWork(work,videoBrowse);
 internal async Task SetBrowseMode(bool video)
 {
  var request=++browseSelectionRequest;++homeRequest;
  videoBrowse=video;browserFolder="";circleFilter=tagFilter=null;globalBrowse=true;folderBrowsing=false;viewMode="works";lastHomeSignature=null;
  Search.Text="";searchTimer.Stop();UpdateBrowserMode();RefreshFilterChoices();
  await RefreshOrganization();
  if(request!=browseSelectionRequest||closing)return;
  await RenderHome();
  if(request==browseSelectionRequest&&!closing)Navigate("home");
 }
 async void BrowseAudioMode(object sender,RoutedEventArgs e)=>await SetBrowseMode(false);
 async void BrowseVideoMode(object sender,RoutedEventArgs e)=>await SetBrowseMode(true);
 void ImportChoice(object sender,RoutedEventArgs e){var dialog=DialogShell.Create("添加音频",420,240);dialog.Owner=this;var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=panel;var files=new Button{Content="选择本地音频文件"};files.Click+=async(_,_)=>{dialog.Close();var picker=new OpenFileDialog{Multiselect=true,Filter="本地音频|*.wav;*.mp3;*.flac;*.m4a;*.ogg"};if(picker.ShowDialog(this)!=true)return;foreach(var dir in picker.FileNames.Select(Path.GetDirectoryName).OfType<string>().Distinct())if(!settings.Roots.Any(r=>WorkCatalog.Within(dir,r))){settings.Roots.RemoveAll(r=>WorkCatalog.Within(r,dir));settings.Roots.Add(dir);}await Scan(false);library.Watch(settings.Roots);Save();ImportResult.Text=$"已加入 {picker.FileNames.Length} 个所选文件的所在目录；媒体库共 {works.Count} 部作品。";};var folderButton=new Button{Content="选择媒体文件夹"};folderButton.Click+=(_,_)=>{dialog.Close();AddRoot(this,new());};panel.Children.Add(files);panel.Children.Add(folderButton);dialog.ShowDialog();}
 void CaptureQueue(string track){queueFolder=Path.GetDirectoryName(track)!;bool movie=Library.VideoTypes.Contains(Path.GetExtension(track));playbackQueue=library.Folders.TryGetValue(queueFolder,out var group)?group.Audio.Where(p=>movie?Library.VideoTypes.Contains(Path.GetExtension(p)):Library.AudioTypes.Contains(Path.GetExtension(p))).ToList():new(){track};if(!playbackQueue.Contains(track))playbackQueue.Add(track);playbackQueue.Sort(Natural.Instance);}
}
