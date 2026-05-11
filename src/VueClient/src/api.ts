export async function edgeRequest<T>(path: string, init?: RequestInit): Promise<T> {
  return requestJson<T>(path, init)
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })
  const text = await response.text()

  if (!response.ok) {
    throw new Error(readErrorMessage(text) || `${response.status} ${response.statusText}`)
  }

  return (text ? JSON.parse(text) : {}) as T
}

function readErrorMessage(text: string) {
  if (!text) return ''
  try {
    const parsed = JSON.parse(text) as { message?: string }
    return parsed.message || text
  } catch {
    return text
  }
}
