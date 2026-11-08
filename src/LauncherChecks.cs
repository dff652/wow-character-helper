using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

static class LauncherChecks {
 public static int Snapshots(string root){var lines=new List<string>();try{foreach(var name in new[]{"launcher-old-infinite","launcher-new-infinite","launcher-old-classic","launcher-new-retail"})using(var b=new Bitmap(Path.Combine(root,name+".png"))){bool accepted;try{LauncherVision.Play(b);accepted=true;}catch{accepted=false;}if(accepted!=name.EndsWith("infinite"))throw new Exception("Incorrect launcher classification: "+name);lines.Add("PASS: "+name);}File.WriteAllLines(Path.Combine(root,"launcher-snapshots-verification.txt"),lines);return 0;}catch(Exception ex){lines.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"launcher-snapshots-verification.txt"),lines);return 1;}}
 static void Draw(Graphics g,string name,int x,int y){using(var b=new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates",name+".png")))g.DrawImage(b,new Rectangle(x,y,b.Width,b.Height),0,0,b.Width,b.Height,GraphicsUnit.Pixel);}
 public static int Run(string root){
  Directory.CreateDirectory(root);var lines=new List<string>();
  try{
   foreach(bool modern in new[]{false,true})using(var b=new Bitmap(1600,1050)){
    using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(30,33,40));Draw(g,"launcherIcon",180,100);
     int x=modern?1000:80;g.FillRectangle(Brushes.DodgerBlue,x-10,780,340,90);
     if(modern)Draw(g,"launcherAccount",x,680);else{Draw(g,"launcherProductOld",x,640);Draw(g,"launcherAccountOld",x,720);}
     Draw(g,modern?"launcherPlay":"launcherPlayOld",x+50,800);
    }
    b.Save(Path.Combine(root,modern?"modern.png":"classic.png"));var icon=LauncherVision.Icon(b);var play=LauncherVision.Play(b);if(icon.IsEmpty||Math.Abs(play.Y-800)>2)throw new Exception("Moved controls not located: icon="+icon+"; play="+play);lines.Add("PASS: "+(modern?"modern":"classic")+" layout controls at relocated positions");
    using(var g=Graphics.FromImage(b))g.FillRectangle(Brushes.Black,modern?1000:80,modern?680:640,240,modern?60:40);
    bool rejected=false;try{LauncherVision.Play(b);}catch{rejected=true;}if(!rejected)throw new Exception("Unverified product accepted");lines.Add("PASS: wrong/missing product rejected");
   }
   using(var empty=new Bitmap(1600,1000)){if(!LauncherVision.Icon(empty).IsEmpty)throw new Exception("Empty screen accepted");}
   lines.Add("PASS");File.WriteAllLines(Path.Combine(root,"launcher-verification.txt"),lines);return 0;
  }catch(Exception ex){lines.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"launcher-verification.txt"),lines);return 1;}
 }
}
