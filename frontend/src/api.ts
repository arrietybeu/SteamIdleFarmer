import type {
  Achievement,
  AuthStatus,
  Game,
  Job,
  QrStartResponse,
  UnlockResult,
} from "./types";
import { translate } from "./i18n";

/**
 * Fetch wrapper nhỏ gọn: luôn gửi cookie phiên (credentials: include),
 * tự parse JSON và ném Error với thông điệp dễ hiểu khi thất bại.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(path, {
      credentials: "include",
      ...init,
      headers: {
        ...(init?.body ? { "Content-Type": "application/json" } : {}),
        ...(init?.headers ?? {}),
      },
    });
  } catch {
    throw new Error(translate("api.networkError"));
  }

  if (!res.ok) {
    let message = translate("api.serverError", { status: res.status });
    try {
      const data: unknown = await res.json();
      if (
        data &&
        typeof data === "object" &&
        "error" in data &&
        typeof (data as { error: unknown }).error === "string"
      ) {
        message = (data as { error: string }).error;
      }
    } catch {
      /* body không phải JSON — giữ thông điệp mặc định */
    }
    throw new Error(message);
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  // ---- Auth ----
  authStatus: () => request<AuthStatus>("/api/auth/status"),
  qrStart: () => request<QrStartResponse>("/api/auth/qr/start", { method: "POST" }),
  logout: () => request<void>("/api/auth/logout", { method: "POST" }),

  // ---- Thư viện game ----
  games: () => request<Game[]>("/api/games"),
  achievements: (appId: number) =>
    request<Achievement[]>(`/api/games/${appId}/achievements`),
  unlockAchievements: (appId: number, apiNames: string[]) =>
    request<{ results: UnlockResult[] }>("/api/achievements/unlock", {
      method: "POST",
      body: JSON.stringify({ appId, apiNames }),
    }),

  // ---- Jobs treo game ----
  createJobs: (appIds: number[], hoursPerGame: number, jitterPct: number) =>
    request<{ jobs: Job[] }>("/api/jobs", {
      method: "POST",
      body: JSON.stringify({ appIds, hoursPerGame, jitterPct }),
    }),
  jobs: () => request<Job[]>("/api/jobs"),
  pauseJob: (id: string) => request<void>(`/api/jobs/${id}/pause`, { method: "POST" }),
  resumeJob: (id: string) => request<void>(`/api/jobs/${id}/resume`, { method: "POST" }),
  deleteJob: (id: string) => request<void>(`/api/jobs/${id}`, { method: "DELETE" }),
};
