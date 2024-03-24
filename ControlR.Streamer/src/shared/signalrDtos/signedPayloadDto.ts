import { SignalrDtoType } from "./signalrDtoTypes";

export interface SignedPayloadDto {
    payload: Uint8Array;
    signature: Uint8Array;
    dtoType: SignalrDtoType;
    publicKey: Uint8Array;
    publicKeyPem: string;
}