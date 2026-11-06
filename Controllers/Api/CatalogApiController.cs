using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.Services.Api;

namespace ECOLearning.Controllers.Api
{
    /// <summary>
    /// Nhóm B – Danh mục, khóa học, tìm kiếm (Task_Tuan10.docx mục 6.2) và E1 – gửi đánh giá (mục 6.5).
    /// Danh sách chỉ gồm khóa đang mở bán; người đã đăng nhập không thấy khóa đã sở hữu / do mình dạy (giống website).
    /// </summary>
    [RoutePrefix("api/v1")]
    public class CatalogApiController : ApiControllerBase
    {
        private static readonly string[] TrangThaiDangBan = { CourseStatus.DaXuatBan, "Đã duyệt", "Active", "Đang hoạt động" };

        private ApiCourseMapper Mapper => new ApiCourseMapper(Db, AbsoluteUrl);

        public class ClickRequest
        {
            public string Source { get; set; }
        }

        public class ReviewRequest
        {
            public int Rating { get; set; }
            public string Comment { get; set; }
        }

        // B1. GET /api/v1/categories
        [HttpGet, Route("categories")]
        public ActionResult Categories()
        {
            var anh = new CategoryImageService(Db).LayTatCa();
            var soKhoa = Db.KhoaHoc.Where(k => TrangThaiDangBan.Contains(k.trangThai) && k.maDanhMuc != null)
                .GroupBy(k => k.maDanhMuc).Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionary(x => x.Key, x => x.SoLuong);
            var data = Db.DanhMuc.Where(d => d.trangThai == null || d.trangThai == true)
                .OrderBy(d => d.maDanhMuc).ToList()
                .Select(d => new
                {
                    id = d.maDanhMuc,
                    name = d.tenDanhMuc,
                    description = d.moTa,
                    imageUrl = anh.ContainsKey(d.maDanhMuc) ? AbsoluteUrl(anh[d.maDanhMuc]) : null,
                    coursesCount = soKhoa.ContainsKey(d.maDanhMuc) ? soKhoa[d.maDanhMuc] : 0
                }).ToList();
            return Ok(data);
        }

        // B2. GET /api/v1/courses?q=&categoryId=&level=&price=free|paid&minRating=&instructorId=&onSale=&sort=&page=&pageSize=
        // onSale=true: chỉ khóa đang được giảm trực tiếp; sort=discount: % giảm nhiều nhất trước (khóa không giảm xếp cuối)
        [HttpGet, Route("courses")]
        public ActionResult Courses(string q = null, string categoryId = null, string level = null, string price = null,
            double? minRating = null, string sort = null, int page = 1, int pageSize = 12, int? instructorId = null, bool onSale = false, bool webCatalog = false)
        {
            page = ChuanHoaTrang(page);
            pageSize = ChuanHoaKichThuoc(pageSize);
            int userId = CurrentUserId;

            var query = Db.KhoaHoc.Where(k => TrangThaiDangBan.Contains(k.trangThai));
            if (webCatalog)
                query = query.Where(k => k.trangThai == CourseStatus.DaXuatBan || k.trangThai == "Đã duyệt" || k.trangThai == "Active");
            if (userId > 0)
            {
                query = query.Where(k => !Db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId));
                if (!webCatalog) query = query.Where(k => k.maGiaoVien != userId);
            }
            // Khóa của một giáo viên (bấm thẻ "Giáo viên nổi bật" / kết quả tìm giáo viên)
            if (instructorId.HasValue)
                query = query.Where(k => k.maGiaoVien == instructorId.Value);

            // Từ khóa: xếp hạng bằng bộ tìm kiếm của website (không dấu, gõ sai nhẹ), giữ thứ tự liên quan
            List<string> thuTuLienQuan = null;
            if (!string.IsNullOrWhiteSpace(q))
            {
                if (webCatalog)
                {
                    string term = q.Trim().ToLower();
                    query = query.Where(k => k.tenKhoaHoc.ToLower().Contains(term)
                        || (k.NguoiDung != null && k.NguoiDung.hoTen.ToLower().Contains(term))
                        || (k.moTa != null && k.moTa.ToLower().Contains(term))
                        || (k.moTaNgan != null && k.moTaNgan.ToLower().Contains(term))
                        || (k.DanhMuc != null && k.DanhMuc.tenDanhMuc.ToLower().Contains(term)));
                }
                else
                {
                    var search = new SearchService(Db);
                    bool motPhan;
                    thuTuLienQuan = search.XepHang(search.LayChiMuc(), q.Trim(), search.BoQuaKhoaDaSoHuu(userId), out motPhan)
                        .Select(x => x.Key.MaKhoaHoc).ToList();
                    var tapMa = thuTuLienQuan;
                    query = query.Where(k => tapMa.Contains(k.maKhoaHoc));
                }
            }
            if (!string.IsNullOrWhiteSpace(categoryId)) query = query.Where(k => k.maDanhMuc == categoryId);
            if (!string.IsNullOrWhiteSpace(level)) query = query.Where(k => k.capDo == level);
            // Lọc free / paid theo giá gốc (giaBan); giá hiệu lực sau giảm trực tiếp do CourseDiscountService tính
            if (price == "free") query = query.Where(k => k.giaBan == 0);
            else if (price == "paid") query = query.Where(k => k.giaBan > 0);
            if (minRating.HasValue && minRating.Value > 0)
            {
                double diem = minRating.Value;
                query = query.Where(k => k.DanhGiaKhoaHoc.Any() && k.DanhGiaKhoaHoc.Average(d => (double)d.soSao) >= diem);
            }

            Dictionary<string, GiaHieuLuc> dangGiam = null;
            if (onSale || sort == "discount")
            {
                dangGiam = query.Select(k => new { k.maKhoaHoc, k.giaBan, k.maDanhMuc }).ToList()
                    .Select(k => new { k.maKhoaHoc, Gia = CourseDiscountService.Tinh(k.giaBan, k.maKhoaHoc, k.maDanhMuc) })
                    .Where(x => x.Gia.DangGiam)
                    .ToDictionary(x => x.maKhoaHoc, x => x.Gia);
                if (onSale)
                {
                    var tapGiam = dangGiam.Keys.ToList();
                    query = query.Where(k => tapGiam.Contains(k.maKhoaHoc));
                }
            }

            int total = query.Count();
            List<string> ids;
            if (sort == "discount")
            {
                ids = query.Select(k => k.maKhoaHoc).ToList()
                    .OrderByDescending(id => dangGiam.ContainsKey(id) ? dangGiam[id].PhanTram : 0)
                    .ThenByDescending(id => dangGiam.ContainsKey(id) ? dangGiam[id].TienGiam : 0m)
                    .ThenByDescending(id => id)
                    .Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            else if (thuTuLienQuan != null && string.IsNullOrWhiteSpace(sort))
            {
                var conLai = new HashSet<string>(query.Select(k => k.maKhoaHoc).ToList());
                ids = thuTuLienQuan.Where(conLai.Contains).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            else
            {
                IOrderedQueryable<KhoaHoc> sapXep;
                switch (sort)
                {
                    case "newest":
                        // Same release date and tie-breaker as HomeSectionService.MoiRaMat.
                        sapXep = query.OrderByDescending(k => Db.KiemDuyetKhoaHoc
                            .Where(d => d.maKhoaHoc == k.maKhoaHoc && d.trangThai == "Đã duyệt" && d.ngayKiemDuyet != null)
                            .Select(d => d.ngayKiemDuyet).Min())
                            .ThenBy(k => k.maKhoaHoc);
                        break;
                    case "rating":
                        sapXep = query.OrderByDescending(k => k.DanhGiaKhoaHoc.Average(d => (double?)d.soSao) ?? 0)
                            .ThenByDescending(k => k.DanhGiaKhoaHoc.Count()).ThenByDescending(k => k.maKhoaHoc);
                        break;
                    case "price_asc":
                        sapXep = query.OrderBy(k => k.giaBan).ThenByDescending(k => k.maKhoaHoc);
                        break;
                    case "price_desc":
                        sapXep = query.OrderByDescending(k => k.giaBan).ThenByDescending(k => k.maKhoaHoc);
                        break;
                    default: // popular
                        sapXep = query.OrderByDescending(k => Db.DangKyKhoaHoc.Count(d => d.maKhoaHoc == k.maKhoaHoc))
                            .ThenByDescending(k => k.maKhoaHoc);
                        break;
                }
                ids = sapXep.Select(k => k.maKhoaHoc).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            }
            return Ok(Paged(Mapper.Map(ids), page, pageSize, total));
        }

        // B3. GET /api/v1/courses/{id}
        [HttpGet, Route("courses/{id}")]
        public ActionResult Detail(string id)
        {
            var k = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == id);
            if (k == null || !TrangThaiDangBan.Contains(k.trangThai))
                return NotFoundJson("Không tìm thấy khóa học hoặc khóa học đã ngừng bán.", "COURSE_NOT_FOUND");

            var the = Mapper.MapOne(k.maKhoaHoc);
            int userId = CurrentUserId;
            bool laChu = userId > 0 && k.maGiaoVien == userId;
            bool daSoHuu = userId > 0 && Db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId);

            // Ghi lượt xem cho thuật toán gợi ý: mỗi khóa cộng tối đa 1 lần / 30 phút / người (website: 1 lần / phiên)
            if (userId > 0 && !laChu && CurrentUser != null && (CurrentUser.vaiTro == "HocVien" || CurrentUser.vaiTro == "GiaoVien"))
            {
                string khoaDem = "api-view:" + userId + ":" + k.maKhoaHoc;
                bool daDem = HttpRuntime.Cache[khoaDem] != null;
                if (new RecommendationService(Db).GhiLuotXem(userId, k.maKhoaHoc, !daDem) && !daDem)
                    HttpRuntime.Cache.Insert(khoaDem, true, null, DateTime.Now.AddMinutes(30), Cache.NoSlidingExpiration);
            }

            List<BaiHoc> baiKhongChuong;
            var chuongGoc = new LearningService(Db).LayChuong(k.maKhoaHoc, out baiKhongChuong);
            var maBaiKhoa = chuongGoc.SelectMany(c => c.BaiHoc ?? new List<BaiHoc>()).Concat(baiKhongChuong).Select(b => b.maBaiHoc).ToList();
            int soTaiLieu = new LessonDocumentService(Db).DemTheoBai(maBaiKhoa).Values.Sum();
            var chuong = chuongGoc
                .Select(c => new
                {
                    id = c.maChuong,
                    title = c.tenChuong,
                    lessons = (c.BaiHoc ?? new List<BaiHoc>()).OrderBy(b => b.thuTu).Select(BaiTomTat).ToList()
                }).ToList();
            if (baiKhongChuong.Count > 0)
                chuong.Add(new { id = "_", title = "Bài học khác", lessons = baiKhongChuong.Select(BaiTomTat).ToList() });

            bool yeuThich = false;
            object danhGiaCuaToi = null;
            bool coTheDanhGia = false;
            if (userId > 0)
            {
                try
                {
                    yeuThich = new FavoriteService(Db).GetLikedIds(userId).Contains(k.maKhoaHoc);
                }
                catch
                {
                    yeuThich = false;
                }
                var dg = Db.DanhGiaKhoaHoc.FirstOrDefault(d => d.maHocVien == userId && d.maKhoaHoc == k.maKhoaHoc);
                if (dg != null) danhGiaCuaToi = new { rating = dg.soSao, comment = dg.noiDung, createdAt = ApiFormat.Date(dg.ngayDanhGia) };
                coTheDanhGia = daSoHuu && CoTheDanhGia(k.maKhoaHoc, userId);
            }

            var gv = Db.NguoiDung.Find(k.maGiaoVien);
            return Ok(new
            {
                the.Id,
                the.Title,
                the.CategoryId,
                the.Category,
                the.Instructor,
                the.InstructorAvatar,
                the.ImageUrl,
                the.Level,
                the.Rating,
                the.ReviewCount,
                the.Price,
                the.OriginalPrice,
                the.Promotion,
                the.IsFree,
                the.LessonsCount,
                the.DurationSeconds,
                the.StudentsCount,
                the.Tag,
                documentsCount = soTaiLieu,
                shortDescription = k.moTaNgan,
                description = k.moTa,
                outcomes = TachMucTieu(k.mucTieuDauRa),
                chapters = chuong,
                instructorInfo = gv == null ? null : new
                {
                    id = gv.maNguoiDung,
                    name = gv.hoTen,
                    avatarUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(gv.anhDaiDien) ? ApiCourseMapper.AnhDaiDienMacDinh : gv.anhDaiDien)
                },
                isOwner = laChu,
                isEnrolled = daSoHuu,
                isFavorite = yeuThich,
                canReview = coTheDanhGia,
                myReview = danhGiaCuaToi
            });
        }

        private static object BaiTomTat(BaiHoc b) => new
        {
            id = b.maBaiHoc,
            title = b.tenBaiHoc,
            durationSeconds = b.thoiLuong ?? 0,
            isPreview = b.choXemThu == true
        };

        private static List<string> TachMucTieu(string s) => CourseOutcomes.Tach(s);

        /// <summary>Được đánh giá khi đã học đủ bài và đạt quiz cuối khóa (cùng quy tắc ReviewController của website).</summary>
        private bool CoTheDanhGia(string courseId, int userId)
        {
            var quiz = new QuizService(Db);
            var state = quiz.Installed() ? quiz.Student(courseId, userId) : null;
            return state != null && state.LessonsDone && state.Passed;
        }

        // B4. GET /api/v1/courses/{id}/reviews?stars=&page=
        [HttpGet, Route("courses/{id}/reviews")]
        public ActionResult Reviews(string id, int? stars = null, int page = 1)
        {
            var k = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == id);
            if (k == null || !TrangThaiDangBan.Contains(k.trangThai)) return NotFoundJson("Không tìm thấy khóa học.", "COURSE_NOT_FOUND");
            page = ChuanHoaTrang(page);
            if (stars.HasValue && (stars < 1 || stars > 5)) stars = null;

            var ds = new CourseReviewService(Db).LayDanhSach(k, CurrentUserId, stars, null, "moinhat", page,
                ECOLearning.ViewModels.DanhSachDanhGiaViewModel.SoMoiTrang);
            return Ok(new
            {
                summary = new
                {
                    average = Math.Round(ds.DiemTrungBinh, 1),
                    total = ds.TongSo,
                    stars = Enumerable.Range(1, 5).ToDictionary(s => s.ToString(), s => ds.SoTheoSao.Length > s ? ds.SoTheoSao[s] : 0)
                },
                items = ds.Items.Select(r => new
                {
                    id = r.MaDanhGia,
                    userName = r.TenHocVien,
                    userAvatar = AbsoluteUrl(string.IsNullOrWhiteSpace(r.AnhDaiDien) ? ApiCourseMapper.AnhDaiDienMacDinh : r.AnhDaiDien),
                    rating = r.SoSao,
                    comment = r.NoiDung,
                    createdAt = ApiFormat.Date(r.NgayDanhGia),
                    helpfulCount = r.SoHuuIch,
                    notHelpfulCount = r.SoKhongHuuIch,
                    // Bình chọn của người đang xem (E2): true / false / null (chưa chọn hoặc chưa đăng nhập)
                    myVote = r.BinhChonCuaToi.HasValue ? (bool?)(r.BinhChonCuaToi.Value == 1) : null,
                    isMine = CurrentUserId > 0 && r.MaHocVien == CurrentUserId,
                    reply =r.CoPhanHoi ? new { content = r.PhanHoiNoiDung, createdAt = ApiFormat.Date(r.PhanHoiNgayCapNhat ?? r.PhanHoiNgayTao) } : null
                }).ToList(),
                page = ds.Trang,
                pageSize = ECOLearning.ViewModels.DanhSachDanhGiaViewModel.SoMoiTrang,
                total = ds.SoKetQua,
                totalPages = ds.SoTrang
            });
        }

        // E1. POST /api/v1/courses/{id}/reviews
        [HttpPost, Route("courses/{id}/reviews"), ApiAuthorize]
        public ActionResult SubmitReview(string id)
        {
            var req = Body<ReviewRequest>();
            if (req.Rating < 1 || req.Rating > 5)
                return BadRequest("Vui lòng chọn số sao từ 1 đến 5!", "INVALID_RATING", new Dictionary<string, string> { { "rating", "Vui lòng chọn số sao từ 1 đến 5!" } });
            string noiDung = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
            if (noiDung != null && noiDung.Length > 2000)
                return BadRequest("Nhận xét tối đa 2000 ký tự!", "INVALID_DATA", new Dictionary<string, string> { { "comment", "Nhận xét tối đa 2000 ký tự!" } });

            var k = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == id);
            if (k == null) return NotFoundJson("Không tìm thấy khóa học.", "COURSE_NOT_FOUND");
            int userId = CurrentUserId;
            if (!CoTheDanhGia(id, userId))
                return Forbidden("Bạn cần hoàn thành tất cả bài học và đạt quiz cuối khóa trước khi đánh giá.");

            var dg = Db.DanhGiaKhoaHoc.FirstOrDefault(d => d.maHocVien == userId && d.maKhoaHoc == id);
            if (dg != null)
            {
                dg.soSao = req.Rating;
                dg.noiDung = noiDung;
                dg.ngayDanhGia = DateTime.Now;
            }
            else
            {
                dg = new DanhGiaKhoaHoc
                {
                    maDanhGia = "DG" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999),
                    soSao = req.Rating,
                    noiDung = noiDung,
                    ngayDanhGia = DateTime.Now,
                    maHocVien = userId,
                    maKhoaHoc = id
                };
                Db.DanhGiaKhoaHoc.Add(dg);
            }
            Db.SaveChanges();

            var the = Mapper.MapOne(id);
            return Ok(new
            {
                review = new { id = dg.maDanhGia, rating = dg.soSao, comment = dg.noiDung, createdAt = ApiFormat.Date(dg.ngayDanhGia) },
                courseRating = the?.Rating ?? 0,
                courseReviewCount = the?.ReviewCount ?? 0
            }, "Cảm ơn bạn đã đánh giá khóa học!");
        }

        // DELETE /api/v1/courses/{id}/reviews: only the authenticated student's review.
        [HttpDelete, Route("courses/{id}/reviews"), ApiAuthorize]
        public ActionResult DeleteMyReview(string id)
        {
            int userId = CurrentUserId;
            var reviews = Db.DanhGiaKhoaHoc.Where(d => d.maHocVien == userId && d.maKhoaHoc == id).ToList();
            if (reviews.Count > 0)
            {
                Db.DanhGiaKhoaHoc.RemoveRange(reviews);
                Db.SaveChanges();
            }
            return Ok(new { deleted = true }, "Đã xóa đánh giá của bạn.");
        }

        public class HelpfulRequest
        {
            public bool? Helpful { get; set; }
        }

        // E2. POST /api/v1/reviews/{reviewId}/helpful  { helpful: true | false }
        // "Bạn thấy hữu ích?" như Review/Helpful của website: gửi lại đúng lựa chọn đang có thì bỏ bình chọn
        [HttpPost, Route("reviews/{reviewId}/helpful"), ApiAuthorize]
        public ActionResult ReviewHelpful(string reviewId)
        {
            var req = Body<HelpfulRequest>();
            if (!req.Helpful.HasValue) return BadRequest("Vui lòng chọn Hữu ích hoặc Không hữu ích.");

            var service = new CourseReviewService(Db);
            if (!service.IsInstalled()) return Fail(503, "NOT_INSTALLED", "Chức năng chưa sẵn sàng (cần chạy migration 011).");

            var dg = Db.DanhGiaKhoaHoc.Where(d => d.maDanhGia == reviewId).Select(d => new { d.maHocVien }).FirstOrDefault();
            if (dg == null) return NotFoundJson("Không tìm thấy đánh giá.", "REVIEW_NOT_FOUND");
            if (dg.maHocVien == CurrentUserId) return Forbidden("Bạn không thể tự bình chọn đánh giá của mình.", "OWN_REVIEW");

            var kq = service.BinhChon(reviewId, CurrentUserId, req.Helpful.Value);
            if (!kq.Success) return BadRequest(kq.Message);
            return Ok(new
            {
                reviewId,
                helpfulCount = kq.SoHuuIch,
                notHelpfulCount = kq.SoKhongHuuIch,
                myVote = kq.BinhChon.HasValue ? (bool?)(kq.BinhChon.Value == 1) : null
            }, kq.BinhChon.HasValue ? "Cảm ơn bạn đã bình chọn!" : "Đã bỏ bình chọn.");
        }

        // B5. GET /api/v1/search/suggest?q=
        [HttpGet, Route("search/suggest")]
        public ActionResult Suggest(string q = null)
        {
            var search = new SearchService(Db);
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { keywords = search.TuKhoaPhoBien(SearchService.SoTuKhoaGoiY), courses = new List<CourseSummaryDto>(), teachers = new List<object>() });
            string cau = q.Trim();
            if (cau.Length > SearchService.DoDaiToiDa) cau = cau.Substring(0, SearchService.DoDaiToiDa);
            var kq = search.GoiY(cau, CurrentUserId);
            return Ok(new
            {
                keywords = kq.TuKhoa,
                courses = Mapper.Map(kq.KhoaHoc.Select(x => x.MaKhoaHoc)),
                teachers = kq.GiaoVien.Select(x => TeacherJson(x.GiaoVien, x.GanDung)).ToList()
            });
        }

        // ------------------------------------------------------------------ Giáo viên (cùng bộ tìm kiếm với website, SearchService.TimGiaoVien)

        /// <summary>Thẻ giáo viên. isApproximate: true = tên chỉ gần giống (gõ sai / gõ theo cách đọc), app hiện "Có phải bạn muốn tìm…".</summary>
        private object TeacherJson(SearchService.GiaoVienChiMuc g, bool? ganDung)
        {
            return new
            {
                id = g.MaGiaoVien,
                fullName = g.HoTen,
                avatarUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(g.AnhDaiDien) ? ApiCourseMapper.AnhDaiDienMacDinh : g.AnhDaiDien),
                specialty = string.IsNullOrWhiteSpace(g.ChuyenMon) ? null : g.ChuyenMon,
                coursesCount = g.SoKhoaHoc,
                studentsCount = g.SoHocVien,
                rating = Math.Round(g.DiemDanhGia, 1),
                reviewCount = g.SoDanhGia,
                isApproximate = ganDung
            };
        }

        // B9. GET /api/v1/teachers/search?q=&limit= : giáo viên theo tên, gõ thiếu dấu / sai chính tả / theo cách đọc vẫn tìm được
        [HttpGet, Route("teachers/search")]
        public ActionResult SearchTeachers(string q = null, int limit = 20)
        {
            limit = Math.Max(1, Math.Min(limit, SearchService.SoGiaoVienKetQua));
            string cau = (q ?? "").Trim();
            if (cau.Length > SearchService.DoDaiToiDa) cau = cau.Substring(0, SearchService.DoDaiToiDa);
            if (SearchText.KhongDau(cau).Length == 0)
                return Ok(new { query = cau, total = 0, isApproximate = false, items = new List<object>() });

            var search = new SearchService(Db);
            var kq = search.TimGiaoVien(search.LayChiMuc(), cau, limit);
            return Ok(new
            {
                query = cau,
                total = kq.Count,
                // Không ai khớp đúng, chỉ có người tên gần giống: hiện "Có phải bạn muốn tìm giáo viên…?" (giống trang tìm kiếm web)
                isApproximate = kq.Count > 0 && kq.All(x => x.GanDung),
                items = kq.Select(x => TeacherJson(x.GiaoVien, x.GanDung)).ToList()
            });
        }

        // B10. GET /api/v1/teachers/{id} : hồ sơ công khai của giáo viên + các khóa đang bán (không trả email, số điện thoại, tài khoản ngân hàng)
        [HttpGet, Route("teachers/{id:int}")]
        public ActionResult Teacher(int id)
        {
            var u = Db.NguoiDung.FirstOrDefault(x => x.maNguoiDung == id && x.vaiTro == "GiaoVien" && x.trangThai != false);
            if (u == null) return NotFoundJson("Không tìm thấy giáo viên.", "TEACHER_NOT_FOUND");

            var hoSo = new InstructorProfileService(Db).LayHoSo(id);
            var maKhoa = Db.KhoaHoc.Where(k => k.maGiaoVien == id && TrangThaiDangBan.Contains(k.trangThai)).Select(k => k.maKhoaHoc).ToList();
            var khoa = Mapper.Map(maKhoa).OrderByDescending(c => c.StudentsCount).ThenByDescending(c => c.Rating).ThenBy(c => c.Id).ToList();
            return Ok(new
            {
                id = u.maNguoiDung,
                fullName = u.hoTen,
                avatarUrl = AbsoluteUrl(string.IsNullOrWhiteSpace(u.anhDaiDien) ? ApiCourseMapper.AnhDaiDienMacDinh : u.anhDaiDien),
                specialty = hoSo == null || string.IsNullOrWhiteSpace(hoSo.ChuyenMon) ? null : hoSo.ChuyenMon,
                experience = hoSo == null || string.IsNullOrWhiteSpace(hoSo.KinhNghiem) ? null : hoSo.KinhNghiem,
                coursesCount = khoa.Count,
                studentsCount = hoSo?.TongSoHocVien ?? khoa.Sum(c => c.StudentsCount),
                rating = Math.Round(hoSo?.DiemDanhGiaTrungBinh ?? 0, 1),
                reviewCount = hoSo?.TongLuotDanhGia ?? 0,
                courses = khoa
            });
        }

        // B6. POST /api/v1/courses/{id}/click
        [HttpPost, Route("courses/{id}/click"), ApiAuthorize]
        public ActionResult Click(string id)
        {
            var req = Body<ClickRequest>();
            if (!Db.KhoaHoc.Any(k => k.maKhoaHoc == id)) return NotFoundJson("Không tìm thấy khóa học.", "COURSE_NOT_FOUND");
            var k2 = Db.KhoaHoc.Where(k => k.maKhoaHoc == id).Select(k => k.maGiaoVien).First();
            if (k2 != CurrentUserId) new RecommendationService(Db).GhiLuotClick(CurrentUserId, id);
            return Ok(new { recorded = k2 != CurrentUserId, source = req.Source });
        }

        // B7. GET /api/v1/courses/{id}/similar?limit=
        [HttpGet, Route("courses/{id}/similar")]
        public ActionResult Similar(string id, int limit = 8)
        {
            limit = Math.Max(1, Math.Min(limit, 20));
            var k = Db.KhoaHoc.FirstOrDefault(x => x.maKhoaHoc == id);
            if (k == null) return NotFoundJson("Không tìm thấy khóa học.", "COURSE_NOT_FOUND");
            int userId = CurrentUserId;

            // Item-based trên dữ liệu mua của website ("mua cùng nhau", "học viên khác cũng mua")
            var ids = new List<string>();
            try
            {
                var lq = new RelatedCourseService(Db).LayChoKhoaHoc(id, userId);
                ids.AddRange(lq.MuaCungNhau.Concat(lq.CungMua).Select(x => x.The.Course.maKhoaHoc));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("API similar: " + ex.Message);
            }
            ids = ids.Where(x => x != id).Distinct().ToList();

            // Chưa đủ: bù khóa cùng lĩnh vực được nhiều học viên học nhất
            if (ids.Count < limit)
            {
                var bu = Db.KhoaHoc
                    .Where(x => x.maKhoaHoc != id && x.maDanhMuc == k.maDanhMuc && TrangThaiDangBan.Contains(x.trangThai)
                        && x.maGiaoVien != userId && !Db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == x.maKhoaHoc && d.maHocVien == userId))
                    .OrderByDescending(x => Db.DangKyKhoaHoc.Count(d => d.maKhoaHoc == x.maKhoaHoc))
                    .ThenByDescending(x => x.maKhoaHoc)
                    .Select(x => x.maKhoaHoc).Take(limit * 2).ToList();
                ids.AddRange(bu.Where(x => !ids.Contains(x)));
            }
            return Ok(Mapper.Map(ids.Take(limit)));
        }

        // B8. GET /api/v1/courses/{id}/preview/{lessonId}
        [HttpGet, Route("courses/{id}/preview/{lessonId}")]
        public ActionResult Preview(string id, string lessonId)
        {
            var bai = new LearningService(Db).LayBaiHoc(id).FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (bai == null) return NotFoundJson("Không tìm thấy bài học.", "LESSON_NOT_FOUND");
            if (bai.choXemThu != true) return Forbidden("Bài học này không cho xem thử.", "NOT_PREVIEW");
            string url, loai;
            VideoCuaBai(bai.duongDanVideo, out url, out loai);
            return Ok(new { id = bai.maBaiHoc, title = bai.tenBaiHoc, videoUrl = url, videoType = loai, durationSeconds = bai.thoiLuong ?? 0 });
        }
    }
}
