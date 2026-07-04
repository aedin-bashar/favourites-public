export function apiValidationMessage(err: unknown): string | null {
  const messages = apiValidationMessages(err);
  return messages.length > 0 ? messages.join(' ') : null;
}

export function apiValidationMessages(err: unknown): string[] {
  if (typeof err !== 'object' || err === null || !('error' in err)) {
    return [];
  }

  const body = (err as { error?: unknown }).error;
  if (typeof body === 'string' && body.trim().length > 0) {
    return [body.trim()];
  }

  if (typeof body !== 'object' || body === null || !('errors' in body)) {
    return [];
  }

  const errors = (body as { errors?: unknown }).errors;
  if (typeof errors !== 'object' || errors === null) {
    return [];
  }

  const seen = new Set<string>();
  const messages: string[] = [];

  for (const value of Object.values(errors)) {
    if (!Array.isArray(value)) continue;

    for (const item of value) {
      if (typeof item !== 'string') continue;

      const message = item.trim();
      if (message.length === 0 || seen.has(message)) continue;

      seen.add(message);
      messages.push(message);
    }
  }

  return messages;
}
