import { app, ipcMain, screen, Point } from "electron";
import appState from "./appState";
import { getDisplays } from "./mediaHelperMain";
import {
  invokeKeyEvent,
  invokeMouseButtonEvent,
  movePointer,
  resetKeyboardState,
  scrollWheel,
  invokeTypeText,
} from "./inputSimulator";
import { writeLog } from "./logger";
import { verifyDto } from "./dtoVerifier";
import { MainApi } from "../../renderer/mainApi";
import { setClipboardText } from "./clipboardManager";

type MainApiMethod = keyof MainApi;
type MainIpcHandler = (
  event: Electron.IpcMainInvokeEvent,
  ...args: any[]
) => any;

export async function registerIpcHandlers() {
  handleMethod("getServerUri", () => appState.serverUri);
  handleMethod("getSessionId", () => appState.sessionId);
  handleMethod("getViewerConnectionId", () => appState.viewerConnectionId);
  handleMethod("getNotifyUser", () => appState.notifyUser);
  handleMethod("getViewerName", () => appState.viewerName);
  handleMethod("verifyDto", (_, payload, signature, publicKey, publicKeyPem) =>
    verifyDto(payload, signature, publicKey, publicKeyPem),
  );
  handleMethod("getDisplays", () => getDisplays());
  handleMethod("dipToScreenPoint", (_, point: Point) => {
    return screen.dipToScreenPoint(point);
  });
  handleMethod("movePointer", (_, x, y) => movePointer(x, y));
  handleMethod("exit", () => app.exit());
  handleMethod(
    "invokeKeyEvent",
    (_, key, isPressed, shouldRelease) =>
      invokeKeyEvent(key, isPressed, shouldRelease),
  );
  handleMethod("invokeMouseButtonEvent", (_, button, isPressed, x, y) =>
    invokeMouseButtonEvent(button, isPressed, x, y),
  );
  handleMethod("resetKeyboardState", (_) => resetKeyboardState());
  handleMethod("invokeWheelScroll", (_, x, y, scrollY, scrollX) =>
    scrollWheel(x, y, scrollY, scrollX),
  );
  handleMethod("invokeTypeText", (_, text) => invokeTypeText(text));
  handleMethod("setClipboardText", (_, text) => setClipboardText(text));
  handleMethod("writeLog", (_, message, level, args) =>
    writeLog(message, level, args),
  );
}

function handleMethod(methodName: MainApiMethod, handler: MainIpcHandler) {
  ipcMain.handle(methodName, handler);
}
