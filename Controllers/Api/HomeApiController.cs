using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;
using ECOLearning.Services.Api;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Nhóm I – Trang chủ học viên. Dùng chung HomeSectionService với trang chủ website (HomeController.Index) nên app và web
    /// cùng một cách chọn / xếp khóa học. Các mục khác của trang chủ đã có API riêng: danh mục (B1), mã khuyến mãi
    /// (GET /coupons/collectable), "Khóa học dành riêng cho bạn" (C1).
    /// </summary>
    [RoutePrefix("api/v1")]
    public class HomeApiController : ApiControllerBase
    {
        // Cùng số lượng với trang chủ website (HomeController)
        private const int SoKhoaMoiMuc = 10;
        private const int SoTiepTucHoc = 3;
        private const int SoYeuThich = 5;
        private const int SoGiaoVien = 4;

        /// <summary>Thẻ khóa học (cùng trường với B2 / C1) kèm nhãn và ghi chú riêng của từng mục.</summary>
        public class HomeCourseDto : CourseSummaryDto
        {
            /// <summary>Nhãn góc ảnh: "Bán chạy", "Đánh giá cao", "Mới" hoặc null.</summary>
            public string Badge { get; set; }
            /// <summary>Dòng ghi chú nhỏ: "12 lượt đăng ký tuần này", "Ra mắt 19/10/2026", "Đã xem hôm qua" hoặc null.</summary>
            public string Note { get; set; }
            /// <summary>Chỉ mục best-sellers: lượt đăng ký trong kỳ xếp hạng.</summary>
            public int? EnrollmentsInPeriod { get; set; }
        }

        /// <summary>Một mục trang chủ. Mọi mục cùng khung để app dựng một model; trường không dùng thì null.</summary>
        public class HomeSection
        {
            public string Key { get; set; }
            /// <summary>"continue" (thẻ học tiếp), "courses" (thẻ khóa học), "teachers" (thẻ giáo viên).</summary>
            public string Type { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            /// <summary>favorites: tổng số khóa yêu thích (hiện "Xem tất cả (n)" khi lớn hơn số items).</summary>
            public int? Total { get; set; }
            /// <summary>best-sellers: "week" / "30days" / "all".</summary>
            public string Period { get; set; }
            /// <summary>because-you-learned: khóa làm mốc.</summary>
            public string AnchorCourseId { get; set; }
            public string AnchorCourseTitle { get; set; }
            public object Items { get; set; }
        }

        // I1. GET /api/v1/home : các mục trang chủ theo đúng thứ tự trên web; mục không có dữ liệu thì không trả.
        // Có token: đủ các mục (kể cả mục cá nhân). Không có token: chỉ các mục chung (bán chạy, đánh giá cao, mới ra mắt, giáo viên).
        [HttpGet, Route("home")]
        public ActionResult Home()
        {
            int userId = CurrentUserId;
            bool caNhan = userId > 0;
            var service = new HomeSectionService(Db);
            var mapper = new ApiCourseMapper(Db, AbsoluteUrl);
            var sections = new List<HomeSection>();

            // Mỗi mục lỗi riêng thì chỉ mục đó bị bỏ, các mục khác vẫn trả (giống trang chủ web)
            Action<string, Func<HomeSection>> them = (key, lam) =>
            {
                try
                {
                    var s = lam();
                    if (s != null) sections.Add(s);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("API home " + key + ": " + ex);
                }
            };

            if (caNhan)
            {
                them("continue-learning", () => TiepTucHoc(service, userId));
                them("favorites", () => YeuThich(service, mapper, userId));
            }
            them("best-sellers", () => BanChay(service, mapper, userId));
            if (caNhan)
                them("because-you-learned", () => ViBanDaHoc(service, mapper, userId));
            them("top-rated", () => MucKhoaHoc("top-rated", "Đánh giá cao", "Từ 4 sao trở lên, xếp theo điểm có tính số lượt đánh giá",
                service.DanhGiaCao(userId, SoKhoaMoiMuc), mapper));
            them("new-releases", () => MucKhoaHoc("new-releases", "Mới ra mắt", "Khóa học vừa được duyệt và mở bán",
                service.MoiRaMat(userId, SoKhoaMoiMuc), mapper));
            them("featured-teachers", () => GiaoVienNoiBat(service, userId));
            if (caNhan)
                them("recently-viewed", () => MucKhoaHoc("recently-viewed", "Đã xem gần đây", "Các khóa học bạn đã mở xem chi tiết",
                    service.DaXemGanDay(userId, SoKhoaMoiMuc), mapper));

            return Ok(new { isPersonalized = caNhan, sections });
        }

        private HomeSection TiepTucHoc(HomeSectionService service, int userId)
        {
            var ds = service.TiepTucHoc(userId, SoTiepTucHoc);
            if (ds.Count == 0) return null;
            return new HomeSection
            {
                Key = "continue-learning",
                Type = "continue",
                Title = "Tiếp tục học",
                Subtitle = "Học tiếp đúng bài bạn đang xem",
                Items = ds.Select(t => new
                {
                    courseId = t.MaKhoaHoc,
                    title = t.TenKhoaHoc,
                    imageUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(t.AnhBia) ? ApiCourseMapper.AnhBiaMacDinh : t.AnhBia),
                    instructor = t.TenGiaoVien,
                    progress = t.PhanTram,
                    completedLessons = t.SoBaiXong,
                    totalLessons = t.TongSoBai,
                    lessonId = t.MaBaiHoc,
                    lessonTitle = t.TenBaiHoc,
                    lessonNumber = t.SoThuTuBai,
                    positionSeconds = t.ViTriGiay,
                    isStarted = t.DaBatDau,
                    lastAccessAt = ApiFormat.Date(t.LanHocGanNhat)
                }).ToList()
            };
        }

        private HomeSection YeuThich(HomeSectionService service, ApiCourseMapper mapper, int userId)
        {
            int tong;
            var ds = service.YeuThich(userId, SoYeuThich, out tong);
            if (ds.Count == 0) return null;
            return new HomeSection
            {
                Key = "favorites",
                Type = "courses",
                Title = "Khóa học yêu thích của bạn",
                Subtitle = "Các khóa bạn đã thả tim",
                Total = tong,
                Items = mapper.Map(ds.Select(f => f.Course.maKhoaHoc)).Select(d => The(d, null, null, null)).ToList()
            };
        }

        private HomeSection BanChay(HomeSectionService service, ApiCourseMapper mapper, int userId)
        {
            var bc = service.BanChay(userId, SoKhoaMoiMuc);
            if (bc.KhoaHoc.Count == 0) return null;
            string ky = bc.TenKy == "tuần này" ? "week" : bc.TenKy == "30 ngày qua" ? "30days" : "all";
            return new HomeSection
            {
                Key = "best-sellers",
                Type = "courses",
                Title = ky == "all" ? "Khóa học bán chạy nhất" : "Bán chạy " + bc.TenKy,
                Subtitle = ky == "all" ? "Xếp theo tổng số học viên đăng ký" : "Xếp theo lượt đăng ký mới trong " + (ky == "week" ? "7 ngày qua" : "30 ngày qua"),
                Period = ky,
                Items = mapper.Map(bc.KhoaHoc.Select(k => k.maKhoaHoc)).Select(d =>
                {
                    int luot;
                    bc.LuotTrongKy.TryGetValue(d.Id, out luot);
                    // HomeSectionService chỉ trả khóa có đăng ký trong kỳ cho mục này.
                    // App luôn nhận nhãn Bán chạy, không có thẻ đánh giá cao/khóa bù.
                    return The(d, "Bán chạy",
                        luot > 0 && ky != "all" ? luot + " lượt đăng ký " + bc.TenKy : null,
                        ky == "all" ? (int?)null : luot);
                }).ToList()
            };
        }

        private HomeSection ViBanDaHoc(HomeSectionService service, ApiCourseMapper mapper, int userId)
        {
            var r = service.ViBanDaHoc(userId, SoKhoaMoiMuc);
            if (r == null) return null;
            var s = MucKhoaHoc("because-you-learned", "Vì bạn đã học “" + r.TenKhoaHocMoc + "”", "Các khóa học cùng lĩnh vực với khóa bạn vừa học", r.KhoaHoc, mapper);
            if (s == null) return null;
            s.AnchorCourseId = r.MaKhoaHocMoc;
            s.AnchorCourseTitle = r.TenKhoaHocMoc;
            return s;
        }

        private HomeSection GiaoVienNoiBat(HomeSectionService service, int userId)
        {
            var ds = service.GiaoVienNoiBat(userId, SoGiaoVien);
            if (ds.Count == 0) return null;
            return new HomeSection
            {
                Key = "featured-teachers",
                Type = "teachers",
                Title = "Giáo viên nổi bật",
                Subtitle = "Được nhiều học viên theo học và đánh giá cao",
                Items = ds.Select(g => new
                {
                    id = g.MaGiaoVien,
                    fullName = g.HoTen,
                    avatarUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(g.AnhDaiDien) ? ApiCourseMapper.AnhDaiDienMacDinh : g.AnhDaiDien),
                    specialty = string.IsNullOrWhiteSpace(g.ChuyenMon) ? null : g.ChuyenMon,
                    coursesCount = g.SoKhoaHoc,
                    studentsCount = g.SoHocVien,
                    rating = Math.Round(g.DiemDanhGia, 1),
                    reviewCount = g.SoDanhGia
                }).ToList()
            };
        }

        /// <summary>Mục thẻ khóa học từ danh sách của HomeSectionService (giữ thứ tự, nhãn và ghi chú).</summary>
        private static HomeSection MucKhoaHoc(string key, string title, string subtitle, List<CourseCardViewModel> ds, ApiCourseMapper mapper)
        {
            if (ds == null || ds.Count == 0) return null;
            var theoMa = ds.GroupBy(c => c.Course.maKhoaHoc).ToDictionary(g => g.Key, g => g.First());
            var items = mapper.Map(ds.Select(c => c.Course.maKhoaHoc))
                .Select(d => The(d, theoMa[d.Id].Nhan, theoMa[d.Id].GhiChu, null)).ToList();
            if (items.Count == 0) return null;
            return new HomeSection { Key = key, Type = "courses", Title = title, Subtitle = subtitle, Items = items };
        }

        private static HomeCourseDto The(CourseSummaryDto d, string badge, string note, int? luot)
        {
            return new HomeCourseDto
            {
                Id = d.Id,
                Title = d.Title,
                CategoryId = d.CategoryId,
                Category = d.Category,
                Instructor = d.Instructor,
                InstructorAvatar = d.InstructorAvatar,
                ImageUrl = d.ImageUrl,
                Level = d.Level,
                Rating = d.Rating,
                ReviewCount = d.ReviewCount,
                Price = d.Price,
                OriginalPrice = d.OriginalPrice,
                Promotion = d.Promotion,
                IsFree = d.IsFree,
                LessonsCount = d.LessonsCount,
                DurationSeconds = d.DurationSeconds,
                StudentsCount = d.StudentsCount,
                Tag = d.Tag,
                Badge = badge,
                Note = note,
                EnrollmentsInPeriod = luot
            };
        }
    }
}
