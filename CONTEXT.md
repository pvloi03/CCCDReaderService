# CCCD Reader Service

Dịch vụ chạy ngầm quản lý thiết bị đọc thẻ Căn cước công dân (CCCD) gắn chip và camera sinh trắc học khuôn mặt, cung cấp API giao tiếp chuẩn hóa cho các ứng dụng client trên máy tính.

## Language

### Core Entities

**Citizen**:
Người dân sở hữu thẻ CCCD tham gia vào quy trình xác thực danh tính tại máy tính hoặc quầy giao dịch.
_Avoid_: Khách hàng, user, chủ thẻ, người dùng

**Chip Card**:
Thẻ Căn cước công dân có gắn vi mạch điện tử theo tiêu chuẩn ICAO 9303, chứa thông tin nhân thân và ảnh sinh trắc học được ký số bởi Bộ Công An.
_Avoid_: Thẻ thông minh, thẻ nhựa, thẻ ATM, smartcard

**Data Group (DG)**:
Các phân vùng dữ liệu tiêu chuẩn được lưu trữ bảo mật trong vi mạch của Chip Card (như DG1 lưu thông tin MRZ, DG2 lưu ảnh chân dung gốc, DG13 lưu thông tin nhân thân mở rộng).
_Avoid_: Bảng, phân vùng, partition, field

**MRZ (Machine Readable Zone)**:
Vùng ký tự máy đọc quang học gồm 3 dòng ở mặt sau Chip Card, cung cấp khóa truy cập ban đầu để mở giao tiếp bảo mật với vi mạch thẻ.
_Avoid_: Mã vạch, mã OCR, chuỗi ký tự thẻ

### Workflow & Security

**Card Session**:
Phiên làm việc tạm thời gắn liền với một lần đọc thẻ thành công, lưu trữ có thời hạn (TTL) dữ liệu thẻ và ảnh chân dung trong bộ nhớ để phục vụ các bước đối soát tiếp theo.
_Avoid_: Phiên kết nối, transaction, token

**Face Verification**:
Quy trình đối soát sinh trắc học giữa ảnh khuôn mặt chụp trực tiếp từ Camera và ảnh chân dung gốc được giải mã từ vi mạch của Chip Card.
_Avoid_: Nhận diện khuôn mặt, face check, face search

**Liveness Detection**:
Kỹ thuật phân tích chống giả mạo khuôn mặt (FAS) nhằm chứng minh đối tượng đứng trước camera là người thật còn sống, ngăn chặn việc sử dụng ảnh in, màn hình hoặc mặt nạ.
_Avoid_: Check thật giả, anti-fake, motion test

**Hardware Lock**:
Cơ chế điều phối quyền truy cập độc quyền vào thiết bị phần cứng (đầu đọc thẻ, camera) nhằm bảo vệ cổng giao tiếp vật lý không bị xung đột khi có nhiều ứng dụng hoặc tab gọi đồng thời.
_Avoid_: Khóa tiến trình, mutex, semaphore
