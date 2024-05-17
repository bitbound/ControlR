import { DisplayDto } from "./sharedDtos";

declare type RtcDtoType =
  | "pointerMove"
  | "keyEvent"
  | "mouseButtonEvent"
  | "resetKeyboardState"
  | "wheelScrollEvent"
  | "typeText"
  | "changeDisplay"
  | "clipboardChanged"
  | "displaysChanged";

export interface BaseDto {
  dtoType: RtcDtoType;
}

export interface ChangeDisplayDto extends BaseDto {
  mediaId: string;
  name: string;
}

export interface ClipboardChangedDto extends BaseDto {
  text: string | undefined | null;
}

export interface KeyEventDto extends BaseDto {
  isPressed: boolean;
  key: string;
  shouldRelease?: boolean;
}

export interface MouseButtonEventDto extends BaseDto {
  isPressed: boolean;
  button: number;
  percentX: number;
  percentY: number;
}

export interface PointerMoveDto extends BaseDto {
  percentX: number;
  percentY: number;
}

export interface TypeTextDto extends BaseDto {
  text: string;
}

export interface WheelScrollDto extends BaseDto {
  percentX: number;
  percentY: number;
  scrollX: number;
  scrollY: number;
}

export interface DisplaysChangedDto extends BaseDto {
  currentDisplay: DisplayDto;
  allDisplays: DisplayDto[];
}
