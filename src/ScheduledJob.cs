using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;

public sealed class ScheduledJob {
 public string Id=Guid.NewGuid().ToString("N"),Name,Status="待执行",Detail="等待计划时间。";
 public DateTime RunAt,CreatedAt=DateTime.Now;
 public string LastStarted,LastFinished;
 public CreationConfig Config;
}
static class JobStore {
 internal static string TestRoot;
 public static string Root {get{return TestRoot??Path.Combine(AppPaths.Data,"tasks");}}
 public static void Save(ScheduledJob job){
  if(string.IsNullOrWhiteSpace(job.Name)||job.Name.Length>80)throw new Exception("任务名称需为 1～80 个字符。");
  Guid id;if(!Guid.TryParseExact(job.Id,"N",out id))throw new Exception("任务标识无效。");
  job.Config.Validate();Directory.CreateDirectory(Root);string path=Path.Combine(Root,job.Id+".xml"),temp=path+".tmp";
  using(var f=File.Create(temp))new XmlSerializer(typeof(ScheduledJob)).Serialize(f,job);
  if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
 }
 public static List<ScheduledJob> All(){var result=new List<ScheduledJob>();if(!Directory.Exists(Root))return result;foreach(var file in Directory.GetFiles(Root,"*.xml")){using(var f=File.OpenRead(file))result.Add((ScheduledJob)new XmlSerializer(typeof(ScheduledJob)).Deserialize(f));}return result.OrderBy(j=>j.RunAt).ToList();}
 public static void Delete(ScheduledJob job){if(job.Status=="运行中")throw new Exception("请先停止正在运行的任务。");Guid id;if(!Guid.TryParseExact(job.Id,"N",out id))throw new Exception("任务标识无效。");File.Delete(Path.Combine(Root,job.Id+".xml"));}
 public static void Recover(DateTime now){foreach(var job in All()){if(job.Status=="运行中"){job.Status="已中断";job.Detail="上次程序退出时任务尚未完成。请检查游戏结果后手动启动。";Save(job);}else if(job.Status=="待执行"&&job.RunAt<now){job.Status="已过期";job.Detail="工具关闭期间错过计划时间，请重新排期或立即启动。";Save(job);}}}
 public static ScheduledJob Due(DateTime now){return All().FirstOrDefault(j=>j.Status=="待执行"&&j.RunAt<=now);}
}
