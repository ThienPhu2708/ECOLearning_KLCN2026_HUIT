using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class CourseApprovalController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/CourseApproval/Index/KH... (UC15: Kiểm duyệt khóa học)
        // Bộ lọc thêm: category (danh mục), fromDate/toDate (ngày gửi duyệt, yyyy-MM-dd), sort = newest | oldest
        public ActionResult Index(string id, string statusFilter = null, string search = "",
                                  string category = "", DateTime? fromDate = null, DateTime? toDate = null, string sort = "newest")
        {
            var query = db.KhoaHoc.AsQueryable();

            // Find target course if ID is passed in route/URL
            KhoaHoc targetCourse = null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                targetCourse = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc.ToLower() == id.ToLower());
            }

            // Default statusFilter: if target course is requested, show "All", otherwise default to "Pending"
            if (string.IsNullOrEmpty(statusFilter))
            {
                statusFilter = (targetCourse != null) ? "All" : "Pending";
            }

            // Calculate tab counts
            int pendingCount = db.KhoaHoc.Count(k => k.trangThai == "Chờ duyệt" || k.trangThai == "Chờ kiểm duyệt" || k.trangThai == "Pending");
            int approvedCount = db.KhoaHoc.Count(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt");
            int rejectedCount = db.KhoaHoc.Count(k => k.trangThai == "Cần sửa đổi" || k.trangThai == "Từ chối");
            int allCount = db.KhoaHoc.Count();

            bool revisionTab = string.Equals(statusFilter, "Revisions", StringComparison.OrdinalIgnoreCase);
            var pendingRevisions = new ECOLearning.Services.CourseRevisionService(db, Server.MapPath).Pending();
            if (revisionTab) { query = query.Where(k => false); targetCourse = null; }
            // Filter by status tab
            if (string.Equals(statusFilter, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(k => k.trangThai == "Chờ duyệt" || k.trangThai == "Chờ kiểm duyệt" || k.trangThai == "Pending");
            }
            else if (string.Equals(statusFilter, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt");
            }
            else if (string.Equals(statusFilter, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(k => k.trangThai == "Cần sửa đổi" || k.trangThai == "Từ chối");
            }

            // Filter by search keyword
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(k => k.tenKhoaHoc.ToLower().Contains(s) 
                                      || k.maKhoaHoc.ToLower().Contains(s)
                                      || (k.NguoiDung != null && k.NguoiDung.hoTen.ToLower().Contains(s)));
            }

            // Filter by category
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(k => k.maDanhMuc == category);
            }

            var rawCourses = query.OrderByDescending(k => k.maKhoaHoc).ToList();

            // Guarantee targetCourse is in the list even if tab filtering excluded it
            if (targetCourse != null && !rawCourses.Any(c => c.maKhoaHoc.ToLower() == targetCourse.maKhoaHoc.ToLower()))
            {
                rawCourses.Insert(0, targetCourse);
            }

            var model = rawCourses.Select(k => {
                var kiemDuyet = k.KiemDuyetKhoaHoc.OrderByDescending(kd => kd.ngayGui).FirstOrDefault();
                int videoCount = 0;
                int documentCount = 0;

                if (k.ChuongHoc != null)
                {
                    foreach (var ch in k.ChuongHoc)
                    {
                        if (ch.BaiHoc != null)
                        {
                            videoCount += ch.BaiHoc.Count(b => !string.IsNullOrEmpty(b.duongDanVideo));
                            foreach (var bh in ch.BaiHoc)
                            {
                                if (bh.TaiNguyen != null)
                                {
                                    documentCount += bh.TaiNguyen.Count();
                                }
                            }
                        }
                    }
                }

                return new PendingCourseItem
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    TenGiaoVien = k.NguoiDung != null ? k.NguoiDung.hoTen : "N/A",
                    TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : "Chưa phân loại",
                    NgayGui = kiemDuyet != null ? kiemDuyet.ngayGui : null,
                    HocPhi = k.giaBan,
                    SoVideo = videoCount,
                    SoTaiLieu = documentCount,
                    TrangThai = k.trangThai
                };
            }).ToList();

            if (revisionTab) {
                var categoryNames = db.DanhMuc.ToDictionary(c => c.maDanhMuc, c => c.tenDanhMuc);
                var teacherNames = db.NguoiDung.ToDictionary(u => u.maNguoiDung, u => u.hoTen);
                model = pendingRevisions.Where(r => string.IsNullOrWhiteSpace(category) || r.Read().Model.MaDanhMuc == category).Select(r => {
                    var draft = r.Read().Model;
                    return new PendingCourseItem {
                        MaKhoaHoc = r.CourseId, TenKhoaHoc = draft.TenKhoaHoc,
                        TenGiaoVien = teacherNames.ContainsKey(r.TeacherId) ? teacherNames[r.TeacherId] : "Giáo viên",
                        TenDanhMuc = categoryNames.ContainsKey(draft.MaDanhMuc) ? categoryNames[draft.MaDanhMuc] : "Chưa phân loại",
                        NgayGui = r.SubmittedAt, HocPhi = draft.GiaBan,
                        SoVideo = draft.Lessons.Count(l => !string.IsNullOrEmpty(l.VideoToken)),
                        SoTaiLieu = draft.Lessons.Sum(l => l.AttachmentTokens.Count), TrangThai = "Chờ duyệt bản sửa"
                    };
                }).ToList();
                if (!string.IsNullOrWhiteSpace(search)) {
                    string keyword = search.Trim();
                    model = model.Where(m => m.TenKhoaHoc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                        || m.MaKhoaHoc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                        || m.TenGiaoVien.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
            }

            // Filter by submitted date range (ngày gửi duyệt). Khóa học chưa từng gửi duyệt bị loại khi có lọc ngày.
            if (fromDate.HasValue)
            {
                model = model.Where(m => m.NgayGui.HasValue && m.NgayGui.Value.Date >= fromDate.Value.Date).ToList();
            }
            if (toDate.HasValue)
            {
                model = model.Where(m => m.NgayGui.HasValue && m.NgayGui.Value.Date <= toDate.Value.Date).ToList();
            }

            // Sort by submitted date (khóa học chưa có ngày gửi luôn ở cuối); khóa học được chọn qua id luôn ở đầu
            sort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase) ? "oldest" : "newest";
            model = (sort == "oldest"
                    ? model.OrderBy(m => m.NgayGui.HasValue ? 0 : 1).ThenBy(m => m.NgayGui)
                    : model.OrderBy(m => m.NgayGui.HasValue ? 0 : 1).ThenByDescending(m => m.NgayGui))
                .ToList();
            if (targetCourse != null)
            {
                var target = model.FirstOrDefault(m => string.Equals(m.MaKhoaHoc, targetCourse.maKhoaHoc, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    model.Remove(target);
                    model.Insert(0, target);
                }
            }

            ViewBag.PendingRevisions = pendingRevisions.Count;
            ViewBag.Categories = db.DanhMuc.OrderBy(c => c.tenDanhMuc).ToList();
            ViewBag.Category = category ?? "";
            ViewBag.FromDate = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDate = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.Sort = sort;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.Search = search;
            ViewBag.PendingCount = pendingCount;
            ViewBag.ApprovedCount = approvedCount;
            ViewBag.RejectedCount = rejectedCount;
            ViewBag.AllCount = allCount;
            ViewBag.SelectedCourseId = id;
            ViewBag.BreadcrumbCurrent = "Kiểm duyệt khóa học";

            return View(model);
        }

        // GET: Admin/CourseApproval/GetDetails/5
        [HttpGet]
        public ActionResult GetDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Json(new { success = false, message = "Mã khóa học không hợp lệ!" }, JsonRequestBehavior.AllowGet);
            }

            var k = db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc.ToLower() == id.ToLower());
            if (k == null)
            {
                return Json(new { success = false, message = "Không tìm thấy khóa học mã " + id }, JsonRequestBehavior.AllowGet);
            }

            var kiemDuyet = k.KiemDuyetKhoaHoc.OrderByDescending(kd => kd.ngayGui).FirstOrDefault();

            var detail = new CourseApprovalDetailViewModel
            {
                MaKhoaHoc = k.maKhoaHoc,
                TenKhoaHoc = k.tenKhoaHoc,
                TenGiaoVien = k.NguoiDung != null ? k.NguoiDung.hoTen : "N/A",
                EmailGiaoVien = k.NguoiDung != null ? k.NguoiDung.email : "N/A",
                TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : "Chưa phân loại",
                HocPhi = k.giaBan,
                MoTa = k.moTa,
                MoTaNgan = k.moTaNgan,
                MucTieuDauRa = k.mucTieuDauRa,
                AnhBia = k.anhBia,
                TrangThai = k.trangThai,
                NgayGui = kiemDuyet != null ? kiemDuyet.ngayGui : null,
                LyDoTuChoi = kiemDuyet != null ? kiemDuyet.lyDoTuChoi : ""
            };

            if (k.ChuongHoc != null && k.ChuongHoc.Any())
            {
                foreach (var ch in k.ChuongHoc.OrderBy(c => c.thuTu))
                {
                    var chDetail = new ChuongHocDetail
                    {
                        MaChuong = ch.maChuong,
                        TenChuong = ch.tenChuong
                    };

                    if (ch.BaiHoc != null)
                    {
                        foreach (var bh in ch.BaiHoc.OrderBy(b => b.thuTu))
                        {
                            var bhDetail = new BaiHocDetail
                            {
                                MaBaiHoc = bh.maBaiHoc,
                                TenBaiHoc = bh.tenBaiHoc,
                                VideoUrl = ECOLearning.Services.VideoStorageService.LinkPhat(bh.duongDanVideo),
                                ThoiLuong = bh.thoiLuong,
                                CoXemThu = bh.choXemThu
                            };

                            if (bh.TaiNguyen != null)
                            {
                                foreach (var tn in bh.TaiNguyen)
                                {
                                    bhDetail.TaiNguyens.Add(new TaiNguyenDetail
                                    {
                                        MaTaiNguyen = tn.maTaiNguyen,
                                        TenTaiNguyen = tn.tenTaiNguyen,
                                        FileUrl = tn.duongDan,
                                        LoaiFile = tn.loaiTaiNguyen
                                    });
                                }
                            }

                            chDetail.BaiHocs.Add(bhDetail);
                        }
                    }

                    detail.ChuongHocs.Add(chDetail);
                }
            }

            // Bài học không phân chương (maChuong == null)
            var unchapteredLessons = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(k.maKhoaHoc + "_"))
                .OrderBy(b => b.thuTu)
                .ToList();

            if (unchapteredLessons.Any())
            {
                var chDetail = new ChuongHocDetail
                {
                    MaChuong = "no_chapter",
                    TenChuong = "Bài học độc lập (Không phân chương)"
                };

                foreach (var bh in unchapteredLessons)
                {
                    var bhDetail = new BaiHocDetail
                    {
                        MaBaiHoc = bh.maBaiHoc,
                        TenBaiHoc = bh.tenBaiHoc,
                        VideoUrl = ECOLearning.Services.VideoStorageService.LinkPhat(bh.duongDanVideo),
                        ThoiLuong = bh.thoiLuong,
                        CoXemThu = bh.choXemThu
                    };

                    if (bh.TaiNguyen != null)
                    {
                        foreach (var tn in bh.TaiNguyen)
                        {
                            bhDetail.TaiNguyens.Add(new TaiNguyenDetail
                            {
                                MaTaiNguyen = tn.maTaiNguyen,
                                TenTaiNguyen = tn.tenTaiNguyen,
                                FileUrl = tn.duongDan,
                                LoaiFile = tn.loaiTaiNguyen
                            });
                        }
                    }

                    chDetail.BaiHocs.Add(bhDetail);
                }

                detail.ChuongHocs.Add(chDetail);
            }

            return Json(new { success = true, data = detail }, JsonRequestBehavior.AllowGet);
        }

        // POST: Admin/CourseApproval/Approve
        [HttpPost]
        public ActionResult Approve(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Json(new { success = false, message = "Mã khóa học không hợp lệ!" });
            }

            try
            {
                var k = db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc.ToLower() == id.ToLower());
                if (k == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khóa học mã " + id });
                }

                if (ECOLearning.Services.CourseRevisionService.Published(k.trangThai))
                    return Json(new { success = false, message = "Khóa đang hoạt động. Vào Duyệt bản sửa khóa học để xử lý phiên bản gửi duyệt." });

                // Giáo viên đang bị khóa tài khoản: khóa học của họ đang tạm ẩn, không xuất bản thêm
                var gv = db.NguoiDung.Find(k.maGiaoVien);
                if (gv != null && gv.trangThai == false)
                {
                    return Json(new { success = false, message = "Giáo viên của khóa học đang bị khóa tài khoản nên chưa thể xuất bản. Mở khóa giáo viên ở trang Quản lý giáo viên trước." });
                }


                // Kiểm tra lại nội dung trước khi xuất bản: tài nguyên có thể bị xóa sau khi giáo viên gửi duyệt
                var thieu = new ECOLearning.Services.CourseContentService(db, Server.MapPath).KiemTraKhoaHoc(k.maKhoaHoc);
                if (thieu.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Chưa thể xuất bản: " + ECOLearning.Services.CourseContentService.TomTat(thieu)
                            + " Hãy từ chối kèm lý do để giáo viên bổ sung."
                    });
                }

                // Cập nhật trạng thái khóa học thành "Đã xuất bản"
                using (var transaction = db.Database.BeginTransaction())
                {
                k.trangThai = "Đã xuất bản";

                // Lưu nhật ký kiểm duyệt vào CSDL
                var kd = db.KiemDuyetKhoaHoc.FirstOrDefault(x => x.maKhoaHoc.ToLower() == id.ToLower());
                if (kd == null)
                {
                    kd = new KiemDuyetKhoaHoc
                    {
                        maKiemDuyet = "KD-" + Guid.NewGuid().ToString().Substring(0, 8),
                        maKhoaHoc = k.maKhoaHoc,
                        ngayGui = DateTime.Now
                    };
                    db.KiemDuyetKhoaHoc.Add(kd);
                }

                kd.trangThai = "Đã duyệt";
                kd.ngayKiemDuyet = DateTime.Now;
                kd.lyDoTuChoi = null;
                if (Session["UserId"] != null)
                {
                    kd.maAdmin = Convert.ToInt32(Session["UserId"]);
                }

                db.SaveChanges();
                var audit = new ECOLearning.Services.AccountAdminService(db);
                if (audit.IsInstalled()) audit.GhiNhatKy(Convert.ToInt32(Session["UserId"]),
                    ECOLearning.Services.AccountAdminService.HdDuyetKhoaHoc, "KhoaHoc", k.maKhoaHoc, "Duyệt xuất bản khóa học: " + k.tenKhoaHoc);
                transaction.Commit();
                }
                return Json(new { success = true, message = "Phê duyệt thành công! Khóa học \"" + k.tenKhoaHoc + "\" đã được xuất bản ra trang chủ." });
            }
            catch (Exception ex)
            {
                Exception current = ex;
                while (current.InnerException != null) current = current.InnerException;
                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu phê duyệt: " + current.Message });
            }
        }

        // POST: Admin/CourseApproval/Reject
        [HttpPost]
        public ActionResult Reject(string id, string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Json(new { success = false, message = "Mã khóa học không hợp lệ!" });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối để thông báo cho giáo viên!" });
            }

            try
            {
                var k = db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc.ToLower() == id.ToLower());
                if (k == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khóa học mã " + id });
                }

                // Cập nhật trạng thái khóa học thành "Cần sửa đổi"
                if (ECOLearning.Services.CourseRevisionService.Published(k.trangThai))
                    return Json(new { success = false, message = "Khóa đang hoạt động. Từ chối bản sửa trong mục Duyệt bản sửa khóa học." });
                using (var transaction = db.Database.BeginTransaction())
                {
                k.trangThai = "Cần sửa đổi";

                // Lưu lý do từ chối vào CSDL
                var kd = db.KiemDuyetKhoaHoc.FirstOrDefault(x => x.maKhoaHoc.ToLower() == id.ToLower());
                if (kd == null)
                {
                    kd = new KiemDuyetKhoaHoc
                    {
                        maKiemDuyet = "KD-" + Guid.NewGuid().ToString().Substring(0, 8),
                        maKhoaHoc = k.maKhoaHoc,
                        ngayGui = DateTime.Now
                    };
                    db.KiemDuyetKhoaHoc.Add(kd);
                }

                kd.trangThai = "Từ chối";
                kd.lyDoTuChoi = reason.Trim();
                kd.ngayKiemDuyet = DateTime.Now;
                if (Session["UserId"] != null)
                {
                    kd.maAdmin = Convert.ToInt32(Session["UserId"]);
                }

                db.SaveChanges();
                var audit = new ECOLearning.Services.AccountAdminService(db);
                if (audit.IsInstalled()) audit.GhiNhatKy(Convert.ToInt32(Session["UserId"]),
                    ECOLearning.Services.AccountAdminService.HdTuChoiKhoaHoc, "KhoaHoc", k.maKhoaHoc, reason.Trim());
                transaction.Commit();
                }
                return Json(new { success = true, message = "Đã từ chối phê duyệt và chuyển khóa học sang trạng thái \"Cần sửa đổi\"." });
            }
            catch (Exception ex)
            {
                Exception current = ex;
                while (current.InnerException != null) current = current.InnerException;
                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu từ chối: " + current.Message });
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
