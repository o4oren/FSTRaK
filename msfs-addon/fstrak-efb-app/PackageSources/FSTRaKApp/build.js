'use strict';

require('dotenv').config();
const esbuild = require('esbuild');
const { sassPlugin } = require('esbuild-sass-plugin');
const { globalExternals } = require('@fal-works/esbuild-plugin-global-externals');

const WATCH = process.env.SERVING_MODE === 'WATCH';
const MINIFY = process.env.MINIFY === 'true';

const baseConfig = {
  entryPoints: ['src/FSTRaKApp.tsx'],
  keepNames: true,
  bundle: true,
  outdir: '../../html_ui/efb_ui/efb_apps/FSTRaKApp',
  sourcemap: false,
  minify: MINIFY,
  target: 'es2017',
  define: {
    BASE_URL: '"coui://html_ui/efb_ui/efb_apps/FSTRaKApp"',
  },
  plugins: [
    globalExternals({
      '@microsoft/msfs-sdk': {
        varName: 'msfssdk',
        type: 'cjs',
      },
    }),
    sassPlugin({ type: 'css' }),
  ],
};

if (WATCH) {
  esbuild.context(baseConfig).then((ctx) => ctx.watch());
} else {
  esbuild.build(baseConfig).then(() => {
    console.log('[FSTRaK EFB] build complete → html_ui/efb_ui/efb_apps/FSTRaKApp/');
  });
}
