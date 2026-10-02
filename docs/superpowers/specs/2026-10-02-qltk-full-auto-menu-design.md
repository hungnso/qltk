# Đưa toàn bộ menu Tự động của game vào QLTK

Ngày: 2026-10-02

## Mục tiêu và phạm vi

QLTK có một dòng **Auto** dưới **Hiệu năng**. Từ dòng này, người dùng mở bảng cuộn để xem trạng thái thực tế của toàn bộ mục trong menu **Tự động** của game, chỉnh checkbox hoặc thông số, rồi bấm **Áp dụng cho tab đã chọn**. Các tab được chọn theo quy tắc hiện có: ưu tiên những account được tick, nếu không tick thì dùng những hàng đang chọn. Không tự áp dụng chỉ vì người dùng mở bảng hoặc đổi giá trị nháp.

Phạm vi là **31 checkbox hiện được vẽ bởi `GameScr` và checkbox “Chỉ nhặt vũ khí”**, cùng bảy giá trị đi kèm: ngưỡng HP, ngưỡng MP, cấp thức ăn, và bốn mức lọc theo cấp ở các mục nhặt. Không đưa các màn hình Auto Nhiệm vụ, Auto hằng ngày hay menu khác vào bảng này. Cần đọc danh sách theo thứ tự thực sự được vẽ trong `GameScr`, không lấy toàn bộ mảng `mResources.ri`: mảng có nhãn dự phòng và khoảng trống không phải mục có thể chỉnh.

## Giao diện và hành vi

Giữ dòng cấu hình chính gọn: nhãn **Auto**, nút **Xem / chỉnh Auto**, tóm tắt số tab được chọn và kết quả lần áp dụng gần nhất. Bảng Auto là hộp thoại cuộn, nhóm các mục theo thứ tự trong game: hồi phục và hỗ trợ; nhặt và lọc đồ; di chuyển và chiến đấu; cộng điểm và nhóm. Mỗi mục có checkbox; bảy mục có giá trị thì có thêm ô số với giới hạn đúng của game. Tên mục dùng nhãn game đang hiển thị, kể cả “Chỉ nhặt vũ khí”.

Khi mở bảng cho một tab, QLTK tải trạng thái game mới nhất. Khi chọn nhiều tab, checkbox có ba trạng thái: tất cả bật, tất cả tắt, hoặc **Khác nhau / giữ nguyên**. Giá trị số khác nhau hiện **Khác nhau**. Chỉ các trường người dùng thay đổi sau khi mở bảng mới được gửi; các trường chưa chạm tới giữ nguyên ở từng tab. Trước khi gửi, QLTK kiểm tra phạm vi số và xử lý các nhóm loại trừ như **Không nhặt gì cả**, **Nhặt Trang Bị** và **Chỉ nhặt vũ khí** theo quy tắc game: bật một lựa chọn loại trừ sẽ bỏ các lựa chọn xung đột, rồi hiển thị trạng thái đã được chuẩn hóa.

Các tab chưa sẵn sàng có trạng thái riêng: chưa vào game, đang ngủ, không đọc được trạng thái, và phiên bản game không hỗ trợ. QLTK không báo “đã áp dụng” chỉ vì đã ghi yêu cầu; từng tab phải có xác nhận từ game. Tab đang ngủ giữ yêu cầu ở trạng thái **chờ thức**, không bị tự đánh thức. Khi người dùng đóng hộp thoại, việc đã gửi vẫn tiếp tục và kết quả từng tab hiện trên bảng quản lý/ô thông báo. Không tự áp dụng cho account chưa được chọn.

## Luồng dữ liệu và lưu trữ

Trong từng tiến trình game, cầu nối đang có sẵn luồng quan sát chạy trên event thread của MicroEmulator. Mở rộng luồng này để đọc trực tiếp các cờ và giá trị của `Char`, cộng với `AutoDailyPanel.weaponOnlyPickup`, rồi ghi một `auto-state.xml` riêng cho tab. QLTK dùng bản trạng thái này để hiển thị; thời gian ghi và trạng thái sẵn sàng giúp tránh hiển thị dữ liệu cũ như dữ liệu đang chạy. Tránh ghi lại file nếu giá trị không đổi để phù hợp VPS 1 CPU.

Khi bấm Áp dụng, QLTK ghi nguyên tử một lệnh `auto-command.xml` **theo từng tab/account**, gồm mã phiên chạy, mã yêu cầu tăng dần và chỉ các trường đã sửa. Không chứa mật khẩu hoặc dữ liệu nhạy cảm. Cầu nối đọc lệnh trên event thread, đối chiếu mã phiên và mã yêu cầu để không áp dụng nhầm/lặp, xác minh toàn bộ giá trị trước khi sửa, sửa các trường của đúng classloader game, gọi `Char.b()` để lưu các mục thông thường vào `V7LCSetting`, và dùng thao tác lưu sẵn có của `AutoDailyPanel` cho “Chỉ nhặt vũ khí”. Nếu phát sinh lỗi trong lúc thực hiện, khôi phục trạng thái trước lệnh của tab đó và báo lỗi. Sau đó đọc lại trạng thái thực tế và ghi xác nhận `auto-result.xml` với mã phiên, mã yêu cầu, thành công hoặc lỗi cụ thể. Nếu một trường bị game khóa theo trạng thái hiện tại, cầu nối trả lỗi cho trường/tab đó; QLTK không đánh dấu thành công giả.

Các file lệnh/trạng thái nằm dưới thư mục dữ liệu account hiện có; không sửa `accounts.txt`, `tabs.xml`, lịch sử nhân vật hay file `layout.xml` cũ. Không ghi trực tiếp vào thư mục game ngoài workspace. Lệnh của tab ngủ được thực hiện khi tab thức; khi tab đóng trước lúc xử lý thì yêu cầu cũ hết hiệu lực theo mã phiên. Account chưa mở không nhận lệnh vì chưa có tab để chọn. Khi tab được mở lại, dữ liệu RMS game và xác nhận mới là nguồn xác thực trạng thái.

## Tương thích và lỗi

`account-bridge.jar` cần kiểm tra hợp đồng game cho các trường Auto trước khi bật nút áp dụng trên tab. Nếu JAR game không có trường/phương thức cần thiết, QLTK ghi rõ **Phiên bản game chưa hỗ trợ Auto** cho tab đó và không làm gián đoạn đăng nhập, đọc nhân vật hoặc các thống kê đang hoạt động. Lệnh lỗi/không hợp lệ không được làm ngừng luồng quan sát. Ghi file nguyên tử, giới hạn kích thước/giá trị khi đọc XML; kết quả chỉ được gắn với đúng mã yêu cầu và đúng account. Timeout chỉ báo chưa xác nhận để người dùng thử lại hoặc thức tab; không coi là thành công.

## Kiểm tra chấp nhận

1. Mở game giả lập với các giá trị Auto khác mặc định: QLTK hiển thị đúng 32 checkbox và bảy giá trị số theo thứ tự menu thật.
2. Chọn hai tab có cấu hình khác nhau: trạng thái hỗn hợp hiện rõ; sửa một mục rồi Áp dụng chỉ đổi mục đó trên hai tab. Tab không chọn giữ nguyên.
3. Bật các mục loại trừ hoặc nhập số ngoài phạm vi: UI/game chuẩn hóa đúng hoặc báo lỗi; không ghi cấu hình nửa chừng.
4. Đổi Auto trong menu game: QLTK đọc lại trạng thái thật. Áp dụng từ QLTK: game đọc lại và vẫn giữ các mục sau khi đóng/mở tab.
5. Tab ngủ không thức vì thao tác Auto; lệnh có trạng thái chờ và được xác nhận khi thức. Tab lỗi/chưa hỗ trợ không làm ảnh hưởng các tab khác.
6. Bộ kiểm tra hiện có cho mở account, thông tin NV, lịch sử, ngủ/thức và sắp xếp tab tiếp tục qua; kiểm tra luồng Auto dùng fixture game ngoại tuyến, không đăng nhập account thật.

## Phần mã dự kiến

- `src/AccountManager.cs`: dòng Auto, hộp thoại và trạng thái áp dụng từng tab.
- `src/GameAccountObserver.java` và lớp Auto nhỏ riêng: đọc/ghi lệnh trên event thread, snapshot/xác nhận.
- `src/AccountBootstrap.java`: kiểm tra hợp đồng Auto cùng phiên bản game.
- `tests/`: kiểm tra danh sách mục, hỗn hợp nhiều tab, trường hợp lỗi và lưu RMS bằng fixture ngoại tuyến.
- `HUONG_DAN_QLTK_ACCOUNTS.md`: hướng dẫn sử dụng và giới hạn phiên bản game.
