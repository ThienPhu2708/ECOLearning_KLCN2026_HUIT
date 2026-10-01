using System;
using System.Globalization;
using System.IO;
using ECOLearning.Areas.Admin.Models;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tạo file PDF "Báo cáo doanh thu & vận hành hệ thống" từ SystemReportViewModel
    /// (cùng dữ liệu và bộ lọc kỳ báo cáo với trang Admin/SystemReport).
    /// Dùng PDFsharp-MigraDoc (GDI) với font Arial và mã hóa Unicode để hiển thị đúng tiếng Việt.
    /// </summary>
    public static class SystemReportPdfService
    {
        private const string FontName = "Arial";
        private static readonly CultureInfo Vi = new CultureInfo("vi-VN");
        private static readonly Color Primary = new Color(67, 76, 232);
        private static readonly Color HeaderBg = new Color(238, 240, 255);
        private static readonly Color BorderColor = new Color(203, 213, 225);
        private static readonly Color Muted = new Color(100, 116, 139);

        public static byte[] Build(SystemReportViewModel model)
        {
            var doc = new Document();
            doc.Info.Title = "Báo cáo hệ thống ECOLearning";
            doc.Info.Author = "ECOLearning Admin";

            var normal = doc.Styles["Normal"];
            normal.Font.Name = FontName;
            normal.Font.Size = 10;

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);

            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.Format.Font.Color = Muted;
            footer.AddText("ECOLearning — Báo cáo hệ thống • Trang ");
            footer.AddPageField();
            footer.AddText(" / ");
            footer.AddNumPagesField();

            // Tiêu đề
            var title = section.AddParagraph("BÁO CÁO DOANH THU & VẬN HÀNH HỆ THỐNG");
            title.Format.Font.Size = 16;
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Primary;
            title.Format.SpaceAfter = Unit.FromPoint(4);

            var sub = section.AddParagraph(string.Format("Kỳ báo cáo: {0}   •   Phạm vi: {1}   •   Ngày xuất: {2:dd/MM/yyyy HH:mm}", PeriodLabel(model.Period), model.FilterLabel, DateTime.Now));
            sub.Format.Font.Color = Muted;
            sub.Format.SpaceAfter = Unit.FromPoint(14);

            // 1. Chỉ số tổng quan
            AddHeading(section, "1. Chỉ số tổng quan");
            var kpi = CreateTable(section, 8.4, 8.4);
            AddKeyValueRow(kpi, "Doanh thu thuần", Money(model.TotalNetRevenue));
            AddKeyValueRow(kpi, "Số đơn hàng thành công", model.TotalOrders.ToString("N0", Vi));
            AddKeyValueRow(kpi, "Giá trị đơn trung bình", Money(model.AvgOrderValue));
            AddKeyValueRow(kpi, "Tổng số học viên", model.NewStudentsCount.ToString("N0", Vi));
            AddKeyValueRow(kpi, "Tăng trưởng so với kỳ trước", (model.GrowthPercentage >= 0 ? "+" : "") + model.GrowthPercentage.ToString("0.0", Vi) + "%");

            // 2. Bảng số liệu theo kỳ
            AddHeading(section, "2. Số liệu theo kỳ (" + PeriodLabel(model.Period) + ")");
            var period = CreateTable(section, 4.2, 3.4, 2.2, 3.0, 2.0, 2.0);
            AddHeaderRow(period, "Giai đoạn", "Doanh thu thuần", "Số đơn", "Giá trị đơn TB", "Học viên", "Tăng trưởng");
            if (model.PeriodReports.Count == 0)
            {
                AddEmptyRow(period, "Không có dữ liệu cho kỳ báo cáo đã chọn.");
            }
            foreach (var row in model.PeriodReports)
            {
                AddRow(period, row.GiaiDoan, Money(row.DoanhThu), row.SoDonHang.ToString("N0", Vi),
                    Money(row.GiaTriDonTrungBinh), row.HocVienMoi.ToString("N0", Vi), row.TangTruong);
            }

            // 3. Doanh thu & đơn hàng theo tháng (dữ liệu của biểu đồ trên trang web)
            AddHeading(section, "3. Doanh thu & đơn hoàn tất theo tháng (năm " + DateTime.Now.Year + ")");
            var monthly = CreateTable(section, 5.6, 5.6, 5.6);
            AddHeaderRow(monthly, "Tháng", "Doanh thu (triệu VNĐ)", "Số đơn hoàn tất");
            for (int i = 0; i < model.ChartLabels.Count; i++)
            {
                decimal revenue = i < model.RevenueChartData.Count ? model.RevenueChartData[i] : 0;
                int orders = i < model.OrderChartData.Count ? model.OrderChartData[i] : 0;
                AddRow(monthly, model.ChartLabels[i], revenue.ToString("N2", Vi), orders.ToString("N0", Vi));
            }

            // 4. Cơ cấu theo danh mục (hoặc theo khóa học khi lọc 1 danh mục)
            AddHeading(section, "4. Cơ cấu theo " + (model.CategorySegmentsByCourse ? "khóa học" : "danh mục")
                + (model.CategorySegmentsByRevenue ? " (theo doanh thu)" : " (chưa có doanh thu — theo số khóa học)"));
            var category = CreateTable(section, 11.2, 5.6);
            AddHeaderRow(category, model.CategorySegmentsByCourse ? "Khóa học" : "Danh mục", "Tỷ trọng");
            if (model.CategoryRevenueSegments.Count == 0)
            {
                AddEmptyRow(category, "Chưa có dữ liệu danh mục.");
            }
            foreach (var seg in model.CategoryRevenueSegments)
            {
                AddRow(category, seg.Label, seg.Value.ToString("0.0", Vi) + "%");
            }

            // 5. Doanh thu theo giáo viên
            AddHeading(section, "5. Doanh thu theo giáo viên");
            var teacherRevenue = CreateTable(section, 6.2, 2.4, 2.4, 2.2, 3.6);
            AddHeaderRow(teacherRevenue, "Giáo viên", "Số khóa học", "Đang bán", "Số đơn", "Doanh thu");
            if (model.TeacherRevenueRows.Count == 0)
            {
                AddEmptyRow(teacherRevenue, "Không có giáo viên nào trong phạm vi phân tích.");
            }
            foreach (var r in model.TeacherRevenueRows)
            {
                AddRow(teacherRevenue, r.TeacherName, r.CourseCount.ToString("N0", Vi), r.PublishedCourseCount.ToString("N0", Vi),
                    r.Orders.ToString("N0", Vi), Money(r.Revenue));
            }

            // 6. Giáo viên & khóa học
            AddHeading(section, "6. Giáo viên & khóa học");
            var teacher = CreateTable(section, 8.4, 8.4);
            AddKeyValueRow(teacher, "Tổng số giáo viên", model.TotalInstructors.ToString("N0", Vi));
            AddKeyValueRow(teacher, "Giáo viên có khóa học đang bán", model.InstructorsWithPublishedCourse.ToString("N0", Vi));
            AddKeyValueRow(teacher, "Hồ sơ đăng ký giáo viên chờ duyệt", model.PendingTeacherApplications.ToString("N0", Vi));

            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(6);
            var status = CreateTable(section, 8.4, 4.2, 4.2);
            AddHeaderRow(status, "Trạng thái khóa học", "Số khóa học", "Tỷ lệ");
            foreach (var s in model.CourseStatusStats)
            {
                AddRow(status, s.Label, s.Count.ToString("N0", Vi), s.Percent.ToString("0.0", Vi) + "%");
            }
            var totalRow = AddRow(status, "Tổng cộng", model.TotalCourses.ToString("N0", Vi), model.TotalCourses > 0 ? "100%" : "0%");
            totalRow.Format.Font.Bold = true;

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            using (var stream = new MemoryStream())
            {
                renderer.PdfDocument.Save(stream, false);
                return stream.ToArray();
            }
        }

        private static string PeriodLabel(string period)
        {
            switch (period)
            {
                case "month": return "Theo tháng";
                case "year": return "Theo năm";
                default: return "Theo quý";
            }
        }

        private static string Money(decimal value)
        {
            return value.ToString("N0", Vi) + " đ";
        }

        private static void AddHeading(Section section, string text)
        {
            var p = section.AddParagraph(text);
            p.Format.Font.Size = 12;
            p.Format.Font.Bold = true;
            p.Format.SpaceBefore = Unit.FromPoint(12);
            p.Format.SpaceAfter = Unit.FromPoint(6);
            p.Format.KeepWithNext = true;
        }

        private static Table CreateTable(Section section, params double[] columnWidthsCm)
        {
            var table = section.AddTable();
            table.Borders.Color = BorderColor;
            table.Borders.Width = 0.5;
            table.Format.Font.Size = 9;
            table.LeftPadding = Unit.FromPoint(5);
            table.RightPadding = Unit.FromPoint(5);
            table.TopPadding = Unit.FromPoint(3);
            table.BottomPadding = Unit.FromPoint(3);
            foreach (double width in columnWidthsCm)
            {
                table.AddColumn(Unit.FromCentimeter(width));
            }
            return table;
        }

        private static void AddHeaderRow(Table table, params string[] cells)
        {
            var row = AddRow(table, cells);
            row.HeadingFormat = true;
            row.Format.Font.Bold = true;
            row.Shading.Color = HeaderBg;
        }

        private static Row AddRow(Table table, params string[] cells)
        {
            var row = table.AddRow();
            for (int i = 0; i < cells.Length && i < table.Columns.Count; i++)
            {
                row.Cells[i].AddParagraph(cells[i] ?? "");
            }
            return row;
        }

        private static void AddKeyValueRow(Table table, string key, string value)
        {
            var row = AddRow(table, key, value);
            row.Cells[0].Shading.Color = HeaderBg;
            row.Cells[1].Format.Font.Bold = true;
        }

        private static void AddEmptyRow(Table table, string text)
        {
            var row = table.AddRow();
            row.Cells[0].MergeRight = table.Columns.Count - 1;
            var p = row.Cells[0].AddParagraph(text);
            p.Format.Font.Color = Muted;
            p.Format.Alignment = ParagraphAlignment.Center;
        }
    }
}
