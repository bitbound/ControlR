// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from "electron";
import { MainApi } from "./mainApi";

type MainApiMethod = keyof MainApi;

const mainApiIpc: MainApi = {
  getServerUri: () => invokeInMain("getServerUri"),
  getViewerConnectionId: () => invokeInMain("getViewerConnectionId"),
  getSessionId: () => invokeInMain("getSessionId"),
  getNotifyUser: () => invokeInMain("getNotifyUser"),
  getViewerName: () => invokeInMain("getViewerName"),

  verifyDto: (payload, signature, publicKey, publicKeyPem) =>
    invokeInMain("verifyDto", payload, signature, publicKey, publicKeyPem),

  getDisplays: () => invokeInMain("getDisplays"),
  movePointer: (x, y) => invokeInMain("movePointer", x, y),
  dipToScreenPoint: (point) => invokeInMain("dipToScreenPoint", point),
  exit: () => invokeInMain("exit"),

  invokeKeyEvent: (key, isPressed, shouldRelease) =>
    invokeInMain("invokeKeyEvent", key, isPressed, shouldRelease),

  invokeMouseButtonEvent: (button, isPressed, x, y) =>
    invokeInMain("invokeMouseButtonEvent", button, isPressed, x, y),

  resetKeyboardState: () => invokeInMain("resetKeyboardState"),

  invokeWheelScroll: (x, y, scrollY, scrollX) =>
    invokeInMain("invokeWheelScroll", x, y, scrollY, scrollX),

  invokeTypeText: (text: string) => invokeInMain("invokeTypeText", text),
  setClipboardText: (text: string | undefined | null) =>
    invokeInMain("setClipboardText", text),

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
    return invokeInMain("writeLog", message, level, args);
  },

  onLocalClipboardChanged(callback: (text: string | undefined | null) => void) {
    ipcRenderer.on("localClipboardChanged", (ev, text) => callback(text));
  },

  onInputDesktopChanged(callback: (desktopName: string) => void) {
    ipcRenderer.on("inputDesktopChanged", (ev, desktopName) =>
      callback(desktopName),
    );
  },
  onDisplaysChanged(callback: () => void) {
    ipcRenderer.on("displaysChanged", (ev) => {
      callback();
    });
  },
};

contextBridge.exposeInMainWorld("mainApi", mainApiIpc);

function invokeInMain(methodName: MainApiMethod, ...args: any[]) {
  return ipcRenderer.invoke(methodName, ...args);
}
