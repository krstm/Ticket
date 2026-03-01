import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';

export default defineConfig({
    plugins: [
        tailwindcss(),
    ],
    build: {
        outDir: 'wwwroot/dist',
        emptyOutDir: true,
        lib: {
            entry: path.resolve(__dirname, 'Frontend/src/main.js'),
            name: 'TicketFrontend',
            fileName: 'main',
            formats: ['iife'],
        },
        rollupOptions: {
            output: {
                assetFileNames: (assetInfo) => {
                    if (assetInfo.name === 'style.css') return 'main.css';
                    return assetInfo.name;
                },
            },
        },
    },
});
