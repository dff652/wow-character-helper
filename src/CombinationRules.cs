using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

enum CombinationState { Verified, Unsupported, Unknown }
static class CombinationRules {
 public const string Build="1.60.1.70862";
 // UI availability observed in the male PvP creation screen; not an automation whitelist.
 static readonly Dictionary<string,string[]> available=new Dictionary<string,string[]> {
  {"人类",new[]{"战士","猎人","法师","潜行者","牧师","术士","圣骑士"}},
  {"矮人",new[]{"战士","猎人","潜行者","牧师","圣骑士","萨满祭司"}},
  {"暗夜精灵",new[]{"战士","猎人","潜行者","牧师","德鲁伊"}},
  {"侏儒",new[]{"战士","法师","潜行者","牧师","术士"}},
  {"兽人",new[]{"战士","猎人","法师","潜行者","术士","萨满祭司"}},
  {"亡灵",new[]{"战士","法师","潜行者","牧师","术士","圣骑士"}},
  {"牛头人",new[]{"战士","猎人","德鲁伊","萨满祭司"}},
  {"巨魔",new[]{"战士","猎人","法师","潜行者","牧师","术士","萨满祭司"}}
 };
 public static bool HasCatalog(CreationConfig c){return c.ClientBuild==Build&&c.RuleSet=="PvP"&&c.Gender=="男性"&&available.ContainsKey(c.Race);}

 public static CombinationState Assess(CreationConfig c){
  if(!HasCatalog(c))return CombinationState.Unknown;
  if(!available[c.Race].Contains(c.ClassName))return CombinationState.Unsupported;
  // Evidence: local game preview and submissions, 2026-11-08.
  if(c.Race=="牛头人"&&c.ClassName=="德鲁伊")return CombinationState.Verified;
  return CombinationState.Unknown;
 }
 public static string[] Choices(string race,string build,string ruleSet="PvP",string gender="男性"){
  var c=new CreationConfig{Race=race,ClientBuild=build,RuleSet=ruleSet,Gender=gender};
  return HasCatalog(c)?available[race].ToArray():CreationConfig.Classes.ToArray();
 }
 public static void ValidateSave(CreationConfig c){c.Validate();if(Assess(c)==CombinationState.Unsupported)throw new Exception("当前无限服规则不支持“"+c.Race+"—"+c.ClassName+"”。请更换种族或职业；预览校准不能解决非法组合。");}
 public static void ValidateExecution(CreationConfig c){ValidateSave(c);if(Assess(c)!=CombinationState.Verified)throw new Exception("这组种族与职业尚未列入已核实白名单（客户端 "+c.ClientBuild+"）。可保存草稿或进行不提交预览，不能自动执行。");}
 public static void ValidateInstalledBuild(CreationConfig c){if(!File.Exists(c.GamePath))throw new Exception("游戏程序不存在。");if(FileVersionInfo.GetVersionInfo(c.GamePath).FileVersion!=c.ClientBuild)throw new Exception("实际游戏版本与组合规则版本不一致，请更新规则后再执行。");}
}
