import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { SignedPayloadDto } from "../../shared/signalrDtos";
import { receiveDto } from "./signalrDtoHandler";
import rtcSession from "./rtcSession";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { waitFor } from "../../shared/helpers";

class StreamerHubConnection {
  connection?: HubConnection;
  serverUri?: string;
  sessionId?: string;

  async connect(): Promise<void> {
    try {
      this.serverUri = await window.mainApi.getServerUri();
      this.sessionId = await window.mainApi.getSessionId();

      window.mainApi.writeLog("Starting SignalR connection.");
      window.mainApi.writeLog("ServerUri: ", "Info", this.serverUri);
      window.mainApi.writeLog("Session ID: ", "Info", this.sessionId);

      if (this.connection) {
        await this.connection.stop();
      }

      this.connection = new HubConnectionBuilder()
        .withUrl(`${this.serverUri}/hubs/streamer`)
        .withHubProtocol(new MessagePackHubProtocol())
        .configureLogging(LogLevel.Information)
        .build();

      this.setHandlers();

      window.mainApi.onInputDesktopChanged(async (ev) => {
        const sessionId = await window.mainApi.getSessionId();
        window.mainApi.writeLog(
          "Notifying viewer of desktop change.  Session ID: ",
          "Info",
          sessionId,
        );
        await streamerHubConnection.notifyViewerDesktopChanged(sessionId);
      });

      await this.connection.start();

      if (this.sessionId) {
        const screens = await window.mainApi.getDisplays();
        await this.connection.invoke(
          "setSessionDetails",
          this.sessionId,
          screens,
        );
        await rtcSession.startRtcSession();
      }
    } catch (ex) {
      window.mainApi.writeLog(
        "Error while connecting to streamer hub.",
        "Error",
        ex,
      );
    }

    // TODO: Broadcast state changes.
  }

  async notifyViewerDesktopChanged(sessionId: string) {
    if (!(await this.waitForConnection())) {
      return;
    }

    try {
      await this.connection.invoke("notifyViewerDesktopChanged", sessionId);
    } catch (err) {
      window.mainApi.writeLog(
        "Error while notifying viewer of desktop change.",
        "Error",
        err,
      );
    }
  }

  async getIceServers(): Promise<RTCIceServer[]> {
    if (!(await this.waitForConnection())) {
      return [];
    }

    try {
      return await this.connection.invoke("getIceServers");
    } catch (err) {
      window.mainApi.writeLog("Error while getting ICE servers.", "Error", err);
    }
    return [];
  }

  async sendIceCandidate(candidateJson: string): Promise<void> {
    if (!(await this.waitForConnection())) {
      return;
    }

    try {
      await this.connection.invoke(
        "sendIceCandidate",
        this.sessionId,
        candidateJson,
      );
    } catch (err) {
      window.mainApi.writeLog(
        "Error while sending ICE candidate.",
        "Error",
        err,
      );
    }
  }

  async sendRtcSessionDescription(sessionDescription: RTCSessionDescription) {
    if (!(await this.waitForConnection())) {
      return;
    }

    try {
      await this.connection.invoke(
        "sendRtcSessionDescription",
        this.sessionId,
        sessionDescription,
      );
    } catch (err) {
      window.mainApi.writeLog(
        "Error while sending RTC session description.",
        "Error",
        err,
      );
    }
  }

  private setHandlers() {
    this.connection.onclose((err) => {
      window.mainApi.writeLog("Connection closed.  Exiting.");
      window.setTimeout(() => {
        window.mainApi.exit();
      }, 1000);
    });

    this.connection.on("receiveDto", async (dto: SignedPayloadDto) => {
      await receiveDto(dto);
    });
  }

  private async waitForConnection() {
    return await waitFor(
      () => this.connection?.state == HubConnectionState.Connected,
      100,
      10000,
    );
  }
}

const streamerHubConnection = new StreamerHubConnection();

export default streamerHubConnection;
