import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
} from "react";
import type { ReactNode } from "react";
import { useI18n } from "./i18n";

export type ToastKind = "success" | "error" | "info" | "gold";

export interface ToastInput {
  kind: ToastKind;
  title: string;
  detail?: string;
}

interface ToastItem extends ToastInput {
  id: number;
  leaving: boolean;
}

interface ToastContextValue {
  push: (toast: ToastInput) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const MAX_VISIBLE = 5;
const LIFETIME_MS = 4600;
const LEAVE_MS = 260;

const KIND_ICON: Record<ToastKind, string> = {
  success: "M5 13l4 4L19 7",
  error: "M6 6l12 12M18 6L6 18",
  info: "M12 8h.01M12 12v4",
  gold: "M8 21h8m-4-4v4M6 4h12v3a6 6 0 0 1-12 0V4zM6 6H4v1a4 4 0 0 0 3 3.9M18 6h2v1a4 4 0 0 1-3 3.9",
};

export function ToastProvider({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((prev) =>
      prev.map((t) => (t.id === id ? { ...t, leaving: true } : t)),
    );
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, LEAVE_MS);
  }, []);

  const push = useCallback(
    (toast: ToastInput) => {
      const id = nextId.current++;
      setToasts((prev) => {
        const next = [...prev, { ...toast, id, leaving: false }];
        // Giữ tối đa MAX_VISIBLE — bỏ toast cũ nhất khi tràn.
        return next.length > MAX_VISIBLE
          ? next.slice(next.length - MAX_VISIBLE)
          : next;
      });
      setTimeout(() => dismiss(id), LIFETIME_MS);
    },
    [dismiss],
  );

  const value = useMemo(() => ({ push }), [push]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="toast-viewport" aria-live="polite">
        {toasts.map((item) => (
          <div
            key={item.id}
            className={`toast toast--${item.kind}${item.leaving ? " toast--leaving" : ""}`}
            role="status"
          >
            <span className="toast__icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d={KIND_ICON[item.kind]} />
                {item.kind === "info" && <circle cx="12" cy="12" r="9" />}
              </svg>
            </span>
            <span className="toast__body">
              <span className="toast__title">{item.title}</span>
              {item.detail && <span className="toast__detail">{item.detail}</span>}
            </span>
            <button
              className="toast__close"
              onClick={() => dismiss(item.id)}
              aria-label={t("toast.close")}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                <path d="M6 6l12 12M18 6L6 18" />
              </svg>
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within <ToastProvider>");
  return ctx;
}
