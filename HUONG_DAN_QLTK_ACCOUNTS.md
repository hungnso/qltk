# QLTK đọc accounts.txt

Mở **QLTK_Accounts.exe** trong thư mục này. Đây là bản quản lý account mới có mã nguồn kèm theo; `QLTK_NST.exe` cũ vẫn giữ nguyên để sử dụng lại khi cần. Không cần chạy `setup.bat` nếu đã có `jre/bin/javaw.exe`.

## Cách dùng

1. Mỗi dòng trong `accounts.txt` là `tài_khoản|mật_khẩu|server`, ví dụ với tài khoản giả:

   ```text
   tai_khoan_1|mat_khau_1|Bokken
   tai_khoan_2|mat_khau_2|Shuriken
   ```

2. Tên server phải có trong `servers.txt`. Giữ thứ tự server giống thứ tự server của bản game đang dùng. Dòng trống và dòng bắt đầu bằng `#` được bỏ qua. Dòng lỗi được hiển thị theo số dòng, không hiển thị mật khẩu.

   Với bản `e90_auto_account_resume_watchdog.jar`, danh sách có 14 server; ba dòng cuối là `Hirosaki`, `Haruna`, `NinjaMobile S1 - Ronin`. Bản game này không có `Bisento`, nên không thêm tên đó vào danh sách vì sẽ làm lệch vị trí server phía sau. Sau khi sửa server của account đang chạy, đóng tab đó, bấm **Đọc lại danh sách**, rồi mở lại account.
3. QLTK chỉ tải danh sách khi khởi động. Tick các account rồi bấm **Mở tài khoản đã chọn**, hoặc bấm **Mở tất cả**. Nếu chưa tick ô nào, nút mở tài khoản đã chọn dùng các hàng đang được chọn trong bảng.
4. Các cửa sổ game mở lần lượt, mỗi account/server một tab riêng. **Hiện tab** đưa cửa sổ tương ứng lên trước; **Đóng tab đã chọn** đóng các cửa sổ tương ứng.
5. Sau khi sửa file, bấm **Đọc lại danh sách**. Account đang chạy không bị mở trùng. Tên account viết hoa/thường được nhận diện là cùng account theo cách xử lý của bản game hiện tại.

## Tự chọn nhân vật và thông tin NV

- Khi `AutoLogin=true`, sau khi server trả danh sách nhân vật, QLTK tự chọn **ô nhân vật số 1** (ô bên trái), rồi vào game. Nếu ô đó trống, tool báo trạng thái để bạn chọn thủ công; không tự tạo nhân vật hoặc chọn ô khác.
- Cột **Thông tin NV** hiển thị tên nhân vật, level, xu trên người, xu trong rương và lượng. Dữ liệu được đọc từ tab game tương ứng và cập nhật khoảng 2 giây một lần.
- Sau khi vào game, cầu nối yêu cầu dữ liệu rương bằng lệnh đọc rương của game. Nếu chưa nhận được dữ liệu, hiển thị **Chưa đọc được**. Chỉ hiện `0` khi đã đọc được số dư bằng 0. Lệnh đọc rương được thử tối đa 3 lần, cách nhau ít nhất 30 giây; nếu server không trả dữ liệu thì cần mở rương trong game để tải dữ liệu.
- Khi `AutoLogin=false`, bạn tự đăng nhập và chọn nhân vật; tool vẫn đọc thông tin nhân vật sau khi vào game.
- Sau khi cập nhật tool, đóng QLTK và các tab game cũ rồi mở lại `QLTK_Accounts.exe`. Các tab đã chạy trước đó không tự nhận cầu nối mới. Bản EXE trước được lưu dưới tên `QLTK_Accounts.previous-....exe` để có thể khôi phục.

## Cấu hình và dữ liệu

- Dùng `EmulatorPath`, `GamePath`, `TabWidth`, `TabHeight`, `MaxTab`, `AutoLogin` từ `settings.xml` và vị trí cửa sổ trong `layout.xml`.
- `MaxTab` là số tab đồng thời. Account vượt giới hạn chưa được mở; đóng bớt tab rồi bấm mở lại. Số tab được tính cả trong lúc game đang tải.
- `AutoLogin=true`: cầu nối gửi tài khoản/mật khẩu và chọn đúng server khi game sẵn sàng. `AutoLogin=false`: mở game với thông tin account đã lưu để đăng nhập thủ công.
- Dữ liệu bản mới nằm trong `data/accounts/tab_N`, liên kết account–tab nằm trong `data/accounts/tabs.xml`. Sửa thứ tự danh sách hoặc mật khẩu không đổi liên kết. Đổi username hoặc server tạo liên kết mới.
- Dữ liệu cũ trong `data/tab_N` không bị sửa hoặc chuyển. Bản mới dùng dữ liệu riêng nên các thiết lập trong game cần cấu hình lại ở lần mở đầu tiên.
- Một tab được cố định cho một account; cầu nối tắt chế độ xoay account của game trong dữ liệu tab đó.
- Trạng thái **Đã gửi đăng nhập** xác nhận đã gọi hàm đăng nhập, chưa xác nhận server chấp nhận mật khẩu hoặc nhân vật đã vào game. Kiểm tra kết quả trong cửa sổ game.

## Phạm vi bản mới

Bản này bổ sung danh sách account và mở cửa sổ game theo account. Các chức năng riêng của QLTK cũ như proxy theo tab và AutoNst không được chuyển sang bản này. Dùng EXE cũ khi cần các chức năng đó; tránh mở cùng account trên cả hai bản cùng lúc.

Cầu nối đã được kiểm tra với cấu trúc `e90_auto_account_resume_watchdog.jar` đang cấu hình. Đổi sang JAR có tên lớp/hàm khác có thể báo lỗi khởi động; không tự động đoán luồng đăng nhập của bản khác.

## Mã nguồn và kiểm tra

- `src/AccountCore.cs`: đọc account, nhận diện trùng, giới hạn mở tab và lưu liên kết.
- `src/AccountManager.cs`: giao diện Windows và quản lý cửa sổ Java.
- `src/AccountBootstrap.java`: ghi dữ liệu account theo định dạng MicroEmulator và gọi hàm đăng nhập của game.
- `src/GameAccountObserver.java`: chọn nhân vật đầu tiên, đọc dữ liệu nhân vật/rương trên luồng sự kiện của game.
- `src/CharacterSnapshot.cs`: đọc và hiển thị thông tin nhân vật trong một cột của QLTK.

Máy build cần .NET Framework compiler và JDK. Máy chạy chỉ cần JRE đã đi kèm.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-accounts.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

Kiểm tra dùng dữ liệu giả và game fixture không kết nối mạng. Không tự mở hoặc đăng nhập account thật trong quá trình kiểm tra.
