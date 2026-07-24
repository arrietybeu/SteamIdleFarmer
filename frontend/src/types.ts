/** Kiểu dữ liệu dùng chung — bám sát hợp đồng API của backend. */

export type AuthState =
  | "disconnected"
  | "awaiting_qr"
  | "logging_in"
  | "logged_in"
  | "error";

export interface AuthStatus {
  state: AuthState;
  persona?: string;
  steamId?: string;
  error?: string;
}

export interface QrStartResponse {
  challengeUrl: string;
  sessionId: string;
}

export interface Game {
  appId: number;
  name: string;
  iconUrl?: string;
  playtimeHours: number;
  hasStats: boolean;
}

export interface Achievement {
  apiName: string;
  displayName: string;
  description?: string;
  hidden: boolean;
  unlocked: boolean;
  protected: boolean;
  iconUrl?: string;
}

export interface UnlockResult {
  apiName: string;
  ok: boolean;
  error?: string;
}

export type JobState = "pending" | "running" | "paused" | "completed" | "failed";

export interface Job {
  id: string;
  appId: number;
  name: string;
  state: JobState;
  hoursTarget: number;
  hoursElapsed: number;
  totalAchievable: number;
  unlockedCount: number;
  nextUnlockInSec?: number | null;
}

/** Sự kiện đẩy qua WebSocket /ws */
export type WsEvent =
  | { type: "auth"; status: AuthStatus }
  | { type: "challenge"; challengeUrl: string }
  | { type: "jobs"; jobs: Job[] }
  | { type: "achievement"; appId: number; apiName: string; name: string };
