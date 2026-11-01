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
        // GET: /Instructor/Message
        public ActionResult Index(int? id, string courseId = "", string status = "all")
        {
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
                SelectedHoiThoaiId = id
            };

            int selectedId = id ?? (conversations.FirstOrDefault()?.MaHoiThoai ?? 0);
            if (selectedId > 0)
            {
                var currentConv = conversations.FirstOrDefault(c => c.MaHoiThoai == selectedId)
                               ?? service.LayChiTietHoiThoai(selectedId, teacherId);

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
                }
            }

            ViewBag.BreadcrumbCurrent = "Tin nhắn học viên";
            return View(model);
        }

        // GET: /Instructor/Message/StudentProfile?courseId=...&studentId=...
        [HttpGet]
        public JsonResult StudentProfile(string courseId, int studentId)
        {
            var service = new ChatService(db);
            var info = service.LayThongTinHocVien(courseId, studentId);
            return Json(new { success = info != null, data = info }, JsonRequestBehavior.AllowGet);
        }
    }
}

