using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Nocturne;
// One native streaming decoder. DrawingImage holds no decoded frame archive.
public sealed class VideoSession : IDisposable
{
 MediaPlayer? player;TaskCompletionSource<bool>? opening;
 public string? Path {get;private set;} public bool Playing {get;private set;}
 public bool Muted {get;private set;} public DrawingImage? Image {get;private set;}
 public double Position=>player?.Position.TotalSeconds??0;
 public double Duration=>player?.NaturalDuration.HasTimeSpan==true?player.NaturalDuration.TimeSpan.TotalSeconds:0;
 public event Action<Exception?>? Ended;
 public int StartRecoveries {get;private set;}
 public async Task Open(string path,bool muted,double volume)
 {
  Close();var native=new MediaPlayer();player=native;Path=path;Muted=muted;
  var completion=opening=new(TaskCreationOptions.RunContinuationsAsynchronously);
  native.MediaOpened+=(_,_)=>{if(player!=native)return;Image=new DrawingImage(new VideoDrawing{Player=native,Rect=new Rect(0,0,Math.Max(1,native.NaturalVideoWidth),Math.Max(1,native.NaturalVideoHeight))});completion.TrySetResult(true);};
  native.MediaFailed+=(_,e)=>{if(player!=native)return;Playing=false;if(!completion.TrySetException(e.ErrorException))Ended?.Invoke(e.ErrorException);};
  native.MediaEnded+=(_,_)=>{if(player==native){Playing=false;Ended?.Invoke(null);}};
  native.IsMuted=muted;native.Volume=volume;native.ScrubbingEnabled=true;
  try{native.Open(new Uri(System.IO.Path.GetFullPath(path)));await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));if(player!=native)throw new OperationCanceledException();}
  catch{if(player==native)Close();throw;}
 }
 public void Play(bool restartAtEnd=true){if(player==null)return;if(restartAtEnd&&Duration>0&&Position>=Duration-.02)Seek(0);player.Play();Playing=true;}
 public void Pause(){player?.Pause();Playing=false;}
 public async Task<bool> ConfirmStarted()
 {
  var native=player;if(native==null||!Playing)return true;var start=Position;
  async Task<bool> Advanced(int attempts)
  {
   for(int i=0;i<attempts;i++){if(player!=native||!Playing)return true;if(Math.Abs(Position-start)>.04)return true;await Task.Delay(30);}return false;
  }
  if(await Advanced(20))return true;
  // Some Media Foundation graphs remain at the preroll frame after a visual
  // reattachment. Flush the state transition once the drawing target is live.
  // Keep the public playback intent; a user pause or close cancels recovery.
  StartRecoveries++;native.Pause();await Task.Delay(90);if(player!=native||!Playing)return true;native.Play();
  if(await Advanced(20))return true;
  native.Stop();native.Position=TimeSpan.FromSeconds(start);await Task.Delay(90);if(player!=native||!Playing)return true;native.Play();
  return await Advanced(25);
 }
 public void Seek(double seconds){if(player!=null)player.Position=TimeSpan.FromSeconds(Math.Clamp(seconds,0,Math.Max(0,Duration-.01)));}
 public void Volume(double value){if(player!=null)player.Volume=value;}
 public void Close(){opening?.TrySetCanceled();opening=null;var old=player;player=null;Playing=false;Path=null;Image=null;old?.Close();}
 public void Dispose()=>Close();
}
