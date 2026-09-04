# Game P2 - Scene Độc Lập: Checklist Triển Khai

## Phạm vi khóa

- Scene chính: `Assets/MainGame/GameP2.unity`.
- Thư mục riêng: `Assets/MainGame/P2/`.
- Không sửa nội dung các scene khác.
- `GameP2.unity` phải là bản sao kế thừa từ `Assets/MainGame/Game.unity` của Chapter 1 để tái sử dụng House, terrain, decor, lighting baseline và asset đã dựng.
- Giữ nguyên nhân vật/player controller, camera gameplay, UI cũ, model ma cũ, ánh sáng môi trường và không khí scene cũ.
- Chỉ thay lớp cốt truyện, audio thoại và cutscene/event của Chapter 2.
- Runtime/cutscene Chapter 1 liên quan intro/checkpoint/chase/ending cũ trong bản sao được tắt hoặc cấu hình lại để không đè lên flow P2.
- Layer P2 mới chỉ add thêm object/script trong scene copy và thư mục `Assets/MainGame/P2/`.

## 1. Khung scene và hướng chơi

- Tạo scene `GameP2` bằng cách copy từ `Game.unity`.
- Giữ root/environment chính của Chapter 1, đặc biệt là `House`, terrain và decor đã có.
- Dùng lại player/camera/ma/UI của Chapter 1.
- Tạo hệ thống objective/hint/subtitle/interact prompt bằng chính Canvas/Text style cũ.
- Tạo progression tuyến tính:
  1. Cổng vào và tiền sảnh.
  2. Đọc nhật ký Bà Lan.
  3. Lên khu hành lang tầng một, nghe log Đỗ Linh.
  4. Lật tranh gia đình bị úp.
  5. Lấy KEY_05 trong phòng ông Đỗ.
  6. Mở tủ trang sức phòng Bà Lan và nghe BL-LOG-02.
  7. Đi qua khu Ma Da/mặt nước an toàn.
  8. Vào phòng bé Linh.
  9. Tương tác búp bê nghe DL-LOG-03.
  10. Đọc phấn E-C-F-D-G trên tường.
  11. Nghe BL-LOG-03 từ hộp ghi âm nhỏ.
  12. Gõ tường tìm ô rỗng.
  13. Nhặt gương bạc.
  14. Trigger gương vỡ đồng loạt và Ma Vú Dài thức dậy.
  15. Chạy qua đường mảnh gương vỡ ra sân sau.
  16. Death sequence gương bạc dưới ánh trăng.

## 2. Runtime scripts riêng cho P2

- `P2GameController`: quản lý stage, objective, subtitles, audio one-shot, ending.
- `P2FirstPersonController`: di chuyển/nhìn first-person, khóa input khi cutscene.
- `P2Interactor`: raycast tương tác dùng phím E.
- `P2Interactable`: component khai báo loại tương tác.
- `P2OilLamp`: đèn dầu thay đèn pin, phím T thổi tắt/bật, tắt đèn thì khóa đọc chữ/tương tác lore.
- `P2GhostController`: Ma Vú Dài patrol nhẹ trước gương vỡ, tăng tốc sau gương vỡ, có trạng thái truy đuổi demo.
- `P2MirrorBreakable`: gom mirror trong scene, đổi visual nứt/vỡ khi event xảy ra.
- `P2WallKnockPuzzle`: gõ từng ô ốp gỗ, ô rỗng mở hốc tường.
- `P2GlassShardField`: mảnh gương vỡ phát tiếng động khi người chơi chạy qua.
- `P2DeathSequence`: camera/animation kéo Ngọc vào mặt gương, hiện card tên.

## 3. Audio và thoại

- Bind audio có sẵn trong `Assets/MainGame/Audio/Phan 2/`.
- Các trigger chính:
  - `ngọc 1_1`: mở đầu.
  - `ngọc 2_1`: nhắc gương bạc/mặt nước.
  - `linh 1_1`: DL-LOG-AUTO-CH2 ở hành lang.
  - `ngọc 3_1`: sau nhật ký Bà Lan.
  - `ngọc 4_1`: tự nhắc khi qua mặt nước.
  - `linh 2_1`: DL-LOG-03 từ búp bê.
  - `ngọc 5_1`: phản ứng nến/phòng Linh.
  - `ngọc 6_1`: thấy phấn E-C-F-D-G.
  - `ngọc 7_1`: tìm ô rỗng.
  - `ngọc 8_1`: nhặt gương bạc.
  - `ngọc 9_1`: phản ứng gương vỡ.
  - `ma 1_1`, `ma 2_1`: Ma Vú Dài.
  - `ma da 2_1`, `ma da 3_1`: death sequence.
  - `ngọc 10_1`: thoại cuối sân sau.

## 4. Layout playable demo

- Dùng lại layout Chapter 1 làm nền.
- Add marker/prop P2 lên các khu vực tương ứng theo cốt truyện mới.
- Đường mòn/cổng vào tối xám 1970 trên nền outdoor Ch.1.
- Tiền sảnh tầng trệt trên nền House Ch.1:
  - Gương lớn phủ vải đỏ.
  - Tranh gia đình.
  - Lối thư phòng.
- Thư phòng tầng trệt:
  - Nhật ký Bà Lan 2 đoạn.
- Cầu thang/hành lang tầng một:
  - Trigger audio Đỗ Linh.
  - Gương phủ vải và tranh chân dung.
  - Vùng patrol ban đầu của Ma Vú Dài.
- Phòng tranh úp:
  - Tranh lật được, thấy bóng người thứ năm.
- Phòng ông Đỗ:
  - KEY_05 trong bàn.
- Phòng Bà Lan:
  - Tủ trang sức khóa.
  - Hộp ghi âm BL-LOG-02.
- Phòng tắm/Ma Da:
  - Bồn nước tối, trigger cảnh báo không nhìn vào nước.
- Phòng bé Linh:
  - Nến không lay.
  - Búp bê.
  - Phấn E-C-F-D-G.
  - Hộp ghi âm BL-LOG-03.
  - Tường gõ và hốc giấu gương bạc.
- Đường chạy:
  - Mảnh kính vỡ.
  - Objective thoát ra sân sau.
- Sân sau:
  - Ánh trăng.
  - Death sequence với gương bạc.

## 5. Visual/lighting

- PSX/low-poly demo bằng primitive và prefab sẵn có khi phù hợp.
- Decay nhẹ năm 1970: bụi mỏng, đồ gỗ còn màu, rèm/vải chưa mục nát.
- Tối hơn sau gương vỡ.
- Mirror đồng loạt chuyển sang vật liệu nứt/vỡ.

## 6. Verification

- Unity compile không lỗi.
- Scene `GameP2` mở được và có root setup.
- Không ghi đè `Assets/MainGame/Game.unity`, `Menu.unity`, `EndingP2Transition.unity`, `Credits.unity`.
- Git commit theo mốc:
  1. Checklist/scope.
  2. Runtime P2.
  3. Scene builder.
  4. Built GameP2 scene + bound assets.
  5. Verification/build settings hoặc polish nếu cần.
