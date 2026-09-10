# System Tray Hosting Over Windows Service

Dịch vụ cần chạy nền để phục vụ các ứng dụng khác nhưng phải truy cập phần cứng Camera (DirectShow) và Đầu đọc thẻ CCCD (USB). Kể từ Windows Vista, Windows Service chạy cô lập trong Session 0 không có giao diện đồ họa khiến DirectShow/DirectX bị chặn truy cập và không mở được video capture. Chúng tôi quyết định triển khai dịch vụ dưới dạng ứng dụng System Tray chạy trong Interactive User Session với tùy chọn tự khởi động cùng Windows, đảm bảo quyền truy cập phần cứng đầy đủ mà vẫn hoạt động ngầm êm ái.
