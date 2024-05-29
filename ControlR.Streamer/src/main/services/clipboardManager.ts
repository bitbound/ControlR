import { clipboard } from "electron";
import { writeLog } from "./logger";
import { sendClipboardChanged } from "./rendererApi";

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
    lastClipboardText = clipboard.readText("clipboard");
  } catch (err) {
    writeLog("Error while getting initial clipboard text.", "Error", err);
  }

  watcherInterval = setInterval(async () => {
    try {
      const currentText = clipboard.readText("clipboard");
      if (!currentText || currentText === lastClipboardText) {
        return;
      }

      writeLog("Clipboard change detected in main process.");
      lastClipboardText = currentText;

      sendClipboardChanged(currentText);
    } catch (err) {
      writeLog("Error while watching clipboard.", "Error", err);
    }
  }, 1000);
}
