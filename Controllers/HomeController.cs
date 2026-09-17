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
            EnsureSampleCoursesSeeded(db);

            List<KhoaHoc> courses = new List<KhoaHoc>();
            try
            {
                courses = db.KhoaHoc.Include(k => k.NguoiDung).Include(k => k.DanhMuc).Include(k => k.DanhGiaKhoaHoc).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch courses error: " + ex.Message);
            }

            return View(courses);
        }

        public static void EnsureSampleCoursesSeeded(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            try
            {
                if (db == null) return;
                var category = db.DanhMuc.FirstOrDefault();
                if (category == null)
                {
                    category = new DanhMuc
                    {
                        maDanhMuc = "DM01",
                        tenDanhMuc = "Lập trình Web & CNTT",
                        moTa = "Các khóa học lập trình Web, phần mềm và CNTT",
                        trangThai = true
                    };
                    db.DanhMuc.Add(category);
                    db.SaveChanges();
                }

                var teacher = db.NguoiDung.FirstOrDefault(u => u.vaiTro == "GiaoVien");
                if (teacher == null)
                {
                    teacher = db.NguoiDung.FirstOrDefault();
                    if (teacher == null)
                    {
                        teacher = new NguoiDung
                        {
                            hoTen = "ThS. Nguyễn Văn An",
                            email = "giao_vien@elearning.vn",
                            matKhau = "123456",
                            vaiTro = "GiaoVien",
                            trangThai = true,
                            anhDaiDien = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80"
                        };
                        db.NguoiDung.Add(teacher);
                        db.SaveChanges();
                    }
                }

                // 1. Khóa học Test01
                var test01 = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == "KH_TEST01" || k.maKhoaHoc == "Test01" || k.maKhoaHoc == "KH_TESTER2026" || k.maKhoaHoc == "KH_WEB2026");
                if (test01 == null)
                {
                    test01 = new KhoaHoc
                    {
                        maKhoaHoc = "KH_TEST01",
                        tenKhoaHoc = "Test01 - Khóa học Thử nghiệm hệ thống 01",
                        moTa = "Khóa học thử nghiệm dùng để kiểm tra dữ liệu, giao diện và luồng logic tính năng trên hệ thống (Mockup Data).",
                        moTaNgan = "Khóa học thử nghiệm 01 phục vụ test luồng hệ thống.",
                        mucTieuDauRa = "Kiểm tra luồng Đăng ký học, Xem chi tiết đề cương, Học bài và Đánh giá tiến độ.",
                        giaBan = 500000,
                        giaKhuyenMai = 299000,
                        trangThai = "DaDuyet",
                        maDanhMuc = category.maDanhMuc,
                        maGiaoVien = teacher.maNguoiDung,
                        capDo = "Mọi cấp độ",
                        anhBia = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80"
                    };
                    db.KhoaHoc.Add(test01);
                }
                else
                {
                    test01.maKhoaHoc = "KH_TEST01";
                    test01.tenKhoaHoc = "Test01 - Khóa học Thử nghiệm hệ thống 01";
                    test01.moTaNgan = "Khóa học thử nghiệm 01 phục vụ test luồng hệ thống.";
                    test01.mucTieuDauRa = "Kiểm tra luồng Đăng ký học, Xem chi tiết đề cương, Học bài và Đánh giá tiến độ.";
                }

                // 2. Khóa học Test02
                var test02 = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == "KH_TEST02" || k.maKhoaHoc == "Test02" || k.maKhoaHoc == "KH_UIUX2026");
                if (test02 == null)
                {
                    test02 = new KhoaHoc
                    {
                        maKhoaHoc = "KH_TEST02",
                        tenKhoaHoc = "Test02 - Khóa học Thử nghiệm hệ thống 02",
                        moTa = "Khóa học thử nghiệm 02 dành cho việc test luồng học bài và giỏ hàng (Mockup Data).",
                        moTaNgan = "Khóa học thử nghiệm 02 phục vụ test luồng hệ thống.",
                        mucTieuDauRa = "Kiểm tra tiến độ học tập và chứng chỉ.",
                        giaBan = 800000,
                        giaKhuyenMai = 0,
                        trangThai = "DaDuyet",
                        maDanhMuc = category.maDanhMuc,
                        maGiaoVien = teacher.maNguoiDung,
                        capDo = "Cơ bản",
                        anhBia = "https://images.unsplash.com/photo-1581291518857-4e27b48ff24e?w=600&auto=format&fit=crop&q=80"
                    };
                    db.KhoaHoc.Add(test02);
                }
                else
                {
                    test02.maKhoaHoc = "KH_TEST02";
                    test02.tenKhoaHoc = "Test02 - Khóa học Thử nghiệm hệ thống 02";
                }

                db.SaveChanges();

                // Seed bài học mẫu (không dùng tên kiểm thử phần mềm)
                SeedCurriculumForTestCourses(db, test01.maKhoaHoc);
                SeedCurriculumForTestCourses(db, test02.maKhoaHoc);

                // Seed đúng 1 đánh giá lưu CSDL cho KH_TEST01 và 0 đánh giá cho KH_TEST02
                EnsureReviewsSeeded(db);

                // Dọn dẹp các khóa học khác không phải KH_TEST01 / KH_TEST02
                var extraCourses = db.KhoaHoc.Where(k => k.maKhoaHoc != "KH_TEST01" && k.maKhoaHoc != "KH_TEST02").ToList();
                if (extraCourses.Count > 0)
                {
                    foreach (var c in extraCourses)
                    {
                        var cartDetails = db.ChiTietDonHang.Where(cd => cd.maKhoaHoc == c.maKhoaHoc).ToList();
                        if (cartDetails.Count > 0) db.ChiTietDonHang.RemoveRange(cartDetails);

                        var enrollments = db.DangKyKhoaHoc.Where(d => d.maKhoaHoc == c.maKhoaHoc).ToList();
                        if (enrollments.Count > 0) db.DangKyKhoaHoc.RemoveRange(enrollments);

                        var reviews = db.DanhGiaKhoaHoc.Where(r => r.maKhoaHoc == c.maKhoaHoc).ToList();
                        if (reviews.Count > 0) db.DanhGiaKhoaHoc.RemoveRange(reviews);

                        var prog = db.TienDoHocTap.Where(t => t.maKhoaHoc == c.maKhoaHoc).ToList();
                        if (prog.Count > 0) db.TienDoHocTap.RemoveRange(prog);

                        db.KhoaHoc.Remove(c);
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureSampleCoursesSeeded Exception: " + ex.Message);
            }
        }

        public static void EnsureReviewsSeeded(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            try
            {
                if (db == null) return;

                var student = db.NguoiDung.FirstOrDefault(u => u.email == "anh675262@gmail.com") 
                           ?? db.NguoiDung.FirstOrDefault(u => u.vaiTro == "HocVien") 
                           ?? db.NguoiDung.FirstOrDefault();

                if (student != null)
                {
                    // 1. Đảm bảo Đăng ký khóa học tồn tại (đáp ứng điều kiện Trigger)
                    var enrollment = db.DangKyKhoaHoc.FirstOrDefault(d => d.maHocVien == student.maNguoiDung && d.maKhoaHoc == "KH_TEST01");
                    if (enrollment == null)
                    {
                        db.DangKyKhoaHoc.Add(new DangKyKhoaHoc
                        {
                            maDangKy = "DK_TEST01_01",
                            maHocVien = student.maNguoiDung,
                            maKhoaHoc = "KH_TEST01",
                            ngayDangKy = DateTime.Now,
                            trangThaiDK = "DaThanhToan"
                        });
                        db.SaveChanges();
                    }

                    // 2. Đảm bảo Tiến độ học tập >= 25% (đáp ứng điều kiện Trigger trg_DanhGia_KiemTraDieuKien)
                    var progress = db.TienDoHocTap.FirstOrDefault(t => t.maHocVien == student.maNguoiDung && t.maKhoaHoc == "KH_TEST01");
                    if (progress == null)
                    {
                        db.TienDoHocTap.Add(new TienDoHocTap
                        {
                            maTienDo = "TD_TEST01_01",
                            maHocVien = student.maNguoiDung,
                            maKhoaHoc = "KH_TEST01",
                            phanTramHoanThanh = 100,
                            lanTruyCapGanNhat = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                    else if (progress.phanTramHoanThanh < 25)
                    {
                        progress.phanTramHoanThanh = 100;
                        db.SaveChanges();
                    }

                    // 3. Thêm bản ghi Đánh giá khóa học cho KH_TEST01
                    var reviewTest01 = db.DanhGiaKhoaHoc.FirstOrDefault(r => r.maKhoaHoc == "KH_TEST01");
                    if (reviewTest01 == null)
                    {
                        db.DanhGiaKhoaHoc.Add(new DanhGiaKhoaHoc
                        {
                            maDanhGia = "DG_TEST01_01",
                            maKhoaHoc = "KH_TEST01",
                            maHocVien = student.maNguoiDung,
                            NguoiDung = student,
                            soSao = 5,
                            noiDung = "Khóa học thử nghiệm giao diện rất tuyệt vời, luồng học bài và thao tác mượt mà!",
                            ngayDanhGia = DateTime.Now
                        });
                        db.SaveChanges();
                    }
                }

                var extraReviews = db.DanhGiaKhoaHoc.Where(r => r.maKhoaHoc == "KH_TEST02").ToList();
                if (extraReviews.Count > 0)
                {
                    db.DanhGiaKhoaHoc.RemoveRange(extraReviews);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureReviewsSeeded Exception: " + ex.Message);
            }
        }

        public static void SeedCurriculumForTestCourses(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string courseId)
        {
            try
            {
                if (db == null) return;
                if (db.ChuongHoc.Any(c => c.maKhoaHoc == courseId)) return;

                if (courseId.Contains("TEST01") || courseId.Contains("01"))
                {
                    // Chương 1
                    var ch1 = new ChuongHoc
                    {
                        maChuong = "CH_" + courseId + "_01",
                        tenChuong = "Chương 1: Chương học thử nghiệm 01",
                        thuTu = 1,
                        maKhoaHoc = courseId
                    };
                    db.ChuongHoc.Add(ch1);
                    db.SaveChanges();

                    db.BaiHoc.Add(new BaiHoc
                    {
                        maBaiHoc = "BH_" + courseId + "_01",
                        tenBaiHoc = "Bài 1: Bài học thử nghiệm 1.1",
                        thoiLuong = 300,
                        thuTu = 1,
                        maChuong = ch1.maChuong,
                        duongDanVideo = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4",
                        choXemThu = true
                    });

                    db.BaiHoc.Add(new BaiHoc
                    {
                        maBaiHoc = "BH_" + courseId + "_02",
                        tenBaiHoc = "Bài 2: Bài học thử nghiệm 1.2 (Video đang phát triển)",
                        thoiLuong = 450,
                        thuTu = 2,
                        maChuong = ch1.maChuong,
                        duongDanVideo = null, // Video đang phát triển
                        choXemThu = false
                    });

                    // Chương 2
                    var ch2 = new ChuongHoc
                    {
                        maChuong = "CH_" + courseId + "_02",
                        tenChuong = "Chương 2: Chương học thử nghiệm 02",
                        thuTu = 2,
                        maKhoaHoc = courseId
                    };
                    db.ChuongHoc.Add(ch2);
                    db.SaveChanges();

                    db.BaiHoc.Add(new BaiHoc
                    {
                        maBaiHoc = "BH_" + courseId + "_03",
                        tenBaiHoc = "Bài 3: Bài học thử nghiệm 2.1 (Video đang phát triển)",
                        thoiLuong = 600,
                        thuTu = 1,
                        maChuong = ch2.maChuong,
                        duongDanVideo = null, // Video đang phát triển
                        choXemThu = false
                    });

                    db.SaveChanges();

                    // Seed đúng 1 đánh giá mẫu duy nhất cho KH_TEST01
                    var oldReviews = db.DanhGiaKhoaHoc.Where(r => r.maKhoaHoc == courseId).ToList();
                    if (oldReviews.Count != 1)
                    {
                        if (oldReviews.Count > 0) db.DanhGiaKhoaHoc.RemoveRange(oldReviews);

                        var student = db.NguoiDung.FirstOrDefault(u => u.vaiTro == "HocVien") ?? db.NguoiDung.FirstOrDefault();
                        if (student != null)
                        {
                            db.DanhGiaKhoaHoc.Add(new DanhGiaKhoaHoc
                            {
                                maDanhGia = "DG_" + courseId + "_01",
                                maKhoaHoc = courseId,
                                maHocVien = student.maNguoiDung,
                                soSao = 5,
                                noiDung = "Khóa học thử nghiệm giao diện tuyệt vời, luồng học bài và thao tác mượt mà!",
                                ngayDanhGia = DateTime.Now
                            });
                            db.SaveChanges();
                        }
                    }
                }
                else
                {
                    // Course Test02: Đảm bảo 0 đánh giá
                    var oldReviews = db.DanhGiaKhoaHoc.Where(r => r.maKhoaHoc == courseId).ToList();
                    if (oldReviews.Count > 0)
                    {
                        db.DanhGiaKhoaHoc.RemoveRange(oldReviews);
                        db.SaveChanges();
                    }

                    var ch1 = new ChuongHoc
                    {
                        maChuong = "CH_" + courseId + "_01",
                        tenChuong = "Chương 1: Chương học thử nghiệm 01",
                        thuTu = 1,
                        maKhoaHoc = courseId
                    };
                    db.ChuongHoc.Add(ch1);
                    db.SaveChanges();

                    db.BaiHoc.Add(new BaiHoc
                    {
                        maBaiHoc = "BH_" + courseId + "_01",
                        tenBaiHoc = "Bài 1: Bài học demo 1.1 (Video đang phát triển)",
                        thoiLuong = 360,
                        thuTu = 1,
                        maChuong = ch1.maChuong,
                        duongDanVideo = null, // Video đang phát triển
                        choXemThu = true
                    });

                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SeedCurriculum Exception: " + ex.Message);
            }
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
