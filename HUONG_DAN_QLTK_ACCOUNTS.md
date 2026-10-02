# QLTK đọc accounts.txt

Mở **QLTK_Accounts.exe** trong thư mục này. Đây là bản quản lý account mới có mã nguồn kèm theo; `QLTK_NST.exe` cũ vẫn giữ nguyên để sử dụng lại khi cần. Không cần chạy `setup.bat` nếu đã có `jre/bin/javaw.exe`.

## Cách dùng

Phía trên danh sách account có phần cấu hình giống tool cũ:

- **Emulator / Game**: bấm **Duyệt** chọn file `.jar` hoặc nhập đường dẫn. **Game** là phiên bản game sẽ mở cho các tài khoản.
- **Kích thước tab**: nhập chiều rộng × chiều cao màn hình game, mỗi chiều từ 100 đến 2000; mặc định 220 × 240.
- **MaxTab**: số tab game tối đa được mở đồng thời, từ 1 đến 200. Bấm **Lưu cấu hình** để lưu và cập nhật giới hạn ở dòng thông tin bên dưới; bấm mở tài khoản cũng lưu giá trị đang chọn. Tăng giới hạn rồi bấm mở để mở thêm account. Giảm giới hạn không tự đóng các tab đang chạy; tool chỉ ngăn mở thêm khi đã đạt hoặc vượt giới hạn.
- **Auto login**: bật để tự đăng nhập và chọn nhân vật đầu tiên; tắt để đăng nhập thủ công.
- **VPS nhẹ / Heap Java/tab (MB)**: chế độ giảm tải cho máy ít CPU/RAM; xem hướng dẫn hiệu năng bên dưới.
- Bấm **Lưu cấu hình** để lưu vào `settings.xml`. Khi bấm mở tài khoản, tool cũng kiểm tra và lưu các lựa chọn hiện tại. File JAR không tồn tại thì tool báo lỗi và không ghi đè cấu hình.
- Các lựa chọn áp dụng cho **tab mở mới**. Muốn đổi phiên bản hoặc kích thước của tab đang chạy, đóng tab đó rồi mở lại. Các mục khác trong cấu hình như `MaxTab`, proxy và AutoNst được giữ lại.

Các phiên bản game khác cần có cấu trúc tương thích với cầu nối và thứ tự server phù hợp với `servers.txt`; chọn được file JAR không đồng nghĩa mọi phiên bản đều hỗ trợ tự đăng nhập và đọc thông tin NV.

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
- Cột **Thông tin NV** dùng chữ cỡ 9, hiển thị trên một dòng và ngăn cách bằng `|`: `Tên NV (Lv 51) | Xu: 600.000 | Rương: 0 | Lượng: 901 | VK: Lv 50 +12`. Đây là ví dụ định dạng; số thực được đọc từ tab game tương ứng và cập nhật khoảng 2 giây ở chế độ thường, 5 giây ở chế độ VPS nhẹ.
- **VK** là vũ khí đang trang bị: `Lv` là level yêu cầu của vũ khí, `+12` là mức cộng hiện tại. Không có vũ khí thì hiện **Chưa trang bị**; dữ liệu trang bị chưa tải thì hiện **Chưa đọc được**.
- Sau khi vào game, cầu nối yêu cầu dữ liệu rương bằng lệnh đọc rương của game. Nếu chưa nhận được dữ liệu, hiển thị **Chưa đọc được**. Chỉ hiện `0` khi đã đọc được số dư bằng 0. Lệnh đọc rương được thử tối đa 3 lần, cách nhau ít nhất 30 giây; nếu server không trả dữ liệu thì cần mở rương trong game để tải dữ liệu.
- Khi `AutoLogin=false`, bạn tự đăng nhập và chọn nhân vật; tool vẫn đọc thông tin nhân vật sau khi vào game.
- Sau khi cập nhật tool, đóng QLTK và các tab game cũ rồi mở lại `QLTK_Accounts.exe`. Các tab đã chạy trước đó không tự nhận cầu nối mới. Bản EXE trước được lưu dưới tên `QLTK_Accounts.previous-....exe` để có thể khôi phục.

## Sắp xếp cửa sổ tab

- **Sắp xếp đã chọn** áp dụng cho các account được tick; nếu chưa tick thì dùng các hàng đang chọn. **Sắp xếp tất cả** áp dụng cho các cửa sổ game do bản quản lý này mở.
- Cửa sổ được xếp theo thứ tự **Tab 0 → 1 → 2…**, từ trái sang phải rồi xuống hàng, trên màn hình đang chứa cửa sổ quản lý. Tool giữ kích thước thực tế hiện tại của từng cửa sổ, không đưa tab lên trước và không đổi account đang chạy.
- Tab đang ngủ vẫn được gửi lệnh sắp xếp mà không bị đánh thức; vị trí mới được áp dụng khi bấm **Thức**. Các cửa sổ đang thu nhỏ hoặc chưa mở xong được bỏ qua và có thông báo; bấm **Hiện tab** rồi sắp xếp lại khi cần. Đợi nhóm mở tab hoặc ngủ/thức hoàn tất trước khi sắp xếp.
- Vị trí được tự lưu trong `data/accounts/layout.xml`, gắn với account + server. Mở lại tab hoặc đổi thứ tự `accounts.txt` vẫn dùng đúng vị trí đã lưu. File `layout.xml` của tool cũ được giữ nguyên và chỉ làm vị trí dự phòng cho account chưa có vị trí mới.
- Nếu màn hình không đủ chỗ, một số cửa sổ có thể chồng nhau; tool báo trong ô thông tin. Tab không bị thu nhỏ kích thước để ép vừa màn hình.

## Ngủ / thức hàng loạt tab

- **Ngủ đã chọn / Thức đã chọn** áp dụng cho các account được tick; nếu chưa tick thì dùng các hàng đang chọn, giống nút mở tài khoản.
- **Ngủ tất cả / Thức tất cả** áp dụng cho toàn bộ tab game đang chạy do bản `QLTK_Accounts.exe` này mở. Không tác động đến tab của tool cũ hoặc ứng dụng khác. Đợi lượt mở tab hoặc nhóm ngủ/thức hoàn tất trước khi gửi nhóm tiếp theo.
- Ngủ dùng `SuspendThread`, thức dùng `ResumeThread`, cùng cơ chế tool QLTK cũ. Tool giữ riêng các luồng đã tạm dừng và chỉ đánh thức một lần tương ứng; bấm Ngủ lặp không cộng thêm lượt dừng. Các tab được xử lý lần lượt bằng timer để giao diện tiếp tục phản hồi.
- Khi ngủ, trạng thái hiện **Đang ngủ**, các thông tin NV/đồ vẫn hiển thị bản **Đã lưu**. Dòng thông tin bên dưới có số tab đang ngủ. Tab ngủ vẫn tính vào MaxTab vì tiến trình còn tồn tại.
- Ngủ tạm dừng game, luồng tự động và kết nối mạng; ngủ lâu có thể bị server ngắt kết nối. Thức chạy tiếp tiến trình hiện có, không tự bảo đảm đăng nhập lại. Ngủ giảm hoạt động CPU nhưng không giải phóng RAM đang dùng.
- **Đóng tab đã chọn** và đóng tool sẽ đánh thức tab trước khi yêu cầu đóng. Yêu cầu ngủ còn trong hàng đợi của tab được đóng sẽ bị loại để tránh tạm dừng lại tab đó. **Hiện tab** không tự đánh thức tab; cần bấm Thức để game tiếp tục nhận thao tác.
- Nếu báo **Ngủ một phần — bấm Thức lại**, có luồng chưa đánh thức được; bấm Thức để thử lại và xem lỗi cụ thể ở ô thông báo. Các tab khác trong nhóm vẫn được xử lý.
- Mở lại **QLTK_Accounts.exe** sau khi cập nhật để thấy bốn nút mới. Chức năng đã được kiểm tra bằng tiến trình giả lập có nhịp hoạt động, không dùng tài khoản thật trong bài kiểm tra.

## Cấu hình và dữ liệu

### Thông tin lần đăng nhập gần nhất

- Tool tự lưu bản thông tin NV cuối đọc thành công cho từng **account + server**. Khi đóng tab, đọc lại danh sách hoặc mở lại tool, các cột thông tin vẫn hiện dữ liệu với nhãn **Đã lưu**. Trạng thái tab vẫn hiển thị riêng để phân biệt account đang chạy với dữ liệu cũ.
- Cột **Lần đăng nhập / cập nhật** hiển thị thời điểm vào game thành công và thời điểm lưu bản thông tin mới nhất. Giờ hiển thị theo thiết lập giờ của Windows; file lưu giờ UTC. Cột ở phía phải bảng, dùng thanh cuộn ngang nếu cần.
- Lần đăng nhập được xác nhận khi game đã vào màn chơi và có thông tin NV. Đăng nhập lại trong cùng tab cập nhật thời điểm này; chuyển map thông thường không tính thành lần đăng nhập mới. Chỉ gửi yêu cầu đăng nhập chưa được tính là thành công.
- Khi mở lại account, tool giữ bản cũ đến khi đọc được thông tin mới. Trạng thái chờ, lỗi đọc hoặc lỗi đăng nhập không xóa bản đã lưu. Các phần thống kê đang tắt sẽ giữ lại dữ liệu đã có; khi bật lại, game đọc mới phần đó.
- Dữ liệu lưu trong `data/accounts/history/<mã-account-server>.xml`, không chứa mật khẩu. Mỗi account + server giữ **một bản gần nhất**, chưa có danh sách mọi lần đăng nhập. Thay đổi mật khẩu hoặc thứ tự dòng không đổi liên kết; đổi username/server tạo liên kết khác.
- Dữ liệu không đổi thì không ghi lịch sử lại theo từng nhịp kiểm tra. **Cập nhật** là thời điểm lưu thay đổi thông tin hoặc ghi nhận lần đăng nhập mới, không phải mỗi nhịp báo hoạt động.
- Nếu nhập ID chưa từng được ghi trong bản cũ, tool báo **Chưa lưu dữ liệu ID**, không coi đó là số lượng bằng 0. Mở account để đọc các ID mới. Số lượng từ dữ liệu chưa đầy đủ vẫn giữ cảnh báo hành trang/rương chưa tải.
- Tool tự nhập lại `character.xml` của bản trước nếu file còn chứa thông tin NV hợp lệ. Nếu bản cũ chưa có thời điểm đăng nhập thì hiện **Chưa ghi nhận**; dữ liệu đã bị ghi thành trạng thái chờ hoặc bị xóa trước đó không thể khôi phục từ file này.
- Nếu không ghi được lịch sử, tool báo lỗi để kiểm tra quyền ghi/dung lượng và thử lại. Đóng tab hoặc tool theo nút đóng thông thường để tool thu nhận dữ liệu cuối còn đọc được. Lần đầu cập nhật chức năng này, mở lại **QLTK_Accounts.exe** và các tab game để nhận đầy đủ dấu thời gian từ cầu nối mới.

### Thống kê trang bị và vật phẩm

- Tick **Hiện đồ dưới +8** để mở cột **Đồ đang mặc dưới +8**. Cột hiển thị số món và tên/mức cộng, ví dụ `2 món | Áo +6 | Giày +7`. Chỉ tính trang bị thông thường đang mặc mà game cho phép nâng cấp (type 0–9, level từ 10); không tính ô trống, phụ kiện khác hoặc đồ đã đạt +8 trở lên. Dữ liệu chưa tải thì hiện **Chưa đọc được trang bị**.
- Tick **Hiện vật phẩm theo ID**, nhập ID vào ô **ID**, ngăn bằng dấu phẩy (ví dụ `123,456`), rồi bấm **Lưu cấu hình**. Hỗ trợ tối đa 128 ID từ 0 đến 32767; ID trùng được gộp, ID sai không ghi đè cấu hình.
- Cột **Vật phẩm theo ID** hiển thị `Tên vật phẩm: số lượng | Tên vật phẩm khác: số lượng`. Tên được tra từ dữ liệu game theo ID; chưa tra được thì dùng `ID 123`. Tổng số lượng cộng từ hành trang và rương, gộp các ô cùng ID; vật phẩm không xếp chồng được tính từng món. Không cộng đồ đang mặc vào tổng này. ID không có vật phẩm được bỏ qua.
- Nếu hành trang hoặc rương chưa tải, số hiện tại chỉ là phần đã đọc và có ghi **số lượng chưa đầy đủ**. Tool dùng luồng đọc rương sẵn có; nếu server chưa trả dữ liệu, mở rương trong game để tải.
- Công tắc hiện/ẩn cột có hiệu lực ngay. Bấm **Lưu cấu hình** để lưu công tắc và danh sách ID; các tab đã dùng cầu nối mới nhận cấu hình thống kê trong lần cập nhật tiếp theo (khoảng 2 giây ở chế độ thường, 5 giây ở VPS nhẹ), không cần mở lại tab khi đổi ID. Cấu hình dùng chung cho mọi account.
- Lần đầu cập nhật chức năng này, đóng tool và các tab game cũ rồi mở lại **QLTK_Accounts.exe**. Mặc định hai cột thống kê đều tắt; bật mục cần xem. Nếu bảng rộng, dùng thanh cuộn ngang và đưa chuột lên ô để đọc nội dung đầy đủ.
- Các mục lưu trong `settings.xml`: `ShowUnder8`, `ShowTrackedItems`, `TrackedItemIds`. Luồng thống kê chỉ đọc dữ liệu, không thực hiện nâng cấp hoặc chuyển đồ.

### Hiệu năng trên VPS

- **VPS nhẹ** mặc định bật khi cấu hình chưa có mục này. Tab đầu mở ngay, các tab còn lại cách nhau 5 giây (chế độ thường là 1,2 giây). Giãn mở giúp giảm tải khởi động dồn, nhưng tổng thời gian mở hết danh sách dài hơn. Tool không dùng `Sleep` để chặn giao diện trong lúc chờ mở tab.
- Khi đã vào game, cầu nối thống kê khoảng 5 giây/lần; trước khi vào game vẫn kiểm tra chọn nhân vật khoảng 2 giây/lần. Giao diện quản lý kiểm tra file mỗi 1,5 giây trong VPS nhẹ (0,8 giây ở chế độ thường).
- Tool chỉ phân tích lại XML khi file thay đổi, chỉ cập nhật ô UI khi nội dung đổi và không quét lại mọi hàng cho từng tab. Cấu hình thống kê và tên vật phẩm đã tra được dùng lại; số lượng hành trang/rương vẫn được đọc mới.
- Dữ liệu nhân vật không đổi thì không ghi file liên tục; cầu nối vẫn ghi nhịp báo hoạt động khoảng 10 giây để giữ phát hiện thông tin cũ sau 15 giây. Trạng thái đăng nhập chỉ ghi khi thay đổi.
- **Heap Java/tab (MB)** cho nhập 64–512. Cấu hình mới dùng 128 MB trong VPS nhẹ, hoặc 256 MB ở chế độ thường nếu chưa chỉ định. Đây là mức tối đa của heap Java (`-Xmx`), không phải tổng RAM tiến trình. VPS nhẹ còn dùng `-Xms16m` và Serial GC. Nếu bản game thiếu heap ở 128 MB, tăng lên 192 hoặc 256 rồi mở lại tab để thử.
- Bấm **Lưu cấu hình**. Nhịp mở tab và kiểm tra UI áp dụng cho lượt mở tiếp theo; nhịp thống kê được tab dùng cầu nối mới cập nhật. Thay đổi heap hoặc bộ thu gom rác cần đóng và mở lại tab. Tắt VPS nhẹ không tự thay giá trị heap đã chọn.
- Lần đầu cập nhật bản tối ưu này, đóng tool và tab game cũ rồi mở lại **QLTK_Accounts.exe**. Các mục lưu: `VpsLight`, `JavaHeapMb`.
- Bộ kiểm tra giả lập chạy được với heap 128 MB và Serial GC. Chưa đo hiệu năng với tài khoản thật trên VPS 1 CPU/2 GB RAM; số tab phù hợp phụ thuộc bản game và hoạt động từng nhân vật. Tool không thay đổi tốc độ chiến đấu, mạng hoặc luồng tự động trong game.

- Dùng `EmulatorPath`, `GamePath`, `TabWidth`, `TabHeight`, `MaxTab`, `AutoLogin` từ `settings.xml` và vị trí cửa sổ trong `layout.xml`.
- `MaxTab` là số tab đồng thời. Account vượt giới hạn chưa được mở; đóng bớt tab rồi bấm mở lại. Số tab được tính cả trong lúc game đang tải.
- `AutoLogin=true`: cầu nối gửi tài khoản/mật khẩu và chọn đúng server khi game sẵn sàng. `AutoLogin=false`: mở game với thông tin account đã lưu để đăng nhập thủ công.
- Dữ liệu bản mới nằm trong `data/accounts/tab_N`, liên kết account–tab nằm trong `data/accounts/tabs.xml`. Sửa thứ tự danh sách hoặc mật khẩu không đổi liên kết. Đổi username hoặc server tạo liên kết mới.
- Cột **Tab** và tiêu đề cửa sổ game bắt đầu từ **Tab 0, Tab 1, Tab 2…**, theo thứ tự account trong danh sách, kể cả account chưa mở. Đọc lại danh sách sau khi đổi thứ tự sẽ cập nhật số hiển thị; cửa sổ đang ngủ cập nhật tiêu đề khi được đánh thức. Mã dữ liệu nội bộ trong `tabs.xml` và thư mục `tab_N` được giữ nguyên để bảo toàn lịch sử và cấu hình từng account. Số cửa sổ đang chạy được hiển thị riêng ở dòng thông tin dưới cùng.
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
- `src/ItemStatistics.java`: đọc trang bị dưới +8, tra tên vật phẩm theo ID và cộng số lượng hành trang/rương.
- `src/CharacterSnapshot.cs`: đọc và hiển thị thông tin nhân vật trong một cột của QLTK.
- `src/AccountHistory.cs`: lưu và nạp bản thông tin của lần đăng nhập gần nhất theo account/server.
- `src/ProcessSleep.cs`: tạm dừng/đánh thức các luồng thuộc tiến trình tab game và quản lý các handle đã mở.

Máy build cần .NET Framework compiler và JDK. Máy chạy chỉ cần JRE đã đi kèm.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-accounts.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

Kiểm tra dùng dữ liệu giả và game fixture không kết nối mạng. Không tự mở hoặc đăng nhập account thật trong quá trình kiểm tra.

## Điều khiển mục Tự động từ QLTK

Sau khi game vào nhân vật, cột **Auto** cho biết QLTK đã đọc được cấu hình Tự động của tab. Tick các tài khoản cần chỉnh rồi bấm **Auto** ở dòng dưới phần Hiệu năng. Hộp thoại hiển thị đủ 32 lựa chọn và 7 giá trị số của menu **Tự động** trong game; cuộn xuống để xem các mục phía dưới.

Nếu các tab đang chọn có giá trị khác nhau, ô đánh dấu hiện trạng thái trung gian và ô số hiện **Khác nhau**. Chỉ những mục bạn thực sự chỉnh rồi bấm **Áp dụng** mới được gửi tới các tab đã chọn; các mục còn lại giữ nguyên giá trị riêng của từng tab. Các lựa chọn nhặt đồ loại trừ nhau được xử lý giống menu game. Giá trị số phải nằm trong giới hạn của mục tương ứng; nhập sai sẽ được báo ngay và không gửi lệnh.

Cột **Auto** hiện **Đang áp dụng** trong lúc chờ, **Đã áp dụng** sau khi game lưu và xác nhận, hoặc lý do lỗi nếu phiên bản game không hỗ trợ hay game từ chối giá trị. Tab đang ngủ hiện **Auto chờ thức**; lệnh được xử lý khi bạn bấm **Thức**, QLTK không tự đánh thức tab. Tab chưa vào nhân vật hoặc đã đóng chưa nhận lệnh. Nếu quá 30 giây không có xác nhận, kiểm tra tab game rồi thử lại.

Thiết lập được lưu bằng cơ chế RMS của game. Muốn dùng chức năng này sau khi cập nhật, hãy đóng QLTK cùng các tab game cũ rồi mở lại **QLTK_Accounts.exe** để nạp cầu nối mới. Game JAR có cấu trúc Auto khác bản đang cấu hình có thể hiện **Phiên bản game chưa hỗ trợ Auto**; việc đăng nhập và đọc thông tin nhân vật vẫn hoạt động riêng.
