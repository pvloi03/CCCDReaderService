# Exclusion of Fingerprint Subsystem

Mặc dù thiết bị phần cứng gốc HN-212 tích hợp cả module cảm biến vân tay cùng đầu đọc thẻ, yêu cầu của hệ thống chỉ tập trung vào xác minh căn cước công dân và đối soát sinh trắc học không tiếp xúc qua khuôn mặt. Chúng tôi quyết định loại bỏ hoàn toàn module vân tay cùng các thư viện liên quan (NextBiometrics, NBBiometrics native DLLs). Quyết định này giúp loại bỏ phụ thuộc vào các thư viện native cồng kềnh, giảm kích thước phân phối của ứng dụng và tinh giản quy trình xác thực.
