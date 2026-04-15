const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (!response.ok) {
    throw new Error(await response.text())
  }

  return (await response.json()) as T
}

export function getJson<T>(path: string) {
  return request<T>(path)
}

export function postJson<TResponse, TBody>(path: string, body: TBody) {
  return request<TResponse>(path, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}
