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
  return navigator.maxTouchPoints > 0 && navigator.maxTouchPoints !== 256;
}
function log(category, message) {
  console.log("Got: ", category, message);
}
function preventTabOut(element) {
  element.addEventListener("keydown", ev => {
    if (!ev.key) {
      return;
    }
    if (ev.key.toLowerCase() == "tab") {
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
    _wakeLock?.release();
    _wakeLock ??= await navigator.wakeLock.request("screen");
    console.log("Wake lock acquired.");
    return;
  }

  console.log("Releasing screen wake lock.");
  _wakeLock?.release();
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
  function pointerUpOrLeave(ev) {
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

document.addEventListener("visibilitychange", async () => {
  if (_wakeEnabled && document.visibilityState == "visible") {
    setScreenWakeLock(true);
  }
});