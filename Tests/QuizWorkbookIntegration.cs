using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;
class QuizWorkbookIntegration {
 static int checks;
 static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);checks++;}
 static void Reject(Action action,string text){try{action();}catch(InvalidOperationException){Check(true,text);return;}throw new Exception("Expected rejection: "+text);}
 static QuizWorkbookImport Read(byte[] data,string course,out List<string> errors){errors=new List<string>();using(var stream=new MemoryStream(data))return QuizWorkbookService.Read(stream,course,errors);}
 static byte[] Matrix(string name,string easy="1",string required="Có"){return QuizExcel.WriteWorkbook(new[]{new QuizExcelSheet{Name="Phân bổ đề",Headers=QuizWorkbookService.MatrixHeaders,Rows=new List<string[]>{new[]{name,easy,"0","0","75",required}}}});}
 static int Main(){string course="QW"+Guid.NewGuid().ToString("N").Substring(0,10);int teacher=0;
 using(var db=new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())try {
  var user=new NguoiDung{hoTen="Workbook fixture teacher",email=course+"@test.invalid",matKhau="test-only",vaiTro="GiaoVien",trangThai=true};db.NguoiDung.Add(user);db.SaveChanges();teacher=user.maNguoiDung;
  db.KhoaHoc.Add(new KhoaHoc{maKhoaHoc=course,tenKhoaHoc="Workbook import fixture",maGiaoVien=teacher,maDanhMuc=db.DanhMuc.First().maDanhMuc,giaBan=0,trangThai="Bản nháp"});db.SaveChanges();
  var importer=new QuizWorkbookService(db);var quiz=new QuizService(db);var skills=new QuizAssessmentService(db);
  List<string> errors;var template=QuizWorkbookService.Template();var bundle=Read(template,course,out errors);
  Check(errors.Count==0&&bundle.Questions.Count==1&&bundle.Rules.Count==1,"two-sheet template round trips with question skill and allocation");
  using(var stream=new MemoryStream(template))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){XNamespace r="http://schemas.openxmlformats.org/package/2006/relationships";var xml=XDocument.Load(zip.GetEntry("xl/_rels/workbook.xml.rels").Open());Check(xml.Descendants(r+"Relationship").All(x=>(string)x.Attribute("Type")=="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),"worksheet relationship types are valid OpenXML URIs");}
  var sheets=QuizExcel.ReadSheets(new MemoryStream(template));sheets.Reverse();var reversed=Read(QuizExcel.WriteWorkbook(sheets),course,out errors);Check(errors.Count==0&&reversed.Rules.Count==1&&reversed.Questions.Count==1,"sheet order does not affect import");
  importer.Validate(bundle);Check(skills.Skills(course).Count==0&&quiz.Questions(course).Count==0,"preview validation does not write any data");
  Reject(()=>importer.Apply(bundle,teacher+1000000),"another user cannot import course data");
  importer.Apply(bundle,teacher);var config=quiz.Config(course);var question=quiz.Questions(course).Single();
  Check(skills.Skills(course).Count==1&&question.SkillId.HasValue,"confirmation creates skill and attaches imported question");
  Check(config.UseMatrix&&!config.Published&&config.Total==1,"confirmation saves allocation as unpublished draft");
  Check(skills.Rules(config).Single().SkillId==question.SkillId&&skills.Rules(config).Single().PassPercent==100,"allocation resolves skill ID and threshold");
  config.Minutes=17;config.MaxAttempts=4;config.PassPercent=65;config.Published=true;quiz.SaveConfig(config);
  var onlyMatrix=Read(Matrix(" phép tính cơ bản "),course,out errors);Check(errors.Count==0&&onlyMatrix.Questions.Count==0&&onlyMatrix.Rules.Count==1,"allocation-only workbook accepted");
  importer.Validate(onlyMatrix);importer.Apply(onlyMatrix,teacher);config=quiz.Config(course);
  Check(quiz.Questions(course).Count==1&&skills.Skills(course).Count==1,"allocation-only import reuses existing bank and case-insensitive skill");
  Check(config.Minutes==17&&config.MaxAttempts==4&&config.PassPercent==65&&!config.Published&&config.RandomSelection,"existing timing attempts and global pass preserved while publication requires confirmation");
  Check(skills.Rules(config).Single().PassPercent==75,"new allocation replaces previous allocation");
  var insufficient=Read(Matrix("Phép tính cơ bản","2"),course,out errors);Reject(()=>importer.Validate(insufficient),"preview rejects insufficient skill difficulty pool");
  // Change availability after a valid preview; confirm must validate again.
  quiz.Archive(course,question.Id);Reject(()=>importer.Apply(onlyMatrix,teacher),"confirm rechecks availability after bank changes");
  var legacyRow=new[]{"Topic","1","Legacy question","A","B","C","D","B","Explanation","1"};
  var legacy=Read(QuizExcel.Write(QuizExcel.Headers,new[]{legacyRow}),course,out errors);Check(errors.Count==0&&legacy.QuestionSkills.Single()=="","legacy 10-column workbook remains compatible");
  importer.Apply(legacy,teacher);Check(quiz.Questions(course).Count==2,"legacy question-only import succeeds");
  // Failure after inserting a new skill/question must roll everything back.
  var rollback=Read(template,course,out errors);rollback.QuestionSkills[0]="Rollback only";rollback.Rules[0].Name="Rollback only";rollback.Questions[0].Options[1]=rollback.Questions[0].Options[0];
  Reject(()=>importer.Apply(rollback,teacher),"invalid question fails confirmation");Check(!skills.Skills(course).Any(x=>x.Name=="Rollback only")&&quiz.Questions(course).Count==2,"transaction rolls back new skills and questions together");
  var invalid=Read(Matrix("Syntax","-1"),course,out errors);Check(errors.Any(x=>x.Contains("Phân bổ đề")&&x.Contains("2")),"matrix errors identify sheet and original row");
  Read(Matrix("Syntax","1","Maybe"),course,out errors);Check(errors.Count==1,"invalid required flag rejected");
  var duplicate=QuizExcel.ReadSheets(new MemoryStream(Matrix("Syntax")));duplicate[0].Rows.Add(duplicate[0].Rows[0]);Read(QuizExcel.WriteWorkbook(duplicate),course,out errors);Check(errors.Any(x=>x.Contains("lặp")),"duplicate allocation skill rejected");
  var wrongHeader=QuizExcel.ReadSheets(new MemoryStream(template));wrongHeader[0].Headers[0]="Wrong";Read(QuizExcel.WriteWorkbook(wrongHeader),course,out errors);Check(errors.Any(x=>x.Contains("tiêu đề")),"wrong headers rejected before confirmation");
  var emptyQuestion=QuizExcel.ReadSheets(new MemoryStream(template));emptyQuestion[0].Rows.Clear();emptyQuestion[1].Rows.Clear();Read(QuizExcel.WriteWorkbook(emptyQuestion),course,out errors);Check(errors.Count>0,"empty workbook cannot confirm");
  byte[] formula=QuizExcel.WriteWorkbook(new[]{new QuizExcelSheet{Name="Phân bổ đề",Headers=QuizWorkbookService.MatrixHeaders,Rows=new List<string[]>{new[]{"Syntax","1","0","0","75","Có"}}}});
  using(var stream=new MemoryStream()){stream.Write(formula,0,formula.Length);using(var zip=new ZipArchive(stream,ZipArchiveMode.Update,true)){var entry=zip.GetEntry("xl/worksheets/sheet1.xml");XDocument xml;using(var input=entry.Open())xml=XDocument.Load(input);XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";xml.Descendants(s+"c").First(x=>(string)x.Attribute("r")=="B2").Add(new XElement(s+"f","1+1"));entry.Delete();using(var writer=new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()))writer.Write(xml.ToString());}formula=stream.ToArray();}
  Reject(()=>{List<string> es;Read(formula,course,out es);},"formula cells rejected in allocation sheet");
  using(var legacyStream=new MemoryStream(QuizExcel.Write(QuizExcel.Headers,new[]{legacyRow}))){var es=new List<string>();Check(QuizExcel.Read(legacyStream,course,es).Count==1&&es.Count==0,"original reader and single-sheet exporter still work");}
  File.WriteAllBytes(Path.Combine("obj","MauQuizHaiSheet.xlsx"),template);
  Console.WriteLine(checks+" workbook checks passed.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}finally{
  db.Database.ExecuteSqlCommand("DELETE dbo.QuizConfig WHERE CourseId=@p0; DELETE dbo.QuizQuestion WHERE CourseId=@p0; DELETE dbo.QuizSkill WHERE CourseId=@p0; DELETE dbo.KhoaHoc WHERE maKhoaHoc=@p0 AND tenKhoaHoc=N'Workbook import fixture';",course);
  if(teacher>0)db.Database.ExecuteSqlCommand("DELETE dbo.NguoiDung WHERE maNguoiDung=@p0 AND email=@p1",teacher,course+"@test.invalid");Console.WriteLine("Workbook fixtures removed.");
 }
 }
}
