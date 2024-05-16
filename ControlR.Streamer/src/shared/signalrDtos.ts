export type SignalrDtoType =
  | "None"
  | "RtcSessionDescription"
  | "RtcIceCandidate"
  | "CloseStreamingSession";

export interface SignedPayloadDto {
  payload: Uint8Array;
  signature: Uint8Array;
  dtoType: SignalrDtoType;
  publicKey: Uint8Array;
  publicKeyPem: string;
}