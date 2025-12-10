(function () {
  const progress = {
    files: new Map(),
    updateDisplay() {
      let totalBytes = 0;
      let loadedBytes = 0;

      this.files.forEach(file => {
        totalBytes += file.total;
        loadedBytes += file.loaded;
      });

      const percent = totalBytes > 0
        ? Math.min(100, Math.round((loadedBytes / totalBytes) * 100))
        : 0;
      const el = document.getElementById('blazor-load-progress');
      if (el) el.textContent = `${percent}%`;
    }
  };

  const originalFetch = window.fetch;
  window.fetch = async function (...args) {
    const url = args[0];

    if (!url.includes('_framework/') || !url.endsWith('.wasm')) {
      return originalFetch.apply(this, args);
    }

    const response = await originalFetch.apply(this, args);
    const contentLength = response.headers.get('content-length');
    const fileId = url;
    if (contentLength) {
      progress.files.set(fileId, { total: parseInt(contentLength), loaded: 0 });
    }
    const reader = response.body.getReader();
    return new Response(new ReadableStream({
      start(controller) {
        function push() {
          reader.read().then(({ done, value }) => {
            if (done) {
              controller.close();
              return;
            }

            const file = progress.files.get(fileId);
            if (file) {
              file.loaded += value.byteLength;
              progress.updateDisplay();
            }

            controller.enqueue(value);
            push();
          });
        }
        push();
      }
    }), { headers: response.headers });
  };
})();
