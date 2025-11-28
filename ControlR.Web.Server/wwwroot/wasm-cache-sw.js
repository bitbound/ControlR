// Service worker for caching Blazor WASM assets using stale-while-revalidate strategy.
// Serves cached files immediately for fast startup, then revalidates in the background.
// If files have changed, the cache is updated for the next load.

const CACHE_NAME = 'blazor-wasm-cache-v1';

// Asset patterns to cache with stale-while-revalidate
const CACHE_PATTERNS = [
  /\/_framework\/.*\.wasm$/,
  /\/_framework\/.*\.dat$/,
  /\/_framework\/.*\.js$/,
];

// Patterns that should always go to network first
// NOTE: We intentionally do NOT put blazor.boot.json here.
// If we serve a fresh boot.json but stale DLLs, Blazor's integrity checks will fail and crash the app.
// By serving both from cache (stale-while-revalidate), we ensure the manifest matches the assemblies.
// The app will update in the background and apply on the next reload.
const NETWORK_FIRST_PATTERNS = [
  // Add any specific API endpoints or non-versioned assets here if needed
];

self.addEventListener('install', (event) => {
  console.log('[WasmCache] Installing...');
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  console.log('[WasmCache] Activating...');
  event.waitUntil(
    Promise.all([
      // Clean up old cache versions
      caches.keys().then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((name) => name.startsWith('blazor-wasm-cache-') && name !== CACHE_NAME)
            .map((name) => {
              console.log('[WasmCache] Deleting old cache:', name);
              return caches.delete(name);
            })
        );
      }),
      self.clients.claim()
    ])
  );
});

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  if (event.request.method !== 'GET') {
    return;
  }

  const shouldCache = CACHE_PATTERNS.some((pattern) => pattern.test(url.pathname));
  if (!shouldCache) {
    return;
  }

  const isNetworkFirst = NETWORK_FIRST_PATTERNS.some((pattern) => pattern.test(url.pathname));

  if (isNetworkFirst) {
    event.respondWith(networkFirstStrategy(event.request));
  } else {
    event.respondWith(staleWhileRevalidateStrategy(event.request));
  }
});

/**
 * Stale-while-revalidate: Serve from cache immediately, revalidate in background.
 * If the resource has changed, update the cache for next time.
 */
async function staleWhileRevalidateStrategy(request) {
  const cache = await caches.open(CACHE_NAME);
  const cachedResponse = await cache.match(request);

  // Start the network fetch in the background
  const networkFetchPromise = fetch(request).then(async (networkResponse) => {
    if (networkResponse.ok) {
      // Check if the response has actually changed before updating cache
      const shouldUpdate = await hasResponseChanged(cachedResponse, networkResponse.clone());
      if (shouldUpdate) {
        console.log('[WasmCache] Updating cache:', request.url);
        await cache.put(request, networkResponse.clone());
      }
    }
    return networkResponse;
  }).catch((error) => {
    console.warn('[WasmCache] Network fetch failed:', request.url, error);
    return null;
  });

  if (cachedResponse) {
    // Serve from cache immediately, don't wait for network
    // Fire and forget the network request for background revalidation
    networkFetchPromise.catch(() => { });
    return cachedResponse;
  }

  // No cache available, wait for network
  console.log('[WasmCache] Cache miss, fetching:', request.url);
  const networkResponse = await networkFetchPromise;
  if (networkResponse) {
    return networkResponse;
  }

  // Both cache and network failed
  return new Response('Network error', { status: 503, statusText: 'Service Unavailable' });
}

/**
 * Network-first strategy for critical files like boot.json.
 * Falls back to cache if network fails.
 */
async function networkFirstStrategy(request) {
  const cache = await caches.open(CACHE_NAME);

  try {
    const networkResponse = await fetch(request);
    if (networkResponse.ok) {
      await cache.put(request, networkResponse.clone());
    }
    return networkResponse;
  } catch (error) {
    console.warn('[WasmCache] Network failed, trying cache:', request.url);
    const cachedResponse = await cache.match(request);
    if (cachedResponse) {
      return cachedResponse;
    }
    return new Response('Network error', { status: 503, statusText: 'Service Unavailable' });
  }
}

/**
 * Compare responses to determine if cache should be updated.
 * Uses ETag or Last-Modified headers, or content length as fallback.
 */
async function hasResponseChanged(cachedResponse, networkResponse) {
  if (!cachedResponse) {
    return true;
  }

  // Compare ETags
  const cachedEtag = cachedResponse.headers.get('ETag');
  const networkEtag = networkResponse.headers.get('ETag');
  if (cachedEtag && networkEtag) {
    return cachedEtag !== networkEtag;
  }

  // Compare Last-Modified
  const cachedLastModified = cachedResponse.headers.get('Last-Modified');
  const networkLastModified = networkResponse.headers.get('Last-Modified');
  if (cachedLastModified && networkLastModified) {
    return cachedLastModified !== networkLastModified;
  }

  // Compare Content-Length as fallback
  const cachedLength = cachedResponse.headers.get('Content-Length');
  const networkLength = networkResponse.headers.get('Content-Length');
  if (cachedLength && networkLength) {
    return cachedLength !== networkLength;
  }

  // If we can't compare, assume it changed
  return true;
}

// Message handlers for interop with the main thread
self.addEventListener('message', async (event) => {
  if (!event.data || !event.data.type) {
    return;
  }

  switch (event.data.type) {
    case 'CLEAR_WASM_CACHE': {
      const deleted = await caches.delete(CACHE_NAME);
      console.log('[WasmCache] Cache cleared:', deleted);
      if (event.ports && event.ports[0]) {
        event.ports[0].postMessage({ success: deleted });
      }
      break;
    }

    case 'GET_CACHE_STATUS': {
      try {
        const cache = await caches.open(CACHE_NAME);
        const keys = await cache.keys();
        if (event.ports && event.ports[0]) {
          event.ports[0].postMessage({
            cacheName: CACHE_NAME,
            entryCount: keys.length
          });
        }
      } catch {
        if (event.ports && event.ports[0]) {
          event.ports[0].postMessage({ cacheName: CACHE_NAME, entryCount: 0 });
        }
      }
      break;
    }
  }
});
