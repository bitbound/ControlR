import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { SignedPayloadDto, StreamerInitData } from "../../shared/signalrDtos";
import { receiveDto } from "./signalrDtoHandler";
import rtcSession from "./rtcSession";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";
import { waitFor } from "../../shared/helpers";

class StreamerHubConnection {
  connection?: HubConnection;
  serverUri?: string;
  viewerConnectionId?: string;
  sessionId?: string;

  async connect(): Promise<void> {
    try {
      this.serverUri = await window.mainApi.getServerUri();
      this.sessionId = await window.mainApi.getSessionId();
      this.viewerConnectionId = await window.mainApi.getViewerConnectionId();

      window.mainApi.writeLog("Starting SignalR connection.");
      window.mainApi.writeLog("ServerUri: ", "Info", this.serverUri);
      window.mainApi.writeLog("Session ID: ", "Info", this.sessionId);
      window.mainApi.writeLog("Viewer Connection ID: ", "Info", this.viewerConnectionId);

      if (this.connection) {
        await this.connection.stop();
      }

      this.connection = new HubConnectionBuilder()
        .withUrl(`${this.serverUri}/hubs/streamer`)
        .configureLogging(LogLevel.Information)
        .withHubProtocol(new MessagePackHubProtocol())
        .build();

      this.setHandlers();

      window.mainApi.onInputDesktopChanged(async (ev) => {
        const viewerConnectionId = await window.mainApi.getViewerConnectionId();
        const sessionId = await window.mainApi.getSessionId();

        window.mainApi.writeLog(
          "Notifying viewer of desktop change.  Viewer Connection ID: ",
          "Info",
          viewerConnectionId,
        );
        await streamerHubConnection.notifyViewerDesktopChanged(viewerConnectionId, sessionId);
      });

      await this.connection.start();

      if (this.viewerConnectionId) {
        const displays = await window.mainApi.getDisplays();
        await this.connection.invoke(
          "sendStreamerInitDataToViewer",
          this.viewerConnectionId,
          {
            displays: displays,
            streamerConnectionId: this.connection.connectionId,
            sessionId: this.sessionId,
          } as StreamerInitData,
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

  async notifyViewerDesktopChanged(viewerConnectionId: string, sessionId: string) {
    if (!(await this.waitForConnection())) {
      return;
    }

    try {
      await this.connection.invoke("notifyViewerDesktopChanged", viewerConnectionId, sessionId);
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
        this.viewerConnectionId,
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
        this.viewerConnectionId,
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
