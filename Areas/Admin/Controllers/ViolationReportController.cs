using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    // Quản lý báo cáo vi phạm khóa học (hậu kiểm nội dung)
    public class ViolationReportController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();
        private ViolationReportService service;

        public ViolationReportController()
        {
            service = new ViolationReportService(db);
        }

        private int CurrentAdminId
        {
            get { return Convert.ToInt32(Session["UserId"]); }
        }

        // GET: Admin/ViolationReport?filter=pending|locked|banned|all
        // sort: priority (mặc định: chờ Admin xem xét, nhiều báo cáo chờ trước) | latest | oldest | deadline
        public ActionResult Index(string filter = "pending", string sort = "priority")
        {
            filter = (filter ?? "pending").ToLower();
            sort = (sort ?? "priority").ToLower();
            if (sort != "latest" && sort != "oldest" && sort != "deadline") sort = "priority";
            ViewBag.BreadcrumbCurrent = "Báo cáo vi phạm";
            ViewBag.Filter = filter;
            ViewBag.Sort = sort;

            if (!service.IsInstalled())
            {
                ViewBag.NotInstalled = true;
                return View(new System.Collections.Generic.List<ViolationCourseSummary>());
            }

            var all = service.GetCourseSummaries("all");
            ViewBag.PendingCount = all.Count(c => c.soChoXuLy > 0);
            ViewBag.HasDiscussion = service.HasDiscussion();
            ViewBag.ReviewCount = all.Count(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa && c.trangThaiXuLy == ViolationReportService.StepWaitingAdmin);
            ViewBag.LockedCount = all.Count(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa);
            ViewBag.BannedCount = all.Count(c => c.trangThaiKhoaHoc == CourseStatus.KhoaVinhVien);
            ViewBag.AllCount = all.Count;

            var list = service.GetCourseSummaries(filter);
            switch (sort)
            {
                case "latest":
                    list = list.OrderByDescending(c => c.baoCaoGanNhat ?? DateTime.MinValue).ToList();
                    break;
                case "oldest":
                    list = list.OrderBy(c => c.baoCaoGanNhat ?? DateTime.MaxValue).ToList();
                    break;
                case "deadline":
                    // Khóa học đang tạm khóa có hạn phản hồi gần nhất lên đầu, còn lại xếp sau
                    list = list.OrderBy(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa && c.hanPhanHoi.HasValue ? 0 : 1)
                               .ThenBy(c => c.hanPhanHoi ?? DateTime.MaxValue)
                               .ThenByDescending(c => c.baoCaoGanNhat ?? DateTime.MinValue)
                               .ToList();
                    break;
            }
            return View(list);
        }

        // GET: Admin/ViolationReport/Detail/KH...
        public ActionResult Detail(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !service.IsInstalled())
            {
                return RedirectToAction("Index");
            }

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id);
            if (course == null)
            {
                return HttpNotFound();
            }

            var actions = service.GetActions(id);
            var lastLock = actions.FirstOrDefault(a => a.hanhDong == ViolationReportService.ActionLock);

            ViewBag.Course = course;
            ViewBag.Reports = service.GetReports(id);
            ViewBag.Actions = actions;
            ViewBag.LastLock = lastLock;
            ViewBag.HasDiscussion = service.HasDiscussion();
            ViewBag.Messages = service.GetMessages(lastLock != null ? lastLock.maXuLy : null);
            ViewBag.MinDays = ViolationReportService.MinDeadlineDays;
            ViewBag.MaxDays = ViolationReportService.MaxDeadlineDays;
            ViewBag.BreadcrumbCurrent = "Báo cáo vi phạm / " + course.tenKhoaHoc;
            return View();
        }

        // POST: Admin/ViolationReport/Dismiss (Bác bỏ toàn bộ báo cáo đang chờ của khóa học)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Dismiss(string id, string ghiChu)
        {
            return Run(() => service.Dismiss(id, CurrentAdminId, ghiChu), "Đã bác bỏ các báo cáo đang chờ xử lý.");
        }

        // POST: Admin/ViolationReport/Lock (Tạm khóa khóa học, chờ giáo viên phản hồi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Lock(string id, string lyDo, int soNgay = 7)
        {
            return Run(() => service.Lock(id, CurrentAdminId, lyDo, soNgay),
                $"Đã tạm khóa khóa học. Giáo viên có {soNgay} ngày để xử lý.");
        }

        // POST: Admin/ViolationReport/RequestChanges (Chưa đồng ý phản hồi của GV: yêu cầu sửa thêm + gia hạn)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RequestChanges(string id, string noiDung, int soNgay = 3)
        {
            return Run(() => service.RequestMoreChanges(id, CurrentAdminId, noiDung, soNgay),
                $"Đã gửi yêu cầu sửa thêm. Giáo viên có thêm {soNgay} ngày để xử lý.");
        }

        // POST: Admin/ViolationReport/Reopen (Mở lại khóa học đang tạm khóa)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Reopen(string id, string ghiChu)
        {
            return Run(() => service.Reopen(id, CurrentAdminId, ghiChu), "Đã mở lại khóa học.");
        }

        // POST: Admin/ViolationReport/PermanentLock (Khóa vĩnh viễn — không xóa dữ liệu khóa học)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult PermanentLock(string id, string lyDo)
        {
            return Run(() => service.PermanentLock(id, CurrentAdminId, lyDo), "Đã khóa vĩnh viễn khóa học.");
        }

        private JsonResult Run(Func<string> action, string successMessage)
        {
            if (!service.IsInstalled())
            {
                return Json(new { success = false, message = ViolationReportService.NotInstalledMessage });
            }

            try
            {
                string error = action();
                return error == null
                    ? Json(new { success = true, message = successMessage })
                    : Json(new { success = false, message = error });
            }
            catch (Exception ex)
            {
                Exception current = ex;
                while (current.InnerException != null) current = current.InnerException;
                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu: " + current.Message });
            }
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
