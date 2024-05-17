import RtcSession from "./rtcSession";
import { setMediaStreams } from "./mediaHelperRenderer";
import {
  BaseDto,
  PointerMoveDto,
  WheelScrollDto,
  TypeTextDto,
  ChangeDisplayDto,
  ClipboardChangedDto,
  KeyEventDto,
  MouseButtonEventDto,
} from "../../shared/rtcDtos";
import { Point } from "electron";

export async function handleDataChannelMessage(data: string) {
  const dto = JSON.parse(data) as BaseDto;
  switch (dto.dtoType) {
    case "pointerMove":
      {
        const moveDto = dto as PointerMoveDto;
        const point = await getAbsoluteScreenPoint(
          moveDto.percentX,
          moveDto.percentY,
        );
        await window.mainApi.movePointer(point.x, point.y);
      }
      break;
    case "keyEvent":
      {
        const keyDto = dto as KeyEventDto;
        await window.mainApi.invokeKeyEvent(
          keyDto.key,
          keyDto.isPressed,
          keyDto.shouldRelease,
        );
      }
      break;
    case "mouseButtonEvent":
      {
        const buttonDto = dto as MouseButtonEventDto;
        const point = await getAbsoluteScreenPoint(
          buttonDto.percentX,
          buttonDto.percentY,
        );
        await window.mainApi.invokeMouseButtonEvent(
          buttonDto.button,
          buttonDto.isPressed,
          point.x,
          point.y,
        );
      }
      break;
    case "resetKeyboardState":
      {
        await window.mainApi.resetKeyboardState();
      }
      break;
    case "wheelScrollEvent":
      {
        const scrollDto = dto as WheelScrollDto;
        const point = await getAbsoluteScreenPoint(
          scrollDto.percentX,
          scrollDto.percentY,
        );
        await window.mainApi.invokeWheelScroll(
          point.x,
          point.y,
          scrollDto.scrollY,
          scrollDto.scrollX,
        );
      }
      break;
    case "typeText":
      {
        const typeDto = dto as TypeTextDto;
        await window.mainApi.invokeTypeText(typeDto.text);
      }
      break;
    case "changeDisplay":
      {
        const changeDisplayDto = dto as ChangeDisplayDto;
        window.mainApi.writeLog(
          `Changing display to media ID: ${changeDisplayDto.mediaId}`,
        );
        await setMediaStreams(
          changeDisplayDto.mediaId,
          changeDisplayDto.name,
          RtcSession.peerConnection,
        );
        RtcSession.changeCurrentScreen(changeDisplayDto.mediaId);
      }
      break;
    case "clipboardChanged":
      {
        const clipboardDto = dto as ClipboardChangedDto;
        window.mainApi.writeLog("Received clipboard text.");
        window.mainApi.setClipboardText(clipboardDto.text);
      }
      break;
    default:
      console.warn("Unhandled DTO type: ", dto.dtoType);
      break;
  }
}

async function getAbsoluteScreenPoint(
  percentX: number,
  percentY: number,
): Promise<Point> {
  const currentScreen = RtcSession.currentScreen;

  const x = currentScreen.left + currentScreen.width * percentX;
  const y = currentScreen.top + currentScreen.height * percentY;
  return { x: x, y: y };
}
