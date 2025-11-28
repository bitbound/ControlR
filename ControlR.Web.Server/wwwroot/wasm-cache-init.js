// Bootstrapper for the WASM cache service worker.
// Registers the service worker on page load and exposes interop functions.

(function () {
  if (!('serviceWorker' in navigator)) {
    return;
  }

  window.addEventListener('load', async function () {
    try {
      const registration = await navigator.serviceWorker.register('/wasm-cache-sw.js', {
        scope: '/'
      });
      console.log('[WasmCache] Service worker registered:', registration.scope);
    } catch (error) {
      console.error('[WasmCache] Service worker registration failed:', error);
    }
  });
})();

/**
 * Clear the WASM cache. Returns true if successful.
 * @returns {Promise<boolean>}
 */
async function clearWasmCache() {
  const registration = await navigator.serviceWorker.ready;
  if (!registration.active) {
    // Fallback: clear directly if no active service worker
    return await caches.delete('blazor-wasm-cache-v1');
  }

  return new Promise((resolve) => {
    const messageChannel = new MessageChannel();
    messageChannel.port1.onmessage = (event) => {
      resolve(event.data?.success ?? false);
    };
    registration.active.postMessage(
      { type: 'CLEAR_WASM_CACHE' },
      [messageChannel.port2]
    );
    // Timeout after 5 seconds
    setTimeout(() => resolve(false), 5000);
  });
}

/**
 * Get the status of the WASM cache.
 * @returns {Promise<{cacheName: string, entryCount: number} | null>}
 */
async function getWasmCacheStatus() {
  const registration = await navigator.serviceWorker.ready;
  if (!registration.active) {
    return null;
  }

  return new Promise((resolve) => {
    const messageChannel = new MessageChannel();
    messageChannel.port1.onmessage = (event) => {
      resolve(event.data);
    };
    registration.active.postMessage(
      { type: 'GET_CACHE_STATUS' },
      [messageChannel.port2]
    );
    // Timeout after 5 seconds
    setTimeout(() => resolve(null), 5000);
  });
}
