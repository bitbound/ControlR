import { CSSProperties, useEffect, useState } from "react";
import appicon from "../../assets/appicon.png";
import "./sessionIndicator.tsx.css";

export default function SessionIndicator() {
  const [state, setState] = useState({
    viewerName: "",
  });

  useEffect(() => {
    (async () => {
      const viewerName = await window.mainApi.getViewerName();
      window.mainApi.writeLog(
        `Setting viewer name "${viewerName}" for session indicator.`,
      );
      setState({
        ...state,
        viewerName: viewerName,
      });
    })();
  }, []);

  const message = !state.viewerName
    ? "Your screen is being viewed"
    : `${state.viewerName} is viewing your screen`;

  return (
    <div style={wrapperCss} className="draggable">
      <div>
        <img src={appicon} style={logoCss} />
      </div>
      <div className="text-primary">{message}</div>
    </div>
  );
}

const wrapperCss = {
  position: "absolute",
  top: "50%",
  left: "50%",
  display: "grid",
  gridTemplateColumns: "auto 1fr",
  columnGap: "5px",
  alignItems: "center",
  textAlign: "center",
  transform: "translate(-50%, -50%)",
  overflow: "hidden",
  whiteSpace: "nowrap",
} as CSSProperties;

const logoCss = {
  height: "50px",
  width: "50px",
  animationName: "spinLogo",
  animationIterationCount: "infinite",
  animationDirection: "alternate",
  animationDuration: "3s",
  animationTimeline: "linear",
} as CSSProperties;
