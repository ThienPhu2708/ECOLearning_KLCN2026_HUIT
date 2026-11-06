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
    /// Nhóm C – Gợi ý khóa học (lọc cộng tác Item-based, cùng thuật toán với trang chủ website) và sở thích (cold start),
    /// nhóm D – Yêu thích (Task_Tuan10.docx mục 6.3, 6.4).
    /// </summary>
    [RoutePrefix("api/v1")]
    public class RecommendationApiController : ApiControllerBase
    {
        public class InterestRequest
        {
            public List<string> CategoryIds { get; set; }
        }

        // Trang gợi ý giống web: chỉ gợi ý phù hợp, không bù khóa phổ biến.
        [HttpGet, Route("recommendations/personal"), ApiAuthorize]
        public ActionResult PersonalRecommendations(int limit = 12)
        {
            limit = Math.Max(1, Math.Min(limit, 30));
            var model = new RecommendationService(Db).GetPersonalizedPage(CurrentUserId, limit, true);
            if (!model.DaKhoiTao) return Fail(503, "RECOMMENDATIONS_NOT_INSTALLED", "Chức năng gợi ý chưa sẵn sàng.");
            return Ok(new { interactedCourses = model.SoKhoaDaTuongTac, total = model.Items.Count,
                items = new ApiCourseMapper(Db, AbsoluteUrl).Map(model.Items.Select(i => i.Course.maKhoaHoc)), badge = "Phù hợp với bạn" });
        }

        [HttpPost, Route("recommendations/personal/refresh"), ApiAuthorize]
        public ActionResult RefreshPersonalRecommendations(int limit = 12)
        {
            limit = Math.Max(1, Math.Min(limit, 30));
            var service = new RecommendationService(Db);
            var computed = service.Compute(CurrentUserId, limit, false);
            if (!computed.DaKhoiTao) return Fail(503, "RECOMMENDATIONS_NOT_INSTALLED", "Chức năng gợi ý chưa sẵn sàng.");
            return PersonalRecommendations(limit);
        }

        // C1. GET /api/v1/recommendations?limit=
        [HttpGet, Route("recommendations")]
        public ActionResult Recommendations(int limit = 10)
        {
            limit = Math.Max(1, Math.Min(limit, 30));
            int userId = CurrentUserId;
            var mapper = new ApiCourseMapper(Db, AbsoluteUrl);
            var service = new RecommendationService(Db);

            if (service.IsInstalled())
            {
                var model = service.GetForHome(userId, limit, userId > 0);
                string type;
                string reason = null;
                switch (model.CheDo)
                {
                    case CheDoGoiY.TheoKhoaVuaXem:
                        type = "personal";
                        reason = "Vì bạn đã xem \"" + model.TenKhoaHocMoc + "\"";
                        break;
                    case CheDoGoiY.TongHop:
                        type = "personal";
                        reason = "Dựa trên các khóa bạn đã xem, thích, học và đánh giá";
                        break;
                    case CheDoGoiY.TheoSoThich:
                        type = "interest";
                        reason = "Vì bạn quan tâm " + string.Join(", ", model.LinhVucQuanTam.Select(l => l.TenDanhMuc));
                        break;
                    default:
                        type = "popular";
                        break;
                }
                return Ok(new
                {
                    type,
                    reason,
                    items = mapper.Map(model.Items.Select(i => i.Course.maKhoaHoc))
                });
            }

            // Chưa chạy migration 005 (gợi ý): trả khóa có nhiều học viên nhất
            var phoBien = Db.KhoaHoc
                .Where(k => k.trangThai == CourseStatus.DaXuatBan || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                .OrderByDescending(k => Db.DangKyKhoaHoc.Count(d => d.maKhoaHoc == k.maKhoaHoc))
                .Select(k => k.maKhoaHoc).Take(limit).ToList();
            return Ok(new { type = "popular", reason = (string)null, items = mapper.Map(phoBien) });
        }

        // C2. GET /api/v1/interests
        [HttpGet, Route("interests"), ApiAuthorize]
        public ActionResult Interests()
        {
            var service = new InterestService(Db);
            if (!service.IsInstalled())
                return Ok(new { needSurvey = false, maxSelect = 3, selected = new string[0], categories = new object[0] });

            var anh = new CategoryImageService(Db).LayTatCa();
            bool laHocVien = CurrentUser.vaiTro == "HocVien" || CurrentUser.vaiTro == "GiaoVien";
            return Ok(new
            {
                needSurvey = laHocVien && service.ShouldAsk(CurrentUserId),
                maxSelect = 3,
                selected = service.GetSelected(CurrentUserId).Select(l => l.MaDanhMuc).ToList(),
                categories = service.GetChoices().Select(l => new
                {
                    id = l.MaDanhMuc,
                    name = l.TenDanhMuc,
                    imageUrl = anh.ContainsKey(l.MaDanhMuc) ? AbsoluteUrl(anh[l.MaDanhMuc]) : null,
                    coursesCount = l.SoKhoaHoc
                }).ToList()
            });
        }

        // C3. POST /api/v1/interests
        [HttpPost, Route("interests"), ApiAuthorize]
        public ActionResult SaveInterests()
        {
            var req = Body<InterestRequest>();
            var ids = (req.CategoryIds ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (ids.Count == 0) return BadRequest("Vui lòng chọn ít nhất 1 lĩnh vực!");
            if (ids.Count > 3) return BadRequest("Chỉ chọn tối đa 3 lĩnh vực!", "TOO_MANY");

            var service = new InterestService(Db);
            if (!service.IsInstalled()) return Fail(500, "NOT_INSTALLED", "Chức năng sở thích chưa được khởi tạo.");
            var kq = service.Save(CurrentUserId, ids);
            if (!kq.Success) return BadRequest(kq.Message);
            return Ok(null, kq.Message ?? "Đã lưu lĩnh vực quan tâm.");
        }

        // C4. POST /api/v1/interests/skip
        [HttpPost, Route("interests/skip"), ApiAuthorize]
        public ActionResult SkipInterests()
        {
            var service = new InterestService(Db);
            if (service.IsInstalled()) service.Skip(CurrentUserId);
            return Ok(null);
        }

        // D1. POST /api/v1/favorites/{courseId}/toggle
        [HttpPost, Route("favorites/{courseId}/toggle"), ApiAuthorize]
        public ActionResult ToggleFavorite(string courseId)
        {
            var k = Db.KhoaHoc.Where(x => x.maKhoaHoc == courseId).Select(x => new { x.maGiaoVien, x.trangThai }).FirstOrDefault();
            if (k == null || !CourseStatus.IsPublished(k.trangThai))
                return NotFoundJson("Không tìm thấy khóa học hoặc khóa học đã ngừng bán.", "COURSE_NOT_FOUND");
            if (k.maGiaoVien == CurrentUserId) return Forbidden("Bạn không thể thả tim khóa học của chính mình.", "NOT_ALLOWED");
            if (Db.DangKyKhoaHoc.Any(d => d.maHocVien == CurrentUserId && d.maKhoaHoc == courseId))
                return Fail(409, "ALREADY_OWNED", "Bạn đã sở hữu khóa học này, xem ở mục Khóa học của tôi.");

            var kq = new FavoriteService(Db).Toggle(CurrentUserId, courseId);
            if (!kq.Success) return BadRequest(kq.Message);
            return Ok(new { isFavorite = kq.Liked, favoritesCount = kq.Count },
                kq.Liked ? "Đã thêm vào khóa học yêu thích." : "Đã bỏ khỏi khóa học yêu thích.");
        }

        // D2. GET /api/v1/favorites?page=
        [HttpGet, Route("favorites"), ApiAuthorize]
        public ActionResult Favorites(int page = 1, int pageSize = 12)
        {
            page = ChuanHoaTrang(page);
            pageSize = ChuanHoaKichThuoc(pageSize);
            var ids = new FavoriteService(Db).GetFavorites(CurrentUserId).Select(f => f.Course.maKhoaHoc).ToList();
            var trang = ids.Skip((page - 1) * pageSize).Take(pageSize);
            return Ok(Paged(new ApiCourseMapper(Db, AbsoluteUrl).Map(trang), page, pageSize, ids.Count));
        }
    }
}
