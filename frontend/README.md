# Xưởng Cày — Frontend

Giao diện web cho **Steam Idle & Achievement Farmer** (React + Vite + TypeScript).
App đa người dùng: mỗi trình duyệt là một phiên riêng, ai đăng nhập Steam của người nấy.

## Chạy dev

```bash
npm install
npm run dev
```

Dev server sẽ proxy `/api` và `/ws` sang backend ở `http://localhost:5080`
(cấu hình trong `vite.config.ts`) — nhớ bật backend trước.

## Build production

```bash
npm run build
```

Kết quả nằm trong thư mục `dist/` — cho backend serve tĩnh hoặc đặt sau reverse proxy
cùng origin với `/api` và `/ws`.

## Cấu trúc chính

- `src/api.ts` — fetch wrapper (cookie phiên, xử lý lỗi)
- `src/ws.ts` — WebSocket `/ws`, tự reconnect
- `src/toast.tsx` — hệ thống thông báo
- `src/components/` — AuthGate (QR), TopBar, GameSidebar, IdleTab, JobCard, ManualTab, ui
- `src/styles.css` — theme (CSS variables, dark + vàng thành tựu)
