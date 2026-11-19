# Third-party dependencies

Pinned direct packages: NAudio 2.2.1 (MIT), NAudio.Vorbis 1.5.0 (MIT), SkiaSharp 3.119.4 (MIT; native Skia includes additional notices). NVorbis and native Skia assets are transitive dependencies. Microsoft .NET 10 Windows Desktop runtime is redistributed in the self-contained portable build under its distribution license. The portable `licenses` directory contains license/notice files collected from restored packages and runtime output. NuGet assets files in a local build contain exact resolved versions.

Upstream sources and package licensing:

- https://github.com/naudio/NAudio
- https://github.com/naudio/Vorbis
- https://github.com/NVorbis/NVorbis
- https://github.com/mono/SkiaSharp
- https://github.com/dotnet/runtime
- https://github.com/dotnet/wpf

Python, NumPy and soundfile are optional **test-only** tools and are not shipped or required to run Nocturne. Their generated audio is synthetic and contains no downloaded work.

Video playback uses Windows WPF MediaPlayer / VideoDrawing; no extra video codec binaries are redistributed. Optional ffmpeg / imageio-ffmpeg is used only to generate synthetic testsrc2 frames and sine audio fixtures, and is not included in or required by the player. Source fixtures contain only generated media, not the ffmpeg executable. No third-party website content is bundled.

## NAudio.Vorbis 1.5.0

Copyright © Andrew Ward 2021

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Bundled lyric font

LXGW WenKai Regular v1.530, unmodified. Source: https://github.com/lxgw/LxgwWenKai/releases/tag/v1.530 . Font binary is embedded as a WPF Resource. Copyright notices and the complete SIL Open Font License 1.1 are retained in `Assets/Fonts/OFL.txt` in both source and portable packages. It is loaded privately by URI, never installed into Windows. Chinese, Japanese and kana coverage is checked using the bundled GlyphTypeface, with system fallbacks for missing characters.
