using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// Giá khuyến mãi từng khóa học (KhoaHoc.giaKhuyenMai). Giáo viên chỉ nhập giá gốc; giá giảm do Marketing
    /// quản lý tập trung ở đây, để không có hai nguồn giảm giá độc lập. Coupon (PromotionController) áp dụng trên giá bán hiện tại.
    /// </summary>
    public class CoursePriceController : MarketingBaseController
    {
        // GET: Marketing/CoursePrice
        public ActionResult Index(string search = null, string loc = null)
        {
            var query = db.KhoaHoc.Where(k => k.giaBan > 0);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim();
                query = query.Where(k => k.tenKhoaHoc.Contains(kw) || k.maKhoaHoc.Contains(kw));
            }
            var courses = query
                .Select(k => new CoursePriceRow
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    TenDanhMuc = k.DanhMuc.tenDanhMuc,
                    GiaoVien = k.NguoiDung.hoTen,
                    TrangThai = k.trangThai,
                    GiaBan = k.giaBan,
                    GiaKhuyenMai = k.giaKhuyenMai
                })
                .OrderBy(k => k.TenKhoaHoc)
                .ToList();

            if (loc == "dangGiam") courses = courses.Where(c => c.DangGiam).ToList();
            else if (loc == "khongHopLe") courses = courses.Where(c => c.KhongHopLe).ToList();

            ViewBag.CurrentSearch = search;
            ViewBag.Loc = loc;
            ViewBag.BreadcrumbCurrent = "Giá khuyến mãi khóa học";
            return View(courses);
        }

        // POST: Marketing/CoursePrice/Update — giaKhuyenMai trống = bỏ giá giảm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Update(string id, string giaKhuyenMai, string returnUrl = null)
        {
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id);
            if (course == null) return HttpNotFound();

            string raw = (giaKhuyenMai ?? "").Trim().Replace(".", "").Replace(",", "").Replace(" ", "");
            if (raw.Length == 0)
            {
                course.giaKhuyenMai = null;
                db.SaveChanges();
                TempData["Success"] = "Đã bỏ giá khuyến mãi của khóa \"" + course.tenKhoaHoc + "\". Khóa bán theo giá gốc " + course.giaBan.ToString("N0") + " đ.";
            }
            else
            {
                decimal gia;
                if (!decimal.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out gia))
                {
                    TempData["Error"] = "Giá khuyến mãi phải là số nguyên (đồng).";
                }
                else if (gia <= 0 || gia >= course.giaBan)
                {
                    TempData["Error"] = "Giá khuyến mãi của \"" + course.tenKhoaHoc + "\" phải lớn hơn 0 và nhỏ hơn giá gốc " + course.giaBan.ToString("N0") + " đ.";
                }
                else
                {
                    course.giaKhuyenMai = gia;
                    db.SaveChanges();
                    TempData["Success"] = "Đã đặt giá khuyến mãi " + gia.ToString("N0") + " đ cho khóa \"" + course.tenKhoaHoc + "\".";
                }
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index");
        }
    }

    public class CoursePriceRow
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TenDanhMuc { get; set; }
        public string GiaoVien { get; set; }
        public string TrangThai { get; set; }
        public decimal GiaBan { get; set; }
        public decimal? GiaKhuyenMai { get; set; }

        public bool DangGiam => GiaKhuyenMai.HasValue && GiaKhuyenMai.Value > 0 && GiaKhuyenMai.Value < GiaBan;

        /// <summary>Có giá giảm nhưng không dùng được (0 hoặc không nhỏ hơn giá gốc): hệ thống đang bán theo giá gốc.</summary>
        public bool KhongHopLe => GiaKhuyenMai.HasValue && !DangGiam;

        public int PhanTramGiam => DangGiam ? (int)Math.Round((1 - GiaKhuyenMai.Value / GiaBan) * 100) : 0;
    }
}
