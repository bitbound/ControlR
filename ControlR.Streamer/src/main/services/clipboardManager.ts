import { BrowserWindow, clipboard } from "electron";
import { writeLog } from "./logger";

let watcherInterval: NodeJS.Timeout | undefined;
let lastClipboardText: string | undefined | null;

export function setClipboardText(text: string | undefined | null) {
    writeLog("Clipboard updated from remote partner.");
    lastClipboardText = text;
    clipboard.writeText(text);
}

export function watchClipboard() {
    if (watcherInterval) {
        writeLog("Clipboard manager already started.  Aborting.", "Warning");
        return;
    }

    writeLog("Clipboard manager started.");
    try {
        lastClipboardText = clipboard.readText();
    }
    catch (err) {
        writeLog("Error while getting initial clipboard text.", "Error", err);
    }

    watcherInterval = setInterval(
        async () => {
            try {
                const currentText = clipboard.readText();
                if (currentText === lastClipboardText) {
                    return;
                }

                writeLog("Clipboard changed detected in main process.");
                lastClipboardText = currentText;

                const mainWindow = BrowserWindow.getAllWindows()[0];
                if (mainWindow) {
                    mainWindow.webContents.send("localClipboardChanged", currentText);
                }
            }
            catch (err) {
                writeLog("Error while watching clipboard.", "Error", err);
            }
        }, 1000);
}