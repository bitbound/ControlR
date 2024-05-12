import { decode } from "@msgpack/msgpack";
import { SignedPayloadDto } from "../../shared/signalrDtos";
import rtcSession from "./rtcSession";

export async function receiveDto(dto: SignedPayloadDto) {
  const result = await window.mainApi.verifyDto(
    dto.payload,
    dto.signature,
    dto.publicKey,
    dto.publicKeyPem,
  );
  if (!result) {
    window.mainApi.writeLog(
      "DTO signature failed verification.  Aborting.",
      "Error",
    );
    return;
  }

  switch (dto.dtoType) {
    case "RtcSessionDescription":
      const sessionDescription = decode(dto.payload) as RTCSessionDescription;
      await rtcSession.receiveRtcSessionDescription(sessionDescription);
      break;
    case "RtcIceCandidate":
      const iceCandidate = decode(dto.payload) as string;
      await rtcSession.receiveIceCandidate(iceCandidate);
      break;
    case "CloseStreamingSession":
      window.mainApi.writeLog("Received exit request. Shutting down.");
      await window.mainApi.exit();
      break;
    default:
      break;
  }
}
