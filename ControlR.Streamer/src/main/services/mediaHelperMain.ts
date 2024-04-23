import {
    app,
    desktopCapturer,
    DesktopCapturerSource,
    screen,
} from "electron";
import { DisplayDto } from "../../shared/signalrDtos/displayDto";
import { movePointer, movePointerBy } from "./inputSimulator";
import { writeLog } from "./logger";

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

        if (screens.length > 1) {
            let virtualMinX = 0;
            let virtualMaxX = 0;
            let virtualMinY = 0;
            let virtualMaxY = 0;

            screens.forEach(x => {
                virtualMinX = Math.min(x.left, virtualMinX);
                virtualMaxX = Math.max(x.left + x.width, virtualMaxX);
                virtualMinY = Math.min(x.top, virtualMinY);
                virtualMaxY = Math.max(x.top + x.height, virtualMaxY);
            })

            screens.unshift({
                displayId: "all",
                mediaId: "all",
                label: "All",
                name: "All Screens",
                left: virtualMinX,
                top: virtualMinY,
                height: virtualMaxY - virtualMinY,
                width: virtualMaxX - virtualMinX,
                isPrimary: false,
                scaleFactor: primaryDisplay.scaleFactor
            })
        }

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
