/**
 * Mock Data for E-Learning Platform
 * Shared across all 22 functional static pages
 */

const mockData = {
  categories: [
    { id: 'cat-1', name: 'Lập trình & CNTT', count: 48, code: 'DEV', icon: '💻' },
    { id: 'cat-2', name: 'Thiết kế Đồ họa & UI/UX', count: 32, code: 'DSG', icon: '🎨' },
    { id: 'cat-3', name: 'Marketing & Truyền thông số', count: 26, code: 'MKT', icon: '📢' },
    { id: 'cat-4', name: 'Kinh doanh & Khởi nghiệp', count: 19, code: 'BIZ', icon: '💼' },
    { id: 'cat-5', name: 'Khoa học Dữ liệu & Trí tuệ nhân tạo', count: 24, code: 'AI', icon: '🤖' },
    { id: 'cat-6', name: 'Ngoại ngữ & Kỹ năng mềm', count: 35, code: 'LANG', icon: '🌐' }
  ],

  courses: [
    {
      id: 'c-101',
      title: 'Xây dựng Web hiện đại toàn diện với HTML5, CSS3 và Vanilla JS',
      category: 'Lập trình & CNTT',
      categoryId: 'cat-1',
      instructor: 'ThS. Nguyễn Văn An',
      rating: 4.9,
      reviewCount: 420,
      price: 699000,
      originalPrice: 1200000,
      image: 'https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80',
      tag: 'Bán chạy',
      level: 'Người mới bắt đầu',
      duration: '32 giờ',
      lessonsCount: 45,
      featured: true,
      progress: 85,
      isEnrolled: true,
      isCompleted: false
    },
    {
      id: 'c-102',
      title: 'Thiết kế giao diện UI/UX chuyên nghiệp chuẩn Figma và Design System',
      category: 'Thiết kế Đồ họa & UI/UX',
      categoryId: 'cat-2',
      instructor: 'Trần Thị Mai Phương',
      rating: 4.8,
      reviewCount: 310,
      price: 849000,
      originalPrice: 1500000,
      image: 'https://images.unsplash.com/photo-1581291518857-4e27b48ff24e?w=600&auto=format&fit=crop&q=80',
      tag: 'Gợi ý cho bạn',
      level: 'Trung cấp',
      duration: '28 giờ',
      lessonsCount: 38,
      featured: true,
      progress: 100,
      isEnrolled: true,
      isCompleted: true
    },
    {
      id: 'c-103',
      title: 'Machine Learning & Phân tích dữ liệu thực chiến với Python',
      category: 'Khoa học Dữ liệu & Trí tuệ nhân tạo',
      categoryId: 'cat-5',
      instructor: 'TS. Lê Hoàng Nam',
      rating: 4.95,
      reviewCount: 520,
      price: 1190000,
      originalPrice: 2000000,
      image: 'https://images.unsplash.com/photo-1555949963-aa79dcee981c?w=600&auto=format&fit=crop&q=80',
      tag: 'Top Trending',
      level: 'Nâng cao',
      duration: '45 giờ',
      lessonsCount: 60,
      featured: true,
      progress: 42,
      isEnrolled: true,
      isCompleted: false
    },
    {
      id: 'c-104',
      title: 'Nhập môn Lập trình Web căn bản cho người hoàn toàn mới',
      category: 'Lập trình & CNTT',
      categoryId: 'cat-1',
      instructor: 'Phạm Thanh Bình',
      rating: 4.7,
      reviewCount: 180,
      price: 0,
      originalPrice: 0,
      image: 'https://images.unsplash.com/photo-1498050108023-c5249f4df085?w=600&auto=format&fit=crop&q=80',
      tag: 'Miễn phí',
      level: 'Cơ bản',
      duration: '10 giờ',
      lessonsCount: 15,
      featured: false,
      progress: 0,
      isEnrolled: false,
      isCompleted: false
    },
    {
      id: 'c-105',
      title: 'Digital Marketing Thực Chiến & Tối ưu quảng cáo đa kênh Meta & Google',
      category: 'Marketing & Truyền thông số',
      categoryId: 'cat-3',
      instructor: 'Vũ Minh Đức',
      rating: 4.75,
      reviewCount: 290,
      price: 799000,
      originalPrice: 1390000,
      image: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=600&auto=format&fit=crop&q=80',
      tag: 'Phổ biến',
      level: 'Mọi cấp độ',
      duration: '22 giờ',
      lessonsCount: 30,
      featured: false,
      progress: 0,
      isEnrolled: false,
      isCompleted: false
    }
  ],

  curriculum: [
    {
      id: 'sec-1',
      title: 'Phần 1: Tổng quan và Khởi tạo dự án E-Learning',
      duration: '1h 15m',
      lessons: [
        { id: 'les-1', title: '1.1 Giới thiệu kiến trúc nền tảng và tài liệu đặc tả', duration: '12:40', isCompleted: true, isFree: true },
        { id: 'les-2', title: '1.2 Thiết lập Design System (Màu sắc, Typography, Grid)', duration: '18:25', isCompleted: true, isFree: true },
        { id: 'les-3', title: '1.3 Tạo khung sườn HTML chuẩn ngữ nghĩa và Header cố định', duration: '22:10', isCompleted: true, isFree: false },
        { id: 'les-4', title: '1.4 Xây dựng thẻ Course Card với tỉ lệ vàng 16:9', duration: '21:50', isCompleted: true, isFree: false }
      ]
    },
    {
      id: 'sec-2',
      title: 'Phần 2: Xây dựng trình phát video và xử lý tiến độ học',
      duration: '2h 30m',
      lessons: [
        { id: 'les-5', title: '2.1 Thiết kế Player Controls và thanh tua trực quan', duration: '25:10', isCompleted: true, isFree: false },
        { id: 'les-6', title: '2.2 Xử lý tự động tính phần trăm hoàn thành khi đạt 80%', duration: '30:45', isCompleted: true, isFree: false },
        { id: 'les-7', title: '2.3 Cơ chế khôi phục mốc xem và phát hiện ngắt kết nối', duration: '19:20', isCompleted: false, isFree: false, isCurrent: true },
        { id: 'les-8', title: '2.4 Popup đánh giá 5 sao khi đạt ngưỡng 70%', duration: '24:00', isCompleted: false, isFree: false }
      ]
    },
    {
      id: 'sec-3',
      title: 'Phần 3: Giỏ hàng, Áp dụng Voucher và Cổng thanh toán',
      duration: '1h 45m',
      lessons: [
        { id: 'les-9', title: '3.1 Tạo giỏ hàng và danh sách khóa học tạm tính', duration: '20:15', isCompleted: false, isFree: false },
        { id: 'les-10', title: '3.2 Kiểm tra tính hợp lệ của coupon và trừ tiền', duration: '28:30', isCompleted: false, isFree: false },
        { id: 'les-11', title: '3.3 Giả lập luồng thanh toán ATM / MoMo / VNPay', duration: '22:00', isCompleted: false, isFree: false }
      ]
    }
  ],

  coupons: [
    { code: 'CHAOMUNG2026', name: 'Ưu đãi thành viên mới', discount: 20, validUntil: '31/12/2026', status: 'Đang chạy', used: 1420, revenue: '185.400.000 đ' },
    { code: 'SUMMERSALE', name: 'Chiến dịch Khởi động Mùa hè', discount: 35, validUntil: '15/07/2026', status: 'Đang chạy', used: 890, revenue: '124.600.000 đ' },
    { code: 'VIPDEV50', name: 'Đặc quyền Lập trình viên VIP', discount: 50, validUntil: '01/05/2026', status: 'Đã kết thúc', used: 520, revenue: '98.000.000 đ' },
    { code: 'FLASHSALE', name: 'Giảm giá chớp nhoáng 24h', discount: 15, validUntil: '20/09/2026', status: 'Tạm ngưng', used: 210, revenue: '31.500.000 đ' }
  ],

  pendingApprovals: [
    {
      id: 'appr-01',
      title: 'Phát triển Ứng dụng Di động Flutter từ căn bản đến nâng cao',
      instructor: 'Nguyễn Đình Tuấn',
      category: 'Lập trình & CNTT',
      submitDate: '06/09/2026',
      lessons: 42,
      duration: '35 giờ',
      status: 'Chờ duyệt',
      videoCount: 42,
      resCount: 15
    },
    {
      id: 'appr-02',
      title: 'Chiến lược Thương mại điện tử trên nền tảng TikTok Shop & Shopee',
      instructor: 'Hoàng Kim Ngân',
      category: 'Kinh doanh & Khởi nghiệp',
      submitDate: '07/09/2026',
      lessons: 28,
      duration: '18 giờ',
      status: 'Chờ duyệt',
      videoCount: 28,
      resCount: 8
    }
  ],

  students: [
    { id: 'st-1', name: 'Nguyễn Hải Yến', email: 'haiyen.nguyen@email.com', date: '01/09/2026', course: 'Xây dựng Web hiện đại', progress: 85, status: 'Đang học' },
    { id: 'st-2', name: 'Lê Quốc Bảo', email: 'bao.le@email.com', date: '28/08/2026', course: 'Thiết kế giao diện UI/UX', progress: 100, status: 'Hoàn thành' },
    { id: 'st-3', name: 'Đặng Minh Khôi', email: 'khoi.dang@email.com', date: '03/09/2026', course: 'Machine Learning Python', progress: 42, status: 'Đang học' },
    { id: 'st-4', name: 'Phạm Thu Trang', email: 'trang.pham@email.com', date: '05/09/2026', course: 'Xây dựng Web hiện đại', progress: 15, status: 'Đang học' }
  ]
};

// Export helper
if (typeof window !== 'undefined') {
  window.mockData = mockData;
}
