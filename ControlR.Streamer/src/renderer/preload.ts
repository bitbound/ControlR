// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from "electron";
import { MainApi } from ".";

import { ipcRtmChannels } from "../shared/ipcChannels";

contextBridge.exposeInMainWorld("mainApi", {
    "getServerUri": () => ipcRenderer.invoke(ipcRtmChannels.getServerUri),
    "getSessionId": () => ipcRenderer.invoke(ipcRtmChannels.getSessionId),

    "verifyDto": (payload, signature, publicKey, publicKeyPem) =>
        ipcRenderer.invoke(ipcRtmChannels.verifyDto, payload, signature, publicKey, publicKeyPem),

    "getDisplays": () => ipcRenderer.invoke(ipcRtmChannels.getDisplays),
    "movePointer": (x, y) => ipcRenderer.invoke(ipcRtmChannels.movePointer, x, y),
    "exit": () => ipcRenderer.invoke(ipcRtmChannels.exit),

    "invokeKeyEvent": (key, isPressed, shouldRelease) =>
        ipcRenderer.invoke(ipcRtmChannels.invokeKeyEvent, key, isPressed, shouldRelease),

    "invokeMouseButton": (button, isPressed, x, y) =>
        ipcRenderer.invoke(ipcRtmChannels.invokeMouseButtonEvent, button, isPressed, x, y),

    "resetKeyboardState": () => ipcRenderer.invoke(ipcRtmChannels.resetKeyboardState),

    "invokeWheelScroll": (deltaX, deltaY, deltaZ) =>
        ipcRenderer.invoke(ipcRtmChannels.invokeWheelScroll, deltaX, deltaY, deltaZ),

    "invokeTypeText": (text: string) => ipcRenderer.invoke(ipcRtmChannels.invokeTypeText, text),

    "writeLog": (message, level, args) => {
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
        ipcRenderer.invoke(ipcRtmChannels.writeLog, message, level, args);
    }

} as MainApi)