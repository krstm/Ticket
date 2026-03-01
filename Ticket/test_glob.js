import { globby } from 'globby';
import { fileURLToPath } from 'url';
import path from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function run() {
    const pattern = path.resolve(__dirname, 'Views/**/*.cshtml').replace(/\\/g, '/');
    console.log('Testing pattern:', pattern);

    // try different globs
    const fastGlob = await import('fast-glob');
    const result = await fastGlob.default(pattern);
    console.log('Files matched:', result.length);
    console.log(result.slice(0, 3));
}

run();
