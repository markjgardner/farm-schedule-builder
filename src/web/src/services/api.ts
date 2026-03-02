import type { BarnConfig, BlackoutDate, Schedule, Worker } from '../types';

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (res.status === 401) {
    window.location.href = '/.auth/login/aad?post_login_redirect_uri=/';
    throw new Error('Unauthorized');
  }

  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }

  return res.json() as Promise<T>;
}

// Returns the Worker if registered, null if not (403)
export async function checkRegistration(): Promise<Worker | null> {
  const res = await fetch('/api/me', {
    headers: { 'Content-Type': 'application/json' },
  });
  if (res.status === 401) {
    window.location.href = '/.auth/login/aad?post_login_redirect_uri=/';
    throw new Error('Unauthorized');
  }
  if (res.status === 403) {
    return null;
  }
  if (!res.ok) {
    throw new Error(`API error: ${res.status} ${res.statusText}`);
  }
  return res.json() as Promise<Worker>;
}

export function getAvailability(
  windowStart: string,
): Promise<{ date: string; status: string }[]> {
  return apiFetch<{ date: string; status: string }[]>(
    `/api/availability/${encodeURIComponent(windowStart)}`,
  );
}

export function saveAvailability(
  windowStart: string,
  items: { date: string; status: string }[],
): Promise<void> {
  return apiFetch(`/api/availability/${encodeURIComponent(windowStart)}`, {
    method: 'PUT',
    body: JSON.stringify(items),
  });
}

export function getWorkers(): Promise<Worker[]> {
  return apiFetch<Worker[]>('/api/workers');
}

export function getAdminWorkers(): Promise<Worker[]> {
  return apiFetch<Worker[]>('/api/manage/workers');
}

export function addWorker(displayName: string, email: string): Promise<Worker> {
  return apiFetch<Worker>('/api/manage/workers', {
    method: 'POST',
    body: JSON.stringify({ displayName, email }),
  });
}

export function deactivateWorker(id: string): Promise<void> {
  return apiFetch<void>(`/api/manage/workers/${encodeURIComponent(id)}/deactivate`, {
    method: 'PUT',
  });
}

export function activateWorker(id: string): Promise<void> {
  return apiFetch<void>(`/api/manage/workers/${encodeURIComponent(id)}/activate`, {
    method: 'PUT',
  });
}

export function setWorkerAdmin(id: string, isAdmin: boolean): Promise<void> {
  return apiFetch<void>(`/api/manage/workers/${encodeURIComponent(id)}/admin`, {
    method: 'PUT',
    body: JSON.stringify({ isAdmin }),
  });
}

export function deleteWorker(id: string): Promise<void> {
  return apiFetch<void>(`/api/manage/workers/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}

export function triggerScheduleGeneration(): Promise<Schedule> {
  return apiFetch<Schedule>('/api/manage/schedule/generate', {
    method: 'POST',
  });
}

export function getWorkerAvailability(
  windowStart: string,
  workerId: string,
): Promise<{ date: string; status: string }[]> {
  return apiFetch<{ date: string; status: string }[]>(
    `/api/manage/availability/${encodeURIComponent(windowStart)}/${encodeURIComponent(workerId)}`,
  );
}

export function saveWorkerAvailability(
  windowStart: string,
  workerId: string,
  items: { date: string; status: string }[],
): Promise<void> {
  return apiFetch(
    `/api/manage/availability/${encodeURIComponent(windowStart)}/${encodeURIComponent(workerId)}`,
    {
      method: 'PUT',
      body: JSON.stringify(items),
    },
  );
}

// --- Barn Config ---

export function getBarnConfigs(): Promise<BarnConfig[]> {
  return apiFetch<BarnConfig[]>('/api/manage/config/barns');
}

export function setBarnConfig(barn: string, workersPerShift: number): Promise<BarnConfig> {
  return apiFetch<BarnConfig>(`/api/manage/config/barns/${encodeURIComponent(barn)}`, {
    method: 'PUT',
    body: JSON.stringify({ workersPerShift }),
  });
}

// --- Blackout Dates ---

export function getBlackouts(): Promise<BlackoutDate[]> {
  return apiFetch<BlackoutDate[]>('/api/manage/config/blackouts');
}

export function addBlackout(blackout: {
  date: string;
  description: string;
  barn?: string | null;
  shift?: string | null;
}): Promise<BlackoutDate> {
  return apiFetch<BlackoutDate>('/api/manage/config/blackouts', {
    method: 'POST',
    body: JSON.stringify(blackout),
  });
}

export function deleteBlackout(id: string): Promise<void> {
  return apiFetch<void>(`/api/manage/config/blackouts/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}
