import { Component, CSSProperties } from "react";
import { iconImage512x512Base64 } from "../images";
import "./sessionIndicator.tsx.css";

export class SessionIndicator extends Component {
    render() {
       
        return (
            <div style={wrapperCss} className="draggable">
                <div>
                    <img
                        src={iconImage512x512Base64}
                        style={logoCss} />

                </div>
                <div className="text-primary">
                    Your screen is being viewed
                </div>
            </div>
        );
    }
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
    animationTimeline: "linear"
} as CSSProperties;


