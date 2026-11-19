using System;
using NAudio.Wave;
using System.Threading.Tasks;

namespace Nocturne;
// A bounded, sample-accurate envelope. Every channel in a frame receives the same gain.
public sealed class FadeProvider : ISampleProvider
{
 readonly ISampleProvider source;readonly object gate=new();float current=1,target=1,step;int remaining;
 TaskCompletionSource? completion;
 public Task Completion{get{lock(gate)return completion?.Task??Task.CompletedTask;}}
 public FadeProvider(ISampleProvider source)=>this.source=source;
 public WaveFormat WaveFormat=>source.WaveFormat;
 public float Gain {get{lock(gate)return current;}}
 public void Set(float value,int milliseconds=0){lock(gate){completion?.TrySetCanceled();completion=new(TaskCreationOptions.RunContinuationsAsynchronously);target=Math.Clamp(value,0,1);remaining=WaveFormat.SampleRate*milliseconds/1000;if(remaining==0){current=target;completion.TrySetResult();}else step=(target-current)/remaining;}}
 public int Read(float[] buffer,int offset,int count)
 {
  int read=source.Read(buffer,offset,count);lock(gate){for(int i=offset;i<offset+read;i+=WaveFormat.Channels){if(remaining>0){current+=step;if(--remaining==0){current=target;completion?.TrySetResult();}}for(int c=0;c<WaveFormat.Channels&&i+c<offset+read;c++)buffer[i+c]*=current;}}return read;
 }
}
