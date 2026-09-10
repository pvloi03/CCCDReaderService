# Dual Channel REST and SignalR Communication

Các ứng dụng client (Web trên trình duyệt, Desktop app) cần gửi lệnh nghiệp vụ đồng thời phản ứng tức thì khi có sự kiện phần cứng như cắm hoặc rút thẻ CCCD. Thay vì bắt client phải liên tục gửi request thăm dò (polling) gây lãng phí tài nguyên và tạo độ trễ, chúng tôi quyết định sử dụng mô hình kênh đôi gồm HTTP REST API cho các thao tác theo yêu cầu và SignalR WebSocket Hub để đẩy sự kiện thời gian thực. Cơ chế này giúp giảm tải CPU, tối ưu độ nhạy phản hồi và đơn giản hóa mã nguồn tích hợp phía client.
