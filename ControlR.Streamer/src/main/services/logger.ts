import { platform, tmpdir, EOL } from "os";
import { appendFileSync, statSync, readFileSync, writeFileSync, existsSync, mkdirSync, readdirSync, rmSync } from "fs";
import path from "path";
import appState from "./appState"

const maxLogAge = 1000 * 60 * 60 * 24 * 7; // 7 days

export function cleanupLogs() {
    try {
        const logDir = getLogDir();
        readdirSync(logDir).forEach(x => {
            const xStat = statSync(path.join(logDir, x));
            const now = Date.now()

            if (xStat.isFile() && now - xStat.mtime.getTime() > maxLogAge) {
                try {
                    writeLog("Removing expired log file: ", "Info", x);
                    rmSync(path.join(logDir, x));
                }
                catch (ex) {
                    writeLog("Error while removing log file.", "Error", ex);
                }
            }
        });
    }
    catch (ex) {
        writeLog("Error while cleaning logs directory.", "Error", ex);
    }
}

export function writeLog(message: string, level: LogLevel = "Info", ...args: unknown[]) {
    try {
        if (level == "Info") {
            console.log(message, args);
        }
        else if (level == "Warning") {
            console.warn(message, args);
        }
        else if (level == "Error") {
            console.error(message, args);
        }

        const logDir = getLogDir();

        const date = new Date();
        const year = date.getFullYear();
        const month = (date.getMonth() + 1).toString().padStart(2, "0");
        const day = date.getDate().toString().padStart(2, "0");

        const logPath = path.join(logDir, `Streamer-${year}-${month}-${day}.log`);

        if (existsSync(logPath)) {
            while (statSync(logPath).size > 1000000) {
                const content = readFileSync(logPath, { encoding: "utf8" });
                writeFileSync(logPath, content.substring(content.length / 2));
            }
        }

        let entry = `[${level}]\t[${(new Date()).toLocaleString()}]\t${message}`;

        if (args && args.length > 0) {
            args = args.filter(x => !!x);
            entry += ` ${JSON.stringify(args)}`
        }

        entry += EOL;

        appendFileSync(logPath, entry);
    }
    catch (ex) {
        console.error("Failed to write to log file.", ex);
    }

}

function getLogDir() {
    const logEnvDir = appState.isDev ? "Logs_Debug" : "Logs";

    let logDir = path.join(tmpdir(), "ControlR", logEnvDir);
    const rootDir = path.parse(logDir).root;

    if (platform() == "win32") {
        logDir = path.join(rootDir, "ProgramData", "ControlR", logEnvDir, "ControlR.Streamer");
    }

    if (!existsSync(logDir)) {
        mkdirSync(logDir, { recursive: true });
    }
    return logDir;
}