class State {
    constructor() {
        this.windowEventHandlers = [];
        this.lastPointerMove = Date.now();
        this.touchList = { length: 0 };
        this.previousPinchDistance = -1;
        this.mouseMoveTimeout = -1;
    }

    /** @type {any} */
    componentRef;

    /** @type {string} */
    currentPointerType;

    /** @type {RTCDataChannel} */
    dataChannel;

    /** @type {boolean} */
    isDragging;

    /** @type {boolean} */
    isMakingOffer;

    /** @type {number} */
    lastPointerMove;

    /** @type {boolean} */
    longPressStarted;

    /** @type {number} */
    longPressStartOffsetX;

    /** @type {number} */
    longPressStartOffsetY;

    /** @type {number} */
    mouseMoveTimeout;

    /** @type {RTCPeerConnection} */
    peerConnection;

    /** @type {PointerEvent} */
    pointerDownEvent;

    /** @type {number} */
    previousPinchDistance;

    /** @type {TouchList} */
    touchList;

    /** @type {string} */
    videoId;

    /** @type {HTMLVideoElement} */
    videoElement;

    /** @type {WindowEventHandler[]} */
    windowEventHandlers;
}

class WindowEventHandler {
    /**
     * 
     * @param {keyof WindowEventMap} type
     * @param {EventListener} handler
     */
    constructor(type, handler) {
        this.type = type;
        this.handler = handler;
    }

    /** @type {keyof WindowEventMap} */
    type;

    /** @type {EventListener} */
    handler;
}

/**
 * 
 * @param {string} videoId
 * @param {string} mediaId
 * @param {string} name
 */
export async function changeDisplays(videoId, mediaId, name) {
    const state = getState(videoId);
    const dto = {
        dtoType: "changeDisplay",
        mediaId: mediaId,
        name: name
    }
    
    state.dataChannel.send(JSON.stringify(dto));
}

/**
 * 
 * @param {string} videoId
 */
export async function dispose(videoId) {
    const state = getState(videoId);

    try {
        if (state.peerConnection) {
            state.peerConnection.close();
        }
    }
    catch { }

    try {
        if (state.dataChannel) {
            state.dataChannel.close();
        }
    }
    catch { }

    state.windowEventHandlers.forEach(x => {
        console.log("Removing event handler: ", x);
        window.removeEventListener(x.type, x.handler);
    })

    delete window[videoId];
}

/**
 * Retains a reference to the video element ID and registers
 * event handlers for the video element.
 * @param {any} componentRef
 * @param {string} videoId
 * @param {RTCIceServer[]} iceServers
 */
export async function initialize(componentRef, videoId, iceServers) {
    const state = getState(videoId);
    console.log("Initializing with state: ", state);

    /** @type {HTMLVideoElement} */
    const video = document.getElementById(videoId);

    state.componentRef = componentRef;
    state.videoId = videoId;
    state.videoElement = video;

    video.muted = true;
    video.defaultMuted = true;

    console.log("Creating peer connection with ICE servers: ", iceServers);
    invokeDotNet("LogInfo", videoId, "Creating peer connection with ICE servers: " + JSON.stringify(iceServers));
    state.peerConnection = new RTCPeerConnection({
        iceServers: iceServers
    });

    setPeerConnectionHandlers(state.peerConnection, videoId);

    video.addEventListener("pointerup", ev => {
        if (video.classList.contains("scroll-mode")) {
            ev.preventDefault();
            ev.stopPropagation();
            return;
        }

        if (state.longPressStarted && !state.isDragging) {
            sendMouseButtonEvent(ev.offsetX, ev.offsetY, true, 2, state);
            sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, 2, state);
        }

        if (state.longPressStarted && state.isDragging) {
            sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, 0, state);
        }

        resetTouchState(state);
    });

    video.addEventListener("pointercancel", ev => {
        resetTouchState(state);
    });
    video.addEventListener("pointerout", ev => {
        resetTouchState(state);
    });
    video.addEventListener("pointerleave", ev => {
        resetTouchState(state);
    });
    
    video.addEventListener("pointermove", ev => {
        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }
        
        if (video.classList.contains("scroll-mode")) {
            ev.preventDefault();
            ev.stopPropagation();

            sendPointerMove(state.pointerDownEvent.offsetX, state.pointerDownEvent.offsetY, state);

            let wheelScrollDto = {};
            const percentX = ev.offsetX / state.videoElement.clientWidth;
            const percentY = ev.offsetY / state.videoElement.clientHeight;

            if (Math.abs(ev.movementY) > Math.abs(ev.movementX)) {
                wheelScrollDto = {
                    dtoType: "wheelScrollEvent",
                    percentX: percentX,
                    percentY: percentY,
                    scrollY: ev.movementY * 4,
                    scrollX: 0
                };
            }
            else if (Math.abs(ev.movementX) > Math.abs(ev.movementY)) {
                wheelScrollDto = {
                    dtoType: "wheelScrollEvent",
                    percentX: percentX,
                    percentY: percentY,
                    scrollY: 0,
                    scrollX: ev.movementX * -4,
                };
            }
            else {
                return;
            }
            
            state.dataChannel.send(JSON.stringify(wheelScrollDto));
            return;
        }

        if (state.longPressStarted && !state.isDragging) {
            ev.preventDefault();
            ev.stopPropagation();

            const moveDistance = getDistanceBetween(
                state.longPressStartOffsetX,
                state.longPressStartOffsetY,
                ev.offsetX,
                ev.offsetY);

            if (moveDistance > 10) { 
                state.isDragging = true;
                sendPointerMove(state.longPressStartOffsetX, state.longPressStartOffsetY, state);
                sendMouseButtonEvent(state.longPressStartOffsetX, state.longPressStartOffsetY, true, 0, state);
            }

            return;
        }

        if (state.isDragging) {
            ev.preventDefault();
            ev.stopPropagation();
            sendPointerMove(ev.offsetX, ev.offsetY, state);
        }
    })

    
    video.addEventListener("pointerdown", ev => {
        state.currentPointerType = ev.pointerType;
        state.pointerDownEvent = ev;
    });

    video.addEventListener("pointerenter", ev => {
        state.currentPointerType = ev.pointerType;
    });

    video.addEventListener("touchmove", ev => {
        if (state.longPressStarted || state.isDragging || video.classList.contains("scroll-mode")) {
            ev.preventDefault();
        }
    });

    video.addEventListener("mousemove", async ev => {
        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }
     
        const now = Date.now();
        if (now - state.lastPointerMove < 50) {
            if (state.mouseMoveTimeout > -1) {
                window.clearTimeout(state.mouseMoveTimeout);
            }
            state.mouseMoveTimeout = window.setTimeout(() => {
                sendPointerMove(ev.offsetX, ev.offsetY, state);
            }, 60);
            return;
        }

        state.lastPointerMove = now;
        sendPointerMove(ev.offsetX, ev.offsetY, state);
    });

    video.addEventListener("mousedown", async ev => {
        ev.stopPropagation();
        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }

        sendMouseButtonEvent(ev.offsetX, ev.offsetY, true, ev.button, state);
    });

    video.addEventListener("mouseup", async ev => {
        ev.stopPropagation();
        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }

        sendMouseButtonEvent(ev.offsetX, ev.offsetY, false, ev.button, state);
    });

    video.addEventListener("wheel", ev => {
        ev.preventDefault();
        ev.stopPropagation();

        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }
        
        const percentX = ev.offsetX / state.videoElement.clientWidth;
        const percentY = ev.offsetY / state.videoElement.clientHeight;
        
        const wheelScrollDto = {
            dtoType: "wheelScrollEvent",
            percentX: percentX,
            percentY: percentY,
            scrollY: -ev.deltaY
        };
        state.dataChannel.send(JSON.stringify(wheelScrollDto));
    });

    video.addEventListener("contextmenu", async ev => {
        ev.preventDefault();
        ev.stopPropagation();

        if (!isDataChannelReady(videoId) ||
            video.classList.contains("minimized") ||
            video.classList.contains("scroll-mode")) {
            return;
        }
     
        if (state.currentPointerType == "touch") {
            state.longPressStarted = true;
            state.longPressStartOffsetX = ev.offsetX;
            state.longPressStartOffsetY = ev.offsetY;
        }
    });

    video.addEventListener("canplay", async () => {
        await invokeDotNet("LogInfo", videoId, "CanPlay event fired.  Playing.");
        await video.play();
        await invokeDotNet("NotifyStreamLoaded", videoId);
        //video.muted = false;
        //await invokeDotNet("NotifyStreamLoaded", videoId);
    });

    /** @param {KeyboardEvent} ev */
    const onKeyDown = (ev) => {
        if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
            return;
        }

        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }

        if (!ev.ctrlKey || !ev.shiftKey || ev.key.toLowerCase() != "i") {
            ev.preventDefault();
        }

        const keyPressDto = {
            dtoType: "keyEvent",
            isPressed: true,
            key: ev.key
        };
        state.dataChannel.send(JSON.stringify(keyPressDto));
    };
    window.addEventListener("keydown", onKeyDown);
    state.windowEventHandlers.push(new WindowEventHandler("keydown", onKeyDown));

    /** @param {KeyboardEvent} ev */
    const onKeyUp = (ev) => {
        if (document.querySelector("input:focus") || document.querySelector("textarea:focus")) {
            return;
        }

        if (!isDataChannelReady(videoId) || video.classList.contains("minimized")) {
            return;
        }
        ev.preventDefault();
        const keyPressDto = {
            dtoType: "keyEvent",
            isPressed: false,
            key: ev.key
        };
        state.dataChannel.send(JSON.stringify(keyPressDto));
    }
    window.addEventListener("keyup", onKeyUp);
    state.windowEventHandlers.push(new WindowEventHandler("keyup", onKeyUp));

    const onBlur = () => {
        if (!isDataChannelReady(videoId)) {
            return;
        }
        const resetKeysDto = {
            dtoType: "resetKeyboardState"
        };
        state.dataChannel.send(JSON.stringify(resetKeysDto));
    }
    window.addEventListener("blur", onBlur);
    state.windowEventHandlers.push("blur", onBlur);
}

/**
 * 
 * @param {HTMLVideoElement} videoElement
 */
export async function playVideo(videoElement) {
    videoElement.muted = false;
    await videoElement.play();
}

/**
 * 
 * @param {string} candidateJson
 * @param {string} videoId
 */
export async function receiveIceCandidate(candidateJson, videoId) {
    try {
        const state = getState(videoId);

        if (!candidateJson) {
            console.log("Received null (terminating) ICE candidate.");
            invokeDotNet("LogInfo", videoId, "Received null (terminating) ICE candidate");
            await this.peerConnection.addIceCandidate(null);
            return;
        }

        invokeDotNet("LogInfo", videoId, "Adding ICE candidate: " + candidateJson);
        const candidate = JSON.parse(candidateJson);
        console.log("Adding ICE candidate: ", candidate);
        state.peerConnection.addIceCandidate(candidate);
    }
    catch (ex) {
        console.error(ex);
        invokeDotNet("LogError", videoId, "Error while receiving ICE candidate: " + JSON.stringify(ex));
    }
}

/**
 * 
 * @param {RTCSessionDescription} sessionDescription
 * @param {string} videoId
 */
export async function receiveRtcSessionDescription(sessionDescription, videoId) {
    try {
        const state = getState(videoId);

        console.log("Setting remote description: ", sessionDescription);
        invokeDotNet("LogInfo", videoId, "Setting remote description: " + JSON.stringify(sessionDescription));

        await state.peerConnection.setRemoteDescription(sessionDescription);

        if (sessionDescription.type == "offer") {
            console.log("Creating RTC answer.");
            invokeDotNet("LogInfo", videoId, "Creating RTC answer.");
            await state.peerConnection.setLocalDescription();
            console.log("Sending RTC answer.", state.peerConnection.localDescription);
            invokeDotNet("LogInfo", videoId, "Sending RTC answer: " + JSON.stringify(state.peerConnection.localDescription));
            await invokeDotNet("SendRtcDescription", videoId, state.peerConnection.localDescription);
        }
    }
    catch (ex) {
        console.error(ex);
        invokeDotNet("LogError", videoId, "Error while receiving session description: " + JSON.stringify(ex));
    }
}

/**
 * 
 * @param {RTCIceServer[]} iceServers
 * @param {string} videoId
 */
export async function resetPeerConnection(iceServers, videoId) {
    const state = getState(videoId);

    if (state.peerConnection) {
        state.peerConnection.close();
    }

    state.peerConnection = new RTCPeerConnection({
        iceServers: iceServers,
    });
    setPeerConnectionHandlers(state.peerConnection, videoId);
}

/**
 * 
 * @param {number} pinchCenterX
 * @param {number} pinchCenterY
 * @param {HTMLDivElement} contentDiv
 * @param {HTMLVideoElement} videoRef
 * @param {number} videoCssWidth
 * @param {number} videoCssHeight
 * @param {number} widthChange
 * @param {number} heightChange
 */
export async function scrollTowardPinch(pinchCenterX, pinchCenterY, contentDiv, videoRef, videoCssWidth, videoCssHeight, widthChange, heightChange) {
    videoRef.style.width = `${videoCssWidth}px`;
    videoRef.style.height = `${videoCssHeight}px`;

    var clientAdjustedScrollLeftPercent = (contentDiv.scrollLeft + (contentDiv.clientWidth * .5)) / contentDiv.scrollWidth;
    var clientAdjustedScrollTopPercent = (contentDiv.scrollTop + (contentDiv.clientHeight * .5)) / contentDiv.scrollHeight;
    
    var pinchAdjustX = pinchCenterX / window.innerWidth - .5;
    var pinchAdjustY = pinchCenterY / window.innerHeight - .5;

    var scrollByX = widthChange * (clientAdjustedScrollLeftPercent + (pinchAdjustX * contentDiv.clientWidth / contentDiv.scrollWidth));
    var scrollByY = heightChange * (clientAdjustedScrollTopPercent + (pinchAdjustY * contentDiv.clientHeight / contentDiv.scrollHeight));

    contentDiv.scrollBy(scrollByX, scrollByY);
}

/**
 * 
 * @param {string} text
 * @param {string} videoId
 */
export async function sendClipboardText(text, videoId) {
    const state = getState(videoId);

    const typeDto = {
        dtoType: "clipboardChanged",
        text: text
    };
    state.dataChannel.send(JSON.stringify(typeDto));
}

/**
 * 
 * @param {string} key
 * @param {string} videoId
 */
export async function sendKeyPress(key, videoId) {
    const state = getState(videoId);

    const keyPressDto = {
        dtoType: "keyEvent",
        isPressed: true,
        shouldRelease: true,
        key: key,
    };
    state.dataChannel.send(JSON.stringify(keyPressDto));
}

/**
 * 
 * @param {string} text
 * @param {string} videoId
 */
export async function typeText(text, videoId) {
    const state = getState(videoId);

    const typeDto = {
        dtoType: "typeText",
        text: text
    };
    state.dataChannel.send(JSON.stringify(typeDto));
}

/**
 * @param {number} point1X
 * @param {number} point1Y
 * @param {number} point2X
 * @param {number} point2Y
 */
function getDistanceBetween(point1X, point1Y, point2X, point2Y) {
    return Math.sqrt(Math.pow(point1X - point2X, 2) +
        Math.pow(point1Y - point2Y, 2));
}

/**
 * 
 * @param {string} videoId
 * @returns {State}
 */
function getState(videoId) {
    if (!window[`state-${videoId}`]) {
        window[`state-${videoId}`] = new State();
    }
    return window[`state-${videoId}`];
}


/**
 * 
 * @param {string} videoId
 * @returns
 */
function isDataChannelReady(videoId) {
    const state = getState(videoId);
    if (!state.dataChannel || state.dataChannel.readyState != "open") {
        return false;
    }

    return true;
}

/**
 * 
 * @param {State} state
 */
function resetTouchState(state) {
    state.longPressStarted = false;
    state.isDragging = false;
    state.videoElement.parentElement.style.touchAction = "";
}

/**
 * 
 * @param {number} offsetX
 * @param {number} offsetY
 * @param {State} state
 */
function sendPointerMove(offsetX, offsetY, state) {
    const pointerMoveDto = {
        dtoType: "pointerMove",
        percentX: offsetX / state.videoElement.clientWidth,
        percentY: offsetY / state.videoElement.clientHeight
    };

    state.dataChannel.send(JSON.stringify(pointerMoveDto));
}

/**
 * 
 * @param {number} offsetX
 * @param {number} offsetY
 * @param {boolean} isPressed
 * @param {number} button
 * @param {State} state
 */
function sendMouseButtonEvent(offsetX, offsetY, isPressed, button, state) {
    const mouseButtonDto = {
        dtoType: "mouseButtonEvent",
        percentX: offsetX / state.videoElement.clientWidth,
        percentY: offsetY / state.videoElement.clientHeight,
        isPressed: isPressed,
        button: button
    };

    state.dataChannel.send(JSON.stringify(mouseButtonDto));
}


/**
 * 
 * @param {RTCPeerConnection} peerConnection
 * @param {string} videoId
 */
function setPeerConnectionHandlers(peerConnection, videoId) {
    const state = getState(videoId);

    peerConnection.addEventListener("connectionstatechange", ev => {
        console.log("Connection state changed: ", peerConnection.connectionState);
        invokeDotNet("LogInfo", videoId, "Connection state changed: " + peerConnection.connectionState);
        switch (peerConnection.connectionState) {
            case "closed":
            case "disconnected":
                peerConnection.restartIce();
                invokeDotNet("SetStatusMessage", videoId, "Reconnecting");
                invokeDotNet("NotifyConnectionLost", videoId, false);
                break;
            case "failed":
                invokeDotNet("SetStatusMessage", videoId, "Connection failed");
                invokeDotNet("NotifyConnectionLost", videoId, true);
                break;
            case "connected":
                break;
            default:
                break;
        }
    });

    peerConnection.addEventListener("iceconnectionstatechange", ev => {
        console.log("ICE connection state changed: ", peerConnection.iceConnectionState);
        invokeDotNet("LogInfo", videoId, "ICE connection state changed: " + peerConnection.iceConnectionState);
    });

    peerConnection.addEventListener("icecandidateerror", ev => {
        const err = {
            errorCode: ev.errorCode,
            errorText: ev.errorText,
            port: ev.port,
            url: ev.url,
            address: ev.address
        }
        console.log("ICE candidate error: ", ev);
        invokeDotNet("LogInfo", videoId, "ICE candidate error: " + JSON.stringify(err));
    });

    peerConnection.addEventListener("signalingstatechange", ev => {
        console.log("Signaling state changed: ", peerConnection.signalingState);
        invokeDotNet("LogInfo", videoId, "Signaling state changed: " + peerConnection.signalingState);
    });

    peerConnection.addEventListener("track", async ev => {
        console.log("Received track: ", ev.track);

        state.videoElement.srcObject = ev.streams[0];

        const trackObj = {
            kind: ev.track.kind,
            id: ev.track.id,
            label: ev.track.label,
            readyState: ev.track.readyState
        }
        await invokeDotNet("LogInfo", videoId, "Received track: " + JSON.stringify(trackObj));
    });

    peerConnection.addEventListener("datachannel", async ev => {
        console.log("Received data channel: ", ev);
        await invokeDotNet("LogInfo", videoId, "Received data channel: " + JSON.stringify(ev.channel));
        state.dataChannel = ev.channel;
        setDataChannelHandlers(state.dataChannel, videoId);
    })

    peerConnection.addEventListener("negotiationneeded", async () => {
        try {
            state.isMakingOffer = true;
            console.log("Negotiation needed.");
            await invokeDotNet("LogInfo", videoId, "Negotiation needed.");
            await peerConnection.setLocalDescription();
            await invokeDotNet("LogInfo", videoId, "Sending RTC offer: " + JSON.stringify(peerConnection.localDescription));
            await invokeDotNet("SendRtcDescription", videoId, peerConnection.localDescription);
        }
        catch (ex) {
            console.error(ex);
            await invokeDotNet("LogError", videoId, "Error occurred: " + JSON.stringify(ex));
        }
        finally {
            state.isMakingOffer = false;
        }
    });

    peerConnection.addEventListener("icecandidate", async ev => {
        if (!ev.candidate) {
            console.log("End of ICE candidates.");
            invokeDotNet("LogInfo", videoId, "End of ICE candidates.");
            return;
        }

        console.log("Sending ICE candidate: ", ev.candidate);
        invokeDotNet("LogInfo", videoId, "Sending ICE candidate: " + JSON.stringify(ev.candidate));
        await invokeDotNet("SendIceCandidate", videoId, JSON.stringify(ev.candidate));
    });
}

/**
 * 
 * @param {RTCDataChannel} dataChannel
 * @param {string} videoId
 */
function setDataChannelHandlers(dataChannel, videoId) {
    dataChannel.addEventListener("close", () => {
        console.log("DataChannel closed.");
        invokeDotNet("LogInfo", videoId, "DataChannel closed.");
    });

    dataChannel.addEventListener("error", () => {
        console.log("DataChannel error.");
        invokeDotNet("LogInfo", videoId, "DataChannel error.");
    });

    dataChannel.addEventListener("open", async () => {
        console.log("DataChannel opened");
        invokeDotNet("LogInfo", videoId, "DataChannel opened.");
    });

    dataChannel.addEventListener("message", async ev => {
        console.log("Got DataChannel message: ", ev.data);
        await invokeDotNet("LogInfo", videoId, "Got DataChannel message: " + ev.data);
        const dto = JSON.parse(ev.data);
        switch (dto.dtoType) {
            case "clipboardChanged":
                await invokeDotNet("SetClipboardText", videoId, dto.text)
                break;
            case "displaysChanged":
                await invokeDotNet("SetDisplays", videoId, dto.allDisplays);
                await invokeDotNet("SetCurrentDisplay", videoId, dto.currentDisplay);
                break;
            default:
                console.log("Unrecogized DTO type: ", dto);
                await invokeDotNet("LogError", videoId, `Unrecognized DTO type: ${ev.data}`);
                break;
        }
    });
}

/**
 * @param {string} methodName
 * @param {string} videoId
 * @returns {Promise<any>}
 */
function invokeDotNet(methodName, videoId, args) {
    const state = getState(videoId);
    return state.componentRef.invokeMethodAsync(methodName, args);
}