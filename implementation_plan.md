# 🏪 Shop Simulator – Module Breakdown (MVP Demo)

> **Mục tiêu:** Xây dựng vòng lặp gameplay hoàn chỉnh trong 2 tuần.
> **Engine:** Unity (đã có sẵn PolygonShops, KayKit, FlatKit, PrimeTween, CodeMonkey Toolkit)
> **Style:** 3D Low-poly, Top-down hoặc Third-person

---

## 🗺️ Tổng quan vòng lặp game (Core Game Loop)

```
[Kho hàng] → [Xếp kệ] → [Khách đến mua] → [Thanh toán] → [Tiền về] → [Mua thêm hàng]
```

---

## 📦 MODULE 1 — Item & Inventory Data System
**Ưu tiên: ⭐⭐⭐⭐⭐ (Làm TRƯỚC TIÊN — mọi module khác phụ thuộc)**

### Mục tiêu
Định nghĩa dữ liệu mặt hàng và hệ thống kho hàng (Inventory) trung tâm.

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `ItemSO.cs` | ScriptableObject định nghĩa 1 mặt hàng (tên, giá bán, giá nhập, icon, prefab 3D) |
| `InventorySystem.cs` | Singleton quản lý kho: số lượng từng loại item trong kho |
| `ItemDatabase.cs` | Danh sách tất cả ItemSO trong game (dùng Resources.Load hoặc SO list) |

### Chi tiết
- **ItemSO fields:** `itemName`, `buyPrice`, `sellPrice`, `sprite`, `prefab3D`, `itemID (enum hoặc string)`
- **InventorySystem:** Dictionary `<ItemSO, int>` lưu số lượng trong kho
- Chỉ cần **3 mặt hàng** cho demo: `Táo`, `Sữa`, `Bánh mì`
- Dùng `ScriptableObject` để dễ mở rộng sau này

---

## 🪑 MODULE 2 — Shelf System (Kệ hàng)
**Ưu tiên: ⭐⭐⭐⭐⭐**

### Mục tiêu
Kệ hàng chứa nhiều slot, mỗi slot chứa đúng 1 vật phẩm (ItemObject). Player xếp hàng bằng cách đặt ItemObject vào slot trống.

### Thiết kế cấu trúc

```
Shelf (ShelfController)
├── ShelfSlot_0  (ShelfSlot) ← chứa 1 ItemObject hoặc trống
├── ShelfSlot_1  (ShelfSlot)
├── ShelfSlot_2  (ShelfSlot)
└── ...
```

- **1 `ShelfController`** quản lý một danh sách `ShelfSlot[]`
- **1 `ShelfSlot`** chứa đúng **1 `ItemObject`** hoặc rỗng (`null`)
- Player mang `ItemObject` đến → tương tác với Shelf → tự động đặt vào slot trống đầu tiên

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `ShelfSlot.cs` | Một ô trên kệ: có/không có 1 `ItemObject`. Có `Transform` vị trí đặt đồ. |
| `ShelfController.cs` | Quản lý list `ShelfSlot`, expose `AddItem(ItemObject)` / `TakeItem()` cho AI khách |

> ⚠️ **UI tạm thời bỏ qua** — xếp hàng bằng tương tác trực tiếp, không mở menu.

### Chi tiết
- `ShelfSlot` có `bool IsOccupied` → true khi đang chứa ItemObject
- Khi Player `Interact()` với kệ → `ShelfController` tìm slot trống → đặt item từ tay player vào slot
- Khi khách muốn lấy đồ → `ShelfController.TakeItem(slotIndex)` → trả về `ItemObject`, slot trở thành rỗng
- Kiểm tra `IsEmpty` (toàn bộ slot trống) → dùng để AI khách bỏ qua kệ này

---

## 🚶 MODULE 3 — Customer AI (Khách hàng)
**Ưu tiên: ⭐⭐⭐⭐⭐**

### Mục tiêu
Khách hàng tự động đi vào cửa hàng, chọn đồ, xếp hàng ở quầy tính tiền.

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `CustomerAgent.cs` | State machine: `Enter → GoToShelf → PickItem → GoToCheckout → WaitInQueue → Leave` |
| `CustomerSpawner.cs` | Spawn khách theo timer hoặc theo số lượng khách tối đa |
| `CustomerQueue.cs` | Singleton quản lý hàng chờ ở quầy tính tiền |

### Customer State Machine
```
[Spawn tại cửa vào]
        ↓
[GoToShelf] → NavMesh đến ShelfController ngẫu nhiên còn hàng
        ↓
[PickItem] → Chờ 1-2 giây (animation giả), lấy item từ Shelf
        ↓
[GoToCheckout] → NavMesh đến vị trí quầy tính tiền
        ↓
[WaitInQueue] → Xếp hàng theo thứ tự (Queue position offset)
        ↓
[Leave] → NavMesh ra cửa → Despawn
```

### NavMesh Setup
- Dùng **Unity NavMesh** (AI Navigation package)
- Bake NavMesh trên scene một lần
- Khách dùng `NavMeshAgent` component

---

## 💳 MODULE 4 — Checkout System (Quầy tính tiền)
**Ưu tiên: ⭐⭐⭐⭐⭐**

### Mục tiêu
Player đứng sau quầy, click để "quét" từng món hàng của khách, tiền được cộng vào cửa hàng.

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `CheckoutCounter.cs` | Quầy tính tiền: nhận khách đầu hàng, hiển thị các item cần quét |
| `CheckoutUI.cs` | UI hiển thị danh sách item, tổng tiền, nút "Confirm/Quét" |
| `MoneyManager.cs` | Singleton quản lý tổng tiền của cửa hàng |

### Flow
1. Khách đến đầu hàng → `CheckoutCounter` nhận khách, lấy list item của khách
2. Hiện UI: danh sách item + giá từng món + tổng tiền
3. Player click **"Tính tiền"** → `MoneyManager.AddMoney(total)` → hiệu ứng tiền bay lên
4. Khách nhận lại "túi đồ" (optional) → State chuyển sang `Leave`

---

## 💰 MODULE 5 — Money & Economy System
**Ưu tiên: ⭐⭐⭐⭐**

### Mục tiêu
Quản lý vốn cửa hàng, cho phép mua hàng từ nhà cung cấp.

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `MoneyManager.cs` | Lưu tổng tiền, expose event `OnMoneyChanged` |
| `SupplierShopUI.cs` | UI mua hàng: chọn item, số lượng, trừ tiền, cộng vào kho |

### Chi tiết
- Số tiền ban đầu: `$500`
- Mua hàng từ nhà cung cấp: trừ `buyPrice × quantity` từ `MoneyManager`
- Bán hàng cho khách: cộng `sellPrice × quantity`
- UI HUD: hiển thị tổng tiền ở góc màn hình, animate khi thay đổi

---

## 🎮 MODULE 6 — Player Controller
**Ưu tiên: ⭐⭐⭐⭐**

### Mục tiêu
Điều khiển nhân vật player di chuyển trong cửa hàng, tương tác với kệ và quầy.

### Scripts cần tạo
| File | Mô tả |
|---|---|
| `PlayerController.cs` | Di chuyển WASD/Joystick, camera follow |
| `PlayerInteract.cs` | Raycast phát hiện đối tượng có thể tương tác (IInteractable) |
| `IInteractable.cs` | Interface: `Interact()` — dùng cho Shelf, CheckoutCounter, SupplierBox |

### Chi tiết
- Camera: **Third-person** hoặc **Isometric** (dễ setup hơn)
- Input: dùng **New Input System** (đã có `InputSystem_Actions.inputactions`)
- Tương tác: Nhấn `E` hoặc `Click` khi đến gần đối tượng → Gọi `Interact()`

---

## 🖥️ MODULE 7 — HUD & UI System
**Ưu tiên: ⭐⭐⭐**

### Mục tiêu
Giao diện người chơi: tiền, thông báo, menu kho hàng.

### Scripts/Prefabs cần tạo
| File | Mô tả |
|---|---|
| `HUDManager.cs` | Cập nhật UI: tiền, ngày, thông báo |
| `MoneyFloatingText.cs` | Text "+$X" bay lên khi có giao dịch (dùng PrimeTween) |
| `NotificationUI.cs` | Toast notification: "Kệ A hết hàng!", "Khách đang chờ!" |
| `ShelfStockUI.cs` | Panel chọn hàng để xếp vào kệ |
| `SupplierUI.cs` | Panel mua hàng từ nhà cung cấp |

### HUD Layout
```
[💰 $1,250]          [⏰ Ngày 3]
                           [🔔 Kệ Táo: Hết hàng!]

            [GAME VIEW]

[Hướng dẫn: E = Tương tác | TAB = Kho hàng]
```

---

## ✨ MODULE 8 — Juice & Visual Feedback (Hiệu ứng "Juicy")
**Ưu tiên: ⭐⭐⭐ (Làm SAU CÙNG nhưng cực quan trọng cho demo)**

### Mục tiêu
Làm game cảm giác mượt mà, vui, hút mắt khi demo.

### Scripts/Effects
| File | Mô tả |
|---|---|
| `MoneyParticle.cs` | Hiệu ứng tiền xu bay ra khi thanh toán (dùng PolygonParticleFX) |
| `ShelfFillEffect.cs` | Hiệu ứng đồ vật "pop" vào kệ khi xếp hàng (PrimeTween scale bounce) |
| `CustomerSatisfaction.cs` | Icon 😊 hoặc 😡 bay lên trên đầu khách sau khi mua |
| `CameraShake.cs` | Rung nhẹ camera khi có giao dịch lớn |

### Âm thanh cần có
- `kaching.wav` — tiếng nhận tiền
- `shelf_stock.wav` — tiếng xếp hàng vào kệ
- `beep.wav` — tiếng quét mã
- `door_bell.wav` — tiếng khách vào/ra
- `bg_music.mp3` — nhạc nền vui tươi

---

## 📅 Lịch triển khai cho AI (Thứ tự Module)

```
TUẦN 1:
┌─────────────────────────────────────────────────────┐
│ Ngày 1  │ MODULE 1 — Item & Inventory Data System    │
│ Ngày 2  │ MODULE 2 — Shelf System                    │
│ Ngày 3  │ MODULE 3 — Customer AI (State Machine)     │
│ Ngày 4  │ MODULE 4 — Checkout System                 │
│ Ngày 5  │ MODULE 5 — Money & Economy                 │
│ Ngày 6  │ MODULE 6 — Player Controller               │
│ Ngày 7  │ TEST — Chạy vòng lặp end-to-end lần đầu   │
└─────────────────────────────────────────────────────┘

TUẦN 2:
┌─────────────────────────────────────────────────────┐
│ Ngày 8  │ MODULE 7 — HUD & UI System                 │
│ Ngày 9  │ MODULE 8 — Juice & Visual Feedback         │
│ Ngày 10 │ Tích hợp Assets (PolygonShops, KayKit)     │
│ Ngày 11 │ Bug fix — AI bị kẹt, UI lỗi               │
│ Ngày 12 │ Balance — giá tiền, tốc độ khách, số lượng │
│ Ngày 13 │ Build & Test (.exe hoặc WebGL)             │
│ Ngày 14 │ Buffer — Sửa lỗi phát sinh cuối           │
└─────────────────────────────────────────────────────┘
```

---

## 🗂️ Cấu trúc thư mục _Scripts đề xuất

```
Assets/_Scripts/
├── Core/
│   ├── ItemSO.cs
│   ├── ItemDatabase.cs
│   └── GameManager.cs
├── Inventory/
│   ├── InventorySystem.cs
│   └── MoneyManager.cs
├── Shelf/
│   ├── ShelfSlot.cs
│   ├── ShelfController.cs
│   └── ShelfUI.cs
├── Customer/
│   ├── CustomerAgent.cs
│   ├── CustomerSpawner.cs
│   └── CustomerQueue.cs
├── Checkout/
│   ├── CheckoutCounter.cs
│   └── CheckoutUI.cs
├── Player/
│   ├── PlayerController.cs
│   ├── PlayerInteract.cs
│   └── IInteractable.cs
├── UI/
│   ├── HUDManager.cs
│   ├── MoneyFloatingText.cs
│   ├── NotificationUI.cs
│   └── SupplierUI.cs
└── Effects/
    ├── MoneyParticle.cs
    ├── ShelfFillEffect.cs
    └── CameraShake.cs
```

---

## ✅ Điều kiện "Demo thành công"

- [ ] Khách tự động vào → lấy đồ → xếp hàng → chờ tính tiền
- [ ] Player click "Tính tiền" → tiền cộng lên → khách ra về
- [ ] Kệ hết hàng → Player vào kho → chọn hàng → kệ được fill lại
- [ ] HUD hiển thị số tiền hiện tại, cập nhật realtime
- [ ] Không có lỗi crash trong 3 phút chơi liên tục

---

> **Ghi chú:** Mỗi khi bắt đầu module mới, hãy nói: *"Làm Module X"* — AI sẽ viết toàn bộ code, tạo script trong Unity, và hướng dẫn setup từng bước.
