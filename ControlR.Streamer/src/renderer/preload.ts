// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from "electron";
import { MainApi } from "./mainApi";

contextBridge.exposeInMainWorld("mainApi", {
  getServerUri: () => ipcRenderer.invoke("getServerUri"),
  getSessionId: () => ipcRenderer.invoke("getSessionId"),
  getNotifyUser: () => ipcRenderer.invoke("getNotifyUser"),
  getViewerName: () => ipcRenderer.invoke("getViewerName"),

  verifyDto: (payload, signature, publicKey, publicKeyPem) =>
    ipcRenderer.invoke(
      "verifyDto",
      payload,
      signature,
      publicKey,
      publicKeyPem,
    ),

  getDisplays: () => ipcRenderer.invoke("getDisplays"),
  movePointer: (x, y) => ipcRenderer.invoke("movePointer", x, y),
  exit: () => ipcRenderer.invoke("exit"),

  invokeKeyEvent: (key, isPressed, shouldRelease) =>
    ipcRenderer.invoke("invokeKeyEvent", key, isPressed, shouldRelease),

  invokeMouseButtonEvent: (button, isPressed, x, y) =>
    ipcRenderer.invoke("invokeMouseButtonEvent", button, isPressed, x, y),

  resetKeyboardState: () => ipcRenderer.invoke("resetKeyboardState"),

  invokeWheelScroll: (deltaX, deltaY, deltaZ) =>
    ipcRenderer.invoke("invokeWheelScroll", deltaX, deltaY, deltaZ),

  invokeTypeText: (text: string) => ipcRenderer.invoke("invokeTypeText", text),
  setClipboardText: (text: string | undefined | null) => ipcRenderer.invoke("setClipboardText", text),

  onLocalClipboardChanged: (callback: (text: string | undefined | null) => void) =>
    ipcRenderer.on("localClipboardChanged", (ev, text) => callback(text)),

  writeLog: (message, level, args) => {
    switch (level) {
      case "Info":
        console.log(message, args);
        break;
      case "Error":
        console.error(message, args);
        break;
      case "Warning":
        console.warn(message, args);
        break;
      default:
        break;
    }
    ipcRenderer.invoke("writeLog", message, level, args);
  },
} as MainApi);
