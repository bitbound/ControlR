import { app, ipcMain, IpcMainInvokeEvent } from "electron";
import { ipcRtmChannels } from "../../shared/ipcChannels";
import appState from "./appState";
import { verify, createPublicKey } from "crypto";
import { getDisplays } from "./mediaHelperMain";
import { invokeKeyEvent, invokeMouseButtonEvent, movePointer, resetKeyboardState, scrollWheel, invokeTypeText } from "./inputSimulator";
import { writeLog } from "./logger";

export async function registerIpcHandlers() {
  ipcMain.handle(ipcRtmChannels.getServerUri, () => appState.serverUri);
  ipcMain.handle(ipcRtmChannels.getSessionId, () => appState.sessionId);
  ipcMain.handle(ipcRtmChannels.verifyDto, verifyDto);
  ipcMain.handle(ipcRtmChannels.getDisplays, () => getDisplays());
  ipcMain.handle(ipcRtmChannels.movePointer, (_, x, y) => movePointer(x, y));
  ipcMain.handle(ipcRtmChannels.exit, () => app.exit());
  ipcMain.handle(ipcRtmChannels.invokeKeyEvent, (_, key, isPressed, shouldRelease) => invokeKeyEvent(key, isPressed, shouldRelease));
  ipcMain.handle(ipcRtmChannels.invokeMouseButtonEvent, (_, button, isPressed, x, y) => invokeMouseButtonEvent(button, isPressed, x, y));
  ipcMain.handle(ipcRtmChannels.resetKeyboardState, (_) => resetKeyboardState());
  ipcMain.handle(ipcRtmChannels.invokeWheelScroll, (_, deltaX, deltaY, deltaZ) => scrollWheel(deltaX, deltaY, deltaZ));
  ipcMain.handle(ipcRtmChannels.invokeTypeText, (_, text) => invokeTypeText(text));
  ipcMain.handle(ipcRtmChannels.writeLog, (_, message, level, args) => writeLog(message, level, args));
}

const verifyDto = (
  event: IpcMainInvokeEvent,
  payload: Uint8Array,
  signature: Uint8Array,
  publicKey: Uint8Array,
  publicKeyPem: string
): boolean => {

  console.log("Verifying DTO signature.");

  const publicKeyBase64 = Buffer.from(publicKey).toString('base64');

  writeLog(`Comparing public key ${publicKeyBase64}`);

  if (publicKeyBase64 != appState.authorizedKey) {
    writeLog("Public key from DTO does not match the authorized key.", 'Error');
    return false;
  }

  const publicKeyObject = createPublicKey({
    key: publicKeyPem,
    type: "pkcs1",
    format: "pem"
  });
  
  const result = verify(
    "RSA-SHA512", 
    payload, 
    publicKeyObject, 
    signature);

  if (!result) {
    writeLog("Public key from DTO does not pass verification!", 'Error');
    return false;
  }

  console.info("DTO passed signature verification.");

  return true;
};
