export type SignalrDtoType =
  | "None"
  | "RtcSessionDescription"
  | "RtcIceCandidate"
  | "CloseStreamingSession";

export interface DisplayDto {
  left: number;
  top: number;
  isPrimary: boolean;
  name: string;
  displayId: string;
  mediaId: string;
  width: number;
  height: number;
  label: string;
  scaleFactor: number;
}

export interface SignedPayloadDto {
  payload: Uint8Array;
  signature: Uint8Array;
  dtoType: SignalrDtoType;
  publicKey: Uint8Array;
  publicKeyPem: string;
}
