import js from '@eslint/js';

const recommended = js.configs.recommended;
const baseLanguageOptions = recommended.languageOptions ?? {};
const baseGlobals = baseLanguageOptions.globals ?? {};

export default [
  {
    ignores: [
      'wwwroot/**',
      'node_modules/**',
      'bin/**',
      'obj/**',
      'Frontend/src/css/**'
    ],
  },
  {
    ...recommended,
    files: ['Frontend/src/**/*.js'],
    languageOptions: {
      ...baseLanguageOptions,
      ecmaVersion: 2023,
      sourceType: 'module',
      globals: {
        ...baseGlobals,
        window: 'readonly',
        document: 'readonly',
        fetch: 'readonly',
        console: 'readonly',
        alert: 'readonly',
        location: 'readonly',
        atob: 'readonly',
        HTMLCanvasElement: 'readonly'
      },
    },
    rules: {
      ...recommended.rules,
      'no-console': ['warn', { allow: ['error'] }],
    },
  },
];
