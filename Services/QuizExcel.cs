using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    // Small, dependency-free Open XML reader/writer for the quiz's tabular .xlsx format.
    public static class QuizExcel
    {
        private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static void Entry(ZipArchive zip,string path,string content) {using(var writer=new StreamWriter(zip.CreateEntry(path).Open()))writer.Write(content);}
        public static byte[] Write(string[] headers,IEnumerable<string[]> records)
        { return WriteWorkbook(new[]{new QuizExcelSheet{Name="DuLieu",Headers=headers,Rows=records.ToList()}}); }
        public static byte[] WriteWorkbook(IList<QuizExcelSheet> sheets)
        {
            using(var output=new MemoryStream()) {
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)) {
                    XNamespace package="http://schemas.openxmlformats.org/package/2006/content-types";
                    XNamespace relations="http://schemas.openxmlformats.org/package/2006/relationships";
                    XNamespace documentRelations="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                    Entry(zip,"[Content_Types].xml",new XElement(package+"Types",new XElement(package+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(package+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),new XElement(package+"Override",new XAttribute("PartName","/xl/workbook.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),sheets.Select((x,i)=>new XElement(package+"Override",new XAttribute("PartName","/xl/worksheets/sheet"+(i+1)+".xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))).ToString());
                    Entry(zip,"_rels/.rels",new XElement(relations+"Relationships",new XElement(relations+"Relationship",new XAttribute("Id","rId1"),new XAttribute("Type",documentRelations.NamespaceName+"/officeDocument"),new XAttribute("Target","xl/workbook.xml"))).ToString());
                    Entry(zip,"xl/workbook.xml",new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",documentRelations),new XElement(S+"sheets",sheets.Select((x,i)=>new XElement(S+"sheet",new XAttribute("name",x.Name),new XAttribute("sheetId",i+1),new XAttribute(documentRelations+"id","rId"+(i+1)))))).ToString());
                    Entry(zip,"xl/_rels/workbook.xml.rels",new XElement(relations+"Relationships",sheets.Select((x,i)=>new XElement(relations+"Relationship",new XAttribute("Id","rId"+(i+1)),new XAttribute("Type",documentRelations.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet"+(i+1)+".xml")))).ToString());
                    for(int sheetIndex=0;sheetIndex<sheets.Count;sheetIndex++) {
                    var headers=sheets[sheetIndex].Headers;var records=sheets[sheetIndex].Rows;
                    int row=0;
                    var data=new XElement(S+"sheetData");
                    foreach(var values in new[]{headers}.Concat(records)) {
                        row++;var r=new XElement(S+"row",new XAttribute("r",row));
                        for(int i=0;i<values.Length;i++) {
                            string value=values[i] ?? "";
                            // Only declared numeric columns become numbers; user text is never a formula.
                            decimal number;
                            bool numeric=new[]{"MucDo","ChoDaoDapAn","Tiến độ %","Số lượt","Lượt được cấp thêm","Số câu đúng","Tổng câu","Điểm cao nhất /10","Điểm /10","Lượt làm"}.Contains(headers[i]);
                            if(row>1 && numeric && decimal.TryParse(value,NumberStyles.Number,CultureInfo.CurrentCulture,out number))
                                r.Add(new XElement(S+"c",new XAttribute("r",Column(i)+row),new XElement(S+"v",number.ToString(CultureInfo.InvariantCulture))));
                            else r.Add(new XElement(S+"c",new XAttribute("r",Column(i)+row),new XAttribute("t","inlineStr"),new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value))));
                        }
                        data.Add(r);
                    }
                    var sheet=new XElement(S+"worksheet",new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XElement(S+"pane",new XAttribute("ySplit",1),new XAttribute("topLeftCell","A2"),new XAttribute("state","frozen")))),
                        new XElement(S+"cols",Enumerable.Range(1,headers.Length).Select(i=>new XElement(S+"col",new XAttribute("min",i),new XAttribute("max",i),new XAttribute("width",i==3?60:25),new XAttribute("customWidth",1)))),data,
                        new XElement(S+"autoFilter",new XAttribute("ref","A1:"+Column(headers.Length-1)+row)));
                    Entry(zip,"xl/worksheets/sheet"+(sheetIndex+1)+".xml",sheet.ToString());
                    }
                }
                return output.ToArray();
            }
        }
        private static string Column(int i) {string s="";do{s=(char)('A'+i%26)+s;i=i/26-1;}while(i>=0);return s;}
        private static XDocument Xml(ZipArchive zip,string path)
        {
            var entry=zip.GetEntry(path);if(entry==null)throw new InvalidOperationException("File Excel thiếu thành phần "+path);
            using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20000000}))return XDocument.Load(reader);
        }
        public static List<QuizExcelSheet> ReadSheets(Stream stream) {
            var result=new List<QuizExcelSheet>();
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)) {
                if(zip.Entries.Count>100 || zip.Entries.Sum(e=>e.Length)>20000000)throw new InvalidOperationException("File Excel quá lớn.");
                var strings=zip.GetEntry("xl/sharedStrings.xml")==null?new string[0]:Xml(zip,"xl/sharedStrings.xml").Descendants(S+"si").Select(si=>string.Concat(si.Descendants(S+"t").Select(t=>t.Value))).ToArray();
                XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var relationships=Xml(zip,"xl/_rels/workbook.xml.rels").Root.Elements().ToList();
                foreach(var sheet in Xml(zip,"xl/workbook.xml").Descendants(S+"sheet")) {
                    string name=(string)sheet.Attribute("name");
                    var link=relationships.SingleOrDefault(x=>(string)x.Attribute("Id")== (string)sheet.Attribute(rel+"id"));
                    string target=(string)link?.Attribute("Target");
                    if(string.IsNullOrEmpty(target)||(string)link.Attribute("TargetMode")=="External")throw new InvalidOperationException("Sheet không hợp lệ.");
                    string path=target.StartsWith("/")?target.TrimStart('/') : "xl/"+target;
                    if(path.Contains("..")||!path.StartsWith("xl/worksheets/",StringComparison.Ordinal))throw new InvalidOperationException("Đường dẫn sheet không hợp lệ.");
                    var data=Xml(zip,path).Descendants(S+"sheetData").Elements(S+"row").ToList();
                    if(data.Count>501)throw new InvalidOperationException("Sheet "+name+": tối đa 500 dòng dữ liệu.");
                    var table=new QuizExcelSheet{Name=name,Rows=new List<string[]>(),RowNumbers=new List<int>()};
                    foreach(var row in data) {
                        var values=new string[11];
                        foreach(var cell in row.Elements(S+"c")) {
                            if(cell.Element(S+"f")!=null)throw new InvalidOperationException("Sheet "+name+", dòng "+(string)row.Attribute("r")+": không hỗ trợ công thức.");
                            int column=0;foreach(char ch in ((string)cell.Attribute("r")??"").TakeWhile(char.IsLetter))column=column*26+(char.ToUpperInvariant(ch)-'A'+1);
                            string type=(string)cell.Attribute("t"),value=cell.Element(S+"v")?.Value??"";
                            int index;if(type=="s"){if(!int.TryParse(value,out index)||index<0||index>=strings.Length)throw new InvalidOperationException("Ô văn bản không hợp lệ.");value=strings[index];}
                            else if(type=="inlineStr")value=string.Concat(cell.Descendants(S+"t").Select(x=>x.Value));
                            if(column<1||column>11){if(!string.IsNullOrWhiteSpace(value))throw new InvalidOperationException("Sheet "+name+": có cột ngoài mẫu.");continue;}
                            values[column-1]=value;
                        }
                        if(values.All(string.IsNullOrWhiteSpace))continue;
                        int number;if(!int.TryParse((string)row.Attribute("r"),out number))number=table.Rows.Count+2;
                        if(table.Headers==null)table.Headers=values;else{table.Rows.Add(values);table.RowNumbers.Add(number);}
                    }
                    result.Add(table);
                }
            }
            return result;
        }
        public static readonly string[] Headers={"ChuDe","MucDo","CauHoi","DapAnA","DapAnB","DapAnC","DapAnD","DapAnDung","GiaiThich","ChoDaoDapAn"};
        public static List<QuizQuestion> Read(Stream stream,string course,List<string> errors)
        {
            var result=new List<QuizQuestion>();
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)) {
                if(zip.Entries.Count>100 || zip.Entries.Sum(e=>e.Length)>20000000)throw new InvalidOperationException("File Excel quá lớn.");
                var strings=zip.GetEntry("xl/sharedStrings.xml")==null?new string[0]:Xml(zip,"xl/sharedStrings.xml").Descendants(S+"si").Select(si=>string.Concat(si.Descendants(S+"t").Select(t=>t.Value))).ToArray();
                var workbook=Xml(zip,"xl/workbook.xml");XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var first=workbook.Descendants(S+"sheet").FirstOrDefault();if(first==null)throw new InvalidOperationException("File không có sheet.");
                string rid=(string)first.Attribute(rel+"id");
                var relationship=Xml(zip,"xl/_rels/workbook.xml.rels").Root.Elements().SingleOrDefault(e=>(string)e.Attribute("Id")==rid);
                string target=(string)relationship?.Attribute("Target");
                if(string.IsNullOrEmpty(target) || (string)relationship.Attribute("TargetMode")=="External")throw new InvalidOperationException("Sheet không hợp lệ.");
                string path=target.StartsWith("/")?target.TrimStart('/') : "xl/"+target;
                if(path.Contains("..") || !path.StartsWith("xl/worksheets/",StringComparison.Ordinal))throw new InvalidOperationException("Đường dẫn sheet không hợp lệ.");
                var rows=Xml(zip,path).Descendants(S+"sheetData").Elements(S+"row").ToList();
                if(rows.Count>501)throw new InvalidOperationException("Mỗi lần nhập tối đa 500 câu hỏi.");
                bool header=true;
                foreach(var row in rows) {
                    var values=new string[10];
                    try {
                        foreach(var cell in row.Elements(S+"c")) {
                            if(cell.Element(S+"f")!=null)throw new InvalidOperationException("Không hỗ trợ ô chứa công thức.");
                            string reference=(string)cell.Attribute("r") ?? "";int col=0;
                            foreach(char ch in reference.TakeWhile(char.IsLetter))col=col*26+(char.ToUpperInvariant(ch)-'A'+1);
                            if(col<1 || col>10)continue;
                            string type=(string)cell.Attribute("t"),value=cell.Element(S+"v")?.Value ?? "";
                            if(type=="s")value=strings[int.Parse(value,CultureInfo.InvariantCulture)];
                            else if(type=="inlineStr")value=string.Concat(cell.Descendants(S+"t").Select(t=>t.Value));
                            values[col-1]=value;
                        }
                        if(values.All(string.IsNullOrWhiteSpace))continue;
                        if(header) {header=false;if(!Headers.SequenceEqual(values))throw new InvalidOperationException("Tiêu đề cột không khớp. Hãy tải file mẫu.");continue;}
                        int difficulty;
                        if(!int.TryParse(values[1],out difficulty))throw new InvalidOperationException("MucDo phải là 1 (Dễ), 2 (Trung bình), 3 (Khó).");
                        string correct=(values[7] ?? "").Trim().ToUpperInvariant();
                        if(correct.Length!=1 || correct[0]<'A' || correct[0]>'D')throw new InvalidOperationException("DapAnDung phải là A, B, C hoặc D.");
                        string shuffle=(values[9] ?? "").Trim();
                        if(shuffle!="0" && shuffle!="1")throw new InvalidOperationException("ChoDaoDapAn phải là 0 hoặc 1.");
                        var q=new QuizQuestion{CourseId=course,Topic=values[0],Difficulty=difficulty,Text=values[2],Options=values.Skip(3).Take(4).ToArray(),CorrectOption=correct[0]-'A',Explanation=values[8],ShuffleOptions=shuffle=="1"};
                        QuizService.ValidateQuestion(q);result.Add(q);
                    } catch(Exception ex) when(ex is InvalidOperationException || ex is FormatException || ex is IndexOutOfRangeException) {
                        errors.Add("Dòng "+(string)row.Attribute("r")+": "+ex.Message);
                    }
                }
                if(result.Count==0 && errors.Count==0)errors.Add("File chưa có câu hỏi.");
            }
            return result;
        }
    }
}

namespace ECOLearning.Services {
 public class QuizExcelSheet {public string Name;public string[] Headers;public List<string[]> Rows;public List<int> RowNumbers;}
 public class QuizWorkbookImport {
  public string CourseId;public List<QuizQuestion> Questions=new List<QuizQuestion>();public List<string> QuestionSkills=new List<string>();public List<QuizSkillRule> Rules=new List<QuizSkillRule>();public List<int> RuleRows=new List<int>();
 }
 public class QuizWorkbookService {
  readonly ECOLearning.Models.QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public QuizWorkbookService(ECOLearning.Models.QL_HETHONG_CUNGCAP_KHOAHOCEntities1 context){db=context;}
  public static readonly string[] QuestionHeaders=QuizExcel.Headers.Concat(new[]{"KyNang"}).ToArray();
  public static readonly string[] MatrixHeaders={"KyNang","CauDe","CauTrungBinh","CauKho","MucDatPhanTram","BatBuocDat"};
  static string Clean(string value){return (value??"").Trim();}
  static bool Equal(string a,string b){return string.Equals(Clean(a),Clean(b),StringComparison.OrdinalIgnoreCase);}
  static string Name(string value){value=Clean(value);if(value.Length<1||value.Length>120)throw new InvalidOperationException("KyNang cần từ 1 đến 120 ký tự.");return value;}
  static int Number(string value,int min,int max,string column){int n;if(!int.TryParse(value,out n)||n<min||n>max)throw new InvalidOperationException(column+" phải là số nguyên từ "+min+" đến "+max+".");return n;}
  public static byte[] Template(){return QuizExcel.WriteWorkbook(new[]{
   new QuizExcelSheet{Name="Câu hỏi",Headers=QuestionHeaders,Rows=new List<string[]>{new[]{"Chương 1","1","2 + 2 bằng bao nhiêu?","3","4","5","6","B","2 + 2 = 4","1","Phép tính cơ bản"}}},
   new QuizExcelSheet{Name="Phân bổ đề",Headers=MatrixHeaders,Rows=new List<string[]>{new[]{"Phép tính cơ bản","1","0","0","100","Có"}}}});}
  public static QuizWorkbookImport Read(Stream stream,string course,List<string> errors) {
   var bundle=new QuizWorkbookImport{CourseId=course};var sheets=QuizExcel.ReadSheets(stream);
   if(sheets.Count==0){errors.Add("File không có sheet.");return bundle;}
   bool questionsSeen=false,matrixSeen=false;
   foreach(var sheet in sheets) {
    bool questions=Equal(sheet.Name,"Câu hỏi")||Equal(sheet.Name,"CauHoi")||(sheets.Count==1&&sheet.Headers!=null&&QuizExcel.Headers.SequenceEqual(sheet.Headers.Take(10)));
    bool matrix=Equal(sheet.Name,"Phân bổ đề")||Equal(sheet.Name,"PhanBoDe");
    if(!questions&&!matrix){errors.Add("Sheet "+sheet.Name+": tên sheet không khớp mẫu (Câu hỏi / Phân bổ đề).");continue;}
    if((questions&&questionsSeen)||(matrix&&matrixSeen)){errors.Add("Sheet bị lặp: "+sheet.Name);continue;}
    if(questions)questionsSeen=true;else matrixSeen=true;
    if(sheet.Headers==null){continue;}
    var expected=questions?QuestionHeaders:MatrixHeaders;
    bool legacy=questions&&QuizExcel.Headers.SequenceEqual(sheet.Headers.Take(10))&&string.IsNullOrWhiteSpace(sheet.Headers[10]);
    if(!legacy&&(!expected.SequenceEqual(sheet.Headers.Take(expected.Length))||sheet.Headers.Skip(expected.Length).Any(x=>!string.IsNullOrWhiteSpace(x)))){errors.Add("Sheet "+sheet.Name+": tiêu đề cột không khớp, hãy tải file mẫu.");continue;}
    for(int i=0;i<sheet.Rows.Count;i++) {var v=sheet.Rows[i];string location="Sheet "+sheet.Name+", dòng "+sheet.RowNumbers[i]+": ";
     try {
      if(questions) {
       string correct=Clean(v[7]).ToUpperInvariant();if(correct.Length!=1||correct[0]<'A'||correct[0]>'D')throw new InvalidOperationException("DapAnDung phải là A, B, C hoặc D.");
       string shuffle=Clean(v[9]);if(shuffle!="0"&&shuffle!="1")throw new InvalidOperationException("ChoDaoDapAn phải là 0 hoặc 1.");
       var q=new QuizQuestion{CourseId=course,Topic=v[0],Difficulty=Number(v[1],1,3,"MucDo"),Text=v[2],Options=v.Skip(3).Take(4).ToArray(),CorrectOption=correct[0]-'A',Explanation=v[8],ShuffleOptions=shuffle=="1"};
       QuizService.ValidateQuestion(q);string skill=Clean(v[10]);if(skill.Length>0)skill=Name(skill);bundle.Questions.Add(q);bundle.QuestionSkills.Add(skill);
      } else {
       string name=Name(v[0]);if(bundle.Rules.Any(r=>Equal(r.Name,name)))throw new InvalidOperationException("Kỹ năng bị lặp trong phân bổ đề.");
       string required=Clean(v[5]);bool yes=Equal(required,"Có")||Equal(required,"Co")||required=="1";bool no=Equal(required,"Không")||Equal(required,"Khong")||required=="0";
       if(!yes&&!no)throw new InvalidOperationException("BatBuocDat phải là Có/Không hoặc 1/0.");
       var rule=new QuizSkillRule{Name=name,Easy=Number(v[1],0,100,"CauDe"),Medium=Number(v[2],0,100,"CauTrungBinh"),Hard=Number(v[3],0,100,"CauKho"),PassPercent=Number(v[4],1,100,"MucDatPhanTram"),Required=yes};
       if(rule.Total<1||rule.Total>100)throw new InvalidOperationException("Mỗi kỹ năng cần từ 1 đến 100 câu.");bundle.Rules.Add(rule);bundle.RuleRows.Add(sheet.RowNumbers[i]);
      }
     }catch(InvalidOperationException ex){errors.Add(location+ex.Message);}
    }
   }
   if(bundle.Questions.Count==0&&bundle.Rules.Count==0&&errors.Count==0)errors.Add("File chưa có câu hỏi hoặc phân bổ đề.");
   if(bundle.Rules.Sum(r=>r.Total)>100)errors.Add("Sheet Phân bổ đề: tổng đề không được quá 100 câu.");
   return bundle;
  }
  public void Validate(QuizWorkbookImport bundle) {
   if(bundle.Questions.Count!=bundle.QuestionSkills.Count)throw new InvalidOperationException("Dữ liệu xem trước không hợp lệ.");
   if(bundle.Rules.Count==0&&bundle.QuestionSkills.All(string.IsNullOrEmpty))return;
   var assessment=new QuizAssessmentService(db);if(!assessment.Installed())throw new InvalidOperationException("Cần chạy migration 027 trước khi nhập kỹ năng.");
   var skills=assessment.Skills(bundle.CourseId);var existing=new QuizService(db).Questions(bundle.CourseId,true);
   foreach(var name in bundle.QuestionSkills.Concat(bundle.Rules.Select(r=>r.Name)).Where(n=>!string.IsNullOrEmpty(n)))
    if(skills.Any(s=>Equal(s.Name,name)&&!s.Active))throw new InvalidOperationException("Kỹ năng "+name+" đã ngừng sử dụng.");
   foreach(var rule in bundle.Rules) {
    int ruleIndex=bundle.Rules.IndexOf(rule);string row=ruleIndex<bundle.RuleRows.Count?", dòng "+bundle.RuleRows[ruleIndex]:"";
    for(int d=1;d<=3;d++) {
     int available=existing.Count(q=>q.Difficulty==d&&skills.Any(s=>s.Id==q.SkillId&&s.Active&&Equal(s.Name,rule.Name)))+bundle.Questions.Where((q,i)=>q.Difficulty==d&&Equal(bundle.QuestionSkills[i],rule.Name)).Count();
     int needed=d==1?rule.Easy:d==2?rule.Medium:rule.Hard;
     if(available<needed)throw new InvalidOperationException("Sheet Phân bổ đề"+row+", kỹ năng "+rule.Name+": cần "+needed+" câu mức "+d+" nhưng ngân hàng và file chỉ có "+available+" câu.");
    }
   }
  }
  public void Apply(QuizWorkbookImport bundle,int teacher) {
   var service=new QuizService(db);if(!service.Owns(bundle.CourseId,teacher))throw new InvalidOperationException("Không có quyền nhập vào khóa học này.");
   using(var tx=db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable)) {
    // Serialize imports/configuration for this course without modifying existing attempts.
    db.Database.ExecuteSqlCommand("DECLARE @r int; EXEC @r=sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'Quiz import lock timeout',1;","QuizImport_"+bundle.CourseId);
    Validate(bundle);var assessment=new QuizAssessmentService(db);var skills=assessment.Skills(bundle.CourseId);
    foreach(var name in bundle.QuestionSkills.Concat(bundle.Rules.Select(r=>r.Name)).Where(n=>!string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase)) {
     if(!skills.Any(s=>Equal(s.Name,name))){assessment.AddSkill(bundle.CourseId,teacher,name);skills=assessment.Skills(bundle.CourseId);}
    }
    for(int i=0;i<bundle.Questions.Count;i++){var q=bundle.Questions[i];q.Id=0;q.CourseId=bundle.CourseId;q.SkillId=string.IsNullOrEmpty(bundle.QuestionSkills[i])?(int?)null:skills.Single(s=>Equal(s.Name,bundle.QuestionSkills[i])).Id;service.SaveQuestion(q);}
    if(bundle.Rules.Any()) {
     var config=service.Config(bundle.CourseId)??new QuizConfig{CourseId=bundle.CourseId};
     config.UseMatrix=true;config.Published=false;config.RandomSelection=true;config.SelectedIdsJson="[]";config.Topic=null;
     config.SkillRules=bundle.Rules.Select(r=>new QuizSkillRule{SkillId=skills.Single(s=>Equal(s.Name,r.Name)).Id,Name=r.Name,Easy=r.Easy,Medium=r.Medium,Hard=r.Hard,PassPercent=r.PassPercent,Required=r.Required}).ToList();
     service.ValidateConfig(config);service.SaveConfig(config);
    }
    tx.Commit();
   }
  }
 }
}
