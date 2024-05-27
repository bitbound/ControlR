import { Outlet } from "react-router";
import controlrIcon from "/assets/appicon.svg";
import { ThemeProvider, createTheme } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { styled } from "@mui/material/styles";
import { Link as RouterLink } from "react-router-dom";
import { Container, Link, Paper, Typography } from "@mui/material";

function Layout() {
  const darkTheme = createTheme({
    palette: {
      mode: "dark",
    },
  });

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Container fixed>
        <Paper elevation={1} sx={{ mt: 5 }}>
          <HeaderRow>
            <HeaderPaper variant="outlined">
              <Typography variant="h2" color="primary.main">
                ControlR
                <Logo src={controlrIcon} />
              </Typography>

              <Typography variant="subtitle1">
                Zero-trust remote control
              </Typography>
            </HeaderPaper>
          </HeaderRow>
          <>
            <Outlet />
          </>
          <LayoutFooter>
            <Link component={RouterLink} to={"/"}>
              Home
            </Link>
            <Spacer>|</Spacer>
            <Link component={RouterLink} to="/privacy">
              Privacy
            </Link>
            <Spacer>|</Spacer>
            <Link
              href="https://github.com/bitbound/controlr"
              target="_blank"
              rel="noreferrer"
            >
              GitHub
            </Link>
          </LayoutFooter>
        </Paper>
      </Container>
    </ThemeProvider>
  );
}

const Logo = styled("img")({
  transform: "translate(0.1em, -0.25em)",
  position: "absolute",
  height: "1em",
  width: "1em",
  backgroundColor: "rgb(15,15,15)",
  borderRadius: "25%",
});

const HeaderRow = styled("div")({
  textAlign: "center",
});

const HeaderPaper = styled(Paper)({
  display: "inline-block",
  padding: "1.5rem 3rem 2rem 3rem",
  marginTop: "2rem",
});

const LayoutFooter = styled("footer")({
  marginTop: "2rem",
  padding: "2rem",
  textAlign: "center",
  fontSize: "80%",
});

const Spacer = styled("div")({
  display: "inline-block",
  marginRight: "1em",
  marginLeft: "1em",
});

export default Layout;
