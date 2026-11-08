using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace CharacterVision {
 public class Word {public string Text;public int Line;public double X,Y,Width,Height;}
 public class OcrResult {public string Text,Language;public long Milliseconds;public Word[] Words;}
 public class Report {public string Version="0.1.0-preview",Source,Page,Notes,Session;public int Width,Height;public double OcrScale;public OcrResult Ocr;public string[] Candidates;}
 static class Recognition {
  public static Report Run(string imagePath,string outputRoot){
   string session=Path.Combine(outputRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(session);
   using(var input=new Bitmap(imagePath)){
    if((long)input.Width*input.Height>40000000)throw new Exception("图片过大，请使用不超过 4000 万像素的截图。");
    double scale=Math.Min(1,2400.0/Math.Max(input.Width,input.Height));
    using(var normalized=new Bitmap(Math.Max(1,(int)Math.Round(input.Width*scale)),Math.Max(1,(int)Math.Round(input.Height*scale)),PixelFormat.Format32bppArgb)){
     using(var g=Graphics.FromImage(normalized)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(input,new Rectangle(Point.Empty,normalized.Size));}
     normalized.Save(Path.Combine(session,"ocr-input.png"));
    }
    var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"));
    start.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"recognize-image.ps1"))+" -ImagePath "+Quote(Path.Combine(session,"ocr-input.png"))+" -OutputPath "+Quote(Path.Combine(session,"ocr.json"));start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardError=true;
    using(var process=Process.Start(start)){var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(45000)){process.Kill();throw new Exception("OCR 超过 45 秒，已停止识别进程。");}string message=error.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new Exception("本地 OCR 失败："+message);}
    var serializer=new JavaScriptSerializer();var ocr=serializer.Deserialize<OcrResult>(File.ReadAllText(Path.Combine(session,"ocr.json")));if(ocr==null||ocr.Words==null)throw new Exception("OCR 结果格式不完整。");
    double sx=input.Width/(double)Math.Max(1,(int)Math.Round(input.Width*scale)),sy=input.Height/(double)Math.Max(1,(int)Math.Round(input.Height*scale));
    foreach(var word in ocr.Words){word.X*=sx;word.Y*=sy;word.Width*=sx;word.Height*=sy;}
    var report=Analyze(ocr,input.Width,input.Height);report.Source=Path.GetFileName(imagePath);report.Session=session;report.OcrScale=scale;
    using(var annotated=new Bitmap(input)){using(var g=Graphics.FromImage(annotated))using(var pen=new Pen(Color.LimeGreen,Math.Max(1,input.Width/1600f)))foreach(var word in ocr.Words)g.DrawRectangle(pen,(float)word.X,(float)word.Y,(float)word.Width,(float)word.Height);annotated.Save(Path.Combine(session,"annotated.png"));}
    File.WriteAllText(Path.Combine(session,"report.json"),serializer.Serialize(report));File.WriteAllText(Path.Combine(session,"summary.txt"),Summary(report));return report;
   }
  }
  static string Quote(string s){if(s.Contains("\"")||s.Contains("\r")||s.Contains("\n"))throw new Exception("路径包含无效字符。");return "\""+s+"\"";}
  internal static Report Analyze(OcrResult ocr,int width,int height){
   string text=ocr.Text??"";string compact=new string(text.Where(c=>!char.IsWhiteSpace(c)).ToArray());string page="未知页面";
   var rows=ocr.Words.GroupBy(w=>w.Line).Select(g=>new {Text=string.Concat(g.OrderBy(w=>w.X).Select(w=>w.Text)),X=g.Min(w=>w.X),Y=g.Min(w=>w.Y)}).ToArray();
   string first=string.Concat(ocr.Words.Where(w=>w.X>width*.34&&w.X<width*.49&&w.Y>height*.025&&w.Y<height*.075).OrderBy(w=>w.X).Select(w=>w.Text));
   string last=string.Concat(ocr.Words.Where(w=>w.X>width*.51&&w.X<width*.66&&w.Y>height*.025&&w.Y<height*.075).OrderBy(w=>w.X).Select(w=>w.Text));
   bool dialog=string.Concat(ocr.Words.Where(w=>w.X>width*.3&&w.X<width*.7&&w.Y>height*.35&&w.Y<height*.75).OrderBy(w=>w.Line).ThenBy(w=>w.X).Select(w=>w.Text)).Contains("确定");
   if(compact.Contains("阵营")&&(compact.Contains("不平衡")||compact.Contains("人口")))page="疑似阵营限制提示";
   else if(dialog)page="疑似对话框，正文尚未确认";
   else if(compact.Contains("规则集")&&compact.IndexOf("PvP",StringComparison.OrdinalIgnoreCase)>=0)page="疑似规则集选择页";
   else if(compact.Contains("自定义")||compact.Contains("肤色")||(first.Length>0&&last.Length>0&&compact.Contains("后退")&&compact.Contains("眼睛")))page="疑似外观/姓名页";
   else if(compact.Contains("种族特长")||(compact.Contains("种族")&&compact.Contains("职业")))page="疑似角色配置页";
   else if(compact.Contains("进入游戏")&&compact.Contains("WoW"))page="疑似战网页面";
   var candidates=new List<string>();foreach(var group in new[]{new[]{"阵营","部落","联盟"},new[]{"种族","人类","矮人","暗夜精灵","侏儒","兽人","亡灵","牛头人","巨魔"},new[]{"职业","战士","猎人","法师","潜行者","牧师","术士","圣骑士","德鲁伊","萨满祭司"}}){var matches=group.Skip(1).Where(compact.Contains).ToArray();candidates.Add(group[0]+"文字候选："+(matches.Length==0?"未读到":string.Join("、",matches)));}
   foreach(var group in new[]{new[]{"阵营","部落","联盟"},new[]{"种族","人类","矮人","暗夜精灵","侏儒","兽人","亡灵","牛头人","巨魔"},new[]{"职业","战士","猎人","法师","潜行者","牧师","术士","圣骑士","德鲁伊","萨满祭司"}}){var titles=rows.Where(r=>r.X>width*.75&&group.Skip(1).Contains(r.Text)).Select(r=>r.Text).Distinct().ToArray();candidates.Add("右侧独立标题 / "+group[0]+"："+(titles.Length==1?titles[0]:titles.Length==0?"未确认":"存在多个候选"));}
   if(page=="疑似外观/姓名页"){candidates.Add("主名位置候选："+(first.Length==0?"未读到":first));candidates.Add("次名位置候选："+(last.Length==0?"未读到":last));}else candidates.Add("姓名：未归属；请核对标注图。");
   return new Report{Width=width,Height=height,Ocr=ocr,Page=page,Candidates=candidates.ToArray(),Notes="文字出现不等于控件已选中。页面分类为启发式候选；不确认角色配置或建号成功。Windows OCR 未提供本工具可用的置信分数。"};
  }
  public static string Summary(Report r){return r.Page+Environment.NewLine+r.Width+" × "+r.Height+"；本地 OCR "+r.Ocr.Milliseconds+" ms；"+r.Ocr.Words.Length+" 个文字框"+Environment.NewLine+string.Join(Environment.NewLine,r.Candidates)+Environment.NewLine+r.Notes+Environment.NewLine+Environment.NewLine+"OCR 原文："+Environment.NewLine+r.Ocr.Text;}
 }
}
