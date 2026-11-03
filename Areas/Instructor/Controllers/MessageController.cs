using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Controllers;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class MessageController : InstructorBaseController
    {
        [HttpGet]
        public JsonResult Conversations()
        {
            var service = new ChatService(db);
            var conversations = service.LayDanhSachHoiThoaiGiaoVien(CurrentTeacherId);
            return Json(new {
                count = service.DemTongTinChuaDoc(CurrentTeacherId, true),
                items = conversations.Where(c => c.TinCuoiLuc.HasValue).Take(20).Select(c => new {
                    name = c.TenHocVien, course = c.TenKhoaHoc,
                    preview = (c.MaNguoiGuiCuoi == CurrentTeacherId ? "Bạn: " : "") + c.TinCuoiTomTat, unread = c.SoTinChuaDoc,
                    time = c.TinCuoiLuc.HasValue ? c.TinCuoiLuc.Value.ToString(c.TinCuoiLuc.Value.Date == DateTime.Today ? "HH:mm" : "dd/MM") : "",
                    url = Url.Action("Index", "Message", new { area = "Instructor", id = c.MaHoiThoai })
                })
            }, JsonRequestBehavior.AllowGet);
        }
        // GET: /Instructor/Message
        public ActionResult Index(int? id, string courseId = "", string status = "all", bool list = false)
        {
            status = status == "unread" ? "unread" : "all";
            int teacherId = CurrentTeacherId;
            var service = new ChatService(db);

            if (!service.DaCaiDat())
            {
                ViewBag.Error = "Chức năng nhắn tin chưa sẵn sàng trong CSDL.";
                return View(new InstructorChatIndexViewModel());
            }

            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .Select(k => new CourseFilterItem { MaKhoaHoc = k.maKhoaHoc, TenKhoaHoc = k.tenKhoaHoc })
                .ToList();

            var conversations = service.LayDanhSachHoiThoaiGiaoVien(teacherId, courseId, status);

            var model = new InstructorChatIndexViewModel
            {
                DanhSachHoiThoai = conversations,
                DanhSachKhoaHoc = teacherCourses,
                FilterCourseId = courseId,
                FilterStatus = status,
                SelectedHoiThoaiId = null
            };

            int selectedId = list ? 0 : id ?? 0;
            if (selectedId > 0)
            {
                var currentConv = conversations.FirstOrDefault(c => c.MaHoiThoai == selectedId);

                if (currentConv != null && currentConv.MaGiaoVien == teacherId)
                {
                    model.SelectedHoiThoaiId = selectedId;
                    var studentInfo = service.LayThongTinHocVien(currentConv.MaKhoaHoc, currentConv.MaHocVien);
                    var messages = service.LayLichSuTinNhan(selectedId, teacherId);

                    model.PhongChatHienTai = new ChatRoomViewModel
                    {
                        HoiThoai = currentConv,
                        DanhSachTinNhan = messages,
                        ThongTinHocVien = studentInfo,
                        LaGiaoVien = true,
                        CurrentUserId = teacherId
                    };

                    service.DanhDauDaDoc(selectedId, teacherId);
                    currentConv.SoTinChuaDoc = 0;
                }
            }

            ViewBag.BreadcrumbCurrent = "Tin nhắn học viên";
            return View(model);
        }

        // GET: /Instructor/Message/StudentProfile?courseId=...&studentId=...
        [HttpGet]
        public JsonResult StudentProfile(string courseId, int studentId)
        {
            if (!db.KhoaHoc.Any(k => k.maKhoaHoc == courseId && k.maGiaoVien == CurrentTeacherId))
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            var service = new ChatService(db);
            var info = service.LayThongTinHocVien(courseId, studentId);
            return Json(new { success = info != null, data = info }, JsonRequestBehavior.AllowGet);
        }
    }
}

