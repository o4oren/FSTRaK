'use strict';

require('dotenv').config();
const esbuild = require('esbuild');
const { sassPlugin } = require('esbuild-sass-plugin');
const { globalExternals } = require('@fal-works/esbuild-plugin-global-externals');

const WATCH = process.env.SERVING_MODE === 'WATCH';
const MINIFY = process.env.MINIFY === 'true';

const baseConfig = {
  bundle: true,
  minify: MINIFY,
  sourcemap: false,
  target: ['es2017'],
  outdir: '../../html_ui/efb_ui/efb_apps/FSTRaKApp',
  loader: { '.svg': 'copy', '.png': 'copy' },
  plugins: [
    // Map @efb/efb-api to the global EFB_API exposed by the MSFS EFB runtime
    globalExternals({
      '@efb/efb-api': {
        varName: 'EFB_API',
        type: 'cjs',
      },
    }),
    sassPlugin({ type: 'css' }),
  ],
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
    console.log('[FSTRaK EFB] build complete → html_ui/efb_ui/efb_apps/FSTRaKApp/');
  }
}

build().catch((e) => { console.error(e); process.exit(1); });
