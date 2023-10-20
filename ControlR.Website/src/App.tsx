import "./App.css"
import controlrIcon from "/assets/appicon.svg";
import { ThemeProvider, createTheme } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import { styled } from '@mui/material/styles';
import { Button, Paper, Stack, Typography } from '@mui/material'

function App() {

  const darkTheme = createTheme({
    palette: {
      mode: 'dark',
    },
  });

  return (
    <>
      <ThemeProvider theme={darkTheme}>
        <CssBaseline />

        <HeaderRow>
          <HeaderPaper>
            <Typography variant='h2' color='primary.main'>
              ControlR
              <Logo src={controlrIcon} />
            </Typography>

            <Typography variant='subtitle1'>
              Zero-trust remote control
            </Typography>
          </HeaderPaper>
        </HeaderRow>

        <Stack sx={{ textAlign: 'center' }}>
          <Typography 
            variant='h4'
            color='success.main'
            mt={3}
            mb={1}>
            Viewer
          </Typography>

          <Typography variant='h6'>
            Windows 10/11
          </Typography>
          <ButtonWrapper>
            <Button 
              variant='outlined'
              href='/downloads/ControlR.Viewer.msix'
              target='_blank'>
                MSIX
            </Button>
          </ButtonWrapper>

          <Typography variant='h6'>
            Android
          </Typography>
          <ButtonWrapper>
            <Button 
              variant='outlined'
              href="/downloads/ControlR.Viewer.apk"
              target='_blank'>
              APK
            </Button>
          </ButtonWrapper>

          <Typography 
            variant='h4'
            color='success.main'
            mt={3}
            mb={1}>
            Agent
          </Typography>

          <Typography variant='h6'>
            Windows
          </Typography>
          <ButtonWrapper>
            <Button 
              variant='outlined'
              href="/downloads/ControlR.Agent.exe"
              target='_blank'>
              EXE
            </Button>
          </ButtonWrapper>

          <Typography variant='h6'>
            Ubuntu
          </Typography>
          <ButtonWrapper>
            <Button 
              variant='outlined'
              href="/downloads/ControlR.Agent"
              target='_blank'>
              EXE
            </Button>
          </ButtonWrapper>
        </Stack>
      </ThemeProvider>
    </>
  )
}

const HeaderRow = styled('div')({
  textAlign: 'center'
})

const HeaderPaper = styled(Paper)({
  display: 'inline-block',
  padding: '1.5rem 3rem 2rem 3rem',
  marginTop: '4rem'
})

const Logo = styled('img')({
  transform: 'translate(0.1em, -0.25em)',
  position: 'absolute',
  height: '1em',
  width: '1em',
  backgroundColor: "rgb(15,15,15)",
  borderRadius: "25%",
})

const ButtonWrapper = styled('div')({
  marginTop: '0.5rem',
  marginBottom: '1rem'
})

export default App