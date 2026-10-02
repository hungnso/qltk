# Cấu hình khu up Vùng đất ma quái từ QLTK

Ngày: 2026-10-02

## Mục tiêu và phạm vi

Người dùng chọn một hoặc nhiều account đang mở trong QLTK và xem/sửa danh sách khu mà Auto Vùng đất ma quái (VDMQ) được phép up. Đây là cấu hình **theo từng tab/account**, được game lưu trong RMS; QLTK không tạo một cấu hình toàn cục ghi đè mọi account khi khởi động.

Chỉ chuyển mục **“Cấu hình khu up”** ra QLTK. Không chọn map, không bắt đầu/dừng Auto VDMQ, không chuyển khu ngay khi bấm Áp dụng và không thay đổi các checkbox trong menu Tự động chung.

Game hiện có `AutoVungDatMaQuai.configuredZones`, đọc/ghi khóa RMS `AutoVdmqZones`; danh sách rỗng nghĩa là dùng mọi khu. `VdmqZonePolicy.chooseZone` chọn khu ít người trong danh sách, ưu tiên dưới 10 người và dùng khu dự phòng nếu danh sách hợp lệ đã đầy. Các số là **ID khu bắt đầu từ 0**, không phải vị trí thứ nhất/thứ hai trong giao diện.

## Giao diện

Thêm nút **Khu up VDMQ** cạnh nút Auto hiện có trên cùng một dòng để không giảm chiều cao bảng account trên cửa sổ 900×480. Thêm cột **Khu VDMQ** cạnh cột Auto, hiển thị `Tất cả khu`, `Khu: 0 1 2`, `Chờ thức`, `Đang áp dụng`, `Đã áp dụng`, hoặc lỗi theo từng tab. Cột có thể cuộn ngang như các cột hiện tại.

Hộp thoại cho phép chọn **Tất cả khu** hoặc **Chỉ các khu** với ô nhập `0 1 2` (chấp nhận cả dấu phẩy và khoảng trắng). Khi các tab chọn có danh sách khác nhau, hiện **Khác nhau — giữ nguyên**; mở/đóng hộp thoại không ghi gì. Chỉ khi người dùng chọn chế độ hoặc sửa danh sách rồi bấm **Áp dụng cho tab đã chọn** mới gửi lệnh. Quy tắc chọn account giống nút Auto: ưu tiên account được tick, nếu không có thì dùng các hàng đang chọn. Các tab chưa vào game, đã đóng hoặc không hỗ trợ VDMQ được bỏ qua với thông báo rõ ràng.

Chuẩn hóa đầu vào thành các ID nguyên không âm, không trùng, theo thứ tự người dùng nhập; tối đa 64 ID, mỗi ID từ 0 đến 9999. Chuỗi rỗng chỉ hợp lệ với **Tất cả khu**. Không đoán số khu có sẵn khi chưa ở map VDMQ: game vẫn áp dụng chính sách hiện có và bỏ qua ID ngoài danh sách khu của map. Hướng dẫn giải thích rằng nhập toàn ID không tồn tại sẽ khiến Auto VDMQ chờ dữ liệu khu, nên cần kiểm tra số khu trong game.

## Đồng bộ với game và lưu trữ

Cầu nối QLTK đọc trạng thái trên event thread của MicroEmulator. Khi phiên bản game có lớp và trường cần thiết, nó đọc RMS `AutoVdmqZones`; nếu Auto VDMQ đã khởi tạo, nó cũng đọc danh sách đang chạy để phản ánh việc người dùng sửa trong menu game. Trạng thái ghi vào `vdmq-state.xml` trong thư mục `data/accounts/tab_N`, kèm session tab; chỉ ghi lại khi nội dung đổi. Khi rời màn game, trạng thái thành `WAITING`, không tiếp tục trình bày snapshot cũ như dữ liệu đang chạy. Thiếu lớp/trường/phương thức thì thành `UNSUPPORTED` nhưng login, thông tin nhân vật và Auto chung vẫn hoạt động.

QLTK gửi `vdmq-command.xml` riêng cho từng tab, gồm session, request ID và danh sách khu chuẩn hóa. Cầu nối kiểm tra đầy đủ trước khi thay đổi, ghi RMS bằng `mResources.a("AutoVdmqZones", String)`, đọc lại bằng `mResources.c("AutoVdmqZones")`, rồi cập nhật `configuredZones` của instance `NSOT_MOB.autoVdmq` nếu có. Nếu instance đang hoạt động, đặt lại cache `zoneDataMap` để lần cập nhật kế tiếp xét danh sách mới; không gọi hàm bắt đầu Auto hoặc yêu cầu đổi khu trực tiếp. Nếu ghi/lưu/cập nhật thất bại, cố khôi phục RMS và trạng thái runtime trước đó, trả lỗi cho đúng tab. Cầu nối báo `vdmq-result.xml` chỉ sau khi đọc lại được trạng thái mong muốn; nếu file kết quả tạm thời không ghi được, thử gửi lại mà không áp dụng lệnh lần hai.

Các file dùng session đã có (`QLTK_AUTO_SESSION`) và request ID tăng dần. Lệnh cũ sau khi đóng/mở tab không được phát lại. Giới hạn kích thước XML và cấm DTD/entity ngoài. Không ghi mật khẩu hay thông tin đăng nhập vào file VDMQ. Tab đang ngủ giữ lệnh chờ tới khi thức, không tự thức; thời gian ngủ không tính vào hạn chờ xác nhận 30 giây. Tab đóng trước khi xử lý mất lệnh theo session cũ. Trạng thái RMS trong từng tab vẫn tồn tại sau khi đóng game và được đọc lại khi mở tab đó.

## Tương thích và kiểm tra

Triển khai trong QLTK và `account-bridge.jar` tại workspace; không sửa trực tiếp JAR hoặc mã game nằm ngoài workspace. Cầu nối dùng reflection với lớp game đã nạp, tương tự luồng Auto chung. Kiểm tra offline bằng game fixture trên event thread: đọc RMS mặc định/danh sách, đổi lúc Auto VDMQ đang chạy, áp dụng riêng một số tab, trạng thái hỗn hợp, ngủ/thức, session cũ, giá trị sai, ghi RMS thất bại, phiên bản game thiếu VDMQ và việc giữ nguyên chức năng Auto chung. Chạy `build-accounts.ps1` và toàn bộ `tests/run-tests.ps1`; không đăng nhập account thật trong bài kiểm tra.
