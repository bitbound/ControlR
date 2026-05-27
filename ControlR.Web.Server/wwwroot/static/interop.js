// noinspection JSUnusedGlobalSymbols

/** 
 * @type {WakeLockSentinel}
 */
let _wakeLock = null;

/**
 * @type {boolean}
 */
let _wakeEnabled = false;

function addClassName(element, className) {
  element.classList.add(className);
}

/**
 * Create a blob URL from image data
 * @param {Uint8Array} imageData - The image data
 * @param {string} mimeType - The MIME type (e.g., 'image/jpeg')
 * @returns {string | null} The blob URL
 */
function createBlobUrl(imageData, mimeType) {
  try {
    // Create a blob and return URL
    const blob = new Blob([imageData], { type: mimeType });
    return URL.createObjectURL(blob);
  } catch (error) {
    console.error('Error creating blob URL:', error);
    return null;
  }
}

/**
 * Revoke a blob URL to free memory
 * @param {string} blobUrl - The blob URL to revoke
 */
function revokeBlobUrl(blobUrl) {
  if (blobUrl && blobUrl.startsWith('blob:')) {
    URL.revokeObjectURL(blobUrl);
  }
}

function getClipboardText() {
  return navigator.clipboard.readText();
}
function getSelectionStart(element) {
  return element.selectionStart;
}
function getSelectionStartById(elementId) {
  const element = document.getElementById(elementId);
  if (!element) {
    console.warn(`Element with ID ${elementId} not found.`);
    return -1;
  }
  return element.selectionStart;
}

/**
 * Check if the system/browser prefers dark mode
 * @returns {boolean} True if dark mode is preferred
 */
function getSystemDarkMode() {
  if (window.matchMedia) {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }
  // Default to dark if media queries not supported
  return true;
}

function invokeClick(elementId) {
  document.getElementById(elementId).click();
}
function invokeConfirm(message) {
  return confirm(message);
}
function invokeAlert(message) {
  alert(message);
}
function invokePrompt(message) {
  return prompt(message);
}
function isTouchScreen() {
  // Check for touchpoint support
  if (navigator.maxTouchPoints > 0) {
    return true;
  }
  
  // Fallback for older browsers
  return !!('ontouchstart' in window || window.TouchEvent);
  
  
}
function log(category, message) {
  console.log("Got: ", category, message);
}
function preventTabOut(element) {
  element.addEventListener("keydown", ev => {
    if (!ev.key) {
      return;
    }
    if (ev.key.toLowerCase() === "tab") {
      ev.preventDefault();
    }
  })
}
function preventTabOutById(elementId) {
  preventTabOut(document.getElementById(elementId));
}
function reload() {
  window.location.reload();
}
function scrollToEnd(element) {
  if (!element) {
    return;
  }
  element.scrollTop = element.scrollHeight;
}
function scrollToElement(element) {
  window.setTimeout(() => {
    window.scrollTo({ top: element.offsetTop, behavior: "smooth" });
  }, 200);
}
function setClipboardText(text) {
  return navigator.clipboard.writeText(text);
}

/**
 * Try to acquire a wake lock to keep the screen from going to sleep.
 * @param {boolean} isWakeEnabled
 */
async function setScreenWakeLock(isWakeEnabled) {
  _wakeEnabled = isWakeEnabled;

  if (!navigator.wakeLock) {
    console.warn("Wake Lock API is not supported on this browser.");
    return;
  }

  if (isWakeEnabled) {
    console.log("Requesting screen wake lock.");
    await _wakeLock?.release();
    _wakeLock ??= await navigator.wakeLock.request("screen");
    console.log("Wake lock acquired.");
    return;
  }

  console.log("Releasing screen wake lock.");
  await _wakeLock?.release();
  _wakeLock = null;
}

function setSelectionStartById(elementId, cursorPosition) {
  /** @type {HTMLInputElement} */
  const element = document.getElementById(elementId);
  if (!element) {
    console.warn(`Element with ID ${elementId} not found.`);
    return -1;
  }
  element.setSelectionRange(cursorPosition, cursorPosition, 'none');
}

function setStyleProperty(element, propertyName, value) {
  element.style[propertyName] = value;
}
function startDraggingY(element, clientY) {
  if (!element) {
    return;
  }
  let startTop = Number(clientY);
  function pointerMove(ev) {
    if (Math.abs(ev.clientY - startTop) > 10) {
      if (ev.clientY < 0 || ev.clientY > window.innerHeight - element.clientHeight) {
        return;
      }
      element.style.top = `${ev.clientY}px`;
    }
  }
  function pointerUpOrLeave() {
    window.removeEventListener("pointermove", pointerMove);
    window.removeEventListener("pointerup", pointerUpOrLeave);
    window.removeEventListener("pointerleave", pointerUpOrLeave);
  }
  pointerUpOrLeave();
  window.addEventListener("pointermove", pointerMove);
  window.addEventListener("pointerup", pointerUpOrLeave);
  window.addEventListener("pointerleave", pointerUpOrLeave);
}
function openWindow(url, target) {
  window.open(url, target);
}

/**
 * @param {HTMLElement | undefined} element
 */
async function toggleFullscreen(element) {
  if (document.fullscreenElement) {
    await document.exitFullscreen();
  } else {
    try {
      const targetElement = element ?? document.documentElement;
      await targetElement.requestFullscreen();
    }
    catch (err) {
      console.error("Error attempting to enable full-screen mode.");
      console.error(err);
    }
  }
 }

document.addEventListener("visibilitychange", async () => {
  if (_wakeEnabled && document.visibilityState === "visible") {
    await setScreenWakeLock(true);
  }
});

/** Session Storage */

/**
* 
* @param {string} key
*/
function getFromSessionStorage(key) {
  return sessionStorage.getItem(key);
}

/**
 * 
 * @param {string} key
 * @param {string} value
 */
function setToSessionStorage(key, value) {
  sessionStorage.setItem(key, value);
}

/**
 * 
 * @param {string} key
 */
function removeFromSessionStorage(key) {
  return sessionStorage.removeItem(key);
}

function clearSessionStorage() {
  return sessionStorage.clear();
}

/** Local Storage */

/**
*
* @param {string} key
*/
function getFromLocalStorage(key) {
  return localStorage.getItem(key);
}

/**
 *
 * @param {string} key
 * @param {string} value
 */
function setToLocalStorage(key, value) {
  localStorage.setItem(key, value);
}

/**
 *
 * @param {string} key
 */
function removeFromLocalStorage(key) {
  return localStorage.removeItem(key);
}

function clearLocalStorage() {
  return localStorage.clear();
}