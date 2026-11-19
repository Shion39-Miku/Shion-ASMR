param([string]$Output='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Output){$Output=Join-Path $root '.validation\current'}
$v=Get-Content -LiteralPath (Join-Path $Output 'verification.json') -Raw | ConvertFrom-Json
$samples=Get-Content -LiteralPath (Join-Path $Output 'gpu-samples.json') -Raw | ConvertFrom-Json
$gpu=Get-Content -LiteralPath (Join-Path $Output 'gpu.json') -Raw | ConvertFrom-Json
$cpu=Get-Content -LiteralPath (Join-Path $Output 'cpu.json') -Raw | ConvertFrom-Json
$os=Get-Content -LiteralPath (Join-Path $Output 'os.json') -Raw | ConvertFrom-Json
$rows=[System.Collections.Generic.List[string]]::new()
$summary=[System.Collections.Generic.List[object]]::new()
foreach($p in $v.performance){
    $start=([DateTimeOffset]$p.started).AddSeconds(1)
    $end=[DateTimeOffset]$p.ended
    $valid=@($samples | Where-Object {-not $_.error -and $_.pid -eq $p.pid -and $_.engineCount -gt 0 -and ([DateTimeOffset]$_.time) -ge $start -and ([DateTimeOffset]$_.time) -le $end})
    $avg=if($valid.Count){($valid | Measure-Object maxEngine -Average).Average}else{$null}
    $gpuText=if($null -ne $avg){'{0:F3}%' -f $avg}else{'未取得有效样本'}
    $rows.Add(('| {0} | {1:F1} | {2:F1} | {3:F3}% | {4} | {5} |' -f $p.name,$p.workingSetMiB,$p.privateMiB,$p.cpuPercent,$gpuText,$valid.Count))
    $summary.Add([pscustomobject]@{state=$p.name;workingSetMiB=$p.workingSetMiB;privateMiB=$p.privateMiB;cpuPercent=$p.cpuPercent;gpuBusiestEngineMean=$avg;gpuValidSamples=$valid.Count;started=$p.started;ended=$p.ended;pid=$p.pid})
}
$count=$v.checks.Count
$allChecks=($v.checks | ForEach-Object {'- '+$(if($_.passed){'通过'}else{'失败'})+'：'+$_.name+$(if($_.detail){' — '+$_.detail}else{''})}) -join "`n"
$gpuNames=($gpu | ForEach-Object {$_.Name+' / '+$_.DriverVersion}) -join '; '
$date=([DateTimeOffset]$v.completed).ToString('yyyy-MM-dd HH:mm:ss zzz')
$scroll=Get-Content -LiteralPath (Join-Path $Output 'scroll-performance.json') -Raw | ConvertFrom-Json
$revision=@"
## 大库滚动、分类和字体专项

自行生成 2400 个带独立封面的作品、真实小 WAV 和明确社团元数据。专项运行时作品总数 $($scroll.works)，实际实例化 $($scroll.visibleRows) 行。封面缓存 $($scroll.coverCache) 张；解码线程 $($scroll.coverDecodeThread)，界面线程 $($scroll.uiThread)。系统 SendInput 发送普通滚轮、15 单位精细 delta 和反向滚轮；偏移依次为 $([Math]::Round($scroll.before,1)) / $([Math]::Round($scroll.down,1)) / $([Math]::Round($scroll.precision,1)) / $([Math]::Round($scroll.reverse,1)) DIP。完整输入轨迹见 scroll-input-trace.json。

WPF CompositionTarget.Rendering 回调间隔：中位 $([Math]::Round($scroll.frameMedianMs,2)) ms，P95 $([Math]::Round($scroll.frameP95Ms,2)) ms，最大 $([Math]::Round($scroll.frameMaxMs,2)) ms，共 $($scroll.frameCount) 个间隔。此指标反映 WPF 渲染调度，**不是 GPU 实际呈现帧率或端到端输入延迟**。10 ms 低优先级 Dispatcher 心跳 P95 $([Math]::Round($scroll.uiP95Ms,2)) ms，最大 $([Math]::Round($scroll.uiMaxMs,2)) ms，可能受渲染／输入优先级挤占；未隐藏这部分数据，也未与未实测的旧版帧率作比较。

作品进入／返回检查保留原滚动位置，滚动期间 ItemsSource 没有重建；仅星空运行不增加封面绘制和解码计数。社团批量设置和标签创建／添加通过真实管理窗口控件处理，组合筛选经过主页下拉框事件；重命名、移除、删除和重扫持久化另有检查。另验证明确的“社团/社团名/作品”结构和旧标签扩展迁移。没有使用复制作品来实现标签。

字幕使用真实临时 WAV 和 FLAC，同名 LRC／VTT 包含中文、日文、空格、多句点及大小写扩展名，覆盖单击播放、旧索引、扫描后新增、暂停后继续、快速切歌、字幕缺失和真实新进程恢复；已打开桌面字幕直接读取新文档。字体检查直接读取发布包内 GlyphTypeface 的 cmap，并检查实际歌词 TextBlock 的 GlyphRun.FontUri 指向程序集字体；艺术／清晰两种真实界面渲染分别留存为 lyrics-art.png 和 lyrics-clear.png。字体为未修改的霞鹜文楷 v1.530，授权文件随包保留。

1.4 新增专项通过真实鼠标单击／双击、系统键盘输入检查歌词跳转次数、播放状态、偏移和三处同步，验证指定的方向键映射、输入框避让、音量边界与空格重复；逐帧 PCM 包络检查声道关系及连续性，真实音频／视频检查快速反转、切歌／定位取消、静音、最小化和关闭过渡。层级专项使用真实多级媒体目录及整理窗口控件，验证创建、改名、移动、重扫、重启与跨目录标签。

布局实际渲染 820×680、960×720、1180×800、1600×1000 的主页、详情与歌词页；标题采用系统字体与适当字重，歌词仍使用包内艺术字体。125% / 150% / 200% 仅为 WPF 等效布局缩放测试，记录于 layout-validation.json，不能当作已更改 Windows 物理显示比例。没有不同物理 DPI 显示器切换实测。

没有物理精密触控板硬件实测；精细 delta 通过 Windows 系统输入链路验证，不能冒充所有触控板驱动、手势和惯性行为均已测试。没有 Windows 11、混合 DPI 多屏、数小时稳定性或端到端呈现延迟实测。问题原因及代码位置见 REPAIRS-1.5.md。
"@
$text=@"
# 实际验证与性能报告

完成时间：$date。待测对象是 Windows x64 自包含发布版本 Shion.exe；最终 ZIP 解压后核对全部 DLL／EXE 与受测版本一致，并另做启动检查。完整功能检查 **$count 项，失败 $($v.failures) 项**。所有测试音频、视频、字幕、图片均自行生成，未下载作品。

## 测试环境

- 系统：$($os.Caption)，$($os.Version)，x64；内存 $([Math]::Round($os.TotalVisibleMemorySize/1048576,1)) GiB。
- CPU：$($cpu.Name)，$($cpu.NumberOfLogicalProcessors) 个逻辑处理器。
- 显卡与驱动：$gpuNames。
- 自包含运行时：.NET $($v.environment.runtime)，WPF 渲染层级 $($v.environment.renderTier)。

## 资源占用实测

每种状态采集约 12 秒；内存为每秒采样的平均值。CPU 用进程 TotalProcessorTime 差值除以实测时间和逻辑处理器数，100% 表示整机全部逻辑处理器满载。GPU 使用 Windows GPU Engine 计数器，按 PID 筛选，并仅保留准确状态时间区间内的数据，排除开始后第 1 秒；表中数值为每个样本该进程最忙引擎百分比的平均值，**不等于整块显卡总利用率**。没有用其他进程或系统总 GPU 数据代替播放器数据。

| 状态 | 工作集 MiB | 私有提交 MiB | CPU | GPU 最忙引擎均值 | GPU 样本数 |
|---|---:|---:|---:|---:|---:|
$($rows -join "`n")

空闲时没有播放，使用静态星空；正常播放使用真实立体声 WAV、字幕和静态星空；组合效果状态启用 20 FPS 星空、真实 PCM 波形，并在采样期间每秒触发图片交叉淡入及详情／歌词页切换；最小化状态保持真实音频与独立桌面字幕，星空、波形分析和绘制均停止。每种状态都保留其必要功能，未改变音质。该进程还执行了编码／解码和扫描自测，因此数据包含已加载组件的开销，不是干净启动的最低基线。测试顺序、JIT、缓存和调度会影响短采样，不能从表中断言动画降低了 CPU。

切歌诊断执行了 60 次实际解码器打开／释放，并检查存活托管对象与句柄；该诊断使用 GC 检查可回收对象，性能采样窗口内不强制 GC。图片缓存实际检查为最多 4 项。没有进行数小时／数天稳定性浸泡测试，不能据此宣称完全不存在长期泄漏。

视频组合状态使用真实立体声音频、640×360／24 FPS 的 H.264/AAC 静音配套视频、主窗口字幕和 20 FPS 星空；配套视频独立播放。全屏复用同一原生会话，不重新打开解码器；显式全屏后把输入目标设为全屏窗口，再发送真实 OS Esc 并检查返回。最小化配套视频会暂停并卸下画面；直接视频则保留声音和实际播放时钟，与桌面字幕共用。关闭后用 FileShare.None 独占打开原视频验证句柄释放。

$revision

## 1.5 媒体页面与桌面效果专项

真实单击音轨自动进入歌词页；以自生成WAV、多行VTT、两张不同比例图片及同名H.264/AAC MP4检查无媒体、仅图片、仅视频、图片加视频4种组合。820 / 1180 / 1600 DIP宽分别渲染并检查歌词与媒体不重叠，另测820×680最小窗口。设置窗口的真实控件检查隐藏/显示和同侧分区；图片自然顺序、页码、完整比例、封面独立均有检查。配套视频默认静音，隐藏暂停无用解码，直接视频声音管理与旧版一致。

当前多行歌词直接检查实际圆角Border高度与TextBlock高度、Tag活动状态及Bold字重。波形检查实际布局宽高、轻声视觉映射和原音量不变；静音没有随机动画。星空检查真实相位、位移及不同星点亮度差。透明桌面字幕使用WPF像素alpha与实际桌面合成角落像素前后比较，并检查原生穿透、不激活及包内艺术字体。三栏图片/视频/字幕/大波形/20FPS星空的12秒测量另列；最小化检查星空帧数、波形帧数冻结且主音频继续。

社团固定入口在1.6恢复为系列文件夹与直属作品，标签收纳；全部作品为跨社团快捷入口。返回保持筛选及列表数据源。旧手动文件夹关系仍由原模型保存。此前清理记录见 CLEANUP-1.5.md。

## Shion 1.6 专项

恢复实际系列文件夹导航：原生点击社团、系列、子系列与返回，检查面包屑、直属作品、手动系列重扫和1.5数据迁移。网格按实际列表视口计算1—6列，820/1180/1600/2100 DIP宽检查常见桌面3—6列；长标题两行及完整提示。横图、竖图、方图使用四角颜色标记，经真实CoverView渲染检查四角均保留。

原生系统鼠标点击与按住拖动检查音频、直接视频及独立静音配套视频的实际播放时钟，不只读取Slider.Value。100%/125%/150% WPF等效缩放下，在轨道非可见细线处点击、拖动未松手及反向拖动，检查时间、暂停状态、字幕和桌面字幕。等效缩放不代表更改Windows物理显示设置；输入前确认实际原生窗口可见且指针目标属于测试窗口。轨迹见pointer-seek-trace.json和pointer-input-trace.json。

快捷键按本次要求验证：左键后退3秒，右键前进5秒；保留输入框避让、音量边界、空格重复和失焦不拦截。Shion窗口、托盘、关于和EXE版本产品信息一致；程序集及原数据目录仍为Nocturne，保留旧记录和包内字体URI。

纤细波形默认32DIP、72根1.6DIP线，最大20FPS；真实自生成音频包含底噪、轻声、较强三段，检查实际绘制幅度随PCM变化且用户音量不变。原1.5“大波形”与平铺系列的检查已按本次需求替换，其历史文字不代表本次默认设计。

## 功能检查原始结果

$allChecks

## 验证边界

音频验证打开了本机真实 Windows 输出设备并检查设备位置前进、暂停冻结、定位和播放结束事件；未做人工听感评审或系统输出环回录音。立体声检查验证解码器输出的左右通道不同，播放链路不包含混成单声道或音效处理。

桌面字幕默认 WS_EX_TRANSPARENT、WS_EX_NOACTIVATE，无可误触边框；托盘菜单实际触发编辑并验证解除穿透。另启动独立进程的自制 Pong 游戏，分别测试窗口化与无边框全屏：先激活游戏，再显示覆盖字幕，发送真实 OS 鼠标移动、点击、拖动、滚轮与键盘输入，检查游戏接收事件增量，并在字幕更新、显示及切歌前后检查 GetForegroundWindow 保持为游戏。没有安装全局钩子。该结果不代表所有商业游戏／反作弊环境，独占全屏不保证显示，未通过抢焦点或注入游戏强行覆盖。窗口在屏幕外的位置恢复已验证；真实双屏热插拔、不同显示器混合 DPI、Windows 11、Windows N/KN 及所有音频设备组合尚未实测。

大音频使用 30 分钟、345600044 字节 PCM WAV，实际定位到 900 秒继续播放。压力库包含约 1200 个多级目录；扫描线程与 UI 线程不同，扫描期间音频持续播放。它不等于对百万文件库的基准测试。

视频实际检查 MP4/H.264 8 bit yuv420p + AAC 立体声，以及 WMV2 + WMA2 的 WMV。M4V、MOV、AVI、MKV 入口依赖系统解码能力，HEVC／AV1／10 bit／DRM 未验证。WPF 使用系统解码与渲染链路，未独立证明 DXVA 硬解启用；GPU 计数器读数不能单独证明硬件视频解码。未进行人工视频听感／画质评审或环回录音。

原始 JSON、GPU 逐样本数据和界面渲染在 validation 子目录。截图仅用于界面 QA；交付包含实际 EXE、源代码与构建脚本。
"@
$docs=Join-Path $root 'docs';New-Item -ItemType Directory -Force $docs | Out-Null
$text | Set-Content -LiteralPath (Join-Path $docs 'VALIDATION.md') -Encoding utf8
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $docs 'performance-summary.json') -Encoding utf8
$raw=Join-Path $docs 'validation';New-Item -ItemType Directory -Force $raw | Out-Null
foreach($name in @('verification.json','performance.json','gpu-samples.json','os.json','cpu.json','gpu.json','main-window.png','desktop-subtitles.png','home.png','lyrics.png','startup-probe.json','restart-home.png','video-layout.png','scroll-performance.json','scroll-input-trace.json','lyrics-art.png','lyrics-clear.png','home-circles-tags.png','fade-trace.json','layout-validation.json','lyric-click-trace.json','native-click-points.json','keyboard-trace.json','media-layout-validation.json','desktop-transparent-pink.png')){Copy-Item -LiteralPath (Join-Path $Output $name) -Destination $raw -Force}
Get-ChildItem -LiteralPath $Output -Filter "layout-*.png" -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $raw -Force}
Get-ChildItem -LiteralPath $Output -Filter "v15-*.png" -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $raw -Force}
foreach($name in @('shion-layout-validation.json','pointer-seek-trace.json','pointer-input-trace.json')){Copy-Item -LiteralPath (Join-Path $Output $name) -Destination $raw -Force}
Get-ChildItem -LiteralPath $Output -Filter "v16-*.png" -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $raw -Force}
Write-Output "Report: $docs\VALIDATION.md"
$summary | Format-Table state,workingSetMiB,cpuPercent,gpuBusiestEngineMean,gpuValidSamples
