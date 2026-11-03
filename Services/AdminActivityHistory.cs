using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    // Read existing decisions from their source tables, including decisions made before central auditing.
    public static class AdminActivityHistory
    {
        private static bool Column(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string table, string column)
            => db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH(@p0,@p1) IS NULL THEN 0 ELSE 1 END", "dbo." + table, column).Single() == 1;

        public static void Append(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int admin, List<NhatKyQuanTriItem> entries)
        {
            var candidates = new List<NhatKyQuanTriItem>();
            if (Column(db, "DangKyGiaoVien", "maAdmin"))
                candidates.AddRange(db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                    SELECT 0 AS MaNhatKy, CASE WHEN d.trangThai='Approved' THEN 'DuyetGiaoVien' ELSE 'TuChoiGiaoVien' END AS HanhDong,
                        d.lyDoTuChoi AS LyDo,d.ngayDuyet AS NgayTao,a.hoTen AS TenAdmin,
                        'HoSoGiaoVien' AS LoaiDoiTuong,d.maDon AS MaDoiTuong,u.hoTen AS TenDoiTuong
                    FROM dbo.DangKyGiaoVien d JOIN dbo.NguoiDung a ON a.maNguoiDung=d.maAdmin
                    LEFT JOIN dbo.NguoiDung u ON u.maNguoiDung=d.maNguoiDung
                    WHERE d.maAdmin=@admin AND d.ngayDuyet IS NOT NULL AND d.trangThai IN ('Approved','Rejected')", new SqlParameter("@admin",admin)).ToList());
            if (Column(db, "TeacherSpecialtyRequest", "ReviewedBy"))
                candidates.AddRange(db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                    SELECT 0 AS MaNhatKy,CASE WHEN r.Status='Approved' THEN 'DuyetChuyenMon' ELSE 'TuChoiChuyenMon' END AS HanhDong,
                        r.Reason AS LyDo,r.ReviewedAt AS NgayTao,a.hoTen AS TenAdmin,
                        'ChuyenMon' AS LoaiDoiTuong,CAST(r.Id AS VARCHAR(30)) AS MaDoiTuong,u.hoTen AS TenDoiTuong
                    FROM dbo.TeacherSpecialtyRequest r JOIN dbo.NguoiDung a ON a.maNguoiDung=r.ReviewedBy
                    LEFT JOIN dbo.NguoiDung u ON u.maNguoiDung=r.TeacherId
                    WHERE r.ReviewedBy=@admin AND r.ReviewedAt IS NOT NULL AND r.Status IN ('Approved','Rejected')", new SqlParameter("@admin",admin)).ToList());
            if (Column(db, "NhanThanhToan", "maAdminXuLy"))
                candidates.AddRange(db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                    SELECT 0 AS MaNhatKy,CASE WHEN p.trangThai=N'Đã thanh toán' THEN 'DuyetChiTra' ELSE 'TuChoiChiTra' END AS HanhDong,
                        p.lyDoTuChoi AS LyDo,p.ngayXuLy AS NgayTao,a.hoTen AS TenAdmin,
                        'PhieuChi' AS LoaiDoiTuong,p.maPhieuChi AS MaDoiTuong,u.hoTen AS TenDoiTuong
                    FROM dbo.NhanThanhToan p JOIN dbo.NguoiDung a ON a.maNguoiDung=p.maAdminXuLy
                    LEFT JOIN dbo.NguoiDung u ON u.maNguoiDung=p.maGiaoVien
                    WHERE p.maAdminXuLy=@admin AND p.ngayXuLy IS NOT NULL AND p.trangThai IN (N'Đã thanh toán',N'Từ chối')", new SqlParameter("@admin",admin)).ToList());
            if (Column(db, "XuLyViPham", "maAdmin"))
                candidates.AddRange(db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                    SELECT 0 AS MaNhatKy,CASE x.hanhDong WHEN N'Bác bỏ' THEN 'BacBaoCao' WHEN N'Tạm khóa' THEN 'TamKhoaKhoaHoc'
                        WHEN N'Mở lại' THEN 'MoLaiKhoaHoc' ELSE 'KhoaVinhVienKhoaHoc' END AS HanhDong,
                        x.lyDo AS LyDo,x.ngayXuLy AS NgayTao,a.hoTen AS TenAdmin,
                        'XuLyViPham' AS LoaiDoiTuong,x.maXuLy AS MaDoiTuong,k.tenKhoaHoc AS TenDoiTuong
                    FROM dbo.XuLyViPham x JOIN dbo.NguoiDung a ON a.maNguoiDung=x.maAdmin
                    LEFT JOIN dbo.KhoaHoc k ON k.maKhoaHoc=x.maKhoaHoc
                    WHERE x.maAdmin=@admin", new SqlParameter("@admin",admin)).ToList());
            if (Column(db, "PhanHoiViPham", "vaiTroGui"))
                candidates.AddRange(db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                    SELECT 0 AS MaNhatKy,'YeuCauSuaViPham' AS HanhDong,p.noiDung AS LyDo,p.ngayGui AS NgayTao,a.hoTen AS TenAdmin,
                        'PhanHoiViPham' AS LoaiDoiTuong,p.maPhanHoi AS MaDoiTuong,k.tenKhoaHoc AS TenDoiTuong
                    FROM dbo.PhanHoiViPham p JOIN dbo.NguoiDung a ON a.maNguoiDung=p.maNguoiGui
                    JOIN dbo.XuLyViPham x ON x.maXuLy=p.maXuLy LEFT JOIN dbo.KhoaHoc k ON k.maKhoaHoc=x.maKhoaHoc
                    WHERE p.maNguoiGui=@admin AND p.vaiTroGui='Admin'", new SqlParameter("@admin",admin)).ToList());
            foreach (var candidate in candidates)
            {
                var matches = entries.Where(e => e.LoaiDoiTuong == candidate.LoaiDoiTuong && e.MaDoiTuong == candidate.MaDoiTuong && e.HanhDong == candidate.HanhDong).ToList();
                if (matches.Count == 0)
                    entries.Add(candidate);
                else foreach (var match in matches) if (string.IsNullOrEmpty(match.TenDoiTuong)) match.TenDoiTuong = candidate.TenDoiTuong;
            }
        }

        public static string Label(string action)
        {
            switch (action)
            {
                case "DuyetGiaoVien": return "Duyệt hồ sơ giáo viên";
                case "TuChoiGiaoVien": return "Từ chối hồ sơ giáo viên";
                case "DuyetChuyenMon": return "Duyệt bổ sung chuyên môn";
                case "TuChoiChuyenMon": return "Từ chối bổ sung chuyên môn";
                case "TaoDanhMuc": return "Tạo danh mục";
                case "SuaDanhMuc": return "Chỉnh sửa danh mục";
                case "BatDanhMuc": return "Kích hoạt danh mục";
                case "TatDanhMuc": return "Tạm ngưng danh mục";
                case "XoaDanhMuc": return "Xóa danh mục";
                case "DuyetChiTra": return "Duyệt chi trả giáo viên";
                case "TuChoiChiTra": return "Từ chối chi trả";
                case "BacBaoCao": return "Bác bỏ báo cáo vi phạm";
                case "TamKhoaKhoaHoc": return "Tạm khóa khóa học";
                case "MoLaiKhoaHoc": return "Mở lại khóa học";
                case "KhoaVinhVienKhoaHoc": return "Khóa vĩnh viễn khóa học";
                case "YeuCauSuaViPham": return "Yêu cầu giáo viên sửa vi phạm";
                case "ChuyenVideo": return "Chuyển video lên lưu trữ";
                case "ThuLaiVideo": return "Thử lại xử lý video";
                case "XuatBaoCao": return "Xuất báo cáo";
                case "TaiSqlVideo": return "Tải SQL chuyển đổi video";
                default: return action;
            }
        }
        public static string ObjectLabel(string type)
        {
            switch(type)
            {
                case "KhoaHoc": return "Khóa học";
                case "NguoiDung": return "Tài khoản";
                case "HoSoGiaoVien": return "Hồ sơ giáo viên";
                case "ChuyenMon": return "Yêu cầu chuyên môn";
                case "PhieuChi": return "Phiếu chi";
                case "XuLyViPham": return "Xử lý vi phạm";
                case "PhanHoiViPham": return "Phản hồi vi phạm";
                case "DanhMuc": return "Danh mục";
                case "KhuyenMai": return "Khuyến mãi";
                case "BaoCao": return "Báo cáo";
                case "LuuTru": return "Lưu trữ";
                default: return type;
            }
        }
    }
}
