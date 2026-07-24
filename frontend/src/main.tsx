import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

// Font đóng gói offline (không CDN): Be Vietnam Pro cho nội dung,
// Chakra Petch cho tiêu đề/số liệu, Playfair Display (italic) chỉ dùng
// cho dòng "truyền thuyết" ở màn đăng nhập. Playfair có subset Vietnamese
// nên hiển thị đầy đủ dấu (ế, ữ, ả, ị, ầ, ẵ…).
import "@fontsource/be-vietnam-pro/400.css";
import "@fontsource/be-vietnam-pro/500.css";
import "@fontsource/be-vietnam-pro/600.css";
import "@fontsource/chakra-petch/600.css";
import "@fontsource/chakra-petch/700.css";
import "@fontsource/playfair-display/500-italic.css";

import "./styles.css";
import App from "./App";
import { ToastProvider } from "./toast";
import { I18nProvider } from "./i18n";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <I18nProvider>
      <ToastProvider>
        <App />
      </ToastProvider>
    </I18nProvider>
  </StrictMode>,
);
