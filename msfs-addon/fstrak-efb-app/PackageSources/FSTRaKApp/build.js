'use strict';

require('dotenv').config();
const esbuild = require('esbuild');
const { sassPlugin } = require('esbuild-sass-plugin');

const WATCH = process.env.SERVING_MODE === 'WATCH';
const MINIFY = process.env.MINIFY === 'true';

const baseConfig = {
  bundle: true,
  minify: MINIFY,
  sourcemap: false,
  target: ['es2017'],
  outdir: 'dist',
  loader: { '.svg': 'copy', '.png': 'copy' },
  plugins: [
    sassPlugin({ type: 'css-text' }),
  ],
  // @efb/efb-api is provided by the MSFS EFB runtime — must NOT be bundled
  external: ['@efb/efb-api'],
};

const appConfig = {
  ...baseConfig,
  entryPoints: ['src/FSTRaKApp.tsx'],
  format: 'iife',
};

async function build() {
  if (WATCH) {
    const ctx = await esbuild.context(appConfig);
    await ctx.watch();
    console.log('[FSTRaK EFB] watching for changes...');
  } else {
    await esbuild.build(appConfig);
    console.log('[FSTRaK EFB] build complete → dist/');
  }
}

build().catch((e) => { console.error(e); process.exit(1); });
