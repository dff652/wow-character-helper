using System;
using System.Linq;
namespace CharacterVision {
 static class DiagnosticChecks {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  public static void Run(){
   var empty=Recognition.Analyze(new OcrResult{Text="",Words=new Word[0]},1920,1080);Check(empty.Page=="未知页面","Blank page must stay unknown");
   var menu=Recognition.Analyze(new OcrResult{Text="联盟 部落 牛头人 德鲁伊",Words=new[]{new Word{Text="联盟",X=30,Y=80,Line=0},new Word{Text="部落",X=280,Y=80,Line=1}}},1920,1080);Check(menu.Candidates.Contains("右侧独立标题 / 阵营：未确认"),"Menu options are not selected faction evidence");
   var names=Recognition.Analyze(new OcrResult{Text="肤色",Words=new[]{new Word{Text="测试主名",X=750,Y=45,Line=0},new Word{Text="新次名",X=1100,Y=45,Line=1}}},1920,1080);Check(names.Candidates.Contains("主名位置候选：测试主名")&&names.Candidates.Contains("次名位置候选：新次名"),"Names must come from OCR rather than fixed templates");
   var dialog=Recognition.Analyze(new OcrResult{Text="肤色 确 定",Words=new[]{new Word{Text="确",X=900,Y=540,Line=0},new Word{Text="定",X=925,Y=540,Line=0}}},1920,1080);Check(dialog.Page=="疑似对话框，正文尚未确认","Unread dialog must take precedence over background page");
   var mention=Recognition.Analyze(new OcrResult{Text="说明：由于阵营人口不平衡，目前无法选择该阵营",Words=new Word[0]},1920,1080);Check(mention.Page!="疑似阵营限制提示","Mention in ordinary text must not be treated as blocking dialog");
  }
 }
}
