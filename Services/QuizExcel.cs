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
        {
            using(var output=new MemoryStream()) {
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)) {
                    Entry(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                    Entry(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    Entry(zip,"xl/workbook.xml","<workbook xmlns=\""+S+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"DuLieu\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                    Entry(zip,"xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
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
                    Entry(zip,"xl/worksheets/sheet1.xml",sheet.ToString());
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
