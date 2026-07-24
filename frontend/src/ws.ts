import type { WsEvent } from "./types";

/**
 * Kết nối WebSocket /ws cùng origin (ws/wss tự chọn theo giao thức trang),
 * tự reconnect với backoff. Trả về hàm hủy kết nối.
 */
export function connectWs(
  onEvent: (event: WsEvent) => void,
  onStatus?: (online: boolean) => void,
): () => void {
  let socket: WebSocket | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let retryDelay = 1000;
  let disposed = false;

  function open() {
    if (disposed) return;
    const proto = location.protocol === "https:" ? "wss://" : "ws://";
    try {
      socket = new WebSocket(`${proto}${location.host}/ws`);
    } catch {
      scheduleRetry();
      return;
    }

    socket.onopen = () => {
      retryDelay = 1000;
      onStatus?.(true);
    };

    socket.onmessage = (msg: MessageEvent) => {
      if (typeof msg.data !== "string") return;
      try {
        const event = JSON.parse(msg.data) as WsEvent;
        if (event && typeof event === "object" && "type" in event) {
          onEvent(event);
        }
      } catch {
        /* bỏ qua frame không phải JSON */
      }
    };

    socket.onclose = () => {
      socket = null;
      onStatus?.(false);
      scheduleRetry();
    };

    socket.onerror = () => {
      socket?.close();
    };
  }

  function scheduleRetry() {
    if (disposed || retryTimer !== null) return;
    retryTimer = setTimeout(() => {
      retryTimer = null;
      open();
    }, retryDelay);
    retryDelay = Math.min(retryDelay * 2, 10000);
  }

  open();

  return () => {
    disposed = true;
    if (retryTimer !== null) clearTimeout(retryTimer);
    if (socket) {
      socket.onclose = null;
      socket.close();
    }
  };
}
