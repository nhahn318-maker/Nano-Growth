# 🛸 NANO GROWTH: ABSORB & EVOLVE (Game Design Document)

## 🔘 1. TẦM NHÌN & LỐI CHƠI CHÍNH (Core Loop)
Người chơi điều khiển một đám mây **Nano-Bot** thế hệ mới thoát ra từ phòng thí nghiệm. Mục tiêu: **Hấp thụ mọi thứ silicon và kim loại** để tiến hóa từ vi mô thành cơn bão Nano nuốt chửng thành phố.

**Vòng lặp (Core Loop):** 
`Di chuyển (Move)` -> `Hấp thụ (Absorb)` -> `Tích năng lượng (Grow)` -> `Biến hình (Morph)` -> `Chinh phục (Conquer)`.

---

## 🗺️ 2. THIẾT KẾ MAP: "PHÒNG THÍ NGHIỆM TƯƠNG LAI" (Cyber Lab)
Map được thiết kế theo dạng **Dark Mode** với đèn **Neon xanh/tím** để làm nổi bật bầy Nano.

### Phân khu Map:
*   **Khu A: Văn phòng (Giai đoạn yếu):** Bàn làm việc, ghế sofa, tủ hồ sơ. Đồ đạc nhỏ giúp Swarm to lên gấp đôi.
*   **Khu B: Phòng máy chủ (Giai đoạn biến hình):** Dãy Server phát sáng, dây cáp, Robot bảo vệ. Mở khóa kỹ năng **Kiếm**.
*   **Khu C: Kho bãi & Giao thông (Giai đoạn bá đạo):** Xe nâng, thùng container, máy bay Drone. Mở khóa kỹ năng **Vortex (Hố đen)**.

---

## 💻 3. DANH SÁCH VẬT THỂ & ĐIỂM (The Prey)
Mỗi vật thể bị hút sẽ tan biến (Dissolve Effect) và "nhảy số" điểm.

| Vật thể | Điểm (Growth) | Hiệu ứng đặc biệt |
| :--- | :--- | :--- |
| **Linh kiện nhỏ** (Chuột, Phím) | +10 | Tan nhanh, Swarm bắn tia điện nhỏ. |
| **Laptop / Máy tính** | +50 | **Highlight:** Rung màn hình, Glow rực rỡ, Swarm giật Pulse. |
| **Robot lính** | +150 | Phải dùng **Kiếm** để phá hủy -> Rã thành nano rồi mới bị hút. |
| **Server / Máy chủ lớn** | +300 | Hút vào tâm xoáy lâu hơn (0.5s), tiếng Static rè rè. |
| **Xe điện / Drone** | +1000 | **Power Trip:** Nhấc bổng vật thể lên không trung rồi nát vụn. |

---

## ⚡ 4. HỆ THỐNG SKILL (Morphing Mechanics)
Các trạng thái biến hình để quay video UA "kích thích Dopamine":

1.  **Dạng Mây (Default):** Hút tự động vật thể nhỏ xung quanh.
2.  **Dạng Kiếm (Sword):** 
    *   *Sử dụng:* Quét sạch robot và phá hủy vật cản cứng.
    *   *Video UA:* Hình ảnh bầy nano nén lại thành lưỡi kiếm rực sáng quét ngang màn hình.
3.  **Dạng Hố Đen (Vortex):** 
    *   *Sử dụng:* Luồng hút diện rộng (Pull Radius).
    *   *Video UA:* Đoạn cao trào hút cả một chiếc xe tank/xe điện vào tâm xoáy, số lượng hạt nhảy số từ 1,000 lên 10,000 cực nhanh.

---

## 🎬 5. KỊCH BẢN VIDEO UA - 10 GIÂY "VÀNG"
1.  **0s - 2s:** Đàn nano lướt qua laptop -> Laptop rã -> Swarm "giật" mạnh và to gấp 5 lần. Chữ: **"ABSORB TO GROW!"**
2.  **2s - 5s:** Bầy nano biến thành Cây Kiếm khổng lồ chém nát 3 con Robot. Chữ: **"MORPH INTO WEAPONS!"**
3.  **5s - 8s:** Đàn biến thành Vòng Xoáy (Vortex) hút cả một chiếc Drone cực lớn. Điểm số nhảy liên tục cháy màn hình.
4.  **8s - 10s:** Swarm bao trùm cả phòng lab. Nút **"PLAY NOW"** hiện lên.

---

## 🎨 6. CHECKLIST CÔNG VIỆC TRONG 7 NGÀY
- [x] Ngày 1-2: Code di chuyển & Particle Swarm cơ bản.
- [x] Ngày 3: Code cơ chế "Ăn" (Dissolve, Grow, Camera Shake).
- [ ] Ngày 4: Tạo hình dạng biến hình: Kiếm và Vortex.
- [ ] Ngày 5: Hiệu ứng Post-processing (Bloom, Color Grading).
- [ ] Ngày 6: Quay Gameplay UA.
- [ ] Ngày 7: Edit video & Subtitle.

---
*GDD này được lưu lại để nhóm phát triển theo dõi tiến độ sản xuất Video UA & Gameplay.*
