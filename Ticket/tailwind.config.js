import { fileURLToPath } from 'url';
import path from 'path';
import forms from '@tailwindcss/forms';
import typography from '@tailwindcss/typography';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/** @type {import('tailwindcss').Config} */
export default {
    content: [
        path.resolve(__dirname, 'Views/**/*.cshtml').replace(/\\/g, '/'),
        path.resolve(__dirname, 'Frontend/src/**/*.{js,css}').replace(/\\/g, '/'),
    ],
    theme: {
        extend: {
            colors: {
                primary: {
                    50: '#f5f7ff',
                    100: '#ebf0fe',
                    200: '#ced9fd',
                    300: '#b1c2fc',
                    400: '#7695f9',
                    500: '#3b68f6',
                    600: '#355ddd',
                    700: '#2c4ea9',
                    800: '#233e87',
                    900: '#1d336e',
                    950: '#111e41',
                },
            },
            fontFamily: {
                sans: ['Inter', 'ui-sans-serif', 'system-ui', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'Helvetica Neue', 'Arial', 'Noto Sans', 'sans-serif'],
            },
        },
    },
    plugins: [
        forms,
        typography,
    ],
}
