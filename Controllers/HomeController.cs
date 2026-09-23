using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Controllers
{
    public class HomeController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Trang chủ (Hero banner, Khóa học nổi bật, Gợi ý UC7)
        public ActionResult Index()
        {
            List<KhoaHoc> courses = new List<KhoaHoc>();
            try
            {
                courses = db.KhoaHoc
                    .Include(k => k.NguoiDung)
                    .Include(k => k.DanhMuc)
                    .Include(k => k.DanhGiaKhoaHoc)
                    .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                    .ToList();

                // Lấy danh sách danh mục thực sự có trong CSDL SQL Server
                ViewBag.Categories = db.DanhMuc
                    .Where(c => c.trangThai == null || c.trangThai == true)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch home data error: " + ex.Message);
            }

            return View(courses);
        }

        public ActionResult About()
        {
            ViewBag.Message = "ECO Learning Platform";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Contact ECO Learning Support";
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
