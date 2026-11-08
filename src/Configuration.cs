using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Serialization;

public sealed class CreationConfig {
 public string RuleSet="PvP",Faction="部落",Race="牛头人",Gender="男性",ClassName="德鲁伊";
 public string FirstName="歪瑞歪瑞",LastName="古德";
 public string DisplayMode="自动识别";
 public static readonly string[] DisplayModes={"自动识别","标准 16:9（自动分辨率）","1920×1080 · 16:9","2560×1440 · 16:9","3840×2160 · 16:9"};
 public void ValidateDisplay(int width,int height){if(width<1280||height<720||Math.Abs(width/(double)height-16.0/9)>0.03)throw new Exception("检测到 "+width+"×"+height+"；当前仅适配标准 16:9 布局，请将游戏切换为 16:9 窗口。");int index=Array.IndexOf(DisplayModes,DisplayMode);if(index>=2&&(width!=new[]{1920,2560,3840}[index-2]||height!=new[]{1080,1440,2160}[index-2]))throw new Exception("实际游戏客户区 "+width+"×"+height+" 与手动设置不一致，请选择自动识别或调整游戏窗口。");}
 public int IntervalSeconds=120,MaxAttempts=60,MaxMinutes=0;
 public bool Restart=true,Scheduled=false;
 public DateTime StartAt=DateTime.Now.AddMinutes(5);
 public string GamePath=@"D:\Games\wow\World of Warcraft_cn\_forever_\Wow.exe";
 public string LauncherPath=@"C:\Program Files (x86)\Battle.net\Battle.net.exe";
 public static readonly string[] Rules={"普通","PvP","角色扮演","角色扮演 PvP"};
 public static readonly string[] Alliance={"人类","矮人","暗夜精灵","侏儒"};
 public static readonly string[] Horde={"兽人","亡灵","牛头人","巨魔"};
 public static readonly string[] Classes={"战士","猎人","法师","潜行者","牧师","术士","圣骑士","德鲁伊","萨满祭司"};
 [XmlIgnore] public string Summary {get{return RuleSet+" / "+Faction+" / "+Race+" / "+Gender+" / "+ClassName+" / "+FirstName+" · "+LastName;}}
 [XmlIgnore] public bool Builtin {get{return RuleSet=="PvP"&&Faction=="部落"&&Race=="牛头人"&&Gender=="男性"&&ClassName=="德鲁伊"&&FirstName=="歪瑞歪瑞"&&LastName=="古德";}}
 [XmlIgnore] public int RuleIndex {get{return Array.IndexOf(Rules,RuleSet);}}
 [XmlIgnore] public Point RacePoint {get{return new Point(Faction=="部落"?329:104,new[]{190,307,423,540}[Array.IndexOf(Faction=="部落"?Horde:Alliance,Race)]);}}
 [XmlIgnore] public Point ClassPoint {get{return new Point(new[]{815,934,1049,1165,1280,1395,1510,1625,1740}[Array.IndexOf(Classes,ClassName)],1343);}}
 [XmlIgnore] public int GenderX {get{return Gender=="男性"?1235:1325;}}
 [XmlIgnore] public string Key {get{using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",new[]{RuleSet,Faction,Race,Gender,ClassName,FirstName,LastName})))).Replace("-","").ToLowerInvariant();}}
 public CreationConfig Copy(){return (CreationConfig)MemberwiseClone();}
 public void Validate(){
  if(!DisplayModes.Contains(DisplayMode))throw new Exception("请选择显示布局模式。");
  if(!Rules.Contains(RuleSet)||!(Faction=="部落"||Faction=="联盟")||!(Faction=="部落"?Horde:Alliance).Contains(Race)||!Classes.Contains(ClassName)||!(Gender=="男性"||Gender=="女性"))throw new Exception("请选择完整的规则集、阵营、种族、性别和职业。");
  foreach(var n in new[]{FirstName,LastName})if(string.IsNullOrWhiteSpace(n)||n!=n.Trim()||n.Any(char.IsControl)||n.Length>24)throw new Exception("主名和次名均不能为空，不含首尾空格或控制字符，最多 24 字符。最终命名限制由游戏校验。");
  if(IntervalSeconds<30||IntervalSeconds>86400||MaxAttempts<1||MaxAttempts>10000||MaxMinutes<0||MaxMinutes>10080)throw new Exception("定时参数超出范围。");
  if(!Path.IsPathRooted(GamePath)||!Path.IsPathRooted(LauncherPath)||!GamePath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!LauncherPath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new Exception("请选择游戏和战网的完整程序路径。");
 }
 internal static string TestRoot;
 public static string SettingsPath {get{return Path.Combine(TestRoot??AppPaths.Config,"建号配置.xml");}}
 public static CreationConfig Load(){if(!File.Exists(SettingsPath))return new CreationConfig();using(var f=File.OpenRead(SettingsPath)){var c=(CreationConfig)new XmlSerializer(typeof(CreationConfig)).Deserialize(f);c.Validate();return c;}}
 public void Save(){Validate();Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));string temp=SettingsPath+".tmp";using(var f=File.Create(temp))new XmlSerializer(typeof(CreationConfig)).Serialize(f,this);if(File.Exists(SettingsPath))File.Replace(temp,SettingsPath,null);else File.Move(temp,SettingsPath);}
}

sealed class CalibrationBundle:IDisposable {
 public CreationConfig Config;public Bitmap RaceImage,NameImage;
 public void Dispose(){if(RaceImage!=null)RaceImage.Dispose();if(NameImage!=null)NameImage.Dispose();}
}
static class CalibrationStore {
 internal static string TestRoot;
 public static string Dir(CreationConfig c){return Path.Combine(TestRoot??AppPaths.Data,"profiles",c.Key);}
 public static bool Ready(CreationConfig c){if(c.Builtin)return true;string d=Dir(c);return File.Exists(Path.Combine(d,"confirmed.txt"))&&File.ReadAllText(Path.Combine(d,"confirmed.txt"))==c.Key&&new[]{"faction","race","gender","class","first","last"}.All(n=>File.Exists(Path.Combine(d,n+".png")));}
 public static void Save(CalibrationBundle b){
  string d=Dir(b.Config);Directory.CreateDirectory(d);var lines=new List<string>();
  Add(b.RaceImage,d,lines,"faction",2170,86,100,40);
  Add(b.RaceImage,d,lines,"race",2170,410,180,40);
  Add(b.RaceImage,d,lines,"class",2170,735,180,40);
  Add(b.RaceImage,d,lines,"gender",b.Config.GenderX-40,22,80,72);
  Add(b.NameImage,d,lines,"first",900,45,350,39);
  Add(b.NameImage,d,lines,"last",1310,45,350,39);
  File.WriteAllLines(Path.Combine(d,"index.txt"),lines);
  b.RaceImage.Save(Path.Combine(d,"角色预览.png"));b.NameImage.Save(Path.Combine(d,"姓名预览.png"));
  File.WriteAllText(Path.Combine(d,"配置说明.txt"),b.Config.Summary,Encoding.UTF8);
  File.WriteAllText(Path.Combine(d,"confirmed.txt"),b.Config.Key);
 }
 static void Add(Bitmap image,string dir,List<string> lines,string name,int x,int y,int w,int h){using(var crop=image.Clone(new Rectangle(x,y,w,h),System.Drawing.Imaging.PixelFormat.Format32bppArgb))crop.Save(Path.Combine(dir,name+".png"));lines.Add(string.Join("|",new[]{name,x.ToString(),y.ToString(),w.ToString(),h.ToString()}));}
}

public sealed class AttemptRecord {
 public string Id=Guid.NewGuid().ToString("N"),Time=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),Status,Detail,Screenshot,ConfirmedAt;
 public int Attempt;public CreationConfig Config;
}
static class ResultsStore {
 static readonly object Gate=new object();
 internal static string TestRoot;
 public static string Root {get{return TestRoot==null?AppPaths.Results:Path.Combine(TestRoot,"records");}}
 public static AttemptRecord Add(CreationConfig config,int attempt,string status,string detail,Bitmap picture){
  var record=new AttemptRecord{Config=config.Copy(),Attempt=attempt,Status=status,Detail=detail};
  lock(Gate){Directory.CreateDirectory(Root);if(picture!=null){record.Screenshot=record.Id+".png";picture.Save(ImagePath(record));}Save(record);}return record;
 }
 static void Save(AttemptRecord record){string file=Path.Combine(Root,record.Id+".xml"),tmp=file+".tmp";using(var f=File.Create(tmp))new XmlSerializer(typeof(AttemptRecord)).Serialize(f,record);if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);}
 public static void Update(AttemptRecord record,string status,string detail,Bitmap picture){lock(Gate){if(picture!=null){string image=Path.Combine(Root,record.Id+"-result.png");picture.Save(image);record.Screenshot=Path.GetFileName(image);}record.Status=status;record.Detail=detail;Save(record);}}
 public static List<AttemptRecord> All(){var list=new List<AttemptRecord>();lock(Gate){if(!Directory.Exists(Root))return list;foreach(string file in Directory.GetFiles(Root,"*.xml")){try{using(var f=File.OpenRead(file))list.Add((AttemptRecord)new XmlSerializer(typeof(AttemptRecord)).Deserialize(f));}catch{}}}return list.OrderByDescending(r=>r.Time).ToList();}
 public static string ImagePath(AttemptRecord r){return string.IsNullOrEmpty(r.Screenshot)?null:Path.Combine(Root,Path.GetFileName(r.Screenshot));}
 public static void Confirm(AttemptRecord r){if(r.Status!="待确认")throw new Exception("只有待确认记录可以标记成功。");lock(Gate){r.Status="成功（人工确认）";r.ConfirmedAt=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");r.Detail="用户核对游戏后确认建号成功；程序未自动判定。";Save(r);}}
}
