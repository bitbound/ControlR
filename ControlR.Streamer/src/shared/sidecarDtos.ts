declare type SidecarDtoType =
  | "Unknown"
  | "DesktopChanged"
  | "MovePointer"
  | "MouseButtonEvent";

declare type MovePointerType = "Absolute" | "Relative";

export interface SidecarDtoBase {
  dtoType: SidecarDtoType;
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
