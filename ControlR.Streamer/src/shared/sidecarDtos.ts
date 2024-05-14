declare type SidecarDtoType =
  | "Unknown"
  | "DesktopChanged"
  | "MovePointer"
  | "MouseButtonEvent"
  | "KeyEvent"
  | "TypeText"
  | "ResetKeyboardState"
  | "WheelScroll";

declare type MovePointerType = "Absolute" | "Relative";

export interface SidecarDtoBase {
  dtoType: SidecarDtoType;
}

export interface DesktopChangedDto extends SidecarDtoBase {
  desktopName: string;
}

export interface KeyEventDto extends SidecarDtoBase {
  key: string;
  isPressed: boolean;
}

export interface MouseButtonEventDto extends SidecarDtoBase {
  x: number;
  y: number;
  isPressed: boolean;
  button: number;
}

export interface MovePointerDto extends SidecarDtoBase {
  x: number;
  y: number;
  moveType: MovePointerType;
}

export interface TypeTextDto extends SidecarDtoBase {
  text: string;
}

export interface WheelScrollDto extends SidecarDtoBase {
  x: number;
  y: number;
  scrollY: number;
  scrollX: number;
}
