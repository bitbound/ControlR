/** 
 * @type {WakeLockSentinel}
 */
var _wakeLock = null;

/**
 * @type {boolean}
 */
var _wakeEnabled = false;

window.addClassName = (element, className) => {
  element.classList.add(className);
}
window.getClipboardText = async () => {
  return await navigator.clipboard.readText();
}
window.getSelectionStart = (element) => {
  return element.selectionStart;
}

window.invokeClick = elementId => {
  document.getElementById(elementId).click();
}

window.invokeConfirm = async (message) => {
  return confirm(message);
}

window.invokeAlert = async (message) => {
  alert(message);
}

window.invokePrompt = async (message) => {
  return prompt(message);
}

window.isTouchScreen = async () => {
    return navigator.maxTouchPoints > 0 && navigator.maxTouchPoints !== 256;
}

window.log = async (category, message) => {
  console.log("Got: ", category, message);
}

window.preventTabOut = (element) => {
  element.addEventListener("keydown", ev => {
    if (ev.key.toLowerCase() == "tab") {
      ev.preventDefault();
    }
  })
}
window.reload = () => {
  window.location.reload();
}
window.scrollToEnd = (element) => {
  if (!element) {
    return;
  }

  element.scrollTop = element.scrollHeight;
}
window.scrollToElement = (element) => {
  window.setTimeout(() => {
    window.scrollTo({ top: element.offsetTop, behavior: "smooth" });
  }, 200);
}

window.setClipboardText = async (text) => {
  await navigator.clipboard.writeText(text);
}

/**
 * Try to acquire a wake lock to keep the screen from going to sleep.
 * @param {boolean} isWakeEnabled
 */
window.setScreenWakeLock = async (isWakeEnabled) => {
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

window.setStyleProperty = (element, propertyName, value) => {
  element.style[propertyName] = value;
}

window.startDraggingY = (element, clientY) => {
  if (!element) {
    return;
  }

  var startTop = Number(clientY);

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

window.openWindow = (url, target) => {
  window.open(url, target);
}

document.addEventListener("visibilitychange", async ev => {
  if (_wakeEnabled && document.visibilityState == "visible") {
    await window.setScreenWakeLock(true);
  }
});