import { HubConnection, HubConnectionBuilder, JsonHubProtocol, LogLevel } from "@microsoft/signalr";
import { SignedPayloadDto } from "../../shared/signalrDtos/signedPayloadDto";
import { receiveDto } from "./signalrDtoHandler";
import rtcSession from "./rtcSession";
import { MessagePackHubProtocol } from "@microsoft/signalr-protocol-msgpack";

class StreamerHubConnection {
    connection?: HubConnection;
    serverUri?: string;
    sessionId?: string;

    async connect(): Promise<void> {
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

        await this.connection.start();

        if (this.sessionId) {
            const screens = await window.mainApi.getDisplays();
            await this.connection.invoke("setSessionDetails", this.sessionId, screens);
            await rtcSession.startRtcSession();
        }
    }

    async getIceServers() : Promise<RTCIceServer[]> {
        return await this.connection.invoke("getIceServers");
    }

    async sendIceCandidate(candidateJson: string) : Promise<void> {
        await this.connection.invoke("sendIceCandidate", this.sessionId, candidateJson);
    }

    async sendRtcSessionDescription(sessionDescription: RTCSessionDescription) {
        await this.connection.invoke("sendRtcSessionDescription", this.sessionId, sessionDescription);
    }

    private setHandlers() {
        this.connection.onclose((err) => {
            window.mainApi.writeLog("Connection closed.  Exiting.");
            window.setTimeout(() => {
                window.mainApi.exit();
            }, 1000);
        });

        this.connection.on(
            "receiveDto",
            async (dto: SignedPayloadDto) => {
                await receiveDto(dto);
            }
        );
    }
}

const streamerHubConnection = new StreamerHubConnection();

export default streamerHubConnection;