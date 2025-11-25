// noinspection JSUnusedGlobalSymbols

const MAX_KEYPRESS_AGE_MS = 5000;

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
  /** @type {number} */
  lastMouseMove;
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
  /** @type {Map<string, {code: string|null, timestamp: number}>} */
  pressedKeysWithCode;
  /** @type {number} */
  cleanupIntervalId;

  constructor() {
    this.windowEventHandlers = [];
    this.touchList = {length: 0};
    this.previousPinchDistance = -1;
    this.mouseMoveTimeout = -1;
    this.touchClickTimeout = -1;
    this.lastMouseMove = Date.now();
    this.mutationObserver = null;
    this.pressedKeysWithCode = new Map();
    this.cleanupIntervalId = -1;
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
 * @param {Uint8Array} encodedRegion
 */
export async function drawFrame(canvasId, x, y, width, height, encodedRegion) {
  const state = getState(canvasId);
  const imageBlob = new Blob([encodedRegion]);
  const bitmap = await createImageBitmap(imageBlob);
  state.canvas2dContext.drawImage(bitmap, x, y, width, height);
  bitmap.close();
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
  // When the canvas is no longer contained in the document we call dispose to clean up.
  try {
    const observer = new MutationObserver(() => {
      try {
        if (!document.body.contains(canvas)) {
          // disconnect first to avoid re-entrancy
          try { observer.disconnect(); } catch (e) {}
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
    // In very constrained environments MutationObserver may not be available; ignore safely.
    console.warn('MutationObserver not available or failed to initialize', e);
    state.mutationObserver = null;
  }

  canvas.addEventListener("pointerup", async ev => {
    if (state.currentPointerType !== "touch") {
      return;
    }

    if (canvas.classList.contains("scroll-mode")) {
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

    resetTouchState(state);
  });

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
    if (canvas.classList.contains("minimized")) {
      return;
    }

    if (canvas.classList.contains("scroll-mode")) {
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
    }
  })

  canvas.addEventListener("pointerdown", ev => {
    state.currentPointerType = ev.pointerType;
    state.pointerDownEvent = ev;
  });

  canvas.addEventListener("pointerenter", ev => {
    state.currentPointerType = ev.pointerType;
  });

  canvas.addEventListener("touchmove", ev => {
    if (state.longPressStarted || state.isDragging || canvas.classList.contains("scroll-mode")) {
      ev.preventDefault();
    }
  });

  canvas.addEventListener("mousemove", async ev => {
    if (canvas.classList.contains("minimized")) {
      return;
    }

    await sendPointerMove(ev.offsetX, ev.offsetY, state, true);
  });

  canvas.addEventListener("mousedown", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "touch") {
      return;
    }

    if (ev.button === 3 || ev.button === 4) {
      ev.preventDefault();
    }

    if (canvas.classList.contains("minimized")) {
      return;
    }

    await sendMouseButtonEvent(ev.offsetX, ev.offsetY, true, ev.button, state);
  });

  canvas.addEventListener("mouseup", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "touch") {
      return;
    }

    if (ev.button === 3 || ev.button === 4) {
      ev.preventDefault();
    }

    if (canvas.classList.contains("minimized")) {
      return;
    }

    await sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, ev.button, state);
  });

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
  });

  canvas.addEventListener("dblclick", async ev => {
    ev.stopPropagation();

    if (state.currentPointerType === "mouse") {
      return;
    }

    window.clearTimeout(state.touchClickTimeout);
    await sendMouseClick(ev.offsetX, ev.offsetY, ev.button, true, state);
  });

  canvas.addEventListener("contextmenu", async ev => {
    ev.preventDefault();
    ev.stopPropagation();

    if (canvas.classList.contains("minimized") ||
      canvas.classList.contains("scroll-mode")) {
      return;
    }

    if (state.currentPointerType === "touch") {
      state.longPressStarted = true;
      state.longPressStartOffsetX = ev.offsetX;
      state.longPressStartOffsetY = ev.offsetY;
    }
  });

  /** @param {KeyboardEvent} ev */
  const onKeyDown = async (ev) => {
    if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
      return;
    }

    if (canvas.classList.contains("minimized")) {
      return;
    }

    if (!ev.ctrlKey || !ev.shiftKey || ev.key.toLowerCase() !== "i") {
      ev.preventDefault();
    }

    // Hybrid approach: intelligently choose between physical key mode and character mode
    // - For shortcuts (modifier keys held) or non-printable keys -> send code for physical key simulation
    // - For normal typing (printable characters) -> send null to use Unicode injection
    const hasModifiers = ev.ctrlKey || ev.altKey || ev.metaKey;
    const isNonPrintable = ev.key.length > 1;
    const codeToSend = (hasModifiers || isNonPrintable) ? ev.code : null;

    // Track which code was sent for this key with timestamp, so keyup can match
    state.pressedKeysWithCode.set(ev.code, {
      code: codeToSend,
      timestamp: Date.now()
    });

    await state.invokeDotNet("SendKeyEvent", ev.key, codeToSend, true);
  };
  window.addEventListener("keydown", onKeyDown);
  state.windowEventHandlers.push(new WindowEventHandler("keydown", onKeyDown));

  /** @param {KeyboardEvent} ev */
  const onKeyUp = (ev) => {
    if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
      return;
    }

    if (canvas.classList.contains("minimized")) {
      return;
    }

    ev.preventDefault();

    // Use the same code that was sent during keydown for this key
    // This ensures keydown/keyup are paired correctly even if modifiers change between events
    const trackedKey = state.pressedKeysWithCode.get(ev.code);
    const codeToSend = trackedKey ? trackedKey.code : null;

    // Remove from tracking map since the key is now released
    state.pressedKeysWithCode.delete(ev.code);

    state.invokeDotNet("SendKeyEvent", ev.key, codeToSend, false);
  }
  window.addEventListener("keyup", onKeyUp);
  state.windowEventHandlers.push(new WindowEventHandler("keyup", onKeyUp));

  const onBlur = async () => {
    // Clear tracking map since we can't track keys released outside the window
    state.pressedKeysWithCode.clear();
    await state.invokeDotNet("SendKeyboardStateReset");
  }
  window.addEventListener("blur", onBlur);
  state.windowEventHandlers.push(new WindowEventHandler("blur", onBlur));

  // Start periodic cleanup of orphaned tracking entries
  // Keys held longer than 5 seconds are considered stuck-orphaned
  state.cleanupIntervalId = window.setInterval(() => {
    const now = Date.now();
    let cleanedCount = 0;

    for (const [code, data] of state.pressedKeysWithCode.entries()) {
      if (now - data.timestamp > MAX_KEYPRESS_AGE_MS) {
        state.pressedKeysWithCode.delete(code);
        cleanedCount++;
      }
    }

    if (cleanedCount > 0) {
      console.log(`Cleaned up ${cleanedCount} orphaned key tracking entries`);
    }
  }, 1000);

  console.log("Initialized with state: ", state);
}

/**
 *
 * @param {string} canvasId
 */
export async function dispose(canvasId) {
  const state = getState(canvasId);

  // Disconnect mutation observer if present
  try {
    if (state.mutationObserver) {
      try { state.mutationObserver.disconnect(); } catch (e) {}
      state.mutationObserver = null;
    }
  } catch (e) {
    // ignore
  }

  // Stop cleanup interval
  try {
    if (state.cleanupIntervalId !== -1) {
      window.clearInterval(state.cleanupIntervalId);
      state.cleanupIntervalId = -1;
    }
  } catch (e) {
    // ignore
  }

  state.windowEventHandlers.forEach(x => {
    console.log("Removing event handler: ", x);
    window.removeEventListener(x.type, x.handler);
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
  state.canvasElement.parentElement.style.touchAction = "";
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