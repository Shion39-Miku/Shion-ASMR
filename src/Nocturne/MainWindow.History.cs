using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace Nocturne;
public partial class MainWindow
{
 sealed record BrowseState(string Page,string Location,bool Global,bool Folders,bool Tree,string Mode,string? Circle,string? Tag,string Search,string CircleSearch,string? Work,string Group,double Scroll,string[] Expanded);
 readonly List<BrowseState> browseHistory=new();BrowseState? lyricsOrigin;bool restoringBrowser;
 BrowseState CaptureBrowse()=>new(CurrentPage,browserFolder,globalBrowse,folderBrowsing,showFolderTree,viewMode,circleFilter,tagFilter,Search.Text,CircleSearch.Text,currentWork?.Path,folder,BrowserScroll(WorksList)?.VerticalOffset??0,settings.ExpandedFolders.ToArray());
 void RememberBrowse(){if(restoringBrowser||CurrentPage!="home")return;browseHistory.Add(CaptureBrowse());if(browseHistory.Count>64)browseHistory.RemoveAt(0);}
 async void BrowseBack(object sender,RoutedEventArgs e)=>await ReturnBrowse();
 internal async Task ReturnBrowse()
 {
  BrowseState? state=null;if(CurrentPage=="lyrics"){state=lyricsOrigin;lyricsOrigin=null;}else if(browseHistory.Count>0){state=browseHistory[^1];browseHistory.RemoveAt(browseHistory.Count-1);}
  if(state==null){if(CurrentPage=="detail"){await EnterFolder(browserFolder);return;}if(CurrentPage=="home"){await EnterFolder(organization.Folders.TryGetValue(browserFolder,out var parent)?parent.Parent:"");return;}Navigate("home");return;}
  restoringBrowser=true;
  try
  {
   ++browseSelectionRequest;++homeRequest;searchTimer.Stop();filterLoading=true;browserFolder=state.Location;globalBrowse=state.Global;folderBrowsing=state.Folders;showFolderTree=state.Tree;viewMode=state.Mode;circleFilter=state.Circle;tagFilter=state.Tag;Search.Text=state.Search;CircleSearch.Text=state.CircleSearch;settings.ExpandedFolders=state.Expanded.ToList();filterLoading=false;searchTimer.Stop();RefreshFilterChoices();UpdateBrowserMode();
   if(state.Page=="detail"&&state.Work!=null&&works.FirstOrDefault(x=>x.Path==state.Work) is Work work){currentWork=work;folder=state.Group;PopulateGroups();PopulateTracks();UpdatePictures();Navigate("detail");}
   else{await RefreshOrganization();await RenderHome();Navigate(state.Page=="landing"?"landing":"home");await Dispatcher.InvokeAsync(()=>{SmoothScroll.Stop(WorksList);BrowserScroll(WorksList)?.ScrollToVerticalOffset(state.Scroll);},DispatcherPriority.ContextIdle);}
  }
  finally{restoringBrowser=false;}
 }
}
