import {
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogContentText,
  DialogActions,
} from "@mui/material";

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

function DeployAgentDialog(props: Props) {
  return (
    <>
      <Dialog
        open={props.isOpen}
        onClose={props.onClose}
        aria-labelledby="deploy-agent-dialog"
      >
        <DialogTitle id="deploy-agent-dialog">Agent Deployment</DialogTitle>
        <DialogContent>
          <DialogContentText>
            The Viewer app has a Deploy page containing scripts for installing
            the agent. It will be preconfigured with your public key and chosen
            settings.
          </DialogContentText>
          <DialogContentText sx={{ mt: 1 }}>
            You are not required to manually download the agent. The files are
            only listed here for convenience, in case you want to use your own
            deployment method.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={props.onClose} autoFocus>
            Close
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

export default DeployAgentDialog;
