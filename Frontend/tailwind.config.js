/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        "surface-variant": "#353534",
        "surface-container-high": "#2a2a2a",
        "tertiary-container": "#c2b193",
        "surface-container-highest": "#353534",
        "secondary-fixed-dim": "#d6c692",
        "on-secondary-fixed-variant": "#51461e",
        "inverse-primary": "#735c00",
        "surface-bright": "#3a3939",
        "secondary-fixed": "#f3e2ac",
        "on-error-container": "#ffdad6",
        "outline-variant": "#4d4635",
        "on-primary": "#3c2f00",
        "on-tertiary-container": "#4f442c",
        "on-secondary": "#3a3009",
        "background": "#131313",
        "on-tertiary-fixed-variant": "#51452d",
        "on-tertiary": "#392f19",
        "on-error": "#690005",
        "inverse-on-surface": "#313030",
        "secondary-container": "#544820",
        "surface-container": "#201f1f",
        "primary": "#f2ca50",
        "on-surface-variant": "#d0c5af",
        "surface-container-lowest": "#0e0e0e",
        "on-primary-fixed": "#241a00",
        "inverse-surface": "#e5e2e1",
        "on-surface": "#e5e2e1",
        "on-primary-container": "#554300",
        "primary-fixed-dim": "#e9c349",
        "tertiary": "#deccad",
        "surface": "#131313",
        "on-background": "#e5e2e1",
        "surface-tint": "#e9c349",
        "tertiary-fixed-dim": "#d6c4a5",
        "tertiary-fixed": "#f3e0c0",
        "outline": "#99907c",
        "surface-container-low": "#1c1b1b",
        "on-secondary-fixed": "#231b00",
        "error": "#ffb4ab",
        "secondary": "#d6c692",
        "primary-fixed": "#ffe088",
        "on-primary-fixed-variant": "#574500",
        "error-container": "#93000a",
        "surface-dim": "#131313",
        "on-tertiary-fixed": "#231a06",
        "primary-container": "#d4af37",
        "on-secondary-container": "#c8b885"
      },
      fontFamily: {
        "headline": ["Noto Serif", "serif"],
        "body": ["Plus Jakarta Sans", "sans-serif"],
        "label": ["Plus Jakarta Sans", "sans-serif"]
      },
      borderRadius: { "DEFAULT": "0.125rem", "lg": "0.25rem", "xl": "0.5rem", "full": "0.75rem" }
    }
  },
  plugins: [
    require('@tailwindcss/forms'),
    require('@tailwindcss/container-queries')
  ],
}
