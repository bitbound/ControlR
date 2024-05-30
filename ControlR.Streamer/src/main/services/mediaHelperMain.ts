import { app, desktopCapturer, DesktopCapturerSource, screen } from "electron";
import { DisplayDto } from "../../shared/sharedDtos";
import { movePointerBy } from "./inputSimulator";
import { writeLog } from "./logger";
import { sendDisplaysChanged } from "./rendererApi";

export async function getDisplays(): Promise<DisplayDto[]> {
  try {
    writeLog("Getting screen sources.");
    const sources = await getCaptureSources();
    writeLog("Found screen sources: ", "Info", sources);

    if (sources.some((x) => !x.display_id)) {
      app.exit(1);
    }

    const primaryDisplay = screen.getPrimaryDisplay();

    writeLog("Found primary display: ", "Info", primaryDisplay);

    const displays = screen.getAllDisplays();

    const screens = sources.map((x) => {
      const display = displays.find((d) => `${d.id}` == x.display_id);
      return {
        displayId: x.display_id,
        id: x.id,
        name: x.name,
        mediaId: x.id,
        isPrimary: display.id == primaryDisplay.id,
        left: display.bounds.x,
        top: display.bounds.y,
        width: display.bounds.width,
        height: display.bounds.height,
        label: display.label,
        scaleFactor: display.scaleFactor,
      } as DisplayDto;
    });

    writeLog(
      "Merging results with Electron displays.  Result: ",
      "Info",
      screens,
    );

    return screens;
  } catch (exception) {
    writeLog(
      "Error while getting screens.",
      "Error",
      JSON.stringify(exception),
    );
  }
}

export function watchForDisplayChanges() {
  screen.on("display-added", () => {
    writeLog("Display added.  Sending change notification.");
    sendDisplaysChanged();
  });
  screen.on("display-metrics-changed", () => {
    writeLog("Display metris changed.  Sending change notification.");
    sendDisplaysChanged();
  });
  screen.on("display-removed", () => {
    writeLog("Display removed.  Sending change notification.");
    sendDisplaysChanged();
  });
}

function getCaptureSources(): Promise<DesktopCapturerSource[]> {
  return new Promise<DesktopCapturerSource[]>(async (resolve, reject) => {
    for (var i = 0; i < 5; i++) {
      const sources = await desktopCapturer.getSources({ types: ["screen"] });

      if (sources.every((x) => !!x.display_id)) {
        resolve(sources);
        return;
      }

      writeLog(
        "Desktop capture sources are missing display IDs.  Attempting to wake up the screen.",
      );
      await movePointerBy(1, 1);
      await movePointerBy(-1, -1);
      await new Promise((resolve) => setTimeout(resolve, 200));
    }
    reject("Failed to find desktop capture sources.");
  });
}
