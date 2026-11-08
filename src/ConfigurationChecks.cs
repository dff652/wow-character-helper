using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Xml.Serialization;

static class ConfigurationChecks {
 public static int Run(string root){
  root=Path.GetFullPath(root);Directory.CreateDirectory(root);CreationConfig.TestRoot=root;CalibrationStore.TestRoot=root;ResultsStore.TestRoot=root;
  var log=new List<string>();
  Action<bool,string> check=(yes,name)=>{if(!yes)throw new Exception("FAIL: "+name);log.Add("PASS: "+name);};
  try{
   var c=new CreationConfig();c.Validate();c.ValidateDisplay(3840,2160);var manual=c.Copy();manual.DisplayMode=CreationConfig.DisplayModes[2];bool mismatch=false;try{manual.ValidateDisplay(3840,2160);}catch{mismatch=true;}check(mismatch,"manual resolution mismatch stops");bool ultrawide=false;try{c.ValidateDisplay(3440,1440);}catch{ultrawide=true;}check(ultrawide,"unsupported ultrawide stops");check(c.Builtin&&CalibrationStore.Ready(c),"default verified profile");
   var changed=c.Copy();changed.FirstName="测试配置";check(!changed.Builtin&&!CalibrationStore.Ready(changed),"changed name requires calibration");
   changed.Save();var read=CreationConfig.Load();check(read.FirstName==changed.FirstName&&read.Key==changed.Key,"Unicode configuration round trip");
   changed.LastName="测试次名";changed.IntervalSeconds=300;changed.Scheduled=true;changed.StartAt=DateTime.Now.AddDays(1);changed.Save();read=CreationConfig.Load();check(read.LastName==changed.LastName&&read.IntervalSeconds==300&&read.Scheduled,"configuration replacement and schedule");
   var invalid=c.Copy();invalid.Faction="联盟";bool rejected=false;try{invalid.Validate();}catch{rejected=true;}check(rejected,"mismatched faction and race rejected");
   invalid=c.Copy();invalid.FirstName=" ";rejected=false;try{invalid.Validate();}catch{rejected=true;}check(rejected,"blank name rejected");
   invalid=c.Copy();invalid.IntervalSeconds=0;rejected=false;try{invalid.Validate();}catch{rejected=true;}check(rejected,"invalid interval rejected");
   check(new CreationConfig{Faction="联盟",Race="暗夜精灵"}.RacePoint==new Point(104,423),"faction changes actual race coordinates");
   check(new CreationConfig{Gender="女性"}.GenderX==1325,"gender routes to female control");
   foreach(var rule in CreationConfig.Rules){var a=c.Copy();a.RuleSet=rule;a.Validate();check(a.RuleIndex>=0&&a.RuleIndex<4,"ruleset "+rule);}
   using(var image=new Bitmap(16,16)){
    var pending=ResultsStore.Add(changed,1,"待确认","test",image);check(File.Exists(ResultsStore.ImagePath(pending)),"result screenshot retained");ResultsStore.Confirm(pending);
    var saved=ResultsStore.All().Single(r=>r.Id==pending.Id);check(saved.Status=="成功（人工确认）"&&!string.IsNullOrEmpty(saved.ConfirmedAt)&&saved.Config.FirstName==changed.FirstName,"success confirmation persisted with identity and time");
    var blocked=ResultsStore.Add(c,2,"待确认","before submit",image);ResultsStore.Update(blocked,"被拦截","server blocked",image);check(ResultsStore.All().Count(r=>r.Id==blocked.Id)==1&&File.Exists(ResultsStore.ImagePath(blocked)),"one durable attempt updated with result screenshot");rejected=false;try{ResultsStore.Confirm(blocked);}catch{rejected=true;}check(rejected,"blocked result cannot be marked success");
   }
   // This profile exercises actual non-default coordinates in the no-submit preview test.
   File.WriteAllText(Path.Combine(ResultsStore.Root,"malformed.xml"),"<AttemptRecord><Status>待确认</Status></AttemptRecord>");check(ResultsStore.All().All(r=>r.Config!=null),"incomplete result record cannot crash result list");
   var preview=c.Copy();preview.Gender="女性";preview.FirstName="歪瑞歪瑞";preview.LastName="古德";
   using(var f=File.Create(Path.Combine(root,"preview-config.xml")))new XmlSerializer(typeof(CreationConfig)).Serialize(f,preview);
   // Relocation tests use an isolated legacy installation, never the user's data.
   string migration=Path.Combine(root,"migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(migration);
   AppPaths.TestRoot=migration;CreationConfig.TestRoot=migration;ResultsStore.TestRoot=migration;changed.Save();
   AttemptRecord legacy;using(var picture=new Bitmap(16,16))legacy=ResultsStore.Add(changed,1,"待确认","legacy",picture);
   legacy.Screenshot=Path.Combine(migration,"records",legacy.Id+".png");
   using(var f=File.Create(Path.Combine(migration,"records",legacy.Id+".xml")))new XmlSerializer(typeof(AttemptRecord)).Serialize(f,legacy);
   File.WriteAllText(Path.Combine(migration,"运行记录.txt"),"original log");File.WriteAllText(Path.Combine(migration,"调试-尺寸.txt"),"3840x2160");
   using(var picture=new Bitmap(8,8))picture.Save(Path.Combine(migration,"最后结果.png"));
   Directory.CreateDirectory(Path.Combine(migration,"profiles","sample"));File.WriteAllText(Path.Combine(migration,"profiles","sample","confirmed.txt"),"preserve");
   AppPaths.Initialize();CreationConfig.TestRoot=null;ResultsStore.TestRoot=null;CalibrationStore.TestRoot=null;
   check(CreationConfig.Load().FirstName==changed.FirstName&&!File.Exists(Path.Combine(migration,"建号配置.xml")),"legacy config moved and loaded");
   var moved=ResultsStore.All().Single();check(File.Exists(ResultsStore.ImagePath(moved)),"legacy absolute screenshot reference resolves after move");
   check(File.ReadAllText(Path.Combine(AppPaths.Logs,"运行记录.txt"))=="original log"&&File.Exists(Path.Combine(AppPaths.Screenshots,"最后结果.png")),"legacy logs and screenshots preserved");
   check(File.Exists(Path.Combine(AppPaths.Profiles,"sample","confirmed.txt")),"legacy calibration moved");
   AppPaths.Initialize();check(ResultsStore.All().Count==1,"migration is idempotent");
   File.WriteAllText(Path.Combine(migration,"运行记录.txt"),"conflict log");AppPaths.Initialize();
   check(File.ReadAllText(Path.Combine(AppPaths.Logs,"运行记录.txt"))=="original log"&&File.ReadAllText(Path.Combine(AppPaths.Data,"legacy","运行记录.txt"))=="conflict log","migration conflict keeps both files");
   check(Directory.GetFiles(migration).Length==0,"runtime data does not clutter application root");
   JobStore.TestRoot=Path.Combine(root,"jobs-"+Guid.NewGuid().ToString("N"));var now=DateTime.Now;
   var future=new ScheduledJob{Name="future",RunAt=now.AddMinutes(5),Config=c.Copy()};JobStore.Save(future);check(JobStore.Due(now)==null,"future task does not run early");
   future.RunAt=now.AddSeconds(-1);JobStore.Save(future);check(JobStore.Due(now).Id==future.Id,"due task selected");
   future.Status="已停止";JobStore.Save(future);check(JobStore.Due(now)==null,"stopped task is not scheduled");
   future.Status="运行中";JobStore.Save(future);JobStore.Recover(now);check(JobStore.All().Single().Status=="已中断","interrupted task requires manual restart");
   future.Status="待执行";JobStore.Save(future);JobStore.Recover(now);check(JobStore.All().Single().Status=="已过期","missed schedule is not replayed on startup");
   future.Config.FirstName="任务快照";JobStore.Save(future);check(JobStore.All().Single().Config.FirstName=="任务快照"&&c.FirstName!="任务快照","task stores separate configuration snapshot");
   future.Status="运行中";bool cannotDelete=false;try{JobStore.Delete(future);}catch{cannotDelete=true;}check(cannotDelete,"running task cannot be deleted");future.Status="已停止";JobStore.Delete(future);check(JobStore.All().Count==0,"stopped task can be deleted");
   var validJob=new ScheduledJob{Name="healthy",RunAt=DateTime.Now.AddMinutes(-1),Config=c.Copy()};JobStore.Save(validJob);File.WriteAllText(Path.Combine(JobStore.Root,"broken.xml"),"<broken");check(JobStore.All().Count==1&&JobStore.LastReadError!=null,"corrupt task isolated without losing valid tasks");bool blocksQueue=false;try{JobStore.Due(DateTime.Now);}catch{blocksQueue=true;}check(blocksQueue,"corrupt task prevents automatic dispatch");check(File.Exists(Path.Combine(JobStore.Root,"broken.xml")),"corrupt file preserved for investigation");
   double tick=0;var flow=new FlowTrace(()=>tick);flow.Begin("rules","select");tick=2;flow.Begin("role","configure");tick=5;var steps=flow.Snapshot();check(steps.Single(x=>x.Key=="rules").Seconds==2&&steps.Single(x=>x.Key=="role").Seconds==3,"node durations follow actual transitions");
   flow.Finish("已停止","cancelled");tick=20;check(flow.Snapshot().Single(x=>x.Key=="role").Seconds==3,"stopped node timing freezes");flow.Begin("rules","retry");tick=24;flow.Finish("已完成","selected");check(flow.Snapshot().Single(x=>x.Key=="rules").Seconds==6&&flow.Snapshot().Single(x=>x.Key=="rules").Visits==2,"retry accumulates node time and visit count");
   flow.Begin("result","server result");flow.Finish("被拦截","faction imbalance");flow.Begin("wait","cooldown");check(flow.Snapshot().Single(x=>x.Key=="result").Status=="被拦截","blocked result stays distinct from completion");
   log.Add("PASS");File.WriteAllLines(Path.Combine(root,"config-verification.txt"),log);return 0;
  }catch(Exception ex){log.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"config-verification.txt"),log);return 1;}
  finally{CreationConfig.TestRoot=null;CalibrationStore.TestRoot=null;ResultsStore.TestRoot=null;AppPaths.TestRoot=null;JobStore.TestRoot=null;}
 }
}
