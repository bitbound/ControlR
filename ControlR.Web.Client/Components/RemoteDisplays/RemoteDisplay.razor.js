// noinspection JSUnusedGlobalSymbols

/** @typedef {"scale" | "fit" | "stretch" | "unknown"} ViewMode */

class State {
  /** @type {CanvasRenderingContext2D} */
  canvas2dContext;
  /** @type {HTMLCanvasElement} */
  canvasElement;
  /** @type {string} */
  canvasId;
  /** @type {any} */
  componentRef;
  /** @type {"touch" | "mouse"} */
  currentPointerType;
  /** @type {boolean} */
  isDragging;
  /** @type {boolean} */
  isPanning;
  /** @type {number} */
  lastMouseMove;
  /** @type {number} */
  lastPanClientX;
  /** @type {number} */
  lastPanClientY;
  /** @type {boolean} */
  longPressStarted;
  /** @type {number} */
  longPressStartOffsetX;
  /** @type {number} */
  longPressStartOffsetY;
  /** @type {number} */
  mouseMoveTimeout;
  /** @type {PointerEvent} */
  pointerDownEvent;
  /** @type {number} */
  previousPinchDistance;
  /** @type {number} */
  touchClickTimeout;
  /** @type {TouchList} */
  touchList;
  /** @type {WindowEventHandler[]} */
  windowEventHandlers;
  /** @type {MutationObserver|null} */
  mutationObserver;

  constructor() {
    this.windowEventHandlers = [];
    this.touchList = { length: 0 };
    this.previousPinchDistance = -1;
    this.mouseMoveTimeout = -1;
    this.touchClickTimeout = -1;
    this.lastMouseMove = Date.now();
    this.mutationObserver = null;

    // Single-finger panning state
    this.isPanning = false;
    this.lastPanClientX = -1;
    this.lastPanClientY = -1;

    // Panning inertia state (velocity in pixels per ms)
    this.panVelocityX = 0;
    this.panVelocityY = 0;
    this.panAnimationFrameId = -1;
    this.lastPanTime = -1;
  }

  get isScrollModeEnabled() {
    if (!this.canvasElement) {
      return false;
    }

    return this.canvasElement.classList.contains("scroll-mode");
  }

  get isMinimized() {
    if (!this.canvasElement) {
      return false;
    }
    return this.canvasElement.classList.contains("minimized");
  }

  /** @returns {ViewMode} */
  get viewMode() {
    if (!this.canvasElement) {
      return "unknown";
    }
    if (this.canvasElement.classList.contains("scale")) {
      return "scale";
    }
    if (this.canvasElement.classList.contains("fit")) {
      return "fit";
    }
    if (this.canvasElement.classList.contains("stretch")) {
      return "stretch";
    }
    return "unknown";
  }

  /**
   * @param {string} methodName
   * @param {...any} args
   * @returns {Promise<any>}
   */
  invokeDotNet(methodName, ...args) {
    // noinspection JSUnresolvedReference
    return this.componentRef.invokeMethodAsync(methodName, ...args);
  }
}

/**
 * @typedef {Object} MemoryView
 * @property {number} length
 * @property {number} byteLength
 * @property {function(TypedArray, number=): void} set - Copies elements from provided source to the wasm memory
 * @property {function(TypedArray, number=): void} copyTo - Copies elements from wasm memory to provided target
 * @property {function(number=, number=): TypedArray} slice - Same as TypedArray.slice()
 * @property {function(): void} dispose
 */

class WindowEventHandler {
  /** @type {keyof WindowEventMap} */
  type;
  /** @type {EventListener} */
  handler;

  /**
   *
   * @param {keyof WindowEventMap} type
   * @param {EventListener} handler
   */
  constructor(type, handler) {
    this.type = type;
    this.handler = handler;
  }
}

/**
 * Applies mouse wheel zoom with panning towards cursor position
 * @param {HTMLDivElement} contentDiv
 * @param {HTMLCanvasElement} canvasRef
 * @param {number} canvasCssWidth
 * @param {number} canvasCssHeight
 * @param {number} widthChange
 * @param {number} heightChange
 * @param {number} cursorX - Cursor X position relative to canvas
 * @param {number} cursorY - Cursor Y position relative to canvas
 */
export async function applyMouseWheelZoom(contentDiv, canvasRef, canvasCssWidth, canvasCssHeight, widthChange, heightChange, cursorX, cursorY) {
  canvasRef.style.width = `${canvasCssWidth}px`;
  canvasRef.style.height = `${canvasCssHeight}px`;

  // Calculate cursor position as percentage of canvas
  const cursorPercentX = cursorX / canvasRef.clientWidth;
  const cursorPercentY = cursorY / canvasRef.clientHeight;

  // Scroll to maintain cursor position relative to the canvas
  const scrollByX = widthChange * cursorPercentX;
  const scrollByY = heightChange * cursorPercentY;

  contentDiv.scrollBy(scrollByX, scrollByY);
}

/**
 *
 * @param {HTMLDivElement} contentDiv
 * @param {HTMLCanvasElement} canvasRef
 * @param {number} canvasCssWidth
 * @param {number} canvasCssHeight
 * @param {number} widthChange
 * @param {number} heightChange
 * @param {number} scrollDeltaX
 * @param {number} scrollDeltaY
 */
export async function applyPinchZoom(contentDiv, canvasRef, canvasCssWidth, canvasCssHeight, widthChange, heightChange, scrollDeltaX, scrollDeltaY) {
  canvasRef.style.width = `${canvasCssWidth}px`;
  canvasRef.style.height = `${canvasCssHeight}px`;

  // center of the visible client area expressed as a percent of the full scrollable size
  const clientCenterPercentX = (contentDiv.scrollLeft + contentDiv.clientWidth * 0.5) / contentDiv.scrollWidth;
  const clientCenterPercentY = (contentDiv.scrollTop + contentDiv.clientHeight * 0.5) / contentDiv.scrollHeight;

  const scrollByX = widthChange * clientCenterPercentX;
  const scrollByY = heightChange * clientCenterPercentY;

  contentDiv.scrollBy(scrollByX + scrollDeltaX * 2, scrollByY + scrollDeltaY * 2);
}

/**
 * Draws the encoded image onto the canvas at the specified region.
 * @param {string} canvasId
 * @param {Number} x
 * @param {Number} y
 * @param {Number} width
 * @param {Number} height
 * @param {MemoryView} encodedRegion
 */
export async function drawFrame(canvasId, x, y, width, height, encodedRegion) {
  try {
    const state = getState(canvasId);
    const byteArray = new Uint8Array(encodedRegion.slice());
    const imageBlob = new Blob([byteArray], { type: "image/jpeg" });
    const bitmap = await createImageBitmap(imageBlob);
    state.canvas2dContext.drawImage(bitmap, x, y, width, height);
    bitmap.close();
  }
  finally {
    encodedRegion.dispose()
  }
}

/**
 * Retains a reference to the canvas element ID and registers
 * event handlers for the canvas element.
 * @param {any} componentRef
 * @param {string} canvasId
 */
export async function initialize(componentRef, canvasId) {
  const state = getState(canvasId);
  console.log("Initializing with state: ", state);

  /** @type {HTMLCanvasElement} */
  const canvas = document.getElementById(canvasId);

  state.componentRef = componentRef;
  state.canvasId = canvasId;
  state.canvasElement = canvas;
  state.canvas2dContext = canvas.getContext("2d");

  // Create a MutationObserver that watches for the canvas being removed from the DOM.
  // We observe document.body with subtree:true to catch removals anywhere in the tree.
  // When the canvas is no longer contained in the document, we call dispose to clean up.
  try {
    const observer = new MutationObserver(() => {
      try {
        if (!document.body.contains(canvas)) {
          // disconnect first to avoid re-entrancy
          try { observer.disconnect(); } catch (e) { }
          // call dispose to run cleanup logic
          try { dispose(canvasId); } catch (e) { console.error('Error disposing canvas after removal', e); }
        }
      } catch (e) {
        console.error('MutationObserver callback error', e);
      }
    });
    observer.observe(document.body, { childList: true, subtree: true });
    state.mutationObserver = observer;
  } catch (e) {
    console.warn('MutationObserver not available or failed to initialize', e);
    state.mutationObserver = null;
  }

  canvas.addEventListener("pointerup", async ev => {
    if (state.currentPointerType !== "touch") {
      return;
    }

    if (state.isScrollModeEnabled) {
      ev.preventDefault();
      ev.stopPropagation();
      return;
    }

    if (state.longPressStarted && !state.isDragging) {
      await sendMouseButtonEvent(ev.offsetX, ev.offsetY, true, 2, state);
      await sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, 2, state);
    }

    if (state.longPressStarted && state.isDragging) {
      await sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, 0, state);
    }

    // If we were panning, start inertia on lift
    if (state.isPanning) {
      const screenArea = canvas.parentElement;
      if (screenArea) {
        startPanInertia(state, screenArea);
      }
    }

    resetTouchState(state);
  }, { passive: false });

  canvas.addEventListener("pointercancel", () => {
    resetTouchState(state);
  });
  canvas.addEventListener("pointerout", () => {
    resetTouchState(state);
  });
  canvas.addEventListener("pointerleave", () => {
    resetTouchState(state);
  });

  canvas.addEventListener("pointermove", async ev => {
    if (state.isMinimized) {
      return;
    }

    if (state.isScrollModeEnabled) {
      ev.preventDefault();
      ev.stopPropagation();

      await sendPointerMove(state.pointerDownEvent.offsetX, state.pointerDownEvent.offsetY, state);

      const percentX = ev.offsetX / state.canvasElement.clientWidth;
      const percentY = ev.offsetY / state.canvasElement.clientHeight;

      if (Math.abs(ev.movementY) > Math.abs(ev.movementX)) {
        await state.invokeDotNet("SendWheelScroll", percentX, percentY, ev.movementY * 3, 0);
      } else if (Math.abs(ev.movementX) > Math.abs(ev.movementY)) {
        await state.invokeDotNet("SendWheelScroll", percentX, percentY, 0, ev.movementX * -3);
      }
      return;
    }

    if (state.longPressStarted && !state.isDragging) {
      ev.preventDefault();
      ev.stopPropagation();

      const moveDistance = getDistanceBetween(
        state.longPressStartOffsetX,
        state.longPressStartOffsetY,
        ev.offsetX,
        ev.offsetY);

      if (moveDistance > 10) {
        state.isDragging = true;
        await sendPointerMove(state.longPressStartOffsetX, state.longPressStartOffsetY, state);
        await sendMouseButtonEvent(state.longPressStartOffsetX, state.longPressStartOffsetY, true, 0, state);
      }

      return;
    }

    if (state.isDragging) {
      ev.preventDefault();
      ev.stopPropagation();
      await sendPointerMove(ev.offsetX, ev.offsetY, state, true);
      return;
    }

    // Single-finger panning in scaled view: emulate native pan by scrolling the parent container.
    if (state.viewMode === "scale" &&
      state.currentPointerType === "touch" &&
      state.touchList.length === 1 &&
      !state.longPressStarted && !state.isDragging) {

      const screenArea = canvas.parentElement;
      if (screenArea) {
        ev.preventDefault();

        const now = performance.now();
        const dt = state.lastPanTime > 0 ? Math.max(1, now - state.lastPanTime) : 16;

        // Prefer movementX/Y when available (negate to match existing dx convention)
        const deltaX = typeof ev.movementX === 'number' ? -ev.movementX : (state.lastPanClientX - ev.clientX);
        const deltaY = typeof ev.movementY === 'number' ? -ev.movementY : (state.lastPanClientY - ev.clientY);

        if (!state.isPanning) {
          state.isPanning = true;
          state.lastPanClientX = ev.clientX;
          state.lastPanClientY = ev.clientY;
          // Initialize velocity
          state.panVelocityX = deltaX / dt;
          state.panVelocityY = deltaY / dt;
          stopPanInertia(state);
          state.lastPanTime = now;
        } else {
          // Immediate scroll for responsive feel
          screenArea.scrollBy(deltaX, deltaY);

          // Smooth velocity (pixels per ms). alpha closer to 1 preserves previous velocity
          const vx = deltaX / dt;
          const vy = deltaY / dt;
          const alpha = 0.75;
          state.panVelocityX = state.panVelocityX * alpha + vx * (1 - alpha);
          state.panVelocityY = state.panVelocityY * alpha + vy * (1 - alpha);

          state.lastPanClientX = ev.clientX;
          state.lastPanClientY = ev.clientY;
          state.lastPanTime = now;
        }

        return;
      }
    }
  }, { passive: false });

  canvas.addEventListener("pointerdown", ev => {
    state.currentPointerType = ev.pointerType;
    state.pointerDownEvent = ev;
    // Stop any ongoing inertia when user starts a new interaction
    stopPanInertia(state);
    state.lastPanTime = -1;
  }, { passive: false });

  canvas.addEventListener("pointerenter", ev => {
    state.currentPointerType = ev.pointerType;
  }, { passive: false });

  canvas.addEventListener("touchstart", ev => {
    state.touchList = ev.touches;
  });

  canvas.addEventListener("touchend", ev => {
    state.touchList = ev.touches;
  });

  canvas.addEventListener("touchmove", ev => {
    if (state.longPressStarted || state.isDragging || state.isScrollModeEnabled) {
      ev.preventDefault();
    }
  }, { passive: false });

  canvas.addEventListener("mousemove", async ev => {
    if (state.isMinimized) {
      return;
    }

    await sendPointerMove(ev.offsetX, ev.offsetY, state, true);
  }, { passive: false });

  canvas.addEventListener("mousedown", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "touch") {
      return;
    }

    if (ev.button === 3 || ev.button === 4) {
      ev.preventDefault();
    }

    if (state.isMinimized) {
      return;
    }

    await sendMouseButtonEvent(ev.offsetX, ev.offsetY, true, ev.button, state);
  }, { passive: false });

  canvas.addEventListener("mouseup", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "touch") {
      return;
    }

    if (ev.button === 3 || ev.button === 4) {
      ev.preventDefault();
    }

    if (state.isMinimized) {
      return;
    }

    await sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, ev.button, state);
  }, { passive: false });

  canvas.addEventListener("click", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "mouse") {
      return;
    }

    window.clearTimeout(state.touchClickTimeout);
    state.touchClickTimeout = window.setTimeout(
      async () => {
        await sendMouseClick(ev.offsetX, ev.offsetY, ev.button, false, state);
      },
      500);
  }, { passive: false });

  canvas.addEventListener("dblclick", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "mouse") {
      return;
    }

    window.clearTimeout(state.touchClickTimeout);
    await sendMouseClick(ev.offsetX, ev.offsetY, ev.button, true, state);
  }, { passive: false });

  canvas.addEventListener("contextmenu", async ev => {
    ev.preventDefault();
    ev.stopPropagation();

    if (state.isMinimized ||
      state.isScrollModeEnabled) {
      return;
    }

    if (state.currentPointerType === "touch") {
      state.longPressStarted = true;
      state.longPressStartOffsetX = ev.offsetX;
      state.longPressStartOffsetY = ev.offsetY;
      // Cancel any single-finger panning candidate so subsequent moves can start a drag
      state.isPanning = false;
      state.lastPanClientX = -1;
      state.lastPanClientY = -1;
      stopPanInertia(state);
    }
  }, { passive: false });

  /** @param {KeyboardEvent} ev */
  const onKeyDown = async (ev) => {
    if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
      return;
    }

    if (state.isMinimized) {
      return;
    }

    if (!ev.ctrlKey || !ev.shiftKey || ev.key.toLowerCase() !== "i") {
      ev.preventDefault();
    }

    await state.invokeDotNet("SendKeyEvent", ev.key, ev.code, true, ev.ctrlKey, ev.shiftKey, ev.altKey, ev.metaKey);
  };
  window.addEventListener("keydown", onKeyDown, { passive: false });
  state.windowEventHandlers.push(new WindowEventHandler("keydown", onKeyDown));

  /** @param {KeyboardEvent} ev */
  const onKeyUp = (ev) => {
    if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
      return;
    }

    if (state.isMinimized) {
      return;
    }

    ev.preventDefault();

    state.invokeDotNet("SendKeyEvent", ev.key, ev.code, false, ev.ctrlKey, ev.shiftKey, ev.altKey, ev.metaKey);
  }
  window.addEventListener("keyup", onKeyUp, { passive: false });
  state.windowEventHandlers.push(new WindowEventHandler("keyup", onKeyUp));

  const onBlur = async () => {
    await state.invokeDotNet("SendKeyboardStateReset");
  }
  window.addEventListener("blur", onBlur, { passive: false });
  state.windowEventHandlers.push(new WindowEventHandler("blur", onBlur));

  console.log("Initialized with state: ", state);
}

/**
 * Applies auto-pan based on cursor position relative to the screen-area div.
 * @param {string} canvasId
 * @param {HTMLDivElement} screenArea
 * @param {number} pageX - Mouse page X coordinate
 * @param {number} pageY - Mouse page Y coordinate
 */
export async function applyAutoPan(canvasId, screenArea, pageX, pageY) {
  try {
    if (!screenArea) {
      return;
    }

    const rect = screenArea.getBoundingClientRect();

    // Calculate cursor position relative to screen-area div
    const offsetX = pageX - rect.left;
    const offsetY = pageY - rect.top;

    const edgeZone = 0.15;
    const activeStart = edgeZone;
    const activeEnd = 1.0 - edgeZone;

    const percentX = offsetX / rect.width;
    const percentY = offsetY / rect.height;

    let scrollLeft = null;
    let scrollTop = null;

    // X-axis: edges scroll to min/max, middle zone interpolates
    if (percentX < activeStart) {
      scrollLeft = 0.0; // Left edge - scroll all the way left
    } else if (percentX > activeEnd) {
      scrollLeft = 1.0; // Right edge - scroll all the way right
    } else {
      const normalizedX = (percentX - activeStart) / (activeEnd - activeStart);
      scrollLeft = normalizedX;
    }

    // Y-axis: edges scroll to min/max, middle zone interpolates
    if (percentY < activeStart) {
      scrollTop = 0.0; // Top edge - scroll all the way up
    } else if (percentY > activeEnd) {
      scrollTop = 1.0; // Bottom edge - scroll all the way down
    } else {
      const normalizedY = (percentY - activeStart) / (activeEnd - activeStart);
      scrollTop = normalizedY;
    }

    const maxScrollLeft = screenArea.scrollWidth - screenArea.clientWidth;
    const maxScrollTop = screenArea.scrollHeight - screenArea.clientHeight;

    screenArea.scrollLeft = maxScrollLeft * scrollLeft;
    screenArea.scrollTop = maxScrollTop * scrollTop;
  } catch (e) {
    console.error("Error applying auto-pan:", e);
  }
}


/**
 *
 * @param {string} canvasId
 */
async function dispose(canvasId) {
  const state = getState(canvasId);

  // Disconnect mutation observer if present
  try {
    if (state.mutationObserver) {
      try { state.mutationObserver.disconnect(); } catch (e) { }
      state.mutationObserver = null;
    }
  } catch (e) {
    // ignore
  }

  state.windowEventHandlers.forEach(x => {
    console.log("Removing event handler: ", x);
    window.removeEventListener(x.type, x.handler, { passive: false });
  })
  delete window[`controlr-canvas-${canvasId}`];
}

/**
 * @param {number} point1X
 * @param {number} point1Y
 * @param {number} point2X
 * @param {number} point2Y
 */
function getDistanceBetween(point1X, point1Y, point2X, point2Y) {
  return Math.sqrt(Math.pow(point1X - point2X, 2) +
    Math.pow(point1Y - point2Y, 2));
}

/**
 *
 * @param {string} canvasId
 * @returns {State}
 */
function getState(canvasId) {
  if (!window[`controlr-canvas-${canvasId}`]) {
    window[`controlr-canvas-${canvasId}`] = new State();
  }
  return window[`controlr-canvas-${canvasId}`];
}

/**
 *
 * @param {State} state
 */
function resetTouchState(state) {
  state.longPressStarted = false;
  state.isDragging = false;
  state.isPanning = false;
}

/**
 *
 * @param {number} offsetX
 * @param {number} offsetY
 * @param {boolean} isPressed
 * @param {number} button
 * @param {State} state
 */
async function sendMouseButtonEvent(offsetX, offsetY, isPressed, button, state) {
  const percentX = offsetX / state.canvasElement.clientWidth;
  const percentY = offsetY / state.canvasElement.clientHeight;
  await state.invokeDotNet("SendMouseButtonEvent", button, isPressed, percentX, percentY);
}

/**
 *
 * @param {number} offsetX
 * @param {number} offsetY
 * @param {number} button
 * @param {boolean} isDoubleClick
 * @param {State} state
 */
async function sendMouseClick(offsetX, offsetY, button, isDoubleClick, state) {
  const percentX = offsetX / state.canvasElement.clientWidth;
  const percentY = offsetY / state.canvasElement.clientHeight;
  await state.invokeDotNet("SendMouseClick", button, isDoubleClick, percentX, percentY);
}

/**
 *
 * @param {number} offsetX
 * @param {number} offsetY
 * @param {State} state
 * @param {boolean} throttle
 */
async function sendPointerMove(offsetX, offsetY, state, throttle = false) {
  const percentX = offsetX / state.canvasElement.clientWidth;
  const percentY = offsetY / state.canvasElement.clientHeight;
  const throttleTimeout = 25;

  if (!throttle) {
    await state.invokeDotNet("SendPointerMove", percentX, percentY);
    return
  }

  window.clearTimeout(state.mouseMoveTimeout);

  const now = Date.now();
  if (now - state.lastMouseMove > throttleTimeout) {
    await state.invokeDotNet("SendPointerMove", percentX, percentY);
    state.lastMouseMove = now;
    return;
  }

  state.mouseMoveTimeout = window.setTimeout(async () => {
    await state.invokeDotNet("SendPointerMove", percentX, percentY);
  }, throttleTimeout);
}

/**
 * Start inertia animation using pan velocities (pixels per ms).
 * @param {State} state
 * @param {HTMLElement} screenArea
 */
function startPanInertia(state, screenArea) {
  stopPanInertia(state);
  let last = performance.now();

  const step = (timestamp) => {
    const now = performance.now();
    const dt = Math.max(1, now - last);
    last = now;

    const dx = state.panVelocityX * dt;
    const dy = state.panVelocityY * dt;

    screenArea.scrollBy(dx, dy);

    // decay factor per 16ms frame
    const decay = Math.pow(0.95, dt / 16);
    state.panVelocityX *= decay;
    state.panVelocityY *= decay;

    const speed = Math.hypot(state.panVelocityX, state.panVelocityY);
    if (speed < 0.02) {
      state.panVelocityX = 0;
      state.panVelocityY = 0;
      state.panAnimationFrameId = -1;
      return;
    }

    state.panAnimationFrameId = requestAnimationFrame(step);
  };

  state.panAnimationFrameId = requestAnimationFrame(step);
}

/**
 * Stop any ongoing pan inertia.
 * @param {State} state
 */
function stopPanInertia(state) {
  if (state.panAnimationFrameId !== -1) {
    cancelAnimationFrame(state.panAnimationFrameId);
    state.panAnimationFrameId = -1;
  }
}