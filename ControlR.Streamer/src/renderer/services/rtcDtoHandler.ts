import { Point } from "@nut-tree/nut-js";
import RtcSession from "./rtcSession";
import { setMediaStreams } from "./mediaHelperRenderer";

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
          keyDto.keyCode,
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
        await window.mainApi.invokeWheelScroll(
          scrollDto.deltaX,
          scrollDto.deltaY,
          scrollDto.deltaZ,
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
  return await window.mainApi.dipToScreenPoint({ x: x, y: y });
}
