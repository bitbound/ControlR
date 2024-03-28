import { SignedPayloadDto } from "src/shared/dtos/signedPayloadDto";
import { DisplayDto } from "src/shared/signalrDtos/displayDto";

declare interface MainApi {
  exit(): Promise<void>;
  verifyDto(payload: Uint8Array, signature: Uint8Array, publicKey: Uint8Array, publicKeyPem: string): Promise<boolean>;
  getServerUri(): Promise<string>;
  getSessionId(): Promise<string>;
  getNotifyUser(): Promise<boolean>;
  getViewerName(): Promise<string>;
  getDisplays(): Promise<DisplayDto[]>;
  movePointer(x: number, y: number): Promise<void>;
  invokeMouseButtonEvent(button: number, isPressed: boolean, x: number, y: number): Promise<void>;
  invokeKeyEvent(keyCode: string, isPressed: boolean, shouldRelease: boolean): Promise<void>;
  resetKeyboardState(): Promise<void>;
  invokeWheelScroll(deltaX: number, deltaY: number, deltaZ: number): Promise<void>;
  invokeTypeText(text: string): Promise<void>;
  writeLog(message: string, level: LogLevel = "Info", ...args: any[]);
}

declare global {
  interface Window {
    mainApi: MainApi
  }
}