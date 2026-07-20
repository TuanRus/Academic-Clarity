import type { ApiResponse } from '../types/api';

// Base URL lấy từ env (mặc định "/api" dùng Vite dev proxy).
const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '/api';

export class ApiError extends Error {
  statusCode: number;
  constructor(message: string, statusCode: number) {
    super(message);
    this.name = 'ApiError';
    this.statusCode = statusCode;
  }
}

// --- JWT token (gắn vào header Authorization của mọi request) ---
const TOKEN_KEY = 'stt_access_token';
const REFRESH_KEY = 'stt_refresh_token';

export function setAuthToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

function authHeaders(): Record<string, string> {
  const t = getAuthToken();
  return t ? { Authorization: `Bearer ${t}` } : {};
}

function buildUrl(path: string, params?: Record<string, unknown>): string {
  const url = `${BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;
  if (!params) return url;
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      qs.append(key, String(value));
    }
  }
  const query = qs.toString();
  return query ? `${url}?${query}` : url;
}

// --- Auto refresh token khi access token hết hạn (401) ---
let refreshing: Promise<boolean> | null = null;

function refreshTokens(): Promise<boolean> {
  if (refreshing) return refreshing;
  const run = (async () => {
    const accessToken = getAuthToken();
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (!refreshToken) return false;
    try {
      const res = await fetch(buildUrl('/Auth/refresh-token'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ accessToken, refreshToken }),
      });
      const body = (await res.json()) as ApiResponse<{ accessToken: string; refreshToken: string }>;
      if (!res.ok || !body || body.success === false || !body.data) return false;
      setAuthToken(body.data.accessToken);
      localStorage.setItem(REFRESH_KEY, body.data.refreshToken);
      return true;
    } catch {
      return false;
    }
  })();
  refreshing = run;
  run.finally(() => { if (refreshing === run) refreshing = null; });
  return run;
}

// Gửi request, tự refresh token + thử lại 1 lần nếu gặp 401.
async function execute<T>(path: string, makeInit: () => RequestInit, params?: Record<string, unknown>): Promise<T> {
  const url = buildUrl(path, params);
  let res = await fetch(url, makeInit());

  const isAuthFlow = path.includes('/Auth/refresh-token') || path.includes('/Auth/login');
  if (res.status === 401 && !isAuthFlow) {
    const ok = await refreshTokens();
    if (ok) res = await fetch(url, makeInit());
  }

  let body: ApiResponse<T> | null = null;
  try {
    body = (await res.json()) as ApiResponse<T>;
  } catch {
    // body không phải JSON hợp lệ
  }

  if (!res.ok || !body || body.success === false) {
    const message = body?.message ?? `Yêu cầu thất bại (HTTP ${res.status}).`;
    throw new ApiError(message, body?.statusCode ?? res.status);
  }

  return body.data;
}

/**
 * Gọi GET tới backend và tự bóc tách ApiResponse<T> -> T.
 * Ném ApiError nếu request lỗi hoặc backend trả success=false.
 */
export async function apiGet<T>(path: string, params?: Record<string, unknown>): Promise<T> {
  return execute<T>(path, () => ({ headers: { Accept: 'application/json', ...authHeaders() } }), params);
}

/**
 * Gọi POST (JSON body) tới backend và tự bóc tách ApiResponse<T> -> T.
 * Ném ApiError nếu request lỗi hoặc backend trả success=false.
 */
export async function apiPost<T>(path: string, payload?: unknown): Promise<T> {
  return apiSend<T>('POST', path, payload);
}

/** Gọi PUT (JSON body) tới backend và bóc tách ApiResponse<T> -> T. */
export async function apiPut<T>(path: string, payload?: unknown): Promise<T> {
  return apiSend<T>('PUT', path, payload);
}

/**
 * Upload file (multipart/form-data) tới backend và bóc tách ApiResponse<T> -> T.
 * KHÔNG set Content-Type để trình duyệt tự thêm boundary cho multipart.
 */
export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  return execute<T>(path, () => ({
    method: 'POST',
    headers: { Accept: 'application/json', ...authHeaders() },
    body: formData,
  }));
}

/** Gọi PATCH (JSON body) tới backend và bóc tách ApiResponse<T> -> T. */
export async function apiPatch<T>(path: string, payload?: unknown): Promise<T> {
  return apiSend<T>('PATCH', path, payload);
}

/** Gọi DELETE tới backend và bóc tách ApiResponse<T> -> T. */
export async function apiDelete<T>(path: string): Promise<T> {
  return apiSend<T>('DELETE', path);
}

function apiSend<T>(method: 'POST' | 'PUT' | 'PATCH' | 'DELETE', path: string, payload?: unknown): Promise<T> {
  return execute<T>(path, () => ({
    method,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...authHeaders() },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  }));
}
