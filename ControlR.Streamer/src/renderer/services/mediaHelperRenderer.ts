export async function setMediaStreams(sourceId: string, peerConnection: RTCPeerConnection) {
    window.mainApi.writeLog("Getting stream for media source ID: ", "Info", sourceId);

    const constraints = getDefaultConstraints();
    if (sourceId && sourceId !== "all") {
        constraints.video.mandatory.chromeMediaSourceId = sourceId;
    }
    else {
        delete constraints.video.mandatory.chromeMediaSourceId;
    }

    let stream: MediaStream;

    try {
        stream = await navigator.mediaDevices.getUserMedia(constraints as MediaStreamConstraints);
        setTrack(stream, peerConnection);
    }
    catch (ex) {
        window.mainApi.writeLog("Failed to get media with audio constraints.  Dropping audio.", "Warning", ex);
        delete constraints.audio;
        stream = await navigator.mediaDevices.getUserMedia(constraints as MediaStreamConstraints);
        setTrack(stream, peerConnection);
    }
}

function getDefaultConstraints() {
    return {
        video: {
            mandatory: {
                chromeMediaSource: 'desktop',
                chromeMediaSourceId: ''
            }
        },
        audio: {
            mandatory: {
                chromeMediaSource: 'desktop',
            }
        }
    };
}

function setTrack(stream: MediaStream, peerConnection: RTCPeerConnection) {
    stream.getTracks().forEach(track => {
        const existingSenders = peerConnection.getSenders();
        const existingTracks = existingSenders.filter(x => x.track.kind == track.kind);
        if (existingTracks && existingTracks.length > 0) {
            existingTracks.forEach(x => {
                const trackObj = {
                    kind: x.track.kind,
                    id: x.track.id,
                    label: x.track.label,
                    readyState: x.track.readyState
                }
                window.mainApi.writeLog("Replacing existing track: ", "Info", trackObj);
                x.replaceTrack(track);
            });
        }
        else {
            const trackObj = {
                kind: track.kind,
                id: track.id,
                label: track.label,
                readyState: track.readyState
            }
            window.mainApi.writeLog("Adding track: ", "Info", trackObj);
            peerConnection.addTrack(track, stream);
        }
    });
}