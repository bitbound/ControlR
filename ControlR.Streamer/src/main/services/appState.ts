import { app } from "electron";
import { parseBoolean } from "../../shared/helpers";

const sessionId = app.commandLine.getSwitchValue("session-id");
const authorizedKey = app.commandLine.getSwitchValue("authorized-key");
const viewerName = app.commandLine.getSwitchValue("viewer-name") ?? "";
const notifyUser =
  parseBoolean(app.commandLine.getSwitchValue("notify-user")) || false;
const isDev = app.commandLine.hasSwitch("dev");

const serverUri =
  app.commandLine.getSwitchValue("server-uri").replace(/\/$/, "") ||
  "https://app.controlr.app";

const websocketUri = serverUri.replace("http", "ws");

interface AppState {
  authorizedKey: string;
  isUnattended: boolean;
  isDev: boolean;
  sessionId: string;
  serverUri: string;
  websocketUri: string;
  viewerName: string;
  notifyUser: boolean;
}

export default {
  authorizedKey: authorizedKey,
  isDev: isDev,
  isUnattended: !!sessionId,
  sessionId: sessionId,
  serverUri: serverUri,
  websocketUri: websocketUri,
  notifyUser: notifyUser,
  viewerName: viewerName,
} as AppState;
