export function parseBoolean(value: string): boolean | null {
  if (/^\s*true\s*$/i.test(value)) {
    return true;
  }

  if (/^\s*false\s*$/i.test(value)) {
    return false;
  }

  return null;
}

export function waitFor(
  condition: () => boolean,
  intervalMs: number,
  timeoutMs: number,
): Promise<boolean> {
  return new Promise((resolve, reject) => {
    try {
      if (condition()) {
        resolve(true);
        return;
      }
    } catch (err) {
      reject(err);
    }

    const interval = setInterval(() => {
      try {
        if (condition()) {
          clearInterval(interval);
          resolve(true);
          return;
        }
      } catch (err) {
        reject(err);
      }
    }, intervalMs);

    setTimeout(() => {
      clearInterval(interval);
      resolve(false);
    }, timeoutMs);
  });
}
