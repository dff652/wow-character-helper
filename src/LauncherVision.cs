using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

// Launcher controls move between layouts. Locate their pixels before every click.
static class LauncherVision {
 public static Rectangle Find(Bitmap image,string name,Rectangle area,double threshold=0.86,Action check=null){
  var mask=new Mask(image);double best=threshold;Rectangle found=Rectangle.Empty;
  using(var original=new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates",name+".png")))
  foreach(double scale in new[]{1.0,0.8,2.0/3,1.25,1.5,2.0}){
   using(var resized=new Bitmap(Math.Max(1,(int)Math.Round(original.Width*scale)),Math.Max(1,(int)Math.Round(original.Height*scale)),PixelFormat.Format32bppArgb)){
    using(var g=Graphics.FromImage(resized))g.DrawImage(original,new Rectangle(Point.Empty,resized.Size));
    var template=new Mask(resized);var points=new List<Point>();for(int y=0;y<template.H;y++)for(int x=0;x<template.W;x++)if(template.V[y*template.W+x]!=0)points.Add(new Point(x,y));
    if(points.Count<15)continue;var bounds=Rectangle.Intersect(area,new Rectangle(0,0,image.Width,image.Height));
    for(int y=bounds.Top;y<=bounds.Bottom-template.H;y++){if(check!=null&&(y-bounds.Top)%64==0)check();for(int x=bounds.Left;x<=bounds.Right-template.W;x++){
     int count=mask.Count(x,y,template.W,template.H);if(count<points.Count*.75||count>points.Count*1.3)continue;
     int sample=0;for(int i=0;i<16;i++){var p=points[i*(points.Count-1)/15];sample+=mask.V[(y+p.Y)*mask.W+x+p.X];}if(sample<12)continue;
     int hit=0;foreach(var p in points)hit+=mask.V[(y+p.Y)*mask.W+x+p.X];double score=2.0*hit/(count+points.Count);
     if(score>best){best=score;found=new Rectangle(x,y,template.W,template.H);}
    }}
   }
  }
  return found;
 }
 public static Rectangle Icon(Bitmap image,Action check=null){return Find(image,"launcherIcon",new Rectangle(0,0,image.Width/2,Math.Min(400,image.Height/3)),0.9,check);}
 public static Rectangle Play(Bitmap image,Action check=null){
  var area=new Rectangle(0,image.Height/3,image.Width,image.Height-image.Height/3);
  var account=Find(image,"launcherAccount",area,0.86,check);
  bool modern=!account.IsEmpty;
  if(!modern){
   account=Find(image,"launcherAccountOld",area,0.88,check);
   if(account.IsEmpty)throw new Exception("未找到 WoW3 (CN) 账号。请在无限版选择 WoW3 (CN)，再重新运行。");
   var productArea=new Rectangle(Math.Max(0,account.X-20),Math.Max(0,account.Y-account.Height*4),account.Width*4,account.Height*4);
   if(Find(image,"launcherProductOld",productArea,0.88,check).IsEmpty)throw new Exception("当前账号上方未确认无限版，已停止，不会启动其他 WoW 版本。");
  }
  var playArea=new Rectangle(Math.Max(0,account.X-account.Width/2),account.Bottom,account.Width*4,account.Height*6);
  var play=Find(image,modern?"launcherPlay":"launcherPlayOld",playArea,0.88,check);
  if(play.IsEmpty)throw new Exception("无限版入口已选中，但未识别到可用的进入游戏按钮。请处理更新、登录或弹窗。");
  var blue=image.GetPixel(Math.Max(0,play.X-4),play.Y+play.Height/2);
  if(blue.B<140||blue.R>80||blue.G<60)throw new Exception("进入游戏按钮不可用，已停止。");
  return play;
 }
}
