using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

enum CombinationState { Verified, Unsupported, Unknown }
static class CombinationRules {
 public const string Build="1.60.1.70862";

 public static CombinationState Assess(CreationConfig c){
  if(c.ClientBuild!=Build)return CombinationState.Unknown;
  // Evidence: local game preview and submissions, 2026-11-08.
  if(c.Race=="牛头人"&&c.ClassName=="德鲁伊")return CombinationState.Verified;
  // Explicitly reported unsupported by the user for this client.
  if(c.Race=="亡灵"&&c.ClassName=="德鲁伊")return CombinationState.Unsupported;
  return CombinationState.Unknown;
 }
 public static string[] Choices(string race,string build){return CreationConfig.Classes.Where(job=>Assess(new CreationConfig{Race=race,ClassName=job,ClientBuild=build})!=CombinationState.Unsupported).ToArray();}
 public static void ValidateSave(CreationConfig c){c.Validate();if(Assess(c)==CombinationState.Unsupported)throw new Exception("当前无限服规则不支持“"+c.Race+"—"+c.ClassName+"”。请更换种族或职业；预览校准不能解决非法组合。");}
 public static void ValidateExecution(CreationConfig c){ValidateSave(c);if(Assess(c)!=CombinationState.Verified)throw new Exception("这组种族与职业尚未列入已核实白名单（客户端 "+c.ClientBuild+"）。可保存草稿或进行不提交预览，不能自动执行。");}
 public static void ValidateInstalledBuild(CreationConfig c){if(!File.Exists(c.GamePath))throw new Exception("游戏程序不存在。");if(FileVersionInfo.GetVersionInfo(c.GamePath).FileVersion!=c.ClientBuild)throw new Exception("实际游戏版本与组合规则版本不一致，请更新规则后再执行。");}
}
