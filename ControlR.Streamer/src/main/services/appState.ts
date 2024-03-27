import { app } from "electron";

const sessionId = app.commandLine.getSwitchValue("session-id");
const authorizedKey = app.commandLine.getSwitchValue("authorized-key");
const isDev = app.commandLine.hasSwitch("dev");

const serverUri = app.commandLine.getSwitchValue("server-uri").replace(/\/$/, "") ?? "https://app.controlr.app";


const websocketUri = serverUri.replace("http", "ws");

interface AppState {
    authorizedKey: string;
    isUnattended: boolean;
    isDev: boolean;
    sessionId: string;
    serverUri: string;
    websocketUri: string;
}

export default {
    authorizedKey: authorizedKey,
    isDev: isDev,
    isUnattended: !!sessionId,
    sessionId: sessionId,
    serverUri: serverUri,
    websocketUri: websocketUri
} as AppState;