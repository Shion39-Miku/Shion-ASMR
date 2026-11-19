using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using System.Threading.Tasks;
using System.Threading;

namespace Nocturne;
public sealed class ImageCache
{
 sealed record Entry(BitmapSource Image,long Stamp,DateTime Checked);
 readonly Dictionary<string,Entry> cache=new(StringComparer.OrdinalIgnoreCase); readonly LinkedList<string> order=new();
 readonly object gate=new();readonly int capacity,edge;
 readonly SemaphoreSlim decodeGate=new(1,1);
 public ImageCache(int capacity=4,int edge=1600){this.capacity=capacity;this.edge=edge;}
 public long Decodes {get;private set;} public int DecodeThread {get;private set;}
 public bool TryGet(string path,out BitmapSource? image){lock(gate){if(cache.TryGetValue(path,out var entry)&&DateTime.UtcNow-entry.Checked<TimeSpan.FromSeconds(3)){order.Remove(path);order.AddLast(path);image=entry.Image;return true;}}image=null;return false;}
 public async Task<BitmapSource> LoadAsync(string path,CancellationToken token=default){if(TryGet(path,out var hit))return hit!;await decodeGate.WaitAsync(token);try{token.ThrowIfCancellationRequested();return await Task.Run(()=>Load(path),token);}finally{decodeGate.Release();}}
 public int Count{get{lock(gate)return cache.Count;}}
 public BitmapSource Load(string path)
 {
  return Decode(path);
 }
 BitmapSource Decode(string path)
 {
  var stamp=File.GetLastWriteTimeUtc(path).Ticks;
  lock(gate){if(cache.TryGetValue(path,out var old)&&old.Stamp==stamp){order.Remove(path);order.AddLast(path);cache[path]=old with{Checked=DateTime.UtcNow};return old.Image;}}
  // Never hold the cache mutex while reading or decoding. UI cache hits must not wait for a raster decode.
  Decodes++;DecodeThread=Environment.CurrentManagedThreadId;
  using var codec=SKCodec.Create(path)??throw new IOException("无法读取此图片。");
  var info=codec.Info;double scale=Math.Min(1,edge/(double)Math.Max(info.Width,info.Height));
  var size=codec.GetScaledDimensions((float)scale);
  // WebP/JPEG decode directly at a reduced size. Reject formats that require enormous full-size intermediate allocations.
  if((long)size.Width*size.Height>24_000_000)throw new IOException("该图片过大，无法安全生成预览。");
  using var decoded=new SKBitmap(new SKImageInfo(size.Width,size.Height,SKColorType.Bgra8888,SKAlphaType.Premul));
  var status=codec.GetPixels(decoded.Info,decoded.GetPixels());if(status!=SKCodecResult.Success&&status!=SKCodecResult.IncompleteInput)throw new IOException("图片解码失败："+status);
  SKBitmap? resized=null;var source=decoded;
  if(Math.Max(size.Width,size.Height)>edge){resized=decoded.Resize(new SKImageInfo(Math.Max(1,(int)(info.Width*scale)),Math.Max(1,(int)(info.Height*scale)),SKColorType.Bgra8888,SKAlphaType.Premul),new SKSamplingOptions(SKFilterMode.Linear));source=resized??decoded;}
  var bmp=BitmapSource.Create(source.Width,source.Height,96,96,PixelFormats.Pbgra32,null,source.GetPixels(),source.ByteCount,source.RowBytes);bmp.Freeze();resized?.Dispose();
  lock(gate){cache[path]=new(bmp,stamp,DateTime.UtcNow);order.Remove(path);order.AddLast(path);while(order.Count>capacity){cache.Remove(order.First!.Value);order.RemoveFirst();}}return bmp;
 }
}
