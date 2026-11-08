using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

sealed partial class ConfigurationForm {
 readonly DataGridView jobsGrid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};
 readonly TextBox jobName=new TextBox{Text="我的建号任务",Dock=DockStyle.Fill,MaxLength=80};
 readonly DateTimePicker jobTime=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm:ss",Value=DateTime.Now.AddMinutes(5),Dock=DockStyle.Fill};
 readonly TextBox jobDetail=new TextBox{ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
 ScheduledJob activeJob;DateTime nextJobCheck;
 void BuildJobsPage(){
  var page=new TabPage("任务管理"){Padding=new Padding(10)};tabs.TabPages.Add(page);
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,70));root.RowStyles.Add(new RowStyle(SizeType.Percent,30));page.Controls.Add(root);
  var top=Table();Add(top,"任务名称",jobName);Add(top,"计划时间",jobTime);Wide(top,Note("新建任务会保存当前“建号与定时”和“环境设置”的配置快照。任务到点串行执行；工具需保持打开，建号会占用前台。关闭期间错过的任务不会补跑。"));root.Controls.Add(top,0,0);
  var create=ButtonText("保存为定时任务");var run=ButtonText("立即启动");var stopJob=ButtonText("停止 / 取消排期");var reschedule=ButtonText("按上方时间重新排期");var remove=ButtonText("删除");var refresh=ButtonText("刷新");root.Controls.Add(Buttons(create,run,stopJob,reschedule,remove,refresh),0,1);root.Controls.Add(jobsGrid,0,2);root.Controls.Add(jobDetail,0,3);
  foreach(var name in new[]{"任务名称","计划时间","状态","角色配置"})jobsGrid.Columns.Add(name,name);jobsGrid.Columns["角色配置"].FillWeight=180;jobsGrid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;jobsGrid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
  create.Click+=(s,e)=>JobAction(()=>{if(jobTime.Value<=DateTime.Now)throw new Exception("请选择未来的计划时间。");var c=ReadConfig();if(!CalibrationStore.Ready(c))throw new Exception("请先完成这组配置的预览校准。");c.Scheduled=false;JobStore.Save(new ScheduledJob{Name=jobName.Text.Trim(),RunAt=jobTime.Value,Config=c.Copy()});Write("定时任务已保存；关闭工具后不会执行。");RefreshJobs();});
  run.Click+=(s,e)=>{var job=SelectedJob();if(job!=null)StartJob(job);};
  stopJob.Click+=(s,e)=>JobAction(()=>{var job=SelectedJob();if(job==null)return;if(activeJob!=null&&activeJob.Id==job.Id){if(cancel!=null)cancel.Cancel();Write("正在停止任务："+job.Name);}else{job.Status="已停止";job.Detail="用户取消计划；可重新排期或立即启动。";JobStore.Save(job);RefreshJobs();}});
  reschedule.Click+=(s,e)=>JobAction(()=>{var job=SelectedJob();if(job==null)return;if(job.Status=="运行中")throw new Exception("请先停止任务。");if(jobTime.Value<=DateTime.Now)throw new Exception("请选择未来的计划时间。");job.RunAt=jobTime.Value;job.Status="待执行";job.Detail="等待计划时间。";JobStore.Save(job);RefreshJobs();});
  remove.Click+=(s,e)=>JobAction(()=>{var job=SelectedJob();if(job==null)return;JobStore.Delete(job);RefreshJobs();});refresh.Click+=(s,e)=>JobAction(RefreshJobs);jobsGrid.SelectionChanged+=(s,e)=>ShowJob();RefreshJobs();
 }
 void JobAction(Action action){try{action();}catch(Exception ex){Warn(ex.Message);}}
 ScheduledJob SelectedJob(){return jobsGrid.SelectedRows.Count==0?null:jobsGrid.SelectedRows[0].Tag as ScheduledJob;}
 void RefreshJobs(){var selected=SelectedJob();jobsGrid.Rows.Clear();foreach(var job in JobStore.All()){int i=jobsGrid.Rows.Add(job.Name,job.RunAt.ToString("yyyy-MM-dd HH:mm:ss"),job.Status,job.Config.Summary);jobsGrid.Rows[i].Tag=job;if(selected!=null&&selected.Id==job.Id)jobsGrid.Rows[i].Selected=true;}ShowJob();}
 void ShowJob(){var job=SelectedJob();jobDetail.Text=job==null?"尚无任务。先配置角色，再保存为定时任务。每个任务按自身的重试次数和间隔执行。":job.Config.Summary+Environment.NewLine+"状态："+job.Status+"；"+job.Detail+Environment.NewLine+"最近启动："+job.LastStarted+"；最近结束："+job.LastFinished+Environment.NewLine+"“已完成”表示本轮任务结束，不代表建号成功。结果请在结果记录页核对。";}
 async void StartJob(ScheduledJob job){if(preview)return;if(busy){Warn("已有操作运行中，请先停止；到点任务会等当前操作结束后再执行。");return;}try{job.Config.Validate();if(!CalibrationStore.Ready(job.Config))throw new Exception("任务配置缺少校准，请使用对应配置完成预览。");job.Status="运行中";job.LastStarted=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");job.LastFinished=null;job.Detail="正在执行。";JobStore.Save(job);activeJob=job;RefreshJobs();var c=job.Config.Copy();c.Scheduled=false;Write("启动任务："+job.Name);await ExecuteConfig(c,job);}catch(Exception ex){activeJob=null;job.Status="需检查";job.Detail=ex.Message;JobStore.Save(job);RefreshJobs();Write("任务停止："+ex.Message);}}
 void CheckDueJobs(){if(preview||busy||DateTime.UtcNow<nextJobCheck)return;nextJobCheck=DateTime.UtcNow.AddSeconds(2);try{var job=JobStore.Due(DateTime.Now);if(job!=null)StartJob(job);}catch(Exception ex){nextJobCheck=DateTime.UtcNow.AddMinutes(1);Write("读取定时任务失败："+ex.Message);}}
 internal async System.Threading.Tasks.Task CheckTaskCancellation(string root){
  string previous=JobStore.TestRoot;JobStore.TestRoot=System.IO.Path.Combine(root,Guid.NewGuid().ToString("N"));
  try{var c=new CreationConfig{Scheduled=true,StartAt=DateTime.Now.AddHours(1)};var job=new ScheduledJob{Name="cancellation check",RunAt=c.StartAt,Config=c,Status="运行中"};JobStore.Save(job);activeJob=job;var running=ExecuteConfig(c,job);cancel.Cancel();await running;
   if(busy||activeJob!=null||JobStore.All().Single().Status!="已停止"||activeRunner.AttemptNumber!=0)throw new Exception("任务停止检查失败。");
   System.IO.Directory.CreateDirectory(root);System.IO.File.WriteAllText(System.IO.Path.Combine(root,"task-ui-verification.txt"),"PASS: scheduled execution cancelled before any game interaction; task state persisted and UI unlocked.");
  }finally{JobStore.TestRoot=previous;}
 }
}
