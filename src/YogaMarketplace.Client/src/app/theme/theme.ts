import { createTheme } from "@mui/material/styles";
import { brand } from "@/constants/brand";

export const theme = createTheme({
  palette: {
    primary: { main: brand.primary, contrastText: brand.onPrimary },
    secondary: { main: brand.secondary, contrastText: brand.textPrimary },
    background: { default: brand.background, paper: brand.surface },
    text: { primary: brand.textPrimary, secondary: brand.textSecondary },
    divider: brand.border,
    warning: { main: brand.accent },
  },
  typography: {
    fontFamily: '"Raleway Variable", Raleway, sans-serif',
    button: { textTransform: "none", fontWeight: 600 },
    h1: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
    h2: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
    h3: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
    h4: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
    h5: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
    h6: { fontFamily: '"Lora Variable", Lora, serif', fontWeight: 500 },
  },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: {
          cursor: "pointer",
          minHeight: 44,
          transition: "background-color 200ms ease, opacity 200ms ease",
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          backgroundColor: brand.surface,
          "&:hover .MuiOutlinedInput-notchedOutline": {
            borderColor: brand.primary,
          },
        },
      },
    },
    MuiTab: {
      styleOverrides: {
        root: {
          cursor: "pointer",
          minHeight: 44,
          minWidth: 0,
        },
      },
    },
  },
});
