export async function setMediaStreams(
  sourceId: string,
  name: string,
  peerConnection: RTCPeerConnection,
) {
  window.mainApi.writeLog(
    "Getting stream for media source ID: ",
    "Info",
    sourceId,
  );

  const constraints = getDefaultConstraints();
  if (sourceId && name !== "Entire screen") {
    constraints.video.mandatory.chromeMediaSourceId = sourceId;
  }

  let stream: MediaStream;

  window.mainApi.writeLog(
    "Getting user media with constraints: ",
    "Info",
    constraints,
  );

  try {
    stream = await navigator.mediaDevices.getUserMedia(
      constraints as MediaStreamConstraints,
    );
    setTracks(stream, peerConnection);
  } catch (ex) {
    window.mainApi.writeLog(
      "Failed to get media with audio constraints.  Dropping audio.",
      "Warning",
      ex,
    );
    delete constraints.audio;
    stream = await navigator.mediaDevices.getUserMedia(
      constraints as MediaStreamConstraints,
    );
    setTracks(stream, peerConnection);
  }
}

function getDefaultConstraints() {
  return {
    audio: {
      mandatory: {
        chromeMediaSource: "desktop",
      },
    },
    video: {
      mandatory: {
        chromeMediaSource: "desktop",
      },
    },
  } as ElectronMediaStreamConstraints;
}

function setTracks(stream: MediaStream, peerConnection: RTCPeerConnection) {
  const existingSenders = peerConnection.getSenders();
  stream.getTracks().forEach((track) => {
    const existingTracks = existingSenders.filter(
      (x) => x.track.kind == track.kind,
    );
    if (existingTracks && existingTracks.length > 0) {
      existingTracks.forEach((x) => {
        const trackObj = {
          kind: x.track.kind,
          id: x.track.id,
          label: x.track.label,
          readyState: x.track.readyState,
        };
        window.mainApi.writeLog("Replacing existing track: ", "Info", trackObj);
        x.replaceTrack(track);
      });
    } else {
      const trackObj = {
        kind: track.kind,
        id: track.id,
        label: track.label,
        readyState: track.readyState,
      };
      window.mainApi.writeLog("Adding track: ", "Info", trackObj);
      peerConnection.addTrack(track, stream);
    }
  });
}

interface ElectronMediaStreamConstraints extends MediaStreamConstraints {
  video: ElectronMediaTrackConstraints;
  audio: ElectronMediaTrackConstraints;
}

interface ElectronMediaTrackConstraints extends MediaTrackConstraints {
  mandatory?: {
    chromeMediaSourceId?: string;
    chromeMediaSource?: string;
  };
}
