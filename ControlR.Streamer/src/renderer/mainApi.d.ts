import { SignedPayloadDto } from "src/shared/dtos/signedPayloadDto";
import { DisplayDto } from "src/shared/signalrDtos/displayDto";
import { Point } from "electron";

declare interface MainApi {
  setClipboardText(text: string): Promise<void>;
  exit(): Promise<void>;
  verifyDto(
    payload: Uint8Array,
    signature: Uint8Array,
    publicKey: Uint8Array,
    publicKeyPem: string,
  ): Promise<boolean>;
  getServerUri(): Promise<string>;
  getSessionId(): Promise<string>;
  getViewerConnectionId(): Promise<string>;
  getNotifyUser(): Promise<boolean>;
  getViewerName(): Promise<string>;
  getDisplays(): Promise<DisplayDto[]>;
  dipToScreenPoint(point: Point): Promise<Point>;
  movePointer(x: number, y: number): Promise<void>;
  invokeMouseButtonEvent(
    button: number,
    isPressed: boolean,
    x: number,
    y: number,
  ): Promise<void>;
  invokeKeyEvent(
    key: string,
    isPressed: boolean,
    shouldRelease: boolean,
  ): Promise<void>;
  resetKeyboardState(): Promise<void>;
  invokeWheelScroll(
    x: number,
    y: number,
    scrollY: number,
    scrollX: number,
  ): Promise<void>;
  invokeTypeText(text: string): Promise<void>;
  writeLog(message: string, level: LogLevel = "Info", ...args: any[]);

  onLocalClipboardChanged(callback: (text: string | undefined | null) => void);
  onInputDesktopChanged(callback: (desktopName: string) => void);
  onDisplaysChanged(callback: () => void);
}

declare global {
  interface Window {
    mainApi: MainApi;
  }
}
