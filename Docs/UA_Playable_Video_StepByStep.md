# UA Playable Ads Video - Step by Step

Muc tieu: quay 1 video 25-35s de apply vi tri UA Playable Ads Creator.

## Step 1 da chot san cho Nano Growth
- Main message: `Eat to 3000 -> Transform -> Destroy`
- Hook 0-3s: `CON 200 NUA DE HOA KIEM`
- CTA 28-30s: `BAN CO DAT 3000 KIP KHONG? TAP TO PLAY`
- Timeline 1 dong:
`0-3 Hook | 3-18 Farm + ap luc | 18-23 Unlock Sword | 23-28 Multi-kill Slash | 28-30 CTA`

Script text de ban dat vao video:
- Text mo dau: `CON 200 NUA DE HOA KIEM`
- Text unlock: `UNLOCKED: SWORD MODE`
- Text cuoi: `BAN CO DAT 3000 KIP KHONG?`
- Dong cuoi nho: `TAP TO PLAY`

## 1) Chot concept va thong diep (5-10 phut)
- [ ] Chot 1 cau thong diep: `Eat to 3000 -> Transform -> Destroy`.
- [ ] Chot hook 3s dau: `CON 200 NUA DE HOA KIEM`.
- [ ] Chot CTA cuoi video: `BAN CO DAT 3000 KIP KHONG? TAP TO PLAY`.

## 2) Chuan bi scene quay (15-30 phut)
- [ ] Tao scene quay rieng (khong dung scene test lon).
- [ ] Dat map gon, de nhin, it vat can roi.
- [ ] Dat spawn de nguoi choi dat ~3000 trong 15-20s.
- [ ] Kiem tra FPS on dinh khi vua di chuyen vua spawn.

## 3) Setup UI de nguoi xem hieu ngay (10-20 phut)
- [ ] Thanh tien do ro rang: `0/3000`.
- [ ] Text canh bao khi chua du: `CHUA DU 3000`.
- [ ] Text unlock khi du: `UNLOCKED: SWORD MODE`.
- [ ] Font to, dam, contrast cao (uu tien de doc tren mobile).

## 4) Lam feedback sinh dong (20-40 phut)
- [ ] Pop text khi an: `+20`, `+50`.
- [ ] Combo text: `x2`, `x3`, `x4` khi an lien tiep.
- [ ] SFX an/doi combo tang nhip theo combo.
- [ ] Camera shake nhe khi slash trung nhieu muc tieu.

## 5) Lam khoanh khac unlock da mat (15-30 phut)
- [ ] Them freeze 0.1-0.2s khi cham 3000.
- [ ] Them flash/VFX transform + am thanh manh.
- [ ] Chuyen mode muot, khong mat hat, khong giat khung.

## 6) Tao payoff ro (10-20 phut)
- [ ] Sap xep 1 cum enemy de co canh multi-kill.
- [ ] Quay duoc 1 canh slash "trung nhieu muc tieu cung luc".
- [ ] Co 1 canh "suýt thua" truoc khi unlock de tang cam xuc.

## 7) Quay footage (20-40 phut)
- [ ] Quay 5-10 take, moi take 30-40s.
- [ ] Moi take deu co: farm -> can 3000 -> transform -> slash.
- [ ] Giu UI gọn, khong de debug log/element du thua len hinh.
- [ ] Chon 1 take chinh + 1-2 take phu (canh can/slow/alternate angle).

## 8) Dung video chinh chu (30-60 phut)
- [ ] Cat theo timeline 30s (mau ben duoi).
- [ ] Can text to, ngan, de doc trong <1s.
- [ ] Dong bo cut voi beat nhac (dac biet luc unlock + slash).
- [ ] Them CTA 2s cuoi ro rang.
- [ ] Export ban doc: `1080x1920`, `30fps` hoac `60fps`.

---

## Timeline goi y 30 giay
0-3s:
- Hook text lon: `CON 200 NUA DE HOA KIEM`
- Nhan vat an lien tuc 2-3 muc tieu.

3-12s:
- Farm nhanh + combo feedback (`x2`, `x3`, `x4`).
- Thanh tien do tang ro.

12-18s:
- Gap ap luc nhe (enemy ep), tao cam giac gap nguy.

18-23s:
- Dat 3000 -> freeze ngan -> flash -> transform sword.

23-28s:
- Slash trung nhieu enemy trong 1-2 nhat.
- Hit effect + camera shake nhe.

28-30s:
- CTA: `BAN CO DAT 3000 KIP KHONG?`
- Dong duoi: `TAP TO PLAY`.

---

## QA truoc khi xuat file (bat buoc)
- [ ] Xem thu tren dien thoai: text co doc duoc khong?
- [ ] 3s dau da "hut mat" chua?
- [ ] Co bug mat hat/nhap nhay/roi FPS khong?
- [ ] Nguoi xem co hieu objective trong 1-2s dau khong?
- [ ] Ket video co CTA ro khong?

## Ten file de nop (goi y)
- `UA_Playable_NanoGrowth_V1_1080x1920.mp4`
- `UA_Playable_NanoGrowth_V2_1080x1920.mp4`
