import { BrowserWindow } from "electron";

export function sendClipboardChanged(currentText: string) {
  const mainWindow = BrowserWindow.getAllWindows()[0];
  if (mainWindow) {
    mainWindow.webContents.send("localClipboardChanged", currentText);
  }
}

export function sendDesktopChanged() {
  const mainWindow = BrowserWindow.getAllWindows()[0];
  if (mainWindow) {
    mainWindow.webContents.send("inputDesktopChanged");
  }
}

export function sendDisplaysChanged() {
  const mainWindow = BrowserWindow.getAllWindows()[0];
  if (mainWindow) {
    mainWindow.webContents.send("displaysChanged");
  }
}
