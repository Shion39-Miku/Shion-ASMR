using System;
using System.Windows.Media;

namespace Nocturne;
public static class FontBook
{
 public const string Resource="./Assets/Fonts/#LXGW WenKai";
 static readonly Uri root=new("pack://application:,,,/Nocturne;component/");
 public static readonly FontFamily Art=new(root,Resource);
 public static readonly FontFamily ArtWithFallback=new(root,Resource+", Microsoft YaHei, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI Emoji, Segoe UI");
 public static FontFamily For(Settings s)=>s.ArtLyrics?ArtWithFallback:new FontFamily(s.LyricFontFamily+", Microsoft YaHei UI, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI");
}
