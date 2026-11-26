# ReceiptCapture audit

The audit covers the `web/` submodule at `24e923c` and compares its tests with `c6d97d2`. The initial root commit was `23ac797`. The root advanced to `1dc8006` during the audit while the web commit stayed fixed. Findings use observed code, fresh command output and browser probes. The full-suite gate here covers the frontend submodule.

## 1. Raw command output

<details>
<summary>Step 0 — checkout, history, diff and tool versions</summary>

```text
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
23ac797 Update web submodule with reusable receipt capture
1005a71 Step C: add extraction schema constraints
7e834b5 Step B: compose receipt field parser
7712d2b Step 3: refactor extraction pipeline and add document classification features
dd7cdb6 Step 3: add vendor extractor
0baa65f Step 3: add tax invoice detector
a075a0c Step 3: add VAT classifier
3b17878 Step 3: add VAT number validator
30de5b0 Step 3: add printed VAT amount extractor
4618c3d Step 3: add date parser
ada8376 Step 3: add amount parser
a776c44 feat: Add expense extraction tables and models
68b1a81 feat(auth): update email verification to require password and add rate limiting
7bac1b4 Mark subproject as dirty due to uncommitted changes in the web directory.
27e0be1 chore: Mark web submodule as dirty
 web | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
 24e923cd218e8268ed71fe6f045d952c19380406 web (heads/main)
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
24e923c Verify and optimise receipt capture for the mobile PWA
4f0a7d5 Replace simulated receipt capture with camera and draft review
c6d97d2 Remove references to data storage location in various components for improved privacy compliance
5fceee8 Remove references to data storage location in various components for improved privacy compliance
cb11aa7 Enhance ExpensesPage with detailed expense view and improved UI components
4f9311a Add invoice images and update gallery receipt image logic
6224f8d Add allowedHosts configuration for Vercel preview deployment
685350c Refactor and enhance various components for improved readability and consistency
8ad67a4 Add project README
d0ea9b6 Added real export page
a31fdc3 Changes
841c889 Changes
ce5ec45 Changes
25fc454 Changes
62ced8d Changes
 .gitignore                                        |   4 +
 bun.lock                                          |  17 +
 package.json                                      |   4 +
 playwright.config.ts                              |  20 +
 public/app-icon-192.png                           | Bin 0 -> 16397 bytes
 public/app-icon-512.png                           | Bin 0 -> 92161 bytes
 public/manifest.webmanifest                       |  13 +
 receipt-capture.md                                |  87 +++
 src/components/receipt-capture/ReceiptCapture.tsx | 816 ++++++++++++++++++++++
 src/components/receipt-capture/ReceiptDialog.tsx  |  51 ++
 src/components/receipt-capture/camera.ts          | 150 ++++
 src/components/receipt-capture/config.ts          |  17 +
 src/components/receipt-capture/draft.ts           |  36 +
 src/components/receipt-capture/images.test.js     |  81 +++
 src/components/receipt-capture/images.ts          | 144 ++++
 src/components/receipt-capture/service.ts         |  83 +++
 src/components/receipt-capture/useDraft.ts        |  64 ++
 src/components/receipt-capture/useReceiptSave.ts  | 123 ++++
 src/routes/__root.tsx                             |   1 +
 src/routes/_app/capture.image.tsx                 | 730 ++-----------------
 src/routes/_app/capture.index.tsx                 |  46 +-
 tests/receipt-capture.spec.ts                     | 298 ++++++++
 vite.config.ts                                    |   1 +
 23 files changed, 2076 insertions(+), 710 deletions(-)
v22.14.0
10.9.2
1.4.5
bun.lock
eslint.config.js
package.json
playwright.config.ts
receipt-capture.md
src/components/receipt-capture/ReceiptCapture.tsx
src/components/receipt-capture/ReceiptDialog.tsx
src/components/receipt-capture/camera.ts
src/components/receipt-capture/config.ts
src/components/receipt-capture/draft.ts
src/components/receipt-capture/images.test.js
src/components/receipt-capture/images.ts
src/components/receipt-capture/service.ts
src/components/receipt-capture/useDraft.ts
src/components/receipt-capture/useReceiptSave.ts
src/routes/_app/capture.image.tsx
src/routes/_app/capture.index.tsx
tests/receipt-capture.spec.ts
tsconfig.json
vite.config.ts
```

</details>

<details>
<summary>Step 1 — frozen install</summary>

```text
bun install v1.4.5 (a7733435c)

Checked 546 installs across 644 packages (no changes) [96.00ms]

exit=0
```

</details>

<details>
<summary>Step 1 — production build in mock mode</summary>

```text

> build
> vite build

[nitro] build:before: 0.226ms
[nitro] rollup:before: 0.01ms
vite v7.3.2 building client environment for production...
transforming...
✓ 2127 modules transformed.
rendering chunks...
computing gzip size...
.output/public/assets/ibm-plex-mono-vietnamese-400-normal-DDuiU_S-.woff              5.62 kB
.output/public/assets/ibm-plex-mono-vietnamese-600-normal-iLQfcSjf.woff              5.71 kB
.output/public/assets/ibm-plex-mono-cyrillic-ext-600-normal-9HEixskS.woff            5.79 kB
.output/public/assets/ibm-plex-mono-cyrillic-ext-400-normal-DMdlQ8Kv.woff            5.82 kB
.output/public/assets/ibm-plex-mono-vietnamese-400-normal-BulugwFq.woff2             5.87 kB
.output/public/assets/ibm-plex-mono-cyrillic-ext-400-normal-xuaO2J-f.woff2           6.91 kB
.output/public/assets/ibm-plex-mono-vietnamese-600-normal-D2EvbN8M.woff2             6.93 kB
.output/public/assets/ibm-plex-mono-cyrillic-400-normal-CEL4l2ZJ.woff                7.20 kB
.output/public/assets/ibm-plex-mono-cyrillic-600-normal-fLZuRloM.woff                7.27 kB
.output/public/assets/ibm-plex-mono-cyrillic-ext-600-normal-V-xxqcpd.woff2           7.69 kB
.output/public/assets/ibm-plex-mono-cyrillic-400-normal-BSMlKf0J.woff2               8.36 kB
.output/public/assets/bricolage-grotesque-vietnamese-wght-normal-BUzh504Q.woff2      8.61 kB
.output/public/assets/ibm-plex-mono-cyrillic-600-normal-CTOM6hUh.woff2               9.35 kB
.output/public/assets/ibm-plex-mono-latin-ext-400-normal-D3D2R8hC.woff              11.77 kB
.output/public/assets/ibm-plex-mono-latin-ext-600-normal-DmB0ttJJ.woff              11.83 kB
.output/public/assets/ibm-plex-mono-latin-400-normal-CvHOgSBP.woff                  13.14 kB
.output/public/assets/ibm-plex-mono-latin-600-normal-DWFSQ4vo.woff                  13.21 kB
.output/public/assets/ibm-plex-mono-latin-ext-400-normal-BmRBH3aV.woff2             13.35 kB
.output/public/assets/ibm-plex-mono-latin-ext-600-normal-D38SheWl.woff2             14.33 kB
.output/public/assets/ibm-plex-mono-latin-400-normal-DMJ8VG8y.woff2                 14.71 kB
.output/public/assets/ibm-plex-mono-latin-600-normal-BgSNZQsw.woff2                 15.62 kB
.output/public/assets/bricolage-grotesque-latin-ext-wght-normal-CcLUaPy7.woff2      18.67 kB
.output/public/assets/bricolage-grotesque-latin-wght-normal-DLoelf7F.woff2          41.34 kB
.output/public/assets/receipt-electronics-capture-CPQ7lXVx.jpg                     107.45 kB
.output/public/assets/receipt-karoo-capture-CvRRVTit.jpg                           111.72 kB
.output/public/assets/receipt-fuel-capture-DSdOBm4z.jpg                            129.09 kB
.output/public/assets/logo-mark-CpTYI4y_.png                                       643.63 kB
.output/public/assets/receipt-insurance-invoice-DIBI8VHe.png                     1,312.20 kB
.output/public/assets/receipt-mobile-invoice-EfIp6agP.png                        1,328.29 kB
.output/public/assets/receipt-city-power-invoice-BoPkANGX.png                    1,460.70 kB
.output/public/assets/receipt-rent-invoice-6Dw3L9pl.png                          1,642.96 kB
.output/public/assets/receipt-bank-fees-invoice-CgMixDs6.png                     1,691.03 kB
.output/public/assets/styles-4HiGrmUD.css                                          124.82 kB │ gzip:  20.62 kB
.output/public/assets/capture-CBPujPvr.js                                            0.07 kB │ gzip:   0.09 kB
.output/public/assets/check-DExNaN1_.js                                              0.12 kB │ gzip:   0.14 kB
.output/public/assets/plus-CYygj3sk.js                                               0.15 kB │ gzip:   0.15 kB
.output/public/assets/x-B64-XQHn.js                                                  0.16 kB │ gzip:   0.15 kB
.output/public/assets/clock-D8tvubMq.js                                              0.17 kB │ gzip:   0.17 kB
.output/public/assets/arrow-right-Bxqj1_Px.js                                        0.17 kB │ gzip:   0.16 kB
.output/public/assets/menu-CDks159y.js                                               0.19 kB │ gzip:   0.16 kB
.output/public/assets/upload-CTTZ04TR.js                                             0.23 kB │ gzip:   0.19 kB
.output/public/assets/triangle-alert-JQARisZo.js                                     0.27 kB │ gzip:   0.22 kB
.output/public/assets/pencil-BBHUxczi.js                                             0.28 kB │ gzip:   0.23 kB
.output/public/assets/mock-session-D7iUPPfs.js                                       0.30 kB │ gzip:   0.22 kB
.output/public/assets/settings-CMJLTDkO.js                                           0.33 kB │ gzip:   0.24 kB
.output/public/assets/camera-Dxb9zrD7.js                                             0.34 kB │ gzip:   0.25 kB
.output/public/assets/Stamp-DID1xPjJ.js                                              0.57 kB │ gzip:   0.34 kB
.output/public/assets/service-BoRO-u4h.js                                            1.29 kB │ gzip:   0.76 kB
.output/public/assets/capture.image-BqSZsOZ3.js                                      1.47 kB │ gzip:   0.75 kB
.output/public/assets/ThemeToggle-DA2CJ1G8.js                                        1.59 kB │ gzip:   0.81 kB
.output/public/assets/GoogleSignIn-2YE7k4XM.js                                       1.82 kB │ gzip:   0.99 kB
.output/public/assets/capture.index-Doa0YiEX.js                                      1.87 kB │ gzip:   0.86 kB
.output/public/assets/forgot-password-RoiAz1Jo.js                                    3.12 kB │ gzip:   1.30 kB
.output/public/assets/mock-gallery-media-DK_rzOSp.js                                 4.00 kB │ gzip:   1.98 kB
.output/public/assets/SiteFooter-0ftgfuzA.js                                         4.97 kB │ gzip:   1.67 kB
.output/public/assets/privacy-Sju5GyAl.js                                            5.32 kB │ gzip:   2.00 kB
.output/public/assets/onboarding-DFNYHxXT.js                                         6.08 kB │ gzip:   2.33 kB
.output/public/assets/subscribe-Dc9J2cwd.js                                          6.47 kB │ gzip:   2.42 kB
.output/public/assets/mock-data-B3Gxx59r.js                                          6.67 kB │ gzip:   2.30 kB
.output/public/assets/index-C7ABsEl3.js                                              7.57 kB │ gzip:   2.59 kB
.output/public/assets/dashboard-Bha8uMUx.js                                          8.22 kB │ gzip:   2.60 kB
.output/public/assets/capture.manual-BVcH5Ruq.js                                     8.34 kB │ gzip:   2.99 kB
.output/public/assets/login-DUH4GqFn.js                                              8.71 kB │ gzip:   3.15 kB
.output/public/assets/tutorials-U62KgncZ.js                                          9.36 kB │ gzip:   3.60 kB
.output/public/assets/register-7QrAiloA.js                                           9.42 kB │ gzip:   3.40 kB
.output/public/assets/gallery-E7sDxo1b.js                                            9.62 kB │ gzip:   3.48 kB
.output/public/assets/export-BHnE_pOd.js                                             9.89 kB │ gzip:   3.16 kB
.output/public/assets/_app-D9l2_BsG.js                                              10.49 kB │ gzip:   3.37 kB
.output/public/assets/expenses-BZvfPhNm.js                                          13.31 kB │ gzip:   4.25 kB
.output/public/assets/ReceiptCapture-CowI10-O.js                                    25.69 kB │ gzip:   8.89 kB
.output/public/assets/index-wIJHF4Ll.js                                             33.07 kB │ gzip:  11.09 kB
.output/public/assets/browser-image-compression-5Xiw9QKB.js                         53.17 kB │ gzip:  21.07 kB
.output/public/assets/index-BxPuUqdS.js                                            493.28 kB │ gzip: 154.51 kB
✓ built in 3.44s
vite v7.3.2 building ssr environment for production...
transforming...
"createRequestHandler", "defineHandlerCallback", "transformPipeableStreamWithRouter" and "transformReadableStreamWithRouter" are imported from external module "@tanstack/router-core/ssr/server" but never used in "node_modules/@tanstack/start-server-core/dist/esm/index.js".
"RawStream" is imported from external module "@tanstack/router-core" but never used in "node_modules/@tanstack/start-client-core/dist/esm/index.js".
"defaultSerovalDeserializerPlugins", "hydrate" and "json" are imported from external module "@tanstack/router-core/ssr/client" but never used in "node_modules/@tanstack/start-client-core/dist/esm/index.js".
✓ 122 modules transformed.
rendering chunks...
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-vietnamese-400-normal-DDuiU_S-.woff              5.62 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-vietnamese-600-normal-iLQfcSjf.woff              5.71 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-ext-600-normal-9HEixskS.woff            5.79 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-ext-400-normal-DMdlQ8Kv.woff            5.82 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-vietnamese-400-normal-BulugwFq.woff2             5.87 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-ext-400-normal-xuaO2J-f.woff2           6.91 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-vietnamese-600-normal-D2EvbN8M.woff2             6.93 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-400-normal-CEL4l2ZJ.woff                7.20 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-600-normal-fLZuRloM.woff                7.27 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-ext-600-normal-V-xxqcpd.woff2           7.69 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-400-normal-BSMlKf0J.woff2               8.36 kB
node_modules/.nitro/vite/services/ssr/assets/bricolage-grotesque-vietnamese-wght-normal-BUzh504Q.woff2      8.61 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-cyrillic-600-normal-CTOM6hUh.woff2               9.35 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-ext-400-normal-D3D2R8hC.woff              11.77 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-ext-600-normal-DmB0ttJJ.woff              11.83 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-400-normal-CvHOgSBP.woff                  13.14 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-600-normal-DWFSQ4vo.woff                  13.21 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-ext-400-normal-BmRBH3aV.woff2             13.35 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-ext-600-normal-D38SheWl.woff2             14.33 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-400-normal-DMJ8VG8y.woff2                 14.71 kB
node_modules/.nitro/vite/services/ssr/assets/ibm-plex-mono-latin-600-normal-BgSNZQsw.woff2                 15.62 kB
node_modules/.nitro/vite/services/ssr/assets/bricolage-grotesque-latin-ext-wght-normal-CcLUaPy7.woff2      18.67 kB
node_modules/.nitro/vite/services/ssr/assets/bricolage-grotesque-latin-wght-normal-DLoelf7F.woff2          41.34 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-electronics-capture-CPQ7lXVx.jpg                     107.45 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-karoo-capture-CvRRVTit.jpg                           111.72 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-fuel-capture-DSdOBm4z.jpg                            129.09 kB
node_modules/.nitro/vite/services/ssr/assets/logo-mark-CpTYI4y_.png                                       643.63 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-insurance-invoice-DIBI8VHe.png                     1,312.20 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-mobile-invoice-EfIp6agP.png                        1,328.29 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-city-power-invoice-BoPkANGX.png                    1,460.70 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-rent-invoice-6Dw3L9pl.png                          1,642.96 kB
node_modules/.nitro/vite/services/ssr/assets/receipt-bank-fees-invoice-CgMixDs6.png                     1,691.03 kB
node_modules/.nitro/vite/services/ssr/assets/styles-4HiGrmUD.css                                          124.82 kB
node_modules/.nitro/vite/services/ssr/assets/capture-hdlc17Rz.js                                            0.12 kB
node_modules/.nitro/vite/services/ssr/assets/empty-plugin-adapters-BFgPZ6_d.js                              0.13 kB
node_modules/.nitro/vite/services/ssr/assets/settings-CoEIMid7.js                                           0.44 kB
node_modules/.nitro/vite/services/ssr/assets/mock-session-Bim5X3GX.js                                       0.51 kB
node_modules/.nitro/vite/services/ssr/assets/capture.image-CQUVDYJR.js                                      0.74 kB
node_modules/.nitro/vite/services/ssr/assets/Stamp-DZ6-xlKf.js                                              0.78 kB
node_modules/.nitro/vite/services/ssr/assets/ThemeToggle-1p4VzRLZ.js                                        1.70 kB
node_modules/.nitro/vite/services/ssr/assets/start-DxrNY2Pg.js                                              1.71 kB
node_modules/.nitro/vite/services/ssr/assets/GoogleSignIn-DEKqH6Vb.js                                       2.36 kB
node_modules/.nitro/vite/services/ssr/assets/capture.index-1aHUpwPH.js                                      2.45 kB
node_modules/.nitro/vite/services/ssr/index.js                                                              3.86 kB
node_modules/.nitro/vite/services/ssr/assets/forgot-password-D33FtRaD.js                                    5.39 kB
node_modules/.nitro/vite/services/ssr/assets/_tanstack-start-manifest_v-DXv92hHk.js                         5.57 kB
node_modules/.nitro/vite/services/ssr/assets/mock-gallery-media-D8KbnjFX.js                                 5.63 kB
node_modules/.nitro/vite/services/ssr/assets/privacy-DWHArCMl.js                                            7.47 kB
node_modules/.nitro/vite/services/ssr/assets/onboarding-VEKPZ03c.js                                         8.05 kB
node_modules/.nitro/vite/services/ssr/assets/SiteFooter-B61eSrHd.js                                         8.23 kB
node_modules/.nitro/vite/services/ssr/assets/subscribe-t_iXh4dm.js                                          8.69 kB
node_modules/.nitro/vite/services/ssr/assets/mock-data-Cd2feQ2T.js                                          8.99 kB
node_modules/.nitro/vite/services/ssr/assets/index-OiX3OSYk.js                                             10.75 kB
node_modules/.nitro/vite/services/ssr/assets/capture.manual-FRtV0Gzn.js                                    11.71 kB
node_modules/.nitro/vite/services/ssr/assets/tutorials-CX0HznpC.js                                         11.78 kB
node_modules/.nitro/vite/services/ssr/assets/dashboard-Ci4R1svs.js                                         12.13 kB
node_modules/.nitro/vite/services/ssr/assets/_app-ByP-soIy.js                                              12.82 kB
node_modules/.nitro/vite/services/ssr/assets/gallery-BwXan2v9.js                                           13.35 kB
node_modules/.nitro/vite/services/ssr/assets/login-z10axFW2.js                                             13.44 kB
node_modules/.nitro/vite/services/ssr/assets/export-FzJy86qA.js                                            14.14 kB
node_modules/.nitro/vite/services/ssr/assets/register-BEIJ81k0.js                                          15.76 kB
node_modules/.nitro/vite/services/ssr/assets/expenses-Wzh-FF9V.js                                          19.54 kB
node_modules/.nitro/vite/services/ssr/assets/router-Cush_Qeb.js                                            30.80 kB
node_modules/.nitro/vite/services/ssr/assets/server-wcqgOGZo.js                                            68.70 kB
✓ built in 1.36s

[nitro] ◐ Building [Nitro] (preset: cloudflare-module, compatibility: 2026-11-26)
[nitro] ✔ Generated public .output/public
[nitro] prerender:routes: 0.006ms
vite v7.3.2 building nitro environment for production...
Unknown input options: platform. Allowed options: cache, context, experimentalCacheExpiry, experimentalLogSideEffects, external, fs, input, jsx, logLevel, makeAbsoluteExternalsRelative, maxParallelFileOps, moduleContext, onLog, onwarn, perf, plugins, preserveEntrySignatures, preserveSymlinks, shimMissingExports, strictDeprecations, treeshake, watch
transforming...
node_modules/@tanstack/react-router/dist/esm/index.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/index.js" was ignored.
node_modules/sonner/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/sonner/dist/index.mjs" was ignored.
node_modules/@tanstack/react-router/dist/esm/utils.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/utils.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/ClientOnly.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/ClientOnly.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/useNavigate.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useNavigate.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/RouterProvider.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/RouterProvider.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/useLocation.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useLocation.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/useRouterState.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useRouterState.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/HeadContent.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/HeadContent.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/useBlocker.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useBlocker.js" was ignored.
node_modules/@radix-ui/react-dialog/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-dialog/dist/index.mjs" was ignored.
node_modules/@tanstack/react-query/build/modern/useQueries.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useQueries.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useQuery.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useQuery.js" was ignored.
node_modules/@tanstack/react-query/build/modern/HydrationBoundary.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/HydrationBoundary.js" was ignored.
node_modules/@tanstack/react-query/build/modern/QueryErrorResetBoundary.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/QueryErrorResetBoundary.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useMutation.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useMutation.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useIsFetching.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useIsFetching.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useMutationState.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useMutationState.js" was ignored.
node_modules/@tanstack/react-query/build/modern/QueryClientProvider.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/QueryClientProvider.js" was ignored.
node_modules/@tanstack/react-query/build/modern/IsRestoringProvider.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/IsRestoringProvider.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useInfiniteQuery.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useInfiniteQuery.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/routerContext.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/routerContext.js" was ignored.
node_modules/@radix-ui/react-dismissable-layer/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-dismissable-layer/dist/index.mjs" was ignored.
node_modules/@radix-ui/react-portal/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-portal/dist/index.mjs" was ignored.
node_modules/@radix-ui/react-presence/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-presence/dist/index.mjs" was ignored.
node_modules/@radix-ui/react-focus-guards/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-focus-guards/dist/index.mjs" was ignored.
node_modules/@tanstack/react-query/build/modern/errorBoundaryUtils.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/errorBoundaryUtils.js" was ignored.
node_modules/@radix-ui/react-focus-scope/dist/index.mjs (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@radix-ui/react-focus-scope/dist/index.mjs" was ignored.
node_modules/@tanstack/react-router/dist/esm/useRouter.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useRouter.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/CatchBoundary.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/CatchBoundary.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/useMatch.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/useMatch.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/Matches.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/Matches.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/Match.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/Match.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/link.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/link.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/Asset.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/Asset.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/matchContext.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/matchContext.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useSuspenseInfiniteQuery.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useSuspenseInfiniteQuery.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useSuspenseQueries.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useSuspenseQueries.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useSuspenseQuery.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useSuspenseQuery.js" was ignored.
node_modules/@tanstack/react-query/build/modern/useBaseQuery.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-query/build/modern/useBaseQuery.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/nonRouteComponentContext.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/nonRouteComponentContext.js" was ignored.
node_modules/@tanstack/react-router/dist/esm/Transitioner.js (1:0): Module level directives cause errors when bundled, "use client" in "node_modules/@tanstack/react-router/dist/esm/Transitioner.js" was ignored.
✓ 2129 modules transformed.
rendering chunks...
.output/server/_libs/unstorage.mjs                                       0.00 kB
.output/server/_libs/use-sync-external-store.mjs                         0.00 kB
.output/server/_libs/detect-node-es.mjs                                  0.00 kB
.output/server/_libs/croner.mjs                                          0.00 kB
.output/server/_libs/ufo.mjs                                             0.00 kB
.output/server/_libs/ohash.mjs                                           0.00 kB
.output/server/_libs/ocache.mjs                                          0.00 kB
.output/server/_libs/tanstack__store.mjs                                 0.00 kB
.output/server/_libs/tanstack__react-store.mjs                           0.02 kB
.output/server/_libs/@radix-ui/react-use-effect-event+[...].mjs          0.02 kB
.output/server/_libs/crossws.mjs                                         0.03 kB
.output/server/_ssr/empty-plugin-adapters-BFgPZ6_d.mjs                   0.13 kB
.output/server/_libs/get-nonce.mjs                                       0.16 kB
.output/server/_libs/@radix-ui/react-use-layout-effect+[...].mjs         0.17 kB
.output/server/_libs/srvx.mjs                                            0.20 kB
.output/server/_libs/rou3.mjs                                            0.21 kB
.output/server/_libs/@radix-ui/react-use-callback-ref+[...].mjs          0.34 kB
.output/server/_libs/radix-ui__primitive.mjs                             0.35 kB
.output/server/_ssr/capture-hdlc17Rz.mjs                                 0.45 kB
.output/server/_libs/tanstack__react-query.mjs                           0.46 kB
.output/server/_ssr/settings-CoEIMid7.mjs                                0.51 kB
.output/server/_ssr/mock-session-Bim5X3GX.mjs                            0.51 kB
.output/server/_libs/clsx.mjs                                            0.51 kB
.output/server/_libs/radix-ui__react-id.mjs                              0.51 kB
.output/server/_chunks/ssr-renderer.mjs                                  0.53 kB
.output/server/_libs/@radix-ui/react-use-escape-keydown+[...].mjs        0.69 kB
.output/server/_libs/tanstack__zod-adapter.mjs                           0.75 kB
.output/server/_ssr/Stamp-DZ6-xlKf.mjs                                   0.83 kB
.output/server/_libs/radix-ui__react-portal.mjs                          0.83 kB
.output/server/_libs/radix-ui__react-focus-guards.mjs                    0.93 kB
.output/server/_libs/radix-ui__react-compose-refs.mjs                    0.96 kB
.output/server/_libs/radix-ui__react-primitive.mjs                       1.12 kB
.output/server/_ssr/capture.image-CQUVDYJR.mjs                           1.16 kB
.output/server/_libs/hookable.mjs                                        1.18 kB
.output/server/_libs/tslib.mjs                                           1.34 kB
.output/server/_libs/cookie-es.mjs                                       1.46 kB
.output/server/_libs/react-style-singleton.mjs                           1.67 kB
.output/server/_libs/use-callback-ref.mjs                                1.75 kB
.output/server/_ssr/ThemeToggle-1p4VzRLZ.mjs                             1.80 kB
.output/server/_ssr/start-DxrNY2Pg.mjs                                   1.84 kB
.output/server/_libs/tanstack__history.mjs                               2.04 kB
.output/server/_libs/class-variance-authority.mjs                        2.14 kB
.output/server/_libs/@radix-ui/react-use-controllable-state+[...].mjs    2.29 kB
.output/server/_ssr/GoogleSignIn-DEKqH6Vb.mjs                            2.45 kB
.output/server/_libs/isbot.mjs                                           2.56 kB
.output/server/_libs/use-sidecar.mjs                                     2.66 kB
.output/server/_libs/seroval-plugins.mjs                                 2.96 kB
.output/server/_ssr/capture.index-1aHUpwPH.mjs                           3.07 kB
.output/server/_libs/radix-ui__react-context.mjs                         3.36 kB
.output/server/_ssr/index.mjs                                            3.86 kB
.output/server/_libs/react-remove-scroll-bar.mjs                         3.96 kB
.output/server/_libs/aria-hidden.mjs                                     4.06 kB
.output/server/_libs/radix-ui__react-presence.mjs                        4.86 kB
.output/server/_tanstack-start-manifest_v-DXv92hHk.mjs                   5.57 kB
.output/server/_ssr/mock-gallery-media-D8KbnjFX.mjs                      5.75 kB
.output/server/_ssr/forgot-password-D33FtRaD.mjs                         6.38 kB
.output/server/_libs/unenv.mjs                                           6.60 kB
.output/server/_libs/radix-ui__react-focus-scope.mjs                     8.07 kB
.output/server/_ssr/privacy-DWHArCMl.mjs                                 8.93 kB
.output/server/_libs/@radix-ui/react-dismissable-layer+[...].mjs         8.97 kB
.output/server/_ssr/mock-data-Cd2feQ2T.mjs                               8.99 kB
.output/server/_ssr/SiteFooter-B61eSrHd.mjs                              9.19 kB
.output/server/_ssr/onboarding-VEKPZ03c.mjs                              9.63 kB
.output/server/_libs/h3-v2.mjs                                          10.00 kB
.output/server/_ssr/subscribe-t_iXh4dm.mjs                              10.25 kB
.output/server/_libs/radix-ui__react-slot.mjs                           11.58 kB
.output/server/_ssr/index-OiX3OSYk.mjs                                  12.72 kB
.output/server/_ssr/tutorials-CX0HznpC.mjs                              13.35 kB
.output/server/_libs/react-remove-scroll.mjs                            13.39 kB
.output/server/_ssr/capture.manual-FRtV0Gzn.mjs                         13.41 kB
.output/server/_libs/radix-ui__react-dialog.mjs                         13.47 kB
.output/server/_ssr/dashboard-Ci4R1svs.mjs                              14.19 kB
.output/server/_libs/h3.mjs                                             14.34 kB
.output/server/_app-ByP-soIy.mjs                                        14.78 kB
.output/server/_libs/lucide-react.mjs                                   15.55 kB
.output/server/_ssr/login-z10axFW2.mjs                                  15.56 kB
.output/server/_ssr/gallery-BwXan2v9.mjs                                16.12 kB
.output/server/_ssr/export-FzJy86qA.mjs                                 16.22 kB
.output/server/_ssr/register-BEIJ81k0.mjs                               17.80 kB
.output/server/_libs/react.mjs                                          20.03 kB
.output/server/_ssr/expenses-Wzh-FF9V.mjs                               23.16 kB
.output/server/index.mjs                                                29.30 kB
.output/server/_ssr/router-Cush_Qeb.mjs                                 32.30 kB
.output/server/_libs/tanstack__react-router.mjs                         36.33 kB
.output/server/_libs/tanstack__query-core.mjs                           54.58 kB
.output/server/_libs/sonner.mjs                                         56.10 kB
.output/server/_ssr/server-wcqgOGZo.mjs                                 68.31 kB
.output/server/_libs/tailwind-merge.mjs                                 96.41 kB
.output/server/_libs/seroval.mjs                                        97.38 kB
.output/server/_libs/zod.mjs                                           132.32 kB
.output/server/_libs/tanstack__router-core.mjs                         180.20 kB
.output/server/_libs/react-dom.mjs                                     499.25 kB
✓ built in 2.42s
[nitro] close: 0.13ms

[nitro]  WARN  [cloudflare] Wrangler config main is overridden and will be ignored.

ℹ Generated .output/server/wrangler.json
ℹ Generated .wrangler/deploy/config.json
ℹ Generated .output/public/_headers
[nitro] compiled: 3.3ms
ℹ Generated .output/nitro.json

[nitro] ✔ You can preview this build using npx vite preview
[nitro] ✔ You can deploy this build using npx nitro deploy --prebuilt

exit=0
```

</details>

<details>
<summary>Step 1 — ESLint</summary>

```text

> lint
> eslint .


/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/slip/Stamp.tsx
  28:17  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/badge.tsx
  32:17  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/button.tsx
  49:18  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/form.tsx
  163:3  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/navigation-menu.tsx
  111:3  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/sidebar.tsx
  743:3  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/ui/toggle.tsx
  42:18  warning  Fast refresh only works when a file only exports components. Use a new file to share constants or functions between components  react-refresh/only-export-components

✖ 7 problems (0 errors, 7 warnings)


exit=0
```

</details>

<details>
<summary>Step 1 — TypeScript</summary>

```text

exit=0
```

</details>

<details>
<summary>Step 1 — baseline bun test from git archive c6d97d2</summary>

```text
bun test v1.4.5 (a7733435c)

src/lib/mock-mode.test.js:
(pass) dummy sign-in > allows the demo account [1.00ms]
(pass) dummy sign-in > does not accept arbitrary account credentials [0.27ms]
(pass) sample receipt data > attached receipts contain the matching supplier and total [8.88ms]
(pass) sample receipt data > pending and missing proof do not invent attachments [0.56ms]

 4 pass
 0 fail
 10 expect() calls
Ran 4 tests across 1 file. [86.00ms]

exit=0
```

</details>

<details>
<summary>Step 1 — current bare bun test</summary>

```text
bun test v1.4.5 (a7733435c)

tests/receipt-capture.spec.ts:

# Unhandled error between tests
-------------------------------
2308 |     this.test = test;
2309 |   }
2310 |   _currentSuite(location, title) {
2311 |     const suite = (0, import_globals2.currentlyLoadingFileSuite)();
2312 |     if (!suite) {
2313 |       throw new Error([
                       ^
error: Playwright Test did not expect test.beforeEach() to be called here.
Most common reasons include:
- You are calling test.beforeEach() in a configuration file.
- You are calling test.beforeEach() in a file that is imported by the configuration file.
- You have two different versions of @playwright/test. This usually happens
  when one of the dependencies in your package.json depends on @playwright/test.
- You are calling test.beforeEach() from an async test.describe() block. Only sync ones are supported.
      at _currentSuite (/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/node_modules/playwright/lib/common/index.js:2313:17)
      at _hook (/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/node_modules/playwright/lib/common/index.js:2401:24)
      at /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/tests/receipt-capture.spec.ts:30:6
-------------------------------


src/lib/mock-mode.test.js:
(pass) dummy sign-in > allows the demo account [0.20ms]
(pass) dummy sign-in > does not accept arbitrary account credentials
(pass) sample receipt data > attached receipts contain the matching supplier and total [0.16ms]
(pass) sample receipt data > pending and missing proof do not invent attachments [0.03ms]

src/components/receipt-capture/images.test.js:
(pass) receipt image validation > accepts JPEG, PNG and WebP signatures, including files without a declared type [0.49ms]
(pass) receipt image validation > reports unsupported formats, disguised PDFs and HEIC and files over 25 MB [4.16ms]
(pass) chooses WebP when the browser encodes it and cleans up the canvas [1.01ms]
(pass) falls back to JPEG when a canvas returns PNG for a WebP request [0.07ms]
(pass) reorders pages without mutating the draft [0.04ms]
(pass) mock upload reports progress, fails per page, and honours abort [12.52ms]

 10 pass
 1 fail
 1 error
 28 expect() calls
Ran 11 tests across 3 files. [316.00ms]

exit=1
```

</details>

<details>
<summary>Step 1 — current bun test src</summary>

```text
bun test v1.4.5 (a7733435c)

src/lib/mock-mode.test.js:
(pass) dummy sign-in > allows the demo account [0.03ms]
(pass) dummy sign-in > does not accept arbitrary account credentials [0.01ms]
(pass) sample receipt data > attached receipts contain the matching supplier and total [0.34ms]
(pass) sample receipt data > pending and missing proof do not invent attachments [0.05ms]

src/components/receipt-capture/images.test.js:
(pass) receipt image validation > accepts JPEG, PNG and WebP signatures, including files without a declared type [0.37ms]
(pass) receipt image validation > reports unsupported formats, disguised PDFs and HEIC and files over 25 MB [4.24ms]
(pass) chooses WebP when the browser encodes it and cleans up the canvas [0.26ms]
(pass) falls back to JPEG when a canvas returns PNG for a WebP request [0.06ms]
(pass) reorders pages without mutating the draft [0.04ms]
(pass) mock upload reports progress, fails per page, and honours abort [11.91ms]

 10 pass
 0 fail
 28 expect() calls
Ran 10 tests across 2 files. [22.00ms]

exit=0
```

</details>

<details>
<summary>Step 4 — Chromium, production mock build, fake camera</summary>

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts


Running 8 tests using 1 worker

(node:99969) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:99969) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✓  1 tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards (4.1s)
  ✓  2 tests/receipt-capture.spec.ts:94:1 › keeps drafts offline, supports cancellation, and retries only the failed page (7.9s)
  ✓  3 tests/receipt-capture.spec.ts:129:1 › renders picker and compression errors and offers native fallback scenarios (1.4s)
  ✓  4 tests/receipt-capture.spec.ts:170:1 › handles a rejected camera permission and remains usable with files (1.2s)
  ✓  5 tests/receipt-capture.spec.ts:185:1 › uses a fake live camera, stops tracks after capture and when leaving the route (4.0s)
  ✓  6 tests/receipt-capture.spec.ts:235:1 › re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata (1.1s)
  ✓  7 tests/receipt-capture.spec.ts:277:1 › degrades to in-memory drafts when browser storage is blocked (1.9s)
  ✓  8 tests/receipt-capture.spec.ts:291:1 › keeps the 375px dark layout usable and audits the empty view (2.2s)

  8 passed (26.2s)

exit=0
```

</details>

<details>
<summary>Step 4 — screenshots and keyboard-only Chromium probe</summary>

```text
SCREENSHOT light empty [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/light-empty.png
SCREENSHOT light camera [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/light-camera.png
SCREENSHOT light preview [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/light-preview.png
SCREENSHOT light error [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/light-error.png
SCREENSHOT light success [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/light-success.png
MOCK STATE FLOW PASS light
SCREENSHOT dark empty [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/dark-empty.png
SCREENSHOT dark camera [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/dark-camera.png
SCREENSHOT dark preview [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/dark-preview.png
SCREENSHOT dark error [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/dark-error.png
SCREENSHOT dark success [ 375, 812 ] /tmp/receipt-capture-audit.xXTqRA/dark-success.png
MOCK STATE FLOW PASS dark
KEYBOARD Take photo tabs 4
KEYBOARD Capture photo tabs 1
KEYBOARD Review page 1 of 1 tabs 4
KEYBOARD Rotate 90° tabs 2
KEYBOARD Escape restored thumbnail focus
KEYBOARD Save tabs 4
KEYBOARD Done tabs 1
KEYBOARD CYCLE PASS camera -> review -> edit -> save -> done
KEYBOARD Take photo tabs 4
KEYBOARD Capture photo tabs 1
KEYBOARD Review page 1 of 1 tabs 4
KEYBOARD Remove tabs 3
FOCUS AFTER REMOVE LAST { tag: 'BODY', text: '' }
LANDSCAPE CAMERA {
  width: 812,
  height: 375,
  horizontalOverflow: false,
  scrollableY: true,
  animation: 'none'
}

exit=0
```

</details>

<details>
<summary>Step 4 — decoded previews, oversized-file state and visibility cleanup</summary>

```text
DECODED PREVIEW light { natural: [ 310, 430 ], displayed: [ 343, 343 ], complete: true }
DECODED PREVIEW dark { natural: [ 310, 430 ], displayed: [ 343, 343 ], complete: true }
FILE TOO LARGE oversized.jpg — This image is larger than 25 MB. Choose a smaller image or take a new photo.
VISIBILITYCHANGE TRACKS [ 'ended' ]

exit=0
```

</details>

<details>
<summary>Step 4 — stale-alert reproduction</summary>

```text
STALE ERROR AFTER SUCCESS {"heading":"Demo save complete","alert":"wrong.pdf — PDFs cannot be added here. Take a photo or choose a JPG, PNG or WebP image."}

exit=0
```

</details>

<details>
<summary>Step 5 — Lighthouse mobile and build chunk sizes</summary>

```text
12.8.2
lighthouse run 1 exit=0
lighthouse run 2 exit=0
lighthouse run 3 exit=0
{"run":1,"score":93,"formFactor":"mobile","throttling":"simulate","url":"http://localhost:4191/capture/image","requested":"http://localhost:4191/capture/image","fcp":2169.8468000000003,"lcp":2959.2702000000004,"si":2169.8468000000003,"tbt":0,"cls":0,"lcpNode":"Use good light, lay the receipt flat and fit it all in the frame. You can add a…","opportunities":[["render-blocking-insight",null,[["http://localhost:4191/assets/styles-4HiGrmUD.css",461]]],["unused-javascript","Est savings of 69 KiB",[["http://localhost:4191/assets/index-BxPuUqdS.js",70620]]],["unused-css-rules","",[]]],"largestRequests":[["http://localhost:4191/assets/index-BxPuUqdS.js",155144],["http://localhost:4191/assets/bricolage-grotesque-latin-wght-normal-DLoelf7F.woff2",41528],["http://localhost:4191/assets/styles-4HiGrmUD.css",20910],["http://localhost:4191/app-icon-192.png",16579],["http://localhost:4191/assets/ibm-plex-mono-latin-400-normal-DMJ8VG8y.woff2",14892]]}
{"run":2,"score":91,"formFactor":"mobile","throttling":"simulate","url":"http://localhost:4191/capture/image","requested":"http://localhost:4191/capture/image","fcp":2346.3410000000003,"lcp":3039.3092,"si":2346.3410000000003,"tbt":0,"cls":0.005256397820869401,"lcpNode":"Use good light, lay the receipt flat and fit it all in the frame. You can add a…","opportunities":[["render-blocking-insight",null,[["http://localhost:4191/assets/styles-4HiGrmUD.css",757]]],["unused-javascript","Est savings of 69 KiB",[["http://localhost:4191/assets/index-BxPuUqdS.js",70620]]],["unused-css-rules","",[]]],"largestRequests":[["http://localhost:4191/assets/index-BxPuUqdS.js",155144],["http://localhost:4191/assets/bricolage-grotesque-latin-wght-normal-DLoelf7F.woff2",41528],["http://localhost:4191/assets/styles-4HiGrmUD.css",20910],["http://localhost:4191/app-icon-192.png",16579],["http://localhost:4191/assets/ibm-plex-mono-latin-400-normal-DMJ8VG8y.woff2",14892]]}
{"run":3,"score":91,"formFactor":"mobile","throttling":"simulate","url":"http://localhost:4191/capture/image","requested":"http://localhost:4191/capture/image","fcp":2364.877875,"lcp":3062.65345,"si":2364.877875,"tbt":0,"cls":0.005256397820869401,"lcpNode":"Use good light, lay the receipt flat and fit it all in the frame. You can add a…","opportunities":[["render-blocking-insight",null,[["http://localhost:4191/assets/styles-4HiGrmUD.css",765]]],["unused-javascript","Est savings of 69 KiB",[["http://localhost:4191/assets/index-BxPuUqdS.js",70620]]],["unused-css-rules","",[]]],"largestRequests":[["http://localhost:4191/assets/index-BxPuUqdS.js",155144],["http://localhost:4191/assets/bricolage-grotesque-latin-wght-normal-DLoelf7F.woff2",41528],["http://localhost:4191/assets/styles-4HiGrmUD.css",20910],["http://localhost:4191/app-icon-192.png",16579],["http://localhost:4191/assets/ibm-plex-mono-latin-400-normal-DMJ8VG8y.woff2",14892]]}

Matching emitted sizes
.output/public/assets/styles-4HiGrmUD.css                                          124.82 kB │ gzip:  20.62 kB
.output/public/assets/capture.image-BqSZsOZ3.js                                      1.47 kB │ gzip:   0.75 kB
.output/public/assets/ReceiptCapture-CowI10-O.js                                    25.69 kB │ gzip:   8.89 kB
.output/public/assets/browser-image-compression-5Xiw9QKB.js                         53.17 kB │ gzip:  21.07 kB
.output/public/assets/index-BxPuUqdS.js                                            493.28 kB │ gzip: 154.51 kB

Assets containing getUserMedia
.output/public/assets/ReceiptCapture-CowI10-O.js
```

</details>

<details>
<summary>Step 5 — SSR preload inspection and gzip sizes</summary>

```text
HTTP 200
SSR fallback: True
SSR interactive inputs: False
/assets/index-BxPuUqdS.js 493280 154110 getUserMedia= False
/assets/_app-D9l2_BsG.js 10493 3353 getUserMedia= False
/assets/mock-data-B3Gxx59r.js 6674 2301 getUserMedia= False
/assets/clock-D8tvubMq.js 165 165 getUserMedia= False
/assets/ThemeToggle-DA2CJ1G8.js 1586 810 getUserMedia= False
/assets/mock-session-D7iUPPfs.js 295 218 getUserMedia= False
/assets/plus-CYygj3sk.js 154 147 getUserMedia= False
/assets/menu-CDks159y.js 190 156 getUserMedia= False
/assets/x-B64-XQHn.js 155 148 getUserMedia= False
/assets/capture-CBPujPvr.js 74 88 getUserMedia= False
/assets/capture.image-BqSZsOZ3.js 1467 745 getUserMedia= False
Total initial JavaScript gzip bytes 162241
```

</details>

The install used `bun install --frozen-lockfile`. The build used `NEXT_PUBLIC_MOCK_MODE=true npm run build`. TypeScript emitted no diagnostics. Early overlapping audit launches produced an occupied-port error and a first-test timeout. The Chromium output above is from the standalone repeat. Lighthouse used `http://localhost:4191/capture/image` with default mobile simulation and three fresh runs. The production server was Wrangler using the generated `.output/server/wrangler.json`.

| Gate | Result | Evidence |
| --- | --- | --- |
| Frozen install | PASS | Exit 0. |
| Production build | PASS | Exit 0 and emitted capture and compression chunks. |
| ESLint | PASS | Exit 0, 0 errors, 7 Fast Refresh warnings. |
| TypeScript | PASS | `tsc --noEmit` exited 0. The project include list covers source and Vite/ESLint config (`web/tsconfig.json:2`). |
| Baseline default test command | PASS | `bun test` at `c6d97d2` ran 4 tests, all passed. |
| Current default test command | FAIL | `bun test` reports 10 passes and 1 failing suite-discovery error because Bun loads the Playwright spec. |
| Current suites with their respective runners | PASS | `bun test src` ran 10 tests, all passed. Chromium Playwright ran 8 tests, all passed on repeat. |
| Test retention | PASS | The baseline test file has no diff. The change added 6 Bun tests and 8 Playwright tests. A scan of tracked test files found no removed, skipped, todo, or commented-out tests. |
| Mobile performance | PASS | Lighthouse 12.8.2 scored 93, 91 and 91. LCP was 2,959–3,063 ms and TBT was 0 ms in all three runs. The LCP node was empty-state guidance. The largest observed transfers were the app entry (155,144 B), Bricolage Grotesque (41,528 B) and CSS (20,910 B). Lighthouse attributed 70,620 B of unused JavaScript to the entry and 461–765 ms of render-blocking delay to the stylesheet. |
| Camera bundle separation | PASS | Vite emitted an initial capture route of 1.47 kB minified and 0.75 kB gzip, a lazy capture chunk of 25.69 kB minified and 8.89 kB gzip, and a separate compression chunk of 53.17 kB minified and 21.07 kB gzip. The shared entry was 493.28 kB minified and 154.51 kB gzip. The SSR preload set totalled 162,241 gzip bytes when measured separately. The only emitted JS file containing `getUserMedia` was the lazy capture chunk. |

### Browser screenshots

All ten images were taken at 375 × 812 in Chromium. Camera screenshots used a fake stream, and preview screenshots waited for JPEG/WebP decoding before capture.

| State | Light | Dark |
| --- | --- | --- |
| Empty | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/light-empty.png) | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/dark-empty.png) |
| Live camera | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/light-camera.png) | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/dark-camera.png) |
| Review, page 2 of 3 | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/light-preview.png) | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/dark-preview.png) |
| Error | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/light-error.png) | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/dark-error.png) |
| Success | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/light-success.png) | [Screenshot](/tmp/receipt-capture-audit.xXTqRA/dark-success.png) |

## 2. Requirement checklist

`ReceiptCapture.tsx`, `ReceiptDialog.tsx`, `camera.ts`, `images.ts`, `service.ts`, `config.ts`, `draft.ts`, `useDraft.ts`, `useReceiptSave.ts` and `images.test.js` are under `web/src/components/receipt-capture/`. `capture.image.tsx` and `capture.index.tsx` are under `web/src/routes/_app/`. `receipt-capture.spec.ts` is under `web/tests/`.

| # | Requirement | Result | File and line | Proving test or output |
| --- | --- | --- | --- | --- |
| A1 | Client-only component, dynamic import and SSR-safe browser access | PASS | `ReceiptCapture.tsx:1,156–167,244–247`, `capture.image.tsx:11–18,45–54,67–80`, `camera.ts:56–95` | SSR returned HTTP 200 with the loading shell and without interactive inputs. Bundle grep located the camera code only in the lazy chunk. |
| A1a | Locked Next.js App Router and `next-themes` integration | FAIL | `Architecture.md:269–275`, `capture.image.tsx:3,20`, `web/src/routes/__root.tsx:109–117`, `web/src/hooks/use-theme.ts:7–22` | Route and theme use TanStack Start and a custom class/local storage bootstrap. Neither Next.js nor `next-themes` appears in `web/package.json`. |
| A2 | Component props and mock/stub service boundary | PASS | `ReceiptCapture.tsx:27–48,123–136`, `config.ts:2–10`, `service.ts:14–28,45–83`, `useReceiptSave.ts:43–71,91–96` | The limit defaults to 10. The callbacks and typed interface exist. The real adapter throws `NotImplemented`. Production capture grep found no fetch/XHR/axios calls. The mock success path ran in Chromium. |
| A3 | Previous interactive capture implementation removed | PASS | `git show c6d97d2:src/routes/_app/capture.image.tsx:24–25,79–109,141–154`, current `capture.image.tsx:11–15,67–80` | The old faux camera and Data URL upload stages are gone. Search found the live camera solely in `camera.ts`. `capture.index.tsx:24–43` only links to routes. `web/src/components/CaptureModesMock.tsx:3,27–31` is an unreferenced static mock. Manual entry remains separate. |
| B4 | Rear camera preference, ideal resolution and constraint fallback | PASS | `camera.ts:81–95`, `ReceiptCapture.tsx:250–268` | The live path asks for ideal environment facing and 8192 × 8192, retries on `OverconstrainedError` with relaxed resolution, and draws a video frame to canvas. The fake stream exercised acquisition and capture. The retry branch was inspected in code. |
| B5 | Release tracks after capture, route change, unmount and hidden tab | PASS | `camera.ts:34–46,56–67,97–100`, `ReceiptCapture.tsx:250–262` | Playwright checks ended tracks after capture, Escape and leaving the route (`receipt-capture.spec.ts:185–232`). A fresh synthetic visibility-change probe returned `['ended']`. Mutation a isolated unmount cleanup. |
| B5a | Device loss and interruption | PASS | `camera.ts:103–116,124–133` | Code handles a track ending and sustained mute. Mock interruption copy is asserted. Physical unplug, iOS interruption and resumption remain unverified. |
| B6 | Automatic fallback for insecure origin, missing API, no device, denial and in-app failure | PASS | `camera.ts:3–12,74–84,103–105,127–133`, `ReceiptCapture.tsx:487–503` | The branches close live camera and reveal the native camera action alongside gallery upload. Playwright verifies each simulated message and a rejected `NotAllowedError` through the real catch branch (`receipt-capture.spec.ts:129–182`). Actual in-app detection is limited to the listed UA patterns. |
| B7 | Two inputs and two equal mobile actions, upload emphasis on desktop | PASS | `ReceiptCapture.tsx:311,371–386,456–481` | The native input has `capture="environment"`; the separate picker is `multiple` with no capture attribute. The 375px empty screenshots show both actions. Fake-camera and native-input injection paths add pages. |
| B8 | Feature-detected torch and zoom and a canvas capture path | PASS | `camera.ts:117–123,137–145`, `ReceiptCapture.tsx:256,681–703` | Rendering checks returned capabilities behind an Android/Chrome UA gate that excludes Samsung, Edge and Android WebView. No ImageCapture calls were found. Physical capability detection remains unverified. |
| C9 | Review before Save, zero disabled and 10-page limit | PASS | `config.ts:2–4`, `ReceiptCapture.tsx:38–41,175–191,354–358,388–436,448–477,582–592`, `useReceiptSave.ts:32–34` | The review is rendered when a page exists. The browser spec validates an empty disabled save and overflow (`receipt-capture.spec.ts:38–43,78–82)). Mutations b and e failed their respective assertions. |
| C10 | Remove, rotate, replace/retake, drag, move buttons, labels and counter | PASS | `ReceiptCapture.tsx:280–303,354–358,393–435,439–454,715–797` | Tests exercise add, open, rotate, label, replacement, drag, move left and remove (`receipt-capture.spec.ts:41–80)). Camera-source Retake is present and enabled at lines 180–182 of the test. Physical touch and a complete retake are unverified. |
| C11 | Confirm discard while there are images | PASS | `ReceiptCapture.tsx:305–308,594–602,802–813` | Playwright presses Escape to keep the draft, then confirms discard and verifies zero images on return (`receipt-capture.spec.ts:83–91`). |
| C12 | Page status, progress, retry, cancellation and partial failure | PASS | `ReceiptCapture.tsx:513–592`, `useReceiptSave.ts:43–97,112–121` | Browser test checks overall and page cancellation, one completed page, failed page retry, no second-page progress on targeted retry, then success (`receipt-capture.spec.ts:94–126`). |
| D13 | Configurable compression, WebP detection, JPEG fallback and MIME type | PASS | `config.ts:2–10`, `images.ts:44–91,114–135`, `useReceiptSave.ts:53–63` | MIME is checked against the actual canvas result and the upload page records the resulting MIME. The Bun test asserts WebP and JPEG output types and qualities. Mutation f failed. The 1.5 MiB setting is a target. |
| D14 | Input formats, actionable HEIC/HEIF/PDF rejection, size and dimensions | PASS | `config.ts:4–7`, `images.ts:12–56` | Bun asserts accepted signatures and rejected types/sizes. Playwright checks PDF, HEIC and GIF UI copy. Fresh probe returned the 25 MB alert. Dimension bounds were verified by code inspection. |
| D15 | Upright EXIF-oriented content and stripped EXIF/GPS | PASS | `images.ts:76–110,114–130`, `receipt-capture.spec.ts:235–274` | The test builds an EXIF-6/GPS JPEG, inspects the actual preview Blob bytes and upright pixel positions. Mutation c preserved the original bytes and failed on the EXIF field. Browser evidence covers Chromium. |
| D16 | Compressed Blob drafts and object-URL lifetimes | PASS | `images.ts:4–10,93–111`, `ReceiptCapture.tsx:52–82,188–205,393–435,722–728` | Both receipt `createObjectURL` sites have matching revocation. A temporary decoding URL is revoked in `finally`; mounted preview/thumbnail URLs are revoked on replacement or unmount. All thumbnails in the scroll strip mount together. Test coverage of revocation failed mutation d. |
| D17 | IndexedDB draft restore/clear and in-memory fallback | PASS | `draft.ts:5–35`, `useDraft.ts:14–58`, `ReceiptCapture.tsx:128–134,305–308` | Browser specs assert restored ordering, label and rotation, clears after discard/save, and blocked-storage fallback (`receipt-capture.spec.ts:59–91,122–126,277–288`). Receipt source grep found no local or session storage calls for images. |
| E1 | Empty, both sources, input guidance | PASS | `ReceiptCapture.tsx:354–386,456–486` | Open a clean draft in mock mode. Both themes captured at 375px. |
| E2 | Camera starting | PASS | `camera.ts:79–95,124–126`, `ReceiptCapture.tsx:664–679` | Select Take photo with the normal mock camera setting. Fake-stream test holds acquisition briefly and asserts Starting camera (`receipt-capture.spec.ts:190–212`). |
| E3 | Intake compression and upload loading | PASS | `ReceiptCapture.tsx:181–196,250–268,505–509,513–544`, `useReceiptSave.ts:53–69` | Pick photos or capture, then Save. Intake status is gated by `busy` and save shows per-page state. The Playwright suite asserts visible progress (`receipt-capture.spec.ts:106–108`); there is no committed assertion of the intake loading message. |
| E4 | Permission denied with recovery, no camera and in-app error | PASS | `camera.ts:3–12,127–133`, `ReceiptCapture.tsx:88–97,487–503,617–632` | Select each camera scenario in Demo controls, then Take photo. Denial gives iPhone/iPad and Android/desktop recovery instructions. The browser spec asserts messages and fallbacks. Device-specific recovery remains unverified. |
| E5 | Unsupported format, too large, limit and compression error | PASS | `images.ts:12–41`, `ReceiptCapture.tsx:175–195,207–217,327–334,441–454,652–661` | Pick unsupported/oversized files, exceed the capacity or select Simulate compression failure and pick an image. Playwright asserts format, limit and compression error. Fresh Chromium probe asserts oversized file UI. |
| E6 | Individual upload failure | PASS | `service.ts:58–65`, `ReceiptCapture.tsx:551–565,634–642` | Check Simulate upload failure, add two images, then Save. Playwright asserts one completed page and a retryable failed page. |
| E7 | Offline, retained draft and retry after reconnect | PASS | `ReceiptCapture.tsx:138,156–167,321–325,554–556,582–592,643–650` | Check Simulate offline with a draft. Save is disabled with an offline banner. Uncheck it to save or retry. The existing browser spec exercises this mock path. Hardware network transitions remain unverified. |
| E8 | Success summary | PASS | `ReceiptCapture.tsx:128–135,336–349` | Save a valid batch with failure simulation off. Both themes reached three-page success. |
| E9 | Clear stale errors on successful completion | FAIL | `ReceiptCapture.tsx:128–135,331–344` | A PDF rejection remained in the alert region next to Demo save complete after a valid page saved. The runtime defect probe reproduced it. |
| F1 | Focus trap, Escape, focus rings, accessible names, image alt and live regions | PASS | `ReceiptDialog.tsx:4–5,25–47`, `ReceiptCapture.tsx:182,217–223,262,280–291,317–334,393–430,529–565,724–727` | The browser spec verifies a trapped Shift+Tab and Escape return, alongside scoped axe scans. An independent Tab/Enter/Escape flow completed capture, review, rotation, save and Done. |
| F2 | Restore meaningful focus after dialog actions | FAIL | `ReceiptCapture.tsx:286–291`, `ReceiptDialog.tsx:34–36` | Removing the final page leaves focus on `BODY`. The handler preferentially stores the Add another button, which disappears when the last image is removed. |
| F3 | 44 px controls, reduced motion and landscape | PASS | `ReceiptDialog.tsx:4–5,28–32`, `ReceiptCapture.tsx:610–657,691–703`, `web/src/styles.css:232–235` | Buttons use 44px minimum boxes and the demo checkbox labels provide 44px-high click areas. Under reduced motion, the 812 × 375 Chromium camera modal reported no horizontal overflow and `animation: 'none'`. Physical safe areas remain unverified. |
| G | Claims and privacy copy | PASS | `ReceiptCapture.tsx:483–485,664–670`, `useReceiptSave.ts:32–65` | Capture strings contained none of the listed legal or accuracy claims. The literal-number grep found only CSS sizing. Permission and camera dialog copy say photos stay on the device until Save. |
| H | `NEXT_PUBLIC_MOCK_MODE` and independent upload-failure simulation | PASS | `capture.image.tsx:17–18,45–54,60–73`, `web/vite.config.ts:12`, `ReceiptCapture.tsx:122–125,634–642`, `service.ts:45–69` | Mock-mode production render worked. The build-time failure flag seeds a local toggle, and the Chromium suite turns it on and off and observes failure then success. The alternate real adapter branch was inspected statically. |
| I | Dependency delta and justification | PASS | `web/package.json:12,52,76–79`, `web/bun.lock:87–90,367–369,728–730,741–743,1043–1048,1177–1179` | Seven lockfile additions are listed below. Browser-side compression is deferred. The additional direct dev packages run the requested Chromium and axe checks. |

<details>
<summary>Step 2 — package sizes, test-file diff and capture-only source scans</summary>

```text
browser-image-compression 2.0.2 MIT 862967 installed bytes
uzip 0.20201231.0 MIT 33966 installed bytes
@axe-core/playwright 4.14.1 MPL-2.0 48901 installed bytes
axe-core 4.14.1 MPL-2.0 3437349 installed bytes
@playwright/test 1.65.1 Apache-2.0 28542 installed bytes
playwright 1.65.1 Apache-2.0 5064151 installed bytes
playwright-core 1.65.1 Apache-2.0 13971424 installed bytes
A	src/components/receipt-capture/images.test.js
A	tests/receipt-capture.spec.ts
capture production grep exit=1
src/components/receipt-capture/ReceiptDialog.tsx:32:              : "fixed z-[61] top-1/2 left-1/2 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-xl bg-slip p-5 text-ink shadow-lg"
src/components/receipt-capture/ReceiptCapture.tsx:71:          ? "mx-auto block aspect-square w-full max-w-[min(100%,55dvh)]"
claims raw grep exit=0
src/components/receipt-capture/images.ts:94:  const url = URL.createObjectURL(blob);
src/components/receipt-capture/images.ts:110:    URL.revokeObjectURL(url);
src/components/receipt-capture/ReceiptCapture.tsx:63:    const next = URL.createObjectURL(image.blob);
src/components/receipt-capture/ReceiptCapture.tsx:65:    return () => URL.revokeObjectURL(next);
baseline skip scan exit=1
current skip scan exit=1
```

The production scan searched for fetch, XMLHttpRequest, axios, local/session storage and ImageCapture. Exit 1 means zero matches. The claim scan matched two CSS sizing expressions. Both test-file skip scans exited 1 after searching for skip, todo and commented-out test calls.

</details>

### Dependency delta

Exact installed file bytes below are summed from each package directory. Browser transfer sizes are reported separately in the build output.

| Added package | Version | Licence | Installed bytes | Observed use |
| --- | --- | --- | ---: | --- |
| `browser-image-compression` | 2.0.2 | MIT | 862,967 | Deferred recompression in `images.ts:114–130`. The emitted chunk is 21.07 kB gzip including `uzip`. |
| `uzip` | 0.20201231.0 | MIT | 33,966 | Compression package transitive dependency. |
| `@axe-core/playwright` | 4.14.1 | MPL-2.0 | 48,901 | Axe runner used at `receipt-capture.spec.ts:2,25–27`. |
| `axe-core` | 4.14.1 | MPL-2.0 | 3,437,349 | Axe runner transitive dependency. |
| `@playwright/test` | 1.65.1 | Apache-2.0 | 28,542 | Browser tests and configuration. |
| `playwright` | 1.65.1 | Apache-2.0 | 5,064,151 | Playwright test runner transitive dependency. |
| `playwright-core` | 1.65.1 | Apache-2.0 | 13,971,424 | Playwright browser driver and axe peer dependency. |

The three direct dependency changes are compression, Playwright and axe. The four other additions are transitive. The emitted initial and deferred application assets contain the compression library and application code. The independent browser-test imports are confined to the Playwright configuration and tests.

## 3. Test quality and mutation results

| Check | Result | Proving assertion or missing coverage |
| --- | --- | --- |
| File add, remove, reorder and 10-page cap | PASS | `receipt-capture.spec.ts:41–82` asserts count, changed page number, persisted label order, drag and overflow. Mutation b changes actual capacity while leaving visible denominator text unchanged. |
| Type, maximum size, HEIC/HEIF and PDF | PASS | `images.test.js:8–24` passes crafted byte signatures and asserts thrown messages, including oversized input. |
| WebP to JPEG | PASS | `images.test.js:41–56` uses an encoder double that returns PNG for a WebP request. It asserts the final Blob's JPEG MIME and both requested qualities. Mutation f kills this test. |
| EXIF and GPS removal, orientation | PASS | `receipt-capture.spec.ts:235–274` injects an orientation tag and GPS directory into a real JPEG. The test reads output bytes via the preview Blob URL and checks dimensions and orientation colours. Mutation c demonstrates EXIF is tested independently of browser orientation. |
| Draft storage, restore, clear and blocked-storage fallback | PASS | `receipt-capture.spec.ts:59–91,122–126,277–288` asserts retained edits after reload, absent draft after clearing, and retained in-memory images after an IndexedDB error. |
| Permission-denied native picker fallback | PASS | `receipt-capture.spec.ts:170–182` rejects `getUserMedia` with a DOMException and opens the actual file chooser path. It verifies one restored camera-source page and the Retake action. |
| Track stop on unmount | PASS | `receipt-capture.spec.ts:185–232` observes real fake-stream track state. Mutation a leaves the third track live and fails. |
| Hidden-tab track stop in the committed suite | FAIL (MISSING) | The camera spec ends tracks by capture, Escape and route navigation. The audit's separate `VISIBILITYCHANGE TRACKS` probe passed. |
| Empty, starting, uploading, picker errors, simulated compression failure, partial failure, offline, success | PASS | `receipt-capture.spec.ts:35–44,94–167,185–213,291–297` checks user-visible text, progress, or actions for these states. |
| Visible intake compression and oversized-error state in the committed browser suite | FAIL (MISSING) | No `expect` matches the intake loading or 25 MB message in `receipt-capture.spec.ts`. The size rule is unit-tested. The audit independently reached the oversized error view. |
| Axe on major capture views | PASS | `receipt-capture.spec.ts:25–27,38,44,54,84,108,117,124,213,297` asserts an empty violations list for empty, populated review, review modal, discard, upload/partial failure, success, camera and dark empty. Scan selectors are scoped to the capture section or dialog. |
| URL release | FAIL (MISSING) | Both production revocation calls were removed in mutation d and all 8 browser tests passed. |
| Test assertion quality | PASS | The encoder test checks Blob MIME as well as arguments. The mocked service test checks failure, progress sequences and abort (`images.test.js:65–80`). Browser tests inspect state, tracks and Blob content. No test without an assertion or relying solely on a mock call assertion was found. |

Mutations ran one at a time in a detached web worktree. Each case ran on the original commit with that case's diff only. After each test run, `git restore --source=HEAD` reverted affected source and the generated route tree. The sixth case used the focused Bun unit file directly.

| Mutation | Result | Focused result |
| --- | --- | --- |
| a. Remove the unmount cleanup call to `stop()` | PASS | Exit 1. `receipt-capture.spec.ts:232` expected the third track to be `ended`, received `live`. |
| b. Allow one extra input page | PASS | Exit 1. `receipt-capture.spec.ts:79` could not find `10 of 10` after the overflowing batch. |
| c. Bypass canvas re-encode for unrotated input | PASS | Exit 1. `receipt-capture.spec.ts:272` expected `exif: false`, received `exif: true`. |
| d. Remove both `URL.revokeObjectURL` calls | FAIL | Exit 0. All 8 browser tests passed with both calls removed. |
| e. Enable the Save control with zero images | PASS | Exit 1. `receipt-capture.spec.ts:39` expected disabled, received enabled. |
| f. Remove the JPEG fallback | PASS | Exit 1. `images.test.js:51` threw “Image compression failed” instead of returning JPEG. Five other unit tests passed. |

<details>
<summary>Mutation diffs and exact focused test output</summary>

`mutation-a.diff`

```text
diff --git a/src/components/receipt-capture/camera.ts b/src/components/receipt-capture/camera.ts
index e8f20bc..d3c3707 100644
--- a/src/components/receipt-capture/camera.ts
+++ b/src/components/receipt-capture/camera.ts
@@ -60,7 +60,6 @@ export function useReceiptCamera(simulateIssue?: CameraIssue) {
     window.addEventListener("pagehide", close);
     document.addEventListener("visibilitychange", visibility);
     return () => {
-      stop();
       window.removeEventListener("pagehide", close);
       document.removeEventListener("visibilitychange", visibility);
     };
```

`mutation-a.log`

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts --grep stops tracks


Running 1 test using 1 worker

(node:4956) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:4956) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✘  1 tests/receipt-capture.spec.ts:185:1 › uses a fake live camera, stops tracks after capture and when leaving the route (9.9s)


  1) tests/receipt-capture.spec.ts:185:1 › uses a fake live camera, stops tracks after capture and when leaving the route 

    Error: expect(received).toEqual(expected) // deep equality

    - Expected  - 1
    + Received  + 1

      Array [
        "ended",
        "ended",
    -   "ended",
    +   "live",
      ]

    Call Log:
    - Timeout 5000ms exceeded while waiting on the predicate

      230 |   await page.evaluate(() => dispatchEvent(new PopStateEvent("popstate")));
      231 |   await expect(page.getByRole("heading", { name: "Capture an expense" })).toBeVisible();
    > 232 |   await expect.poll(trackStates).toEqual(["ended", "ended", "ended"]);
          |                                  ^
      233 | });
      234 |
      235 | test("re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata", async ({
        at /private/tmp/receipt-capture-audit.xXTqRA/mutation/tests/receipt-capture.spec.ts:232:34

    Error Context: test-results/receipt-capture-uses-a-fak-eeeea--and-when-leaving-the-route/error-context.md

  1 failed
    tests/receipt-capture.spec.ts:185:1 › uses a fake live camera, stops tracks after capture and when leaving the route
```

`mutation-b.diff`

```text
diff --git a/src/components/receipt-capture/ReceiptCapture.tsx b/src/components/receipt-capture/ReceiptCapture.tsx
index af2cb4d..cc87932 100644
--- a/src/components/receipt-capture/ReceiptCapture.tsx
+++ b/src/components/receipt-capture/ReceiptCapture.tsx
@@ -173,7 +173,7 @@ function CaptureSession({
     preEncoded = false,
   ) {
     if (inProgress.current || !canEdit || !files.length) return;
-    const available = target ? 1 : limit - current.current.length;
+    const available = target ? 1 : limit + 1 - current.current.length;
     if (available <= 0) {
       setError(`You can add up to ${limit} images. Remove a page to add another.`);
       return;
```

`mutation-b.log`

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts --grep reviews, labels


Running 1 test using 1 worker

(node:5516) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:5516) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✘  1 tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards (9.1s)


  1) tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards 

    Error: expect(locator).toBeVisible() failed

    Locator: getByRole('region', { name: 'Receipt photos' }).getByText('10 of 10', { exact: true })
    Expected: visible
    Timeout: 5000ms
    Error: element(s) not found

    Call log:
      - Expect "toBeVisible" getByRole('region', { name: 'Receipt photos' }).getByText('10 of 10', { exact: true }) with timeout 5000ms
      - waiting for getByRole('region', { name: 'Receipt photos' }).getByText('10 of 10', { exact: true })


      77 |
      78 |   await gallery(page).setInputFiles(Array(10).fill(first));
    > 79 |   await expect(receipt(page).getByText("10 of 10", { exact: true })).toBeVisible();
         |                                                                      ^
      80 |   await expect(receipt(page).getByRole("alert")).toContainText("Some files were left out");
      81 |   await page.getByRole("button", { name: "Add another" }).click();
      82 |   await expect(receipt(page).getByRole("alert")).toContainText("Remove a page");
        at /private/tmp/receipt-capture-audit.xXTqRA/mutation/tests/receipt-capture.spec.ts:79:70

    Error Context: test-results/receipt-capture-reviews-la-7b44e-removes-limits-and-discards/error-context.md

  1 failed
    tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards
```

`mutation-c.diff`

```text
diff --git a/src/components/receipt-capture/images.ts b/src/components/receipt-capture/images.ts
index 8bfe0fc..784094e 100644
--- a/src/components/receipt-capture/images.ts
+++ b/src/components/receipt-capture/images.ts
@@ -91,6 +91,7 @@ export async function encodeCanvas(canvas: HTMLCanvasElement) {
 }
 
 export async function normaliseImage(blob: Blob, rotation = 0) {
+  if (rotation === 0) return blob;
   const url = URL.createObjectURL(blob);
   const image = new Image();
   try {
```

`mutation-c.log`

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts --grep strips the EXIF


Running 1 test using 1 worker

(node:6031) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:6031) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✘  1 tests/receipt-capture.spec.ts:235:1 › re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata (1.2s)


  1) tests/receipt-capture.spec.ts:235:1 › re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata 

    Error: expect(received).toMatchObject(expected)

    - Expected  - 1
    + Received  + 1

      Object {
    -   "exif": false,
    +   "exif": true,
        "height": 60,
        "width": 30,
      }

      270 |       };
      271 |     });
    > 272 |   expect(output).toMatchObject({ width: 30, height: 60, exif: false });
          |                  ^
      273 |   expect(output.top[0]).toBeGreaterThan(200);
      274 |   expect(output.bottom[2]).toBeGreaterThan(200);
      275 | });
        at /private/tmp/receipt-capture-audit.xXTqRA/mutation/tests/receipt-capture.spec.ts:272:18

    Error Context: test-results/receipt-capture-re-encodes-90791-EXIF-including-GPS-metadata/error-context.md

  1 failed
    tests/receipt-capture.spec.ts:235:1 › re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata
```

`mutation-d.diff`

```text
diff --git a/src/components/receipt-capture/ReceiptCapture.tsx b/src/components/receipt-capture/ReceiptCapture.tsx
index af2cb4d..1df9c8e 100644
--- a/src/components/receipt-capture/ReceiptCapture.tsx
+++ b/src/components/receipt-capture/ReceiptCapture.tsx
@@ -62,7 +62,6 @@ function PreviewImage({
   useEffect(() => {
     const next = URL.createObjectURL(image.blob);
     setUrl(next);
-    return () => URL.revokeObjectURL(next);
   }, [image.blob]);
   return (
     <span
diff --git a/src/components/receipt-capture/images.ts b/src/components/receipt-capture/images.ts
index 8bfe0fc..7ea3508 100644
--- a/src/components/receipt-capture/images.ts
+++ b/src/components/receipt-capture/images.ts
@@ -107,7 +107,6 @@ export async function normaliseImage(blob: Blob, rotation = 0) {
     );
   } finally {
     image.removeAttribute("src");
-    URL.revokeObjectURL(url);
   }
 }
```

`mutation-d.log`

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts


Running 8 tests using 1 worker

(node:6306) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:6306) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✓  1 tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards (4.4s)
  ✓  2 tests/receipt-capture.spec.ts:94:1 › keeps drafts offline, supports cancellation, and retries only the failed page (7.0s)
  ✓  3 tests/receipt-capture.spec.ts:129:1 › renders picker and compression errors and offers native fallback scenarios (1.4s)
  ✓  4 tests/receipt-capture.spec.ts:170:1 › handles a rejected camera permission and remains usable with files (1.2s)
  ✓  5 tests/receipt-capture.spec.ts:185:1 › uses a fake live camera, stops tracks after capture and when leaving the route (4.0s)
  ✓  6 tests/receipt-capture.spec.ts:235:1 › re-encodes an EXIF-oriented JPEG upright and strips the EXIF including GPS metadata (1.3s)
  ✓  7 tests/receipt-capture.spec.ts:277:1 › degrades to in-memory drafts when browser storage is blocked (1.9s)
  ✓  8 tests/receipt-capture.spec.ts:291:1 › keeps the 375px dark layout usable and audits the empty view (2.2s)

  8 passed (25.4s)
```

`mutation-e.diff`

```text
diff --git a/src/components/receipt-capture/ReceiptCapture.tsx b/src/components/receipt-capture/ReceiptCapture.tsx
index af2cb4d..74cacbd 100644
--- a/src/components/receipt-capture/ReceiptCapture.tsx
+++ b/src/components/receipt-capture/ReceiptCapture.tsx
@@ -582,7 +582,7 @@ function CaptureSession({
                 <button
                   type="button"
                   className={cn(receiptButton, primary)}
-                  disabled={offline || restoring || busy || !images.length}
+                  disabled={offline || restoring || busy}
                   onClick={() => void upload.save(images)}
                 >
                   {upload.locked
```

`mutation-e.log`

```text

> test:receipt
> playwright test tests/receipt-capture.spec.ts --grep reviews, labels


Running 1 test using 1 worker

(node:7145) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
(node:7145) Warning: The 'NO_COLOR' env is ignored due to the 'FORCE_COLOR' env being set.
(Use `node --trace-warnings ...` to show where the warning was created)
  ✘  1 tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards (6.4s)


  1) tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards 

    Error: expect(locator).toBeDisabled() failed

    Locator:  getByRole('button', { name: 'Save', exact: true })
    Expected: disabled
    Received: enabled
    Timeout:  5000ms

    Call log:
      - Expect "toBeDisabled" getByRole('button', { name: 'Save', exact: true }) with timeout 5000ms
      - waiting for getByRole('button', { name: 'Save', exact: true })
        14 × locator resolved to <button type="button" class="min-h-11 min-w-11 rounded-md border px-3 py-2 text-sm font-semibold disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ink border-ink bg-ink text-slip enabled:hover:bg-ink/85">Save</button>
           - unexpected value "enabled"


      37 | }) => {
      38 |   await axe(page);
    > 39 |   await expect(page.getByRole("button", { name: "Save", exact: true })).toBeDisabled();
         |                                                                         ^
      40 |   const first = await fixture(page, "receipt.jpg");
      41 |   const second = await fixture(page, "proof.jpg");
      42 |   await gallery(page).setInputFiles([first, second]);
        at /private/tmp/receipt-capture-audit.xXTqRA/mutation/tests/receipt-capture.spec.ts:39:73

    Error Context: test-results/receipt-capture-reviews-la-7b44e-removes-limits-and-discards/error-context.md

  1 failed
    tests/receipt-capture.spec.ts:35:1 › reviews, labels, reorders, rotates, replaces, restores, removes, limits and discards
```

`mutation-f.diff`

```text
diff --git a/src/components/receipt-capture/images.ts b/src/components/receipt-capture/images.ts
index 8bfe0fc..08c527f 100644
--- a/src/components/receipt-capture/images.ts
+++ b/src/components/receipt-capture/images.ts
@@ -81,8 +81,6 @@ export async function encodeCanvas(canvas: HTMLCanvasElement) {
     // Unsupported encoders return PNG. Only accept the format actually requested.
     const webp = await encode("image/webp", config.webpQuality);
     if (webp?.type === "image/webp") return webp;
-    const jpeg = await encode("image/jpeg", config.jpegQuality);
-    if (jpeg?.type === "image/jpeg") return jpeg;
     throw new Error("Image compression failed. Choose another image or take a new photo.");
   } finally {
     canvas.width = 0;
```

`mutation-f.log`

```text
bun test v1.4.5 (a7733435c)

src/components/receipt-capture/images.test.js:
(pass) receipt image validation > accepts JPEG, PNG and WebP signatures, including files without a declared type [11.85ms]
(pass) receipt image validation > reports unsupported formats, disguised PDFs and HEIC and files over 25 MB [8.88ms]
(pass) chooses WebP when the browser encodes it and cleans up the canvas [2.18ms]
79 |     new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, type, quality));
80 |   try {
81 |     // Unsupported encoders return PNG. Only accept the format actually requested.
82 |     const webp = await encode("image/webp", config.webpQuality);
83 |     if (webp?.type === "image/webp") return webp;
84 |     throw new Error("Image compression failed. Choose another image or take a new photo.");
                   ^
error: Image compression failed. Choose another image or take a new photo.
      at encodeCanvas (/private/tmp/receipt-capture-audit.xXTqRA/mutation/src/components/receipt-capture/images.ts:84:15)
      at async <anonymous> (/private/tmp/receipt-capture-audit.xXTqRA/mutation/src/components/receipt-capture/images.test.js:51:17)
(fail) falls back to JPEG when a canvas returns PNG for a WebP request [1.36ms]
(pass) reorders pages without mutating the draft [0.11ms]
(pass) mock upload reports progress, fails per page, and honours abort [25.82ms]

 5 pass
 1 fail
 16 expect() calls
Ran 6 tests across 1 file. [116.00ms]
```

</details>

<details>
<summary>Mutation results and revert proof</summary>

`mutation-results.log`

```text
case=a build_exit=0
case=a test_exit=1
case=b build_exit=0
case=b test_exit=1
case=c build_exit=0
case=c test_exit=1
case=d build_exit=0
case=d test_exit=0
case=e build_exit=0
case=e test_exit=1
case=f build_exit=0
case=f test_exit=1
```

`mutation-revert.log`

```text
BASELINE

AFTER REVERT a
git diff exit=0

AFTER REVERT b
git diff exit=0

AFTER REVERT c
git diff exit=0

AFTER REVERT d
git diff exit=0

AFTER REVERT e
git diff exit=0

AFTER REVERT f
git diff exit=0

FINAL MUTATION WORKTREE
Not currently on any branch.
nothing to commit, working tree clean
git worktree remove exit=0
```

`final-status-before-report.log`

```text
Root checkout
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
1dc8006
git diff exit=0
Web checkout
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
24e923c
git diff exit=0
/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/.git/modules/web  24e923c [main]
```

</details>

## 4. Contract gaps

The standalone cross-agent service-contract document was unverified. The tracked-path search returned no contract document. `Architecture.md:133–142,246–248` and `Product-Requirements.md:95–105,201–207` are the available references. The former describes a short-lived R2 URL from initialisation, WebP sent directly to R2, and a process call. The real adapter in `service.ts:74–83` contains no gateway requests or wire DTOs.

| Assumption | Result | Observed difference or gap |
| --- | --- | --- |
| Initialise a draft and upload before confirming processing | PASS | The client orchestration follows the reference order. `useReceiptSave.ts:43–71,91–96` calls the service boundary. No live gateway request was exercised. |
| Where the URL originates, how many URLs are issued, expiry and signed headers | UNVERIFIED | Architecture lists a URL returned at initialisation and a short TTL scoped to one object key, content type and size. The client-only interface splits `initializeExpense` and `requestUpload` and models a target as `{url}` (`service.ts:11,14–26`). The mock invents the local `mock:r2/page/` URL. No header, expiry, key or refresh contract was found. |
| Direct PUT and confirmation request/response | UNVERIFIED | Service names suggest PUT and confirmation and the overall flow matches the reference. Gateway method details, bodies, status transitions, process response, authentication, and R2 CORS and signed requests were unavailable. The stub never makes a request. |
| Content types and max sizes | UNVERIFIED | Architecture specifies WebP. Intake permits JPEG/PNG/WebP at 25 MiB. Output is WebP or JPEG, with a 2,048 px edge and 1.5 MiB target (`config.ts:2–10`, `images.ts:75–90,114–135`). The UI records MIME and byte size (`useReceiptSave.ts:57–63`). JPEG acceptance and actual object limits were unverified, and the client can return an output above its target. |
| Page order and field name | UNVERIFIED | Order is represented as one-based `pageNo: index + 1` (`useReceiptSave.ts:45,57–62`). `Companion.Data/CompanionDbContext.cs:172,189` stores `page_no` in extraction results. No gateway numbering base or wire field mapping was found. |
| Receipt and proof labels | UNVERIFIED | The two local values `"Receipt"` and `"Proof of payment"` is a UI hint passed to the mock (`images.ts:3`, `service.ts:5–10`, `ReceiptCapture.tsx:778–793`). The backend extraction model lists `TaxInvoiceOrReceipt / EftConfirmation / CardMachineSlip / Unknown` (`Companion.Data/Models/ExpenseImageResult.cs:18–21`). No gateway label field, allowed values, or mapping was found. |
| Expense identity, client tracking ID, confirmation and idempotency | UNVERIFIED | The interface returns a string and mock invents a `demo-` ID. The product reference describes a client tracking ID and a public GUID externally. The service has no initialisation payload slot for a client tracking ID. Already-uploaded pages and the expense ID live in a view-local ref (`service.ts:12,14–16,50–56,67–69`, `useReceiptSave.ts:21–23,44–47`). Gateway recovery, abandoned uploads and idempotency remain open. |
| Errors, retry codes and progress units | UNVERIFIED | The mock throws ordinary errors with English messages and reports progress numerically on a 0–100 scale (`service.ts:17–21,58–65`). The component uses the message or a catch-all string (`useReceiptSave.ts:75–85,98–105`). No typed gateway error payload, retry group or confirmation error mapping was found. |

The invented shapes are scoped to the client/mock. They are `UploadPage {pageNo, contentType, size, label?}`, `UploadTarget {url}`, `ReceiptCaptureResult {expenseId, pages}`, the demo expense ID and mock R2 URL, local labels, progress values and failure messages. Search found no invented HTTP endpoint in capture production code.

## 5. Defects by impact

### Blocking

1. **FAIL — locked stack.** The feature runs in TanStack Start with a custom class-based theme. The specified Next.js App Router and `next-themes` integration are absent. Evidence in A1a.
2. **FAIL — default frontend test gate.** The previously passing `bun test` command exits 1 on the new suite discovery conflict. The two explicitly scoped runners pass. Evidence in Step 1.
3. **FAIL — dialog focus restoration on last-page removal.** Keyboard focus falls to the document body, away from the active capture controls. Evidence in F2 and the independent keyboard probe.

### Non-blocking

1. **FAIL — URL revocation regression coverage.** The suites remain green while both revocations are removed. The production cleanup calls are present. Evidence in D16 and mutation d.
2. **FAIL — stale validation alert.** A successful save can announce an earlier picker failure on the same screen. [Reproduction screenshot](/tmp/receipt-capture-audit.xXTqRA/success-with-stale-error.png)
3. **FAIL — missing committed state assertions.** Visible intake compression, the oversized-file alert and hidden-tab cleanup have no committed browser assertions. The size rule is unit-tested. The audit independently observed the oversized alert and visibility cleanup. Evidence in Step 3.

## 6. Not verified

| Status | Item | Limit of evidence |
| --- | --- | --- |
| UNVERIFIED | Standalone cross-agent service contract and live gateway/R2 | The tracked-path search returned no matching document. The real adapter throws `NotImplemented: Receipt uploads are not connected in this build.` |
| UNVERIFIED | WebKit and Firefox camera handling and EXIF behaviour | `web/playwright.config.ts:8–12` configures Chromium only. The independent browser probes also used Chromium. |
| UNVERIFIED | Real device permissions, OS native picker, physical camera unplug/mute, hardware torch/zoom, safe areas and actual network switching | Browser checks used fake-media Chromium and test file injection. The installed Home Screen app was not launched. |
| UNVERIFIED | Alternate real-mode runtime bundle and environment-seeded failure startup | The audit built and ran the mock variant and exercised the interactive simulation switch. The alternate build and the env-seeded failure were inspected in code only. |
| UNVERIFIED | Additional camera error catch branches through a real media API | Chromium directly exercised `NotAllowedError`. Other named errors were reached through the mock scenario control and inspected in code. |

## 7. Manual device checklist

Run each row on an HTTPS build. Test **Prompt** by allowing access. Test **Deny/recover** by revoking permission in the browser or OS, confirming fallback and restoring access. Test **Reload** with labels and an edited order. Test **Offline** by trying Save while offline, then reconnecting and retrying. Use an EXIF-rotated JPEG for **Orientation**, and test the manual Rotate control. The iPhone Home Screen row starts from the installed standalone icon.

| Device or browser | Prompt | Camera | Folder picker | Preview | Reorder | Save | Deny/recover | Reload | Offline | Orientation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| iPhone Safari | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| iPhone installed PWA | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| Android Chrome | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| Samsung Internet | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| In-app browser | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| Windows Chrome | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| Windows Edge | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| Windows Firefox | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| macOS Safari | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| macOS Chrome | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |
| ChromeOS Chrome | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE | NOT DONE |

### Final checkout after writing this report

```text
?? docs/receipt-capture-audit.md
Root git diff exit=0
Web git diff exit=0
```
